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
        public class CoordinateSystem { }

        public class Cartesian : CoordinateSystem { }
        public class Spherical : CoordinateSystem { }
        public class Geographic : CoordinateSystem { }

        public class Unit { }
        public class None : Unit { }
        public class Radian : Unit { }
        public class Degree : Unit { }

        public class Point<CS, U>// where CS : CoordinateSystem where U : Unit
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

        public class Box<CS, U>
        {
            public Box()
            { }

            public Box(Point<CS, U> min, Point<CS, U> max)
            {
                this.min = min;
                this.max = max;
            }

            public bool IsValid() { return min[0] <= max[0] && min[1] <= max[1]; }

            public double Width { get { return max[0] - min[0]; } }
            public double Height { get { return max[1] - min[1]; } }

            public Point<CS, U> min, max;
        }

        public static void AssignInverse<CS, U>(Box<CS, U> b)
        {
            b.min = new Point<CS, U>(double.MaxValue, double.MaxValue);
            b.max = new Point<CS, U>(double.MinValue, double.MinValue);
        }

        public static void Expand<CS, U>(Box<CS, U> box, Point<CS, U> p)
        {
            if (p[0] < box.min[0]) box.min[0] = p[0];
            if (p[1] < box.min[1]) box.min[1] = p[1];
            if (p[0] > box.max[0]) box.max[0] = p[0];
            if (p[1] > box.max[1]) box.max[1] = p[1];
        }

        public static void Expand<CS, U>(Box<CS, U> box, Box<CS, U> b)
        {
            if (b.min[0] < box.min[0]) box.min[0] = b.min[0];
            if (b.min[1] < box.min[1]) box.min[1] = b.min[1];
            if (b.max[0] > box.max[0]) box.max[0] = b.max[0];
            if (b.max[1] > box.max[1]) box.max[1] = b.max[1];
        }

        public class Segment<CS, U>
        {
            public Segment()
            { }

            public Segment(Point<CS, U> p0, Point<CS, U> p1)
            {
                this.p0 = p0;
                this.p1 = p1;
            }

            public Point<CS, U> this[int i]
            {
                get
                {
                    Debug.Assert(i == 0 || i == 1);
                    return i == 0 ? p0 : p1;
                }
            }

            public int Count { get { return 2; } }

            protected Point<CS, U> p0;
            protected Point<CS, U> p1;
        }

        public static Box<CS, U> Envelope<CS, U>(Segment<CS, U> seg)
        {
            return new Box<CS, U>(
                    new Point<CS, U>(Math.Min(seg[0][0], seg[1][0]),
                                     Math.Min(seg[0][1], seg[1][1])),
                    new Point<CS, U>(Math.Max(seg[0][0], seg[1][0]),
                                     Math.Max(seg[0][1], seg[1][1]))
                );
        }

        public class Linestring<CS, U>
        {
            public Linestring()
            {
                points = new List<Point<CS, U>>();
            }

            public void Add(Point<CS, U> p) { points.Add(p); }

            public Point<CS, U> this[int i] { get { return points[i]; } }
            public int Count { get { return points.Count; } }

            protected List<Point<CS, U>> points;
        }

        public class Ring<CS, U>
        {
            public Ring()
            {
                linestring = new Linestring<CS, U>();
            }

            public void Add(Point<CS, U> p) { linestring.Add(p); }

            public Point<CS, U> this[int i] { get { return linestring[i]; } }
            public int Count { get { return linestring.Count; } }

            protected Linestring<CS, U> linestring;
        }

        public class Polygon<CS, U>
        {
            public Polygon()
            {
                outer = new Ring<CS, U>();
                inners = new List<Ring<CS, U>>();
            }

            public Ring<CS, U> Outer { get { return outer; } }
            public List<Ring<CS, U>> Inners { get { return inners; } }

            protected Ring<CS, U> outer;
            protected List<Ring<CS, U>> inners;
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
    }
}
