//------------------------------------------------------------------------------
// <copyright file="GraphicalWatchPackage.cs">
//     Copyright (c) Adam Wulkiewicz.
// </copyright>
//------------------------------------------------------------------------------

using Microsoft.VisualStudio.Shell;
using System.ComponentModel;

namespace GraphicalDebugging
{
    public class GeneralOptionPage : DialogPage
    {
        private bool enableDirectMemoryAccess = true;
        private string userTypesPathCpp = "";
        private string userTypesPathCS = "";

        public bool isUserTypesPathCppChanged = false;
        public bool isUserTypesPathCSChanged = false;
        public System.DateTime userTypesCppWriteTime = new System.DateTime(0);
        public System.DateTime userTypesCSWriteTime = new System.DateTime(0);

        [Category("Data Access")]
        [DisplayName("Enable Direct Memory Access")]
        [Description("Enable/disable loading data directly from memory of debugged process.")]
        public bool EnableDirectMemoryAccess
        {
            get { return enableDirectMemoryAccess; }
            set { enableDirectMemoryAccess = value; }
        }

        [Category("User Types")]
        [DisplayName("C++")]
        [Description("Path to XML file defining C++ user types.")]
        public string UserTypesPathCpp
        {
            get { return userTypesPathCpp; }
            set { userTypesPathCpp = value; isUserTypesPathCppChanged = true; }
        }

        [Category("User Types")]
        [DisplayName("C#")]
        [Description("Path to XML file defining C# user types.")]
        public string UserTypesPathCS
        {
            get { return userTypesPathCS; }
            set { userTypesPathCS = value; isUserTypesPathCSChanged = true; }
        }

        public GeneralOptionPage()
        {
            control = new GeneralOptionControl();
            control.EnableDirectMemoryAccess = EnableDirectMemoryAccess;
            control.UserTypesPathCpp = UserTypesPathCpp;
            control.UserTypesPathCS = UserTypesPathCS;
        }

        protected override System.Windows.Forms.IWin32Window Window
        {
            get { return control; }
        }

        public override void LoadSettingsFromStorage()
        {
            base.LoadSettingsFromStorage();
            control.EnableDirectMemoryAccess = EnableDirectMemoryAccess;
            control.UserTypesPathCpp = UserTypesPathCpp;
            control.UserTypesPathCS = UserTypesPathCS;
        }
        
        protected override void OnApply(PageApplyEventArgs e)
        {
            if (e.ApplyBehavior == ApplyKind.Apply)
            {
                EnableDirectMemoryAccess = control.EnableDirectMemoryAccess;
                if (UserTypesPathCpp != control.UserTypesPathCpp)
                    UserTypesPathCpp = control.UserTypesPathCpp;
                if (UserTypesPathCS != control.UserTypesPathCS)
                    UserTypesPathCS = control.UserTypesPathCS;                
            }

            base.OnApply(e);
        }

        GeneralOptionControl control;
    }
}
