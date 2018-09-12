//------------------------------------------------------------------------------
// <copyright file="ExpressionDrawer.cs">
//     Copyright (c) Adam Wulkiewicz.
// </copyright>
//------------------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.Drawing.Drawing2D;

using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;

namespace GraphicalDebugging
{
    class ExpressionDrawer
    {
        // -------------------------------------------------
        // Members
        // -------------------------------------------------

        ExpressionLoader expressionLoader = new ExpressionLoader();

        // -------------------------------------------------
        // Settings
        // -------------------------------------------------

        public class Settings
        {
            public enum PlotType { Bar, Point, Line };

            public Settings()
            { }

            public Settings(Color color)
            {
                this.color = color;
            }

            public Settings(Color color, bool showDir, bool showLabels)
            {
                this.color = color;
                this.showDir = showDir;
                this.showLabels = showLabels;
            }

            public Settings(Color color, PlotType plotType)
            {
                this.color = color;
                this.plotType = plotType;
            }

            public Color color = Color.Black;
            public bool showDir = false;
            public bool showLabels = false;
            public PlotType plotType = PlotType.Bar;
        }

        // -------------------------------------------------
        // Drawables and drawing
        // -------------------------------------------------

        public interface IDrawable
        {
            void Draw(Geometry.Box box, Graphics graphics, Settings settings, Geometry.Traits traits);
            Geometry.Box Aabb(Geometry.Traits traits, bool calculateEnvelope);
            Color DefaultColor(Colors colors);
        }

        private static void DrawPoint(Geometry.Point point, Geometry.Box box, Graphics graphics, Settings settings, Geometry.Traits traits)
        {
            LocalCS cs = new LocalCS(box, graphics);
            Drawer drawer = new Drawer(graphics, settings.color);

            if (traits.Unit == Geometry.Unit.None)
            {
                PointF p = cs.Convert(point);
                drawer.DrawPoint(p);
            }
            else // Radian, Degree
            {
                drawer.DrawPeriodicPoint(cs, point, box, traits.Unit);
            }
        }

        public class Point : Geometry.Point, IDrawable
        {
            public Point(double x, double y)
                : base(x, y)
            { }

            public Point(double x, double y, double z)
                : base(x, y, z)
            { }

            public void Draw(Geometry.Box box, Graphics graphics, Settings settings, Geometry.Traits traits)
            {
                DrawPoint(this, box, graphics, settings, traits);
            }

            public Geometry.Box Aabb(Geometry.Traits traits, bool calculateEnvelope)
            {
                return new Geometry.Box(this, this);
            }

            public Color DefaultColor(Colors colors) { return colors.PointColor; }
        }

        public class Box : Geometry.Box, IDrawable
        {
            public Box()
            { }

            public Box(Geometry.Point min, Geometry.Point max)
                : base(min, max)
            { }

            public void Draw(Geometry.Box box, Graphics graphics, Settings settings, Geometry.Traits traits)
            {
                LocalCS cs = new LocalCS(box, graphics);
                Drawer drawer = new Drawer(graphics, settings.color);

                double width = Dim(0);
                double height = Dim(1);
                float rw = cs.ConvertDimensionX(Math.Abs(width));
                float rh = cs.ConvertDimensionY(Math.Abs(height));

                if (traits.Unit == Geometry.Unit.None)
                {
                    float rx = cs.ConvertX(Math.Min(Min[0], Max[0]));
                    float ry = cs.ConvertY(Math.Max(Min[1], Max[1]));

                    if (rw == 0 && rh == 0)
                        drawer.DrawPoint(rx, ry);
                    else if (rw == 0 || rh == 0)
                        drawer.DrawLine(rx, ry, rx + rw, ry + rh);
                    else
                    {
                        drawer.DrawRectangle(rx, ry, rw, rh);

                        bool isInvalid = width < 0 || height < 0;
                        if (!isInvalid)
                        {
                            drawer.FillRectangle(rx, ry, rw, rh);
                        }
                        else
                        {
                            drawer.DrawLine(rx, ry, rx + rw, ry + rh);
                            drawer.DrawLine(rx + rw, ry, rx, ry + rh);
                        }
                    }
                }
                else // Radian, Degree
                {
                    if (rw == 0 && rh == 0)
                        drawer.DrawPeriodicPoint(cs, Min, box, traits.Unit);
                    else if (rw == 0 || rh == 0)
                    {
                        Drawer.PeriodicDrawableBox pd = new Drawer.PeriodicDrawableBox(cs, new Geometry.Segment(Min, Max), box, traits.Unit);
                        drawer.DrawPeriodic(pd, false, false, false, settings.showDir);
                    }
                    else
                    {
                        Geometry.Ring ring = new Geometry.Ring();
                        ring.Add(new Geometry.Point(Min[0], Min[1]));
                        ring.Add(new Geometry.Point(Max[0], Min[1]));
                        ring.Add(new Geometry.Point(Max[0], Max[1]));
                        ring.Add(new Geometry.Point(Min[0], Max[1]));
                        Drawer.PeriodicDrawableBox pd = new Drawer.PeriodicDrawableBox(cs, ring, box, traits.Unit);
                        drawer.DrawPeriodic(pd, true, true, false, settings.showDir);
                    }
                }
            }

