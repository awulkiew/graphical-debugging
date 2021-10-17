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
        public Colors()
        {
            // for safety, m_colors are updated in Update() again
            CreateColors(DarkColorValues);
            m_brightness = 0.5f;

            Update();

            VSColorTheme.ThemeChanged += VSColorTheme_ThemeChanged;
        }

        public delegate void ColorsChangedEventHandler();
        public event ColorsChangedEventHandler ColorsChanged;
        
        private void VSColorTheme_ThemeChanged(ThemeChangedEventArgs e)
        {
            Update();

            ColorsChanged?.Invoke();
        }

        public void Update()
        {
            float brightness = GetBrightness();
            if (brightness == m_brightness)
                return;

            m_brightness = brightness;

            if (m_brightness > 0.45f)
            {
                CreateColors(DarkColorValues);

                ClearColor = Color.White;
                TextColor = Color.Black;
                AabbColor = Color.Black;
                AxisColor = Color.LightGray;
                DrawColor = Color.Black;

                PointColor = Color.Orange;
                BoxColor = Color.Red;
                SegmentColor = Color.YellowGreen;
                NSphereColor = Color.Crimson;
                LinestringColor = Color.ForestGreen;
                RingColor = Color.FromArgb(0xFF, 150, 32, 150);
                PolygonColor = Color.RoyalBlue;
                MultiPointColor = Color.FromArgb(0xFF, 255, 92, 0);
                MultiLinestringColor = Color.DarkGreen;
                MultiPolygonColor = Color.FromArgb(0xFF, 0, 0, 128);
                TurnColor = Color.DarkOrange;
            }
            else
            {
                CreateColors(LightColorValues);

                ClearColor = Color.FromArgb(0xFF, 24, 24, 24);
                TextColor = Color.White;
                AabbColor = Color.White;
                AxisColor = Color.Gray;
                DrawColor = Color.White;

                PointColor = Color.FromArgb(0xFF, 255, 205, 128);
                BoxColor = Color.FromArgb(0xFF, 255, 128, 128);
                SegmentColor = Color.FromArgb(0xFF, 205, 0xFF, 150);
                NSphereColor = Color.FromArgb(0xFF, 238, 138, 158);
                LinestringColor = Color.FromArgb(0xFF, 128, 224, 128);
                MultiLinestringColor = Color.FromArgb(0xFF, 128, 192, 128);
                RingColor = Color.FromArgb(0xFF, 230, 172, 230);
                PolygonColor = Color.FromArgb(0xFF, 160, 180, 245);
                MultiPointColor = Color.FromArgb(0xFF, 255, 172, 128);
                MultiLinestringColor = Color.FromArgb(0xFF, 128, 172, 128);
                MultiPolygonColor = Color.FromArgb(0xFF, 128, 128, 172);
                TurnColor = Color.FromArgb(0xFF, 255, 197, 128);
            }
        }

        private float GetBrightness()
        {
            float result = 0.5f;
            var col = Application.Current.TryFindResource(VsColors.ToolWindowBackgroundKey);
            if (col != null)
                result = Util.ConvertColor((System.Windows.Media.Color)col).GetBrightness();
            return result;
        }

        private static uint[] DarkColorValues = new uint[] {
            0xFFC00000, 0xFF00C000, 0xFF0000C0,
            0xFFC08000, 0xFF00C080, 0xFF8000C0, 0xFFC00080, 0xFF80C000, 0xFF0080C0,
            0xFFC08080, 0xFF80C080, 0xFF8080C0
        };

        private static uint[] LightColorValues = new uint[] {
            0xFFF06060, 0xFF60F060, 0xFF6070F0,
            0xFFF0B060, 0xFF60F0B0, 0xFFB060F0, 0xFFF060B0, 0xFFB0F060, 0xFF60B0F0,
            0xFFF0B0B0, 0xFFB0F0B0, 0xFFB0B0F0
        };

        public static readonly Color Transparent = Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF);

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
        public Color MultiPointColor { get; set; }
        public Color MultiLinestringColor { get; set; }
        public Color MultiPolygonColor { get; set; }
        public Color TurnColor { get; set; }

        private void CreateColors(uint[] colorValues)
        {
            m_colors = new Color[colorValues.Length];
            for (int i = 0; i < colorValues.Length; ++i)
                m_colors[i] = Color.FromArgb((int)colorValues[i]);
        }

        public int Count { get { return m_colors.Length; } }
        public Color this[int i]
        {
            get
            {
                if (i < 0)
                    return Transparent;
                else if (i >= m_colors.Length)
                    return DrawColor;
                else
                    return m_colors[i];                    
            }
        }

        private Color[] m_colors;
        private float m_brightness;
    }
}
