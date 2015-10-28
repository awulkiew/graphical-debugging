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
            Color DefaultColor(Colors colors);
        }

        private class Point : Geometry.Point, IDrawable
        {
            public Point(double x, double y)
                : base(x, y)
            { }

            public Point(double x, double y, double z)
                : base(x, y, z)
            { }

            public void Draw(Geometry.Box box, Graphics graphics, Settings settings, Geometry.Traits traits)
            {
                LocalCS cs = new LocalCS(box, graphics);
                Drawer drawer = new Drawer(graphics, settings.color);

                if (traits.Unit == Geometry.Unit.None)
                {
                    PointF p = cs.Convert(this);
                    drawer.DrawPoint(p);
                }
                else // Radian, Degree
                {
                    drawer.DrawPeriodicPoint(cs, this, box, traits.Unit);
                }
            }

            public Geometry.Box Aabb { get {
                    return new Geometry.Box(this, this);
                } }

            public Color DefaultColor(Colors colors) { return colors.PointColor; }
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
                Drawer drawer = new Drawer(graphics, settings.color);

                double width = Dim(0);
                double height = Dim(1);
                float rw = cs.ConvertDimension(Math.Abs(width));
                float rh = cs.ConvertDimension(Math.Abs(height));

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

            public Geometry.Box Aabb { get { return Box_; } }
            public Geometry.Box Box_ { get; set; }

            public Color DefaultColor(Colors colors) { return colors.BoxColor; }
        }

        private class NSphere : Geometry.NSphere, IDrawable
        {
            public NSphere(Geometry.Point center, double radius)
                : base(center, radius)
            { }

            public void Draw(Geometry.Box box, Graphics graphics, Settings settings, Geometry.Traits traits)
            {
                LocalCS cs = new LocalCS(box, graphics);
                Drawer drawer = new Drawer(graphics, settings.color);
                
                float r = cs.ConvertDimension(Radius);

                if (r < 0)
                    return;

                if (traits.Unit == Geometry.Unit.None)
                {
                    PointF c = cs.Convert(Center);
                    if (r == 0)
                        drawer.DrawPoint(c.X, c.Y);
                    else
                    {
                        float x = c.X - r;
                        float y = c.Y - r;
                        float d = r * 2;
                        drawer.DrawEllipse(x, y, d, d);
                        drawer.FillEllipse(x, y, d, d);
                    }
                }
                else // Radian, Degree
                {
                    if (r == 0)
                        drawer.DrawPeriodicPoint(cs, Center, box, traits.Unit);
                    else
                    {
                        Drawer.PeriodicDrawableNSphere pd = new Drawer.PeriodicDrawableNSphere(cs, this, box, traits.Unit);
                        drawer.DrawPeriodic(pd, true, true, false, settings.showDir);
                    }
                }
            }

            public Geometry.Box Aabb { get { return Box; } }
            public Color DefaultColor(Colors colors) { return colors.NSphereColor; }

            public Geometry.Box Box { get; set; }
        }

        private class Segment : Geometry.Segment, IDrawable
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

            public Geometry.Box Aabb { get { return Box; } }
            public Color DefaultColor(Colors colors) { return colors.SegmentColor; }

            public Geometry.Box Box { get; set; }
        }

        private class Linestring : Geometry.Linestring, IDrawable
        {
            public void Draw(Geometry.Box box, Graphics graphics, Settings settings, Geometry.Traits traits)
            {
                LocalCS cs = new LocalCS(box, graphics);
                Drawer drawer = new Drawer(graphics, settings.color);

                if (traits.Unit == Geometry.Unit.None)
                {
                    for (int i = 1; i < Count; ++i)
                    {
                        PointF p0 = cs.Convert(this[i - 1]);
                        PointF p1 = cs.Convert(this[i]);
                        drawer.DrawLine(p0, p1, settings.showDir);
                    }
                }
                else // Radian, Degree
                {
                    Drawer.PeriodicDrawableRange pd = new Drawer.PeriodicDrawableRange(cs, this, box, traits.Unit);
                    drawer.DrawPeriodic(pd, false, false, settings.showDir, settings.showDir);
                }
            }

            public Geometry.Box Aabb { get { return Box; } }
            public Color DefaultColor(Colors colors) { return colors.LinestringColor; }

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

            public Geometry.Box Aabb { get { return Box; } }
            public Color DefaultColor(Colors colors) { return colors.RingColor; }

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
                Drawer drawer = new Drawer(graphics, settings.color);

                if (traits.Unit == Geometry.Unit.None)
                {
                    PointF[] dst_outer_points = cs.Convert(outer);
                    if (dst_outer_points != null)
                    {
                        GraphicsPath gp = new GraphicsPath();
                        gp.AddPolygon(dst_outer_points);

                        if (settings.showDir)
                        {
                            drawer.DrawDirs(dst_outer_points, true);
                            drawer.DrawPoint(dst_outer_points[0]);
                        }

                        foreach (Ring inner in inners)
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
                    Drawer.PeriodicDrawablePolygon pd = new Drawer.PeriodicDrawablePolygon(cs, outer, inners, box, traits.Unit);
                    drawer.DrawPeriodic(pd, true, true, settings.showDir, settings.showDir);

                    if (settings.showDir)
                    {
                        if (settings.showDir && outer.Count > 0)
                            drawer.DrawPeriodicPoint(cs, outer[0], box, traits.Unit);

                        foreach (Ring inner in inners)
                            if (inner.Count > 0)
                                drawer.DrawPeriodicPoint(cs, inner[0], box, traits.Unit);
                    }
                }
            }

            public Geometry.Box Aabb { get { return Box; } }
            public Color DefaultColor(Colors colors) { return colors.PolygonColor; }

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
            public Color DefaultColor(Colors colors) { return singles.Count > 0 ? singles.First().DefaultColor(colors) : colors.DrawColor; }

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

                double width = box.Dim(0);
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
            public Color DefaultColor(Colors colors) { return colors.DrawColor; }

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
                Drawer drawer = new Drawer(graphics, settings.color);
                SolidBrush text_brush = new SolidBrush(Color.Black);

                Font font = null;
                if (settings.showLabels)
                    font = new Font(new FontFamily(System.Drawing.Text.GenericFontFamilies.SansSerif), 10);

                Dictionary<System.Drawing.Point, string> labelsMap = new Dictionary<System.Drawing.Point, string>();

                int index = 0;
                foreach (Turn turn in turns)
                {
                    PointF p = cs.Convert(turn.point);
                    if (traits.Unit == Geometry.Unit.None)
                        drawer.DrawPoint(p);
                    else
                        drawer.DrawPeriodicPoint(cs, turn.point, box, traits.Unit);

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
            public Color DefaultColor(Colors colors) { return colors.TurnColor; }

            public Geometry.Box Box { get; set; }

            private List<Turn> turns;
        }

        // -------------------------------------------------
        // Loading expressions
        // -------------------------------------------------

        private static double LoadValue(Debugger debugger, string name)
        {
            Expression expr = debugger.GetExpression("(double)" + name);
            double v = double.Parse(expr.Value, System.Globalization.CultureInfo.InvariantCulture);
            return v;
        }

        private static Point LoadPoint(Debugger debugger, string name)
        {
            double x = LoadValue(debugger, name + "[0]");
            double y = LoadValue(debugger, name + "[1]");

            return new Point(x, y);
        }

        private static Box LoadGeometryBox(Debugger debugger, string name, bool calculateEnvelope, Geometry.Traits traits)
        {
            Point first_p = LoadPoint(debugger, name + ".m_min_corner");
            Point second_p = LoadPoint(debugger, name + ".m_max_corner");

            Box result = new Box(first_p, second_p);
            if (calculateEnvelope)
                result.Box_ = Geometry.Envelope(result, traits);
            else
                result.Box_ = Geometry.Aabb(result.Min, result.Max, traits.Unit);
            return result;
        }

        private static Box LoadPolygonBox(Debugger debugger, string name)
        {
            Point first_p = LoadPoint(debugger, name + ".ranges_[0]"); // interval X
            Point second_p = LoadPoint(debugger, name + ".ranges_[1]"); // interval Y

            Box result = new Box(new Point(first_p[0], second_p[0]),
                                 new Point(first_p[1], second_p[1]));
            // NOTE: Instead of this assignment Box_ could be always set in the constructor to this.
            result.Box_ = Geometry.Aabb(result.Min, result.Max, Geometry.Unit.None);
            return result;
        }

        private static Segment LoadSegment(Debugger debugger, string name, string first, string second, bool calculateEnvelope, Geometry.Traits traits)
        {
            Point first_p = LoadPoint(debugger, name + "." + first);
            Point second_p = LoadPoint(debugger, name + "." + second);

            Segment result = new Segment(first_p, second_p);
            if (calculateEnvelope)
                result.Box = Geometry.Envelope(result, traits);
            else
                result.Box = Geometry.Aabb(result, traits);
            return result;
        }

        private static NSphere LoadNSphere(Debugger debugger, string name, string center, string radius, bool calculateEnvelope, Geometry.Traits traits)
        {
            Point center_p = LoadPoint(debugger, name + "." + center);
            double radius_v = LoadValue(debugger, name + "." + radius);

            NSphere result = new NSphere(center_p, radius_v);
            Geometry.Point p_min = new Geometry.Point(center_p[0] - radius_v, center_p[1] - radius_v);
            Geometry.Point p_max = new Geometry.Point(center_p[0] + radius_v, center_p[1] + radius_v);
            if (calculateEnvelope)
                result.Box = Geometry.Envelope(p_min, p_max, traits);
            else
                result.Box = Geometry.Aabb(p_min, p_max, traits.Unit);
            return result;
        }

        private static Linestring LoadLinestring(Debugger debugger, string name, bool calculateEnvelope, Geometry.Traits traits)
        {
            Linestring result = new Linestring();

            int size = LoadSize(debugger, name);
            for (int i = 0; i < size; ++i)
            {
                Point p = LoadPoint(debugger, name + "[" + i + "]");
                result.Add(p);
            }

            if (calculateEnvelope)
                result.Box = Geometry.Envelope(result, traits);
            else
                result.Box = Geometry.Aabb(result, traits);
            return result;
        }

        private static Ring LoadRing(Debugger debugger, string name, string member, bool calculateEnvelope, Geometry.Traits traits)
        {
            string name_suffix = member.Length > 0 ? "." + member : "";
            Linestring ls = LoadLinestring(debugger, name + name_suffix, calculateEnvelope, traits);
            return new Ring(ls, ls.Box);
        }

        private static Polygon LoadPolygon(Debugger debugger, string name, string outer, string inners, bool inners_in_list, string ring_member, bool calculateEnvelope, Geometry.Traits traits)
        {
            Ring outer_r = LoadRing(debugger, name + "." + outer, ring_member, calculateEnvelope, traits);

            Geometry.Box box = (Geometry.Box)outer_r.Box.Clone();
            
            List<Geometry.Ring> inners_r = new List<Geometry.Ring>();

            ContainerElements inner_names = new ContainerElements(debugger, name + "." + inners);
            foreach(string inner_name in inner_names)
            {
                Ring inner_r = LoadRing(debugger, inner_name, ring_member, calculateEnvelope, traits);

                inners_r.Add(inner_r);

                if (calculateEnvelope)
                    Geometry.Expand(box, inner_r.Box, traits);
                else
                    Geometry.Expand(box, inner_r.Box);
            }

            return new Polygon(outer_r, inners_r, box);
        }

        private static Multi<Point> LoadMultiPoint(Debugger debugger, string name, bool calculateEnvelope, Geometry.Traits traits)
        {
            List<IDrawable> singles = new List<IDrawable>();
            Geometry.Box box = null;
            
            int size = LoadSize(debugger, name);

            for (int i = 0; i < size; ++i)
            {
                Point s = LoadPoint(debugger, name + "[" + i + "]");
                singles.Add(s);

                // TODO: in general it's not necessary to create a box here
                Geometry.Box b = new Geometry.Box(new Geometry.Point(s[0], s[1]),
                                                  new Geometry.Point(s[0], s[1]));

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
            {
                box = new Geometry.Box();
                Geometry.AssignInverse(box);
            }

            return new Multi<Point>(singles, box);
        }

        private static Multi<Linestring> LoadMultiLinestring(Debugger debugger, string name, bool calculateEnvelope, Geometry.Traits traits)
        {
            List<IDrawable> singles = new List<IDrawable>();
            Geometry.Box box = null;

            int size = LoadSize(debugger, name);

            for (int i = 0; i < size; ++i)
            {
                Linestring s = LoadLinestring(debugger, name + "[" + i + "]", calculateEnvelope, traits);
                singles.Add(s);

                if (box == null)
                    box = (Geometry.Box)s.Box.Clone();
                else
                {
                    if (calculateEnvelope)
                        Geometry.Expand(box, s.Box, traits);
                    else
                        Geometry.Expand(box, s.Box);
                }  
            }

            if (box == null)
            {
                box = new Geometry.Box();
                Geometry.AssignInverse(box);
            }

            return new Multi<Linestring>(singles, box);
        }

        private static Multi<Polygon> LoadMultiPolygon(Debugger debugger, string name, string outer, string inners, bool calculateEnvelope, Geometry.Traits traits)
        {
            List<IDrawable> singles = new List<IDrawable>();
            Geometry.Box box = null;

            int size = LoadSize(debugger, name);

            for (int i = 0; i < size; ++i)
            {
                Polygon s = LoadPolygon(debugger, name + "[" + i + "]", outer, inners, false, "", calculateEnvelope, traits);
                singles.Add(s);

                if (box == null)
                    box = (Geometry.Box)s.Box.Clone();
                else
                {
                    if (calculateEnvelope)
                        Geometry.Expand(box, s.Box, traits);
                    else
                        Geometry.Expand(box, s.Box);
                }
            }

            if (box == null)
            {
                box = new Geometry.Box();
                Geometry.AssignInverse(box);
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
                double height = box.Dim(1);
                double threshold = float.Epsilon;
                if (height > threshold)
                {
                    box.Max[0] = box.Min[0] + height;
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
                            else if (cs_base_type == "boost::geometry::cs::spherical_equatorial")
                                coordinateSystem = Geometry.CoordinateSystem.SphericalEquatorial;
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

        private static DrawablePair LoadGeometry(Debugger debugger, string name, string type, bool calculateEnvelope)
        {
            IDrawable d = null;
            Geometry.Traits traits = null;
            
            if ((traits = LoadPointTraits(type)) != null)
                d = LoadPoint(debugger, name);
            else if ((traits = LoadGeometryTraits(type, "boost::geometry::model::box")) != null)
                d = LoadGeometryBox(debugger, name, calculateEnvelope, traits);
            else if ((traits = LoadGeometryTraits(type, "boost::geometry::model::nsphere")) != null)
                d = LoadNSphere(debugger, name, "m_center", "m_radius", calculateEnvelope, traits);
            else if ((traits = LoadGeometryTraits(type, "boost::geometry::model::segment")) != null
                  || (traits = LoadGeometryTraits(type, "boost::geometry::model::referring_segment")) != null)
                d = LoadSegment(debugger, name, "first", "second", calculateEnvelope, traits);
            else if ((traits = LoadGeometryTraits(type, "boost::geometry::model::linestring")) != null)
                d = LoadLinestring(debugger, name, calculateEnvelope, traits);
            else if ((traits = LoadGeometryTraits(type, "boost::geometry::model::ring")) != null)
                d = LoadRing(debugger, name, "", calculateEnvelope, traits);
            else if ((traits = LoadGeometryTraits(type, "boost::geometry::model::polygon")) != null)
                d = LoadPolygon(debugger, name, "m_outer", "m_inners", false, "", calculateEnvelope, traits);
            else if ((traits = LoadGeometryTraits(type, "boost::geometry::model::multi_point")) != null)
                d = LoadMultiPoint(debugger, name, calculateEnvelope, traits);
            else if ((traits = LoadGeometryTraits(type, "boost::geometry::model::multi_linestring", "boost::geometry::model::linestring")) != null)
                d = LoadMultiLinestring(debugger, name, calculateEnvelope, traits);
            else if ((traits = LoadGeometryTraits(type, "boost::geometry::model::multi_polygon", "boost::geometry::model::polygon")) != null)
                d = LoadMultiPolygon(debugger, name, "m_outer", "m_inners", calculateEnvelope, traits);
            else
            {
                traits = new Geometry.Traits(2); // 2D cartesian;

                string base_type = Util.BaseType(type);
                if (base_type == "boost::polygon::point_data")
                    d = LoadPoint(debugger, name);
                else if (base_type == "boost::polygon::segment_data")
                    d = LoadSegment(debugger, name, "points_[0]", "points_[1]", false, traits);
                else if (base_type == "boost::polygon::rectangle_data")
                    d = LoadPolygonBox(debugger, name);
                else if (base_type == "boost::polygon::polygon_data")
                    d = LoadRing(debugger, name, "coords_", false, traits);
                else if (base_type == "boost::polygon::polygon_with_holes_data")
                    d = LoadPolygon(debugger, name, "self_", "holes_", true, "coords_", false, traits);

                if (d == null)
                    traits = null;
            }

            return new DrawablePair(d, traits);
        }

        private static DrawablePair LoadGeometryOrVariant(Debugger debugger, string name, string type, bool calculateEnvelope)
        {
            // Currently the supported types are hardcoded as follows:

            // Boost.Geometry models
            DrawablePair result = LoadGeometry(debugger, name, type, calculateEnvelope);

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
                                result = LoadGeometry(debugger, value_str, expr_value.Type, calculateEnvelope);
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

        private static DrawablePair LoadDrawable(Debugger debugger, string name, string type, bool calculateEnvelope)
        {
            DrawablePair res = LoadGeometryOrVariant(debugger, name, type, calculateEnvelope);

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

        private static DrawablePair MakeGeometry(Debugger debugger, string name, bool calculateEnvelope)
        {
            Expression expr = debugger.GetExpression(name);
            if (!expr.IsValidValue)
                return new DrawablePair(null ,null);

            DrawablePair res = LoadGeometryOrVariant(debugger, expr.Name, expr.Type, calculateEnvelope);
            if (res.Drawable != null)
                return res;

            return LoadTurnsContainer(debugger, expr.Name, expr.Type);
        }

        private static DrawablePair MakeDrawable(Debugger debugger, string name, bool calculateEnvelope)
        {
            Expression expr = debugger.GetExpression(name);
            if (!expr.IsValidValue)
                return new DrawablePair(null, null);

            return LoadDrawable(debugger, expr.Name, expr.Type, calculateEnvelope);
        }

        // -------------------------------------------------
        // public Draw
        // -------------------------------------------------

        // For GraphicalWatch
        public static bool Draw(Graphics graphics, Debugger debugger, string name, Colors colors)
        {
            try
            {
                DrawablePair d = MakeDrawable(debugger, name, true);
                if (d.Drawable != null)
                {
                    if (d.Traits.CoordinateSystem == Geometry.CoordinateSystem.Spherical)
                    {
                        throw new Exception("This coordinate system is not yet supported.");
                    }
                    
                    Settings settings = new Settings(d.Drawable.DefaultColor(colors));
                    d.Drawable.Draw(d.Drawable.Aabb, graphics, settings, d.Traits);
                    return true;
                }
            }
            catch(Exception e)
            {
                Drawer.DrawMessage(graphics, e.Message, Color.Red);
            }

            return false;
        }

        // For GeometryWatch
        public static bool DrawGeometries(Graphics graphics, Debugger debugger, string[] names, Settings[] settings, Colors colors)
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
                        drawables[i] = ExpressionDrawer.MakeGeometry(debugger, names[i], false);

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

                if (drawnCount > 0)
                {
                    if (csystems.Count > 1)
                    {
                        throw new Exception("Multiple coordinate systems detected.");
                    }
                    if (csystems.First() == Geometry.CoordinateSystem.Spherical)
                    {
                        throw new Exception("This coordinate system is not yet supported.");
                    }
                    if (units.Count > 1)
                    {
                        throw new Exception("Multiple units detected.");
                    }

                    Geometry.Traits traits = new Geometry.Traits(dimensions.Max(), csystems.First(), units.First());

                    // Aabb
                    Drawer.DrawAabb(graphics, box, traits, colors);

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

                    return true;
                }
            }
            catch (Exception e)
            {
                Drawer.DrawMessage(graphics, e.Message, Color.Red);
            }

            return false;
        }
        
    }
}
