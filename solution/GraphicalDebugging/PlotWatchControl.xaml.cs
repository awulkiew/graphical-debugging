//------------------------------------------------------------------------------
// <copyright file="PlotWatchControl.xaml.cs">
//     Copyright (c) Adam Wulkiewicz.
// </copyright>
//------------------------------------------------------------------------------

namespace GraphicalDebugging
{
    using System;
    using System.Collections.ObjectModel;
    using System.Drawing;
    using System.Reflection;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media.Imaging;

    /// <summary>
    /// Interaction logic for PlotWatchControl.
    /// </summary>
    public partial class PlotWatchControl : UserControl
    {
        readonly Util.IntsPool m_colorIds;

        bool m_isDataGridEdited;

        readonly Bitmap m_emptyBitmap;

        readonly System.Windows.Shapes.Rectangle m_selectionRect = new System.Windows.Shapes.Rectangle();
        readonly System.Windows.Shapes.Line m_mouseVLine = new System.Windows.Shapes.Line();
        readonly System.Windows.Shapes.Line m_mouseHLine = new System.Windows.Shapes.Line();
        readonly TextBlock m_mouseTxt = new TextBlock();
        readonly Geometry.Point m_pointDown = new Geometry.Point(0, 0);
        bool m_mouseDown = false;
        readonly ZoomBox m_zoomBox = new ZoomBox();
        Geometry.Box m_currentBox = null;
        LocalCS m_currentLocalCS = null;

        ObservableCollection<PlotItem> Plots { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PlotWatchControl"/> class.
        /// </summary>
        public PlotWatchControl()
        {
            ExpressionLoader.BreakModeEntered += ExpressionLoader_BreakModeEntered;

            Util.Colors.ColorsChanged += Colors_ColorsChanged;

            m_isDataGridEdited = false;

            m_colorIds = new Util.IntsPool(Util.Colors.Count);

            this.InitializeComponent();

            m_emptyBitmap = new Bitmap(100, 100);
            Graphics graphics = Graphics.FromImage(m_emptyBitmap);
            graphics.Clear(Util.Colors.ClearColor);
            image.Source = Util.BitmapToBitmapImage(m_emptyBitmap);

            m_selectionRect.Width = 0;
            m_selectionRect.Height = 0;
            m_selectionRect.Visibility = Visibility.Hidden;
            System.Windows.Media.Color colR = System.Windows.SystemColors.HighlightColor;
            colR.A = 92;
            m_selectionRect.Fill = new System.Windows.Media.SolidColorBrush(colR);
            System.Windows.Media.Color colL = Util.ConvertColor(Util.Colors.AabbColor);
            colL.A = 128;
            m_mouseVLine.Stroke = new System.Windows.Media.SolidColorBrush(colL);
            //m_mouseVLine.StrokeThickness = 1;
            m_mouseVLine.Visibility = Visibility.Hidden;
            m_mouseHLine.Stroke = new System.Windows.Media.SolidColorBrush(colL);
            //m_mouseHLine.StrokeThickness = 1;
            m_mouseHLine.Visibility = Visibility.Hidden;
            System.Windows.Media.Color colT = Util.ConvertColor(Util.Colors.TextColor);
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

            ResetAt(new PlotItem(), Plots.Count);
        }

        private void Colors_ColorsChanged()
        {
            Graphics graphics = Graphics.FromImage(m_emptyBitmap);
            graphics.Clear(Util.Colors.ClearColor);

            System.Windows.Media.Color colL = Util.ConvertColor(Util.Colors.AabbColor);
            colL.A = 128;
            m_mouseHLine.Stroke = new System.Windows.Media.SolidColorBrush(colL);
            m_mouseVLine.Stroke = new System.Windows.Media.SolidColorBrush(colL);

            System.Windows.Media.Color colT = Util.ConvertColor(Util.Colors.TextColor);
            colL.A = 128;
            m_mouseTxt.Foreground = new System.Windows.Media.SolidColorBrush(colT);

            UpdateItems(false);
        }

        private void PlotItem_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            Util.DataGridItemPropertyChanged(
                dataGrid,
                Plots,
                sender as PlotItem,
                e.PropertyName,
                delegate (int index) {
                    UpdateItems(true, index);
                },
                delegate (int next_index) {
                    ResetAt(new PlotItem(), next_index);
                },
                delegate (PlotItem plot) {
                    m_colorIds.Push(plot.ColorId);
                },
                delegate (int index) {
                    UpdateItems(false);
                });
        }

