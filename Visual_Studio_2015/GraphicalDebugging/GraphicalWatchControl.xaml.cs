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

    /// <summary>
    /// Interaction logic for GraphicalWatchControl.
    /// </summary>
    public partial class GraphicalWatchControl : UserControl
    {
        private DTE2 m_dte;
        private Debugger m_debugger;
        private DebuggerEvents m_debuggerEvents;

        /// <summary>
        /// Initializes a new instance of the <see cref="GraphicalWatchControl"/> class.
        /// </summary>
        public GraphicalWatchControl()
        {
            this.InitializeComponent();

            m_dte = (DTE2)ServiceProvider.GlobalProvider.GetService(typeof(DTE));
            m_debugger = m_dte.Debugger;
            m_debuggerEvents = m_dte.Events.DebuggerEvents;
            m_debuggerEvents.OnEnterBreakMode += DebuggerEvents_OnEnterBreakMode;
        }

        private void listView_Initialized(object sender, System.EventArgs e)
        {
            VariableItem var = new VariableItem();
            ((System.ComponentModel.INotifyPropertyChanged)var).PropertyChanged += VariableItem_NameChanged;
            listView.Items.Add(var);
        }

        private void VariableItem_NameChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            //e.PropertyName == "Name"

            VariableItem variable = (VariableItem)sender;
            int index = listView.Items.IndexOf(variable);

            if (variable.Name == null || variable.Name == "")
            {
                listView.Items.RemoveAt(index);
                return;
            }

            // insert new empty row
            if (index + 1 == listView.Items.Count)
            {
                VariableItem empty_variable = new VariableItem();
                ((System.ComponentModel.INotifyPropertyChanged)empty_variable).PropertyChanged += VariableItem_NameChanged;
                listView.Items.Add(empty_variable);
            }

            Bitmap bmp = null;
            string type = null;

            // debugging
            if (m_debugger.CurrentMode == dbgDebugMode.dbgBreakMode)
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
            VariableItem new_variable = new VariableItem(variable.Name, bmp, type);
            ((System.ComponentModel.INotifyPropertyChanged)new_variable).PropertyChanged += VariableItem_NameChanged;
            listView.Items.RemoveAt(index);
            listView.Items.Insert(index, new_variable);
        }

        private void DebuggerEvents_OnEnterBreakMode(dbgEventReason Reason, ref dbgExecutionAction ExecutionAction)
        {
            if (m_debugger.CurrentMode == dbgDebugMode.dbgBreakMode)
            {
                for (int index = 0 ; index < listView.Items.Count; ++index)
                {
                    VariableItem variable = (VariableItem)listView.Items[index];

                    Bitmap bmp = null;
                    string type = null;

                    if ( variable.Name != null && variable.Name != "" )
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
                    VariableItem new_variable = new VariableItem(variable.Name, bmp, type);
                    ((System.ComponentModel.INotifyPropertyChanged)new_variable).PropertyChanged += VariableItem_NameChanged;
                    listView.Items.RemoveAt(index);
                    listView.Items.Insert(index, new_variable);
                }
            }
        }

        private void listView_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Delete)
            {
                int[] indexes = new int[listView.SelectedItems.Count];
                int i = 0;
                foreach (var item in listView.SelectedItems)
                {
                    indexes[i] = listView.Items.IndexOf(item);
                    ++i;
                }
                System.Array.Sort(indexes, delegate (int l, int r) {
                    return -l.CompareTo(r);
                });

                foreach (int index in indexes)
                {
                    if ( index + 1 < listView.Items.Count)
                        listView.Items.RemoveAt(index);
                }
            }
        }
    }
}