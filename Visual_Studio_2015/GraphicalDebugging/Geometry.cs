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
        public class Point
        {
            public Point()
            { }

            public Point(double x, double y)
            {
                coords = new double[2] { x, y };
            }

            public double this[int i]
            {
                get { return coords[i]; }
                set { coords[i] = value; }
            }

            protected double[] coords;
        }

        public class Box
        {
            public static Box Inverted()
            {
                return new Box(
                    new Point(double.MaxValue, double.MaxValue),
                    new Point(double.MinValue, double.MinValue));
            }

            public Box()
            { }

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

            public bool IsValid() { return min[0] <= max[0] && min[1] <= max[1]; }

            public double Width { get { return max[0] - min[0]; } }
            public double Height { get { return max[1] - min[1]; } }

            public Point min, max;
        }

        public class Segment
        {
            public Segment()
            { }

            public Segment(Point p0, Point p1)
            {
                this.p0 = p0;
                this.p1 = p1;
            }

            public Point this[int i]
            {
                get
                {
                    Debug.Assert(i == 0 || i == 1);
                    return i == 0 ? p0 : p1;
                }
            }

            public int Count { get { return 2; } }

            public Box Envelope()
            {
                return new Geometry.Box(
                        new Geometry.Point(Math.Min(this[0][0], this[1][0]),
                                           Math.Min(this[0][1], this[1][1])),
                        new Geometry.Point(Math.Max(this[0][0], this[1][0]),
                                           Math.Max(this[0][1], this[1][1]))
                    );
            }

            protected Point p0;
            protected Point p1;
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
    }
}