        private void ResetAt(PlotItem item, int index)
        {
            ((System.ComponentModel.INotifyPropertyChanged)item).PropertyChanged += PlotItem_PropertyChanged;
            if (index < Plots.Count)
                Plots.RemoveAt(index);
            Plots.Insert(index, item);
        }

        private void ExpressionLoader_BreakModeEntered()
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
            if (m_isDataGridEdited)
                return;

            if (e.Key == System.Windows.Input.Key.Delete)
            {
                Util.RemoveDataGridItems(dataGrid, Plots,
                    (int selectIndex) => ResetAt(new PlotItem(), selectIndex),
                    (PlotItem plot) => m_colorIds.Push(plot.ColorId),
                    () => UpdateItems(false));
            }
            else if (e.Key == System.Windows.Input.Key.V && e.KeyboardDevice.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Control))
            {
                Util.PasteDataGridItemFromClipboard(dataGrid, Plots);
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

        private void dataGrid_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Util.DataGridSingleClickHack(e.OriginalSource as DependencyObject);
        }

        private ExpressionDrawer.Settings GetOptions()
        {
            ExpressionDrawer.Settings settings = new ExpressionDrawer.Settings();
            PlotWatchOptionPage optionPage = Util.GetDialogPage<PlotWatchOptionPage>();
            if (optionPage != null)
            {
                if (optionPage.ValuePlot_EnableBars || optionPage.ValuePlot_EnableLines || optionPage.ValuePlot_EnablePoints)
                {
                    settings.valuePlot_enableBars = optionPage.ValuePlot_EnableBars;
                    settings.valuePlot_enableLines = optionPage.ValuePlot_EnableLines;
                    settings.valuePlot_enablePoints = optionPage.ValuePlot_EnablePoints;
                }
                if (optionPage.PointPlot_EnableLines || optionPage.PointPlot_EnablePoints)
                {
                    settings.pointPlot_enableLines = optionPage.PointPlot_EnableLines;
                    settings.pointPlot_enablePoints = optionPage.PointPlot_EnablePoints;
                }
            }
            return settings;
        }

