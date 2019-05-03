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
        private string userTypesPath = "";

        [Category("Data Access")]
        [DisplayName("Enable Direct Memory Access")]
        [Description("Enable/disable loading data directly from memory of debugged process.")]
        public bool EnableDirectMemoryAccess
        {
            get { return enableDirectMemoryAccess; }
            set { enableDirectMemoryAccess = value; }
        }

        [Category("User Types")]
        [DisplayName("Path")]
        [Description("Path to XML file defining user types.")]
        public string UserTypesPath
        {
            get { return userTypesPath; }
            set { userTypesPath = value; }
        }
    }
}
