//------------------------------------------------------------------------------
// <copyright file="Colors.cs">
//     Copyright (c) Adam Wulkiewicz.
// </copyright>
//------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Drawing;
using System.Windows;

using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Utilities;

namespace GraphicalDebugging
{
    class Colors
    {
        public Colors(FrameworkElement frameworkElement)
        {
            this.frameworkElement = frameworkElement;
            Update();
        }

        public void Update()
        {
            if (m_colors == null)
                m_colors = new List<Color>();
            else
                m_colors.Clear();

            Transparent = Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF);

            if (GetBrightness() > 0.45f)
            {
                ClearColor = Color.White;
                TextColor = Color.Black;
                AabbColor = Color.Black;
                AxisColor = Color.LightGray;
                DrawColor = Color.Black;

                PointColor = Color.FromArgb(0xFF, 255, 165, 0); // Color.Orange;
                BoxColor = Color.Red;
                SegmentColor = Color.YellowGreen;
                NSphereColor = Color.Crimson;
                LinestringColor = Color.Green;
                RingColor = Color.SlateBlue;
                PolygonColor = Color.RoyalBlue;
                TurnColor = Color.DarkOrange;

                foreach (var v in DarkColorValues)
                    m_colors.Add(Color.FromArgb((int)v));
            }
            else
            {
                ClearColor = Color.FromArgb(0xFF, 24, 24, 24);
                TextColor = Color.White;
                AabbColor = Color.White;
                AxisColor = Color.DarkGray;
                DrawColor = Color.White;

                PointColor = Color.FromArgb(0xFF, 255, 205, 128);
                BoxColor = Color.FromArgb(0xFF, 255, 128, 128);
                SegmentColor = Color.FromArgb(0xFF, 205, 0xFF, 150);
                NSphereColor = Color.FromArgb(0xFF, 238, 138, 158);
                LinestringColor = Color.FromArgb(0xFF, 128, 192, 128);
                RingColor = Color.FromArgb(0xFF, 180, 172, 230);
                PolygonColor = Color.FromArgb(0xFF, 160, 180, 245);
                TurnColor = Color.FromArgb(0xFF, 255, 197, 128);

                foreach (var v in LightColorValues)
                    m_colors.Add(Color.FromArgb((int)v));
            }
        }

        private float GetBrightness()
        {
            float result = 0.5f;
            try
            {
                var tmp = (System.Windows.Media.Color)frameworkElement.FindResource(VsColors.ToolWindowBackgroundKey);
                Color baseColor = Util.ConvertColor(tmp);
                result = baseColor.GetBrightness();
            }
            catch (System.Exception) { }
            return result;
        }

        private static System.UInt32[] DarkColorValues = new System.UInt32[] {
            0xFFC00000, 0xFF00C000, 0xFF0000C0,
            0xFFC08000, 0xFF00C080, 0xFF8000C0, 0xFFC00080, 0xFF80C000, 0xFF0080C0,
            0xFFC08080, 0xFF80C080, 0xFF8080C0
        };

        private static System.UInt32[] LightColorValues = new System.UInt32[] {
            0xFFF06060, 0xFF60F060, 0xFF6070F0,
            0xFFF0B060, 0xFF60F0B0, 0xFFB060F0, 0xFFF060B0, 0xFFB0F060, 0xFF60B0F0,
            0xFFF0B0B0, 0xFFB0F0B0, 0xFFB0B0F0
        };

        private FrameworkElement frameworkElement;

        public Color Transparent { get; set; }

        public Color ClearColor { get; set; }
        public Color TextColor { get; set; }
        public Color AabbColor { get; set; }
        public Color AxisColor { get; set; }
        public Color DrawColor { get; set; }

        public Color PointColor { get; set; }
        public Color BoxColor { get; set; }
        public Color SegmentColor { get; set; }
        public Color NSphereColor { get; set; }
        public Color LinestringColor { get; set; }
        public Color RingColor { get; set; }
        public Color PolygonColor { get; set; }
        public Color TurnColor { get; set; }

        public int Count { get { return m_colors.Count; } }
        public Color this[int i]
        {
            get
            {
                if (i < 0)
                    return Transparent;
                else if (i >= m_colors.Count)
                    return DrawColor;
                else
                    return m_colors[i];                    
            }
        }

        private List<Color> m_colors;
    }
}