        private void UpdateItems(bool load, int modified_index = -1)
        {
            m_currentBox = null;

            bool imageEmpty = true;
            if (ExpressionLoader.IsBreakMode)
            {
                if (load)
                {
                    ExpressionLoader.ReloadUserTypes(Util.GetDialogPage<GeneralOptionPage>());
                }

                // Load settings from option page
                ExpressionDrawer.Settings referenceSettings = GetOptions();

                // TODO: Names are redundant
                string[] names = new string[Plots.Count];
                ExpressionDrawer.Settings[] settings = new ExpressionDrawer.Settings[Plots.Count];
                bool tryDrawing = false;

                // update the list, gather names and settings
                for (int index = 0; index < Plots.Count; ++index)
                {
                    PlotItem plot = Plots[index];

                    bool updateRequred = modified_index < 0 || modified_index == index;

                    if (updateRequred && load)
                    {
                        plot.Type = null;
                        plot.Drawable = null;
                        plot.Traits = null;
                        plot.Error = null;
                    }

                    if (plot.Name != null && plot.Name != ""
                        && plot.IsEnabled)
                    {
                        var expressions = updateRequred
                                        ? ExpressionLoader.GetExpressions(plot.Name)
                                        : null;

                        if (expressions == null || ExpressionLoader.AllValidValues(expressions))
                        {
                            if (expressions != null)
                                plot.Type = ExpressionLoader.TypeFromExpressions(expressions);

                            names[index] = plot.Name;

                            if (updateRequred && plot.ColorId < 0)
                            {
                                plot.ColorId = m_colorIds.Pull();
                                plot.Color = Util.ConvertColor(Util.Colors[plot.ColorId]);
                            }

                            settings[index] = referenceSettings.CopyColored(plot.Color);

                            tryDrawing = true;
                        }
                        else if (expressions != null)
                        {
                            var errorStr = ExpressionLoader.ErrorFromExpressions(expressions);
                            if (!string.IsNullOrEmpty(errorStr))
                            {
                                plot.Error = errorStr;
                            }
                        }
                    }

                    // set new row
                    if (updateRequred)
                    {
                        ResetAt(plot.ShallowCopy(), index);
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
                        graphics.Clear(Util.Colors.ClearColor);

                        try
                        {
                            ExpressionDrawer.IDrawable[] drawables = new ExpressionDrawer.IDrawable[names.Length];
                            Geometry.Traits[] traits = new Geometry.Traits[names.Length];
                            for (int i = 0; i < names.Length; ++i)
                            {
                                if (Plots[i].Drawable == null
                                    && names[i] != null && names[i] != ""
                                    && Plots[i].IsEnabled)
                                {
                                    try
                                    {
                                        ExpressionLoader.Load(names[i], ExpressionLoader.OnlyMultiPoints,
                                                              out Geometry.Traits t, out ExpressionDrawer.IDrawable d);
                                        if (d != null)
                                        {
                                            if (t != null)
                                                t = new Geometry.Traits(t.Dimension); // force cartesian
                                            d = new ExpressionDrawer.PointsContainer(d as ExpressionDrawer.MultiPoint);
                                        }
                                        else
                                            ExpressionLoader.Load(names[i], ExpressionLoader.OnlyValuesContainers, out t, out d);
                                        Plots[i].Drawable = d;
                                        Plots[i].Traits = t;
                                        Plots[i].Error = null;
                                    }
                                    catch (Exception e)
                                    {
                                        Plots[i].Error = e.Message;
                                    }
                                }
                                drawables[i] = Plots[i].Drawable;
                                traits[i] = Plots[i].Traits;
                            }

                            m_currentBox = ExpressionDrawer.DrawPlots(graphics, drawables, traits, settings, Util.Colors, m_zoomBox);
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

            imageGridContextMenuCopy.IsEnabled = !imageEmpty;
            imageGridContextMenuResetZoom.IsEnabled = !imageEmpty;
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

        private void imageGrid_MouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            System.Windows.Point point = e.GetPosition(imageGrid);
            bool zoomOut = e.Delta < 0;

            double scale = 0.75;
            if (zoomOut)
                scale = 1.0 / scale;

            double leftFrac = point.X / image.ActualWidth;
            double topFrac = point.Y / image.ActualHeight;
            double w = image.ActualWidth * scale;
            double h = image.ActualHeight * scale;
            double l = point.X - leftFrac * w;
            double t = point.Y - topFrac * h;

            // NOTE: Currently it's possible to zoom out further than the default zoom
            m_zoomBox.Zoom(l, t, w, h, image.ActualWidth, image.ActualHeight);

            UpdateItems(false);
        }

        private void colorTextBlock_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            TextBlock textBlock = sender as TextBlock;
            PlotItem geometry = textBlock.DataContext as PlotItem;
            if (geometry != null && geometry.ColorId >= 0)
            {
                var color = (textBlock.Background as System.Windows.Media.SolidColorBrush).Color;
                var newColor = Util.ShowColorDialog(color);
                if (newColor != color)
                {
                    textBlock.Background = new System.Windows.Media.SolidColorBrush(newColor);
                    m_colorIds.Push(geometry.ColorId);
                    geometry.ColorId = int.MaxValue;
                    geometry.Color = newColor;
                    UpdateItems(false); // TODO: pass modified_index?
                }
            }
        }

        private void dataGridContextMenuDelete_Click(object sender, RoutedEventArgs e)
        {
            Util.RemoveDataGridItems(dataGrid, Plots,
                (int selectIndex) => ResetAt(new PlotItem(), selectIndex),
                (PlotItem plot) => m_colorIds.Push(plot.ColorId),
                () => UpdateItems(false));
        }

        private void dataGridContextMenuEnable_Click(object sender, RoutedEventArgs e)
        {
            Util.EnableDataGridItems(dataGrid, Plots,
                (PlotItem plot) => plot.IsEnabled = true);
        }

        private void dataGridContextMenuDisable_Click(object sender, RoutedEventArgs e)
        {
            Util.EnableDataGridItems(dataGrid, Plots,
                (PlotItem plot) => plot.IsEnabled = false);
        }

        public void OnClose()
        {
            Plots.Clear();
        }
    }
}