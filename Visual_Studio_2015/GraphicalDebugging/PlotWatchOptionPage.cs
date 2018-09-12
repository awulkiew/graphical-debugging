//------------------------------------------------------------------------------
// <copyright file="GraphicalWatchPackage.cs">
//     Copyright (c) Adam Wulkiewicz.
// </copyright>
//------------------------------------------------------------------------------

using Microsoft.VisualStudio.Shell;
using System.ComponentModel;

namespace GraphicalDebugging
{
    public enum PlotWatchPlotType { Bar, Line, Point };

    public class PlotWatchOptionPage : DialogPage
    {
        private PlotWatchPlotType plotType = PlotWatchPlotType.Bar;

        [Category("Graphical Debugging")]
        [DisplayName("Plot Type")]
        [Description("Type of plot representing container of values.")]
        public PlotWatchPlotType PlotType
        {
            get { return plotType; }
            set { plotType = value; }
        }
    }
}
