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
    using EnvDTE80;
    using Microsoft.VisualStudio.Shell;
    using Microsoft.VisualStudio.Utilities;

    using System.Collections.ObjectModel;

    /// <summary>
    /// Interaction logic for GraphicalWatchControl.
    /// </summary>
    public partial class GraphicalWatchControl : UserControl
    {
        private DTE2 m_dte;
        private Debugger m_debugger;
        private DebuggerEvents m_debuggerEvents;

        ObservableCollection<VariableItem> Variables { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="GraphicalWatchControl"/> class.
        /// </summary>
        public GraphicalWatchControl()
        {
            m_dte = (DTE2)ServiceProvider.GlobalProvider.GetService(typeof(DTE));
            m_debugger = m_dte.Debugger;
            m_debuggerEvents = m_dte.Events.DebuggerEvents;
            m_debuggerEvents.OnEnterBreakMode += DebuggerEvents_OnEnterBreakMode;

            Variables = new ObservableCollection<VariableItem>();

            this.InitializeComponent();

            dataGrid.ItemsSource = Variables;

            ResetAt(new VariableItem(), Variables.Count);
        }

        private void VariableItem_NameChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            //e.PropertyName == "Name"

            VariableItem variable = (VariableItem)sender;
            int index = Variables.IndexOf(variable);

            if (variable.Name == null || variable.Name == "")
            {
                Variables.RemoveAt(index);
                return;
            }

            // insert new empty row
            if (index + 1 == Variables.Count)
            {
                ResetAt(new VariableItem(), Variables.Count);
            }

            UpdateItem(index);
        }

        private void ResetAt(VariableItem item, int index)
        {
            ((System.ComponentModel.INotifyPropertyChanged)item).PropertyChanged += VariableItem_NameChanged;
            if (index < Variables.Count)
                Variables.RemoveAt(index);
            Variables.Insert(index, item);
        }

        private void DebuggerEvents_OnEnterBreakMode(dbgEventReason Reason, ref dbgExecutionAction ExecutionAction)
        {
            if (m_debugger.CurrentMode == dbgDebugMode.dbgBreakMode)
            {
                for (int index = 0 ; index < Variables.Count; ++index)
                {
                    UpdateItem(index);
                }
            }
        }

        private void dataGrid_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Delete)
            {
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

        private void UpdateItem(int index)
        {
            VariableItem variable = Variables[index];

            Bitmap bmp = null;
            string type = null;

            if (variable.Name != null && variable.Name != "")
            {
                var expression = m_debugger.GetExpression(variable.Name);
                if (expression.IsValidValue)
                {
                    // create bitmap
                    bmp = new Bitmap(100, 100);

                    Graphics graphics = Graphics.FromImage(bmp);
                    graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    graphics.Clear(System.Drawing.Color.White);

                    if (!ExpressionDrawer.Draw(graphics, m_debugger, variable.Name))
                        bmp = null;

                    type = expression.Type;
                }
            }

            // set new row
            ResetAt(new VariableItem(variable.Name, bmp, type), index);
        }
    }
}