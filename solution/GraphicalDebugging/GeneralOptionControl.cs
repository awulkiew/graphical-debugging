using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GraphicalDebugging
{
    public partial class GeneralOptionControl : UserControl
    {
        public GeneralOptionControl()
        {
            InitializeComponent();

            toolTip.SetToolTip(checkBoxMemoryAccess, "Enable/disable loading data directly from memory of debugged process.");

            toolTip.SetToolTip(labelCpp, "Path to XML file defining C++ user types.");
            toolTip.SetToolTip(textBoxCpp, "Path to XML file defining C++ user types.");
            toolTip.SetToolTip(buttonCpp, "Path to XML file defining C++ user types.");

            toolTip.SetToolTip(labelCS, "Path to XML file defining C# user types.");
            toolTip.SetToolTip(textBoxCS, "Path to XML file defining C# user types.");
            toolTip.SetToolTip(buttonCS, "Path to XML file defining C# user types.");
        }

        private void GeneralOptionControl_SizeChanged(object sender, EventArgs e)
        {
            int interiorWidth = groupBoxUserTypes.Width - groupBoxUserTypes.Padding.Right - groupBoxUserTypes.Padding.Left;

            labelCpp.Width = interiorWidth;

            textBoxCpp.Width = interiorWidth - buttonCpp.Width - 10;
            textBoxCS.Width = interiorWidth - buttonCS.Width - 10;

            buttonCpp.Left = groupBoxUserTypes.Width - groupBoxUserTypes.Padding.Right - buttonCpp.Width;
            buttonCS.Left = groupBoxUserTypes.Width - groupBoxUserTypes.Padding.Right - buttonCS.Width;
        }

        public bool EnableDirectMemoryAccess
        {
            get { return checkBoxMemoryAccess.Checked; }
            set { checkBoxMemoryAccess.Checked = value; }
        }

        public string UserTypesPathCpp
        {
            get { return textBoxCpp.Text; }
            set { textBoxCpp.Text = value; }
        }

        public string UserTypesPathCS
        {
            get { return textBoxCS.Text; }
            set { textBoxCS.Text = value; }
        }

        private void buttonCpp_Click(object sender, EventArgs e)
        {
            OpenFileDialog(textBoxCpp);
        }

        private void buttonCS_Click(object sender, EventArgs e)
        {
            OpenFileDialog(textBoxCS);
        }

        private void OpenFileDialog(TextBox textBox)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.InitialDirectory = textBox.Text;
                openFileDialog.Filter = "XML files (*.xml)|*.xml|All files (*.*)|*.*";
                openFileDialog.FilterIndex = 1;
                openFileDialog.RestoreDirectory = true;

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    textBox.Text = openFileDialog.FileName;
                }
            }
        }        
    }
}
