//------------------------------------------------------------------------------
// <copyright file="GraphicalWatchPackage.cs">
//     Copyright (c) Adam Wulkiewicz.
// </copyright>
//------------------------------------------------------------------------------

using Microsoft.VisualStudio.Shell;
using System.ComponentModel;

namespace GraphicalDebugging
{
    public class PlotWatchOptionPage : DialogPage
    {
        private bool enableBars = true;
        private bool enableLines = false;
        private bool enablePoints = false;

        [Category("Graphical Debugging")]
        [DisplayName("Enable Bars")]
        [Description("Enable/disable drawing bars representing values.")]
        public bool EnableBars
        {
            get { return enableBars; }
            set { enableBars = value; }
        }

        [Category("Graphical Debugging")]
        [DisplayName("Enable Lines")]
        [Description("Enable/disable drawing lines representing values.")]
        public bool EnableLines
        {
            get { return enableLines; }
            set { enableLines = value; }
        }

        [Category("Graphical Debugging")]
        [DisplayName("Enable Points")]
        [Description("Enable/disable drawing points representing values.")]
        public bool EnablePoints
        {
            get { return enablePoints; }
            set { enablePoints = value; }
        }
    }
}