            public Geometry.Box Aabb(Geometry.Traits traits, bool calculateEnvelope)
            {
                return calculateEnvelope
                     ? Geometry.Envelope(this, traits)
                     : Geometry.Aabb(this.Min, this.Max, traits.Unit);
            }

            public Color DefaultColor(Colors colors) { return colors.BoxColor; }
        }

        public class NSphere : Geometry.NSphere, IDrawable
        {
            public NSphere(Geometry.Point center, double radius)
                : base(center, radius)
            { }

            public void Draw(Geometry.Box box, Graphics graphics, Settings settings, Geometry.Traits traits)
            {
                LocalCS cs = new LocalCS(box, graphics);
                Drawer drawer = new Drawer(graphics, settings.color);
                
                float rx = cs.ConvertDimensionX(Radius);
                float ry = cs.ConvertDimensionY(Radius);

                if (rx < 0 || ry < 0)
                    return;

                if (traits.Unit == Geometry.Unit.None)
                {
                    PointF c = cs.Convert(Center);
                    if (rx == 0 || ry == 0)
                        drawer.DrawPoint(c.X, c.Y);
                    else
                    {
                        float x = c.X - rx;
                        float y = c.Y - ry;
                        float w = rx * 2;
                        float h = ry * 2;
                        drawer.DrawEllipse(x, y, w, h);
                        drawer.FillEllipse(x, y, w, h);
                    }
                }
                else // Radian, Degree
                {
                    if (rx == 0 || ry == 0)
                        drawer.DrawPeriodicPoint(cs, Center, box, traits.Unit);
                    else
                    {
                        Drawer.PeriodicDrawableNSphere pd = new Drawer.PeriodicDrawableNSphere(cs, this, box, traits.Unit);
                        drawer.DrawPeriodic(pd, true, true, false, settings.showDir);
                    }
                }
            }

            public Geometry.Box Aabb(Geometry.Traits traits, bool calculateEnvelope)
            {
                Geometry.Point p_min = new Geometry.Point(Center[0] - Radius, Center[1] - Radius);
                Geometry.Point p_max = new Geometry.Point(Center[0] + Radius, Center[1] + Radius);
                return calculateEnvelope
                     ? Geometry.Envelope(p_min, p_max, traits)
                     : Geometry.Aabb(p_min, p_max, traits.Unit);
            }

            public Color DefaultColor(Colors colors) { return colors.NSphereColor; }
        }

        public class Segment : Geometry.Segment, IDrawable
        {
            public Segment(Geometry.Point first, Geometry.Point second)
                : base(first, second)
            {}

            public void Draw(Geometry.Box box, Graphics graphics, Settings settings, Geometry.Traits traits)
            {
                LocalCS cs = new LocalCS(box, graphics);
                Drawer drawer = new Drawer(graphics, settings.color);
                
                if (traits.Unit == Geometry.Unit.None)
                {
                    PointF p0 = cs.Convert(this[0]);
                    PointF p1 = cs.Convert(this[1]);
                    drawer.DrawLine(p0, p1, settings.showDir);
                }
                else // Radian, Degree
                {
                    Drawer.PeriodicDrawableRange pd = new Drawer.PeriodicDrawableRange(cs, this, box, traits.Unit);
                    drawer.DrawPeriodic(pd, false, false, settings.showDir, settings.showDir);
                }
            }

            public Geometry.Box Aabb(Geometry.Traits traits, bool calculateEnvelope)
            {
                return calculateEnvelope
                     ? Geometry.Envelope(this, traits)
                     : Geometry.Aabb(this, traits);
            }

