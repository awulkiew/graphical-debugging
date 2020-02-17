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
using System.Globalization;
using System.IO;
using System.Windows.Media.Imaging;
using System.Threading;
using Task = System.Threading.Tasks.Task;

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
                    if (type[param_last - 1] == ' ')
                        --param_last;
                    result.Add(type.Substring(param_first, param_last - param_first));
                    param_first = -1;
                    param_last = -1;
                }

                ++index;
            }

            return result;
        }

        public static bool Tparams(string type, out string param1)
        {
            param1 = "";
            List<string> list = Tparams(type);
            if (list.Count < 1)
                return false;
            param1 = list[0];
            return true;
        }

        public static bool Tparams(string type, out string param1, out string param2)
        {
            param1 = "";
            param2 = "";
            List<string> list = Tparams(type);
            if (list.Count < 2)
                return false;
            param1 = list[0];
            param2 = list[1];
            return true;
        }

        public static void ShowWindow<WatchWindow>(Package package, string name)
            where WatchWindow : ToolWindowPane
        {
            for (int i = 0; i < 10; ++i)
            {
                ToolWindowPane window = package.FindToolWindow(typeof(WatchWindow), i, false);
                if (window == null)
                {
                    window = package.FindToolWindow(typeof(WatchWindow), i, true);
                    if ((null == window) || (null == window.Frame))
                    {
                        throw new NotSupportedException("Cannot create " + name + " window");
                    }

                    /*if (i > 0)
                    {
                        window.Caption = window.Caption + ' ' + (i + 1);
                    }*/

                    IVsWindowFrame windowFrame = (IVsWindowFrame)window.Frame;
                    Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(windowFrame.Show());
                    break;
                }
            }
        }

        public static T GetDialogPage<T>()
            where T : DialogPage
        {
            GraphicalWatchPackage package = GraphicalWatchPackage.Instance;
            if (package == null)
                return default(T);

            return (T)package.GetDialogPage(typeof(T));
        }

        public static string ToString(double v)
        {
            return v.ToString(CultureInfo.InvariantCulture);
        }

        public static string ToString(double v, string format)
        {
            return v.ToString(format, CultureInfo.InvariantCulture);
        }

        public static void SortDsc<T>(T[] array)
            where T : IComparable
        {
            System.Array.Sort(array, delegate (T l, T r) {
                return -l.CompareTo(r);
            });
        }

        public delegate void RemovedItemPredicate<Item>(Item item);

        public static bool RemoveDataGridItems<Item>(System.Windows.Controls.DataGrid dataGrid,
                                                     System.Collections.ObjectModel.ObservableCollection<Item> itemsCollection,
                                                     RemovedItemPredicate<Item> removedPredicate,
                                                     out int selectIndex)
        {
            int[] indexes = new int[dataGrid.SelectedItems.Count];
            int i = 0;
            foreach (var item in dataGrid.SelectedItems)
            {
                indexes[i] = dataGrid.Items.IndexOf(item);
                ++i;
            }

            Util.SortDsc(indexes);

            bool removed = false;
            foreach (int index in indexes)
            {
                if (index + 1 < itemsCollection.Count)
                {
                    Item item = itemsCollection[index];
                    removedPredicate(item);
                    itemsCollection.RemoveAt(index);
                    removed = true;
                }
            }

            selectIndex = -1;
            if (indexes.Length > 0)
                selectIndex = indexes[indexes.Length - 1];
            
            return removed;
        }

        public static void SelectDataGridItem(System.Windows.Controls.DataGrid dataGrid,
                                              int index)
        {
            if (0 <= index && index < dataGrid.Items.Count)
            {
                object item = dataGrid.Items[index];
                dataGrid.CurrentItem = item;
                dataGrid.SelectedItem = item;
                dataGrid.ScrollIntoView(item);
            }
            else
            {
                dataGrid.CurrentItem = null;
                dataGrid.SelectedItem = null;
            }
        }

        public static System.Xml.XmlElement GetXmlElementByTagName(System.Xml.XmlElement parent, string name)
        {
            if (parent == null)
                return null;
            foreach (System.Xml.XmlElement el in parent.GetElementsByTagName(name))
                return el;
            return null;
        }

        public static System.Xml.XmlElement GetXmlElementByTagNames(System.Xml.XmlElement parent, string name1, string name2)
        {
            return GetXmlElementByTagName(GetXmlElementByTagName(parent, name1), name2);
        }

        public static bool IsHex(string val)
        {
            //return val.StartsWith("0x", StringComparison.CurrentCultureIgnoreCase);
            return val.Length > 1 /*&& val[0] == '0'*/ && val[1] == 'x';
        }

        public static ulong ParseULong(string val)
        {
            return ParseULong(val, IsHex(val));
        }

        public static ulong ParseULong(string val, bool isHex)
        {
            return IsHex(val)
                 ? ulong.Parse(val.Substring(2), NumberStyles.HexNumber)
                 : ulong.Parse(val);
        }

        public static int ParseInt(string val)
        {
            return ParseInt(val, IsHex(val));
        }

        public static int ParseInt(string val, bool isHex)
        {
            return isHex
                 ? int.Parse(val.Substring(2), NumberStyles.HexNumber)
                 : int.Parse(val);
        }

        public static bool TryParseInt(string val, out int result)
        {
            return TryParseInt(val, IsHex(val), out result);
        }

        public static bool TryParseInt(string val, bool isHex, out int result)
        {
            return isHex
                 ? int.TryParse(val.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out result)
                 : int.TryParse(val, out result);
        }

        public static double ParseDouble(string s)
        {
            return double.Parse(s, CultureInfo.InvariantCulture);
        }

        // class -> v != null
        // bool  -> v != false
        public static bool IsOk<T1>(T1 v1)
        {
            return !v1.Equals(default(T1));
        }
        public static bool IsOk<T1, T2>(T1 v1, T2 v2)
        {
            return !v1.Equals(default(T1))
                && !v2.Equals(default(T2));
        }
        public static bool IsOk<T1, T2, T3>(T1 v1, T2 v2, T3 v3)
        {
            return !v1.Equals(default(T1))
                && !v2.Equals(default(T2))
                && !v3.Equals(default(T3));
        }
        public static bool IsOk<T1, T2, T3, T4>(T1 v1, T2 v2, T3 v3, T4 v4)
        {
            return !v1.Equals(default(T1))
                && !v2.Equals(default(T2))
                && !v3.Equals(default(T3))
                && !v4.Equals(default(T4));
        }

        public static bool Assign<T>(ref T v, T v2)
            where T : struct
        {
            bool result = !v.Equals(v2);
            v = v2;
            return result;
        }
    }
}
