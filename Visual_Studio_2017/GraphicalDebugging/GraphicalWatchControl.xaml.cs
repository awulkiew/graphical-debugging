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

        ObservableCollection<GraphicalItem> Variables { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="GraphicalWatchControl"/> class.
        /// </summary>
        public GraphicalWatchControl()
        {
            ExpressionLoader.DebuggerEvents.OnEnterBreakMode += DebuggerEvents_OnEnterBreakMode;

            Util.Colors.ColorsChanged += Colors_ColorsChanged;

            m_isDataGridEdited = false;

            Variables = new ObservableCollection<GraphicalItem>();

            this.InitializeComponent();

            dataGrid.ItemsSource = Variables;

            ResetAt(new GraphicalItem(), Variables.Count);
        }

        private void Colors_ColorsChanged()
        {
            UpdateItems(false);
        }

        private void GraphicalItem_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            Util.DataGridItemPropertyChanged(
                dataGrid,
                Variables,
                sender as GraphicalItem,
                e.PropertyName,
                delegate (int index) {
                    UpdateItem(true, index);
                },
                delegate (int next_index) {
                    ResetAt(new GraphicalItem(), next_index);
                });
        }

        private void ResetAt(GraphicalItem item, int index)
        {
            ((System.ComponentModel.INotifyPropertyChanged)item).PropertyChanged += GraphicalItem_PropertyChanged;
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

                Util.RemoveDataGridItems(dataGrid,
                                         Variables,
                                         delegate (int selectIndex) {
                                             ResetAt(new GraphicalItem(), selectIndex);
                                         },
                                         delegate (GraphicalItem variable) { },
                                         delegate () { });
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
                    variable.Bmp = null;
                    variable.Type = null;
                    variable.Error = null;
                }

                if (variable.Name != null && variable.Name != "")
                {
                    var expressions = ExpressionLoader.GetExpressions(variable.Name);
                    if (ExpressionLoader.AllValidValues(expressions))
                    {
                        // create bitmap
                        variable.Bmp = new Bitmap(imageWidth, imageHeight);

                        Graphics graphics = Graphics.FromImage(variable.Bmp);
                        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                        graphics.Clear(Util.Colors.ClearColor);

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

                            if (!ExpressionDrawer.Draw(graphics, variable.Drawable, variable.Traits, settings, Util.Colors))
                                variable.Bmp = null;

                            variable.Error = null;
                        }
                        catch (Exception e)
                        {
                            variable.Bmp = null;

                            variable.Error = e.Message;
                        }

                        variable.Type = ExpressionLoader.TypeFromExpressions(expressions);
                    }
                }
            }

            // set new row
            ResetAt(variable.ShallowCopy(), index);
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
