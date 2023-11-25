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

namespace GraphicalDebugging
{
    class ExpressionDrawer
    {
        private ExpressionDrawer() { }

        // -------------------------------------------------
        // Settings
        // -------------------------------------------------

        public class Settings
        {
            public Settings()
            { }

            public Settings(Color color)
            {
                this.color = color;
            }

            public Settings(System.Windows.Media.Color color)
            {
                this.color = Util.ConvertColor(color);
            }

            public Settings CopyColored(Color color)
            {
                Settings result = MemberwiseClone() as Settings;
                result.color = color;
                return result;
            }

            public Settings CopyColored(System.Windows.Media.Color color)
            {
                return CopyColored(Util.ConvertColor(color));
            }

            public Color color = Color.Empty;
            // GraphicalWatch and GeometryWatch
            public bool showDir = true;
            public bool showLabels = true;
            public bool showDots = true;
            public bool densify = true;
            // GraphicalWatch and PlotWatch
            public bool valuePlot_enableBars = true;
            public bool valuePlot_enableLines = false;
            public bool valuePlot_enablePoints = false;
            public bool pointPlot_enableLines = false;
            public bool pointPlot_enablePoints = true;
            // GraphicalWatch
            public int imageWidth = 100;
            public int imageHeight = 100;
            public bool displayMultiPointsAsPlots = false;
            public bool image_maintainAspectRatio = false;
        }

        // -------------------------------------------------
        // Util
        // -------------------------------------------------

        private static Geometry.Interval RelativeEnvelopeLon(Geometry.IRandomAccessRange<Geometry.Point> points, bool closed, Geometry.Unit unit)
        {
            Geometry.Interval result = null;

            if (points.Count < 1)
            {
                result = Geometry.InversedInterval();
                return result;
            }

            double x0 = points[0][0];
            result = new Geometry.Interval(x0, x0);

            int count = points.Count + (closed ? 1 : 0);
            for (int ii = 1; ii < count; ++ii)
            {
                int i = ii % points.Count;
                double xi = points[i][0];
                double distNorm = Geometry.NormalizedAngleSigned(xi - x0, unit); // [-pi, pi]
                double x1 = x0 + distNorm;
                result.Expand(x1);
                x0 = x1;
            }
            return result;
        }

        public static Geometry.Interval RelativeEnvelopeLon(NSphere nsphere, Geometry.Unit unit)
        {
            double cx = nsphere.Center[0];
            double r = Math.Abs(nsphere.Radius);
            // NOTE: The radius is always in the units of the CS which is technically wrong
            return new Geometry.Interval(cx - r, cx + r);
        }

        public static Geometry.Interval RelativeEnvelopeLon(Geometry.IRandomAccessRange<Geometry.Point> outer,
                                                            IEnumerable<Geometry.IRandomAccessRange<Geometry.Point>> inners,
                                                            Geometry.Unit unit)
        {
            Geometry.Interval result = RelativeEnvelopeLon(outer, true, unit);
            foreach (var inner in inners)
            {
                result.Expand(RelativeEnvelopeLon(inner, true, unit));
            }
            return result;
        }

        // -------------------------------------------------
        // Drawables and drawing
        // -------------------------------------------------

        public interface IDrawable
        {
            void Draw(Geometry.Box box, Graphics graphics, Settings settings, Geometry.Traits traits);
            Geometry.Box Aabb(Geometry.Traits traits, bool calculateEnvelope);

            bool DrawAxes();
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
                drawer.DrawPeriodicPoint(cs, point, box, traits.Unit, settings.showDots);
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
                return new Geometry.Box(this.Clone(), this.Clone());
            }

