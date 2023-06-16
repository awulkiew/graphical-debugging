//------------------------------------------------------------------------------
// <copyright file="Geometry.cs">
//     Copyright (c) Adam Wulkiewicz.
// </copyright>
//------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace GraphicalDebugging
{
    class Geometry
    {
        public enum CoordinateSystem { None, Cartesian, SphericalPolar, SphericalEquatorial, Geographic, Complex };
        public enum Unit { None, Radian, Degree };

        public static string Name(CoordinateSystem cs)
        {
            switch (cs)
            {
                case CoordinateSystem.Cartesian: return "cartesian";
                case CoordinateSystem.SphericalPolar: return "spherical_polar";
                case CoordinateSystem.SphericalEquatorial: return "spherical_equatorial";
                case CoordinateSystem.Geographic: return "geographic";
                case CoordinateSystem.Complex: return "complex";
                default: return "unknown";
            }
        }

        public static string Name(Unit unit)
        {
            switch (unit)
            {
                case Unit.None: return "";
                case Unit.Radian: return "radian";
                case Unit.Degree: return "degree";
                default: return "unknown";
            }
        }

        public class Traits
        {
            public Traits(int dimension)
            {
                Dimension = dimension;
                CoordinateSystem = CoordinateSystem.Cartesian;
                Unit = Unit.None;
            }

            public Traits(int dimension, CoordinateSystem coordinateSystem, Unit unit)
            {
                Dimension = dimension;
                CoordinateSystem = coordinateSystem;
                Unit = unit;
            }

            public int Dimension { get; }
            public CoordinateSystem CoordinateSystem { get; }
            public Unit Unit { get; }
        }

        public class Point : ICloneable
        {
            protected Point() { }
            public Point(double x, double y) { coords = new double[2] { x, y }; }
            public Point(double x, double y, double z) { coords = new double[3] { x, y, z }; }

            public double this[int i]
            {
                get { return coords[i]; }
                set { coords[i] = value; }
            }

            public int Dimension
            {
                get { return coords.Length; }
            }

            public bool Equals(Point other)
            {
                return coords.AsSpan().SequenceEqual(other.coords);
            }

            public Point Clone()
            {
                Point res = new Point();
                res.coords = (double[])coords.Clone();
                return res;
            }

            object ICloneable.Clone()
            {
                return this.Clone();
            }

            public override string ToString()
            {
                string res = "";
                if (coords != null)
                {
                    for (int i = 0; i < coords.Length; ++i)
                    {
                        res += coords[i].ToString();
                        if (i + 1 < coords.Length)
                            res += ", ";
                    }
                }
                return res;
            }

            protected double[] coords;
        }

        public class Interval
        {
            public Interval()
            { }

            public Interval(double mi, double ma)
            {
                Min = mi;
                Max = ma;
            }

            public void Expand(double v)
            {
                Min = Math.Min(Min, v);
                Max = Math.Max(Max, v);
            }

            public void Expand(Interval i)
            {
                Min = Math.Min(Min, i.Min);
                Max = Math.Max(Max, i.Max);
            }

            public double Min;
            public double Max;
        }

        public class Box : ICloneable
        {
            public Box()
            { }

            public Box(Point min, Point max)
            {
                Min = min;
                Max = max;
            }

            public bool IsValid() { return Min[0] <= Max[0] && Min[1] <= Max[1]; }

            public double Dim(int i) { return Max[i] - Min[i]; }

            public bool Equals(Box other)
            {
                return Min.Equals(other.Min) && Max.Equals(other.Max);
            }

            public Box Clone()
            {
                return new Box(Min.Clone(), Max.Clone());
            }

            object ICloneable.Clone()
            {
                return this.Clone();
            }

            public override string ToString()
            {
                return "{" + Min.ToString() + "}, {" + Max.ToString() + "}";
            }

            public Point Min, Max;
        }

        public class Segment : IRandomAccessRange<Geometry.Point>
        {
            public Segment()
            { }

            public Segment(Point first, Point second)
            {
                First = first;
                Second = second;
            }

            public Point this[int i]
            {
                get
                {
                    Debug.Assert(i == 0 || i == 1);
                    return i == 0 ? First : Second;
                }
            }

            public int Count { get { return 2; } }

            public override string ToString()
            {
                return "{" + First.ToString() + "}, {" + Second.ToString() + "}";
            }

            public Point First;
            public Point Second;
        }

        public class NSphere
        {
            public NSphere()
            { }

            public NSphere(Point center, double radius)
            {
                Center = center;
                Radius = radius;
            }

            public override string ToString()
            {
                return "{" + Center.ToString() + "}, " + Radius.ToString();
            }

            public Point Center;
            public double Radius;
        }

        public interface IRandomAccessRange<T>
        {
            T this[int i] { get; }
            int Count { get; }
        }

        public interface IContainer<T>
        {
            void Add(T v);
            void Clear();
        }

        public class Container<T> : IRandomAccessRange<T>, IContainer<T>
        {
            public T this[int i] { get { return list[i]; } }
            public int Count { get { return list.Count; } }

            public void Add(T v) { list.Add(v); }
            public void Clear() { list.Clear(); }

            public override string ToString()
            {
                return "Count=" + list.Count;
            }

            protected List<T> list = new List<T>();
        }

        public class Linestring : Container<Point>
        { }

        public class Ring : Container<Point>
        { }

        public class Polygon
        {
            public Polygon()
            {
                outer = new Ring();
                inners = new List<Ring>();
            }

            public Ring Outer { get { return outer; } }
            public List<Ring> Inners { get { return inners; } }

            public override string ToString()
            {
                return "Outer={" + Outer.ToString() + "}, Inners={Count=" + inners.Count.ToString() + "}";
            }

            protected Ring outer;
            protected List<Ring> inners;
        }

        public class Multi<G> : Container<G>
        { }

        public class MultiPoint : Multi<Point>
        { }

        public class MultiLinestring : Multi<Linestring>
        { }

        public class MultiPolygon : Multi<Polygon>
        { }

        public static void AssignInverse(Box b)
        {
            b.Min = new Point(double.MaxValue, double.MaxValue);
            b.Max = new Point(double.MinValue, double.MinValue);
        }

        public static void AssignInverse(Interval i)
        {
            i.Min = double.MaxValue;
            i.Max = double.MinValue;
        }

        public static Box InversedBox()
        {
            Box result = new Box();
            AssignInverse(result);
            return result;
        }

        public static Interval InversedInterval()
        {
            Interval result = new Interval();
            AssignInverse(result);
            return result;
        }

        public static Box Aabb(Point p1, Point p2, Unit unit)
        {
            if (unit == Unit.None)
                return Aabb(p1, p2);
            else
                return AabbAngle(p1, p2, unit);
        }
        public static Box Aabb(Point p1, Point p2)
        {
            return new Box(
                    new Point(Math.Min(p1[0], p2[0]),
                              Math.Min(p1[1], p2[1])),
                    new Point(Math.Max(p1[0], p2[0]),
                              Math.Max(p1[1], p2[1]))
                );
        }
        public static Box AabbAngle(Point p1, Point p2, Unit unit)
        {
            Box result = Aabb(p1, p2);
            EnlargeAabbAngle(result, p1, p2, unit);
            return result;
        }

        public static Box Aabb(IRandomAccessRange<Point> points, bool closed, Unit unit)
        {
            if (points.Count < 1)
            {
                return InversedBox();
            }

            Point p1 = points[0];
            if (points.Count < 2)
            {
                // NOTE: unsafe, if this Box is modified then the original points will be modified as well
                return new Box(p1, p1);
            }
            Point p2 = points[1];

            Box result = Aabb(p1, p2, unit);
            int count = points.Count + (closed ? 1 : 0);
            for (int i = 2; i < count; ++i)
            {
                p1 = p2;
                p2 = points[i % points.Count];
                Box b = Aabb(p1, p2, unit);
                Expand(result, b);
            }
            return result;
        }

        private static void EnlargeAabbAngle(Box box, Point p1, Point p2, Unit unit)
        {
            if ((p1[0] < 0 && p2[0] >= 0
              || p1[0] >= 0 && p2[0] < 0)
               && IntersectsAntimeridian(p1, p2, unit))
            {
                box.Min[0] = NearestAntimeridian(box.Min[0], -1, unit);
                box.Max[0] = NearestAntimeridian(box.Max[0], 1, unit);
            }
        }

        public static Box Aabb(Segment seg, Traits traits)
        {
            return Aabb(seg[0], seg[1], traits.Unit);
        }
        public static Box Aabb(Linestring linestring, Traits traits)
        {
            return Aabb(linestring, false, traits.Unit);
        }

        public static bool IntersectsX(Box box, double x)
        {
            return box.Min[0] <= x && x <= box.Max[0];
        }

        public static bool IntersectsY(Box box, double y)
        {
            return box.Min[1] <= y && y <= box.Max[1];
        }

        private static bool IntersectsAntimeridian(Point p1, Point p2, Unit unit)
        {
            if (unit == Unit.None)
            {
                return false;
            }
            else
            {
                double x1 = NormalizedAngleSigned(p1[0], unit);
                double x2 = NormalizedAngleSigned(p2[0], unit);
                double dist = x2 - x1;
                double pi = StraightAngle(unit);
                return dist < -pi || pi < dist;
            }
        }
        
        public static double NearestAntimeridian(double x, int dir, Unit unit)
        {
            double result = x;

            if (unit == Unit.None)
            {
                return result;
            }
            else
            {
                double pi = StraightAngle(unit);
                double ax = Math.Abs(x);
                double periods = (ax - pi) / (2 * pi);
                int calcDir = x < 0 ? -dir : dir;
                double f = calcDir < 0 ?
                           // [0, pi)   : -1 : -pi
                           // pi        : 0  : pi
                           // (pi, 3pi) : 0  : pi
                           // 3pi       : 1  : 3pi
                           Math.Floor(periods) :
                           // [0, pi)   : 0 : pi
                           // pi        : 0 : pi
                           // (pi, 3pi) : 1 : 3pi
                           // 3pi       : 1 : 3pi
                           Math.Ceiling(periods);

                result = pi + f * 2 * pi;

                if (x < 0)
                    result = -result;
            }

            return result;
        }

        public static Box Envelope(Segment seg, Traits traits)
        {
            return Envelope(seg[0], seg[1], traits);
        }
        public static Box Envelope(Point p0, Point p1, Traits traits)
        {
            if (traits.Unit == Unit.None)
                return Envelope(p0, p1);
            else
                return EnvelopeAngle(p0, p1, traits.Unit);
        }
        public static Box Envelope(Point p0, Point p1)
        {
            return new Box(new Point(Math.Min(p0[0], p1[0]),
                                     Math.Min(p0[1], p1[1])),
                           new Point(Math.Max(p0[0], p1[0]),
                                     Math.Max(p0[1], p1[1])));
        }
        public static Box EnvelopeAngle(Point p0_, Point p1_, Unit unit)
        {
            Point p0 = Normalized(p0_, unit);
            Point p1 = Normalized(p1_, unit);
            double distNorm = NormalizedAngleSigned(p1[0] - p0[0], unit); // [-pi, pi]
            if (distNorm < 0)
                p0[0] = p1[0] - distNorm;
            else
                p1[0] = p0[0] + distNorm;
            return Envelope(p0, p1);
        }

        public static Box Envelope(Box box, Traits traits)
        {
            if (traits.Unit == Unit.None)
                return Envelope(box);
            else
                return EnvelopeAngle(box, traits.Unit);
        }
        public static Box Envelope(Box box)
        {
            return Envelope(box.Min, box.Max);
        }
        public static Box EnvelopeAngle(Box box, Unit unit)
        {
            Point p0 = Normalized(box.Min, unit);
            Point p1 = Normalized(box.Max, unit);
            if (p1[0] < p0[0])
                p1[0] += FullAngle(unit);
            return Envelope(p0, p1);
        }

        public static void Expand(Box box, Point p)
        {
            if (p[0] < box.Min[0]) box.Min[0] = p[0];
            if (p[1] < box.Min[1]) box.Min[1] = p[1];
            if (p[0] > box.Max[0]) box.Max[0] = p[0];
            if (p[1] > box.Max[1]) box.Max[1] = p[1];
        }
        // NOTE: Geometries must be normalized
        /*public static void Expand<CS>(Box<CS, Radian> box, Point<CS, Radian> p)
        {
            double minDist = box.min[0] - p[0];
            while (minDist < 0)
                minDist += 2 * Math.PI;
            double maxDist = p[0] - box.Max[0];
            while (maxDist < 0)
                maxDist += 2 * Math.PI;

            if (minDist < maxDist)
                box.min[0] = p[0];
            else
                box.Max[0] = p[0] < box.min[0] ? p[0] + 2 * Math.PI : p[0];

            if (p[1] < box.min[1]) box.min[1] = p[1];
            if (p[1] > box.Max[1]) box.Max[1] = p[1];
        }
        // NOTE: Geometries must be normalized
        public static void Expand<CS>(Box<CS, Degree> box, Point<CS, Degree> p)
        {
            double minDist = box.Min[0] - p[0];
            while (minDist < 0)
                minDist += 360;
            double maxDist = p[0] - box.Max[0];
            while (maxDist < 0)
                maxDist += 360;

            if (minDist < maxDist)
                box.Min[0] = p[0];
            else
                box.Max[0] = p[0] < box.Min[0] ? p[0] + 360 : p[0];

            if (p[1] < box.Min[1]) box.Min[1] = p[1];
            if (p[1] > box.Max[1]) box.Max[1] = p[1];
        }*/

        public static Box Envelope(IRandomAccessRange<Point> range, bool closed, Traits traits)
        {
            if (range.Count <= 0)
            {
                return InversedBox();
            }
            else if (range.Count <= 1)
            {
                return new Box(range[0], range[0]);
            }
            else
            {
                int count = range.Count + (closed ? 1 : 0);
                Box result = Envelope(range[0], range[1], traits);
                for (int i = 2; i < count; ++i)
                {
                    Box b = Envelope(range[i % range.Count], range[i - 1], traits);
                    Expand(result, b, traits);
                }
                return result;
            }
        }

        public static void Expand(Box box, Box b, Traits traits)
        {
            if (traits.Unit == Unit.None)
                Expand(box, b);
            else
                ExpandAngle(box, b, traits.Unit);
        }
        public static void Expand(Box box, Box b)
        {
            if (b.Min[0] < box.Min[0]) box.Min[0] = b.Min[0];
            if (b.Min[1] < box.Min[1]) box.Min[1] = b.Min[1];
            if (b.Max[0] > box.Max[0]) box.Max[0] = b.Max[0];
            if (b.Max[1] > box.Max[1]) box.Max[1] = b.Max[1];
        }
        public static void ExpandAngle(Box box, Box b, Unit unit)
        {
            double xmin1 = NormalizedAngleSigned(box.Min[0], unit);
            double xmax1 = NormalizedAngleSigned(box.Max[0], unit);
            double xmin2 = NormalizedAngleSigned(b.Min[0], unit);
            double xmax2 = NormalizedAngleSigned(b.Max[0], unit);

            double twoPi = FullAngle(unit);
            if (xmax1 < xmin1)
                xmax1 += twoPi;
            if (xmax2 < xmin2)
                xmax2 += twoPi;

            double left_dist = NormalizedAngleSigned(xmin1 - xmin2, unit);
            double right_dist = NormalizedAngleSigned(xmax2 - xmax1, unit);
            if (left_dist >= 0 && right_dist >= 0)
            {
                if (left_dist < right_dist)
                    box.Min[0] -= left_dist;
                else
                    box.Max[0] += right_dist;
            }
            else if (left_dist >= 0)
                box.Min[0] -= left_dist;
            else if (right_dist >= 0)
                box.Max[0] += right_dist;

            if (box.Max[0] < box.Min[0])
                box.Max[0] += twoPi;

            if (b.Min[1] < box.Min[1]) box.Min[1] = b.Min[1];
            if (b.Max[1] > box.Max[1]) box.Max[1] = b.Max[1];
        }

        public static bool Disjoint(Box b, Point p, int dim)
        {
            return p[dim] < b.Min[dim] || b.Max[dim] < p[dim];
        }

        /*public static bool Disjoint(Box l, Box r, int dim)
        {
            return l.Max[dim] < r.Min[dim] || r.Min[dim] < l.Min[dim];
        }
        // NOTE: Boxes must be normalized
        public static bool Disjoint(Box l, Box r, int dim)
        {
            if (dim != 0)
                return l.Max[dim] < r.Min[dim] || r.Max[dim] < l.Min[dim];
            else
                return l.Max[0] < r.Min[0] || (l.Max[0] > Math.PI && l.Max[0] - 2 * Math.PI < r.Min[0])
                    || r.Max[0] < l.Min[0] || (r.Max[0] > Math.PI && r.Max[0] - 2 * Math.PI < l.Min[0]);
        }
        // NOTE: Boxes must be normalized
        public static bool Disjoint(Box l, Box r, int dim)
        {
            if (dim != 0)
                return l.Max[dim] < r.Min[dim] || r.Max[dim] < l.Min[dim];
            else
                return l.Max[0] < r.Min[0] || (l.Max[0] > 180 && l.Max[0] - 360 < r.Min[0])
                    || r.Max[0] < l.Min[0] || (r.Max[0] > 180 && r.Max[0] - 360 < l.Min[0]);
        }*/

        public static void Normalize(Point p, Traits traits)
        {
            if (traits.Unit != Unit.None)
                Normalize(p, traits.Unit);
        }
        private static void Normalize(Point p, Unit unit)
        {
            p[0] = NormalizedAngleSigned(p[0], unit);
        }

        public static Point Normalized(Point p, Unit unit)
        {
            return new Point(NormalizedAngleSigned(p[0], unit), p[1]);
        }

        public static double NormalizedAngleSigned(double x, Unit unit)
        {
            double pi = StraightAngle(unit);
            double twoPi = FullAngle(unit);
            if (x < -pi)
                return ((x - pi) % twoPi) + pi;
            else if (x > pi)
                return ((x + pi) % twoPi) - pi;
            else
                return x;
        }

        public static double NormalizedAngleUnsigned(double x, Unit unit)
        {
            double twoPi = FullAngle(unit);
            if (x < 0)
                return (x % twoPi) + twoPi;
            else if (x > twoPi)
                return (x % twoPi);
            else
                return x;
        }

        /*public static double NormalizedAngle(double x, Unit unit)
        {
            return NormalizedAngleSigned(x, unit);
        }*/

        /*public static void Normalize(Box b, Traits traits)
        {
            if (traits.Unit != Unit.None)
                NormalizeAngle(b, traits.Unit);
        }
        private static void NormalizeAngle(Box b, Unit unit)
        {
            NormalizeAngle(b.Min, unit);
            NormalizeAngle(b.Max, unit);
            if (b.Min[0] > b.Max[0])
                b.Max[0] += FullAngle(unit);
        }*/

        public static double StraightAngle(Unit unit)
        {
            return unit == Unit.Degree ? 180 : Math.PI;
        }

        public static double FullAngle(Unit unit)
        {
            return unit == Unit.Degree ? 360 : (2.0 * Math.PI);
        }

        public static double RightAngle(Unit unit)
        {
            return unit == Unit.Degree ? 90 : (0.5 * Math.PI);
        }

        public static double ToRadian(double angle, Unit unit)
        {
            return unit == Unit.Degree ? (angle * Math.PI / 180.0) : angle;
        }

        public static double FromRadian(double angle, Unit unit)
        {
            return unit == Unit.Degree ? (angle / Math.PI * 180.0) : angle;
        }

        public static double FromDegree(double angle, Unit unit)
        {
            return unit == Unit.Degree ? angle : (angle / 180.0 * Math.PI);
        }

        public static Point SphToCart3d(Point p, Unit unit)
        {
            double lon = ToRadian(p[0], unit);
            double lat = ToRadian(p[1], unit);
            double cosLat = Math.Cos(lat);
            return new Point(cosLat * Math.Cos(lon),
                             cosLat * Math.Sin(lon),
                             Math.Sin(lat));
        }

        public static Point Cart3dToSph(Point p, Unit unit)
        {
            double lon = Math.Atan2(p[1], p[0]);
            double lat = Math.Asin(p[2]);
            return new Point(FromRadian(lon, unit),
                             FromRadian(lat, unit));
        }

        public static double Dot(Point p0, Point p1)
        {
            Debug.Assert(p0.Dimension == p1.Dimension);
            double result = 0;
            for (int i = 0; i < p0.Dimension; ++i)
                result += p0[i] * p1[i];
            return result;
        }

        public static Point Cross(Point p0, Point p1)
        {
            Debug.Assert(p0.Dimension == 3 && p1.Dimension == 3);
            double x = p0[1] * p1[2] - p0[2] * p1[1];
            double y = p0[2] * p1[0] - p0[0] * p1[2];
            double z = p0[0] * p1[1] - p0[1] * p1[0];
            return new Point(x, y, z);
        }

        public static void Add(Point p, Point p2)
        {
            Debug.Assert(p.Dimension == p2.Dimension);
            for (int i = 0; i < p.Dimension; ++i)
                p[i] += p2[i];
        }

        public static Point Added(Point p1, Point p2)
        {
            Point res = p1.Clone();
            Add(res, p2);
            return res;
        }

        public static void Sub(Point p, Point p2)
        {
            Debug.Assert(p.Dimension == p2.Dimension);
            for (int i = 0; i < p.Dimension; ++i)
                p[i] -= p2[i];
        }

        public static Point Subed(Point p1, Point p2)
        {
            Point res = p1.Clone();
            Sub(res, p2);
            return res;
        }

        public static void Div(Point p, double v)
        {
            for (int i = 0; i < p.Dimension; ++i)
                p[i] /= v;
        }

        public static void Mul(Point p, double v)
        {
            for (int i = 0; i < p.Dimension; ++i)
                p[i] *= v;
        }

        public static bool VecNormalize(Point p)
        {
            double l = Math.Sqrt(Dot(p, p));

            if (Equals(l, 0.0))
                return false;

            Div(p, l);

            return true;
        }

        public static bool Equals(double a, double b)
        {
            return Math.Abs(a - b) < double.Epsilon;
        }

        public static Point[] SphericalDensify(Point p0, Point p1, double length, Unit unit)
        {
            Point xyz0 = SphToCart3d(p0, unit);
            Point xyz1 = SphToCart3d(p1, unit);

            double dot01 = Dot(xyz0, xyz1);
            double angle01 = Math.Acos(dot01);
            double threshold = ToRadian(length, unit);

            int n = (int)(angle01 / threshold);

            if (n < 1)
                return new Point[0];

            // make sure the number of additional points is even
            // this way there will always be a segment in the middle
            // and something may be drawn there
            if (n % 2 != 0)
                ++n;

            Point axis;
            if (!Equals(angle01, Math.PI))
            {
                axis = Cross(xyz0, xyz1);
                VecNormalize(axis);
            }
            else
            {
                double halfPi = RightAngle(unit);
                if (Equals(p0[1], halfPi))
                    axis = new Point(0, 1, 0);
                else if (Equals(p0[1], -halfPi))
                    axis = new Point(0, -1, 0);
                else
                {
                    double lon = ToRadian(p0[0], unit);
                    axis = new Point(Math.Sin(lon), -Math.Cos(lon), 0);
                }
            }

            double step = angle01 / (n + 1);

            Point[] result = new Point[n];

            double a = step;
            for (int i = 0; i < n; ++i, a += step)
            {
                // Axis-Angle rotation
                // see: https://en.wikipedia.org/wiki/Axis-angle_representation
                double cos_a = Math.Cos(a);
                double sin_a = Math.Sin(a);
                // cos_a * v
                Point s1 = xyz0.Clone();
                Mul(s1, cos_a);
                // sin_a * (n x v)
                Point s2 = Cross(axis, xyz0);
                Mul(s2, sin_a);
                // (1 - cos_a)(n.v) * n
                Point s3 = axis.Clone();
                Mul(s3, (1.0 - cos_a) * Dot(axis, xyz0));
                // v_rot = cos_a * v + sin_a * (n x v) + (1 - cos_a)(n.v) * e
                Point v_rot = s1.Clone();
                Add(v_rot, s2);
                Add(v_rot, s3);

                result[i] = Cart3dToSph(v_rot, unit);
            }

            return result;
        }

        public static double SphericalTrapezoidArea(Point p0, Point p1, Unit unit)
        {
            double tanLat0 = Math.Tan(ToRadian(p0[1], unit) / 2.0);
            double tanLat1 = Math.Tan(ToRadian(p1[1], unit) / 2.0);
            return 2.0 * Math.Atan(
                            (tanLat0 + tanLat1) / (1 + tanLat0 * tanLat1)
                          * Math.Tan((ToRadian(p1[0], unit) - ToRadian(p0[0], unit)) / 2)
                         );
        }

        public static bool LineBoxIntersection(Point p0, Point p1, Box box, out Point pN, out Point pF)
        {
            pN = null;
            pF = null;
            double tN = double.NegativeInfinity;
            double tF = double.PositiveInfinity;
            int dimension = p0.Dimension;
            for (int i = 0; i < dimension; ++i)
                if (!LineBoxIntersection(p0, p1, box, i, ref tN, ref tF))
                    return false;
            Point d = Subed(p1, p0);
            Point dN = d.Clone();
            Mul(dN, tN);
            Point dF = d.Clone();            
            Mul(dF, tF);
            pN = Added(p0, dN);
            pF = Added(p0, dF);
            return true;
        }

        public static bool LineBoxIntersection(Point p0, Point p1, Box box, int i,
                                               ref double tNear, ref double tFar)
        {
            double o = p0[i];
            double d = p1[i] - o;
            double tN = (box.Min[i] - o) / d;
            double tF = (box.Max[i] - o) / d;
            if (tN > tF)
                Swap(ref tN, ref tF);
            tNear = Math.Max(tNear, tN);
            tFar = Math.Min(tFar, tF);
            return tNear <= tFar;
        }

        static void Swap<T>(ref T lhs, ref T rhs)
        {
            T temp;
            temp = lhs;
            lhs = rhs;
            rhs = temp;
        }
    }
}
