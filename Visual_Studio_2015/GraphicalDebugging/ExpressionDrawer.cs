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

        public interface IAabb
        {
            double Get(int corner, int index);
        }

        public interface IDrawable
        {
            void Draw(IAabb box, Graphics graphics, Settings settings, GeometryTraits traits);
            IAabb Aabb { get; }
            Color DefaultColor { get; }
        }

        private class Point<CS, U> : Geometry.Point<CS, U>, IDrawable
        {
            public Point(double x, double y)
                : base(x, y)
            { }

            public void Draw(IAabb box, Graphics graphics, Settings settings, GeometryTraits trait)
            {
                LocalCS cs = new LocalCS(box, graphics);

                Pen pen = new Pen(Color.FromArgb(112, settings.color), 2);
                SolidBrush brush = new SolidBrush(Color.FromArgb(64, settings.color));

                float x = cs.ConvertX(this[0]);
                float y = cs.ConvertY(this[1]);
                graphics.FillEllipse(brush, x - 2.5f, y - 2.5f, 5, 5);
                graphics.DrawEllipse(pen, x - 2.5f, y - 2.5f, 5, 5);
            }

            public IAabb Aabb { get {
                    return new Box<CS, U>(this, this);
                } }

            public Color DefaultColor { get { return Color.Orange; } }
        }

        private class Box<CS, U> : Geometry.Box<CS, U>, IAabb, IDrawable
        {
            public Box()
            { }

            public Box(Geometry.Point<CS, U> min, Geometry.Point<CS, U> max)
                : base(min, max)
            { }

            public double Get(int corner, int index)
            {
                return corner == 0 ? min[index] : max[index];
            }

            public void Draw(IAabb box, Graphics graphics, Settings settings, GeometryTraits traits)
            {
                LocalCS cs = new LocalCS(box, graphics);

                Pen pen = new Pen(Color.FromArgb(112, settings.color), 2);
                SolidBrush brush = new SolidBrush(Color.FromArgb(64, settings.color));

                float rx = cs.ConvertX(min[0]);
                float ry = cs.ConvertY(max[1]);
                float rw = cs.ConvertDimension(Width);
                float rh = cs.ConvertDimension(Height);

                if (rw == 0 && rh == 0)
                {
                    graphics.FillEllipse(brush, rx - 2.5f, ry - 2.5f, 5, 5);
                    graphics.DrawEllipse(pen, rx - 2.5f, ry - 2.5f, 5, 5);
                }
                else if (rw == 0 || rh == 0)
                {
                    graphics.DrawLine(pen, rx, ry, rx + rw, ry + rh);
                }
                else
                {
                    graphics.FillRectangle(brush, rx, ry, rw, rh);
                    graphics.DrawRectangle(pen, rx, ry, rw, rh);
                }
            }

            public IAabb Aabb { get { return this; } }

            public Color DefaultColor { get { return Color.Red; } }
        }

        private class Segment<CS, U> : Geometry.Segment<CS, U>, IDrawable
        {
            public Segment(Point<CS, U> first, Point<CS, U> second)
                : base(first, second)
            {}

            public void Draw(IAabb box, Graphics graphics, Settings settings, GeometryTraits traits)
            {
                LocalCS cs = new LocalCS(box, graphics);

                Pen pen = new Pen(Color.FromArgb(112, settings.color), 2);

                PointF p0 = cs.Convert(this[0]);
                PointF p1 = cs.Convert(this[1]);
                graphics.DrawLine(pen, p0, p1);

                if (settings.showDir)
                    DrawDir(p0, p1, graphics, pen);
            }

            public IAabb Aabb
            {
                get
                {
                    Geometry.Box<CS, U> b = Geometry.Envelope(this);
                    return new Box<CS, U>(b.min, b.max);
                }
            }

            public Color DefaultColor { get { return Color.YellowGreen; } }
        }

        private class Linestring<CS, U> : Geometry.Linestring<CS, U>, IDrawable
        {
            public void Draw(IAabb box, Graphics graphics, Settings settings, GeometryTraits traits)
            {
                LocalCS cs = new LocalCS(box, graphics);

                Pen pen = new Pen(Color.FromArgb(112, settings.color), 2);

                for (int i = 1; i < Count; ++i)
                {
                    PointF p0 = cs.Convert(this[i - 1]);
                    PointF p1 = cs.Convert(this[i]);
                    graphics.DrawLine(pen, p0, p1);

                    if (settings.showDir)
                        DrawDir(p0, p1, graphics, pen);
                }
            }

            public IAabb Aabb { get { return Box; } }
            public Color DefaultColor { get { return Color.Green; } }

            public Box<CS, U> Box { get; set; }
        }

        private class Ring<CS, U> : Geometry.Ring<CS, U>, IDrawable
        {
            public Ring(Geometry.Linestring<CS, U> linestring, Box<CS, U> box)
            {
                this.linestring = linestring;
                this.Box = box;
            }

            public void Draw(IAabb box, Graphics graphics, Settings settings, GeometryTraits traits)
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
                        DrawDir(dst_points, true, graphics, pen);
                }
            }

            public IAabb Aabb { get { return Box; } }
            public Color DefaultColor { get { return Color.SlateBlue; } }

            public Box<CS, U> Box { get; set; }
        }

        private class Polygon<CS, U> : Geometry.Polygon<CS, U>, IDrawable
        {
            public Polygon(Geometry.Ring<CS, U> outer, List<Geometry.Ring<CS, U>> inners, Box<CS, U> box)
            {
                this.outer = outer;
                this.inners = inners;
                this.Box = box;
            }

            public void Draw(IAabb box, Graphics graphics, Settings settings, GeometryTraits traits)
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
                        DrawDir(dst_outer_points, true, graphics, pen);

                    foreach (Ring<CS, U> inner in inners)
                    {
                        PointF[] dst_inner_points = cs.Convert(inner);
                        if (dst_inner_points != null)
                        {
                            gp.AddPolygon(dst_inner_points);

                            if (settings.showDir)
                                DrawDir(dst_inner_points, true, graphics, pen);
                        }
                    }

                    graphics.FillPath(brush, gp);
                    graphics.DrawPath(pen, gp);
                }
            }

            public IAabb Aabb { get { return Box; } }
            public Color DefaultColor { get { return Color.RoyalBlue; } }

            public Box<CS, U> Box { get; set; }
        }

        private class Multi<S, CS, U> : IDrawable
        {
            public Multi(List<IDrawable> singles, Box<CS, U> box)
            {
                this.singles = singles;
                this.Box = box;
            }
            
            public void Draw(IAabb box, Graphics graphics, Settings settings, GeometryTraits traits)
            {
                foreach (IDrawable single in singles)
                {
                    single.Draw(box, graphics, settings, traits);
                }
            }

            public IAabb Aabb { get { return Box; } }
            public Color DefaultColor { get { return singles.Count > 0 ? singles.First().DefaultColor : Color.Black; } }

            public Box<CS, U> Box { get; set; }

            private List<IDrawable> singles;
        }

        private class ValuesContainer : IDrawable
        {
            public ValuesContainer(List<double> values, Box<Geometry.Cartesian, Geometry.None> box)
            {
                this.values = values;
                this.Box = box;
            }

            public void Draw(IAabb box, Graphics graphics, Settings settings, GeometryTraits traits)
            {
                LocalCS cs = new LocalCS(box, graphics);

                Pen axis_pen = new Pen(settings.color, 2);
                Pen pen = new Pen(Color.FromArgb(112, settings.color), 1);

                float ax0 = cs.ConvertX(box.Get(0, 0));
                float ax1 = cs.ConvertX(box.Get(1, 0));
                float ay = cs.ConvertY(0);
                graphics.DrawLine(axis_pen, ax0, ay, ax1, ay);

                double width = box.Get(1, 0) - box.Get(0, 0);
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

            public IAabb Aabb { get { return Box; } }
            public Color DefaultColor { get { return Color.Black; } }

            public Box<Geometry.Cartesian, Geometry.None> Box { get; set; }

            private List<double> values;
        }

        private class TurnsContainer<CS, U> : IDrawable
        {
            public TurnsContainer(List<Turn> turns, Box<CS, U> box)
            {
                this.turns = turns;
                this.Box = box;
            }

            public class Turn
            {
                public Turn(Geometry.Point<CS, U> p, char m, char o0, char o1)
                {
                    point = p;
                    method = m;
                    operation0 = o0;
                    operation1 = o1;
                }

                public Geometry.Point<CS, U> point;
                public char method, operation0, operation1;
            }

            public void Draw(IAabb box, Graphics graphics, Settings settings, GeometryTraits traits)
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
                    graphics.FillEllipse(brush, p.X - 2.5f, p.Y - 2.5f, 5, 5);
                    graphics.DrawEllipse(pen, p.X - 2.5f, p.Y - 2.5f, 5, 5);

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

            public IAabb Aabb { get { return Box; } }
            public Color DefaultColor { get { return Color.DarkOrange; } }

            public Box<CS, U> Box { get; set; }

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

        private static void DrawDir(PointF[] points, bool closed, Graphics graphics, Pen pen)
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
            public LocalCS(IAabb src_box, Graphics dst_graphics)
            {
                float w = dst_graphics.VisibleClipBounds.Width;
                float h = dst_graphics.VisibleClipBounds.Height;
                dst_x0 = w / 2;
                dst_y0 = h / 2;
                float dst_w = w * 0.9f;
                float dst_h = h * 0.9f;

                double src_w = src_box.Get(1, 0) - src_box.Get(0, 0);
                double src_h = src_box.Get(1, 1) - src_box.Get(0, 1);
                src_x0 = src_box.Get(0, 0) + src_w / 2;
                src_y0 = src_box.Get(0, 1) + src_h / 2;

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

            public PointF Convert<CS, U>(Geometry.Point<CS, U> p)
            {
                return new PointF(ConvertX(p[0]), ConvertY(p[1]));
            }

            public PointF[] Convert<CS, U>(Geometry.Ring<CS, U> ring)
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

        // -------------------------------------------------
        // Loading expressions
        // -------------------------------------------------

        private static Point<CS, U> LoadPoint<CS, U>(Debugger debugger, string name)
        {
            string name_prefix = "(double)" + name;
            Expression expr_x = debugger.GetExpression(name_prefix + "[0]");
            Expression expr_y = debugger.GetExpression(name_prefix + "[1]");

            double x = double.Parse(expr_x.Value, System.Globalization.CultureInfo.InvariantCulture);
            double y = double.Parse(expr_y.Value, System.Globalization.CultureInfo.InvariantCulture);

            return new Point<CS, U>(x, y);
        }

        private static Box<CS, U> LoadGeometryBox<CS, U>(Debugger debugger, string name)
        {
            Point<CS, U> first_p = LoadPoint<CS, U>(debugger, name + ".m_min_corner");
            Point<CS, U> second_p = LoadPoint<CS, U>(debugger, name + ".m_max_corner");

            return new Box<CS, U>(first_p, second_p);
        }

        private static Box<CS, U> LoadPolygonBox<CS, U>(Debugger debugger, string name)
        {
            Point<CS, U> first_p = LoadPoint<CS, U>(debugger, name + ".ranges_[0]"); // interval X
            Point<CS, U> second_p = LoadPoint<CS, U>(debugger, name + ".ranges_[1]"); // interval Y

            return new Box<CS, U>(
                        new Point<CS, U>(first_p[0], second_p[0]),
                        new Point<CS, U>(first_p[1], second_p[1]));
        }

        private static Segment<CS, U> LoadSegment<CS, U>(Debugger debugger, string name, string first, string second)
        {
            Point<CS, U> first_p = LoadPoint<CS, U>(debugger, name + "." + first);
            Point<CS, U> second_p = LoadPoint<CS, U>(debugger, name + "." + second);

            return new Segment<CS, U>(first_p, second_p);
        }

        private static Linestring<CS, U> LoadLinestring<CS, U>(Debugger debugger, string name)
        {
            Linestring<CS, U> result = new Linestring<CS, U>();
            Box<CS, U> box = new Box<CS, U>();
            Geometry.AssignInverse(box);

            int size = LoadSize(debugger, name);
            for (int i = 0; i < size; ++i)
            {
                Point<CS, U> p = LoadPoint<CS, U>(debugger, name + "[" + i + "]");
                result.Add(p);
                Geometry.Expand(box, p);
            }

            result.Box = box;
            return result;
        }

        private static Ring<CS, U> LoadRing<CS, U>(Debugger debugger, string name, string member)
        {
            string name_suffix = member.Length > 0 ? "." + member : "";
            Linestring<CS, U> ls = LoadLinestring<CS, U>(debugger, name + name_suffix);
            return new Ring<CS, U>(ls, ls.Box);
        }

        private static Polygon<CS, U> LoadPolygon<CS, U>(Debugger debugger, string name, string outer, string inners, bool inners_in_list, string ring_member)
        {
            Ring<CS, U> outer_r = LoadRing<CS, U>(debugger, name + "." + outer, ring_member);

            Box<CS, U> box = new Box<CS, U>();
            Geometry.AssignInverse(box);
            Geometry.Expand(box, outer_r.Box);

            List<Geometry.Ring<CS, U>> inners_r = new List<Geometry.Ring<CS, U>>();

            ContainerElements inner_names = new ContainerElements(debugger, name + "." + inners);
            foreach(string inner_name in inner_names)
            {
                Ring<CS, U> inner_r = LoadRing<CS, U>(debugger, inner_name, ring_member);

                inners_r.Add(inner_r);
                Geometry.Expand(box, inner_r.Box);
            }

            return new Polygon<CS, U>(outer_r, inners_r, box);
        }

        private static Multi<Point<CS, U>, CS, U> LoadMultiPoint<CS, U>(Debugger debugger, string name)
        {
            List<IDrawable> singles = new List<IDrawable>();
            Box<CS, U> box = new Box<CS, U>();
            Geometry.AssignInverse(box);

            int size = LoadSize(debugger, name);

            for (int i = 0; i < size; ++i)
            {
                Point<CS, U> s = LoadPoint<CS, U>(debugger, name + "[" + i + "]");
                singles.Add(s);
                Geometry.Expand(box, s);
            }

            return new Multi<Point<CS, U>, CS, U>(singles, box);
        }

        private static Multi<Linestring<CS, U>, CS, U> LoadMultiLinestring<CS, U>(Debugger debugger, string name)
        {
            List<IDrawable> singles = new List<IDrawable>();
            Box<CS, U> box = new Box<CS, U>();
            Geometry.AssignInverse(box);

            int size = LoadSize(debugger, name);

            for (int i = 0; i < size; ++i)
            {
                Linestring<CS, U> s = LoadLinestring<CS, U>(debugger, name + "[" + i + "]");
                singles.Add(s);
                Geometry.Expand(box, s.Box);
            }

            return new Multi<Linestring<CS, U>, CS, U>(singles, box);
        }

        private static Multi<Polygon<CS, U>, CS, U> LoadMultiPolygon<CS, U>(Debugger debugger, string name, string outer, string inners)
        {
            List<IDrawable> singles = new List<IDrawable>();
            Box<CS, U> box = new Box<CS, U>();
            Geometry.AssignInverse(box);

            int size = LoadSize(debugger, name);

            for (int i = 0; i < size; ++i)
            {
                Polygon<CS, U> s = LoadPolygon<CS, U>(debugger, name + "[" + i + "]", outer, inners, false, "");
                singles.Add(s);
                Geometry.Expand(box, s.Box);
            }

            return new Multi<Polygon<CS, U>, CS, U>(singles, box);
        }

        private static ValuesContainer LoadValuesContainer(Debugger debugger, string name)
        {
            List<double> values = new List<double>();
            Box<Geometry.Cartesian, Geometry.None> box = new Box<Geometry.Cartesian, Geometry.None>();
            Geometry.AssignInverse(box);

            ContainerElements names = new ContainerElements(debugger, name);

            if (names.Count > 0)
                Geometry.Expand(box, new Point<Geometry.Cartesian, Geometry.None>(0.0, 0.0));

            int i = 0;
            foreach (string elem_n in names)
            {
                Expression expr = debugger.GetExpression("(double)" + elem_n);
                if (!expr.IsValidValue)
                    continue;
                double v = double.Parse(expr.Value, System.Globalization.CultureInfo.InvariantCulture);
                values.Add(v);
                Geometry.Expand(box, new Point<Geometry.Cartesian, Geometry.None>(i, v));
                ++i;
            }

            // make square, scaling Y
            if (names.Count > 0)
            {
                double threshold = float.Epsilon;
                if (box.Height > threshold)
                {
                    box.max[0] = box.min[0] + box.Height;
                }
                else
                {
                    box.max[0] = box.min[0] + threshold;
                    box.max[1] = box.min[1] + threshold;
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

        private static TurnsContainer<CS, U>.Turn LoadTurn<CS, U>(Debugger debugger, string name)
        {
            Point<CS, U> p = LoadPoint<CS, U>(debugger, name + ".point");

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

            return new TurnsContainer<CS, U>.Turn(p, method, op0, op1);
        }

        private static TurnsContainer<CS, U> LoadTurnsContainer<CS, U>(Debugger debugger, string name, int size)
        {
            List<TurnsContainer<CS, U>.Turn> turns = new List<TurnsContainer<CS, U>.Turn>();
            Box<CS, U> box = new Box<CS, U>();
            Geometry.AssignInverse(box);

            for (int i = 0; i < size; ++i)
            {
                TurnsContainer<CS, U>.Turn t = LoadTurn<CS, U>(debugger, name + "[" + i + "]");

                turns.Add(t);
                Geometry.Expand(box, t.point);
            }

            return new TurnsContainer<CS, U>(turns, box);
        }

        // -------------------------------------------------
        // Traits
        // -------------------------------------------------

        public class GeometryTraits
        {
            public GeometryTraits(int dimension)
            {
                this.Dimension = dimension;
                this.CoordinateSystem = CoordinateSystemT.Cartesian;
                this.Unit = UnitT.None;
            }

            public GeometryTraits(int dimension, CoordinateSystemT coordinateSystem, UnitT unit)
            {
                this.Dimension = dimension;
                this.CoordinateSystem = coordinateSystem;
                this.Unit = unit;
            }

            public enum CoordinateSystemT {Cartesian, Spherical, Geographic};
            public enum UnitT { None, Radian, Degree};
            
            public int Dimension { get; }
            public CoordinateSystemT CoordinateSystem { get; }
            public UnitT Unit { get; }
        }

        private static GeometryTraits GetPointTraits(string type)
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
                        return new GeometryTraits(dimension);
                    }
                    else
                    {
                        List<string> cs_tparams = Util.Tparams(cs);
                        if (cs_tparams.Count == 1)
                        {
                            string cs_base_type = Util.BaseType(cs);
                            GeometryTraits.CoordinateSystemT coordinateSystem = GeometryTraits.CoordinateSystemT.Cartesian;
                            if (cs_base_type == "boost::geometry::cs::spherical")
                                coordinateSystem = GeometryTraits.CoordinateSystemT.Spherical;
                            else if (cs_base_type == "boost::geometry::cs::geographic")
                                coordinateSystem = GeometryTraits.CoordinateSystemT.Geographic;

                            string u = cs_tparams[0];
                            GeometryTraits.UnitT unit = GeometryTraits.UnitT.None;
                            if (u == "boost::geometry::radian")
                                unit = GeometryTraits.UnitT.Radian;
                            else if (u == "boost::geometry::degree")
                                unit = GeometryTraits.UnitT.Degree;

                            return new GeometryTraits(dimension, coordinateSystem, unit);
                        }
                    }
                }
            }
            else if (base_type == "boost::geometry::model::d2::point_xy")
            {
                return new GeometryTraits(2);
            }

            return null;
        }

        private static GeometryTraits GetGeometryTraits(string type, string single_type)
        {
            if (Util.BaseType(type) == single_type)
            {
                List<string> tparams = Util.Tparams(type);
                if (tparams.Count > 0)
                    return GetPointTraits(tparams[0]);
            }
            return null;
        }

        private static GeometryTraits GetGeometryTraits(string type, string multi_type, string single_type)
        {
            if (Util.BaseType(type) == multi_type)
            {
                List<string> tparams = Util.Tparams(type);
                if (tparams.Count > 0)
                    return GetGeometryTraits(tparams[0], single_type);
            }
            return null;
        }

        private static GeometryTraits GetTurnsContainerTraits(string type)
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
                            return GetPointTraits(el_tparams[0]);
                    }
                }
            }

            return null;
        }

        // -------------------------------------------------
        // Loading expression+traits -> IDrawable
        // -------------------------------------------------

        private static GeometryTraits TraitsTypes(GeometryTraits traits, out Type cs, out Type u)
        {
            if (traits == null)
            {
                cs = null;
                u = null;
                return null;
            }

            if (traits.CoordinateSystem == GeometryTraits.CoordinateSystemT.Spherical)
            {
                if (traits.Unit == GeometryTraits.UnitT.Radian)
                {
                    cs = typeof(Geometry.Spherical);
                    u = typeof(Geometry.Radian);
                }
                else
                {
                    cs = typeof(Geometry.Spherical);
                    u = typeof(Geometry.Degree);
                }
            }
            else if (traits.CoordinateSystem == GeometryTraits.CoordinateSystemT.Geographic)
            {
                if (traits.Unit == GeometryTraits.UnitT.Radian)
                {
                    cs = typeof(Geometry.Geographic);
                    u = typeof(Geometry.Radian);
                }
                else
                {
                    cs = typeof(Geometry.Geographic);
                    u = typeof(Geometry.Degree);
                }
            }
            else
            {
                cs = typeof(Geometry.Cartesian);
                u = typeof(Geometry.None);
            }

            return traits;
        }

        private static IDrawable LoadGeneric(string methodName, GeometryTraits traits, object[] parameters)
        {
            if (traits == null)
                return null;

            Type cs = null;
            Type u = null;
            TraitsTypes(traits, out cs, out u);

            return (IDrawable)typeof(ExpressionDrawer).
                GetMethod(methodName, System.Reflection.BindingFlags.NonPublic
                                    | System.Reflection.BindingFlags.Static).
                    MakeGenericMethod(new Type[] { cs, u }).
                        Invoke(null, parameters);
        }

        // -------------------------------------------------
        // High level loading
        // -------------------------------------------------

        public class DrawablePair
        {
            public DrawablePair(IDrawable drawable, GeometryTraits traits)
            {
                Drawable = drawable;
                Traits = traits;
            }
            public IDrawable Drawable { get; set; }
            public GeometryTraits Traits { get; set; }
        }

        private static DrawablePair LoadGeometry(Debugger debugger, string name, string type)
        {
            IDrawable d = null;
            GeometryTraits traits = null;
            
            if ((traits = GetPointTraits(type)) != null)
                d = LoadGeneric("LoadPoint", traits, new object[] { debugger, name });
            else if ((traits = GetGeometryTraits(type, "boost::geometry::model::box")) != null)
                d = LoadGeneric("LoadGeometryBox", traits, new object[] { debugger, name });
            else if ((traits = GetGeometryTraits(type, "boost::geometry::model::segment")) != null
                  || (traits = GetGeometryTraits(type, "boost::geometry::model::referring_segment")) != null)
                d = LoadGeneric("LoadSegment", traits, new object[] { debugger, name, "first", "second" });
            else if ((traits = GetGeometryTraits(type, "boost::geometry::model::linestring")) != null)
                d = LoadGeneric("LoadLinestring", traits, new object[] { debugger, name });
            else if ((traits = GetGeometryTraits(type, "boost::geometry::model::ring")) != null)
                d = LoadGeneric("LoadRing", traits, new object[] { debugger, name, "" });
            else if ((traits = GetGeometryTraits(type, "boost::geometry::model::polygon")) != null)
                d = LoadGeneric("LoadPolygon", traits, new object[] { debugger, name, "m_outer", "m_inners", false, "" });
            else if ((traits = GetGeometryTraits(type, "boost::geometry::model::multi_point")) != null)
                d = LoadGeneric("LoadMultiPoint", traits, new object[] { debugger, name });
            else if ((traits = GetGeometryTraits(type, "boost::geometry::model::multi_linestring", "boost::geometry::model::linestring")) != null)
                d = LoadGeneric("LoadMultiLinestring", traits, new object[] { debugger, name });
            else if ((traits = GetGeometryTraits(type, "boost::geometry::model::multi_polygon", "boost::geometry::model::polygon")) != null)
                d = LoadGeneric("LoadMultiPolygon", traits, new object[] { debugger, name, "m_outer", "m_inners" });
            else
            {
                string base_type = Util.BaseType(type);
                if (base_type == "boost::polygon::point_data")
                    d = LoadPoint<Geometry.Cartesian, Geometry.None>(debugger, name);
                else if (base_type == "boost::polygon::segment_data")
                    d = LoadSegment<Geometry.Cartesian, Geometry.None>(debugger, name, "points_[0]", "points_[1]");
                else if (base_type == "boost::polygon::rectangle_data")
                    d = LoadPolygonBox<Geometry.Cartesian, Geometry.None>(debugger, name);
                else if (base_type == "boost::polygon::polygon_data")
                    d = LoadRing<Geometry.Cartesian, Geometry.None>(debugger, name, "coords_");
                else if (base_type == "boost::polygon::polygon_with_holes_data")
                    d = LoadPolygon<Geometry.Cartesian, Geometry.None>(debugger, name, "self_", "holes_", true, "coords_");

                if (d != null)
                    traits = new GeometryTraits(2); // 2D cartesian;
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
            GeometryTraits traits = GetTurnsContainerTraits(type);

            if (traits != null)
            {
                int size = LoadSize(debugger, name);
                d = LoadGeneric("LoadTurnsContainer", traits, new object[] { debugger, name, size });
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

                Box<Geometry.Cartesian, Geometry.None> box = new Box<Geometry.Cartesian, Geometry.None>();
                Geometry.AssignInverse(box);
                int drawnCount = 0;
                int count = names.Length;

                DrawablePair[] drawables = new DrawablePair[count];

                HashSet<int> dimensions = new HashSet<int>();
                HashSet<GeometryTraits.CoordinateSystemT> csystems = new HashSet<GeometryTraits.CoordinateSystemT>();
                HashSet<GeometryTraits.UnitT> units = new HashSet<GeometryTraits.UnitT>();

                for (int i = 0; i < count; ++i)
                {
                    if (names[i] != null && names[i] != "")
                    {
                        drawables[i] = ExpressionDrawer.MakeGeometry(debugger, names[i]);

                        if (drawables[i].Drawable != null)
                        {
                            GeometryTraits traits = drawables[i].Traits;
                            dimensions.Add(traits.Dimension);
                            csystems.Add(traits.CoordinateSystem);
                            units.Add(traits.Unit);

                            IAabb aabb = drawables[i].Drawable.Aabb;
                            Box<Geometry.Cartesian, Geometry.None> b = new Box<Geometry.Cartesian, Geometry.None>(
                                new Point<Geometry.Cartesian, Geometry.None>(aabb.Get(0, 0), aabb.Get(0, 1)),
                                new Point<Geometry.Cartesian, Geometry.None>(aabb.Get(1, 0), aabb.Get(1, 1))
                                );                            
                            Geometry.Expand(box, b);

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
                    int dimension = dimensions.Max();
                    GeometryTraits.CoordinateSystemT csystem = csystems.First();
                    GeometryTraits.UnitT unit = units.First();

                    for (int i = 0; i < count; ++i)
                    {
                        if (drawables[i] != null && drawables[i].Drawable != null)
                        {
                            drawables[i].Drawable.Draw(box, graphics, settings[i], drawables[i].Traits);
                        }
                    }

                    ExpressionDrawer.DrawAabb(graphics, box);

                    return true;
                }
            }
            catch (Exception e)
            {
                DrawMessage(graphics, e.Message, Color.Red);
            }

            return false;
        }

        // -------------------------------------------------
        // private Draw
        // -------------------------------------------------

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

        private static bool DrawAabb(Graphics graphics, IAabb aabb)
        {
            Box<Geometry.Cartesian, Geometry.None> box = new Box<Geometry.Cartesian, Geometry.None>(
                new Point<Geometry.Cartesian, Geometry.None>(aabb.Get(0, 0), aabb.Get(0, 1)),
                new Point<Geometry.Cartesian, Geometry.None>(aabb.Get(1, 0), aabb.Get(1, 1))
                );

            if (!box.IsValid())
                return false;

            LocalCS cs = new LocalCS(box, graphics);

            Pen pen = new Pen(Color.Black, 1);
            SolidBrush brush = new SolidBrush(Color.Black);

            float min_x = cs.ConvertX(box.min[0]);
            float min_y = cs.ConvertY(box.min[1]);
            float max_x = cs.ConvertX(box.max[0]);
            float max_y = cs.ConvertY(box.max[1]);

            graphics.DrawLine(pen, min_x - 1, min_y, min_x + 1, min_y);
            graphics.DrawLine(pen, min_x, min_y - 1, min_x, min_y + 1);
            graphics.DrawLine(pen, max_x - 1, max_y, max_x + 1, max_y);
            graphics.DrawLine(pen, max_x, max_y - 1, max_x, max_y + 1);

            float maxHeight = 20.0f;// Math.Min(Math.Max(graphics.VisibleClipBounds.Height - min_y, 0.0f), 20.0f);
            if (maxHeight > 1)
            {
                string min_x_str = box.min[0].ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
                string min_y_str = box.min[1].ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
                string max_x_str = box.max[0].ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
                string max_y_str = box.max[1].ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
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
    }
}
