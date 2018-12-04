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

        private void DrawPoint(float x, float y, float d, Pen pen)
        {
            float r = d / 2.0f;
            float mx = x - r;
            float my = y - r;
            graphics.DrawEllipse(pen, mx, my, d, d);
            graphics.FillEllipse(brush, mx, my, d, d);
        }
        private void DrawPoint(PointF p, Pen pen) { DrawPoint(p.X, p.Y, 5, pen); }

        public void DrawPoint(float x, float y) { DrawPoint(x, y, 5, pen); }
        public void DrawPoint(float x, float y, float size) { DrawPoint(x, y, size, pen); }
        public void DrawPoint(PointF p) { DrawPoint(p.X, p.Y, 5, pen); }

        public static void DrawLine(Graphics graphics, Pen pen, float x1, float y1, float x2, float y2)
        {
            if (Math.Abs(x1 - x2) < 1 && Math.Abs(y1 - y2) < 1)
            {
                float x = (x1 + x2) / 2 - 0.5f;
                float y = (y1 + y2) / 2 - 0.5f;
                graphics.DrawRectangle(pen, x, y, 1, 1);
            }
            else
                graphics.DrawLine(pen, x1, y1, x2, y2);
        }

        public void DrawLine(Pen pen, float x1, float y1, float x2, float y2)
        {
            DrawLine(graphics, pen, x1, y1, x2, y2);
        }

        public void DrawLine(float x1, float y1, float x2, float y2)
        {
            DrawLine(graphics, pen, x1, y1, x2, y2);
        }

        public void DrawLine(Pen pen, PointF p0, PointF p1)
        {
            DrawLine(graphics, pen, p0.X, p0.Y, p1.X, p1.Y);
        }

        public void DrawLine(PointF p0, PointF p1)
        {
            DrawLine(graphics, pen, p0.X, p0.Y, p1.X, p1.Y);
        }

        public void DrawLines(PointF[] points)
        {
            for (int i = 0; i < points.Length - 1; ++i)
                DrawLine(points[i], points[i + 1]);
        }

        public void DrawLine(PointF p0, PointF p1, bool drawDir)
        {
            DrawLine(p0, p1);
            if (drawDir)
                DrawDir(p0, p1);
        }

        public void DrawLines(PointF[] points, bool closed, bool drawDir)
        {
            DrawLines(points);
            if (closed && points.Length > 1)
                DrawLine(points[points.Length - 1], points[0]);
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
                    DrawLine(sameP0 ? pen : pend, p0, ph);
                    DrawLine(sameP1 ? pen : pend, ph, p1);
                }
                else
                {
                    DrawLine(pend, p0, p1);
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

        public abstract class IPeriodicDrawable
        {
            abstract public void DrawOne(Drawer drawer, float translation, bool fill, bool drawDirs, bool drawDots);
            abstract public bool Good();

            public float MinF { get { return minf; } }
            public float MaxF { get { return maxf; } }

            protected void AssignMinMaxF(PointF[] points)
            {
                if (points.Length == 0)
                    return;

                minf = points[0].X;
                maxf = points[0].X;

                for (int i = 1; i < points.Length; ++i)
                {
                    minf = Math.Min(minf, points[i].X);
                    maxf = Math.Max(maxf, points[i].X);
                }
            }

            protected float minf;
            protected float maxf;
        }

        public class PeriodicDrawableRange : IPeriodicDrawable
        {
            protected PeriodicDrawableRange(bool closed)
            {
                this.closed = closed;
            }

            public PeriodicDrawableRange(LocalCS cs,
                                         Geometry.IRandomAccessRange<Geometry.Point> points,
                                         bool closed,
                                         Geometry.Unit unit)
            {
                this.closed = closed;

                if (points.Count < 2)
                    return;

                xs_orig = new float[points.Count];
                points_rel = new PointF[points.Count];

                xs_orig[0] = cs.ConvertX(points[0][0]);
                points_rel[0] = cs.Convert(points[0]);

                double x_prev = points[0][0];
                for (int i = 1; i < points.Count; ++i)
                {
                    xs_orig[i] = cs.ConvertX(points[i][0]);

                    double distNorm = Geometry.NormalizedAngleSigned(points[i][0] - points[i - 1][0], unit); // [-pi, pi]
                    double x_curr = x_prev + distNorm;
                    points_rel[i] = new PointF(cs.ConvertX(x_curr),
                                               cs.ConvertY(points[i][1]));

                    x_prev = x_curr;
                }

                // calculate relative box X
                AssignMinMaxF(points_rel);
            }

            /*private static PointF[] DensifyAndConvert(LocalCS cs, Geometry.Point p0, Geometry.Point p1, Geometry.Unit unit)
            {
                Geometry.Point[] densPts = Geometry.SphericalDensify(p0, p1, unit);
                PointF[] result = new PointF[densPts.Length];
                for (int i = 0; i < densPts.Length; ++i)
                {
                    result[i] = new PointF(cs.ConvertX(densPts[i][0]),
                                           cs.ConvertY(densPts[i][1]));
                }
                return result;
            }*/

            public override void DrawOne(Drawer drawer, float translation, bool fill, bool drawDirs, bool drawDots)
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

            public override bool Good() { return points_rel != null; }

            public PointF[] points_rel { get; protected set; }
            public float[] xs_orig { get; protected set; }

            protected bool closed;
        }

        public class PeriodicDrawableBox : PeriodicDrawableRange
        {
            public PeriodicDrawableBox(LocalCS cs,
                                       Geometry.IRandomAccessRange<Geometry.Point> points,
                                       Geometry.Unit unit)
                : base(true)
            {
                xs_orig = new float[points.Count];
                points_rel = new PointF[points.Count];

                xs_orig[0] = cs.ConvertX(points[0][0]);
                points_rel[0] = cs.Convert(points[0]);
                
                for (int i = 1; i < points.Count; ++i)
                {
                    xs_orig[i] = cs.ConvertX(points[i][0]);

                    // always relative to p0
                    double distNorm = Geometry.NormalizedAngleUnsigned(points[i][0] - points[0][0], unit); // [0, 2pi] - min is always lesser than max

                    double x_curr = points[0][0] + distNorm; // always relative to p0
                    points_rel[i] = new PointF(cs.ConvertX(x_curr),
                                               cs.ConvertY(points[i][1]));
                }

                // calculate relative box X
                AssignMinMaxF(points_rel);
            }
        }

        public class PeriodicDrawableNSphere : IPeriodicDrawable
        {
            public PeriodicDrawableNSphere(LocalCS cs, Geometry.NSphere nsphere, Geometry.Unit unit)
            {
                c_rel = cs.Convert(nsphere.Center);
                r = cs.ConvertDimensionX(nsphere.Radius);

                // NOTE: The radius is always in the units of the CS which is technically wrong

                minf = c_rel.X - r;
                maxf = c_rel.X + r;
            }

            public override void DrawOne(Drawer drawer, float translation, bool fill, bool drawDirs, bool drawDots)
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

            public override bool Good() { return r >= 0; }

            protected PointF c_rel { get; set; }
            protected float r { get; set; }
        }

        public class PeriodicDrawablePolygon : IPeriodicDrawable
        {
            public PeriodicDrawablePolygon(LocalCS cs,
                                           Geometry.IRandomAccessRange<Geometry.Point> outer,
                                           IEnumerable<Geometry.IRandomAccessRange<Geometry.Point>> inners,
                                           Geometry.Unit unit)
            {
                this.outer = new PeriodicDrawableRange(cs, outer, true, unit);

                minf = this.outer.MinF;
                maxf = this.outer.MaxF;

                this.inners = new List<PeriodicDrawableRange>();
                int i = 0;
                foreach (var inner in inners)
                {
                    this.inners.Add(new PeriodicDrawableRange(cs, inner, true, unit));

                    // expand relative box X
                    minf = Math.Min(minf, this.inners[i].MinF);
                    maxf = Math.Max(maxf, this.inners[i].MaxF);
                }
            }

            public override void DrawOne(Drawer drawer, float translation, bool fill, bool drawDirs, bool drawDots)
            {
                if (!outer.Good())
                    return;

                outer.DrawOne(drawer, translation, false, drawDirs, drawDots);

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
                        inner.DrawOne(drawer, translation, false, drawDirs, drawDots);

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

            public override bool Good() { return outer.Good(); }

            private PeriodicDrawableRange outer;
            private List<PeriodicDrawableRange> inners;
        }

        public void DrawPeriodic(LocalCS cs,
                                 Geometry.Box box, Geometry.Unit unit,
                                 IPeriodicDrawable pd,
                                 bool fill, bool drawDirs, bool drawDots)
        {
            if (!pd.Good())
                return;

            double pi = Geometry.HalfAngle(unit);
            float periodf = cs.ConvertDimensionX(2 * pi);
            float box_minf = cs.ConvertX(box.Min[0]);
            float box_maxf = cs.ConvertX(box.Max[0]);

            if (pd.MaxF >= box_minf && pd.MinF <= box_maxf)
                pd.DrawOne(this, 0, fill, drawDirs, drawDots);

            // west
            float minf_i = pd.MinF;
            float maxf_i = pd.MaxF;
            float translationf = 0;
            while (maxf_i >= box_minf)
            {
                translationf -= periodf;
                minf_i -= periodf;
                maxf_i -= periodf;
                if (maxf_i >= box_minf && minf_i <= box_maxf)
                    pd.DrawOne(this, translationf, fill, drawDirs, drawDots);
            }
            // east
            minf_i = pd.MinF;
            maxf_i = pd.MaxF;
            translationf = 0;
            while (minf_i <= box_maxf)
            {
                translationf += periodf;
                minf_i += periodf;
                maxf_i += periodf;
                if (maxf_i >= box_minf && minf_i <= box_maxf)
                    pd.DrawOne(this, translationf, fill, drawDirs, drawDots);
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

        public static bool DrawAxes(Graphics graphics, Geometry.Box box, Geometry.Unit unit, Colors colors, bool fill)
        {
            if (!box.IsValid())
                return false;

            LocalCS cs = new LocalCS(box, graphics, fill);
            //Geometry.Box viewBox = cs.ViewBox();

            // Axes
            float h = graphics.VisibleClipBounds.Height;
            float w = graphics.VisibleClipBounds.Width;
            Pen prime_pen = new Pen(colors.AxisColor, 1);
            if (unit == Geometry.Unit.None)
            {
                // Y axis
                //if (Geometry.IntersectsX(viewBox, 0.0))
                {
                    float x0 = cs.ConvertX(0.0);
                    graphics.DrawLine(prime_pen, x0, 0, x0, h);
                }
                // X axis
                //if (Geometry.IntersectsY(viewBox, 0.0))
                {
                    float y0 = cs.ConvertY(0.0);
                    graphics.DrawLine(prime_pen, 0, y0, w, y0);
                }
            }
            else
            {
                Pen anti_pen = new Pen(colors.AxisColor, 1);
                anti_pen.DashStyle = DashStyle.Custom;
                anti_pen.DashPattern = new float[] { 5, 5 };
                double pi = Geometry.HalfAngle(unit);
                double anti_mer = Geometry.NearestAntimeridian(box.Min[0], -1, unit);
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
                    if (anti_mer_f >= 0)
                    {
                        graphics.DrawLine(anti_pen, anti_mer_f, 0, anti_mer_f, h);
                    }
                }
                // Prime meridians
                for (; prime_mer_f <= w; prime_mer_f += prime_mer_step)
                {
                    if (prime_mer_f >= 0)
                    {
                        graphics.DrawLine(prime_pen, prime_mer_f, 0, prime_mer_f, h);
                    }
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

            return true;
        }

        public static bool DrawScales(Graphics graphics, Geometry.Box box, Colors colors, bool fill)
        {
            if (!box.IsValid())
                return false;

            LocalCS cs = new LocalCS(box, graphics, fill);
            
            // Aabb            
            float min_x = cs.ConvertX(box.Min[0]);
            float min_y = cs.ConvertY(box.Min[1]);
            float max_x = cs.ConvertX(box.Max[0]);
            float max_y = cs.ConvertY(box.Max[1]);

            // pen for lines
            Pen penAabb = new Pen(colors.AabbColor, 1);
            float maxHeight = 20.0f;
            // font and brush for text
            Font font = new Font(new FontFamily(System.Drawing.Text.GenericFontFamilies.SansSerif), maxHeight / 2.0f);
            SolidBrush brushText = new SolidBrush(colors.TextColor);

            // Scales
            {
                float wWidth = graphics.VisibleClipBounds.Width;
                float wHeight = graphics.VisibleClipBounds.Height;
                // In CS coordinates
                double mi_x = cs.InverseConvertX(0);
                double mi_y = cs.InverseConvertY(wHeight);
                double ma_x = cs.InverseConvertX(wWidth);
                double ma_y = cs.InverseConvertY(0);
                double mima_x = ma_x - mi_x;
                double mima_y = ma_y - mi_y;
                // Esstimate numbers of strings for both axes
                double esst_x = Math.Abs(mima_x) < 10 ? mima_x / 10 : mima_x;
                float wStrNumX = wWidth / StringWidth(graphics, font, esst_x) / 1.25f;
                float wStrNumH = wHeight / StringWidth(graphics, font, 1.0) / 2.0f;
                // Find closest power of 10 lesser than the width and height
                double pd_x = AbsOuterPow10(mima_x / wStrNumX);
                double pd_y = AbsOuterPow10(mima_y / wStrNumH);
                // Find starting x and y values being the first lesser whole
                // values of the same magnitude, per axis
                double x = ScaleStart(mi_x, pd_x);
                double y = ScaleStart(mi_y, pd_y);
                // Make sure the scale starts outside the view
                if (x > mi_x)
                    x -= pd_x;
                if (y > mi_y)
                    y -= pd_y;
                // Create the string output pattern, e.g. 0.00 for previously calculated step
                string xStrFormat = StringFormat(pd_x);
                string yStrFormat = StringFormat(pd_y);
                float wd_x = cs.ConvertDimensionX(pd_x);
                int smallScaleX = SmallScaleSegments(wd_x, 10);
                float wd_x_step = wd_x / smallScaleX;
                float wd_x_limit = wd_x - wd_x_step / 2;
                float wd_y = cs.ConvertDimensionY(pd_y);
                int smallScaleY = SmallScaleSegments(wd_y, 10);
                float wd_y_step = wd_y / smallScaleY;
                float wd_y_limit = wd_y - wd_y_step / 2;
                // Draw horizontal scale
                double limit_x = ma_x + pd_x * 1.001;
                for (; x < limit_x; x += pd_x)
                {
                    float wx = cs.ConvertX(x);
                    // scale
                    graphics.DrawLine(penAabb, wx, wHeight, wx, wHeight - 5);
                    // value
                    string xStr = Util.ToString(x, xStrFormat);
                    SizeF xStrSize = graphics.MeasureString(xStr, font);
                    float xStrLeft = wx - xStrSize.Width / 2;
                    float xStrTop = wHeight - 5 - xStrSize.Height;
                    graphics.DrawString(xStr, font, brushText, xStrLeft, xStrTop);
                    // small scale
                    for (float wsx = wx + wd_x_step; wsx < wx + wd_x_limit; wsx += wd_x_step)
                        graphics.DrawLine(penAabb, wsx, wHeight, wsx, wHeight - 3);
                }
                // Draw vertical scale
                double limit_y = ma_y + pd_y * 1.001;
                for (; y < limit_y; y += pd_y)
                {
                    float wy = cs.ConvertY(y);
                    // scale
                    graphics.DrawLine(penAabb, wWidth, wy, wWidth - 5, wy);
                    // value
                    string yStr = Util.ToString(y, yStrFormat);
                    SizeF yStrSize = graphics.MeasureString(yStr, font);
                    float yStrLeft = wWidth - 5 - yStrSize.Width;
                    float yStrTop = wy - yStrSize.Height / 2;
                    graphics.DrawString(yStr, font, brushText, yStrLeft, yStrTop);
                    // small scale
                    for (float wsy = wy - wd_y_step; wsy > wy - wd_y_limit; wsy -= wd_y_step)
                        graphics.DrawLine(penAabb, wWidth, wsy, wWidth - 3, wsy);
                }
            }

            return true;
        }
        /*
        public static bool DrawAabb(Graphics graphics, Geometry.Box box, Geometry.Unit unit, Colors colors, bool fill)
        {
            if (!box.IsValid())
                return false;

            LocalCS cs = new LocalCS(box, graphics, fill);
            //Geometry.Box viewBox = cs.ViewBox();

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
                string min_x_str = Util.ToString(box.Min[0], "0.00");
                string min_y_str = Util.ToString(box.Min[1], "0.00");
                string max_x_str = Util.ToString(box.Max[0], "0.00");
                string max_y_str = Util.ToString(box.Max[1], "0.00");
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
        */

        private static float StringWidth(Graphics graphics, Font font, double value)
        {
            return StringSize(graphics, font, value).Width;
        }

        private static float MaxStringHeight(Graphics graphics, Font font, double value)
        {
            return StringSize(graphics, font, value).Height;
        }

        private static SizeF StringSize(Graphics graphics, Font font, double value)
        {
            string format = StringFormat(value);
            string str = Util.ToString(value, format);
            return graphics.MeasureString(str, font);
        }

        private static string StringFormat(double p)
        {
            double n = Math.Floor(Math.Log10(p));
            string result = "0";
            if (n < 0.0)
            {
                int ni = (int)Math.Max(n, -16.0);
                result += '.';
                for (int i = -1; i >= ni; --i)
                    result += '0';
            }
            return result;
        }

        private static double AbsOuterPow10(double x)
        {
            return Math.Pow(10, Math.Ceiling(Math.Log10(Math.Abs(x))));
        }

        private static double AbsInnerPow10(double x)
        {
            return Math.Pow(10, Math.Floor(Math.Log10(Math.Abs(x))));
        }

        private static double ScaleStart(double val, double step)
        {
            double r = val / step;
            double i = val >= 0 ? Math.Ceiling(r) : Math.Floor(r);
            return i * step;
        }

        private static int SmallScaleSegments(float wStep, float wMinSize)
        {
            return wStep >= wMinSize * 10 ? 10
                 : wStep >= wMinSize * 5 ? 5
                 : wStep >= wMinSize * 2 ? 2
                 : 1;
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

    // Zoom box representation in relative window coordinates
    // Coordinate system origin in bottom left corner
    class ZoomBox : Geometry.Box
    {
        public ZoomBox()
            : base(new Geometry.Point(0, 0), new Geometry.Point(1, 1))
        { }

        // Takes native window coordinates, origin in top left corner
        public void Zoom(double left, double top, double width, double height,
                         double imageWidth, double imageHeight)
        {
            double xMin = left;
            double yMin = imageHeight - top - height;
            double xMax = left + width;
            double yMax = imageHeight - top;

            double xMinRel = xMin / imageWidth;
            double yMinRel = yMin / imageHeight;
            double xMaxRel = xMax / imageWidth;
            double yMaxRel = yMax / imageHeight;

            double x = Min[0];
            double y = Min[1];
            double w = Dim(0);
            double h = Dim(1);

            Min[0] = x + xMinRel * w;
            Min[1] = y + yMinRel * h;
            Max[0] = x + xMaxRel * w;
            Max[1] = y + yMaxRel * h;
        }

        public bool IsZoomed()
        {
            return Min[0] != 0 || Min[1] != 0
                || Max[0] != 1 || Max[1] != 1;
        }

        public void Reset()
        {
            Min[0] = 0; Min[1] = 0;
            Max[0] = 1; Max[1] = 1;
        }
    }

    class LocalCS
    {
        private static float viewScale = 0.9f;
        private static float viewFix = (1.0f - viewScale) / 2.0f;

        public LocalCS(Geometry.Box src_box, Graphics dst_graphics)
            : this(src_box, dst_graphics.VisibleClipBounds.Width, dst_graphics.VisibleClipBounds.Height)
        { }

        public LocalCS(Geometry.Box src_box, Graphics dst_graphics, bool fill)
            : this(src_box, dst_graphics.VisibleClipBounds.Width, dst_graphics.VisibleClipBounds.Height, fill)
        { }

        public LocalCS(Geometry.Box src_box, float viewWidth, float viewHeight)
        {
            Reset(src_box, viewWidth, viewHeight, false);
        }

        public LocalCS(Geometry.Box src_box, float viewWidth, float viewHeight, bool fill)
        {
            Reset(src_box, viewWidth, viewHeight, fill);
        }

        public void Reset(Geometry.Box src_box, float viewWidth, float viewHeight)
        {
            Reset(src_box, viewWidth, viewHeight, false);
        }

        public void Reset(Geometry.Box src_box, float viewWidth, float viewHeight, bool fill)
        {
            float w = viewWidth;
            float h = viewHeight;
            dst_orig_w = w;
            dst_orig_h = h;
            dst_x0 = w / 2;
            dst_y0 = h / 2;
            float dst_w = w * viewScale;
            float dst_h = h * viewScale;

            double src_w = src_box.Dim(0);
            double src_h = src_box.Dim(1);
            if (src_w < 0 || src_h < 0)
                throw new System.Exception("Invalid box dimensions.");

            src_x0 = src_box.Min[0] + src_w / 2;
            src_y0 = src_box.Min[1] + src_h / 2;

            // point or 1 value
            if (src_w == 0 && src_h == 0)
            {
                scale_x = dst_w / 2;
                scale_y = dst_h / 2;
            }
            // vertical segment or N 1-value plots
            else if (src_w == 0)
            {
                scale_x = dst_w / 2;
                scale_y = dst_h / src_h;
            }
            // horizontal segment or equal values
            else if (src_h == 0)
            {
                scale_x = dst_w / src_w;
                scale_y = dst_h / 2;
            }
            else
            {
                scale_x = dst_w / src_w;
                scale_y = dst_h / src_h;
                if (!fill)
                    scale_x = scale_y = Math.Min(scale_x, scale_y);
            }
        }

        public float ConvertX(double src)
        {
            return dst_x0 + (float)((src - src_x0) * scale_x);
        }

        public float ConvertY(double src)
        {
            return dst_y0 - (float)((src - src_y0) * scale_y);
        }

        public double InverseConvertX(double dst)
        {
            return src_x0 + (dst - dst_x0) / scale_x;
        }

        public double InverseConvertY(double dst)
        {
            return src_y0 - (dst - dst_y0) / scale_y;
        }

        public float ConvertDimensionX(double src)
        {
            return (float)(src * scale_x);
        }

        public float ConvertDimensionY(double src)
        {
            return (float)(src * scale_y);
        }

        public PointF Convert(Geometry.Point p)
        {
            return new PointF(ConvertX(p[0]), ConvertY(p[1]));
        }

        public PointF[] Convert(Geometry.IRandomAccessRange<Geometry.Point> points)
        {
            if (points.Count <= 0)
                return null;

            int dst_count = points.Count + (points[0] == points[points.Count - 1] ? 0 : 1);

            PointF[] dst_points = new PointF[dst_count];
            int i = 0;
            for (; i < points.Count; ++i)
            {
                dst_points[i] = Convert(points[i]);
            }
            if (i < dst_count)
            {
                dst_points[i] = Convert(points[0]);
            }

            return dst_points;
        }

        public Geometry.Box BoxFromZoomBox(ZoomBox zoomBox)
        {
            // fragment of the window in window coordinates
            double w = dst_orig_w;
            double h = dst_orig_h;
            double zw = zoomBox.Dim(0);
            double zh = zoomBox.Dim(1);
            double zmin_x = (zoomBox.Min[0] + zw * viewFix) * w;
            double zmin_y = h - (zoomBox.Min[1] + zh * viewFix) * h;
            double zmax_x = (zoomBox.Max[0] - zw * viewFix) * w;
            double zmax_y = h - (zoomBox.Max[1] - zw * viewFix) * h;
            // convert to box coordinates
            double min_x = InverseConvertX(zmin_x);
            double min_y = InverseConvertY(zmin_y);
            double max_x = InverseConvertX(zmax_x);
            double max_y = InverseConvertY(zmax_y);
            return new Geometry.Box(new Geometry.Point(min_x, min_y),
                                    new Geometry.Point(max_x, max_y));
        }

        public Geometry.Box ViewBox()
        {
            return new Geometry.Box(
                        new Geometry.Point(InverseConvertX(0), InverseConvertY(dst_orig_h)),
                        new Geometry.Point(InverseConvertX(dst_orig_w), InverseConvertY(0)));
        }

        float dst_orig_w, dst_orig_h;
        float dst_x0, dst_y0;
        double src_x0, src_y0;
        double scale_x, scale_y;
    }
}
