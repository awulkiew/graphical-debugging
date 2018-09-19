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

        [Category("Graphical Debugging")]
        [DisplayName("Enable Direct Memory Access")]
        [Description("Enable/disable loading data directly from memory of debugged process.")]
        public bool EnableDirectMemoryAccess
        {
            get { return enableDirectMemoryAccess; }
            set { enableDirectMemoryAccess = value; }
        }
    }
}
