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
    }
}
