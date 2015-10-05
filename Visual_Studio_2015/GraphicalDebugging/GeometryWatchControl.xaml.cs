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
    using Microsoft.VisualStudio.PlatformUI;
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

        Util.IntsPool m_intsPool;

        Colors m_colors;
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

            VSColorTheme.ThemeChanged += VSColorTheme_ThemeChanged;

            m_colors = new Colors(this);
            m_intsPool = new Util.IntsPool(m_colors.Count);
            m_emptyBitmap = new Bitmap(100, 100);
            Graphics graphics = Graphics.FromImage(m_emptyBitmap);
            graphics.Clear(m_colors.ClearColor);

            Geometries = new ObservableCollection<GeometryItem>();

            this.InitializeComponent();

            dataGrid.ItemsSource = Geometries;

            image.Source = Util.BitmapToBitmapImage(m_emptyBitmap);

            ResetAt(new GeometryItem(-1, m_colors), Geometries.Count);
        }

        private void VSColorTheme_ThemeChanged(ThemeChangedEventArgs e)
        {
            m_colors.Update();
            Graphics graphics = Graphics.FromImage(m_emptyBitmap);
            graphics.Clear(m_colors.ClearColor);
            UpdateItems();
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
                if (index < dataGrid.Items.Count - 1)
                {
                    m_intsPool.Push(geometry.ColorId);
                    Geometries.RemoveAt(index);
                    UpdateItems();
                }
            }
            else
            {
                UpdateItems(index);

                // insert new empty row
                int next_index = index + 1;
                if (next_index == Geometries.Count)
                {
                    ResetAt(new GeometryItem(-1, m_colors), index + 1);
                    SelectAt(index + 1, true);
                }
                else
                {
                    SelectAt(index + 1);
                }
            }
        }

        private void ResetAt(GeometryItem item, int index)
        {
            ((System.ComponentModel.INotifyPropertyChanged)item).PropertyChanged += GeometryItem_NameChanged;
            if (index < Geometries.Count)
                Geometries.RemoveAt(index);
            Geometries.Insert(index, item);
        }

        private void SelectAt(int index, bool isNew = false)
        {
            object item = dataGrid.Items[index];

            if (isNew)
            {
                dataGrid.SelectedItem = item;
                dataGrid.ScrollIntoView(item);
                DataGridRow dgrow = (DataGridRow)dataGrid.ItemContainerGenerator.ContainerFromItem(item);
                dgrow.MoveFocus(new System.Windows.Input.TraversalRequest(System.Windows.Input.FocusNavigationDirection.Next));
            }
            else
            {
                dataGrid.SelectedItem = item;
                dataGrid.ScrollIntoView(item);
            }
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
                if (dataGrid.SelectedItems.Count > 0)
                {
                    int[] indexes = new int[dataGrid.SelectedItems.Count];
                    int i = 0;
                    foreach (var item in dataGrid.SelectedItems)
                    {
                        indexes[i] = dataGrid.Items.IndexOf(item);
                        ++i;
                    }
                    System.Array.Sort(indexes, delegate (int l, int r)
                    {
                        return -l.CompareTo(r);
                    });

                    bool removed = false;
                    foreach (int index in indexes)
                    {
                        if (index + 1 < Geometries.Count)
                        {
                            GeometryItem geometry = Geometries[index];
                            m_intsPool.Push(geometry.ColorId);
                            Geometries.RemoveAt(index);

                            removed = true;
                        }
                    }

                    if (removed)
                    {
                        UpdateItems();
                    }
                }
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
                    int colorId = geometry.ColorId;
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

                            if (updateRequred && geometry.ColorId < 0)
                            {
                                colorId = m_intsPool.Pull();
                                color = Util.ConvertColor(m_colors[colorId]);
                            }

                            settings[index] = new ExpressionDrawer.Settings(Util.ConvertColor(color), true, true);

                            tryDrawing = true;
                        }
                    }

                    // set new row
                    if (updateRequred)
                        ResetAt(new GeometryItem(geometry.Name, type, colorId, m_colors), index);
                }

                // draw variables
                if (tryDrawing)
                {
                    Bitmap bmp = new Bitmap((int)System.Math.Round(image.ActualWidth),
                                            (int)System.Math.Round(image.ActualHeight));

                    Graphics graphics = Graphics.FromImage(bmp);
                    graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    graphics.Clear(m_colors.ClearColor);

                    ExpressionDrawer.DrawGeometries(graphics, m_debugger, names, settings, m_colors);

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
    }
}