//------------------------------------------------------------------------------
// <copyright file="Viewport.cs">
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
    class Drawer
    {
        private Graphics graphics;
        private Pen pen;
        private SolidBrush brush;

        public Drawer(Graphics graphics, Color color)
        {
            this.graphics = graphics;
            pen = new Pen(Color.FromArgb(112, color), 2);
            pen.LineJoin = LineJoin.Round;
            pen.EndCap = LineCap.Round;
            brush = new SolidBrush(Color.FromArgb(64, color));
        }

        public bool DrawDir(PointF p0, PointF p1)
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

        public void DrawDirs(PointF[] points, bool closed)
        {
            for (int i = 1; i < points.Length; ++i)
            {
                DrawDir(points[i - 1], points[i]);
            }
            if (closed && points.Length > 1)
            {
                DrawDir(points[points.Length - 1], points[0]);
            }
        }

        public void DrawPoint(float x, float y, Pen pen)
        {
            float mx = x - 2.5f; // r=2.5f
            float my = y - 2.5f;
            float d = 5;
            graphics.DrawEllipse(pen, mx, my, d, d);
            graphics.FillEllipse(brush, mx, my, d, d);
        }
        public void DrawPoint(float x, float y) { DrawPoint(x, y, pen); }
        public void DrawPoint(PointF p, Pen pen) { DrawPoint(p.X, p.Y, pen); }
        public void DrawPoint(PointF p) { DrawPoint(p.X, p.Y, pen); }

        public void DrawLine(float x1, float y1, float x2, float y2)
        {
            graphics.DrawLine(pen, x1, y1, x2, y2);
        }

        public void DrawLine(PointF p0, PointF p1, bool drawDir)
        {
            graphics.DrawLine(pen, p0, p1);
            if (drawDir)
                DrawDir(p0, p1);
        }

        public void DrawLines(PointF[] points, bool closed, bool drawDir)
        {
            graphics.DrawLines(pen, points);
            if (closed && points.Length > 1)
                graphics.DrawLine(pen, points[points.Length - 1], points[0]);
            if (drawDir)
                DrawDirs(points, closed);
        }

        public void DrawLine(PointF p0, PointF p1, float x0_orig, float x1_orig, bool drawDir, bool drawDots)
        {
            bool sameP0 = Math.Abs(p0.X - x0_orig) < 0.001;
            bool sameP1 = Math.Abs(p1.X - x1_orig) < 0.001;
            //bool sameP0 = p0.X == x0_orig;
            //bool sameP1 = p1.X == x1_orig;
            if (!drawDots || sameP0 && sameP1)
            {
                DrawLine(p0, p1, drawDir);
            }
            else
            {
                Pen pend = (Pen)pen.Clone();
                pend.DashStyle = DashStyle.Dot;

                if (sameP0 || sameP1)
                {
                    PointF ph = AddF(p0, DivF(SubF(p1, p0), 2));
                    graphics.DrawLine(sameP0 ? pen : pend, p0, ph);
                    graphics.DrawLine(sameP1 ? pen : pend, ph, p1);
                }
                else
                {
                    graphics.DrawLine(pend, p0, p1);
                }

                if (drawDir)
                    DrawDir(p0, p1);
            }
        }

        public void DrawLines(PointF[] points_rel, float translation_x, float[] xs_orig, bool closed, bool drawDir, bool drawDots)
        {
            for (int i = 1; i < points_rel.Length; ++i)
            {
                int i_1 = i - 1;
                PointF p0 = new PointF(points_rel[i_1].X + translation_x, points_rel[i_1].Y);
                PointF p1 = new PointF(points_rel[i].X + translation_x, points_rel[i].Y);
                DrawLine(p0, p1, xs_orig[i_1], xs_orig[i], drawDir, drawDots);
            }
            if (closed && points_rel.Length > 1)
            {
                int i_1 = points_rel.Length - 1;
                PointF p0 = new PointF(points_rel[i_1].X + translation_x, points_rel[i_1].Y);
                PointF p1 = new PointF(points_rel[0].X + translation_x, points_rel[0].Y);
                DrawLine(p0, p1, xs_orig[i_1], xs_orig[0], drawDir, drawDots);
            }
        }

        public void DrawEllipse(float x, float y, float w, float h)
        {
            graphics.DrawEllipse(pen, x, y, w, h);
        }

        public void FillEllipse(float x, float y, float w, float h)
        {
            graphics.FillEllipse(brush, x, y, w, h);
        }

        public void DrawRectangle(float x, float y, float w, float h)
        {
            graphics.DrawRectangle(pen, x, y, w, h);
        }

        public void FillRectangle(float x, float y, float w, float h)
        {
            graphics.FillRectangle(brush, x, y, w, h);
        }

        public void DrawPolygon(PointF[] points)
        {
            graphics.DrawPolygon(pen, points);
        }

        public void FillPolygon(PointF[] points)
        {
            graphics.FillPolygon(brush, points);
        }

        public void DrawPath(GraphicsPath path)
        {
            graphics.DrawPath(pen, path);
        }

        public void FillPath(GraphicsPath path)
        {
            graphics.FillPath(brush, path);
        }

        public void DrawPeriodicPoint(LocalCS cs, Geometry.Point point, Geometry.Box box, Geometry.Unit unit)
        {
            PointF p = cs.Convert(point);
            DrawPoint(p);

            Pen penDot = (Pen)pen.Clone();
            penDot.DashStyle = DashStyle.Dot;

            double pi2 = 2 * Geometry.HalfAngle(unit);
            // draw points on the west
            double x_tmp = point[0] - pi2;
            while (x_tmp >= box.Min[0])
            {
                p.X = cs.ConvertX(x_tmp);
                DrawPoint(p, penDot);
                x_tmp -= pi2;
            }
            // draw points on the east
            x_tmp = point[0] + pi2;
            while (x_tmp <= box.Max[0])
            {
                p.X = cs.ConvertX(x_tmp);
                DrawPoint(p, penDot);
                x_tmp += pi2;
            }
        }

        public interface IPeriodicDrawable
        {
            void DrawOne(Drawer drawer, float translation, bool closed, bool fill, bool drawDirs, bool drawDots);
            bool Good();

            float minf { get; }
            float maxf { get; }
            float periodf { get; }
            float box_minf { get; }
            float box_maxf { get; }
        }

        public class PeriodicDrawableRange : IPeriodicDrawable
        {
            protected PeriodicDrawableRange() { }

            public PeriodicDrawableRange(LocalCS cs, Geometry.IRandomAccessRange<Geometry.Point> points, Geometry.Box box, Geometry.Unit unit)
            {
                if (points.Count < 2)
                    return;

                double pi = Geometry.HalfAngle(unit);
                periodf = cs.ConvertDimension(2 * pi);

                xs_orig = new float[points.Count];
                points_rel = new PointF[points.Count];

                xs_orig[0] = cs.ConvertX(points[0][0]);
                points_rel[0] = cs.Convert(points[0]);

                minf = points_rel[0].X;
                maxf = points_rel[0].X;

                double x0 = Geometry.NormalizedAngle(points[0][0], unit);
                double x0_prev = points[0][0];
                for (int i = 1; i < points.Count; ++i)
                {
                    xs_orig[i] = cs.ConvertX(points[i][0]);

                    double x1 = Geometry.NormalizedAngle(points[i][0], unit);
                    double dist = x1 - x0; // [-2pi, 2pi]
                    double distNorm = Geometry.NormalizedAngle(dist, unit); // [-pi, pi]

                    double x0_curr = x0_prev + distNorm;
                    points_rel[i] = new PointF(cs.ConvertX(x0_curr),
                                               cs.ConvertY(points[i][1]));

                    // expand relative box X
                    if (points_rel[i].X < minf)
                        minf = points_rel[i].X;
                    if (points_rel[i].X > maxf)
                        maxf = points_rel[i].X;

                    x0_prev = x0_curr;
                    x0 = x1;
                }

                box_minf = cs.ConvertX(box.Min[0]);
                box_maxf = cs.ConvertX(box.Max[0]);
            }

            public void DrawOne(Drawer drawer, float translation, bool closed, bool fill, bool drawDirs, bool drawDots)
            {
                if (!Good())
                    return;

                drawer.DrawLines(points_rel, translation, xs_orig, closed, drawDirs, drawDots);

                if (fill)
                {
                    PointF[] points = new PointF[points_rel.Length];
                    for (int i = 0; i < points_rel.Length; ++i)
                        points[i] = new PointF(points_rel[i].X + translation, points_rel[i].Y);
                    drawer.graphics.FillPolygon(drawer.brush, points);
                }
            }

            public bool Good() { return points_rel != null; }

            public PointF[] points_rel { get; protected set; }
            public float[] xs_orig { get; protected set; }

            public float minf { get; protected set; }
            public float maxf { get; protected set; }
            public float periodf { get; protected set; }
            public float box_minf { get; protected set; }
            public float box_maxf { get; protected set; }
        }

        public class PeriodicDrawableBox : PeriodicDrawableRange
        {
            public PeriodicDrawableBox(LocalCS cs, Geometry.IRandomAccessRange<Geometry.Point> points, Geometry.Box box, Geometry.Unit unit)
            {
                double pi = Geometry.HalfAngle(unit);
                periodf = cs.ConvertDimension(2 * pi);

                xs_orig = new float[points.Count];
                points_rel = new PointF[points.Count];

                xs_orig[0] = cs.ConvertX(points[0][0]);
                points_rel[0] = cs.Convert(points[0]);

                minf = points_rel[0].X;
                maxf = points_rel[0].X;

                double x0 = Geometry.NormalizedAngle(points[0][0], unit);
                for (int i = 1; i < points.Count; ++i)
                {
                    xs_orig[i] = cs.ConvertX(points[i][0]);

                    double x1 = Geometry.NormalizedAngle(points[i][0], unit);
                    double dist = x1 - x0; // [-2pi, 2pi]
                    double distNorm = Geometry.NormalizedAngle(dist, unit); // [-pi, pi]
                    while (distNorm < 0)
                        distNorm += 2 * Geometry.HalfAngle(unit); // [0, 2pi] - min is always lesser than max

                    double x0_curr = points[0][0] + distNorm; // always relative to p0
                    points_rel[i] = new PointF(cs.ConvertX(x0_curr),
                                               cs.ConvertY(points[i][1]));

                    // expand relative box X
                    if (points_rel[i].X < minf)
                        minf = points_rel[i].X;
                    if (points_rel[i].X > maxf)
                        maxf = points_rel[i].X;
                }

                box_minf = cs.ConvertX(box.Min[0]);
                box_maxf = cs.ConvertX(box.Max[0]);
            }
        }

        public class PeriodicDrawableNSphere : IPeriodicDrawable
        {
            public PeriodicDrawableNSphere(LocalCS cs, Geometry.NSphere nsphere, Geometry.Box box, Geometry.Unit unit)
            {
                double pi = Geometry.HalfAngle(unit);
                periodf = cs.ConvertDimension(2 * pi);

                c_rel = cs.Convert(nsphere.Center);
                r = cs.ConvertDimension(nsphere.Radius);

                minf = c_rel.X - r;
                maxf = c_rel.X + r;

                box_minf = cs.ConvertX(box.Min[0]);
                box_maxf = cs.ConvertX(box.Max[0]);
            }

            public void DrawOne(Drawer drawer, float translation, bool closed, bool fill, bool drawDirs, bool drawDots)
            {
                if (!Good())
                    return;

                float cx = c_rel.X - r + translation;
                float cy = c_rel.Y - r;
                float d = r * 2;

                if (!drawDots || Math.Abs(translation) < 0.001)
                {
                    drawer.graphics.DrawEllipse(drawer.pen, cx, cy, d, d);
                }
                else
                {
                    Pen pend = (Pen)drawer.pen.Clone();
                    pend.DashStyle = DashStyle.Dot;
                    drawer.graphics.DrawEllipse(pend, cx, cy, d, d);
                }

                if (fill)
                {
                    drawer.graphics.FillEllipse(drawer.brush, cx, cy, d, d);
                }
            }

            public bool Good() { return r >= 0; }

            protected PointF c_rel { get; set; }
            protected float r { get; set; }

            public float minf { get; protected set; }
            public float maxf { get; protected set; }
            public float periodf { get; protected set; }
            public float box_minf { get; protected set; }
            public float box_maxf { get; protected set; }
        }

        public class PeriodicDrawablePolygon : IPeriodicDrawable
        {
            public PeriodicDrawablePolygon(LocalCS cs,
                                           Geometry.IRandomAccessRange<Geometry.Point> outer,
                                           IEnumerable<Geometry.IRandomAccessRange<Geometry.Point>> inners,
                                           Geometry.Box box,
                                           Geometry.Unit unit)
            {
                this.outer = new PeriodicDrawableRange(cs, outer, box, unit);

                minf = this.outer.minf;
                maxf = this.outer.maxf;
                periodf = this.outer.periodf;
                box_minf = this.outer.box_minf;
                box_maxf = this.outer.box_maxf;

                this.inners = new List<PeriodicDrawableRange>();
                int i = 0;
                foreach (var inner in inners)
                {
                    this.inners.Add(new PeriodicDrawableRange(cs, inner, box, unit));

                    // expand relative box X
                    if (this.inners[i].minf < minf)
                        minf = this.inners[i].minf;
                    if (this.inners[i].maxf > maxf)
                        maxf = this.inners[i].maxf;
                }
            }

            public void DrawOne(Drawer drawer, float translation, bool closed, bool fill, bool drawDirs, bool drawDots)
            {
                if (!outer.Good())
                    return;

                outer.DrawOne(drawer, translation, closed, false, drawDirs, drawDots);

                GraphicsPath gp = new GraphicsPath();
                if (fill && outer.points_rel != null)
                {
                    PointF[] points = new PointF[outer.points_rel.Length];
                    for (int i = 0; i < outer.points_rel.Length; ++i)
                        points[i] = new PointF(outer.points_rel[i].X + translation, outer.points_rel[i].Y);
                    gp.AddPolygon(points);
                }

                foreach (var inner in inners)
                {
                    if (inner.Good())
                    {
                        inner.DrawOne(drawer, translation, closed, false, drawDirs, drawDots);

                        if (fill && inner.points_rel != null)
                        {
                            PointF[] points = new PointF[inner.points_rel.Length];
                            for (int i = 0; i < inner.points_rel.Length; ++i)
                                points[i] = new PointF(inner.points_rel[i].X + translation, inner.points_rel[i].Y);
                            gp.AddPolygon(points);
                        }
                    }
                }

                if (fill)
                    drawer.graphics.FillPath(drawer.brush, gp);
            }

            public bool Good() { return outer.Good(); }

            private PeriodicDrawableRange outer;
            private List<PeriodicDrawableRange> inners;

            public float minf { get; }
            public float maxf { get; }
            public float periodf { get; }
            public float box_minf { get; }
            public float box_maxf { get; }
        }

        public void DrawPeriodic(IPeriodicDrawable pd, bool closed, bool fill, bool drawDirs, bool drawDots)
        {
            if (!pd.Good())
                return;

            if (pd.maxf >= pd.box_minf && pd.minf <= pd.box_maxf)
                pd.DrawOne(this, 0, closed, fill, drawDirs, drawDots);

            // west
            float minf_i = pd.minf;
            float maxf_i = pd.maxf;
            float translationf = 0;
            while (maxf_i >= pd.box_minf)
            {
                translationf -= pd.periodf;
                minf_i -= pd.periodf;
                maxf_i -= pd.periodf;
                if (maxf_i >= pd.box_minf && minf_i <= pd.box_maxf)
                    pd.DrawOne(this, translationf, closed, fill, drawDirs, drawDots);
            }
            // east
            minf_i = pd.minf;
            maxf_i = pd.maxf;
            translationf = 0;
            while (minf_i <= pd.box_maxf)
            {
                translationf += pd.periodf;
                minf_i += pd.periodf;
                maxf_i += pd.periodf;
                if (maxf_i >= pd.box_minf && minf_i <= pd.box_maxf)
                    pd.DrawOne(this, translationf, closed, fill, drawDirs, drawDots);
            }
        }

        public static void DrawMessage(Graphics graphics, string message, Color color)
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

        public static bool DrawAabb(Graphics graphics, Geometry.Box box, Geometry.Traits traits, Colors colors)
        {
            if (!box.IsValid())
                return false;

            LocalCS cs = new LocalCS(box, graphics);

            // Axes
            float h = graphics.VisibleClipBounds.Height;
            float w = graphics.VisibleClipBounds.Width;
            Pen prime_pen = new Pen(colors.AxisColor, 1);
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
                Pen anti_pen = new Pen(colors.AxisColor, 1);
                anti_pen.DashStyle = DashStyle.Custom;
                anti_pen.DashPattern = new float[] { 5, 5 };
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

            // Aabb            
            float min_x = cs.ConvertX(box.Min[0]);
            float min_y = cs.ConvertY(box.Min[1]);
            float max_x = cs.ConvertX(box.Max[0]);
            float max_y = cs.ConvertY(box.Max[1]);

            Pen penAabb = new Pen(colors.AabbColor, 1);
            graphics.DrawLine(penAabb, min_x - 1, min_y, min_x + 5, min_y);
            graphics.DrawLine(penAabb, min_x, min_y - 5, min_x, min_y + 1);
            graphics.DrawLine(penAabb, max_x - 5, max_y, max_x + 1, max_y);
            graphics.DrawLine(penAabb, max_x, max_y - 1, max_x, max_y + 5);

            // Aabb's coordinates
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
                SolidBrush brushText = new SolidBrush(colors.TextColor);
                graphics.DrawString(minStr, font, brushText, drawRectMin, drawFormat);
                graphics.DrawString(maxStr, font, brushText, drawRectMax, drawFormat);
            }

            return true;
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
    }

    class LocalCS
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
            if (src_w < 0 || src_h < 0)
                throw new System.Exception("Invalid box dimensions.");

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
}