            public Color DefaultColor(Colors colors) { return colors.SegmentColor; }
        }

        private static void DrawLinestring(Geometry.Linestring linestring, Geometry.Box box, Graphics graphics, Settings settings, Geometry.Traits traits)
        {
            LocalCS cs = new LocalCS(box, graphics);
            Drawer drawer = new Drawer(graphics, settings.color);

            if (traits.Unit == Geometry.Unit.None)
            {
                for (int i = 1; i < linestring.Count; ++i)
                {
                    PointF p0 = cs.Convert(linestring[i - 1]);
                    PointF p1 = cs.Convert(linestring[i]);
                    drawer.DrawLine(p0, p1, settings.showDir);
                }
            }
            else // Radian, Degree
            {
                Drawer.PeriodicDrawableRange pd = new Drawer.PeriodicDrawableRange(cs, linestring, box, traits.Unit);
                drawer.DrawPeriodic(pd, false, false, settings.showDir, settings.showDir);
            }
        }

        private static Geometry.Box AabbRange(Geometry.IRandomAccessRange<Geometry.Point> rng, Geometry.Traits traits, bool calculateEnvelope)
        {
            return calculateEnvelope
                 ? Geometry.Envelope(rng, traits)
                 : Geometry.Aabb(rng, traits.Unit);
        }

        public class Linestring : Geometry.Linestring, IDrawable
        {
            public void Draw(Geometry.Box box, Graphics graphics, Settings settings, Geometry.Traits traits)
            {
                DrawLinestring(this, box, graphics, settings, traits);
            }

            public Geometry.Box Aabb(Geometry.Traits traits, bool calculateEnvelope)
            {
                return AabbRange(this, traits, calculateEnvelope);
            }

            public Color DefaultColor(Colors colors) { return colors.LinestringColor; }
        }

        public class Ring : Geometry.Ring, IDrawable
        {
            public void Draw(Geometry.Box box, Graphics graphics, Settings settings, Geometry.Traits traits)
            {
                LocalCS cs = new LocalCS(box, graphics);
                Drawer drawer = new Drawer(graphics, settings.color);
                
                if (traits.Unit == Geometry.Unit.None)
                {
                    PointF[] dst_points = cs.Convert(this);

                    if (dst_points != null)
                    {
                        drawer.FillPolygon(dst_points);
                        drawer.DrawPolygon(dst_points);

                        if (settings.showDir)
                        {
                            drawer.DrawDirs(dst_points, true);
                            drawer.DrawPoint(dst_points[0].X, dst_points[0].Y);
                        }
                    }
                }
                else
                {
                    Drawer.PeriodicDrawableRange pd = new Drawer.PeriodicDrawableRange(cs, this, box, traits.Unit);
                    drawer.DrawPeriodic(pd, true, true, settings.showDir, settings.showDir);
                    
                    if (settings.showDir && this.Count > 0)
                        drawer.DrawPeriodicPoint(cs, this[0], box, traits.Unit);
                }
            }

            public Geometry.Box Aabb(Geometry.Traits traits, bool calculateEnvelope)
            {
                return AabbRange(this, traits, calculateEnvelope);
            }

            public Color DefaultColor(Colors colors) { return colors.RingColor; }
        }

        private static void DrawPolygon(Geometry.Polygon polygon, Geometry.Box box, Graphics graphics, Settings settings, Geometry.Traits traits)
        {
            LocalCS cs = new LocalCS(box, graphics);
            Drawer drawer = new Drawer(graphics, settings.color);

            if (traits.Unit == Geometry.Unit.None)
            {
                PointF[] dst_outer_points = cs.Convert(polygon.Outer);
                if (dst_outer_points != null)
                {
                    GraphicsPath gp = new GraphicsPath();
                    gp.AddPolygon(dst_outer_points);

                    if (settings.showDir)
                    {
                        drawer.DrawDirs(dst_outer_points, true);
                        drawer.DrawPoint(dst_outer_points[0]);
                    }

                    foreach (Ring inner in polygon.Inners)
                    {
                        PointF[] dst_inner_points = cs.Convert(inner);
                        if (dst_inner_points != null)
                        {
                            gp.AddPolygon(dst_inner_points);

                            if (settings.showDir)
                            {
                                drawer.DrawDirs(dst_inner_points, true);
                                drawer.DrawPoint(dst_inner_points[0]);
                            }
                        }
                    }

                    drawer.FillPath(gp);
                    drawer.DrawPath(gp);
                }
            }
            else
            {
                Drawer.PeriodicDrawablePolygon pd = new Drawer.PeriodicDrawablePolygon(cs, polygon.Outer, polygon.Inners, box, traits.Unit);
                drawer.DrawPeriodic(pd, true, true, settings.showDir, settings.showDir);

                if (settings.showDir)
                {
                    if (settings.showDir && polygon.Outer.Count > 0)
                        drawer.DrawPeriodicPoint(cs, polygon.Outer[0], box, traits.Unit);

                    foreach (Ring inner in polygon.Inners)
                        if (inner.Count > 0)
                            drawer.DrawPeriodicPoint(cs, inner[0], box, traits.Unit);
                }
            }
        }

