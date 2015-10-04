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

    using System.Collections.Generic;
    using System.Collections.ObjectModel;

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

        ObservableCollection<GeometryItem> Geometries { get; set; }

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
            Clear(graphics);

            Geometries = new ObservableCollection<GeometryItem>();

            this.InitializeComponent();

            dataGrid.ItemsSource = Geometries;

            image.Source = Util.BitmapToBitmapImage(m_emptyBitmap);

            ResetAt(new GeometryItem(m_colorsPool.Transparent), Geometries.Count);
        }
        
        private void GeometryItem_NameChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            //e.PropertyName == "Name"

            GeometryItem geometry = (GeometryItem)sender;
            int index = Geometries.IndexOf(geometry);

            if (index < 0 || index >= dataGrid.Items.Count)
                return;

            if (geometry.Name == null || geometry.Name == "")
            {
                m_colorsPool.Push(Util.ConvertColor(geometry.Color));
                Geometries.RemoveAt(index);
                if (index >= 0)
                    UpdateItems();
                return;
            }

            // insert new empty row
            if (index + 1 == Geometries.Count)
            {
                ResetAt(new GeometryItem(m_colorsPool.Transparent), Geometries.Count);
            }

            UpdateItems(index);
        }

        private void ResetAt(GeometryItem item, int index)
        {
            ((System.ComponentModel.INotifyPropertyChanged)item).PropertyChanged += GeometryItem_NameChanged;
            if (index < Geometries.Count)
                Geometries.RemoveAt(index);
            Geometries.Insert(index, item);
        }

        private void DebuggerEvents_OnEnterBreakMode(dbgEventReason Reason, ref dbgExecutionAction ExecutionAction)
        {
            UpdateItems();
        }

        private void GridSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            UpdateItems();
        }

        private void GeometryWatchWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateItems();
        }

        private void dataGrid_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Delete)
            {
                int[] indexes = new int[dataGrid.SelectedItems.Count];
                int i = 0;
                foreach (var item in dataGrid.SelectedItems)
                {
                    indexes[i] = dataGrid.Items.IndexOf(item);
                    ++i;
                }
                System.Array.Sort(indexes, delegate (int l, int r) {
                    return -l.CompareTo(r);
                });

                bool removed = false;
                foreach (int index in indexes)
                {
                    if (index + 1 < Geometries.Count)
                    {
                        GeometryItem geometry = Geometries[index];
                        m_colorsPool.Push(Util.ConvertColor(geometry.Color));
                        Geometries.RemoveAt(index);

                        removed = true;
                    }
                }

                if (removed)
                    UpdateItems();
            }
        }

        private void UpdateItems(int modified_index = -1)
        {
            if (m_debugger.CurrentMode == dbgDebugMode.dbgBreakMode)
            {
                string[] names = new string[Geometries.Count];
                ExpressionDrawer.Settings[] settings = new ExpressionDrawer.Settings[Geometries.Count];
                bool tryDrawing = false;

                // update the list, gather names and settings
                for (int index = 0; index < Geometries.Count; ++index)
                {
                    GeometryItem geometry = Geometries[index];

                    System.Windows.Media.Color color = geometry.Color;
                    string type = null;

                    bool updateRequred = modified_index < 0 || modified_index == index;

                    if (geometry.Name != null && geometry.Name != "")
                    {
                        var expression = updateRequred ? m_debugger.GetExpression(geometry.Name) : null;
                        if (expression == null || expression.IsValidValue)
                        {
                            if (expression != null)
                                type = expression.Type;

                            names[index] = geometry.Name;

                            if (geometry.Color == Util.ConvertColor(m_colorsPool.Transparent))
                                color = Util.ConvertColor(m_colorsPool.Pull());

                            settings[index] = new ExpressionDrawer.Settings(Util.ConvertColor(color), true, true);

                            tryDrawing = true;
                        }
                    }

                    // set new row
                    if (updateRequred)
                        ResetAt(new GeometryItem(geometry.Name, type, color), index);
                }

                // draw variables
                if (tryDrawing)
                {
                    Bitmap bmp = new Bitmap((int)System.Math.Round(image.ActualWidth),
                                            (int)System.Math.Round(image.ActualHeight));

                    Graphics graphics = Graphics.FromImage(bmp);
                    graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    Clear(graphics);

                    ExpressionDrawer.DrawGeometries(graphics, m_debugger, names, settings);

                    image.Source = Util.BitmapToBitmapImage(bmp);
                    }
                else
                {
                    image.Source = Util.BitmapToBitmapImage(m_emptyBitmap);
                }
            }
            else
            {
                image.Source = Util.BitmapToBitmapImage(m_emptyBitmap);
            }
        }

        private void Clear(Graphics graphics)
        {
            Color backgroundColor = Color.White;
            /*try
            {
                var tmp = (System.Windows.Media.Color)FindResource(VsColors.ToolWindowBackgroundKey);
                backgroundColor = Util.ConvertColor(tmp);
                if (backgroundColor.GetSaturation() >= 0.5f)
                    backgroundColor = Color.White;
                else
                    backgroundColor = Color.FromArgb(0xFF, 24, 24, 24);
            }
            catch (System.Exception) { }*/
            graphics.Clear(backgroundColor);
        }
    }
}