//------------------------------------------------------------------------------
// <copyright file="GraphicalWatchControl.xaml.cs">
//     Copyright (c) Adam Wulkiewicz.
// </copyright>
//------------------------------------------------------------------------------

namespace GraphicalDebugging
{
    using System.Diagnostics.CodeAnalysis;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media.Imaging;
    using System.Windows.Media;
    using System.Drawing;

    using EnvDTE;
    using Microsoft.VisualStudio.PlatformUI;

    using System.Collections.ObjectModel;
    using System;

    /// <summary>
    /// Interaction logic for GraphicalWatchControl.
    /// </summary>
    public partial class GraphicalWatchControl : UserControl
    {
        private bool m_isDataGridEdited;

        private Colors m_colors;

        ObservableCollection<GraphicalItem> Variables { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="GraphicalWatchControl"/> class.
        /// </summary>
        public GraphicalWatchControl()
        {
            ExpressionLoader.DebuggerEvents.OnEnterBreakMode += DebuggerEvents_OnEnterBreakMode;

            VSColorTheme.ThemeChanged += VSColorTheme_ThemeChanged;

            m_isDataGridEdited = false;

            m_colors = new Colors(this);

            Variables = new ObservableCollection<GraphicalItem>();

            this.InitializeComponent();

            dataGrid.ItemsSource = Variables;

            ResetAt(new GraphicalItem(), Variables.Count);
        }

        private void VSColorTheme_ThemeChanged(ThemeChangedEventArgs e)
        {
            m_colors.Update();
            UpdateItems(false);
        }

        private void GraphicalItem_NameChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            //e.PropertyName == "Name"

            GraphicalItem variable = (GraphicalItem)sender;
            int index = Variables.IndexOf(variable);

            if (index < 0 || index >= dataGrid.Items.Count)
                return;

            if (variable.Name == null || variable.Name == "")
            {
                if (index < dataGrid.Items.Count - 1)
                {
                    Variables.RemoveAt(index);
                    if (index > 0)
                    {
                        Util.SelectDataGridItem(dataGrid, index - 1);
                    }
                }
            }
            else
            {
                UpdateItem(true, index);

                int next_index = index + 1;
                // insert new empty row if needed
                if (next_index == Variables.Count)
                {
                    ResetAt(new GraphicalItem(), next_index);
                }
                // select current row, move to next one is automatic
                Util.SelectDataGridItem(dataGrid, index);
            }
        }

        private void ResetAt(GraphicalItem item, int index)
        {
            ((System.ComponentModel.INotifyPropertyChanged)item).PropertyChanged += GraphicalItem_NameChanged;
            if (index < Variables.Count)
                Variables.RemoveAt(index);
            Variables.Insert(index, item);
        }
        
        private void DebuggerEvents_OnEnterBreakMode(dbgEventReason Reason, ref dbgExecutionAction ExecutionAction)
        {
            if (ExpressionLoader.Debugger.CurrentMode == dbgDebugMode.dbgBreakMode)
            {
                UpdateItems(true);
            }
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
                                                        Variables,
                                                        delegate (GraphicalItem variable) { },
                                                        out selectIndex);

                if (removed)
                {
                    if (selectIndex >= 0 && selectIndex == Variables.Count - 1)
                    {
                        ResetAt(new GraphicalItem(), selectIndex);
                    }
                }

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

        private void UpdateItems(bool load)
        {
            for (int index = 0; index < Variables.Count; ++index)
            {
                UpdateItem(load, index);
            }
        }

