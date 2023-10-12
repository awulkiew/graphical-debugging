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
        private Pen penDot;
        private SolidBrush brush;

        public Drawer(Graphics graphics, Color color)
        {
            this.graphics = graphics;

            pen = new Pen(Color.FromArgb(112, color), 2);
            pen.LineJoin = LineJoin.Round;
            pen.EndCap = LineCap.Round;

            penDot = pen.Clone() as Pen;
            penDot.DashStyle = DashStyle.Dot;

            brush = new SolidBrush(Color.FromArgb(64, color));
        }

        public enum DirPos { Begin, Middle, End };

        public bool DrawDir(PointF p0, PointF p1, PointF p0Ref, PointF p1Ref,
                            DirPos dirPos = DirPos.Middle,
                            bool ignoreShort = true)
        {
            if (ignoreShort)
            {
                PointF vRef = SubF(p1Ref, p0Ref);
                float distRefSqr = DotF(vRef, vRef);
                if (distRefSqr < 49.0f) // (1+5+1)^2
                    return false;
            }

            PointF v = SubF(p1, p0);
            PointF p = dirPos == DirPos.Middle ? AddF(p0, MulF(v, 0.5f)) :
                       dirPos == DirPos.End ?    AddF(p0, v) :
                                                 p0;
            float a = AngleF(v);
            PointF ps = AddF(p, RotF(new PointF(-1.25f, -2.5f), a));
            PointF pm = AddF(p, RotF(new PointF(1.25f, 0.0f), a));
            PointF pe = AddF(p, RotF(new PointF(-1.25f, 2.5f), a));
            graphics.DrawLine(pen, pm, ps);
            graphics.DrawLine(pen, pm, pe);

            return true;
        }

        public bool DrawDir(PointF p0, PointF p1,
                            DirPos dirPos = DirPos.Middle,
                            bool ignoreShort = true)
        {
            return DrawDir(p0, p1, p0, p1, dirPos, ignoreShort);
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

        public void DrawLines(PointF[] points, bool closed)
        {
            DrawLines(points);
            if (closed && points.Length > 1)
                DrawLine(points[points.Length - 1], points[0]);
        }

        public void DrawLine(PointF p0, PointF p1, bool dotP0, bool dotP1)
        {
            if (!dotP0 && !dotP1)
            {
                DrawLine(p0, p1);
            }
            else
            {
                if (dotP0 && dotP1)
                {
                    DrawLine(penDot, p0, p1);
                }
                else
                {
                    PointF ph = AddF(p0, DivF(SubF(p1, p0), 2));
                    DrawLine(dotP0 ? penDot : pen, p0, ph);
                    DrawLine(dotP1 ? penDot : pen, ph, p1);
                }
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

        public void DrawPeriodicPoint(LocalCS cs, Geometry.Point point, Geometry.Box box, Geometry.Unit unit, bool drawDots)
        {
            PointF p = cs.Convert(point);
            DrawPoint(p);

            double twoPi = Geometry.FullAngle(unit);
            Pen pen = drawDots ? this.penDot : this.pen;

            // NOTE: Use AssignChanged becasue for big coordinates subtracting/adding
            //   twoPi doesn't change the value of x_tmp which causes infinite loop

            float x = Math.Min(Math.Max(p.X, 0.0f), cs.Width);
            double nPeriodsWest = (point[0] - box.Min[0]) / twoPi;
            float pixelsWest = x;
            double nPeriodsEast = (box.Max[0] - point[0]) / twoPi;
            float pixelsEast = cs.Width - x;

            if (nPeriodsWest <= pixelsWest / 5)
            {
                // draw points on the west
                double x_tmp = point[0];
                while (Util.Assign(ref x_tmp, x_tmp - twoPi)
                    && x_tmp >= box.Min[0])
                {
                    p.X = cs.ConvertX(x_tmp);
                    DrawPoint(p, pen);
                }
            }

            if (nPeriodsEast <= pixelsEast / 5)
            {
                // draw points on the east
                double x_tmp = point[0];
                while (Util.Assign(ref x_tmp, x_tmp + twoPi)
                    && x_tmp <= box.Max[0])
                {
                    p.X = cs.ConvertX(x_tmp);
                    DrawPoint(p, pen);
                }
            }
        }

        public abstract class IPeriodicDrawable
        {
            abstract public void DrawOne(Drawer drawer, float translation, bool fill, bool drawDirs, bool drawDots);
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
                                         Geometry.Unit unit,
                                         bool densify)
            {
                this.closed = closed;
                this.containsPole = ContainsPole.No;

                if (points.Count < 2)
                    return;

                // approx. length of densified segments
                /*double densLength = Math.Min(cs.InverseConvertDimensionX(20),
                                             cs.InverseConvertDimensionY(20));*/
                double densLength = Geometry.FromDegree(5, unit);

                int count = points.Count + (closed ? 1 : 0);

                xs_orig = new float[count];
                points_rel = new PointF[count];
                if (densify)
                    dens_points_rel = new PointF[points.Count][];

                xs_orig[0] = cs.ConvertX(points[0][0]);
                points_rel[0] = cs.Convert(points[0]);

                Geometry.Point p0 = points[0].Clone();
                for (int i = 1; i < count; ++i)
                {
                    Geometry.Point p1 = points[i % points.Count].Clone();

                    xs_orig[i] = cs.ConvertX(p1[0]);

                    double distNorm = Geometry.NormalizedAngleSigned(p1[0] - p0[0], unit); // [-pi, pi]
                    p1[0] = p0[0] + distNorm;
                    points_rel[i] = cs.Convert(p1);

                    if (dens_points_rel != null)
                        dens_points_rel[i-1] = DensifyAndConvert(cs, p0, p1, densLength, unit);

                    p0 = p1;
                }

                if (closed && Math.Abs(points_rel[0].X - points_rel[points.Count].X) > 0.1)
                {
                    // Check which pole
                    double area = 0;
                    p0 = points[0].Clone();
                    for (int i = 1; i < count; ++i)
                    {
                        Geometry.Point p1 = points[i % points.Count].Clone();
                        double distNorm = Geometry.NormalizedAngleSigned(p1[0] - p0[0], unit); // [-pi, pi]
                        p1[0] = p0[0] + distNorm;
                        area += Geometry.SphericalTrapezoidArea(p0, p1, unit);
                        p0 = p1;
                    }

                    int areaSign = Math.Sign(area);
                    int dirSign = Math.Sign(points_rel[points.Count].X - points_rel[0].X);
                    this.containsPole = (areaSign * dirSign >= 0)
                                      ? ContainsPole.North
                                      : ContainsPole.South;
                }
            }

            public override void DrawOne(Drawer drawer, float translation_x, bool fill, bool drawDirs, bool drawDots)
            {
                // TODO: Draw invalid ranges differently
                if (!IsInitialized())
                    return;

                // NOTE: additional point is at the end of the range for closed geometries
                // it may be different than the first point if the geometry goes around a pole
                for (int i = 1; i < points_rel.Length; ++i)
                {
                    PointF p0 = Translated(points_rel[i - 1], translation_x);
                    PointF p1 = Translated(points_rel[i], translation_x);
                    bool sameP0 = true;
                    bool sameP1 = true;
                    if (drawDots)
                    {
                        sameP0 = Math.Abs(p0.X - xs_orig[i - 1]) < 0.001;
                        sameP1 = Math.Abs(p1.X - xs_orig[i]) < 0.001;
                    }

                    if ( dens_points_rel != null
                      && dens_points_rel[i - 1].Length > 0 )
                    {
                        int midJ = dens_points_rel[i - 1].Length / 2;
                        for (int j = 0; j < dens_points_rel[i - 1].Length + 1; ++j)
                        {
                            PointF dp0 = j == 0
                                       ? p0
                                       : Translated(dens_points_rel[i - 1][j - 1], translation_x);
                            PointF dp1 = j == dens_points_rel[i - 1].Length
                                       ? p1
                                       : Translated(dens_points_rel[i - 1][j], translation_x);
                            bool sameDP0 = j <= midJ ? sameP0 : sameP1;
                            bool sameDP1 = j >= midJ ? sameP1 : sameP0;

                            drawer.DrawLine(dp0, dp1, !sameDP0, !sameDP1);
                            if (drawDirs && j == midJ)
                                drawer.DrawDir(dp0, dp1, p0, p1);
                        }
                    }
                    else
                    {
                        drawer.DrawLine(p0, p1, !sameP0, !sameP1);
                        if (drawDirs)
                            drawer.DrawDir(p0, p1);
                    }
                }

                if (fill)
                {
                    PointF[] points = AreaPointsF(drawer, translation_x);
                    drawer.graphics.FillPolygon(drawer.brush, points);
                }
            }

            private static PointF Translated(PointF p, float translation_x)
            {
                return new PointF(p.X + translation_x, p.Y);
            }

            private static PointF[] DensifyAndConvert(LocalCS cs, Geometry.Point p0, Geometry.Point p1, double length, Geometry.Unit unit)
            {
                double distNorm = Geometry.NormalizedAngleSigned(p1[0] - p0[0], unit);
                bool intersPole = IsAntipodal(distNorm, unit);
                double halfPi = Geometry.RightAngle(unit);
                double poleLat = p1[1] - p0[1] >= 0 ? halfPi : -halfPi;
                int intersPoleIndex = -1;

                Geometry.Point[] densPoints = Geometry.SphericalDensify(p0, p1, length, unit);
                PointF[] result = new PointF[densPoints.Length + (intersPole ? 2 : 0)];
                int k = 0;
                for (int j = 0; j < densPoints.Length; ++j, ++k)
                {
                    double densDistNorm = Geometry.NormalizedAngleSigned(densPoints[j][0] - p0[0], unit);
                    densPoints[j][0] = p0[0] + densDistNorm;

                    if (intersPole
                        && intersPoleIndex == -1
                        && Math.Abs(densDistNorm) > halfPi)
                    {
                        intersPoleIndex = j;
                        Geometry.Point p = j == 0 ? p0 : densPoints[j - 1];
                        float poleF = cs.ConvertY(poleLat);
                        result[k++] = new PointF(cs.ConvertX(p[0]), poleF);
                        result[k++] = new PointF(cs.ConvertX(densPoints[j][0]), poleF);
                    }

                    result[k] = cs.Convert(densPoints[j]);
                }

                // last segment
                if (intersPole && intersPoleIndex == -1)
                {
                    int j = densPoints.Length;
                    intersPoleIndex = j;
                    float poleF = cs.ConvertY(poleLat);
                    result[j] = new PointF(cs.ConvertX(densPoints[j - 1][0]), poleF);
                    result[j + 1] = new PointF(cs.ConvertX(p1[0]), poleF);
                }

                return result;
            }

            public static bool IsAntipodal(double distNorm, Geometry.Unit unit)
            {
                double pi = Geometry.StraightAngle(unit);
                return Math.Abs(Math.Abs(distNorm) - pi) < double.Epsilon * pi;
            }

            // NOTE: This method assumes that the geometry is closed
            //   It's suitable only for areal geometries
            public PointF[] AreaPointsF(Drawer drawer, float translation_x)
            {
                int count = points_rel.Length;
                if (dens_points_rel != null)
                {
                    for (int j = 0; j < dens_points_rel.Length; ++j)
                        count += dens_points_rel[j].Length;
                }

                // Add 2 points in case the geometry contains a pole
                //   and area points has to be added later
                count += 2;

                // NOTE: additional point is at the end of the range for closed geometries
                // it may be different than the first point if the geometry goes around a pole

                PointF[] result = new PointF[count];
                int o = 0;
                for (int i = 0; i < points_rel.Length; ++i)
                {
                    result[o++] = Translated(points_rel[i], translation_x);
                    if ( dens_points_rel != null
                      && i < dens_points_rel.Length )
                    {
                        for (int j = 0; j < dens_points_rel[i].Length; ++j)
                        {
                            result[o++] = Translated(dens_points_rel[i][j], translation_x);
                        }
                    }
                }

                // Add 2 potentially dummy points by copying both endpoints
                result[o] = result[o-1];
                result[o+1] = result[0];

                // If the endpoints doesn't match the geometry goes around pole
                if (containsPole != ContainsPole.No)
                {
                    // expand it
                    if (containsPole == ContainsPole.South)
                    {
                        result[o].Y = drawer.graphics.VisibleClipBounds.Height;
                        result[o + 1].Y = drawer.graphics.VisibleClipBounds.Height;
                    }
                    else
                    {
                        result[o].Y = 0;
                        result[o + 1].Y = 0;
                    }
                }

                return result;
            }

            public bool IsInitialized()
            {
                return points_rel != null;
            }

            protected PointF[] points_rel;
            protected PointF[][] dens_points_rel;
            protected float[] xs_orig;
            protected bool closed;
            protected enum ContainsPole { No, North, South };
            ContainsPole containsPole;
        }

        public class PeriodicDrawableBox : PeriodicDrawableRange
        {
            public PeriodicDrawableBox(LocalCS cs,
                                       Geometry.IRandomAccessRange<Geometry.Point> points,
                                       Geometry.Unit unit)
                : base(true)
            {
                int count = points.Count + 1;
                xs_orig = new float[count];
                points_rel = new PointF[count];

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

                // close
                xs_orig[points.Count] = xs_orig[0];
                points_rel[points.Count] = points_rel[0];
            }
        }

        public class PeriodicDrawableNSphere : IPeriodicDrawable
        {
            public PeriodicDrawableNSphere(LocalCS cs, Geometry.NSphere nsphere, Geometry.Unit unit)
            {
                // NOTE: The radius is always in the units of the CS which is technically wrong
                c_rel = cs.Convert(nsphere.Center);
                r = cs.ConvertDimensionX(nsphere.Radius);
            }

            public override void DrawOne(Drawer drawer, float translation, bool fill, bool drawDirs, bool drawDots)
            {
                // TODO: Instead draw invalid nsphere in a different way
                if (r < 0)
                    return;

                float cx = c_rel.X - r + translation;
                float cy = c_rel.Y - r;
                // DrawEllipse throws 'Out of memory' exception for sizes around 0.05
                float d = Math.Max(r * 2, 1.0f);

                if (!drawDots || Math.Abs(translation) < 0.001)
                {
                    drawer.graphics.DrawEllipse(drawer.pen, cx, cy, d, d);
                }
                else
                {
                    drawer.graphics.DrawEllipse(drawer.penDot, cx, cy, d, d);
                }

                if (fill)
                {
                    drawer.graphics.FillEllipse(drawer.brush, cx, cy, d, d);
                }
                
            }

            protected PointF c_rel;
            protected float r;
        }

        public class PeriodicDrawablePolygon : IPeriodicDrawable
        {
            public PeriodicDrawablePolygon(LocalCS cs,
                                           Geometry.IRandomAccessRange<Geometry.Point> outer,
                                           IEnumerable<Geometry.IRandomAccessRange<Geometry.Point>> inners,
                                           Geometry.Unit unit,
                                           bool densify)
            {
                this.outer = new PeriodicDrawableRange(cs, outer, true, unit, densify);

                this.inners = new List<PeriodicDrawableRange>();
                foreach (var inner in inners)
                {
                    PeriodicDrawableRange pd = new PeriodicDrawableRange(cs, inner, true, unit, densify);
                    this.inners.Add(pd);
                }
            }

            public override void DrawOne(Drawer drawer, float translation, bool fill, bool drawDirs, bool drawDots)
            {
                // TODO: Draw invalid rings differently
                if (!outer.IsInitialized())
                    return;

                bool exteriorOnly = (inners.Count == 0);

                outer.DrawOne(drawer, translation, exteriorOnly, drawDirs, drawDots);

                if (exteriorOnly)
                    return;

                GraphicsPath gp = new GraphicsPath();
                if (fill && outer.IsInitialized())
                {
                    PointF[] points = outer.AreaPointsF(drawer, translation);
                    gp.AddPolygon(points);
                }

                foreach (var inner in inners)
                {
                    // TODO: Draw invalid rings differently
                    if (!inner.IsInitialized())
                        continue;

                    inner.DrawOne(drawer, translation, false, drawDirs, drawDots);

                    if (fill)
                    {
                        PointF[] points = inner.AreaPointsF(drawer, translation);
                        gp.AddPolygon(points);
                    }
                }

                if (fill)
                    drawer.graphics.FillPath(drawer.brush, gp);
            }

            private PeriodicDrawableRange outer;
            private List<PeriodicDrawableRange> inners;
        }

        public void DrawPeriodic(LocalCS cs,
                                 Geometry.Box box, Geometry.Interval interval, Geometry.Unit unit,
                                 IPeriodicDrawable drawer,
                                 bool fill, bool drawDirs, bool drawDots)
        {
            double twoPi = Geometry.FullAngle(unit);
            float periodf = cs.ConvertDimensionX(twoPi);
            float box_minf = cs.ConvertX(box.Min[0]);
            float box_maxf = cs.ConvertX(box.Max[0]);

            float minf = cs.ConvertX(interval.Min);
            float maxf = cs.ConvertX(interval.Max);

            if (maxf >= box_minf && minf <= box_maxf)
                drawer.DrawOne(this, 0, fill, drawDirs, drawDots);

            // west
            float minf_i = minf;
            float maxf_i = maxf;
            float translationf = 0;
            while (maxf_i >= box_minf
                && Util.Assign(ref maxf_i, maxf_i - periodf))
            {
                translationf -= periodf;
                minf_i -= periodf;
                //maxf_i -= periodf; // subtracted above
                if (maxf_i >= box_minf && minf_i <= box_maxf)
                    drawer.DrawOne(this, translationf, fill, drawDirs, drawDots);
            }
            // east
            minf_i = minf;
            maxf_i = maxf;
            translationf = 0;
            while (minf_i <= box_maxf
                && Util.Assign(ref minf_i, minf_i + periodf))
            {
                translationf += periodf;
                //minf_i += periodf; // added above
                maxf_i += periodf;
                if (maxf_i >= box_minf && minf_i <= box_maxf)
                    drawer.DrawOne(this, translationf, fill, drawDirs, drawDots);
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

            // NOTE: the coordinates limit of MS GDI+ is 1073741951 so below
            //   avoid passing such coordinates. But instead of checking this
            //   value or similar one check whether or not an axis is range
            //   of an image.

            // Axes
            float h = graphics.VisibleClipBounds.Height;
            float w = graphics.VisibleClipBounds.Width;
            Pen prime_pen = new Pen(colors.AxisColor, 1);
            if (unit == Geometry.Unit.None)
            {
                // Y axis
                float x0 = cs.ConvertX(0.0);
                if (0 <= x0 && x0 <= w)
                    graphics.DrawLine(prime_pen, x0, 0, x0, h);

                // X axis
                float y0 = cs.ConvertY(0.0);
                if (0 <= y0 && y0 <= h)
                    graphics.DrawLine(prime_pen, 0, y0, w, y0);
            }
            else
            {
                Pen anti_pen = new Pen(colors.AxisColor, 1);
                anti_pen.DashStyle = DashStyle.Custom;
                anti_pen.DashPattern = new float[] { 5, 5 };
                double pi = Geometry.StraightAngle(unit);
                double anti_mer = Geometry.NearestAntimeridian(box.Min[0], -1, unit);
                double prime_mer = anti_mer + pi;
                double next_anti_mer = anti_mer + 2 * pi;
                double next_prime_mer = prime_mer + 2 * pi;

                float anti_mer_f = cs.ConvertX(anti_mer);
                float anti_mer_step = cs.ConvertX(next_anti_mer) - anti_mer_f;
                float prime_mer_f = cs.ConvertX(prime_mer);
                float prime_mer_step = cs.ConvertX(next_prime_mer) - prime_mer_f;

                // Antimeridians
                while (anti_mer_f <= w
                    // NOTE: For bug coordinates anti_mer_step may be 0 which results in infinite loop
                    && Util.Assign(ref anti_mer_f, anti_mer_f + anti_mer_step))
                {
                    if (anti_mer_f >= 0 && anti_mer_f <= w)
                    {
                        graphics.DrawLine(anti_pen, anti_mer_f, 0, anti_mer_f, h);
                    }
                }
                // Prime meridians
                bool primeMeridiansDrawn = false;
                while (prime_mer_f <= w
                    // NOTE: For bug coordinates anti_mer_step may be 0 which results in infinite loop
                    && Util.Assign(ref prime_mer_f, prime_mer_f += prime_mer_step))
                {
                    if (prime_mer_f >= 0 && prime_mer_f <= w)
                    {
                        graphics.DrawLine(prime_pen, prime_mer_f, 0, prime_mer_f, h);
                        primeMeridiansDrawn = true;
                    }
                }
                // Prime meridian
                float p = cs.ConvertX(0.0);
                if (!primeMeridiansDrawn
                    && 0 <= p && p <= w)
                {
                    graphics.DrawLine(prime_pen, p, 0, p, h);
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
                // Find axes intervals
                IntervalI xInterval = ScaleStepsInterval(mi_x, ma_x, pd_x);
                IntervalI yInterval = ScaleStepsInterval(mi_y, ma_y, pd_y);
                // Draw horizontal scale
                for (int i = xInterval.Min; i <= xInterval.Max; ++i)
                {
                    double x = i * pd_x;
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
                for (int j = yInterval.Min; j <= yInterval.Max; ++j)
                {
                    double y = j * pd_y;
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
            if (n >= 16 || n <= -16)
            {
                result = "G16";
            }
            else if (n < 0.0)
            {
                int ni = (int)n;
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

        private struct IntervalI
        {
            public IntervalI(int min, int max)
            {
                Min = min;
                Max = max;
            }
            public int Min;
            public int Max;
        }

        private static IntervalI ScaleStepsInterval(double mi, double ma, double step)
        {
            double r = mi / step;
            int min = (int)(mi >= 0 ? Math.Ceiling(r) : Math.Floor(r));
            if (min * step > mi)
                --min;
            r = ma / step;
            int max = (int)(ma >= 0 ? Math.Floor(r) : Math.Ceiling(r));
            if (max * step < ma)
                ++max;
            return new IntervalI(min, max);
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

        public double InverseConvertDimensionX(float dst)
        {
            return dst / scale_x;
        }

        public double InverseConvertDimensionY(float dst)
        {
            return dst / scale_y;
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

        public float Width { get { return dst_orig_w; } }
        public float Height { get { return dst_orig_h; } }

        float dst_orig_w, dst_orig_h;
        float dst_x0, dst_y0;
        double src_x0, src_y0;
        double scale_x, scale_y;
    }
}