        private static Geometry.Box AabbPolygon(Geometry.Polygon poly, Geometry.Traits traits, bool calculateEnvelope)
        {
            Geometry.Box result = AabbRange(poly.Outer, traits, calculateEnvelope);

            foreach (Geometry.Ring inner in poly.Inners)
            {
                Geometry.Box aabb = AabbRange(inner, traits, calculateEnvelope);
                if (calculateEnvelope)
                    Geometry.Expand(result, aabb, traits);
                else
                    Geometry.Expand(result, aabb);
            }

            return result;
        }

        public class Polygon : Geometry.Polygon, IDrawable
        {
            public Polygon()
            { }

            public Polygon(Geometry.Ring outer)
            {
                this.outer = outer;
            }

            public Polygon(Geometry.Ring outer, List<Geometry.Ring> inners)
            {
                this.outer = outer;
                this.inners = inners;
            }

            public void Draw(Geometry.Box box, Graphics graphics, Settings settings, Geometry.Traits traits)
            {
                DrawPolygon(this, box, graphics, settings, traits);
            }

            public Geometry.Box Aabb(Geometry.Traits traits, bool calculateEnvelope)
            {
                return AabbPolygon(this, traits, calculateEnvelope);
            }

            public Color DefaultColor(Colors colors) { return colors.PolygonColor; }
        }

        public class MultiPoint : Geometry.MultiPoint, IDrawable
        {
            public void Draw(Geometry.Box box, Graphics graphics, Settings settings, Geometry.Traits traits)
            {
                for (int i = 0; i < this.Count; ++i)
                {
                    DrawPoint(this[i], box, graphics, settings, traits);
                }
            }

            public Geometry.Box Aabb(Geometry.Traits traits, bool calculateEnvelope)
            {
                Geometry.Box box = null;

                for (int i = 0; i < this.Count; ++i)
                {
                    Geometry.Point p = this[i];
                    
                    // TODO: in general it's not necessary to create a box here
                    Geometry.Box b = new Geometry.Box(new Geometry.Point(p[0], p[1]),
                                                      new Geometry.Point(p[0], p[1]));

                    if (box == null)
                        box = b;
                    else
                    {
                        if (calculateEnvelope)
                            Geometry.Expand(box, b, traits);
                        else
                            Geometry.Expand(box, b);
                    }
                }

                if (box == null)
                    Geometry.AssignInverse(box);

                return box;
            }

            public Color DefaultColor(Colors colors) { return colors.PointColor; }
        }

        public class MultiLinestring : Geometry.MultiLinestring, IDrawable
        {
            public void Draw(Geometry.Box box, Graphics graphics, Settings settings, Geometry.Traits traits)
            {
                for (int i = 0; i < this.Count; ++i)
                {
                    DrawLinestring(this[i], box, graphics, settings, traits);
                }
            }

            public Geometry.Box Aabb(Geometry.Traits traits, bool calculateEnvelope)
            {
                Geometry.Box box = null;

                for (int i = 0; i < this.Count; ++i)
                {
                    Geometry.Box ls_box = AabbRange(this[i], traits, calculateEnvelope);

                    if (box == null)
                        box = ls_box;
                    else
                    {
                        if (calculateEnvelope)
                            Geometry.Expand(box, ls_box, traits);
                        else
                            Geometry.Expand(box, ls_box);
                    }
                }

                if (box == null)
                    Geometry.AssignInverse(box);

                return box;
            }

            public Color DefaultColor(Colors colors) { return colors.LinestringColor; }
        }