            public bool DrawAxes()
            {
                return true;
            }
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
                        drawer.DrawPeriodicPoint(cs, Min, box, traits.Unit, settings.showDots);
                    else if (rw == 0 || rh == 0)
                    {
                        Geometry.Segment seg = new Geometry.Segment(Min, Max);
                        Drawer.PeriodicDrawableBox pd = new Drawer.PeriodicDrawableBox(cs, seg, traits.Unit);
                        Geometry.Interval interval = RelativeEnvelopeLon(seg, false, traits.Unit);
                        drawer.DrawPeriodic(cs, box, interval, traits.Unit, pd, false, false, settings.showDots);
                    }
                    else
                    {
                        Geometry.Ring ring = new Geometry.Ring();
                        ring.Add(new Geometry.Point(Min[0], Min[1]));
                        ring.Add(new Geometry.Point(Max[0], Min[1]));
                        ring.Add(new Geometry.Point(Max[0], Max[1]));
                        ring.Add(new Geometry.Point(Min[0], Max[1]));
                        Drawer.PeriodicDrawableBox pd = new Drawer.PeriodicDrawableBox(cs, ring, traits.Unit);
                        Geometry.Interval interval = RelativeEnvelopeLon(ring, true, traits.Unit);
                        drawer.DrawPeriodic(cs, box, interval, traits.Unit, pd, true, false, settings.showDots);
                    }
                }
            }

            public Geometry.Box Aabb(Geometry.Traits traits, bool calculateEnvelope)
            {
                return calculateEnvelope
                     ? Geometry.Envelope(this, traits)
                     : Geometry.Aabb(this.Min, this.Max, traits.Unit);
            }

            public bool DrawAxes()
            {
                return true;
            }
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
                        drawer.DrawPeriodicPoint(cs, Center, box, traits.Unit, settings.showDots);
                    else
                    {
                        Drawer.PeriodicDrawableNSphere pd = new Drawer.PeriodicDrawableNSphere(cs, this, traits.Unit);
                        Geometry.Interval interval = RelativeEnvelopeLon(this, traits.Unit);
                        drawer.DrawPeriodic(cs, box, interval, traits.Unit, pd, true, false, settings.showDots);
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

            public bool DrawAxes()
            {
                return true;
            }
        }

        public class Segment : Geometry.Segment, IDrawable
        {
            public Segment(Geometry.Point first, Geometry.Point second)
                : base(first, second)
            {}

            public virtual void Draw(Geometry.Box box, Graphics graphics, Settings settings, Geometry.Traits traits)
            {
                LocalCS cs = new LocalCS(box, graphics);
                Drawer drawer = new Drawer(graphics, settings.color);
                
                if (traits.Unit == Geometry.Unit.None)
                {
                    PointF p0 = cs.Convert(this[0]);
                    PointF p1 = cs.Convert(this[1]);
                    drawer.DrawLine(p0, p1);
                    if (settings.showDir)
                        drawer.DrawDir(p0, p1);
                }
                else // Radian, Degree
                {
                    Drawer.PeriodicDrawableRange pd = new Drawer.PeriodicDrawableRange(cs, this, false, traits.Unit, settings.densify);
                    Geometry.Interval interval = RelativeEnvelopeLon(this, false, traits.Unit);
                    drawer.DrawPeriodic(cs, box, interval, traits.Unit, pd, false, settings.showDir, settings.showDots);
                }
            }

            public Geometry.Box Aabb(Geometry.Traits traits, bool calculateEnvelope)
            {
                return calculateEnvelope
                     ? Geometry.Envelope(this, traits)
                     : Geometry.Aabb(this, traits);
            }

            public bool DrawAxes()
            {
                return true;
            }
        }

        public class Ray : Segment
        {
            public Ray(Geometry.Point origin, Geometry.Point direction)
                : base(origin, Geometry.Added(origin, direction))
            { }

            public override void Draw(Geometry.Box box, Graphics graphics, Settings settings, Geometry.Traits traits)
            {
                LocalCS cs = new LocalCS(box, graphics);
                Drawer drawer = new Drawer(graphics, settings.color);
                
                if (traits.Unit == Geometry.Unit.None)
                {
                    PointF p0 = cs.Convert(this[0]);
                    PointF p1 = cs.Convert(this[1]);
                    //drawer.DrawPoint(p0);
                    drawer.DrawLine(p0, p1);
                    drawer.DrawDir(p0, p1, Drawer.DirPos.End, false);
                    Geometry.Box b = cs.ViewBox();
                    Geometry.Point pN, pF;
                    if (Geometry.LineBoxIntersection(this[0], this[1], b, out pN, out pF))
                        drawer.DrawLine(p1, cs.Convert(pF), true, true);
                }
            }
        }

        public class Line : Segment
        {
            public Line(Geometry.Point first, Geometry.Point second)
                : base(first, second)
            { }

            public override void Draw(Geometry.Box box, Graphics graphics, Settings settings, Geometry.Traits traits)
            {
                LocalCS cs = new LocalCS(box, graphics);
                Drawer drawer = new Drawer(graphics, settings.color);

                if (traits.Unit == Geometry.Unit.None)
                {
                    PointF p0 = cs.Convert(this[0]);
                    PointF p1 = cs.Convert(this[1]);
                    //drawer.DrawPoint(p0);
                    drawer.DrawLine(p0, p1);
                    drawer.DrawDir(p0, p1);
                    Geometry.Box b = cs.ViewBox();
                    Geometry.Point pN, pF;
                    if (Geometry.LineBoxIntersection(this[0], this[1], b, out pN, out pF))
                    {
                        drawer.DrawLine(p1, cs.Convert(pF), true, true);
                        drawer.DrawLine(p0, cs.Convert(pN), true, true);
                    }
                }
            }
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
                    drawer.DrawLine(p0, p1);
                    if (settings.showDir)
                        drawer.DrawDir(p0, p1);
                }
            }
            else // Radian, Degree
            {
                Drawer.PeriodicDrawableRange pd = new Drawer.PeriodicDrawableRange(cs, linestring, false, traits.Unit, settings.densify);
                Geometry.Interval interval = RelativeEnvelopeLon(linestring, false, traits.Unit);
                drawer.DrawPeriodic(cs, box, interval, traits.Unit, pd, false, settings.showDir, settings.showDots);
            }
        }

        private static Geometry.Box AabbRange(Geometry.IRandomAccessRange<Geometry.Point> rng, bool closed, Geometry.Traits traits, bool calculateEnvelope)
        {
            return calculateEnvelope
                 ? Geometry.Envelope(rng, closed, traits)
                 : Geometry.Aabb(rng, closed, traits.Unit);
        }

        public class Linestring : Geometry.Linestring, IDrawable
        {
            public void Draw(Geometry.Box box, Graphics graphics, Settings settings, Geometry.Traits traits)
            {
                DrawLinestring(this, box, graphics, settings, traits);
            }

            public Geometry.Box Aabb(Geometry.Traits traits, bool calculateEnvelope)
            {
                return AabbRange(this, false, traits, calculateEnvelope);
            }

            public bool DrawAxes()
            {
                return true;
            }
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
                    Drawer.PeriodicDrawableRange pd = new Drawer.PeriodicDrawableRange(cs, this, true, traits.Unit, settings.densify);
                    Geometry.Interval interval = RelativeEnvelopeLon(this, true, traits.Unit);
                    drawer.DrawPeriodic(cs, box, interval, traits.Unit, pd, true, settings.showDir, settings.showDots);
                    
                    if (settings.showDir && this.Count > 0)
                        drawer.DrawPeriodicPoint(cs, this[0], box, traits.Unit, settings.showDots);
                }
            }

            public Geometry.Box Aabb(Geometry.Traits traits, bool calculateEnvelope)
            {
                return AabbRange(this, true, traits, calculateEnvelope);
            }

            public bool DrawAxes()
            {
                return true;
            }
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
                Drawer.PeriodicDrawablePolygon pd = new Drawer.PeriodicDrawablePolygon(cs, polygon.Outer, polygon.Inners, traits.Unit, settings.densify);
                Geometry.Interval interval = RelativeEnvelopeLon(polygon.Outer, polygon.Inners, traits.Unit);
                drawer.DrawPeriodic(cs, box, interval, traits.Unit, pd, true, settings.showDir, settings.showDots);

                if (settings.showDir)
                {
                    if (settings.showDir && polygon.Outer.Count > 0)
                        drawer.DrawPeriodicPoint(cs, polygon.Outer[0], box, traits.Unit, settings.showDots);

                    foreach (Ring inner in polygon.Inners)
                        if (inner.Count > 0)
                            drawer.DrawPeriodicPoint(cs, inner[0], box, traits.Unit, settings.showDots);
                }
            }
        }

        private static Geometry.Box AabbPolygon(Geometry.Polygon poly, Geometry.Traits traits, bool calculateEnvelope)
        {
            Geometry.Box result = AabbRange(poly.Outer, true, traits, calculateEnvelope);

            foreach (Geometry.Ring inner in poly.Inners)
            {
                Geometry.Box aabb = AabbRange(inner, true, traits, calculateEnvelope);
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

            public bool DrawAxes()
            {
                return true;
            }
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
                    box = Geometry.InversedBox();

                return box;
            }

            public bool DrawAxes()
            {
                return true;
            }
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
                    Geometry.Box ls_box = AabbRange(this[i], false, traits, calculateEnvelope);

                    if (Geometry.InversedBox().Equals(ls_box))
                        continue;

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
                    box = Geometry.InversedBox();

                return box;
            }

            public bool DrawAxes()
            {
                return true;
            }
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

                    if (Geometry.InversedBox().Equals(poly_box))
                        continue;

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
                    box = Geometry.InversedBox();

                return box;
            }

            public bool DrawAxes()
            {
                return true;
            }
        }

        public class DrawablesContainer : List<IDrawable>, IDrawable
        {
            public void Draw(Geometry.Box box, Graphics graphics, Settings settings, Geometry.Traits traits)
            {
                for (int i = 0; i < this.Count; ++i)
                {
                    this[i].Draw(box, graphics, settings, traits);
                }
            }

            // TODO: This overload is defined to allow drawing elements using their specific colors
            //       Incorporate this into the IDrawable interface?
            public void Draw(Geometry.Box box, Graphics graphics, Settings settings, Geometry.Traits traits, Colors colors)
            {
                for (int i = 0; i < this.Count; ++i)
                {
                    IDrawable drawable = this[i];
                    settings.color = DefaultColor(drawable, colors);
                    drawable.Draw(box, graphics, settings, traits);
                }
            }

            public Geometry.Box Aabb(Geometry.Traits traits, bool calculateEnvelope)
            {
                Geometry.Box result = null;

                for (int i = 0; i < this.Count; ++i)
                {
                    Geometry.Box box = this[i].Aabb(traits, calculateEnvelope);

                    if (Geometry.InversedBox().Equals(box))
                        continue;

                    if (result == null)
                        result = box;
                    else
                    {
                        if (calculateEnvelope)
                            Geometry.Expand(result, box, traits);
                        else
                            Geometry.Expand(result, box);
                    }
                }

                if (result == null)
                    result = Geometry.InversedBox();

                return result;
            }

            public bool DrawAxes()
            {
                return true;
            }
        }

        public class ValuesContainer : IDrawable
        {
            public ValuesContainer(List<double> values)
            {
                this.values = values;
            }

            public void Draw(Geometry.Box box, Graphics graphics, Settings settings, Geometry.Traits traits)
            {
                if (settings.valuePlot_enableBars)
                    DrawBars(box, graphics, settings, traits);

                if (settings.valuePlot_enableLines)
                    DrawLines(box, graphics, settings, traits);

                if (settings.valuePlot_enablePoints)
                    DrawPoints(box, graphics, settings, traits);
            }

            void DrawBars(Geometry.Box box, Graphics graphics, Settings settings, Geometry.Traits traits)
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

            void DrawPoints(Geometry.Box box, Graphics graphics, Settings settings, Geometry.Traits traits)
            {
                // NOTE: traits == null
                bool fill = true;

                LocalCS cs = new LocalCS(box, graphics, fill);

                float x0 = cs.ConvertX(0);
                float x1 = cs.ConvertX(1);
                float dx = Math.Abs(x1 - x0);
                float s = Math.Min(Math.Max(dx * 2.0f, 2.0f), 5.0f);

                double i = 0;
                Drawer drawer = new Drawer(graphics, settings.color);
                foreach (double v in values)
                {
                    float x = cs.ConvertX(i);
                    float y = cs.ConvertY(v);
                    drawer.DrawPoint(x, y, s);
                    i += 1;
                }
            }

            void DrawLines(Geometry.Box box, Graphics graphics, Settings settings, Geometry.Traits traits)
            {
                // NOTE: traits == null
                bool fill = true;

                LocalCS cs = new LocalCS(box, graphics, fill);

                //float x0 = cs.ConvertX(0);
                //float x1 = cs.ConvertX(1);
                //float dx = Math.Abs(x1 - x0);
                //float s = Math.Min(Math.Max(dx, 1.0f), 2.0f);

                Drawer drawer = new Drawer(graphics, settings.color);

                if (values.Count == 1)
                {
                    float x = cs.ConvertX(0);
                    float y = cs.ConvertY(values[0]);
                    drawer.DrawLine(x, y - 0.5f, x, y + 0.5f);
                }
                else if (values.Count > 1)
                {
                    double d = 0;
                    float xp = cs.ConvertX(d);
                    float yp = cs.ConvertY(values[0]);
                    d += 1;
                    for (int i = 1; i < values.Count; ++i)
                    {
                        float x = cs.ConvertX(d);
                        float y = cs.ConvertY(values[i]);
                        drawer.DrawLine(xp, yp, x, y);
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

                Geometry.Box box = Geometry.InversedBox();

                if (values.Count > 0)
                    Geometry.Expand(box, new Point(0.0, 0.0));

                for (int i = 0; i < values.Count; ++i)
                    Geometry.Expand(box, new Point(i, values[i]));

                return box;
            }

            public bool DrawAxes()
            {
                return true;
            }

            private List<double> values;
        }

        public class PointsContainer : IDrawable
        {
            public PointsContainer(MultiPoint points)
            {
                this.points = points;
            }

            public MultiPoint MultiPoint { get { return points; } }

            public void Draw(Geometry.Box box, Graphics graphics, Settings settings, Geometry.Traits traits)
            {
                // Esstimate the distance between samples, uniform distribution, 2 samples per X
                Geometry.Box originalBox = this.Aabb(traits, false);
                int count = Math.Max(points.Count, 1);
                double diffX = Math.Abs(originalBox.Dim(0)) / count * 2.0;

                if (settings.pointPlot_enableLines)
                    DrawLines(box, graphics, settings, traits, diffX);

                if (settings.pointPlot_enablePoints)
                    DrawPoints(box, graphics, settings, traits, diffX);
            }

            void DrawPoints(Geometry.Box box, Graphics graphics, Settings settings, Geometry.Traits traits, double diffX)
            {
                // NOTE: traits == null
                bool fill = true;

                LocalCS cs = new LocalCS(box, graphics, fill);

                float dx = cs.ConvertDimensionX(diffX);
                float s = Math.Min(Math.Max(dx * 2.0f, 2.0f), 5.0f);
                bool drawPts = dx < 1;

                Drawer drawer = new Drawer(graphics, settings.color);
                for (int i = 0; i < points.Count; ++i)
                {
                    float x = cs.ConvertX(points[i][0]);
                    float y = cs.ConvertY(points[i][1]);
                    drawer.DrawPoint(x, y, s);
                }
            }

            void DrawLines(Geometry.Box box, Graphics graphics, Settings settings, Geometry.Traits traits, double diffX)
            {
                // NOTE: traits == null
                bool fill = true;

                LocalCS cs = new LocalCS(box, graphics, fill);

                //float dx = cs.ConvertDimensionX(diffX);
                //float s = Math.Min(Math.Max(dx, 1.0f), 2.0f);

                Drawer drawer = new Drawer(graphics, settings.color);

                if (points.Count == 1)
                {
                    float x = cs.ConvertX(points[0][0]);
                    float y = cs.ConvertY(points[0][1]);
                    drawer.DrawLine(x, y - 0.5f, x, y + 0.5f);
                }
                else if (points.Count > 1)
                {
                    float xp = cs.ConvertX(points[0][0]);
                    float yp = cs.ConvertY(points[0][1]);
                    for (int i = 1; i < points.Count; ++i)
                    {
                        float x = cs.ConvertX(points[i][0]);
                        float y = cs.ConvertY(points[i][1]);
                        drawer.DrawLine(xp, yp, x, y);
                        xp = x;
                        yp = y;
                    }
                }
            }

            public Geometry.Box Aabb(Geometry.Traits traits, bool calculateEnvelope)
            {
                // NOTE: traits == null
                // so don't return points.Aabb(traits, calculateEnvelope)

                Geometry.Box box = Geometry.InversedBox();

                for (int i = 0; i < points.Count; ++i)
                    Geometry.Expand(box, points[i]);

                return box;
            }

            public bool DrawAxes()
            {
                return true;
            }

            private MultiPoint points;
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

            public bool DrawAxes()
            {
                return true;
            }

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
                Geometry.Box box = Geometry.InversedBox();

                for (int i = 0; i < turns.Count; ++i)
                {
                    Geometry.Expand(box, turns[i].Point);
                }

                return box;
            }

            public bool DrawAxes()
            {
                return true;
            }

            private List<Turn> turns;
        }

        public class Image : IDrawable
        {
            public Image(System.Drawing.Image image)
            {
                this.image = image;
            }

            public void Draw(Geometry.Box box, Graphics graphics, Settings settings, Geometry.Traits traits)
            {
                // TODO: Axes shouldn't be drawn
                //       The size should be picked so the image pixels are least distorted,
                //       taking into account the VisibleClipBounds and size of image.
                // TODO: should probably be set globally based on settings

                graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
                graphics.PixelOffsetMode = PixelOffsetMode.Half;

                RectangleF rect = graphics.VisibleClipBounds;

                if (settings.image_maintainAspectRatio)
                {
                    float wr = (float)rect.Width / image.Width;
                    float hr = (float)rect.Height / image.Height;
                    float r = Math.Min(wr, hr); // r < 1 <=> downscale
                    rect.Width = image.Width * r;
                    rect.Height = image.Height * r;
                }

                rect.X = (float)Math.Floor(Math.Max(graphics.VisibleClipBounds.Width - rect.Width, 0.0f) / 2.0f);
                rect.Y = (float)Math.Floor(Math.Max(graphics.VisibleClipBounds.Height - rect.Height, 0.0f) / 2.0f);

                graphics.DrawImage(image, rect);
            }

            public Geometry.Box Aabb(Geometry.Traits traits, bool calculateEnvelope)
            {
                return new Geometry.Box(new Geometry.Point(-1, -1),
                                        new Geometry.Point(1, 1));
            }

            public bool DrawAxes()
            {
                return false;
            }

            System.Drawing.Image image;
        }

        // -------------------------------------------------
        // Drawing
        // -------------------------------------------------

        static Color DefaultColor(IDrawable drawable, Colors colors)
        {
            if (drawable is Point)
                return colors.PointColor;
            else if (drawable is Box)
                return colors.BoxColor;
            else if (drawable is NSphere)
                return colors.NSphereColor;
            else if (drawable is Segment) // Ray, Line
                return colors.SegmentColor;
            else if (drawable is Linestring)
                return colors.LinestringColor;
            else if (drawable is Ring)
                return colors.RingColor;
            else if (drawable is Polygon)
                return colors.PolygonColor;
            else if (drawable is MultiPoint)
                return colors.MultiPointColor;
            else if (drawable is MultiLinestring)
                return colors.MultiLinestringColor;
            else if (drawable is MultiPolygon)
                return colors.MultiPolygonColor;
            else if (drawable is Turn || drawable is TurnsContainer)
                return colors.TurnColor;
            else
                return colors.DrawColor;
        }

        // For GraphicalWatch
        public static bool Draw(Graphics graphics,
                                IDrawable drawable, Geometry.Traits traits,
                                Settings settings, Colors colors)
        {
            if (drawable == null)
                return false;

            if (traits != null && traits.CoordinateSystem == Geometry.CoordinateSystem.SphericalPolar)
                throw new Exception("This coordinate system is not yet supported.");

            if (settings.color == Color.Empty)
                settings.color = DefaultColor(drawable, colors);

            Geometry.Box aabb = drawable.Aabb(traits, true);
            if (aabb.IsValid())
            {
                Geometry.Unit unit = (traits != null) ? traits.Unit : Geometry.Unit.None;
                bool fill = (traits == null);
                if (drawable.DrawAxes())
                    Drawer.DrawAxes(graphics, aabb, unit, colors, fill);
                // TODO: This is ugly, it should probably be changed
                if (drawable is DrawablesContainer)
                    (drawable as DrawablesContainer).Draw(aabb, graphics, settings, traits, colors);
                else
                    drawable.Draw(aabb, graphics, settings, traits);
            }
            return true;
        }

        // For GeometryWatch and PlotWatch
        static Geometry.Box Draw(Graphics graphics, bool ignoreTraits,
                                 IDrawable[] drawables, Geometry.Traits[] traits,
                                 Settings[] settings, Colors colors, ZoomBox zoomBox)
        {
            if (drawables.Length != traits.Length || drawables.Length != settings.Length)
                throw new ArgumentOutOfRangeException("drawables.Length, traits.Length, settings.Length");

            Geometry.Box box = Geometry.InversedBox();

            int drawnCount = 0;
            int count = drawables.Length;
            bool[] drawnFlags = new bool[count];

            HashSet<int> dimensions = new HashSet<int>();
            HashSet<Geometry.CoordinateSystem> csystems = new HashSet<Geometry.CoordinateSystem>();
            HashSet<Geometry.Unit> units = new HashSet<Geometry.Unit>();

            for (int i = 0; i < count; ++i)
            {
                if (ignoreTraits)
                    traits[i] = null;

                if (drawables[i] != null)
                {
                    if (traits[i] != null)
                    {
                        dimensions.Add(traits[i].Dimension);
                        csystems.Add(traits[i].CoordinateSystem);
                        units.Add(traits[i].Unit);
                    }

                    Geometry.Box aabb = drawables[i].Aabb(traits[i], false);
                    Geometry.Expand(box, aabb);

                    ++drawnCount;
                    drawnFlags[i] = aabb.IsValid();
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

                Geometry.Traits commonTraits = (dimensions.Count > 0 && csystems.Count > 0 && units.Count > 0)
                                                ? new Geometry.Traits(dimensions.Max(), csystems.First(), units.First())
                                                : null;

                bool fill = (commonTraits == null);

                // Fragment of the box
                if (box.IsValid() && zoomBox.IsZoomed())
                {
                    // window coordinates of the box
                    LocalCS cs = new LocalCS(box, graphics, fill);
                    box = cs.BoxFromZoomBox(zoomBox);

                    // TODO: With current approach changing the original box (resize, enlarge, etc.)
                    // may produce wierd results because zoomBox is relative to the original box.
                }

                // Axes
                if (box.IsValid())
                {
                    Geometry.Unit unit = commonTraits != null ? commonTraits.Unit : Geometry.Unit.None;
                    Drawer.DrawAxes(graphics, box, unit, colors, fill);
                }
                    
                // Drawables
                for (int i = 0; i < count; ++i)
                {
                    if (drawables[i] != null && drawnFlags[i] == true)
                    {
                        drawables[i].Draw(box, graphics, settings[i], commonTraits);
                    }
                }

                // Scales
                if (box.IsValid())
                {
                    Drawer.DrawScales(graphics, box, colors, fill);
                }

                // CS info
                if (commonTraits != null)
                {
                    SolidBrush brush = new SolidBrush(colors.TextColor);
                    Font font = new Font(new FontFamily(System.Drawing.Text.GenericFontFamilies.SansSerif), 10);
                    string str = Geometry.Name(csystems.First());
                    if (units.First() != Geometry.Unit.None)
                        str += '[' + Geometry.Name(units.First()) + ']';
                    graphics.DrawString(str, font, brush, 0, 0);
                }

                return box;
            }

            return null;
        }

        // For GeometryWatch
        public static Geometry.Box DrawGeometries(Graphics graphics,
                                                  IDrawable[] drawables, Geometry.Traits[] traits,
                                                  Settings[] settings, Colors colors, ZoomBox zoomBox)
        {
            return Draw(graphics, false, drawables, traits, settings, colors, zoomBox);
        }

        // For PlotWatch
        public static Geometry.Box DrawPlots(Graphics graphics,
                                             IDrawable[] drawables, Geometry.Traits[] traits,
                                             Settings[] settings, Colors colors, ZoomBox zoomBox)
        {
            return Draw(graphics, true, drawables, traits, settings, colors, zoomBox);
        }

        public static void DrawErrorMessage(Graphics graphics, string message)
        {
            Drawer.DrawMessage(graphics, message, Color.Red);
        }
    }
}
