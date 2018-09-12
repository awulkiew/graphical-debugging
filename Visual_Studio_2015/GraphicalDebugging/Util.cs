//------------------------------------------------------------------------------
// <copyright file="Util.cs">
//     Copyright (c) Adam Wulkiewicz.
// </copyright>
//------------------------------------------------------------------------------

using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

using System;

using System.Collections.Generic;

using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Media.Imaging;
using System.IO;

namespace GraphicalDebugging
{
    class Util
    {
        private Util() { }

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
            return BitmapToBitmapImage(bmp, ImageFormat.Png);
        }

        public static BitmapImage BitmapToBitmapImage(Bitmap bmp, ImageFormat format)
        {
            MemoryStream memory = new MemoryStream();
            bmp.Save(memory, format);
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

        public class IntsPool
        {
            public IntsPool(int count)
            {
                values = new SortedSet<int>();
                for (int i = 0; i < count; ++i)
                    values.Add(i);

                max_count = count;
            }

            public int Pull()
            {
                var en = values.GetEnumerator();
                if (!en.MoveNext())
                    return max_count;

                int result = en.Current;
                values.Remove(result);
                return result;
            }

            public void Push(int value)
            {
                if (value >= 0 && value < max_count)
                    values.Add(value);
            }
            
            private SortedSet<int> values;
            private int max_count;
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

        private class MainPackage
        {
            private static Guid packageGuid = new Guid(GraphicalWatchPackage.PackageGuidString);

            public MainPackage()
            {
                IVsPackage ipackage = null;
                int res = ((IVsShell)ServiceProvider.GlobalProvider.GetService(typeof(IVsShell))).IsPackageLoaded(packageGuid, out ipackage);
                if (res == 0 && ipackage != null)
                    package = (GraphicalWatchPackage)ipackage;
            }

            public GraphicalWatchPackage Get() { return package; }

            private GraphicalWatchPackage package = null;
        }

        private static MainPackage mainPackage = new MainPackage();

        public static GraphicalWatchPackage GetPackage()
        {
            return mainPackage.Get();
        }

        public static T GetDialogPage<T>()
            where T : DialogPage
        {
            GraphicalWatchPackage package = GetPackage();
            if (package == null)
                return default(T);

            return (T)package.GetDialogPage(typeof(T));
        }
    }
}