        private void UpdateItem(bool load, int index)
        {
            GraphicalItem variable = Variables[index];

            Bitmap bmp = null;
            string type = null;

            if (ExpressionLoader.Debugger.CurrentMode == dbgDebugMode.dbgBreakMode)
            {
                if (load)
                {
                    ExpressionLoader.ReloadUserTypes(Util.GetDialogPage<GeneralOptionPage>());
                }

                // Empty color - use default
                ExpressionDrawer.Settings settings = new ExpressionDrawer.Settings();
                settings.densify = true;
                settings.showDir = false;
                settings.showLabels = false;
                settings.showDots = false;
                // Other settings
                int imageWidth = 100;
                int imageHeight = 100;
                bool displayMultiPointsAsPlots = false;
                // Load settings from option page
                GraphicalWatchOptionPage optionPage = Util.GetDialogPage<GraphicalWatchOptionPage>();
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
                    settings.densify = optionPage.Densify;
                    settings.showDir = optionPage.EnableDirections;
                    settings.showLabels = optionPage.EnableLabels;
                    settings.showDots = false;
                    settings.image_maintainAspectRatio = optionPage.Image_MaintainAspectRatio;
                    imageHeight = Math.Max(optionPage.ImageHeight, 20);
                    imageWidth = Math.Max(optionPage.ImageWidth, 20);                    
                    displayMultiPointsAsPlots = optionPage.MultiPointDisplayMode == GraphicalWatchOptionPage.MultiPointDisplayModeValue.PointPlot;
                }

                (dataGrid.Columns[1] as DataGridTemplateColumn).Width = imageWidth + 1;
                dataGrid.RowHeight = imageHeight + 1;

                if (load)
                {
                    variable.Drawable = null;
                    variable.Traits = null;
                }

                if (variable.Name != null && variable.Name != "")
                {
                    var expressions = ExpressionLoader.GetExpressions(variable.Name);
                    if (ExpressionLoader.AllValidValues(expressions))
                    {
                        // create bitmap
                        bmp = new Bitmap(imageWidth, imageHeight);

                        Graphics graphics = Graphics.FromImage(bmp);
                        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                        graphics.Clear(m_colors.ClearColor);

                        try
                        {
                            if (variable.Drawable == null)
                            {
                                Geometry.Traits traits = null;
                                ExpressionDrawer.IDrawable drawable = null;
                                ExpressionLoader.Load(variable.Name, out traits, out drawable);

                                if (drawable != null && displayMultiPointsAsPlots && drawable is ExpressionDrawer.MultiPoint)
                                    drawable = new ExpressionDrawer.PointsContainer(drawable as ExpressionDrawer.MultiPoint);

                                variable.Drawable = drawable;
                                variable.Traits = traits;
                            }
                            else
                            {
                                if (displayMultiPointsAsPlots && variable.Drawable is ExpressionDrawer.MultiPoint)
                                    variable.Drawable = new ExpressionDrawer.PointsContainer(variable.Drawable as ExpressionDrawer.MultiPoint);
                                else if (!displayMultiPointsAsPlots && variable.Drawable is ExpressionDrawer.PointsContainer)
                                    variable.Drawable = (variable.Drawable as ExpressionDrawer.PointsContainer).MultiPoint;
                            }

                            if (!ExpressionDrawer.Draw(graphics, variable.Drawable, variable.Traits, settings, m_colors))
                                bmp = null;
                        }
                        catch (Exception)
                        {
                            bmp = null;
                        }

                        type = ExpressionLoader.TypeFromExpressions(expressions);
                    }
                }
            }

            // set new row
            ResetAt(new GraphicalItem(variable.Drawable, variable.Traits, variable.Name, bmp, type), index);
        }

        private void imageItem_Copy(object sender, RoutedEventArgs e)
        {
            GraphicalItem v = (GraphicalItem)((MenuItem)sender).DataContext;
            if (v.BmpImg != null)
            {
                Clipboard.SetImage(v.BmpImg);
            }
        }

        private void imageItem_Reset(object sender, RoutedEventArgs e)
        {
            GraphicalItem v = (GraphicalItem)((MenuItem)sender).DataContext;
            if (v.BmpImg != null)
            {
                int i = dataGrid.Items.IndexOf(v);
                UpdateItem(false, i);
            }
        }
    }
}
