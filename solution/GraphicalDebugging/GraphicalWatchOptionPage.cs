//------------------------------------------------------------------------------
// <copyright file="GraphicalWatchPackage.cs">
//     Copyright (c) Adam Wulkiewicz.
// </copyright>
//------------------------------------------------------------------------------

using Microsoft.VisualStudio.Shell;
using System;
using System.ComponentModel;

namespace GraphicalDebugging
{
    public class GraphicalWatchOptionPage : DialogPage
    {
        private bool densify = true;
        private bool enableDirs = false;
        private bool enableLabels = false;
        private int imageHeight = 100;
        private int imageWidth = 100;
        private MultiPointDisplayModeValue multiPointDisplayMode = MultiPointDisplayModeValue.Geometry;

        private bool PointPlot_enableLines = false;
        private bool PointPlot_enablePoints = true;

        private bool ValuePlot_enableBars = true;
        private bool ValuePlot_enableLines = false;
        private bool ValuePlot_enablePoints = false;

        private bool Image_maintainAspectRatio = false;

        public enum MultiPointDisplayModeValue
        {
            [Description("Geometry")]
            Geometry,
            [Description("Point Plot")]
            PointPlot
        };

        [Category("Display")]
        [DisplayName("Densify Non-Cartesian Geometries")]
        [Description("Enable/disable densification to reflect curvature of the globe.")]
        public bool Densify
        {
            get { return densify; }
            set { densify = value; }
        }

        [Category("Display")]
        [DisplayName("Enable Directions")]
        [Description("Enable/disable drawing directions of segments.")]
        public bool EnableDirections
        {
            get { return enableDirs; }
            set { enableDirs = value; }
        }

        [Category("Display")]
        [DisplayName("Enable Labels")]
        [Description("Enable/disable drawing labels if applicable (e.g. for Boost.Geometry intersection points).")]
        public bool EnableLabels
        {
            get { return enableLabels; }
            set { enableLabels = value; }
        }

        [Category("Display")]
        [DisplayName("Image Height")]
        [Description("Height of image displayed on the list.")]
        public int ImageHeight
        {
            get { return imageHeight; }
            set { imageHeight = Math.Max(value, 20); }
        }

        [Category("Display")]
        [DisplayName("Image Width")]
        [Description("Width of image displayed on the list.")]
        public int ImageWidth
        {
            get { return imageWidth; }
            set { imageWidth = Math.Max(value, 20); }
        }

        [Category("Display")]
        [DisplayName("MultiPoint Display Mode")]
        [Description("Treat MultiPoints as Geometries or Point Plots.")]
        public MultiPointDisplayModeValue MultiPointDisplayMode
        {
            get { return multiPointDisplayMode; }
            set { multiPointDisplayMode = value; }
        }

        [Category("Image")]
        [DisplayName("Maintain Aspect Ratio")]
        [Description("Maintain aspect ratio of an image.")]
        public bool Image_MaintainAspectRatio
        {
            get { return Image_maintainAspectRatio; }
            set { Image_maintainAspectRatio = value; }
        }

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
