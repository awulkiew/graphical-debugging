namespace GraphicalDebugging
{
    partial class GeneralOptionControl
    {
        /// <summary> 
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.checkBoxMemoryAccess = new System.Windows.Forms.CheckBox();
            this.groupBoxUserTypes = new System.Windows.Forms.GroupBox();
            this.buttonCpp = new System.Windows.Forms.Button();
            this.buttonCS = new System.Windows.Forms.Button();
            this.textBoxCpp = new System.Windows.Forms.TextBox();
            this.textBoxCS = new System.Windows.Forms.TextBox();
            this.labelCS = new System.Windows.Forms.Label();
            this.labelCpp = new System.Windows.Forms.Label();
            this.toolTip = new System.Windows.Forms.ToolTip(this.components);
            this.groupBoxUserTypes.SuspendLayout();
            this.SuspendLayout();
            // 
            // checkBoxMemoryAccess
            // 
            this.checkBoxMemoryAccess.AutoSize = true;
            this.checkBoxMemoryAccess.Checked = true;
            this.checkBoxMemoryAccess.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBoxMemoryAccess.Dock = System.Windows.Forms.DockStyle.Top;
            this.checkBoxMemoryAccess.Location = new System.Drawing.Point(0, 0);
            this.checkBoxMemoryAccess.Name = "checkBoxMemoryAccess";
            this.checkBoxMemoryAccess.Padding = new System.Windows.Forms.Padding(0, 5, 0, 5);
            this.checkBoxMemoryAccess.Size = new System.Drawing.Size(370, 27);
            this.checkBoxMemoryAccess.TabIndex = 0;
            this.checkBoxMemoryAccess.Text = "Enable Direct Memory Access";
            this.checkBoxMemoryAccess.UseVisualStyleBackColor = true;
            // 
            // groupBoxUserTypes
            // 
            this.groupBoxUserTypes.Controls.Add(this.buttonCpp);
            this.groupBoxUserTypes.Controls.Add(this.buttonCS);
            this.groupBoxUserTypes.Controls.Add(this.textBoxCpp);
            this.groupBoxUserTypes.Controls.Add(this.textBoxCS);
            this.groupBoxUserTypes.Controls.Add(this.labelCS);
            this.groupBoxUserTypes.Controls.Add(this.labelCpp);
            this.groupBoxUserTypes.Dock = System.Windows.Forms.DockStyle.Top;
            this.groupBoxUserTypes.Location = new System.Drawing.Point(0, 27);
            this.groupBoxUserTypes.Name = "groupBoxUserTypes";
            this.groupBoxUserTypes.Padding = new System.Windows.Forms.Padding(5);
            this.groupBoxUserTypes.Size = new System.Drawing.Size(370, 121);
            this.groupBoxUserTypes.TabIndex = 1;
            this.groupBoxUserTypes.TabStop = false;
            this.groupBoxUserTypes.Text = "User Types";
            // 
            // buttonCpp
            // 
            this.buttonCpp.Location = new System.Drawing.Point(289, 36);
            this.buttonCpp.Name = "buttonCpp";
            this.buttonCpp.Size = new System.Drawing.Size(75, 23);
            this.buttonCpp.TabIndex = 4;
            this.buttonCpp.Text = "Browse...";
            this.buttonCpp.UseVisualStyleBackColor = true;
            this.buttonCpp.Click += new System.EventHandler(this.buttonCpp_Click);
            // 
            // buttonCS
            // 
            this.buttonCS.Location = new System.Drawing.Point(288, 84);
            this.buttonCS.Name = "buttonCS";
            this.buttonCS.Size = new System.Drawing.Size(75, 23);
            this.buttonCS.TabIndex = 7;
            this.buttonCS.Text = "Browse...";
            this.buttonCS.UseVisualStyleBackColor = true;
            this.buttonCS.Click += new System.EventHandler(this.buttonCS_Click);
            // 
            // textBoxCpp
            // 
            this.textBoxCpp.Location = new System.Drawing.Point(5, 38);
            this.textBoxCpp.Name = "textBoxCpp";
            this.textBoxCpp.Size = new System.Drawing.Size(278, 20);
            this.textBoxCpp.TabIndex = 3;
            // 
            // textBoxCS
            // 
            this.textBoxCS.Location = new System.Drawing.Point(5, 86);
            this.textBoxCS.Name = "textBoxCS";
            this.textBoxCS.Size = new System.Drawing.Size(276, 20);
            this.textBoxCS.TabIndex = 6;
            // 
            // labelCS
            // 
            this.labelCS.Location = new System.Drawing.Point(5, 63);
            this.labelCS.Name = "labelCS";
            this.labelCS.Padding = new System.Windows.Forms.Padding(0, 5, 0, 5);
            this.labelCS.Size = new System.Drawing.Size(347, 23);
            this.labelCS.TabIndex = 5;
            this.labelCS.Text = "C#";
            // 
            // labelCpp
            // 
            this.labelCpp.Location = new System.Drawing.Point(5, 15);
            this.labelCpp.Name = "labelCpp";
            this.labelCpp.Padding = new System.Windows.Forms.Padding(0, 5, 0, 5);
            this.labelCpp.Size = new System.Drawing.Size(347, 23);
            this.labelCpp.TabIndex = 2;
            this.labelCpp.Text = "C++";
            // 
            // GeneralOptionControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.groupBoxUserTypes);
            this.Controls.Add(this.checkBoxMemoryAccess);
            this.Name = "GeneralOptionControl";
            this.Size = new System.Drawing.Size(370, 264);
            this.SizeChanged += new System.EventHandler(this.GeneralOptionControl_SizeChanged);
            this.groupBoxUserTypes.ResumeLayout(false);
            this.groupBoxUserTypes.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.CheckBox checkBoxMemoryAccess;
        private System.Windows.Forms.GroupBox groupBoxUserTypes;
        private System.Windows.Forms.TextBox textBoxCpp;
        private System.Windows.Forms.TextBox textBoxCS;
        private System.Windows.Forms.Label labelCS;
        private System.Windows.Forms.Label labelCpp;
        private System.Windows.Forms.Button buttonCpp;
        private System.Windows.Forms.Button buttonCS;
        private System.Windows.Forms.ToolTip toolTip;
    }
}
