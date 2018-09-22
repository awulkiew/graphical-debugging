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

    /// <summary>
    /// Interaction logic for GraphicalWatchControl.
    /// </summary>
    public partial class GraphicalWatchControl : UserControl
    {
        private bool m_isDataGridEdited;

        private Colors m_colors;

        ExpressionDrawer m_expressionDrawer = new ExpressionDrawer();

        ObservableCollection<VariableItem> Variables { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="GraphicalWatchControl"/> class.
        /// </summary>
        public GraphicalWatchControl()
        {
            ExpressionLoader.DebuggerEvents.OnEnterBreakMode += DebuggerEvents_OnEnterBreakMode;

            VSColorTheme.ThemeChanged += VSColorTheme_ThemeChanged;

            m_isDataGridEdited = false;

            m_colors = new Colors(this);

            Variables = new ObservableCollection<VariableItem>();

            this.InitializeComponent();

            dataGrid.ItemsSource = Variables;

            ResetAt(new VariableItem(), Variables.Count);
        }

        private void VSColorTheme_ThemeChanged(ThemeChangedEventArgs e)
        {
            m_colors.Update();
            UpdateItems();
        }

        private void VariableItem_NameChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            //e.PropertyName == "Name"

            VariableItem variable = (VariableItem)sender;
            int index = Variables.IndexOf(variable);

            if (index < 0 || index >= dataGrid.Items.Count)
                return;

            if (variable.Name == null || variable.Name == "")
            {
                if (index < dataGrid.Items.Count - 1)
                {
                    Variables.RemoveAt(index);
                }
            }
            else
            {
                UpdateItem(index);

                // insert new empty row
                int next_index = index + 1;
                if (next_index == Variables.Count)
                {
                    ResetAt(new VariableItem(), Variables.Count);
                    SelectAt(index + 1, true);
                }
                else
                {
                    SelectAt(index + 1);
                }
            }
        }

        private void ResetAt(VariableItem item, int index)
        {
            ((System.ComponentModel.INotifyPropertyChanged)item).PropertyChanged += VariableItem_NameChanged;
            if (index < Variables.Count)
                Variables.RemoveAt(index);
            Variables.Insert(index, item);
        }

        private void SelectAt(int index, bool isNew = false)
        {
            object item = dataGrid.Items[index];

            if (isNew)
            {
                dataGrid.SelectedItem = item;
                dataGrid.ScrollIntoView(item);
                DataGridRow dgrow = (DataGridRow)dataGrid.ItemContainerGenerator.ContainerFromItem(item);
                dgrow.MoveFocus(new System.Windows.Input.TraversalRequest(System.Windows.Input.FocusNavigationDirection.Next));
            }
            else
            {
                dataGrid.SelectedItem = item;
                dataGrid.ScrollIntoView(item);
            }
        }

        private void DebuggerEvents_OnEnterBreakMode(dbgEventReason Reason, ref dbgExecutionAction ExecutionAction)
        {
            if (ExpressionLoader.Debugger.CurrentMode == dbgDebugMode.dbgBreakMode)
            {
                UpdateItems();
            }
        }

        private void dataGrid_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Delete)
            {
                if (m_isDataGridEdited)
                    return;

                int[] indexes = new int[dataGrid.SelectedItems.Count];
                int i = 0;
                foreach (var item in dataGrid.SelectedItems)
                {
                    indexes[i] = dataGrid.Items.IndexOf(item);
                    ++i;
                }
                System.Array.Sort(indexes, delegate (int l, int r) {
                    return -l.CompareTo(r);
                });

                foreach (int index in indexes)
                {
                    if ( index + 1 < Variables.Count)
                        Variables.RemoveAt(index);
                }
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

        private void UpdateItems()
        {
            for (int index = 0; index < Variables.Count; ++index)
            {
                UpdateItem(index);
            }
        }

        private void UpdateItem(int index)
        {
            VariableItem variable = Variables[index];

            Bitmap bmp = null;
            string type = null;

            if (ExpressionLoader.Debugger.CurrentMode == dbgDebugMode.dbgBreakMode)
            {
                // Empty color - use default
                ExpressionDrawer.Settings settings = new ExpressionDrawer.Settings();
                settings.showDir = false;
                settings.showLabels = false;
                // Load settings from option page
                GraphicalWatchOptionPage optionPage = Util.GetDialogPage<GraphicalWatchOptionPage>();
                if (optionPage != null && (optionPage.EnableBars || optionPage.EnableLines || optionPage.EnablePoints))
                {
                    settings.valuePlot_enableBars = optionPage.EnableBars;
                    settings.valuePlot_enableLines = optionPage.EnableLines;
                    settings.valuePlot_enablePoints = optionPage.EnablePoints;
                }

                if (variable.Name != null && variable.Name != "")
                {
                    var expression = ExpressionLoader.Debugger.GetExpression(variable.Name);
                    if (expression.IsValidValue)
                    {
                        // create bitmap
                        bmp = new Bitmap(100, 100);

                        Graphics graphics = Graphics.FromImage(bmp);
                        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                        graphics.Clear(m_colors.ClearColor);

                        if (!m_expressionDrawer.Draw(graphics, variable.Name, settings, m_colors))
                            bmp = null;

                        type = expression.Type;
                    }
                }
            }

            // set new row
            ResetAt(new VariableItem(variable.Name, bmp, type), index);
        }

        private void imageItem_Copy(object sender, RoutedEventArgs e)
        {
            VariableItem v = (VariableItem)((MenuItem)sender).DataContext;
            if (v.BmpImg != null)
            {
                Clipboard.SetImage(v.BmpImg);
            }
        }

        private void imageItem_Reset(object sender, RoutedEventArgs e)
        {
            VariableItem v = (VariableItem)((MenuItem)sender).DataContext;
            if (v.BmpImg != null)
            {
                int i = dataGrid.Items.IndexOf(v);
                UpdateItem(i);
            }
        }
    }
}
