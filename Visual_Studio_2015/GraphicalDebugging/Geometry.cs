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
        public enum CoordinateSystem { Cartesian, Spherical, Geographic };
        public enum Unit { None, Radian, Degree };

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

        public class Point
        {
            public Point() { }
            //public Point(double x) { coords = new double[1] { x }; }
            public Point(double x, double y) { coords = new double[2] { x, y }; }
            //public Point(double x, double y, double z) { coords = new double[3] { x, y, z }; }

            public double this[int i]
            {
                get { return coords[i]; }
                set { coords[i] = value; }
            }

            public int Dimension { get { return 2/*coords != null ? coords.Length : 0*/; } }

            protected double[] coords;
        }

        public class Box
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

            public Point Min, Max;
        }

        public class Segment
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

        public class Linestring
        {
            public Linestring()
            {
                points = new List<Point>();
            }

            public void Add(Point p) { points.Add(p); }

            public Point this[int i] { get { return points[i]; } }
            public int Count { get { return points.Count; } }

            protected List<Point> points;
        }

        public class Ring
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

        public class Multi<G>
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

        public static Box Envelope(Segment seg)
        {
            return new Box(
                    new Point(Math.Min(seg[0][0], seg[1][0]),
                              Math.Min(seg[0][1], seg[1][1])),
                    new Point(Math.Max(seg[0][0], seg[1][0]),
                              Math.Max(seg[0][1], seg[1][1]))
                );
        }
        // NOTE: Geometries must be normalized
        // U is Radian or Degree
        /*public static Box Envelope(Segment seg)
        {
            Box result = new Box(seg[0], seg[0]);
            Expand(result, seg[1]);
            return result;
        }*/
        
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

        public static void Expand(Box box, Box b)
        {
            if (b.Min[0] < box.Min[0]) box.Min[0] = b.Min[0];
            if (b.Min[1] < box.Min[1]) box.Min[1] = b.Min[1];
            if (b.Max[0] > box.Max[0]) box.Max[0] = b.Max[0];
            if (b.Max[1] > box.Max[1]) box.Max[1] = b.Max[1];
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

        /*public static void Normalize(Point p, Traits traits)
        {
            if (traits.Unit != Unit.None)
                NormalizeAngle(p, traits.Unit);
        }
        private static void NormalizeAngle(Point p, Unit unit)
        {
            double pi = HalfAngle(unit);
            while (p[0] < -pi) p[0] += 2 * pi;
            while (p[0] > pi) p[0] -= 2 * pi;
        }

        public static void Normalize(Box b, Traits traits)
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
        }

        private static double HalfAngle(Unit unit)
        {
            return unit == Unit.Degree ? 180 : Math.PI;
        }*/
    }
}
