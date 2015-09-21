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
        // Settings
        // -------------------------------------------------

        public class Settings
        {
            public Settings()
                : this(Color.Black)
            { }

            public Settings(Color color, bool showDir = false, bool showLabels = false)
            {
                this.color = color;
                this.showDir = showDir;
                this.showLabels = showLabels;
            }

            public Color color;
            public bool showDir;
            public bool showLabels;
        }

        // -------------------------------------------------
        // Drawables and drawing
        // -------------------------------------------------

        public interface IDrawable
        {
            void Draw(Geometry.Box box, Graphics graphics, Settings settings, Geometry.Traits traits);
            Geometry.Box Aabb { get; }
            Color DefaultColor { get; }
        }

        private class Point : Geometry.Point, IDrawable
        {
            public Point(double x, double y)
                : base(x, y)
            { }

            public void Draw(Geometry.Box box, Graphics graphics, Settings settings, Geometry.Traits traits)
            {
                LocalCS cs = new LocalCS(box, graphics);

                Pen pen = new Pen(Color.FromArgb(112, settings.color), 2);
                SolidBrush brush = new SolidBrush(Color.FromArgb(64, settings.color));

                float x = cs.ConvertX(this[0]);
                float y = cs.ConvertY(this[1]);
                DrawCircle(graphics, pen, brush, x, y, 2.5f);
                
                // Radian, Degree
                if (traits.Unit != Geometry.Unit.None)
                {
                    pen.DashStyle = DashStyle.Dash;
                    double pi2 = 2 * Geometry.HalfAngle(traits.Unit);
                    // draw points on the west
                    double x_tmp = this[0] - pi2;
                    while(x_tmp >= box.Min[0])
                    {
                        x = cs.ConvertX(x_tmp);
                        DrawCircle(graphics, pen, brush, x, y, 2.5f);
                        x_tmp -= pi2;
                    }
                    // draw points on the east
                    x_tmp = this[0] + pi2;
                    while (x_tmp <= box.Max[0])
                    {
                        x = cs.ConvertX(x_tmp);
                        DrawCircle(graphics, pen, brush, x, y, 2.5f);
                        x_tmp += pi2;
                    }
                }
            }

            public Geometry.Box Aabb { get {
                    return new Geometry.Box(this, this);
                } }

            public Color DefaultColor { get { return Color.Orange; } }
        }

        private class Box : Geometry.Box, IDrawable
        {
            public Box()
            { }

            public Box(Geometry.Point min, Geometry.Point max)
                : base(min, max)
            { }

            public void Draw(Geometry.Box box, Graphics graphics, Settings settings, Geometry.Traits traits)
            {
                LocalCS cs = new LocalCS(box, graphics);

                Pen pen = new Pen(Color.FromArgb(112, settings.color), 2);
                SolidBrush brush = new SolidBrush(Color.FromArgb(64, settings.color));

                float rx = cs.ConvertX(Math.Min(Min[0], Max[0]));
                float ry = cs.ConvertY(Math.Max(Min[1], Max[1]));
                float rw = cs.ConvertDimension(Math.Abs(Width));
                float rh = cs.ConvertDimension(Math.Abs(Height));

                if (rw == 0 && rh == 0)
                {
                    DrawCircle(graphics, pen, brush, rx, ry, 2.5f);
                }
                else if (rw == 0 || rh == 0)
                {
                    graphics.DrawLine(pen, rx, ry, rx + rw, ry + rh);
                }
                else
                {
                    graphics.DrawRectangle(pen, rx, ry, rw, rh);

                    bool isInvalid = Width < 0 || Height < 0;
                    if (!isInvalid)
                    {
                        graphics.FillRectangle(brush, rx, ry, rw, rh);
                    }
                    else
                    {
                        graphics.DrawLine(pen, rx, ry, rx + rw, ry + rh);
                        graphics.DrawLine(pen, rx + rw, ry, rx, ry + rh);
                    }
                }
            }

            public Geometry.Box Aabb { get {
                    return Geometry.Aabb(Min, Max);
                } }

            public Color DefaultColor { get { return Color.Red; } }
        }

        private class Segment : Geometry.Segment, IDrawable
        {
            public Segment(Geometry.Point first, Geometry.Point second)
                : base(first, second)
            {}

            public void Draw(Geometry.Box box, Graphics graphics, Settings settings, Geometry.Traits traits)
            {
                LocalCS cs = new LocalCS(box, graphics);

                Pen pen = new Pen(Color.FromArgb(112, settings.color), 2);

                PointF p0 = cs.Convert(this[0]);
                PointF p1 = cs.Convert(this[1]);

                if (traits.Unit == Geometry.Unit.None)
                {
                    DrawLine(graphics, pen, p0, p1, settings.showDir);
                }
                else // Radian, Degree
                {
                    double x0 = Geometry.NormalizedAngle(this[0][0], traits.Unit);
                    double x1 = Geometry.NormalizedAngle(this[1][0], traits.Unit);
                    double dist = x1 - x0; // [-2pi, 2pi]
                    double pi = Geometry.HalfAngle(traits.Unit);
                    //bool intersectsAntimeridian = dist < -pi || pi < dist;
                    double distNorm = Geometry.NormalizedAngle(dist, traits.Unit); // [-pi, pi]

                    p0.X = cs.ConvertX(this[0][0]);
                    p1.X = cs.ConvertX(this[0][0] + distNorm);
                    DrawLine(graphics, pen, p0, p1, settings.showDir);

                    //pen.DashStyle = DashStyle.Dash;
                    double pi2 = 2 * pi;
                    // draw lines on the west
                    double x0_tmp = this[0][0] - pi2;
                    double x1_tmp = x0_tmp + distNorm;
                    while (x0_tmp >= box.Min[0] || x1_tmp >= box.Min[0])
                    {
                        p0.X = cs.ConvertX(x0_tmp);
                        p1.X = cs.ConvertX(x1_tmp);
                        DrawLine(graphics, pen, p0, p1, settings.showDir);
                        x0_tmp -= pi2;
                        x1_tmp -= pi2;
                    }
                    // draw lines on the east
                    x0_tmp = this[0][0] + pi2;
                    x1_tmp = x0_tmp + distNorm;
                    while (x0_tmp <= box.Max[0] || x1_tmp <= box.Max[0])
                    {
                        p0.X = cs.ConvertX(x0_tmp);
                        p1.X = cs.ConvertX(x1_tmp);
                        DrawLine(graphics, pen, p0, p1, settings.showDir);
                        x0_tmp += pi2;
                        x1_tmp += pi2;
                    }
                }
            }

            public Geometry.Box Aabb { get { return Box; } }
            public Color DefaultColor { get { return Color.YellowGreen; } }

            public Geometry.Box Box { get; set; }
        }

        private class Linestring : Geometry.Linestring, IDrawable
        {
            public void Draw(Geometry.Box box, Graphics graphics, Settings settings, Geometry.Traits traits)
            {
                LocalCS cs = new LocalCS(box, graphics);

                Pen pen = new Pen(Color.FromArgb(112, settings.color), 2);

                if (traits.Unit == Geometry.Unit.None)
                {
                    for (int i = 1; i < Count; ++i)
                    {
                        PointF p0 = cs.Convert(this[i - 1]);
                        PointF p1 = cs.Convert(this[i]);
                        DrawLine(graphics, pen, p0, p1, settings.showDir);
                    }
                }
                else // Radian, Degree
                {
                    // for arbitrary Geometry
                    // - recalculate the whole geometry and Aabb according to the first point
                    // - do the same as below for all of the points using recalculated Aabb
                    //   to check if a geometry should be drawed for current displacement
                    // - the question remains - how to know which segment shoudl be solid and which dashed?

                    if (Count < 2)
                        return;

                    double pi = Geometry.HalfAngle(traits.Unit);
                    PointF[] points = new PointF[Count];
                    points[0] = cs.Convert(this[0]);
                    float minf = points[0].X;
                    float maxf = points[0].X;
                    float periodf = cs.ConvertDimension(2 * pi);
                    double x0 = Geometry.NormalizedAngle(this[0][0], traits.Unit);
                    double x0_prev = this[0][0];
                    for (int i = 1; i < Count; ++i)
                    {
                        double x1 = Geometry.NormalizedAngle(this[i][0], traits.Unit);
                        double dist = x1 - x0; // [-2pi, 2pi]
                        //bool intersectsAntimeridian = dist < -pi || pi < dist;
                        double distNorm = Geometry.NormalizedAngle(dist, traits.Unit); // [-pi, pi]

                        double x0_curr = x0_prev + distNorm;
                        points[i] = new PointF(cs.ConvertX(x0_curr),
                                               cs.ConvertY(this[i][1]));

                        if (points[i].X < minf)
                            minf = points[i].X;
                        if (points[i].X > maxf)
                            maxf = points[i].X;

                        x0_prev = x0_curr;
                        x0 = x1;
                    }

                    DrawLines(graphics, pen, points, false, settings.showDir);

                    // west
                    float box_minf = cs.ConvertX(box.Min[0]);
                    float maxf_i = maxf;
                    PointF[] transl_points = new PointF[Count];
                    for (int i = 0; i < Count; ++i)
                        transl_points[i] = new PointF(points[i].X, points[i].Y);
                    while (maxf_i >= box_minf)
                    {
                        for (int i = 0; i < Count; ++i)
                            transl_points[i].X -= periodf;

                        DrawLines(graphics, pen, transl_points, false, settings.showDir);
                        maxf_i -= periodf;
                    }
                    // east
                    float box_maxf = cs.ConvertX(box.Max[0]);
                    float minf_i = minf;
                    for (int i = 0; i < Count; ++i)
                        transl_points[i] = new PointF(points[i].X, points[i].Y);
                    while (minf_i <= box_maxf)
                    {
                        for (int i = 0; i < Count; ++i)
                            transl_points[i].X += periodf;

                        DrawLines(graphics, pen, transl_points, false, settings.showDir);
                        minf_i += periodf;
                    }
                }
            }

            public Geometry.Box Aabb { get { return Box; } }
            public Color DefaultColor { get { return Color.Green; } }

            public Geometry.Box Box { get; set; }
        }

        private class Ring : Geometry.Ring, IDrawable
        {
            public Ring(Geometry.Linestring linestring, Geometry.Box box)
            {
                this.linestring = linestring;
                this.Box = box;
            }

            public void Draw(Geometry.Box box, Graphics graphics, Settings settings, Geometry.Traits traits)
            {
                LocalCS cs = new LocalCS(box, graphics);

                PointF[] dst_points = cs.Convert(this);

                if (dst_points != null)
                {
                    Pen pen = new Pen(Color.FromArgb(112, settings.color), 2);
                    SolidBrush brush = new SolidBrush(Color.FromArgb(64, settings.color));
                    
                    graphics.FillPolygon(brush, dst_points);
                    graphics.DrawPolygon(pen, dst_points);

                    if (settings.showDir)
                        DrawDirs(dst_points, true, graphics, pen);
                }
            }

            public Geometry.Box Aabb { get { return Box; } }
            public Color DefaultColor { get { return Color.SlateBlue; } }

            public Geometry.Box Box { get; set; }
        }

        private class Polygon : Geometry.Polygon, IDrawable
        {
            public Polygon(Geometry.Ring outer, List<Geometry.Ring> inners, Geometry.Box box)
            {
                this.outer = outer;
                this.inners = inners;
                this.Box = box;
            }

            public void Draw(Geometry.Box box, Graphics graphics, Settings settings, Geometry.Traits traits)
            {
                LocalCS cs = new LocalCS(box, graphics);

                PointF[] dst_outer_points = cs.Convert(outer);
                if (dst_outer_points != null)
                {
                    Pen pen = new Pen(Color.FromArgb(112, settings.color), 2);
                    SolidBrush brush = new SolidBrush(Color.FromArgb(64, settings.color));

                    GraphicsPath gp = new GraphicsPath();
                    gp.AddPolygon(dst_outer_points);

                    if (settings.showDir)
                        DrawDirs(dst_outer_points, true, graphics, pen);

                    foreach (Ring inner in inners)
                    {
                        PointF[] dst_inner_points = cs.Convert(inner);
                        if (dst_inner_points != null)
                        {
                            gp.AddPolygon(dst_inner_points);

                            if (settings.showDir)
                                DrawDirs(dst_inner_points, true, graphics, pen);
                        }
                    }

                    graphics.FillPath(brush, gp);
                    graphics.DrawPath(pen, gp);
                }
            }

            public Geometry.Box Aabb { get { return Box; } }
            public Color DefaultColor { get { return Color.RoyalBlue; } }

            public Geometry.Box Box { get; set; }
        }

        private class Multi<S> : IDrawable
        {
            public Multi(List<IDrawable> singles, Geometry.Box box)
            {
                this.singles = singles;
                this.Box = box;
            }
            
            public void Draw(Geometry.Box box, Graphics graphics, Settings settings, Geometry.Traits traits)
            {
                foreach (IDrawable single in singles)
                {
                    single.Draw(box, graphics, settings, traits);
                }
            }

            public Geometry.Box Aabb { get { return Box; } }
            public Color DefaultColor { get { return singles.Count > 0 ? singles.First().DefaultColor : Color.Black; } }

            public Geometry.Box Box { get; set; }

            private List<IDrawable> singles;
        }

        private class ValuesContainer : IDrawable
        {
            public ValuesContainer(List<double> values, Geometry.Box box)
            {
                this.values = values;
                this.Box = box;
            }

            public void Draw(Geometry.Box box, Graphics graphics, Settings settings, Geometry.Traits traits)
            {
                LocalCS cs = new LocalCS(box, graphics);

                Pen axis_pen = new Pen(settings.color, 2);
                Pen pen = new Pen(Color.FromArgb(112, settings.color), 1);

                float ax0 = cs.ConvertX(box.Min[0]);
                float ax1 = cs.ConvertX(box.Max[0]);
                float ay = cs.ConvertY(0);
                graphics.DrawLine(axis_pen, ax0, ay, ax1, ay);

                double width = box.Width;
                double step = width / values.Count;
                double i = step * 0.5;
                foreach (double v in values)
                {
                    float x = cs.ConvertX(i);
                    float y = cs.ConvertY(v);
                    graphics.DrawLine(pen, x, ay, x, y);
                    i += step;
                }
            }

            public void Add(double v) { values.Add(v); }

            public Geometry.Box Aabb { get { return Box; } }
            public Color DefaultColor { get { return Color.Black; } }

            public Geometry.Box Box { get; set; }

            private List<double> values;
        }

        private class TurnsContainer : IDrawable
        {
            public TurnsContainer(List<Turn> turns, Geometry.Box box)
            {
                this.turns = turns;
                this.Box = box;
            }

            public class Turn
            {
                public Turn(Geometry.Point p, char m, char o0, char o1)
                {
                    point = p;
                    method = m;
                    operation0 = o0;
                    operation1 = o1;
                }

                public Geometry.Point point;
                public char method, operation0, operation1;
            }

            public void Draw(Geometry.Box box, Graphics graphics, Settings settings, Geometry.Traits traits)
            {
                LocalCS cs = new LocalCS(box, graphics);

                Pen pen = new Pen(Color.FromArgb(112, settings.color), 2);
                SolidBrush brush = new SolidBrush(Color.FromArgb(64, settings.color));
                SolidBrush text_brush = new SolidBrush(Color.Black);

                Font font = null;
                if (settings.showLabels)
                    font = new Font(new FontFamily(System.Drawing.Text.GenericFontFamilies.SansSerif), 10);

                Dictionary<System.Drawing.Point, string> labelsMap = new Dictionary<System.Drawing.Point, string>();

                int index = 0;
                foreach (Turn turn in turns)
                {
                    PointF p = cs.Convert(turn.point);
                    DrawCircle(graphics, pen, brush, p.X, p.Y, 2.5f);

                    if (settings.showLabels)
                    {
                        System.Drawing.Point pi = new System.Drawing.Point((int)Math.Round(p.X), (int)Math.Round(p.Y));
                        string str = index.ToString() + ' ' + turn.method + ':' + turn.operation0 + '/' + turn.operation1;

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

            public Geometry.Box Aabb { get { return Box; } }
            public Color DefaultColor { get { return Color.DarkOrange; } }

            public Geometry.Box Box { get; set; }

            private List<Turn> turns;
        }

        private static bool DrawDir(PointF p0, PointF p1, Graphics graphics, Pen pen)
        {
            PointF v = SubF(p1, p0);
            float distSqr = DotF(v, v);

            if (distSqr < 49.0f) // (1+5+1)^2
                return false;

            PointF ph = AddF(p0, MulF(v, 0.5f));
            float a = AngleF(v);
            PointF ps = AddF(ph, RotF(new PointF(-1.25f, -2.5f), a));
            PointF pm = AddF(ph, RotF(new PointF(1.25f, 0.0f), a));
            PointF pe = AddF(ph, RotF(new PointF(-1.25f, 2.5f), a));
            graphics.DrawLine(pen, pm, ps);
            graphics.DrawLine(pen, pm, pe);

            return true;
        }

        private static void DrawDirs(PointF[] points, bool closed, Graphics graphics, Pen pen)
        {
            bool drawn = false;
            for (int i = 1; i < points.Length; ++i)
            {
                bool ok = DrawDir(points[i - 1], points[i], graphics, pen);
                drawn = drawn || ok;
            }
            if (closed && points.Length > 1)
            {
                bool ok = DrawDir(points[points.Length - 1], points[0], graphics, pen);
                drawn = drawn || ok;
                if (drawn)
                    graphics.DrawEllipse(pen, points[0].X - 2.5f, points[0].Y - 2.5f, 5, 5);
            }
        }

        private static PointF MulF(PointF p, float v) { return new PointF(p.X * v, p.Y * v); }
        private static PointF DivF(PointF p, float v) { return new PointF(p.X / v, p.Y / v); }
        private static PointF MulF(PointF l, PointF r) { return new PointF(l.X * r.X, l.Y * r.Y); }
        private static PointF DivF(PointF l, PointF r) { return new PointF(l.X / r.X, l.Y / r.Y); }
        private static PointF AddF(PointF l, PointF r) { return new PointF(l.X + r.X, l.Y + r.Y); }
        private static PointF SubF(PointF l, PointF r) { return new PointF(l.X - r.X, l.Y - r.Y); }
        private static float DotF(PointF l, PointF r) { return l.X * r.X + l.Y * r.Y; }
        private static float AngleF(PointF v) { return (float)Math.Atan2(v.Y, v.X); }
        private static PointF RotF(PointF v, float a)
        {
            return new PointF((float)(v.X * Math.Cos(a) - v.Y * Math.Sin(a)),
                              (float)(v.Y * Math.Cos(a) + v.X * Math.Sin(a)));
        }

        private class LocalCS
        {
            public LocalCS(Geometry.Box src_box, Graphics dst_graphics)
            {
                float w = dst_graphics.VisibleClipBounds.Width;
                float h = dst_graphics.VisibleClipBounds.Height;
                dst_x0 = w / 2;
                dst_y0 = h / 2;
                float dst_w = w * 0.9f;
                float dst_h = h * 0.9f;

                double src_w = src_box.Width;
                double src_h = src_box.Height;
                src_x0 = src_box.Min[0] + src_w / 2;
                src_y0 = src_box.Min[1] + src_h / 2;

                if (src_w == 0 && src_h == 0)
                    scale = 1; // point
                else if (src_w == 0)
                    scale = (h * 0.9f) / src_h;
                else if (src_h == 0)
                    scale = (w * 0.9f) / src_w;
                else
                {
                    double scale_w = (w * 0.9f) / src_w;
                    double scale_h = (h * 0.9f) / src_h;
                    scale = Math.Min(scale_w, scale_h);
                }
            }

            public float ConvertX(double src)
            {
                return dst_x0 + (float)((src - src_x0) * scale);
            }

            public float ConvertY(double src)
            {
                return dst_y0 - (float)((src - src_y0) * scale);
            }

            public float ConvertDimension(double src)
            {
                return (float)(src * scale);
            }

            public PointF Convert(Geometry.Point p)
            {
                return new PointF(ConvertX(p[0]), ConvertY(p[1]));
            }

            public PointF[] Convert(Geometry.Ring ring)
            {
                if (ring.Count <= 0)
                    return null;

                int dst_count = ring.Count + (ring[0] == ring[ring.Count - 1] ? 0 : 1);

                PointF[] dst_points = new PointF[dst_count];
                int i = 0;
                for (; i < ring.Count; ++i)
                {
                    dst_points[i] = Convert(ring[i]);
                }
                if (i < dst_count)
                {
                    dst_points[i] = Convert(ring[0]);
                }

                return dst_points;
            }

            float dst_x0, dst_y0;
            double src_x0, src_y0;
            double scale;
        }

        private static void DrawCircle(Graphics graphics, Pen pen, Brush brush, float x, float y, float r)
        {
            float mx = x - r;
            float my = y - r;
            float d = 2 * r;
            graphics.DrawEllipse(pen, mx, my, d, d);
            graphics.FillEllipse(brush, mx, my, d, d);
        }

        private static void DrawLine(Graphics graphics, Pen pen, PointF p0, PointF p1, bool drawDir)
        {
            graphics.DrawLine(pen, p0, p1);
            if (drawDir)
                DrawDir(p0, p1, graphics, pen);
        }

        private static void DrawLines(Graphics graphics, Pen pen, PointF[] points, bool closed, bool drawDir)
        {
            graphics.DrawLines(pen, points);
            if (closed && points.Length > 1)
                graphics.DrawLine(pen, points[points.Length - 1], points[0]);
            if (drawDir)
                DrawDirs(points, closed, graphics, pen);
        }

        private static void DrawMessage(Graphics graphics, string message, Color color)
        {
            SolidBrush brush = new SolidBrush(color);
            Font font = new Font(new FontFamily(System.Drawing.Text.GenericFontFamilies.SansSerif), 10);
            StringFormat drawFormat = new StringFormat();
            drawFormat.Alignment = StringAlignment.Center;
            float gh = graphics.VisibleClipBounds.Top + graphics.VisibleClipBounds.Height / 2;
            RectangleF rect = new RectangleF(graphics.VisibleClipBounds.Left,
                                             gh - 5,
                                             graphics.VisibleClipBounds.Right,
                                             gh + 5);

            graphics.DrawString(message, font, brush, rect, drawFormat);
        }

        private static bool DrawAabb(Graphics graphics, Geometry.Box box, Geometry.Traits traits)
        {
            if (!box.IsValid())
                return false;

            LocalCS cs = new LocalCS(box, graphics);

            Pen pen = new Pen(Color.Black, 1);
            SolidBrush brush = new SolidBrush(Color.Black);

            float min_x = cs.ConvertX(box.Min[0]);
            float min_y = cs.ConvertY(box.Min[1]);
            float max_x = cs.ConvertX(box.Max[0]);
            float max_y = cs.ConvertY(box.Max[1]);

            graphics.DrawLine(pen, min_x - 1, min_y, min_x + 1, min_y);
            graphics.DrawLine(pen, min_x, min_y - 1, min_x, min_y + 1);
            graphics.DrawLine(pen, max_x - 1, max_y, max_x + 1, max_y);
            graphics.DrawLine(pen, max_x, max_y - 1, max_x, max_y + 1);

            float h = graphics.VisibleClipBounds.Height;
            float w = graphics.VisibleClipBounds.Width;
            Pen prime_pen = new Pen(Color.LightGray, 1);

            if (traits.Unit == Geometry.Unit.None)
            {
                // Y axis
                float x0 = cs.ConvertX(0.0);
                graphics.DrawLine(prime_pen, x0, 0, x0, h);
                // X axis
                float y0 = cs.ConvertY(0.0);
                graphics.DrawLine(prime_pen, 0, y0, w, y0);
            }
            else
            {
                Pen anti_pen = new Pen(Color.LightGray, 1);
                anti_pen.DashStyle = DashStyle.Custom;
                anti_pen.DashPattern = new float[]{ 5, 5 };
                double pi = Geometry.HalfAngle(traits.Unit);
                double anti_mer = Geometry.NearestAntimeridian(box.Min[0], -1, traits.Unit);
                double prime_mer = anti_mer + pi;
                double next_anti_mer = anti_mer + 2 * pi;
                double next_prime_mer = prime_mer + 2 * pi;

                float anti_mer_f = cs.ConvertX(anti_mer);
                float anti_mer_step = cs.ConvertX(next_anti_mer) - anti_mer_f;
                float prime_mer_f = cs.ConvertX(prime_mer);
                float prime_mer_step = cs.ConvertX(next_prime_mer) - prime_mer_f;

                // Antimeridians
                for (; anti_mer_f <= w; anti_mer_f += anti_mer_step)
                {
                    graphics.DrawLine(anti_pen, anti_mer_f, 0, anti_mer_f, h);
                }
                // Prime meridians
                for (; prime_mer_f <= w; prime_mer_f += prime_mer_step)
                {
                    graphics.DrawLine(prime_pen, prime_mer_f, 0, prime_mer_f, h);
                }
                // Equator
                float e = cs.ConvertY(0.0);
                if (0 <= e && e <= h)
                {
                    graphics.DrawLine(prime_pen, 0, e, w, e);
                }
                // North pole
                float n = cs.ConvertY(pi / 2);
                if (0 <= n && n <= h)
                {
                    graphics.DrawLine(anti_pen, 0, n, w, n);
                }
                // South pole
                float s = cs.ConvertY(-pi / 2);
                if (0 <= s && s <= h)
                {
                    graphics.DrawLine(anti_pen, 0, s, w, s);
                }
            }

            float maxHeight = 20.0f;// Math.Min(Math.Max(graphics.VisibleClipBounds.Height - min_y, 0.0f), 20.0f);
            if (maxHeight > 1)
            {
                string min_x_str = box.Min[0].ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
                string min_y_str = box.Min[1].ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
                string max_x_str = box.Max[0].ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
                string max_y_str = box.Max[1].ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
                Font font = new Font(new FontFamily(System.Drawing.Text.GenericFontFamilies.SansSerif), maxHeight / 2.0f);
                StringFormat drawFormat = new StringFormat();
                drawFormat.Alignment = StringAlignment.Center;
                string minStr = "(" + min_x_str + " " + min_y_str + ")";
                string maxStr = "(" + max_x_str + " " + max_y_str + ")";
                SizeF minSize = graphics.MeasureString(minStr, font);
                SizeF maxSize = graphics.MeasureString(maxStr, font);
                RectangleF drawRectMin = new RectangleF(Math.Max(min_x - minSize.Width, 0.0f),
                                                        Math.Min(min_y + 2, graphics.VisibleClipBounds.Height - maxSize.Height),
                                                        minSize.Width,
                                                        minSize.Height);
                RectangleF drawRectMax = new RectangleF(Math.Min(max_x, graphics.VisibleClipBounds.Width - maxSize.Width),
                                                        Math.Max(max_y - maxHeight, 0.0f),
                                                        maxSize.Width,
                                                        maxSize.Height);
                graphics.DrawString(minStr, font, brush, drawRectMin, drawFormat);
                graphics.DrawString(maxStr, font, brush, drawRectMax, drawFormat);
            }

            return true;
        }

        // -------------------------------------------------
        // Loading expressions
        // -------------------------------------------------

        private static Point LoadPoint(Debugger debugger, string name)
        {
            string name_prefix = "(double)" + name;
            Expression expr_x = debugger.GetExpression(name_prefix + "[0]");
            Expression expr_y = debugger.GetExpression(name_prefix + "[1]");

            double x = double.Parse(expr_x.Value, System.Globalization.CultureInfo.InvariantCulture);
            double y = double.Parse(expr_y.Value, System.Globalization.CultureInfo.InvariantCulture);

            return new Point(x, y);
        }

        private static Box LoadGeometryBox(Debugger debugger, string name)
        {
            Point first_p = LoadPoint(debugger, name + ".m_min_corner");
            Point second_p = LoadPoint(debugger, name + ".m_max_corner");

            return new Box(first_p, second_p);
        }

        private static Box LoadPolygonBox(Debugger debugger, string name)
        {
            Point first_p = LoadPoint(debugger, name + ".ranges_[0]"); // interval X
            Point second_p = LoadPoint(debugger, name + ".ranges_[1]"); // interval Y

            return new Box(new Point(first_p[0], second_p[0]),
                           new Point(first_p[1], second_p[1]));
        }

        private static Segment LoadSegment(Debugger debugger, string name, string first, string second, Geometry.Traits traits)
        {
            Point first_p = LoadPoint(debugger, name + "." + first);
            Point second_p = LoadPoint(debugger, name + "." + second);

            Segment result = new Segment(first_p, second_p);
            result.Box = Geometry.Aabb(result, traits);
            return result;
        }

        private static Linestring LoadLinestring(Debugger debugger, string name, Geometry.Traits traits)
        {
            Linestring result = new Linestring();
            //Geometry.Box box = new Geometry.Box();
            //Geometry.AssignInverse(box);

            int size = LoadSize(debugger, name);
            for (int i = 0; i < size; ++i)
            {
                Point p = LoadPoint(debugger, name + "[" + i + "]");
                result.Add(p);
                //Geometry.Expand(box, p);
            }

            //result.Box = box;
            result.Box = Geometry.Aabb(result, traits);
            return result;
        }

        private static Ring LoadRing(Debugger debugger, string name, string member, Geometry.Traits traits)
        {
            string name_suffix = member.Length > 0 ? "." + member : "";
            Linestring ls = LoadLinestring(debugger, name + name_suffix, traits);
            return new Ring(ls, ls.Box);
        }

        private static Polygon LoadPolygon(Debugger debugger, string name, string outer, string inners, bool inners_in_list, string ring_member, Geometry.Traits traits)
        {
            Ring outer_r = LoadRing(debugger, name + "." + outer, ring_member, traits);

            Geometry.Box box = new Geometry.Box();
            Geometry.AssignInverse(box);
            Geometry.Expand(box, outer_r.Box);

            List<Geometry.Ring> inners_r = new List<Geometry.Ring>();

            ContainerElements inner_names = new ContainerElements(debugger, name + "." + inners);
            foreach(string inner_name in inner_names)
            {
                Ring inner_r = LoadRing(debugger, inner_name, ring_member, traits);

                inners_r.Add(inner_r);
                Geometry.Expand(box, inner_r.Box);
            }

            return new Polygon(outer_r, inners_r, box);
        }

        private static Multi<Point> LoadMultiPoint(Debugger debugger, string name)
        {
            List<IDrawable> singles = new List<IDrawable>();
            Geometry.Box box = new Geometry.Box();
            Geometry.AssignInverse(box);

            int size = LoadSize(debugger, name);

            for (int i = 0; i < size; ++i)
            {
                Point s = LoadPoint(debugger, name + "[" + i + "]");
                singles.Add(s);
                Geometry.Expand(box, s);
            }

            return new Multi<Point>(singles, box);
        }

        private static Multi<Linestring> LoadMultiLinestring(Debugger debugger, string name, Geometry.Traits traits)
        {
            List<IDrawable> singles = new List<IDrawable>();
            Geometry.Box box = new Geometry.Box();
            Geometry.AssignInverse(box);

            int size = LoadSize(debugger, name);

            for (int i = 0; i < size; ++i)
            {
                Linestring s = LoadLinestring(debugger, name + "[" + i + "]", traits);
                singles.Add(s);
                Geometry.Expand(box, s.Box);
            }

            return new Multi<Linestring>(singles, box);
        }

        private static Multi<Polygon> LoadMultiPolygon(Debugger debugger, string name, string outer, string inners, Geometry.Traits traits)
        {
            List<IDrawable> singles = new List<IDrawable>();
            Geometry.Box box = new Geometry.Box();
            Geometry.AssignInverse(box);

            int size = LoadSize(debugger, name);

            for (int i = 0; i < size; ++i)
            {
                Polygon s = LoadPolygon(debugger, name + "[" + i + "]", outer, inners, false, "", traits);
                singles.Add(s);
                Geometry.Expand(box, s.Box);
            }

            return new Multi<Polygon>(singles, box);
        }

        private static ValuesContainer LoadValuesContainer(Debugger debugger, string name)
        {
            List<double> values = new List<double>();
            Geometry.Box box = new Geometry.Box();
            Geometry.AssignInverse(box);

            ContainerElements names = new ContainerElements(debugger, name);

            if (names.Count > 0)
                Geometry.Expand(box, new Point(0.0, 0.0));

            int i = 0;
            foreach (string elem_n in names)
            {
                Expression expr = debugger.GetExpression("(double)" + elem_n);
                if (!expr.IsValidValue)
                    continue;
                double v = double.Parse(expr.Value, System.Globalization.CultureInfo.InvariantCulture);
                values.Add(v);
                Geometry.Expand(box, new Point(i, v));
                ++i;
            }

            // make square, scaling Y
            if (names.Count > 0)
            {
                double threshold = float.Epsilon;
                if (box.Height > threshold)
                {
                    box.Max[0] = box.Min[0] + box.Height;
                }
                else
                {
                    box.Max[0] = box.Min[0] + threshold;
                    box.Max[1] = box.Min[1] + threshold;
                }
            }

            return new ValuesContainer(values, box);
        }

        private static char MethodChar(string method)
        {
            switch (method)
            {
                case "method_none": return '-';
                case "method_disjoint": return 'd';
                case "method_crosses": return 'i';
                case "method_touch": return 't';
                case "method_touch_interior": return 'm';
                case "method_collinear": return 'c';
                case "method_equal": return 'e';
                case "method_error": return '!';
                default: return '?';
            }
        }

        private static char OperationChar(string operation)
        {
            switch (operation)
            {
                case "operation_none": return '-';
                case "operation_union": return 'u';
                case "operation_intersection": return 'i';
                case "operation_blocked": return 'x';
                case "operation_continue": return 'c';
                case "operation_opposite": return 'o';
                default: return '?';
            }
        }

        private static TurnsContainer.Turn LoadTurn(Debugger debugger, string name)
        {
            Point p = LoadPoint(debugger, name + ".point");

            char method = '?';
            Expression expr_method = debugger.GetExpression(name + ".method");
            if (expr_method.IsValidValue)
                method = MethodChar(expr_method.Value);

            char op0 = '?';
            Expression expr_op0 = debugger.GetExpression(name + ".operations[0].operation");
            if (expr_op0.IsValidValue)
                op0 = OperationChar(expr_op0.Value);

            char op1 = '?';
            Expression expr_op1 = debugger.GetExpression(name + ".operations[1].operation");
            if (expr_op1.IsValidValue)
                op1 = OperationChar(expr_op1.Value);

            return new TurnsContainer.Turn(p, method, op0, op1);
        }

        private static TurnsContainer LoadTurnsContainer(Debugger debugger, string name, int size)
        {
            List<TurnsContainer.Turn> turns = new List<TurnsContainer.Turn>();
            Geometry.Box box = new Geometry.Box();
            Geometry.AssignInverse(box);

            for (int i = 0; i < size; ++i)
            {
                TurnsContainer.Turn t = LoadTurn(debugger, name + "[" + i + "]");

                turns.Add(t);
                Geometry.Expand(box, t.point);
            }

            return new TurnsContainer(turns, box);
        }

        // -------------------------------------------------
        // Traits
        // -------------------------------------------------

        private static Geometry.Traits LoadPointTraits(string type)
        {
            string base_type = Util.BaseType(type);
            if (base_type == "boost::geometry::model::point")
            {
                List<string> tparams = Util.Tparams(type);
                if (tparams.Count == 3)
                {
                    int dimension = int.Parse(tparams[1]);

                    string cs = tparams[2];
                    if (cs == "boost::geometry::cs::cartesian")
                    {
                        return new Geometry.Traits(dimension);
                    }
                    else
                    {
                        List<string> cs_tparams = Util.Tparams(cs);
                        if (cs_tparams.Count == 1)
                        {
                            string cs_base_type = Util.BaseType(cs);
                            Geometry.CoordinateSystem coordinateSystem = Geometry.CoordinateSystem.Cartesian;
                            if (cs_base_type == "boost::geometry::cs::spherical")
                                coordinateSystem = Geometry.CoordinateSystem.Spherical;
                            else if (cs_base_type == "boost::geometry::cs::geographic")
                                coordinateSystem = Geometry.CoordinateSystem.Geographic;

                            string u = cs_tparams[0];
                            Geometry.Unit unit = Geometry.Unit.None;
                            if (u == "boost::geometry::radian")
                                unit = Geometry.Unit.Radian;
                            else if (u == "boost::geometry::degree")
                                unit = Geometry.Unit.Degree;

                            return new Geometry.Traits(dimension, coordinateSystem, unit);
                        }
                    }
                }
            }
            else if (base_type == "boost::geometry::model::d2::point_xy")
            {
                return new Geometry.Traits(2);
            }

            return null;
        }

        private static Geometry.Traits LoadGeometryTraits(string type, string single_type)
        {
            if (Util.BaseType(type) == single_type)
            {
                List<string> tparams = Util.Tparams(type);
                if (tparams.Count > 0)
                    return LoadPointTraits(tparams[0]);
            }
            return null;
        }

        private static Geometry.Traits LoadGeometryTraits(string type, string multi_type, string single_type)
        {
            if (Util.BaseType(type) == multi_type)
            {
                List<string> tparams = Util.Tparams(type);
                if (tparams.Count > 0)
                    return LoadGeometryTraits(tparams[0], single_type);
            }
            return null;
        }

        private static Geometry.Traits LoadTurnsContainerTraits(string type)
        {
            string base_type = Util.BaseType(type);
            if (base_type == "std::vector"
             || base_type == "std::deque")
            {
                List<string> tparams = Util.Tparams(type);
                if (tparams.Count > 0)
                {
                    string element_base_type = Util.BaseType(tparams[0]);
                    if (element_base_type == "boost::geometry::detail::overlay::turn_info"
                     || element_base_type == "boost::geometry::detail::overlay::traversal_turn_info")
                    {
                        List<string> el_tparams = Util.Tparams(tparams[0]);
                        if (el_tparams.Count > 0)
                            return LoadPointTraits(el_tparams[0]);
                    }
                }
            }

            return null;
        }

        // -------------------------------------------------
        // High level loading
        // -------------------------------------------------

        public class DrawablePair
        {
            public DrawablePair(IDrawable drawable, Geometry.Traits traits)
            {
                Drawable = drawable;
                Traits = traits;
            }
            public IDrawable Drawable { get; set; }
            public Geometry.Traits Traits { get; set; }
        }

        private static DrawablePair LoadGeometry(Debugger debugger, string name, string type)
        {
            IDrawable d = null;
            Geometry.Traits traits = null;
            
            if ((traits = LoadPointTraits(type)) != null)
                d = LoadPoint(debugger, name);
            else if ((traits = LoadGeometryTraits(type, "boost::geometry::model::box")) != null)
                d = LoadGeometryBox(debugger, name);
            else if ((traits = LoadGeometryTraits(type, "boost::geometry::model::segment")) != null
                  || (traits = LoadGeometryTraits(type, "boost::geometry::model::referring_segment")) != null)
                d = LoadSegment(debugger, name, "first", "second", traits);
            else if ((traits = LoadGeometryTraits(type, "boost::geometry::model::linestring")) != null)
                d = LoadLinestring(debugger, name, traits);
            else if ((traits = LoadGeometryTraits(type, "boost::geometry::model::ring")) != null)
                d = LoadRing(debugger, name, "", traits);
            else if ((traits = LoadGeometryTraits(type, "boost::geometry::model::polygon")) != null)
                d = LoadPolygon(debugger, name, "m_outer", "m_inners", false, "", traits);
            else if ((traits = LoadGeometryTraits(type, "boost::geometry::model::multi_point")) != null)
                d = LoadMultiPoint(debugger, name);
            else if ((traits = LoadGeometryTraits(type, "boost::geometry::model::multi_linestring", "boost::geometry::model::linestring")) != null)
                d = LoadMultiLinestring(debugger, name, traits);
            else if ((traits = LoadGeometryTraits(type, "boost::geometry::model::multi_polygon", "boost::geometry::model::polygon")) != null)
                d = LoadMultiPolygon(debugger, name, "m_outer", "m_inners", traits);
            else
            {
                traits = new Geometry.Traits(2); // 2D cartesian;

                string base_type = Util.BaseType(type);
                if (base_type == "boost::polygon::point_data")
                    d = LoadPoint(debugger, name);
                else if (base_type == "boost::polygon::segment_data")
                    d = LoadSegment(debugger, name, "points_[0]", "points_[1]", traits);
                else if (base_type == "boost::polygon::rectangle_data")
                    d = LoadPolygonBox(debugger, name);
                else if (base_type == "boost::polygon::polygon_data")
                    d = LoadRing(debugger, name, "coords_", traits);
                else if (base_type == "boost::polygon::polygon_with_holes_data")
                    d = LoadPolygon(debugger, name, "self_", "holes_", true, "coords_", traits);

                if (d == null)
                    traits = null;
            }

            return new DrawablePair(d, traits);
        }

        private static DrawablePair LoadGeometryOrVariant(Debugger debugger, string name, string type)
        {
            // Currently the supported types are hardcoded as follows:

            // Boost.Geometry models
            DrawablePair result = LoadGeometry(debugger, name, type);

            if (result.Drawable == null)
            {
                // Boost.Variant containing a Boost.Geometry model
                if (Util.BaseType(type) == "boost::variant")
                {
                    Expression expr_which = debugger.GetExpression(name + ".which_");
                    if (expr_which.IsValidValue)
                    {
                        int which = int.Parse(expr_which.Value);
                        List<string> tparams = Util.Tparams(type);
                        if (which >= 0 && which < tparams.Count)
                        {
                            string value_str = "(*(" + tparams[which] + "*)" + name + ".storage_.data_.buf)";
                            Expression expr_value = debugger.GetExpression(value_str);
                            if (expr_value.IsValidValue)
                            {
                                result = LoadGeometry(debugger, value_str, expr_value.Type);
                            }
                        }
                    }
                }
            }

            return result;
        }

        private static DrawablePair LoadTurnsContainer(Debugger debugger, string name, string type)
        {
            // STL RandomAccess container of Turns
            IDrawable d = null;
            Geometry.Traits traits = LoadTurnsContainerTraits(type);

            if (traits != null)
            {
                int size = LoadSize(debugger, name);
                d = LoadTurnsContainer(debugger, name, size);
            }

            return new DrawablePair(d, traits);
        }

        private static DrawablePair LoadDrawable(Debugger debugger, string name, string type)
        {
            DrawablePair res = LoadGeometryOrVariant(debugger, name, type);

            if (res.Drawable == null)
            {
                res = LoadTurnsContainer(debugger, name, type);
            }

            if (res.Drawable == null)
            {
                // container of 1D values convertible to double
                IDrawable d = LoadValuesContainer(debugger, name);
                res = new DrawablePair(d, null); // the traits are not needed in this case
            }

            return res;
        }

        private static int LoadSize(Debugger debugger, string name)
        {
            // VS2015 vector
            Expression expr_size = debugger.GetExpression(name + "._Mypair._Myval2._Mylast-" + name + "._Mypair._Myval2._Myfirst");
            if (expr_size.IsValidValue)
            {
                int result = int.Parse(expr_size.Value);
                return Math.Max(result, 0);
            }
            // VS2015 deque, list
            expr_size = debugger.GetExpression(name + "._Mypair._Myval2._Mysize");
            if (expr_size.IsValidValue)
            {
                int result = int.Parse(expr_size.Value);
                return Math.Max(result, 0);
            }

            return 0;
        }

        private class ContainerElements : IEnumerable
        {
            public ContainerElements(Debugger debugger, string name)
            {
                enumerator = new ContainerElementsEnumerator(debugger, name);
            }

            public IEnumerator GetEnumerator()
            {
                return enumerator;
            }

            public int Count { get { return enumerator.Count; } }

            private ContainerElementsEnumerator enumerator;
        }

        private class ContainerElementsEnumerator : IEnumerator
        {
            public ContainerElementsEnumerator(Debugger debugger, string name)
            {
                this.name = name;
                this.index = -1;
                this.size = 0;

                Expression expr = debugger.GetExpression(name);
                if (!expr.IsValidValue)
                    return;

                string baseType = Util.BaseType(expr.Type);

                if (baseType == "std::vector")
                {
                    // VS2015
                    Expression size_expr = debugger.GetExpression(name + "._Mypair._Myval2._Mylast-" + name + "._Mypair._Myval2._Myfirst");
                    if (size_expr.IsValidValue)
                    {
                        int result = int.Parse(size_expr.Value);
                        this.size = Math.Max(result, 0);
                    }
                }
                else if (baseType == "std::array" || baseType == "boost::array")
                {
                    List<string> tParams = Util.Tparams(expr.Type);
                    int result = int.Parse(tParams[1]);
                    this.size = Math.Max(result, 0);
                }
                else if (baseType == "std::deque" || baseType == "std::list")
                {
                    // VS2015
                    Expression size_expr = debugger.GetExpression(name + "._Mypair._Myval2._Mysize");
                    if (size_expr.IsValidValue)
                    {
                        int result = int.Parse(size_expr.Value);
                        this.size = Math.Max(result, 0);
                    }

                    if (baseType == "std::list")
                    {
                        this.nextNode = name + "._Mypair._Myval2._Myhead"; // VS2015
                    }
                }
                else if (baseType == "boost::container::vector" || baseType == "boost::container::static_vector")
                {
                    Expression size_expr = debugger.GetExpression(name + ".m_holder.m_size");
                    if (size_expr.IsValidValue)
                    {
                        int result = int.Parse(size_expr.Value);
                        this.size = Math.Max(result, 0);
                    }
                }
            }

            public string CurrentString
            {
                get
                {
                    if (index < 0 || index >= size)
                        throw new InvalidOperationException();

                    if (nextNode == null) // vector, deque, etc.
                        return name + "[" + index + "]";
                    else // list
                        return nextNode + "->_Myval";
                }
            }

            public object Current
            {
                get
                {
                    return CurrentString;
                }
            }

            public bool MoveNext()
            {
                ++index;
                if (nextNode != null)
                    nextNode = nextNode + "->_Next";

                return index < size;
            }

            public void Reset()
            {
                index = -1;
                if (nextNode != null)
                    nextNode = name + "._Mypair._Myval2._Myhead"; // VS2015
            }

            public int Count { get { return size; } }

            private string name;
            private int index;
            private int size;
            private string nextNode;
        }

        // -------------------------------------------------
        // Make
        // -------------------------------------------------

        private static DrawablePair MakeGeometry(Debugger debugger, string name)
        {
            Expression expr = debugger.GetExpression(name);
            if (!expr.IsValidValue)
                return new DrawablePair(null ,null);

            DrawablePair res = LoadGeometryOrVariant(debugger, expr.Name, expr.Type);
            if (res.Drawable != null)
                return res;

            return LoadTurnsContainer(debugger, expr.Name, expr.Type);
        }

        private static DrawablePair MakeDrawable(Debugger debugger, string name)
        {
            Expression expr = debugger.GetExpression(name);
            if (!expr.IsValidValue)
                return new DrawablePair(null, null);

            return LoadDrawable(debugger, expr.Name, expr.Type);
        }

        // -------------------------------------------------
        // public Draw
        // -------------------------------------------------

        // For GraphicalWatch
        public static bool Draw(Graphics graphics, Debugger debugger, string name)
        {
            try
            {
                DrawablePair d = MakeDrawable(debugger, name);
                if (d.Drawable != null)
                {
                    Settings settings = new Settings(d.Drawable.DefaultColor);
                    d.Drawable.Draw(d.Drawable.Aabb, graphics, settings, d.Traits);
                    return true;
                }
            }
            catch(Exception e)
            {
                DrawMessage(graphics, e.Message, Color.Red);
            }

            return false;
        }

        // For GeometryWatch
        public static bool DrawGeometries(Graphics graphics, Debugger debugger, string[] names, Settings[] settings)
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
                        drawables[i] = ExpressionDrawer.MakeGeometry(debugger, names[i]);

                        if (drawables[i].Drawable != null)
                        {
                            Geometry.Traits traits = drawables[i].Traits;
                            dimensions.Add(traits.Dimension);
                            csystems.Add(traits.CoordinateSystem);
                            units.Add(traits.Unit);

                            Geometry.Expand(box, drawables[i].Drawable.Aabb);

                            ++drawnCount;
                        }
                    }
                }

                if (csystems.Count > 1)
                {
                    throw new Exception("Multiple coordinate systems detected.");
                }
                if (units.Count > 1)
                {
                    throw new Exception("Multiple units detected.");
                }

                if (drawnCount > 0)
                {
                    Geometry.Traits traits = new Geometry.Traits(dimensions.Max(), csystems.First(), units.First());

                    ExpressionDrawer.DrawAabb(graphics, box, traits);

                    for (int i = 0; i < count; ++i)
                    {
                        if (drawables[i] != null && drawables[i].Drawable != null)
                        {
                            drawables[i].Drawable.Draw(box, graphics, settings[i], traits);
                        }
                    }

                    return true;
                }
            }
            catch (Exception e)
            {
                DrawMessage(graphics, e.Message, Color.Red);
            }

            return false;
        }
        
    }
}