        public class MultiPolygon : Geometry.MultiPolygon, IDrawable
        {
            public void Draw(Geometry.Box box, Graphics graphics, Settings settings, Geometry.Traits traits)
            {
                for (int i = 0; i < this.Count; ++i)
                {
                    DrawPolygon(this[i], box, graphics, settings, traits);
                }
            }

            public Geometry.Box Aabb(Geometry.Traits traits, bool calculateEnvelope)
            {
                Geometry.Box box = null;

                for (int i = 0; i < this.Count; ++i)
                {
                    Geometry.Box poly_box = AabbPolygon(this[i], traits, calculateEnvelope);

                    if (box == null)
                        box = poly_box;
                    else
                    {
                        if (calculateEnvelope)
                            Geometry.Expand(box, poly_box, traits);
                        else
                            Geometry.Expand(box, poly_box);
                    }
                }

                if (box == null)
                    Geometry.AssignInverse(box);

                return box;
            }

            public Color DefaultColor(Colors colors) { return colors.PolygonColor; }
        }

        public class ValuesContainer : IDrawable
        {
            public ValuesContainer(List<double> values)
            {
                this.values = values;
            }

            public void Draw(Geometry.Box box, Graphics graphics, Settings settings, Geometry.Traits traits)
            {
                if (settings.plotType == Settings.PlotType.Point)
                    DrawPoints(box, graphics, settings, traits);
                else if (settings.plotType == Settings.PlotType.Line)
                    DrawLines(box, graphics, settings, traits);
                else
                    DrawBars(box, graphics, settings, traits);
            }

            public void DrawBars(Geometry.Box box, Graphics graphics, Settings settings, Geometry.Traits traits)
            {
                // NOTE: traits == null
                bool fill = true;

                LocalCS cs = new LocalCS(box, graphics, fill);

                float y0 = cs.ConvertY(0);
                float x0 = cs.ConvertX(0);
                float x1 = cs.ConvertX(1);
                float dx = Math.Abs(x1 - x0);
                bool drawLines = dx < 4;

                double i = 0;
                if (drawLines)
                {
                    float penWidth = dx < 2 ? 1 : 2;
                    Pen pen = new Pen(settings.color, penWidth);
                    foreach (double v in values)
                    {
                        float x = cs.ConvertX(i);
                        float y = cs.ConvertY(v);
                        graphics.DrawLine(pen, x, y0, x, y);
                        i += 1;
                    }
                }
                else
                {
                    Drawer drawer = new Drawer(graphics, settings.color);
                    foreach (double v in values)
                    {
                        float x = cs.ConvertX(i);
                        float y = cs.ConvertY(v);
                        float t = Math.Min(y0, y);
                        float h = Math.Abs(y - y0);
                        float xl = dx / 3.0f;
                        float xw = dx * 2.0f / 3.0f;
                        if (h >= 2)
                        {
                            drawer.DrawRectangle(x - xl, t, xw, h);
                            drawer.FillRectangle(x - xl, t, xw, h);
                        }
                        else
                        {
                            drawer.DrawLine(x - xl, t, x + xl, t);
                        }
                        i += 1;
                    }
                }
            }

            public void DrawPoints(Geometry.Box box, Graphics graphics, Settings settings, Geometry.Traits traits)
            {
                // NOTE: traits == null
                bool fill = true;

                LocalCS cs = new LocalCS(box, graphics, fill);

                float x0 = cs.ConvertX(0);
                float x1 = cs.ConvertX(1);
                float dx = Math.Abs(x1 - x0);
                bool drawPts = dx < 4;

                double i = 0;
                if (drawPts)
                {
                    Pen pen = new Pen(settings.color, 2);
                    foreach (double v in values)
                    {
                        float x = cs.ConvertX(i);
                        float y = cs.ConvertY(v);
                        graphics.DrawLine(pen, x, y - 0.5f, x, y + 0.5f);
                        i += 1;
                    }
                }
                else
                {
                    Drawer drawer = new Drawer(graphics, settings.color);
                    foreach (double v in values)
                    {
                        float x = cs.ConvertX(i);
                        float y = cs.ConvertY(v);
                        drawer.DrawPoint(x, y);
                        i += 1;
                    }
                }
            }

