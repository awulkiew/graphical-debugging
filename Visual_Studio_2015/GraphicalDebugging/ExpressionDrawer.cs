//------------------------------------------------------------------------------
// <copyright file="ExpressionDrawer.cs">
//     Copyright (c) Adam Wulkiewicz.
// </copyright>
//------------------------------------------------------------------------------

using System;
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
        public interface IDrawable
        {
            void Draw(Graphics graphics);
            void Draw(Box box, Graphics graphics);
            void Draw(Box box, Graphics graphics, Color color);
            Box Aabb { get; }
        }

        public class Point : IDrawable
        {
            public Point(double x, double y)
            {
                coords = new double[2] { x, y };
            }

            public static Point Load(Debugger debugger, string name)
            {
                string name_prefix = "(double)" + name;
                Expression expr_x = debugger.GetExpression(name_prefix + "[0]");
                Expression expr_y = debugger.GetExpression(name_prefix + "[1]");

                double x = double.Parse(expr_x.Value, System.Globalization.CultureInfo.InvariantCulture);
                double y = double.Parse(expr_y.Value, System.Globalization.CultureInfo.InvariantCulture);
                
                Point result = new Point(x, y);
                return result;
            }

            // Traversing the Expression is even slower than the currently used solution
            /*public static Point Load(Expression expr)
            {
                double x = 0, y = 0;

                int index = 0;
                foreach(Expression e in expr.DataMembers)
                {
                    if (index > 1)
                        break;

                    if ( e.IsValidValue )
                    {
                        if (index == 0)
                            x = double.Parse(e.Value, System.Globalization.CultureInfo.InvariantCulture);
                        else if (index == 1)
                            y = double.Parse(e.Value, System.Globalization.CultureInfo.InvariantCulture);
                    }

                    ++index;
                }
                
                Point result = new Point(x, y);
                return result;
            }*/

            public void Draw(Graphics graphics)
            {
                this.Draw(new Box(this, this), graphics);
            }

            public void Draw(Box box, Graphics graphics)
            {
                this.Draw(box, graphics, Color.Orange);
            }

            public void Draw(Box box, Graphics graphics, Color color)
            {
                LocalCS cs = new LocalCS(box, graphics);

                Pen pen = new Pen(Color.FromArgb(112, color), 2);
                SolidBrush brush = new SolidBrush(Color.FromArgb(64, color));

                float x = cs.ConvertX(coords[0]);
                float y = cs.ConvertY(coords[1]);
                graphics.FillEllipse(brush, x - 2.5f, y - 2.5f, 5, 5);
                graphics.DrawEllipse(pen, x - 2.5f, y - 2.5f, 5, 5);
            }

            public Box Aabb { get { return new Box(this, this); } }

            public double this[int i]
            {
                get { return coords[i]; }
                set { coords[i] = value; }
            }

            private double[] coords;
        }

        public class Box : IDrawable
        {
            public static Box Inverted()
            {
                return new Box(
                    new Point(double.MaxValue, double.MaxValue),
                    new Point(double.MinValue, double.MinValue));
            }

            public Box(Point min, Point max)
            {
                this.min = min;
                this.max = max;
            }

            public void Expand(Point p)
            {
                if (p[0] < min[0]) min[0] = p[0];
                if (p[1] < min[1]) min[1] = p[1];
                if (p[0] > max[0]) max[0] = p[0];
                if (p[1] > max[1]) max[1] = p[1];
            }

            public void Expand(Box b)
            {
                if (b.min[0] < min[0]) min[0] = b.min[0];
                if (b.min[1] < min[1]) min[1] = b.min[1];
                if (b.max[0] > max[0]) max[0] = b.max[0];
                if (b.max[1] > max[1]) max[1] = b.max[1];
            }

            public static Box Load(Debugger debugger, string name)
            {
                Point min_p = Point.Load(debugger, name + ".min_corner()");
                Point max_p = Point.Load(debugger, name + ".max_corner()");

                Box result = new Box(min_p, max_p);
                return result;
            }

            public void Draw(Graphics graphics)
            {
                this.Draw(this, graphics);
            }

            public void Draw(Box box, Graphics graphics)
            {
                this.Draw(box, graphics, Color.Red);
            }

            public void Draw(Box box, Graphics graphics, Color color)
            {
                LocalCS cs = new LocalCS(box, graphics);

                Pen pen = new Pen(Color.FromArgb(112, color), 2);
                SolidBrush brush = new SolidBrush(Color.FromArgb(64, color));

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

            public Box Aabb { get { return this; } }

            public double Width { get { return max[0] - min[0]; } }
            public double Height { get { return max[1] - min[1]; } }

            public Point min, max;
        }

        public class Segment : IDrawable
        {
            private Segment() { }

            public static Segment Load(Debugger debugger, string name)
            {
                Segment result = new Segment();

                result.p0 = Point.Load(debugger, name + ".first");
                result.p1 = Point.Load(debugger, name + ".second");

                result.box = Box.Inverted();
                result.box.Expand(result.p0);
                result.box.Expand(result.p1);

                return result;
            }

            public void Draw(Graphics graphics)
            {
                this.Draw(box, graphics);
            }

            public void Draw(Box box, Graphics graphics)
            {
                this.Draw(box, graphics, Color.YellowGreen);
            }

            public void Draw(Box box, Graphics graphics, Color color)
            {
                LocalCS cs = new LocalCS(box, graphics);

                Pen pen = new Pen(Color.FromArgb(112, color), 2);

                graphics.DrawLine(pen, cs.ConvertX(p0[0]), cs.ConvertY(p0[1]),
                                       cs.ConvertX(p1[0]), cs.ConvertY(p1[1]));
            }

            public Box Aabb { get { return box; } }

            public Point this[int i] { get { return i == 0 ? p0 : p1; } }
            public int Count { get { return 2; } }

            private Box box;
            private Point p0;
            private Point p1;
        }

        public class Linestring : IDrawable
        {
            private Linestring() { }

            public static Linestring Load(Debugger debugger, string name)
            {
                Linestring result = new Linestring();
                result.box = Box.Inverted();
                result.points = new List<Point>();

                // Traversing the Expression is even slower than the currently used solution
                /*Expression expr = debugger.GetExpression(name);
                if (!expr.IsValidValue)
                    return result;

                // capacity, allocator, N elements, Raw view
                int index = 0;
                foreach (Expression e in expr.DataMembers)
                {
                    if ( e.IsValidValue )
                    {
                        if ( index >= 2 && index < expr.DataMembers.Count - 1)
                        {
                            Point p = Point.Load(e);
                            result.points.Add(p);
                            result.box.Expand(p);
                        }
                    }

                    ++index;
                }*/

                int size = LoadSize(debugger, name);
                for (int i = 0; i < size; ++i)
                {
                    Point p = Point.Load(debugger, name + "[" + i + "]");
                    result.points.Add(p);
                    result.box.Expand(p);
                }

                return result;
            }

            public void Draw(Graphics graphics)
            {
                this.Draw(box, graphics);
            }

            public void Draw(Box box, Graphics graphics)
            {
                this.Draw(box, graphics, Color.Green);
            }

            public void Draw(Box box, Graphics graphics, Color color)
            {
                LocalCS cs = new LocalCS(box, graphics);

                Pen pen = new Pen(Color.FromArgb(112, color), 2);

                for (int i = 1; i < points.Count; ++i)
                {
                    Point p0 = points[i - 1];
                    Point p1 = points[i];
                    graphics.DrawLine(pen, cs.ConvertX(p0[0]), cs.ConvertY(p0[1]),
                                           cs.ConvertX(p1[0]), cs.ConvertY(p1[1]));
                }
            }

            public Box Aabb { get { return box; } }

            public Point this[int i] { get { return points[i]; } }
            public int Count { get { return points.Count; } }

            private Box box;
            private List<Point> points;
        }

        public class Ring : IDrawable
        {
            private Ring() { }

            public static Ring Load(Debugger debugger, string name)
            {
                Ring result = new Ring();
                result.linestring = Linestring.Load(debugger, name);
                return result;
            }

            public PointF[] Convert(Box box, Graphics graphics)
            {
                LocalCS cs = new LocalCS(box, graphics);

                if (this.Count <= 0)
                    return null;

                int dst_count = this.Count + (this[0] == this[this.Count - 1] ? 0 : 1);

                PointF[] dst_points = new PointF[dst_count];
                int i = 0;
                for (; i < this.Count; ++i)
                {
                    Point p = this[i];
                    dst_points[i] = new PointF(cs.ConvertX(p[0]), cs.ConvertY(p[1]));
                }
                if (i < dst_count)
                {
                    Point p = this[0];
                    dst_points[i] = new PointF(cs.ConvertX(p[0]), cs.ConvertY(p[1]));
                }

                return dst_points;
            }

            public void Draw(Graphics graphics)
            {
                this.Draw(Aabb, graphics);
            }

            public void Draw(Box box, Graphics graphics)
            {
                this.Draw(box, graphics, Color.SlateBlue);
            }

            public void Draw(Box box, Graphics graphics, Color color)
            {
                PointF[] dst_points = Convert(box, graphics);

                if (dst_points != null)
                {
                    Pen pen = new Pen(Color.FromArgb(112, color), 2);
                    SolidBrush brush = new SolidBrush(Color.FromArgb(64, color));
                    
                    graphics.FillPolygon(brush, dst_points);
                    graphics.DrawPolygon(pen, dst_points);
                }
            }

            public Box Aabb { get { return linestring.Aabb; } }

            public Point this[int i] { get { return linestring[i]; } }
            public int Count { get { return linestring.Count; } }

            private Linestring linestring;
        }

        public class Polygon : IDrawable
        {
            private Polygon() {}

            public static Polygon Load(Debugger debugger, string name)
            {
                Polygon result = new Polygon();

                result.outer = Ring.Load(debugger, name + ".m_outer");
                result.inners = new List<Ring>();
                result.box = Box.Inverted();
                result.box.Expand(result.outer.Aabb);

                int inners_size = LoadSize(debugger, name + ".m_inners");                
                for (int i = 0; i < inners_size; ++i)
                {
                    Ring r = Ring.Load(debugger, name + ".m_inners[" + i + "]");
                    result.inners.Add(r);
                    result.box.Expand(r.Aabb);
                }

                return result;
            }

            public void Draw(Graphics graphics)
            {
                this.Draw(Aabb, graphics);
            }

            public void Draw(Box box, Graphics graphics)
            {
                this.Draw(box, graphics, Color.RoyalBlue);
            }

            public void Draw(Box box, Graphics graphics, Color color)
            {
                PointF[] dst_outer_points = outer.Convert(box, graphics);
                if (dst_outer_points != null)
                {
                    Pen pen = new Pen(Color.FromArgb(112, color), 2);
                    SolidBrush brush = new SolidBrush(Color.FromArgb(64, color));

                    GraphicsPath gp = new GraphicsPath();
                    gp.AddPolygon(dst_outer_points);

                    foreach(Ring inner in inners)
                    {
                        PointF[] dst_inner_points = inner.Convert(Aabb, graphics);
                        if (dst_inner_points != null)
                        {
                            gp.AddPolygon(dst_inner_points);
                        }
                    }

                    graphics.FillPath(brush, gp);
                    graphics.DrawPath(pen, gp);
                }
            }

            public Box Aabb { get { return box; } }

            private Box box;
            private Ring outer;
            private List<Ring> inners;
        }

        public class Multi : IDrawable
        {
            private Multi() { }

            public enum Single
            {
                Point,
                Linestring,
                Polygon
            };

            public static Multi Load(Debugger debugger, string name, Single single)
            {
                Multi result = new Multi();
                result.singles = new List<IDrawable>();
                result.box = Box.Inverted();

                int size = LoadSize(debugger, name);
                
                if (single == Single.Point)
                {
                    for (int i = 0; i < size; ++i)
                    {
                        Point s = Point.Load(debugger, name + "[" + i + "]");
                        result.singles.Add(s);
                        result.box.Expand(s);
                    }
                }
                else if (single == Single.Linestring)
                {
                    for (int i = 0; i < size; ++i)
                    {
                        Linestring s = Linestring.Load(debugger, name + "[" + i + "]");
                        result.singles.Add(s);
                        result.box.Expand(s.Aabb);
                    }
                }
                else if (single == Single.Polygon)
                {
                    for (int i = 0; i < size; ++i)
                    {
                        Polygon s = Polygon.Load(debugger, name + "[" + i + "]");
                        result.singles.Add(s);
                        result.box.Expand(s.Aabb);
                    }
                }
                
                return result;
            }

            public void Draw(Graphics graphics)
            {
                this.Draw(Aabb, graphics);
            }

            public void Draw(Box box, Graphics graphics)
            {
                foreach (IDrawable single in singles)
                {
                    single.Draw(box, graphics);
                }
            }

            public void Draw(Box box, Graphics graphics, Color color)
            {
                foreach (IDrawable single in singles)
                {
                    single.Draw(box, graphics, color);
                }
            }

            public Box Aabb { get { return box; } }

            private Box box;

            private List<IDrawable> singles;
        }

        public class ValuesContainer : IDrawable
        {
            private ValuesContainer() { }

            public static ValuesContainer Load(Debugger debugger, string name, int size)
            {
                ValuesContainer result = new ValuesContainer();
                result.values = new List<double>();
                result.box = Box.Inverted();

                if (size > 0)
                    result.box.Expand(new Point(0.0, 0.0));

                for (int i = 0; i < size; ++i)
                {
                    Expression expr = debugger.GetExpression("(double)" + name + "[" + i + "]");
                    if (!expr.IsValidValue)
                        continue;
                    double v = double.Parse(expr.Value, System.Globalization.CultureInfo.InvariantCulture);
                    result.values.Add(v);
                    result.box.Expand(new Point(i, v));
                }

                // make square
                if (size > 0)
                    result.box.max[0] = result.box.min[0] + result.box.Height;

                return result;
            }

            public void Draw(Graphics graphics)
            {
                this.Draw(Aabb, graphics);
            }

            public void Draw(Box box, Graphics graphics)
            {
                this.Draw(box, graphics, Color.Black);
            }

            public void Draw(Box box, Graphics graphics, Color color)
            {
                LocalCS cs = new LocalCS(box, graphics);

                Pen axis_pen = new Pen(color, 2);
                Pen pen = new Pen(Color.FromArgb(112, color), 1);

                float ax0 = cs.ConvertX(box.min[0]);
                float ax1 = cs.ConvertX(box.max[0]);
                float ay = cs.ConvertY(0);
                graphics.DrawLine(axis_pen, ax0, ay, ax1, ay);

                double i = 0.0;
                double step = box.Width / values.Count;
                foreach (double v in values)
                {
                    float x = cs.ConvertX(i);
                    float y = cs.ConvertY(v);
                    graphics.DrawLine(pen, x, ay, x, y);
                    i += step;
                }
            }

            public Box Aabb { get { return box; } }

            private Box box;

            private List<double> values;
        }

        private static List<string> Tparams(string type)
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
                    result.Add(type.Substring(param_first, param_last - param_first));
                    param_first = -1;
                    param_last = -1;
                }

                ++index;
            }

            return result;
        }

        // For now the list of handled types is hardcoded
        private static bool IsGeometry(string type)
        {
            if (type.StartsWith("boost::geometry::model::point"))
            {
                List<string> tparams = Tparams(type);
                return tparams.Count == 3 && tparams[1] == "2"; // 2D only for now
            }
            else if (type.StartsWith("boost::geometry::model::d2::point_xy"))
            {
                return true;
            }

            return false;
        }

        private static bool IsGeometry(string type, string single_type)
        {
            if (!type.StartsWith(single_type))
                return false;
            List<string> tparams = Tparams(type);
            return tparams.Count > 0 && IsGeometry(tparams[0]);
        }

        private static bool IsGeometry(string type, string multi_type, string single_type)
        {
            if (!type.StartsWith(multi_type))
                return false;
            List<string> tparams = Tparams(type);
            if (!(tparams.Count > 0 && IsGeometry(tparams[0], single_type)))
                return false;
            List<string> tparams2 = Tparams(tparams[0]);
            return tparams2.Count > 0 && IsGeometry(tparams2[0]);
        }

        private static IDrawable LoadGeometry(Debugger debugger, string name, string type)
        {
            IDrawable d = null;

            if (IsGeometry(type))
                d = Point.Load(debugger, name);
            else if (IsGeometry(type, "boost::geometry::model::box"))
                d = Box.Load(debugger, name);
            else if (IsGeometry(type, "boost::geometry::model::segment")
                  || IsGeometry(type, "boost::geometry::model::referring_segment"))
                d = Segment.Load(debugger, name);
            else if (IsGeometry(type, "boost::geometry::model::linestring"))
                d = Linestring.Load(debugger, name);
            else if (IsGeometry(type, "boost::geometry::model::ring"))
                d = Ring.Load(debugger, name);
            else if (IsGeometry(type, "boost::geometry::model::polygon"))
                d = Polygon.Load(debugger, name);
            else if (IsGeometry(type, "boost::geometry::model::multi_point"))
                d = Multi.Load(debugger, name, Multi.Single.Point);
            else if (IsGeometry(type, "boost::geometry::model::multi_linestring", "boost::geometry::model::linestring"))
                d = Multi.Load(debugger, name, Multi.Single.Linestring);
            else if (IsGeometry(type, "boost::geometry::model::multi_polygon", "boost::geometry::model::polygon"))
                d = Multi.Load(debugger, name, Multi.Single.Polygon);

            return d;
        }

        private static IDrawable LoadGeometryOrVariant(Debugger debugger, string name, string type)
        {
            // Currently the supported types are hardcoded as follows:

            // Boost.Geometry models
            IDrawable d = LoadGeometry(debugger, name, type);

            if (d == null)
            {
                // Boost.Variant containing a Boost.Geometry model
                if (type.StartsWith("boost::variant"))
                {
                    Expression expr_which = debugger.GetExpression(name + ".which_");
                    if (!expr_which.IsValidValue)
                        return null;
                    int which = int.Parse(expr_which.Value);
                    List<string> tparams = Tparams(type);
                    if (which < 0 || which >= tparams.Count)
                        return null;
                    string value_str = "(*(" + tparams[which] + "*)" + name + ".storage_.data_.buf)";
                    Expression expr_value = debugger.GetExpression(value_str);
                    if (!expr_value.IsValidValue)
                        return null;
                    d = LoadGeometry(debugger, value_str, expr_value.Type);
                }
            }

            return d;
        }

        private static IDrawable LoadDrawable(Debugger debugger, string name, string type)
        {
            IDrawable d = LoadGeometryOrVariant(debugger, name, type);

            if (d == null)
            {
                // STL RandomAccess container of 1D values convertible to double
                if (type.StartsWith("std::vector")
                    || type.StartsWith("std::deque"))
                {
                    int size = LoadSize(debugger, name);
                    d = ValuesContainer.Load(debugger, name, size);
                }
                // Boost.Array of 1D values convertible to double
                else if (type.StartsWith("boost::array"))
                {
                    List<string> tparams = Tparams(type);
                    int size = int.Parse(tparams[1]);
                    d = ValuesContainer.Load(debugger, name, size);
                }
            }

            return d;
        }

        public static IDrawable MakeGeometry(Debugger debugger, string name)
        {
            Expression expr = debugger.GetExpression(name);
            if (!expr.IsValidValue)
                return null;

            return LoadGeometryOrVariant(debugger, expr.Name, expr.Type);
        }

        public static IDrawable MakeDrawable(Debugger debugger, string name)
        {
            Expression expr = debugger.GetExpression(name);
            if (!expr.IsValidValue)
                return null;

            return LoadDrawable(debugger, expr.Name, expr.Type);
        }

        public static bool DrawGeometry(Graphics graphics, Debugger debugger, string name)
        {
            IDrawable d = MakeGeometry(debugger, name);
            if (d == null)
                return false;
            d.Draw(graphics);
            return true;
        }

        public static bool Draw(Graphics graphics, Debugger debugger, string name)
        {
            IDrawable d = MakeDrawable(debugger, name);
            if (d == null)
                return false;
            d.Draw(graphics);
            return true;
        }

        public static bool DrawAabb(Graphics graphics, Box box)
        {
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

        private static int LoadSize(Debugger debugger, string name)
        {
            // VS2015 vector
            Expression expr_inners_size = debugger.GetExpression(name + "._Mypair._Myval2._Mylast-" + name + "._Mypair._Myval2._Myfirst");
            if (expr_inners_size.IsValidValue)
            {
                int result = int.Parse(expr_inners_size.Value);
                return Math.Max(result, 0);
            }
            // VS2013 vector
            Expression expr_inners_size2 = debugger.GetExpression(name + "._Mylast-" + name + "._Myfirst");
            if (expr_inners_size2.IsValidValue)
            {
                int result = int.Parse(expr_inners_size2.Value);
                return Math.Max(result, 0);
            }
            // deque, list, etc.
            Expression expr_inners_size3 = debugger.GetExpression(name + "._Mysize");
            if (expr_inners_size3.IsValidValue)
            {
                int result = int.Parse(expr_inners_size3.Value);
                return Math.Max(result, 0);
            }

            return 0;
        }

        private class LocalCS
        {
            public LocalCS(Box src_box, Graphics dst_graphics)
            {
                float w = dst_graphics.VisibleClipBounds.Width;
                float h = dst_graphics.VisibleClipBounds.Height;
                dst_x0 = w / 2;
                dst_y0 = h / 2;
                float dst_w = w * 0.9f;
                float dst_h = h * 0.9f;

                double src_w = src_box.Width;
                double src_h = src_box.Height;
                src_x0 = src_box.min[0] + src_w / 2;
                src_y0 = src_box.min[1] + src_h / 2;

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

            float dst_x0, dst_y0;
            double src_x0, src_y0;
            double scale;
        }
    }
}
