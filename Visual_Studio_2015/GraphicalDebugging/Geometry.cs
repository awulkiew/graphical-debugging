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
        public enum CoordinateSystem { Cartesian, Spherical, SphericalEquatorial, Geographic };
        public enum Unit { None, Radian, Degree };

        public static string Name(CoordinateSystem cs)
        {
            switch (cs)
            {
                case CoordinateSystem.Cartesian: return "cartesian";
                case CoordinateSystem.Spherical: return "spherical";
                case CoordinateSystem.SphericalEquatorial: return "spherical_equatorial";
                case CoordinateSystem.Geographic: return "geographic";
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
            public Point() { }
            //public Point(double x) { coords = new double[1] { x }; }
            public Point(double x, double y) { coords = new double[2] { x, y }; }
            public Point(double x, double y, double z) { coords = new double[3] { x, y, z }; }

            public double this[int i]
            {
                get { return coords[i]; }
                set { coords[i] = value; }
            }

            public object Clone()
            {
                return this.MemberwiseClone();
            }

            public int Dimension { get { return coords != null ? coords.Length : 0; } }

            protected double[] coords;
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

            public double Width { get { return Max[0] - Min[0]; } }
            public double Height { get { return Max[1] - Min[1]; } }

            public object Clone()
            {
                return this.MemberwiseClone();
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

            public Point Center;
            public double Radius;
        }

        public interface IRandomAccessRange<T>
        {
            T this[int i] { get; }
            int Count { get; }
        }

        public class Linestring : IRandomAccessRange<Point>
        {
            public Linestring()
            {
                points = new List<Point>();
            }

            public void Add(Point p) { points.Add(p); }

            public Point this[int i] { get { return points[i]; } }
            public int Count { get { return points.Count; } }

            public IEnumerator<Point> GetEnumerator() { return points.GetEnumerator(); }

            protected List<Point> points;
        }

        public class Ring : IRandomAccessRange<Point>
        {
            public Ring()
            {
                linestring = new Linestring();
            }

            public void Add(Point p) { linestring.Add(p); }

            public Point this[int i] { get { return linestring[i]; } }
            public int Count { get { return linestring.Count; } }

            protected Linestring linestring;
        }

        public class Polygon
        {
            public Polygon()
            {
                outer = new Ring();
                inners = new List<Ring>();
            }

            public Ring Outer { get { return outer; } }
            public List<Ring> Inners { get { return inners; } }

            protected Ring outer;
            protected List<Ring> inners;
        }

        public class Multi<G> : IRandomAccessRange<G>
        {
            public Multi()
            {
                singles = new List<G>();
            }

            public void Add(G g) { singles.Add(g); }

            public G this[int i] { get { return singles[i]; } }
            public int Count { get { return singles.Count; } }

            protected List<G> singles;
        }

        public static void AssignInverse(Box b)
        {
            b.Min = new Point(double.MaxValue, double.MaxValue);
            b.Max = new Point(double.MinValue, double.MinValue);
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

        public static Box Aabb(IEnumerator<Point> points, Unit unit)
        {
            Box result = new Box();

            if (!points.MoveNext())
            {
                AssignInverse(result);
                return result;
            }
            Point p1 = points.Current;
            if (!points.MoveNext())
            {
                // NOTE: unsafe, if this Box is modified then the original points will be modified as well
                result = new Box(p1, p1);
                return result;
            }
            Point p2 = points.Current;

            result = Aabb(p1, p2, unit);
            while(points.MoveNext())
            {
                p1 = p2;
                p2 = points.Current;
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
            return Aabb(linestring.GetEnumerator(), traits.Unit);
        }

        private static bool IntersectsAntimeridian(Point p1, Point p2, Unit unit)
        {
            if (unit == Unit.None)
            {
                return false;
            }
            else
            {
                double x1 = NormalizedAngle(p1[0], unit);
                double x2 = NormalizedAngle(p2[0], unit);
                double dist = x2 - x1;
                double pi = HalfAngle(unit);
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
                double pi = HalfAngle(unit);
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
            double distNorm = NormalizedAngle(p1[0] - p0[0], unit); // [-pi, pi]
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
                p1[0] += 2 * HalfAngle(unit);
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

        public static Box Envelope(IRandomAccessRange<Point> range, Traits traits)
        {
            if (range.Count <= 0)
            {
                Box result = new Box();
                AssignInverse(result);
                return result;
            }
            else if (range.Count <= 1)
            {
                return new Box(range[0], range[0]);
            }
            else
            {
                Box result = Envelope(range[0], range[1], traits);
                for (int i = 2; i < range.Count; ++i)
                {
                    Box b = Envelope(range[i], range[i - 1], traits);
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
            double xmin1 = NormalizedAngle(box.Min[0], unit);
            double xmax1 = NormalizedAngle(box.Max[0], unit);
            double xmin2 = NormalizedAngle(b.Min[0], unit);
            double xmax2 = NormalizedAngle(b.Max[0], unit);
            if (xmax1 < xmin1)
                xmax1 += 2 * HalfAngle(unit);
            if (xmax2 < xmin2)
                xmax2 += 2 * HalfAngle(unit);

            double twoPi = 2 * HalfAngle(unit);
            double left_dist = NormalizedAngle(xmin1 - xmin2, unit);
            double right_dist = NormalizedAngle(xmax2 - xmax1, unit);
            if (left_dist >= 0 && right_dist >= 0)
            {
                if (left_dist < right_dist)
                    box.Min[0] = xmin2;
                else
                    box.Max[0] = xmax2;
            }
            else if (left_dist >= 0)
                box.Min[0] = xmin2;
            else if (right_dist >= 0)
                box.Max[0] = xmax2;

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
                NormalizeAngle(p, traits.Unit);
        }
        private static void NormalizeAngle(Point p, Unit unit)
        {
            p[0] = NormalizedAngle(p[0], unit);
        }

        public static Point Normalized(Point p, Unit unit)
        {
            return new Point(NormalizedAngle(p[0], unit), p[1]);
        }

        public static double NormalizedAngle(double x, Unit unit)
        {
            double pi = HalfAngle(unit);
            while (x < -pi) x += 2 * pi;
            while (x > pi) x -= 2 * pi;
            return x;
        }

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
                b.Max[0] += 2 * HalfAngle(unit);
        }*/

        public static double HalfAngle(Unit unit)
        {
            return unit == Unit.Degree ? 180 : Math.PI;
        }
    }
}