            public void DrawLines(Geometry.Box box, Graphics graphics, Settings settings, Geometry.Traits traits)
            {
                // NOTE: traits == null
                bool fill = true;

                LocalCS cs = new LocalCS(box, graphics, fill);

                float x0 = cs.ConvertX(0);
                float x1 = cs.ConvertX(1);
                float dx = Math.Abs(x1 - x0);
                float penWidth = dx < 2 ? 1 : 2;

                Pen pen = new Pen(settings.color, penWidth);

                if (values.Count < 1)
                {
                    return;
                }
                else if (values.Count == 1)
                {
                    float x = cs.ConvertX(0);
                    float y = cs.ConvertY(values[0]);
                    graphics.DrawLine(pen, x, y - 0.5f, x, y + 0.5f);
                }
                else
                {
                    double d = 0;
                    float xp = cs.ConvertX(d);
                    float yp = cs.ConvertY(values[0]);
                    d += 1;
                    for (int i = 1; i < values.Count; ++i)
                    {
                        float x = cs.ConvertX(d);
                        float y = cs.ConvertY(values[i]);
                        graphics.DrawLine(pen, xp, yp, x, y);
                        d += 1;
                        xp = x;
                        yp = y;
                    }
                }
            }

            public void Add(double v) { values.Add(v); }

            public Geometry.Box Aabb(Geometry.Traits traits, bool calculateEnvelope)
            {
                // NOTE: traits == null

                Geometry.Box box = new Geometry.Box();
                Geometry.AssignInverse(box);

                if (values.Count > 0)
                    Geometry.Expand(box, new Point(0.0, 0.0));

                for (int i = 0; i < values.Count; ++i)
                    Geometry.Expand(box, new Point(i, values[i]));

                return box;
            }

            public Color DefaultColor(Colors colors) { return colors.DrawColor; }
            
            private List<double> values;
        }

        public class Turn : IDrawable
        {
            public Turn(Geometry.Point p, char m, char o0, char o1)
            {
                point = p;
                method = m;
                operation0 = o0;
                operation1 = o1;
            }

            public void DrawPoint(Geometry.Box box, Graphics graphics, Settings settings, Geometry.Traits traits)
            {
                ExpressionDrawer.DrawPoint(point, box, graphics, settings, traits);
            }

            public string GetLabel()
            {
                return "" + method + ':' + operation0 + '/' + operation1;
            }

            public System.Drawing.Point GetDrawingPoint(Geometry.Box box, Graphics graphics)
            {
                LocalCS cs = new LocalCS(box, graphics);
                PointF pf = cs.Convert(point);
                return new System.Drawing.Point((int)Math.Round(pf.X), (int)Math.Round(pf.Y));
            }

            public void Draw(Geometry.Box box, Graphics graphics, Settings settings, Geometry.Traits traits)
            {
                DrawPoint(box, graphics, settings, traits);

                if (settings.showLabels)
                {
                    Drawer drawer = new Drawer(graphics, settings.color);
                    SolidBrush text_brush = new SolidBrush(Color.Black);
                    Font font = new Font(new FontFamily(System.Drawing.Text.GenericFontFamilies.SansSerif), 10);

                    string label = this.GetLabel();
                    System.Drawing.Point pi = GetDrawingPoint(box, graphics);

                    graphics.DrawString(label, font, text_brush, pi);
                }
            }

            public Geometry.Box Aabb(Geometry.Traits traits, bool calculateEnvelope)
            {
                return new Geometry.Box(point, point);
            }
            
            public Color DefaultColor(Colors colors) { return colors.PointColor; }

            public Geometry.Point Point { get { return point; } }

            Geometry.Point point;
            char method, operation0, operation1;
        }

        public class TurnsContainer : IDrawable
        {
            public TurnsContainer(List<Turn> turns)
            {
                this.turns = turns;
            }

            public void Draw(Geometry.Box box, Graphics graphics, Settings settings, Geometry.Traits traits)
            {
                LocalCS cs = new LocalCS(box, graphics);
                Drawer drawer = new Drawer(graphics, settings.color);
                SolidBrush text_brush = new SolidBrush(Color.Black);

                Font font = null;
                if (settings.showLabels)
                    font = new Font(new FontFamily(System.Drawing.Text.GenericFontFamilies.SansSerif), 10);

                Dictionary<System.Drawing.Point, string> labelsMap = new Dictionary<System.Drawing.Point, string>();

                int index = 0;
                foreach (Turn turn in turns)
                {
                    turn.DrawPoint(box, graphics, settings, traits);

                    if (settings.showLabels)
                    {
                        System.Drawing.Point pi = turn.GetDrawingPoint(box, graphics);
                        string str = index.ToString() + ' ' + turn.GetLabel();

                        if (!labelsMap.ContainsKey(pi))
                            labelsMap.Add(pi, str);
                        else
                            labelsMap[pi] = labelsMap[pi] + '\n' + str;
                    }
                    ++index;
                }

                foreach(var label in labelsMap)
                    graphics.DrawString(label.Value, font, text_brush, label.Key);
            }

