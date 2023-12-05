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
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;


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
            
            readonly SortedSet<int> values;
            readonly int max_count;
        }

        public static string TypeId(string type)
        {
            int startIndex = 0;
            int endIndex = type.Length;

            if (type.StartsWith("const ")) // volatile?
                startIndex = 6;
            // Ignore template parameters, C# derived class, Basic generic parameters
            int i = type.IndexOfAny(new char[] { '<', '{', '(' }, startIndex);
            if (i >= 0)
                endIndex = Math.Min(endIndex, i);
            // An artifact of C# derived class, but do in all cases just in case
            while (endIndex > 0 && type[endIndex - 1] == ' ')
                --endIndex;
            if (startIndex > 0 || endIndex < type.Length)
                type = type.Substring(startIndex, endIndex - startIndex);
            return type;
        }

        public static string CppNormalizeType(string type)
        {
            string result = "";
            char prev = '\0';
            foreach (char c in type)
            {
                if (c == '>' && prev == '>')
                    result += " >";
                else
                    result += c;
                prev = c;
            }
            return result;
        }

        public static string CppRemoveCVRef(string type)
        {
            if (type.EndsWith("}"))
            {
                int i = type.LastIndexOf("{");
                if (i > 0) // including space
                {
                    type = type.Remove(i - 1);
                }
            }
            if (type.Contains("!"))
            {
                //remove bracket-less module declaration sometimes appearing in the variable type (ends with "!" symbol)
                int i = type.LastIndexOf("!");
                if (i > 0) // including space
                {
                    type = type.Remove(0, i + 1);
                }
            }
            if (type.StartsWith("const "))
                type = type.Remove(0, 6);
            if (type.StartsWith("volatile "))
                type = type.Remove(0, 9);
            if (type.EndsWith(" const"))
                type = type.Remove(type.Length - 6);
            if (type.EndsWith(" &&"))
                type = type.Remove(type.Length - 3);
            if (type.EndsWith(" &"))
                type = type.Remove(type.Length - 2);
            return type;
        }

        // TODO: Basic generic parameters
        public static List<string> TypesList(string type,
                                             char begCh = '<',
                                             char endCh = '>',
                                             char sepCh = ',')
        {
            List<string> result = new List<string>();

            int param_list_index = 0;
            int index = 0;
            int param_first = -1;
            int param_last = -1;
            foreach (char c in type)
            {
                if (c == begCh)
                {
                    ++param_list_index;
                }
                else if (c == endCh)
                {
                    if (param_last == -1 && param_list_index == 1)
                        param_last = index;

                    --param_list_index;
                }
                else if (c == sepCh)
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

        public static string CSDerivedType(string type)
        {
            List<string> list = TypesList(type, '{', '}');
            return list.Count > 0 ? list[0] : "";
        }

        public static List<string> Tparams(string type)
        {
            return TypesList(type);
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

        public static string Tparam(string type, int index)
        {
            List<string> tparams = Util.Tparams(type);
            return tparams.Count > index
                 ? tparams[index]
                 : "";
        }

        public static void ShowWindow<WatchWindow>(AsyncPackage package, string name)
            where WatchWindow : ToolWindowPane
        {
            ThreadHelper.ThrowIfNotOnUIThread();

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

        private static System.Windows.Forms.ColorDialog colorDialog;

        public static System.Drawing.Color ShowColorDialog(System.Drawing.Color color)
        {
            if (colorDialog == null)
                colorDialog = new System.Windows.Forms.ColorDialog();
            colorDialog.Color = System.Drawing.Color.FromArgb(255, color);
            colorDialog.FullOpen = true;
            if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                color = System.Drawing.Color.FromArgb(255, colorDialog.Color);
            }
            return color;
        }

        public static System.Windows.Media.Color ShowColorDialog(System.Windows.Media.Color color)
        {
            return ConvertColor(ShowColorDialog(ConvertColor(color)));
        }

        private static GraphicalDebugging.Colors colors;
        public static GraphicalDebugging.Colors Colors
        {
            get
            {
                if (colors == null)
                    colors = new GraphicalDebugging.Colors();
                return colors;
            }
        }

        public static T GetDialogPage<T>()
            where T : DialogPage
        {
            GraphicalDebuggingPackage package = GraphicalDebuggingPackage.Instance;
            return package != null
                 ? (T)package.GetDialogPage(typeof(T))
                 : default;
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

        public static int[] DataGridSelectedIndexes(System.Windows.Controls.DataGrid dataGrid)
        {
            int[] indexes = new int[dataGrid.SelectedItems.Count];
            int i = 0;
            foreach (var item in dataGrid.SelectedItems)
            {
                indexes[i] = dataGrid.Items.IndexOf(item);
                ++i;
            }
            return indexes;
        }

        public delegate void ItemUpdatedPredicate(int index);
        public delegate void ItemInsertEmptyPredicate(int index);
        public delegate void ItemRemovePredicate<Item>(Item item);
        public delegate void ItemRemovedPredicate(int index);
        public delegate void ItemsRemovedPredicate();

        public static void RemoveDataGridItems<Item>(System.Windows.Controls.DataGrid dataGrid,
                                                     System.Collections.ObjectModel.ObservableCollection<Item> itemsCollection,
                                                     ItemInsertEmptyPredicate insertEmptyPredicate,
                                                     ItemRemovePredicate<Item> removePredicate,
                                                     ItemsRemovedPredicate removedPredicate)
        {
            if (dataGrid.SelectedItems.Count < 1)
                return;

            int[] indexes = DataGridSelectedIndexes(dataGrid);

            Util.SortDsc(indexes);

            bool removed = false;
            foreach (int index in indexes)
            {
                if (index + 1 < itemsCollection.Count)
                {
                    Item item = itemsCollection[index];

                    removePredicate(item);

                    itemsCollection.RemoveAt(index);

                    removed = true;
                }
            }

            int selectIndex = -1;
            if (indexes.Length > 0)
                selectIndex = indexes[indexes.Length - 1];

            if (removed)
            {
                if (selectIndex >= 0 && selectIndex == itemsCollection.Count - 1)
                {
                    insertEmptyPredicate(selectIndex);
                }

                removedPredicate();
            }

            Util.SelectDataGridItem(dataGrid, selectIndex);
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

        public static void DataGridItemPropertyChanged<Item>(System.Windows.Controls.DataGrid dataGrid,
                                                             System.Collections.ObjectModel.ObservableCollection<Item> items,
                                                             Item item,
                                                             string propertyName,
                                                             ItemUpdatedPredicate updatePredicate,
                                                             ItemInsertEmptyPredicate insertEmptyPredicate,
                                                             ItemRemovePredicate<Item> removePredicate,
                                                             ItemRemovedPredicate removedPredicate)
            where Item : VariableItem
        {
            int index = items.IndexOf(item);

            if (index < 0 || index >= dataGrid.Items.Count)
                return;

            if (propertyName == "Name")
            {
                if (item.Name == null || item.Name == "")
                {
                    if (index < dataGrid.Items.Count - 1)
                    {
                        removePredicate(item);

                        items.RemoveAt(index);

                        removedPredicate(index);

                        if (index > 0)
                        {
                            Util.SelectDataGridItem(dataGrid, index - 1);
                        }
                    }
                }
                else
                {
                    updatePredicate(index);

                    int next_index = index + 1;
                    // insert new empty row if needed
                    if (next_index == items.Count)
                    {
                        insertEmptyPredicate(next_index);
                    }
                    // select current row, move to next one is automatic
                    Util.SelectDataGridItem(dataGrid, index);
                }
            }
            else if (propertyName == "IsEnabled")
            {
                updatePredicate(index);
            }
        }

        public static void DataGridItemPropertyChanged<Item>(System.Windows.Controls.DataGrid dataGrid,
                                                             System.Collections.ObjectModel.ObservableCollection<Item> items,
                                                             Item item,
                                                             string propertyName,
                                                             ItemUpdatedPredicate updatePredicate,
                                                             ItemInsertEmptyPredicate insertEmptyPredicate)
            where Item : VariableItem
        {
            DataGridItemPropertyChanged(dataGrid, items, item, propertyName,
                                        updatePredicate,
                                        insertEmptyPredicate,
                                        delegate (Item it) { },
                                        delegate (int index) { } );
        }

        public delegate void ItemEnablePredicate<Item>(Item item);
        public static void EnableDataGridItems<Item>(System.Windows.Controls.DataGrid dataGrid,
                                                     System.Collections.ObjectModel.ObservableCollection<Item> itemsCollection,
                                                     ItemEnablePredicate<Item> enablePredicate)
        {
            if (dataGrid.SelectedItems.Count < 1)
                return;

            int[] indexes = DataGridSelectedIndexes(dataGrid);

            foreach (int index in indexes)
            {
                Item item = itemsCollection[index];
                enablePredicate(item);
            }
        }

        public static void PasteDataGridItemFromClipboard<Item>(System.Windows.Controls.DataGrid dataGrid,
                                                                System.Collections.ObjectModel.ObservableCollection<Item> itemsCollection)
            where Item : VariableItem
        {
            string text = Clipboard.GetText();
            if (string.IsNullOrEmpty(text))
                return;

            if (dataGrid.SelectedItems.Count != 1)
                return;

            int index = dataGrid.Items.IndexOf(dataGrid.SelectedItems[0]);
            if (index < 0 || index >= dataGrid.Items.Count)
                return;

            Item item = itemsCollection[index];
            item.Name = text;
        }

        // https://softwaremechanik.wordpress.com/2013/10/02/how-to-make-all-wpf-datagrid-cells-have-a-single-click-to-edit/
        public static void DataGridSingleClickHack(DependencyObject originalSource)
        {
            // If the user clicked the TextBlock
            // use single click edit only for the last, empty row
            if (originalSource is TextBlock)
            {
                if (!(originalSource is FrameworkElement))
                    return;
                object item = (originalSource as FrameworkElement).DataContext;
                if (!(item is VariableItem))
                    return;
                VariableItem variable = item as VariableItem;
                if (variable.Name != null && variable.Name != "")
                    return;
            }

            // Find corresponding cell and row
            DataGridCell cell = null;
            DataGridRow row = null;
            {
                DependencyObject parent = originalSource;
                while (parent != null)
                {
                    if (parent is DataGridCell)
                    {
                        cell = parent as DataGridCell;
                    }
                    else if (parent is DataGridRow)
                    {
                        row = parent as DataGridRow;
                        break;
                    }
                    parent = VisualTreeHelper.GetParent(parent);
                }
            }

            // Do magic stuff
            if (cell != null && !cell.IsEditing && !cell.IsReadOnly)
            {
                if (!cell.IsFocused)
                {
                    cell.Focus();
                }

                // NOTE: assuming SelectionUnit == FullRow
                if (row != null && !row.IsSelected)
                {
                    row.IsSelected = true;
                }
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

        public static bool TryParseULong(string val, out ulong result)
        {
            return IsHex(val)
                 ? ulong.TryParse(val.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out result)
                 : ulong.TryParse(val, out result);
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

        public static bool TryParseDouble(string s, out double result)
        {
            return double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out result);
        }

// class -> v != null
// bool  -> v != false
        public static bool IsOk<T1>(T1 v1)
        {
            return !EqualityComparer<T1>.Default.Equals(v1, default);
        }
        public static bool IsOk<T1, T2>(T1 v1, T2 v2)
        {
            return !EqualityComparer<T1>.Default.Equals(v1, default)
                && !EqualityComparer<T2>.Default.Equals(v2, default);
        }
        public static bool IsOk<T1, T2, T3>(T1 v1, T2 v2, T3 v3)
        {
            return !EqualityComparer<T1>.Default.Equals(v1, default)
                && !EqualityComparer<T2>.Default.Equals(v2, default)
                && !EqualityComparer<T3>.Default.Equals(v3, default);
        }
        public static bool IsOk<T1, T2, T3, T4>(T1 v1, T2 v2, T3 v3, T4 v4)
        {
            return !EqualityComparer<T1>.Default.Equals(v1, default)
                && !EqualityComparer<T2>.Default.Equals(v2, default)
                && !EqualityComparer<T3>.Default.Equals(v3, default)
                && !EqualityComparer<T4>.Default.Equals(v4, default);
        }

        public static bool Assign<T>(ref T v, T v2)
            where T : struct
        {
            bool result = !v.Equals(v2);
            v = v2;
            return result;
        }

        public static bool Empty(string s)
        {
            return s == null || s == "";
        }

        public static string TemplateType(string id, string tparam)
        {
            return id + "<" + tparam + (tparam.EndsWith(">") ? " >" : ">");
        }

        public static string TemplateType(string id, string tparam0, string tparam1)
        {
            return id + "<" + tparam0 + "," + tparam1 + (tparam1.EndsWith(">") ? " >" : ">");
        }

        public const int GWL_HWNDPARENT = -8;

        // https://www.medo64.com/2013/07/setwindowlongptr/
        public static IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr newStyle)
        {
            try {
                if (IntPtr.Size == 4)
                    return SetWindowLongPtr32(hwnd, index, newStyle);
                else
                    return SetWindowLongPtr64(hwnd, index, newStyle);
            } catch (Exception) { }
            return IntPtr.Zero;
        }
        [DllImport("user32.dll", SetLastError = true, EntryPoint = "SetWindowLong")]
        private static extern IntPtr SetWindowLongPtr32(IntPtr hwnd, int index, IntPtr newStyle);
        [DllImport("user32.dll", SetLastError = true, EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hwnd, int index, IntPtr newStyle);

        public static IntPtr GetWindowHandle(Window window)
        {
            try {
                WindowInteropHelper helper = new WindowInteropHelper(window);
                return helper.Handle;
            } catch (Exception) { }
            return IntPtr.Zero;
        }

        public static IntPtr SetWindowOwner(IntPtr windowHandle, IntPtr ownerHandle)
        {
            return SetWindowLongPtr(windowHandle, GWL_HWNDPARENT, ownerHandle);
        }
    }
}
