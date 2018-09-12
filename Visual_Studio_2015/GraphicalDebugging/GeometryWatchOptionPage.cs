//------------------------------------------------------------------------------
// <copyright file="GraphicalWatchPackage.cs">
//     Copyright (c) Adam Wulkiewicz.
// </copyright>
//------------------------------------------------------------------------------

using Microsoft.VisualStudio.Shell;
using System.ComponentModel;

namespace GraphicalDebugging
{
    public class GeometryWatchOptionPage : DialogPage
    {
        private bool enableDirs = true;
        private bool enableLabels = true;

        [Category("Graphical Debugging")]
        [DisplayName("Enable Directions")]
        [Description("Enable/disable drawing directions of segments.")]
        public bool EnableDirections
        {
            get { return enableDirs; }
            set { enableDirs = value; }
        }

        [Category("Graphical Debugging")]
        [DisplayName("Enable Labels")]
        [Description("Enable/disable drawing labels if applicable (e.g. for Boost.Geometry intersection points).")]
        public bool EnableLabels
        {
            get { return enableLabels; }
            set { enableLabels = value; }
        }
    }
}