            public Geometry.Box Aabb(Geometry.Traits traits, bool calculateEnvelope)
            {
                Geometry.Box box = new Geometry.Box();
                Geometry.AssignInverse(box);

                for (int i = 0; i < turns.Count; ++i)
                {
                    Geometry.Expand(box, turns[i].Point);
                }

                return box;
            }

            public Color DefaultColor(Colors colors) { return colors.TurnColor; }
            
            private List<Turn> turns;
        }

        // -------------------------------------------------
        // High level loading
        // -------------------------------------------------

        private class DrawablePair
        {
            public DrawablePair(IDrawable drawable, Geometry.Traits traits)
            {
                Drawable = drawable;
                Traits = traits;
            }
            public IDrawable Drawable { get; set; }
            public Geometry.Traits Traits { get; set; }
        }

        class LoadDrawable
        {
            public LoadDrawable(ExpressionLoader el) { expressionLoader = el; }

            virtual public DrawablePair Load(Debugger debugger, string name)
            {
                Geometry.Traits traits = null;
                IDrawable drawable = null;
                expressionLoader.Load(debugger, name, out traits, out drawable);

                return new DrawablePair(drawable, traits);
            }

            protected ExpressionLoader expressionLoader;
        }

        class LoadGeometry : LoadDrawable
        {
            public LoadGeometry(ExpressionLoader el) : base(el) { }

            static ExpressionLoader.GeometryKindConstraint geometriesOnly = new ExpressionLoader.GeometryKindConstraint();

            public override DrawablePair Load(Debugger debugger, string name)
            {
                Geometry.Traits traits = null;
                IDrawable drawable = null;
                expressionLoader.Load(debugger, name, geometriesOnly, out traits, out drawable);
                if (traits == null)
                    drawable = null;

                return new DrawablePair(drawable, traits);
            }
        }

        class LoadPlot : LoadDrawable
        {
            public LoadPlot(ExpressionLoader el) : base(el) { }

            static ExpressionLoader.ContainerKindConstraint containersOnly = new ExpressionLoader.ContainerKindConstraint();

            public override DrawablePair Load(Debugger debugger, string name)
            {
                Geometry.Traits traits = null;
                IDrawable drawable = null;
                expressionLoader.Load(debugger, name, containersOnly, out traits, out drawable);
                return new DrawablePair(drawable, traits);
            }
        }

        // -------------------------------------------------
        // public Draw
        // -------------------------------------------------

        // For GraphicalWatch
        public bool Draw(Graphics graphics, Debugger debugger, string name, Colors colors)
        {
            try
            {
                LoadDrawable loadDrawable = new LoadDrawable(expressionLoader);
                DrawablePair d = loadDrawable.Load(debugger, name);
                if (d.Drawable != null)
                {
                    if (d.Traits != null && d.Traits.CoordinateSystem == Geometry.CoordinateSystem.SphericalPolar)
                    {
                        throw new Exception("This coordinate system is not yet supported.");
                    }
                    
                    Settings settings = new Settings(d.Drawable.DefaultColor(colors));
                    Geometry.Box aabb = d.Drawable.Aabb(d.Traits, true);
                    Geometry.Unit unit = (d.Traits != null) ? d.Traits.Unit : Geometry.Unit.None;
                    bool fill = (d.Traits == null);
                    Drawer.DrawAxes(graphics, aabb, unit, colors, fill);
                    d.Drawable.Draw(aabb, graphics, settings, d.Traits);
                    return true;
                }
            }
            catch(Exception e)
            {
                Drawer.DrawMessage(graphics, e.Message, Color.Red);
            }

            return false;
        }

