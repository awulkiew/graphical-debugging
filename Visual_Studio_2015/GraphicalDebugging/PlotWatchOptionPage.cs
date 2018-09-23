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
        private bool PointPlot_enableLines = false;
        private bool PointPlot_enablePoints = true;

        private bool ValuePlot_enableBars = true;
        private bool ValuePlot_enableLines = false;
        private bool ValuePlot_enablePoints = false;

        [Category("Point Plot")]
        [DisplayName("Enable Lines")]
        [Description("Enable/disable drawing lines between points.")]
        public bool PointPlot_EnableLines
        {
            get { return PointPlot_enableLines; }
            set { PointPlot_enableLines = value; }
        }

        [Category("Point Plot")]
        [DisplayName("Enable Points")]
        [Description("Enable/disable drawing points.")]
        public bool PointPlot_EnablePoints
        {
            get { return PointPlot_enablePoints; }
            set { PointPlot_enablePoints = value; }
        }

        [Category("Value Plot")]
        [DisplayName("Enable Bars")]
        [Description("Enable/disable drawing bars representing values.")]
        public bool ValuePlot_EnableBars
        {
            get { return ValuePlot_enableBars; }
            set { ValuePlot_enableBars = value; }
        }

        [Category("Value Plot")]
        [DisplayName("Enable Lines")]
        [Description("Enable/disable drawing lines between values.")]
        public bool ValuePlot_EnableLines
        {
            get { return ValuePlot_enableLines; }
            set { ValuePlot_enableLines = value; }
        }

        [Category("Value Plot")]
        [DisplayName("Enable Points")]
        [Description("Enable/disable drawing points representing values.")]
        public bool ValuePlot_EnablePoints
        {
            get { return ValuePlot_enablePoints; }
            set { ValuePlot_enablePoints = value; }
        }

        protected override void OnApply(PageApplyEventArgs e)
        {
            if (!PointPlot_enableLines && !PointPlot_enablePoints)
                PointPlot_enablePoints = true;

            if (!ValuePlot_enableBars && !ValuePlot_enableLines && !ValuePlot_enablePoints)
                ValuePlot_enableBars = true;

            base.OnApply(e);
        }
    }
}
