//------------------------------------------------------------------------------
// <copyright file="GeometryWatchControl.xaml.cs" company="Company">
//     Copyright (c) Company.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace GraphicalDebugging
{
    using System.Diagnostics.CodeAnalysis;
    using System.Windows;
    using System.Windows.Controls;

    using System.Drawing;
    using System.Drawing.Imaging;
    using System.IO;

    using EnvDTE;
    using EnvDTE80;
    using Microsoft.VisualStudio.Shell;
    using Microsoft.VisualStudio.Utilities;

    /// <summary>
    /// Interaction logic for GeometryWatchControl.
    /// </summary>
    public partial class GeometryWatchControl : UserControl
    {
        private DTE2 m_dte;
        private Debugger m_debugger;
        private DebuggerEvents m_debuggerEvents;

        Util.ColorsPool m_colorsPool;

        Bitmap m_emptyBitmap;

        /// <summary>
        /// Initializes a new instance of the <see cref="GeometryWatchControl"/> class.
        /// </summary>
        public GeometryWatchControl()
        {
            m_dte = (DTE2)ServiceProvider.GlobalProvider.GetService(typeof(DTE));
            m_debugger = m_dte.Debugger;
            m_debuggerEvents = m_dte.Events.DebuggerEvents;
            m_debuggerEvents.OnEnterBreakMode += DebuggerEvents_OnEnterBreakMode;

            m_colorsPool = new Util.ColorsPool();

            m_emptyBitmap = new Bitmap(100, 100);
            Graphics graphics = Graphics.FromImage(m_emptyBitmap);
            graphics.Clear(Color.White);

            this.InitializeComponent();

            image.Source = Util.BitmapToBitmapImage(m_emptyBitmap);

            GeometryItem var = new GeometryItem(m_colorsPool.Transparent);
            ((System.ComponentModel.INotifyPropertyChanged)var).PropertyChanged += GeometryItem_NameChanged;
            listView.Items.Add(var);
        }

        /// <summary>
        /// Handles click on the button by displaying a message box.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event args.</param>
        [SuppressMessage("Microsoft.Globalization", "CA1300:SpecifyMessageBoxOptions", Justification = "Sample code")]
        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1300:ElementMustBeginWithUpperCaseLetter", Justification = "Default event handler naming pattern")]
        private void button1_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                string.Format(System.Globalization.CultureInfo.CurrentUICulture, "Invoked '{0}'", this.ToString()),
                "GeometriesWatch");
        }

        private void GeometryItem_NameChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            //e.PropertyName == "Name"

            GeometryItem geometry = (GeometryItem)sender;
            int index = listView.Items.IndexOf(geometry);

            if (geometry.Name == null || geometry.Name == "")
            {
                m_colorsPool.Push(Util.ConvertColor(geometry.Color));
                listView.Items.RemoveAt(index);
                if (index >= 0)
                    RedrawGeometries();
                return;
            }

            // insert new empty row
            if (index + 1 == listView.Items.Count)
            {
                GeometryItem empty_variable = new GeometryItem(m_colorsPool.Transparent);
                ((System.ComponentModel.INotifyPropertyChanged)empty_variable).PropertyChanged += GeometryItem_NameChanged;
                listView.Items.Add(empty_variable);
            }

            System.Windows.Media.Color color = geometry.Color;
            ExpressionDrawer.IDrawable drawable = null;
            string type = null;

            // debugging
            if (m_debugger.CurrentMode == dbgDebugMode.dbgBreakMode)
            {
                var expression = m_debugger.GetExpression(geometry.Name);
                if (expression.IsValidValue)
                {
                    drawable = ExpressionDrawer.MakeGeometry(m_debugger, geometry.Name);
                    type = expression.Type;

                    if (drawable != null)
                    {
                        if (color == Util.ConvertColor(m_colorsPool.Transparent))
                            color = Util.ConvertColor(m_colorsPool.Pull());
                    }
                }
            }

            // set new row
            GeometryItem new_variable = new GeometryItem(geometry.Name, drawable, type, color);
            ((System.ComponentModel.INotifyPropertyChanged)new_variable).PropertyChanged += GeometryItem_NameChanged;
            listView.Items.RemoveAt(index);
            listView.Items.Insert(index, new_variable);

            if (drawable != null)
            {
                Geometry.Box aabb = Geometry.Box.Inverted();
                int drawnCount = 0;
                foreach (GeometryItem g in listView.Items)
                {
                    if (g.Drawable != null)
                    {
                        aabb.Expand(g.Drawable.Aabb);
                        ++drawnCount;
                    }
                }

                if (drawnCount > 0)
                {
                    Bitmap bmp = new Bitmap((int)image.ActualWidth, (int)image.ActualHeight);

                    Graphics graphics = Graphics.FromImage(bmp);
                    graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    graphics.Clear(Color.White);

                    ExpressionDrawer.Settings settings = new ExpressionDrawer.Settings(Color.Black, true, true);
                    foreach (GeometryItem g in listView.Items)
                    {
                        if (g.Drawable != null)
                        {
                            settings.color = Util.ConvertColor(g.Color);
                            g.Drawable.Draw(aabb, graphics, settings);
                        }
                    }

                    ExpressionDrawer.DrawAabb(graphics, aabb);

                    image.Source = Util.BitmapToBitmapImage(bmp);
                }
                else
                {
                    image.Source = Util.BitmapToBitmapImage(m_emptyBitmap);
                }
            }
        }

        private void RedrawGeometries()
        {
            if (m_debugger.CurrentMode == dbgDebugMode.dbgBreakMode)
            {
                Geometry.Box aabb = Geometry.Box.Inverted();
                int drawnCount = 0;
                for (int index = 0; index < listView.Items.Count; ++index)
                {
                    GeometryItem geometry = (GeometryItem)listView.Items[index];

                    System.Windows.Media.Color color = geometry.Color;
                    ExpressionDrawer.IDrawable drawable = null;
                    string type = null;

                    if (geometry.Name != null && geometry.Name != "")
                    {
                        var expression = m_debugger.GetExpression(geometry.Name);
                        if (expression.IsValidValue)
                        {
                            drawable = ExpressionDrawer.MakeGeometry(m_debugger, geometry.Name);
                            type = expression.Type;

                            if (drawable != null)
                            {
                                if (geometry.Color == Util.ConvertColor(m_colorsPool.Transparent))
                                    color = Util.ConvertColor(m_colorsPool.Pull());
                                
                                aabb.Expand(drawable.Aabb);
                                ++drawnCount;
                            }
                        }
                    }

                    // set new row
                    GeometryItem new_geometry = new GeometryItem(geometry.Name, drawable, type, color);
                    ((System.ComponentModel.INotifyPropertyChanged)new_geometry).PropertyChanged += GeometryItem_NameChanged;
                    listView.Items.RemoveAt(index);
                    listView.Items.Insert(index, new_geometry);
                }

                if (drawnCount > 0)
                {
                    Bitmap bmp = new Bitmap((int)System.Math.Round(image.ActualWidth),
                                            (int)System.Math.Round(image.ActualHeight));

                    Graphics graphics = Graphics.FromImage(bmp);
                    graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    graphics.Clear(Color.White);

                    ExpressionDrawer.Settings settings = new ExpressionDrawer.Settings(Color.Black, true, true);
                    foreach (GeometryItem g in listView.Items)
                    {
                        if (g.Drawable != null)
                        {
                            settings.color = Util.ConvertColor(g.Color);
                            g.Drawable.Draw(aabb, graphics, settings);
                        }
                    }

                    ExpressionDrawer.DrawAabb(graphics, aabb);

                    image.Source = Util.BitmapToBitmapImage(bmp);
                }
                else
                {
                    image.Source = Util.BitmapToBitmapImage(m_emptyBitmap);
                }
            }
        }

        private void DebuggerEvents_OnEnterBreakMode(dbgEventReason Reason, ref dbgExecutionAction ExecutionAction)
        {
            RedrawGeometries();
        }

        private void GridSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            RedrawGeometries();
        }

        private void GeometryWatchWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            RedrawGeometries();
        }

        private void listView_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Delete)
            {
                int[] indexes = new int[listView.SelectedItems.Count];
                int i = 0;
                foreach (var item in listView.SelectedItems)
                {
                    indexes[i] = listView.Items.IndexOf(item);
                    ++i;
                }
                System.Array.Sort(indexes, delegate (int l, int r) {
                    return -l.CompareTo(r);
                });

                bool removed = false;
                foreach (int index in indexes)
                {
                    if (index + 1 < listView.Items.Count)
                    {
                        GeometryItem geometry = (GeometryItem)listView.Items[index];
                        m_colorsPool.Push(Util.ConvertColor(geometry.Color));
                        listView.Items.RemoveAt(index);

                        removed = true;
                    }
                }

                if (removed)
                    RedrawGeometries();
            }
        }
    }
}