        // For GeometryWatch and PlotWatch
        Geometry.Box Draw(Graphics graphics, Debugger debugger, LoadDrawable loadDrawable,
                          string[] names, Settings[] settings, Colors colors, ZoomBox zoomBox)
        {
            try
            {
                System.Diagnostics.Debug.Assert(names.Length == settings.Length);

                Geometry.Box box = new Geometry.Box();
                Geometry.AssignInverse(box);
                int drawnCount = 0;
                int count = names.Length;

                DrawablePair[] drawables = new DrawablePair[count];

                HashSet<int> dimensions = new HashSet<int>();
                HashSet<Geometry.CoordinateSystem> csystems = new HashSet<Geometry.CoordinateSystem>();
                HashSet<Geometry.Unit> units = new HashSet<Geometry.Unit>();

                for (int i = 0; i < count; ++i)
                {
                    if (names[i] != null && names[i] != "")
                    {
                        drawables[i] = loadDrawable.Load(debugger, names[i]);

                        if (drawables[i].Drawable != null)
                        {
                            Geometry.Traits traits = drawables[i].Traits;
                            if (traits != null)
                            {
                                dimensions.Add(traits.Dimension);
                                csystems.Add(traits.CoordinateSystem);
                                units.Add(traits.Unit);
                            }

                            Geometry.Box aabb = drawables[i].Drawable.Aabb(traits, false);
                            Geometry.Expand(box, aabb);

                            ++drawnCount;
                        }
                    }
                }

                if (drawnCount > 0)
                {
                    if (csystems.Count > 1)
                    {
                        throw new Exception("Multiple coordinate systems detected.");
                    }
                    if (csystems.Count > 0 && csystems.First() == Geometry.CoordinateSystem.SphericalPolar)
                    {
                        throw new Exception("This coordinate system is not yet supported.");
                    }
                    if (units.Count > 1)
                    {
                        throw new Exception("Multiple units detected.");
                    }

                    Geometry.Traits traits = (dimensions.Count > 0 && csystems.Count > 0 && units.Count > 0)
                                           ? new Geometry.Traits(dimensions.Max(), csystems.First(), units.First())
                                           : null;

                    bool fill = (traits == null);

                    // Fragment of the box
                    if (zoomBox.IsZoomed())
                    {
                        // window coordinates of the box
                        LocalCS cs = new LocalCS(box, graphics, fill);
                        box = cs.BoxFromZoomBox(zoomBox);

                        // TODO: With current approach changing the original box (resize, enlarge, etc.)
                        // may produce wierd results because zoomBox is relative to the original box.
                    }

                    // Aabb
                    Geometry.Unit unit = traits != null ? traits.Unit : Geometry.Unit.None;
                    Drawer.DrawAxes(graphics, box, unit, colors, fill);
                    Drawer.DrawScales(graphics, box, colors, fill);

                    if (traits != null)
                    {
                        // CS info
                        SolidBrush brush = new SolidBrush(colors.TextColor);
                        Font font = new Font(new FontFamily(System.Drawing.Text.GenericFontFamilies.SansSerif), 10);
                        string str = Geometry.Name(csystems.First());
                        if (units.First() != Geometry.Unit.None)
                            str += '[' + Geometry.Name(units.First()) + ']';
                        graphics.DrawString(str, font, brush, 0, 0);
                    }

                    for (int i = 0; i < count; ++i)
                    {
                        if (drawables[i] != null && drawables[i].Drawable != null)
                        {
                            drawables[i].Drawable.Draw(box, graphics, settings[i], traits);
                        }
                    }

                    return box;
                }
            }
            catch (Exception e)
            {
                Drawer.DrawMessage(graphics, e.Message, Color.Red);
            }

            return null;
        }

        public Geometry.Box DrawGeometries(Graphics graphics, Debugger debugger, string[] names, Settings[] settings, Colors colors, ZoomBox zoomBox)
        {
            try
            {
                LoadGeometry loadDrawable = new LoadGeometry(expressionLoader);
                return Draw(graphics, debugger, loadDrawable, names, settings, colors, zoomBox);
            }
            catch (Exception e)
            {
                Drawer.DrawMessage(graphics, e.Message, Color.Red);
            }

            return null;
        }

        public Geometry.Box DrawPlots(Graphics graphics, Debugger debugger, string[] names, Settings[] settings, Colors colors, ZoomBox zoomBox)
        {
            try
            {
                LoadPlot loadDrawable = new LoadPlot(expressionLoader);
                return Draw(graphics, debugger, loadDrawable, names, settings, colors, zoomBox);
            }
            catch (Exception e)
            {
                Drawer.DrawMessage(graphics, e.Message, Color.Red);
            }

            return null;
        }
    }
}
