//------------------------------------------------------------------------------
// <copyright file="Util.cs">
//     Copyright (c) Adam Wulkiewicz.
// </copyright>
//------------------------------------------------------------------------------

using System.Collections.Generic;

using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Media.Imaging;
using System.IO;

namespace GraphicalDebugging
{
    class Util
    {
        private Util() {}

        public class Pair<F, S>
        {
            public Pair(F first, S second)
            {
                First = first;
                Second = second;
            }

            public F First { get; set; }
            public S Second { get; set; }
        }

        public static BitmapImage BitmapToBitmapImage(Bitmap bmp)
        {
            MemoryStream memory = new MemoryStream();
            bmp.Save(memory, ImageFormat.Bmp);
            memory.Position = 0;
            BitmapImage result = new BitmapImage();
            result.BeginInit();
            result.StreamSource = memory;
            result.CacheOption = BitmapCacheOption.OnLoad;
            result.EndInit();
            return result;
        }

        public static System.Windows.Media.Color ConvertColor(System.Drawing.Color color)
        {
            return System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B);
        }

        public static System.Drawing.Color ConvertColor(System.Windows.Media.Color color)
        {
            return System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B);
        }

        public class ColorsPool
        {
            public ColorsPool()
            {
                Colors = new HashSet<Color>();
                foreach (System.UInt32 v in ColorValues)
                    Colors.Add(Color.FromArgb((int)v));
            }

            public Color Pull()
            {
                var en = Colors.GetEnumerator();
                if (!en.MoveNext())
                    return Color.Black;

                Color result = en.Current;
                Colors.Remove(result);
                return result;
            }

            public void Push(Color color)
            {
                Color c = Color.FromArgb(color.A, color.R, color.G, color.B);
                if (c.A != 0)
                    Colors.Add(c);
            }

            public Color Transparent { get { return Color.FromArgb(0x00FFFFFF); } }

            private HashSet<Color> Colors;

            private static System.UInt32[] ColorValues = new System.UInt32[] {
                0xFFC00000, 0xFF00C000, 0xFF0000C0,
                0xFFC08000, 0xFF00C080, 0xFF8000C0, 0xFFC00080, 0xFF80C000, 0xFF0080C0,
                0xFFC08080, 0xFF80C080, 0xFF8080C0
            };
        }

        public static string BaseType(string type)
        {
            if (type.StartsWith("const "))
                type = type.Remove(0, 6);
            int i = type.IndexOf('<');
            if (i > 0)
                type = type.Remove(i);
            return type;
        }

        public static List<string> Tparams(string type)
        {
            List<string> result = new List<string>();

            int param_list_index = 0;
            int index = 0;
            int param_first = -1;
            int param_last = -1;
            foreach (char c in type)
            {
                if (c == '<')
                {
                    ++param_list_index;
                }
                else if (c == '>')
                {
                    if (param_last == -1 && param_list_index == 1)
                        param_last = index;

                    --param_list_index;
                }
                else if (c == ',')
                {
                    if (param_last == -1 && param_list_index == 1)
                        param_last = index;
                }
                else
                {
                    if (param_first == -1 && param_list_index == 1)
                        param_first = index;
                }

                if (param_first != -1 && param_last != -1)
                {
                    result.Add(type.Substring(param_first, param_last - param_first));
                    param_first = -1;
                    param_last = -1;
                }

                ++index;
            }

            return result;
        }
    }
}
