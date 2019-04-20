//------------------------------------------------------------------------------
// <copyright file="PlotWatchControl.xaml.cs" company="Company">
//     Copyright (c) Company.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace GraphicalDebugging
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Windows;
    using System.Windows.Controls;

    using System.Drawing;
    using System.Drawing.Imaging;
    using System.IO;
    using System.Windows.Media.Imaging;

    using EnvDTE;
    using Microsoft.VisualStudio.PlatformUI;

    using System.Collections.Generic;
    using System.Collections.ObjectModel;

    /// <summary>
    /// Interaction logic for PlotWatchControl.
    /// </summary>
    public partial class PlotWatchControl : UserControl
    {
        Util.IntsPool m_intsPool;

        private bool m_isDataGridEdited;

        Colors m_colors;
        Bitmap m_emptyBitmap;

        System.Windows.Shapes.Rectangle m_selectionRect = new System.Windows.Shapes.Rectangle();
        System.Windows.Shapes.Line m_mouseVLine = new System.Windows.Shapes.Line();
        System.Windows.Shapes.Line m_mouseHLine = new System.Windows.Shapes.Line();
        TextBlock m_mouseTxt = new TextBlock();
        Geometry.Point m_pointDown = new Geometry.Point(0, 0);
        bool m_mouseDown = false;
        ZoomBox m_zoomBox = new ZoomBox();
        Geometry.Box m_currentBox = null;
        LocalCS m_currentLocalCS = null;

        ObservableCollection<PlotItem> Plots { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PlotWatchControl"/> class.
        /// </summary>
        public PlotWatchControl()
        {
            ExpressionLoader.DebuggerEvents.OnEnterBreakMode += DebuggerEvents_OnEnterBreakMode;

            VSColorTheme.ThemeChanged += VSColorTheme_ThemeChanged;

            m_isDataGridEdited = false;

            m_colors = new Colors(this);
            m_intsPool = new Util.IntsPool(m_colors.Count);

            this.InitializeComponent();

            m_emptyBitmap = new Bitmap(100, 100);
            Graphics graphics = Graphics.FromImage(m_emptyBitmap);
            graphics.Clear(m_colors.ClearColor);
            image.Source = Util.BitmapToBitmapImage(m_emptyBitmap);

            m_selectionRect.Width = 0;
            m_selectionRect.Height = 0;
            m_selectionRect.Visibility = Visibility.Hidden;
            System.Windows.Media.Color colR = System.Windows.SystemColors.HighlightColor;
            colR.A = 92;
            m_selectionRect.Fill = new System.Windows.Media.SolidColorBrush(colR);
            System.Windows.Media.Color colL = Util.ConvertColor(m_colors.AabbColor);
            colL.A = 128;
            m_mouseVLine.Stroke = new System.Windows.Media.SolidColorBrush(colL);
            //m_mouseVLine.StrokeThickness = 1;
            m_mouseVLine.Visibility = Visibility.Hidden;
            m_mouseHLine.Stroke = new System.Windows.Media.SolidColorBrush(colL);
            //m_mouseHLine.StrokeThickness = 1;
            m_mouseHLine.Visibility = Visibility.Hidden;
            System.Windows.Media.Color colT = Util.ConvertColor(m_colors.TextColor);
            colL.A = 128;
            m_mouseTxt.Foreground = new System.Windows.Media.SolidColorBrush(colT);
            //m_mouseTxt.FontFamily = new System.Windows.Media.FontFamily("sans-serif");
            //m_mouseTxt.FontSize = 12;
            m_mouseTxt.Visibility = Visibility.Hidden;
            imageCanvas.Children.Add(m_selectionRect);
            imageCanvas.Children.Add(m_mouseHLine);
            imageCanvas.Children.Add(m_mouseVLine);
            imageCanvas.Children.Add(m_mouseTxt);

            Plots = new ObservableCollection<PlotItem>();
            dataGrid.ItemsSource = Plots;

            ResetAt(new PlotItem(-1, m_colors), Plots.Count);
        }

        private void VSColorTheme_ThemeChanged(ThemeChangedEventArgs e)
        {
            m_colors.Update();
            Graphics graphics = Graphics.FromImage(m_emptyBitmap);
            graphics.Clear(m_colors.ClearColor);

            System.Windows.Media.Color colL = Util.ConvertColor(m_colors.AabbColor);
            colL.A = 128;
            m_mouseHLine.Stroke = new System.Windows.Media.SolidColorBrush(colL);
            m_mouseVLine.Stroke = new System.Windows.Media.SolidColorBrush(colL);

            System.Windows.Media.Color colT = Util.ConvertColor(m_colors.TextColor);
            colL.A = 128;
            m_mouseTxt.Foreground = new System.Windows.Media.SolidColorBrush(colT);

            UpdateItems(false);
        }

        private void PlotItem_NameChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            //e.PropertyName == "Name"

            PlotItem plot = (PlotItem)sender;
            int index = Plots.IndexOf(plot);

            if (index < 0 || index >= dataGrid.Items.Count)
                return;

            if (plot.Name == null || plot.Name == "")
            {
                if (index < dataGrid.Items.Count - 1)
                {
                    m_intsPool.Push(plot.ColorId);
                    Plots.RemoveAt(index);
                    UpdateItems(true);
                    if (index > 0)
                    {
                        Util.SelectDataGridItem(dataGrid, index - 1);
                    }
                }
            }
            else
            {
                UpdateItems(true, index);

                int next_index = index + 1;
                // insert new empty row if needed
                if (next_index == Plots.Count)
                {
                    ResetAt(new PlotItem(-1, m_colors), next_index);
                }
                // select current row, move to next one is automatic
                Util.SelectDataGridItem(dataGrid, index);
            }
        }

        private void ResetAt(PlotItem item, int index)
        {
            ((System.ComponentModel.INotifyPropertyChanged)item).PropertyChanged += PlotItem_NameChanged;
            if (index < Plots.Count)
                Plots.RemoveAt(index);
            Plots.Insert(index, item);
        }

        private void DebuggerEvents_OnEnterBreakMode(dbgEventReason Reason, ref dbgExecutionAction ExecutionAction)
        {
            UpdateItems(true);
        }

        private void GridSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            UpdateItems(false);
        }

        private void PlotWatchWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateItems(false);
        }

        private void dataGrid_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Delete)
            {
                if (m_isDataGridEdited)
                    return;

                if (dataGrid.SelectedItems.Count < 1)
                    return;

                int selectIndex = -1;
                bool removed = Util.RemoveDataGridItems(dataGrid,
                                                        Plots,
                                                        delegate (PlotItem plot)
                                                        {
                                                            m_intsPool.Push(plot.ColorId);
                                                        },
                                                        out selectIndex);

                if (removed)
                    UpdateItems(false);

                Util.SelectDataGridItem(dataGrid, selectIndex);
            }
        }

        private void dataGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            m_isDataGridEdited = true;
        }

        private void dataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            m_isDataGridEdited = false;
        }

        private void UpdateItems(bool load, int modified_index = -1)
        {
            m_currentBox = null;

            bool imageEmpty = true;
            if (ExpressionLoader.Debugger.CurrentMode == dbgDebugMode.dbgBreakMode)
            {
                ExpressionDrawer.Settings referenceSettings = new ExpressionDrawer.Settings();
                PlotWatchOptionPage optionPage = Util.GetDialogPage<PlotWatchOptionPage>();
                if (optionPage != null)
                {
                    if (optionPage.ValuePlot_EnableBars || optionPage.ValuePlot_EnableLines || optionPage.ValuePlot_EnablePoints)
                    {
                        referenceSettings.valuePlot_enableBars = optionPage.ValuePlot_EnableBars;
                        referenceSettings.valuePlot_enableLines = optionPage.ValuePlot_EnableLines;
                        referenceSettings.valuePlot_enablePoints = optionPage.ValuePlot_EnablePoints;
                    }
                    if (optionPage.PointPlot_EnableLines || optionPage.PointPlot_EnablePoints)
                    {
                        referenceSettings.pointPlot_enableLines = optionPage.PointPlot_EnableLines;
                        referenceSettings.pointPlot_enablePoints = optionPage.PointPlot_EnablePoints;
                    }
                }

                // TODO: Names are redundant
                string[] names = new string[Plots.Count];
                ExpressionDrawer.Settings[] settings = new ExpressionDrawer.Settings[Plots.Count];
                bool tryDrawing = false;

                // update the list, gather names and settings
                for (int index = 0; index < Plots.Count; ++index)
                {
                    PlotItem geometry = Plots[index];

                    System.Windows.Media.Color color = geometry.Color;
                    int colorId = geometry.ColorId;
                    string type = null;

                    bool updateRequred = modified_index < 0 || modified_index == index;

                    if (updateRequred && load)
                    {
                        geometry.Drawable = null;
                        geometry.Traits = null;
                    }

                    if (geometry.Name != null && geometry.Name != "")
                    {
                        var expressions = updateRequred
                                        ? ExpressionLoader.GetExpressions(geometry.Name)
                                        : null;

                        if (expressions == null || ExpressionLoader.AllValidValues(expressions))
                        {
                            if (expressions != null)
                                type = ExpressionLoader.TypeFromExpressions(expressions);

                            names[index] = geometry.Name;

                            if (updateRequred && geometry.ColorId < 0)
                            {
                                colorId = m_intsPool.Pull();
                                color = Util.ConvertColor(m_colors[colorId]);
                            }

                            settings[index] = referenceSettings.CopyColored(color);

                            tryDrawing = true;
                        }
                    }

                    // set new row
                    if (updateRequred)
                    {
                        ResetAt(new PlotItem(geometry.Drawable, geometry.Traits,
                                             geometry.Name, type, colorId, m_colors),
                                index);
                    }
                }

                // draw variables
                if (tryDrawing)
                {
                    int width = (int)System.Math.Round(image.ActualWidth);
                    int height = (int)System.Math.Round(image.ActualHeight);
                    if (width > 0 && height > 0)
                    {
                        Bitmap bmp = new Bitmap(width, height);

                        Graphics graphics = Graphics.FromImage(bmp);
                        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                        graphics.Clear(m_colors.ClearColor);

                        try
                        {
                            ExpressionDrawer.IDrawable[] drawables = new ExpressionDrawer.IDrawable[names.Length];
                            Geometry.Traits[] traits = new Geometry.Traits[names.Length];
                            for (int i = 0; i < names.Length; ++i)
                            {
                                if (Plots[i].Drawable == null && names[i] != null && names[i] != "")
                                {
                                    ExpressionDrawer.IDrawable d = null;
                                    Geometry.Traits t = null;
                                    ExpressionLoader.Load(names[i], ExpressionLoader.OnlyMultiPoints, out t, out d);
                                    if (d != null)
                                    {
                                        if (t != null)
                                            t = new Geometry.Traits(t.Dimension); // force cartesian
                                        d = new ExpressionDrawer.PointsContainer(d as ExpressionDrawer.MultiPoint);
                                    }
                                    else
                                        ExpressionLoader.Load(names[i], ExpressionLoader.OnlyContainers, out t, out d);
                                    Plots[i].Drawable = d;
                                    Plots[i].Traits = t;
                                }
                                drawables[i] = Plots[i].Drawable;
                                traits[i] = Plots[i].Traits;
                            }

                            m_currentBox = ExpressionDrawer.DrawPlots(graphics, drawables, traits, settings, m_colors, m_zoomBox);
                        }
                        catch (Exception e)
                        {
                            ExpressionDrawer.DrawErrorMessage(graphics, e.Message);
                        }

                        image.Source = Util.BitmapToBitmapImage(bmp);
                        imageEmpty = false;
                    }
                }
            }

            if (imageEmpty)
            {
                image.Source = Util.BitmapToBitmapImage(m_emptyBitmap);
            }

            imageGrid.ContextMenu = new ContextMenu();
            MenuItem mi = new MenuItem();
            mi.Header = "Copy";
            mi.Click += MenuItem_Copy;
            if (imageEmpty)
                mi.IsEnabled = false;
            imageGrid.ContextMenu.Items.Add(mi);
            imageGrid.ContextMenu.Items.Add(new Separator());
            MenuItem mi2 = new MenuItem();
            mi2.Header = "Reset View";
            mi2.Click += MenuItem_ResetZoom;
            if (imageEmpty)
                mi2.IsEnabled = false;
            imageGrid.ContextMenu.Items.Add(mi2);
        }

        private void MenuItem_Copy(object sender, RoutedEventArgs e)
        {
            if (image != null && image.Source != null)
            {
                Clipboard.SetImage((BitmapImage)image.Source);
            }
        }

        private void MenuItem_ResetZoom(object sender, RoutedEventArgs e)
        {
            // Trust the user, always update
            m_zoomBox.Reset();
            UpdateItems(false);
        }

        private void imageGrid_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
            {
                m_mouseDown = true;
                System.Windows.Point point = e.GetPosition(image);
                m_pointDown[0] = point.X;
                m_pointDown[1] = point.Y;
                imageGrid.CaptureMouse();
            }
        }

        private void imageGrid_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            System.Windows.Point point = e.GetPosition(imageGrid);

            m_mouseVLine.X1 = point.X;
            m_mouseVLine.Y1 = 0;
            m_mouseVLine.X2 = point.X;
            m_mouseVLine.Y2 = image.ActualHeight;
            m_mouseVLine.Visibility = Visibility.Visible;
            m_mouseHLine.X1 = 0;
            m_mouseHLine.Y1 = point.Y;
            m_mouseHLine.X2 = image.ActualWidth;
            m_mouseHLine.Y2 = point.Y;
            m_mouseHLine.Visibility = Visibility.Visible;
            if (m_currentBox != null && m_currentBox.IsValid())
            {
                // TODO: pass correct fill parameter later when point plot is implemented
                bool fill = true;
                if (m_currentLocalCS == null)
                    m_currentLocalCS = new LocalCS(m_currentBox, (float)image.ActualWidth, (float)image.ActualHeight, fill);
                else
                    m_currentLocalCS.Reset(m_currentBox, (float)image.ActualWidth, (float)image.ActualHeight, fill);
                m_mouseTxt.Text = "(" + Util.ToString(m_currentLocalCS.InverseConvertX(point.X))
                                + " " + Util.ToString(m_currentLocalCS.InverseConvertY(point.Y))
                                + ")";
                Canvas.SetLeft(m_mouseTxt, point.X + 2);
                Canvas.SetTop(m_mouseTxt, point.Y + 2);
                m_mouseTxt.Visibility = Visibility.Visible;
            }
            else
                m_mouseTxt.Visibility = Visibility.Hidden;

            if (m_mouseDown)
            {
                if (m_pointDown[0] != point.X || m_pointDown[1] != point.Y)
                {
                    double ox = m_pointDown[0];
                    double oy = m_pointDown[1];
                    double x = Math.Min(Math.Max(point.X, 0), image.ActualWidth);
                    double y = Math.Min(Math.Max(point.Y, 0), image.ActualHeight);
                    double w = Math.Abs(x - ox);
                    double h = Math.Abs(y - oy);

                    double prop = h / w;
                    double iProp = image.ActualHeight / image.ActualWidth;
                    if (prop < iProp)
                        h = iProp * w;
                    else if (prop > iProp)
                        w = h / iProp;

                    double l = ox;
                    double t = oy;

                    if (ox <= x)
                    {
                        if (ox + w > image.ActualWidth)
                        {
                            w = image.ActualWidth - ox;
                            h = iProp * w;
                        }
                    }
                    else
                    {
                        if (ox - w < 0)
                        {
                            w = ox;
                            h = iProp * w;
                        }
                        l = ox - w;
                    }

                    if (oy <= y)
                    {
                        if (oy + h > image.ActualHeight)
                        {
                            h = image.ActualHeight - oy;
                            w = h / iProp;
                        }
                    }
                    else
                    {
                        if (oy - h < 0)
                        {
                            h = oy;
                            w = h / iProp;
                        }
                        t = oy - h;
                    }

                    if (w > 0 && h > 0)
                    {
                        Canvas.SetLeft(m_selectionRect, l);
                        Canvas.SetTop(m_selectionRect, t);
                        m_selectionRect.Width = w;
                        m_selectionRect.Height = h;

                        m_selectionRect.Visibility = Visibility.Visible;
                    }
                }
            }
        }

        private void imageGrid_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (m_mouseDown)
            {
                imageGrid.ReleaseMouseCapture();
                m_mouseDown = false;

                // Calculate zoom box only if the region changed
                if (m_selectionRect.Visibility == Visibility.Visible)
                {
                    m_selectionRect.Visibility = Visibility.Hidden;

                    double leftR = Canvas.GetLeft(m_selectionRect);
                    double topR = Canvas.GetTop(m_selectionRect);
                    double wR = m_selectionRect.Width;
                    double hR = m_selectionRect.Height;

                    if (wR > 0 && hR > 0)
                    {
                        m_zoomBox.Zoom(leftR, topR, wR, hR, image.ActualWidth, image.ActualHeight);
                        UpdateItems(false);
                    }
                }
            }
        }

        private void imageGrid_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            m_mouseVLine.Visibility = Visibility.Hidden;
            m_mouseHLine.Visibility = Visibility.Hidden;
            m_mouseTxt.Visibility = Visibility.Hidden;
        }
    }
}