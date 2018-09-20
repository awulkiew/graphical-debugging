//------------------------------------------------------------------------------
// <copyright file="ExpressionLoader.cs">
//     Copyright (c) Adam Wulkiewicz.
// </copyright>
//------------------------------------------------------------------------------

using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;

using System;
using System.Collections;
using System.Collections.Generic;


namespace GraphicalDebugging
{
    class ExpressionLoader
    {
        public enum Kind { Point = 0, Segment, Box, NSphere, Linestring, Ring, Polygon, MultiPoint, MultiLinestring, MultiPolygon, Container, Variant, Turn, TurnsContainer };
        
        public ExpressionLoader()
        {
            loaders.Add(new BGPoint());
            loaders.Add(new BGPointXY());
            loaders.Add(new BGSegment());
            loaders.Add(new BGReferringSegment());
            loaders.Add(new BGBox());
            loaders.Add(new BGNSphere());
            loaders.Add(new BGMultiPoint());
            loaders.Add(new BGLinestring());
            loaders.Add(new BGMultiLinestring());
            loaders.Add(new BGRing());
            loaders.Add(new BGPolygon());
            loaders.Add(new BGMultiPolygon());
            loaders.Add(new BGBufferedRing());
            loaders.Add(new BGBufferedRingCollection());
            loaders.Add(new BPPoint());
            loaders.Add(new BPSegment());
            loaders.Add(new BPBox());
            loaders.Add(new BPRing());
            loaders.Add(new BPPolygon());
            loaders.Add(new StdArray());
            loaders.Add(new BoostArray());
            loaders.Add(new BGVarray());
            loaders.Add(new BoostContainerVector());
            loaders.Add(new BoostContainerStaticVector());
            loaders.Add(new StdVector());
            loaders.Add(new StdDeque());
            loaders.Add(new StdList());
            loaders.Add(new BVariant());
            loaders.Add(new BGTurn("boost::geometry::detail::overlay::turn_info"));
            loaders.Add(new BGTurn("boost::geometry::detail::overlay::traversal_turn_info"));
            loaders.Add(new BGTurn("boost::geometry::detail::buffer::buffer_turn_info"));
            loaders.Add(new BGTurnContainer("std::vector"));
            loaders.Add(new BGTurnContainer("std::deque"));
        }

        public class KindConstraint
        {
            virtual public bool Check(Kind kind) { return true; }
        }

        public class GeometryKindConstraint : KindConstraint
        {
            public override bool Check(Kind kind) { return kind != Kind.Container; }
        }

        public class ContainerKindConstraint : KindConstraint
        {
            public override bool Check(Kind kind) { return kind == Kind.Container; }
        }

        private static KindConstraint allowAllKinds = new KindConstraint();

        public void Load(Debugger debugger, string name,
                         out Geometry.Traits traits,
                         out ExpressionDrawer.IDrawable result)
        {

            Load(debugger, name, allowAllKinds, out traits, out result);
        }

        public void Load(Debugger debugger, string name,
                         KindConstraint kindConstraint,
                         out Geometry.Traits traits,
                         out ExpressionDrawer.IDrawable result)
        {
            traits = null;
            result = null;

            Expression expr = debugger.GetExpression(name);
            if (!expr.IsValidValue)
                return;

            Loader loader = loaders.FindByType(expr.Type);
            if (loader == null)
                return;

            if (!kindConstraint.Check(loader.Kind()))
                return;

            loader.Load(loaders, debugger, expr.Name, expr.Type, out traits, out result);
        }

        static int ParseInt(string s)
        {
            return int.Parse(s);
        }

        static double ParseDouble(string s)
        {
            return double.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
        }
        
        static double LoadAsDouble(Debugger debugger, string name, out bool ok)
        {
            Expression expr = debugger.GetExpression("(double)" + name);
            ok = expr.IsValidValue;
            return ok ? ParseDouble(expr.Value) : 0.0;
        }

        static bool IsOk<T>(T v1)
        {
            return !v1.Equals(default(T));
        }
        static bool IsOk<T1, T2>(T1 v1, T2 v2)
        {
            return !v1.Equals(default(T1))
                && !v2.Equals(default(T2));
        }
        static bool IsOk<T1, T2, T3>(T1 v1, T2 v2, T3 v3)
        {
            return !v1.Equals(default(T1))
                && !v2.Equals(default(T2))
                && !v3.Equals(default(T3));
        }
        static bool IsOk<T1, T2, T3, T4>(T1 v1, T2 v2, T3 v3, T4 v4)
        {
            return !v1.Equals(default(T1))
                && !v2.Equals(default(T2))
                && !v3.Equals(default(T3))
                && !v4.Equals(default(T4));
        }

        class Loaders
        {
            static int KindsCount = Enum.GetValues(typeof(Kind)).Length;

            public Loaders()
            {
                lists = new List<Loader>[KindsCount];
                for (int i = 0; i < KindsCount; ++i)
                    lists[i] = new List<Loader>();
            }

            public void Add(Loader loader)
            {
                int i = (int)loader.Kind();
                System.Diagnostics.Debug.Assert(0 <= i && i < KindsCount);
                // Put the loaders constrained by types at the beginning
                // to check them first while searching for correct one
                // NOTE: This may not be enough in general case of arbitrary constraints
                if (loader.Constraint() != null)
                    lists[i].Insert(0, loader);
                else
                    lists[i].Add(loader);
            }

            public Loader FindById(Kind kind, string id)
            {
                foreach (Loader l in lists[(int)kind])
                    if (id == l.Id())
                        return l;
                return null;
            }

            public Loader FindById(string id)
            {
                foreach (List<Loader> li in lists)
                    foreach (Loader l in li)
                        if (id == l.Id())
                            return l;
                return null;
            }

            public Loader FindByType(Kind kind, string type)
            {
                string id = Util.BaseType(type);
                foreach (Loader l in lists[(int)kind])
                {
                    TypeConstraint tc = l.Constraint();
                    if (id == l.Id() && (tc == null || tc.Ok(this, type)))
                        return l;
                }
                return null;
            }

            public Loader FindByType(string type)
            {
                string id = Util.BaseType(type);
                foreach (List<Loader> li in lists)
                    foreach (Loader l in li)
                    {
                        TypeConstraint tc = l.Constraint();
                        if (id == l.Id() && (tc == null || tc.Ok(this, type)))
                            return l;
                    }
                return null;
            }

            List<Loader>[] lists;
        }

        abstract class TypeConstraint
        {
            abstract public bool Ok(Loaders loaders, string type);
        }

        abstract class TparamConstraint : TypeConstraint
        {
            abstract public bool Ok(Loaders loaders, List<string> tparams);

            public override bool Ok(Loaders loaders, string type)
            {
                return Ok(loaders, Util.Tparams(type));
            }
        }

        class TparamKindConstraint : TparamConstraint
        {
            public TparamKindConstraint(int i, Kind kind)
            {
                this.i = i;
                this.kind = kind;
            }

            public override bool Ok(Loaders loaders, List<string> tparams)
            {
                if (i < tparams.Count)
                {
                    Loader loader = loaders.FindByType(tparams[i]);
                    if (loader != null)
                        return loader.Kind() == kind;
                }
                return false;
            }

            int i;
            Kind kind;
        }

        abstract class Loader
        {
            abstract public string Id();
            abstract public ExpressionLoader.Kind Kind();
            virtual public TypeConstraint Constraint() { return null; }

            abstract public void Load(Loaders loaders, Debugger debugger, string name, string type,
                                      out Geometry.Traits traits,
                                      out ExpressionDrawer.IDrawable result);
        }

        abstract class LoaderR<ResultType> : Loader
            where ResultType : ExpressionDrawer.IDrawable
        {
            public override void Load(Loaders loaders, Debugger debugger, string name, string type,
                                      out Geometry.Traits traits,
                                      out ExpressionDrawer.IDrawable result)
            {
                ResultType res = default(ResultType);
                this.Load(loaders, debugger, name, type, out traits, out res);
                result = res;
            }

            abstract public void Load(Loaders loaders, Debugger debugger, string name, string type,
                                      out Geometry.Traits traits,
                                      out ResultType result);
        }

        abstract class GeometryLoader<ResultType> : LoaderR<ResultType>
            where ResultType : ExpressionDrawer.IDrawable
        {
            protected GeometryLoader(ExpressionLoader.Kind kind) { this.kind = kind; }
            public override ExpressionLoader.Kind Kind() { return kind; }
            
            ExpressionLoader.Kind kind;
        }

        abstract class PointLoader : GeometryLoader<ExpressionDrawer.Point>
        {
            protected PointLoader() : base(ExpressionLoader.Kind.Point) { }

            public override void Load(Loaders loaders, Debugger debugger, string name, string type,
                                      out Geometry.Traits traits,
                                      out ExpressionDrawer.Point point)
            {
                traits = LoadTraits(type);
                point = traits != null
                      ? LoadPoint(debugger, name, type)
                      : null;
            }

            abstract public Geometry.Traits LoadTraits(string type);
            abstract public ExpressionDrawer.Point LoadPoint(Debugger debugger, string name, string type);
            abstract public MemoryReader.Converter GetMemoryConverter(Debugger debugger, string name, string type);

            protected ExpressionDrawer.Point LoadPoint(Debugger debugger, string name, string type, string ptrName, int count)
            {
                MemoryReader.Converter converter = GetMemoryConverter(debugger, name, type);
                if (converter.Count() != count)
                    throw new ArgumentOutOfRangeException("count");

                if (converter != null)
                {
                    double[] values = new double[count];
                    if (MemoryReader.ReadNumericArray(debugger, ptrName, null, values))
                    {
                        if (count > 1)
                            return new ExpressionDrawer.Point(values[0], values[1]);
                        else if (count > 0)
                            return new ExpressionDrawer.Point(values[0], 0);
                        else
                            return new ExpressionDrawer.Point(0, 0);
                    }
                }

                bool okx = true, oky = true;
                double x = 0, y = 0;
                if (count > 0)
                    x = LoadAsDouble(debugger, ptrName + "[0]", out okx);
                if (count > 1)
                    y = LoadAsDouble(debugger, ptrName + "[1]", out oky);
                return IsOk(okx, oky)
                     ? new ExpressionDrawer.Point(x, y)
                     : null;
            }
        }

        abstract class BoxLoader : GeometryLoader<ExpressionDrawer.Box>
        {
            protected BoxLoader() : base(ExpressionLoader.Kind.Box) { }
        }

        abstract class SegmentLoader : GeometryLoader<ExpressionDrawer.Segment>
        {
            protected SegmentLoader() : base(ExpressionLoader.Kind.Segment) { }
        }

        abstract class NSphereLoader : GeometryLoader<ExpressionDrawer.NSphere>
        {
            protected NSphereLoader() : base(ExpressionLoader.Kind.NSphere) { }
        }

        abstract class RangeLoader<ResultType> : GeometryLoader<ResultType>
            where ResultType : ExpressionDrawer.IDrawable
        {
            protected RangeLoader(ExpressionLoader.Kind kind) : base(kind) { }
        }

        abstract class PolygonLoader : GeometryLoader<ExpressionDrawer.Polygon>
        {
            protected PolygonLoader() : base(ExpressionLoader.Kind.Polygon) { }
        }

        abstract class ContainerLoader : LoaderR<ExpressionDrawer.ValuesContainer>
        {
            public override ExpressionLoader.Kind Kind() { return ExpressionLoader.Kind.Container; }            

            public class ElementsContainer
            {
                public ElementsContainer(ContainerLoader loader, string name, int size)
                {
                    this.loader = loader;
                    this.name = name;
                    this.size = size;
                }
                public IEnumerator<string> GetEnumerator()
                {
                    return loader.GetEnumerator(name, size);
                }
                ContainerLoader loader;
                string name;
                int size;
            }

            public ElementsContainer GetElementsContainer(string name, int size)
            {
                return new ElementsContainer(this, name, size);
            }

            public ElementsContainer GetElementsContainer(Debugger debugger, string name)
            {
                int size = LoadSize(debugger, name);
                return new ElementsContainer(this, name, size);
            }

            abstract public int LoadSize(Debugger debugger, string name);
            abstract public IEnumerator<string> GetEnumerator(string name, int size);

            abstract public string ElementPtrName(string name);
            abstract public MemoryReader.Converter GetMemoryConverter(Debugger debugger, string name, MemoryReader.Converter elementConverter, string elementType);

            protected MemoryReader.Converter GetContiguousMemoryConverter(Debugger debugger, string name, MemoryReader.Converter elementConverter, string elementType)
            {
                if (elementConverter == null)
                    return null;
                int size = LoadSize(debugger, name);
                int elemSizeof = MemoryReader.GetValueTypeSizeof(debugger, elementType);
                if (elemSizeof <= 0)
                    return null;
                return new MemoryReader.WrappingConverter(elementConverter, size, elemSizeof);
            }
        }

        class BGPoint : PointLoader
        {
            public override string Id() { return "boost::geometry::model::point"; }

            public override Geometry.Traits LoadTraits(string type)
            {
                List<string> tparams = Util.Tparams(type);
                if (tparams.Count >= 3)
                {
                    int dimension = ParseInt(tparams[1]);
                    Geometry.CoordinateSystem cs = Geometry.CoordinateSystem.Cartesian;
                    Geometry.Unit unit = Geometry.Unit.None;
                    ParseCSAndUnit(tparams[2], out cs, out unit);

                    return new Geometry.Traits(dimension, cs, unit);
                }

                return null;
            }

            public override ExpressionDrawer.Point LoadPoint(Debugger debugger, string name, string type)
            {
                List<string> tparams = Util.Tparams(type);
                if (tparams.Count < 2)
                    return null;
                int dimension = ParseInt(tparams[1]);
                int count = Math.Min(dimension, 2);
                return LoadPoint(debugger, name, type, name + ".m_values", count);
            }

            public override MemoryReader.Converter GetMemoryConverter(Debugger debugger, string name, string type)
            {
                List<string> tparams = Util.Tparams(type);
                if (tparams.Count < 2)
                    return null;
                string coordType = tparams[0];
                int dimension = ParseInt(tparams[1]);
                int count = Math.Min(dimension, 2);
                // Just in case, offset should be 0
                long byteOffset = MemoryReader.GetAddressDifference(debugger, name, name + ".m_values");
                return MemoryReader.GetNumericConverter(debugger, name + ".m_values", coordType, count, (int)byteOffset);
            }

            protected void ParseCSAndUnit(string cs_type, out Geometry.CoordinateSystem cs, out Geometry.Unit unit)
            {
                cs = Geometry.CoordinateSystem.Cartesian;
                unit = Geometry.Unit.None;

                if (cs_type == "boost::geometry::cs::cartesian")
                {
                    return;
                }

                string cs_base_type = Util.BaseType(cs_type);
                if (cs_base_type == "boost::geometry::cs::spherical")
                    cs = Geometry.CoordinateSystem.SphericalPolar;
                else if (cs_base_type == "boost::geometry::cs::spherical_equatorial")
                    cs = Geometry.CoordinateSystem.SphericalEquatorial;
                else if (cs_base_type == "boost::geometry::cs::geographic")
                    cs = Geometry.CoordinateSystem.Geographic;

                List<string> cs_tparams = Util.Tparams(cs_type);
                if (cs_tparams.Count >= 1)
                {
                    string u = cs_tparams[0];
                    if (u == "boost::geometry::radian")
                        unit = Geometry.Unit.Radian;
                    else if (u == "boost::geometry::degree")
                        unit = Geometry.Unit.Degree;
                }
            }
        }

        class BGPointXY : BGPoint
        {
            public override string Id() { return "boost::geometry::model::d2::point_xy"; }

            public override Geometry.Traits LoadTraits(string type)
            {
                List<string> tparams = Util.Tparams(type);
                if (tparams.Count >= 2)
                {
                    Geometry.CoordinateSystem cs = Geometry.CoordinateSystem.Cartesian;
                    Geometry.Unit unit = Geometry.Unit.None;
                    ParseCSAndUnit(tparams[1], out cs, out unit);

                    return new Geometry.Traits(2, cs, unit);
                }

                return null;
            }

            public override ExpressionDrawer.Point LoadPoint(Debugger debugger, string name, string type)
            {
                return LoadPoint(debugger, name, type, name + ".m_values", 2);
            }

            public override MemoryReader.Converter GetMemoryConverter(Debugger debugger, string name, string type)
            {
                List<string> tparams = Util.Tparams(type);
                if (tparams.Count < 1)
                    return null;
                string coordType = tparams[0];
                // Just in case, offset should be 0
                long byteOffset = MemoryReader.GetAddressDifference(debugger, name, name + ".m_values");
                return MemoryReader.GetNumericConverter(debugger, name + ".m_values", coordType, 2, (int)byteOffset);
            }
        }

        class BGBox : BoxLoader
        {
            public override string Id() { return "boost::geometry::model::box"; }

            public override void Load(Loaders loaders, Debugger debugger, string name, string type,
                                      out Geometry.Traits traits,
                                      out ExpressionDrawer.Box result)
            {
                traits = null;
                result = null;

                List<string> tparams = Util.Tparams(type);
                if (tparams.Count < 1)
                    return;

                string pointType = tparams[0];
                PointLoader pointLoader = (PointLoader)loaders.FindByType(ExpressionLoader.Kind.Point, pointType);
                if (pointLoader == null)
                    return;

                traits = pointLoader.LoadTraits(pointType);
                if (traits == null)
                    return;

                Geometry.Point fp = pointLoader.LoadPoint(debugger, name + ".m_min_corner", pointType);
                Geometry.Point sp = pointLoader.LoadPoint(debugger, name + ".m_max_corner", pointType);

                result = IsOk(fp, sp)
                       ? new ExpressionDrawer.Box(fp, sp)
                       : null;
            }
        }

        class BGSegment : SegmentLoader
        {
            public BGSegment()
                : this("boost::geometry::model::segment")
            { }

            protected BGSegment(string id)
            {
                this.id = id;
            }

            public override string Id() { return id; }

            public override void Load(Loaders loaders, Debugger debugger, string name, string type,
                                      out Geometry.Traits traits,
                                      out ExpressionDrawer.Segment segment)
            {
                traits = null;
                segment = null;

                List<string> tparams = Util.Tparams(type);
                if (tparams.Count < 1)
                    return;

                string pointType = tparams[0];
                PointLoader pointLoader = (PointLoader)loaders.FindByType(ExpressionLoader.Kind.Point, pointType);
                if (pointLoader == null)
                    return;

                traits = pointLoader.LoadTraits(pointType);
                if (traits == null)
                    return;

                Geometry.Point fp = pointLoader.LoadPoint(debugger, name + ".first", pointType);
                Geometry.Point sp = pointLoader.LoadPoint(debugger, name + ".second", pointType);

                segment = IsOk(fp, sp)
                        ? new ExpressionDrawer.Segment(fp, sp)
                        : null;
            }

            string id;
        }

        class BGReferringSegment : BGSegment
        {
            public BGReferringSegment()
                : base("boost::geometry::model::referring_segment")
            { }
        }

        class BGNSphere : NSphereLoader
        {
            public override string Id() { return "boost::geometry::model::nsphere"; }

            public override void Load(Loaders loaders, Debugger debugger, string name, string type,
                                      out Geometry.Traits traits,
                                      out ExpressionDrawer.NSphere result)
            {
                traits = null;
                result = null;

                List<string> tparams = Util.Tparams(type);
                if (tparams.Count < 1)
                    return;

                string pointType = tparams[0];
                PointLoader pointLoader = (PointLoader)loaders.FindByType(ExpressionLoader.Kind.Point, pointType);
                if (pointLoader == null)
                    return;

                traits = pointLoader.LoadTraits(pointType);
                if (traits == null)
                    return;

                Geometry.Point center = pointLoader.LoadPoint(debugger, name + ".m_center", pointType);
                bool ok = false;
                double radius = LoadAsDouble(debugger, name + ".m_radius", out ok);

                result = IsOk(center, ok)
                       ? new ExpressionDrawer.NSphere(center, radius)
                       : null;
            }
        }

        class BGRange<ResultType> : RangeLoader<ResultType>
            where ResultType : ExpressionDrawer.IDrawable
                             , Geometry.IContainer<Geometry.Point>
                             , new()
        {
            protected BGRange(ExpressionLoader.Kind kind, string id, int pointTIndex = 0, int containerTIndex = 1)
                : base(kind)
            {
                this.id = id;
                this.pointTIndex = pointTIndex;
                this.containerTIndex = containerTIndex;
            }

            public override string Id() { return id; }

            public override void Load(Loaders loaders, Debugger debugger, string name, string type,
                                      out Geometry.Traits traits,
                                      out ResultType result)
            {
                traits = null;
                result = default(ResultType);

                List<string> tparams = Util.Tparams(type);
                if (tparams.Count <= Math.Max(pointTIndex, containerTIndex))
                    return;

                string pointType = tparams[pointTIndex];
                PointLoader pointLoader = (PointLoader)loaders.FindByType(ExpressionLoader.Kind.Point, pointType);
                if (pointLoader == null)
                    return;

                traits = pointLoader.LoadTraits(pointType);
                if (traits == null)
                    return;

                string containerType = tparams[containerTIndex];
                ContainerLoader containerLoader = (ContainerLoader)loaders.FindByType(ExpressionLoader.Kind.Container, containerType);
                if (containerLoader == null)
                    return;

                int size = containerLoader.LoadSize(debugger, name);

                // Try loading using direct memory access
                string pointPtrName = containerLoader.ElementPtrName(name);
                if (pointPtrName != null)
                {
                    string pointName = "*((" + pointType + "*)" + pointPtrName + ")";
                    MemoryReader.Converter pointConverter = pointLoader.GetMemoryConverter(debugger, pointName, pointType);
                    MemoryReader.Converter containerConverter = containerLoader.GetMemoryConverter(debugger, name, pointConverter, pointType);
                    if (pointConverter != null && containerConverter != null)
                    {
                        double[] values = new double[containerConverter.ValueCount()];
                        if (MemoryReader.Read(debugger, pointPtrName, values, containerConverter))
                        {
                            int dimension = pointConverter.Count();
                            if (values.Length == size * dimension)
                            {
                                result = new ResultType();
                                for (int i = 0; i < size; ++i)
                                {
                                    double x = dimension > 0 ? values[i * dimension] : 0;
                                    double y = dimension > 1 ? values[i * dimension + 1] : 0;
                                    ExpressionDrawer.Point p = new ExpressionDrawer.Point(x, y);
                                    result.Add(p);
                                }
                                return;
                            }
                        }
                    }
                }

                // Try loading using value strings parsing
                result = new ResultType();                
                foreach (string n in containerLoader.GetElementsContainer(name, size))
                {
                    ExpressionDrawer.Point p = pointLoader.LoadPoint(debugger, n, pointType);
                    if (p == null)
                    {
                        result = default(ResultType);
                        return;
                    }
                    result.Add(p);
                }
            }

            string id;
            int pointTIndex;
            int containerTIndex;
        }

        class BGLinestring : BGRange<ExpressionDrawer.Linestring>
        {
            public BGLinestring()
                : base(ExpressionLoader.Kind.Linestring, "boost::geometry::model::linestring", 0, 1)
            { }
        }

        class BGMultiLinestring : RangeLoader<ExpressionDrawer.MultiLinestring>
        {
            public BGMultiLinestring() : base(ExpressionLoader.Kind.MultiLinestring) { }

            public override string Id() { return "boost::geometry::model::multi_linestring"; }

            public override void Load(Loaders loaders, Debugger debugger, string name, string type,
                                      out Geometry.Traits traits,
                                      out ExpressionDrawer.MultiLinestring result)
            {
                traits = null;
                result = null;

                List<string> tparams = Util.Tparams(type);
                if (tparams.Count < 2)
                    return;

                string lsType = tparams[0];
                RangeLoader<ExpressionDrawer.Linestring> lsLoader
                    = (RangeLoader<ExpressionDrawer.Linestring>)loaders.FindByType(ExpressionLoader.Kind.Linestring, lsType);
                if (lsLoader == null)
                    return;

                string containerType = tparams[1];
                ContainerLoader containerLoader = (ContainerLoader)loaders.FindByType(ExpressionLoader.Kind.Container, containerType);
                if (containerLoader == null)
                    return;

                // TODO: Calculate point traits only once, now it's calculated for each linestring

                result = new ExpressionDrawer.MultiLinestring();

                int size = containerLoader.LoadSize(debugger, name);
                foreach (string n in containerLoader.GetElementsContainer(name, size))
                {
                    ExpressionDrawer.Linestring ls = null;
                    lsLoader.Load(loaders, debugger, n, lsType, out traits, out ls);
                    if (ls == null)
                    {
                        result = null;
                        return;
                    }
                    result.Add(ls);
                }
            }
        }

        class BGRing : BGRange<ExpressionDrawer.Ring>
        {
            public BGRing()
                : base(ExpressionLoader.Kind.Ring, "boost::geometry::model::ring", 0, 3)
            { }
        }

        class BGMultiPoint : BGRange<ExpressionDrawer.MultiPoint>
        {
            public BGMultiPoint()
                : base(ExpressionLoader.Kind.MultiPoint, "boost::geometry::model::multi_point", 0, 1)
            { }
        }

        class BGPolygon : PolygonLoader
        {
            public override string Id() { return "boost::geometry::model::polygon"; }

            public override void Load(Loaders loaders, Debugger debugger, string name, string type,
                                      out Geometry.Traits traits,
                                      out ExpressionDrawer.Polygon result)
            {
                traits = null;
                result = null;

                string outerName = name + ".m_outer";
                string innersName = name + ".m_inners";

                Expression outerExpr = debugger.GetExpression(outerName);
                Expression innersExpr = debugger.GetExpression(innersName);

                string outerType = outerExpr.Type;
                BGRing outerLoader = (BGRing)loaders.FindByType(ExpressionLoader.Kind.Ring, outerType);
                if (outerLoader == null)
                    return;

                string innersType = innersExpr.Type;
                ContainerLoader innersLoader = (ContainerLoader)loaders.FindByType(ExpressionLoader.Kind.Container, innersType);
                if (innersLoader == null)
                    return;

                ExpressionDrawer.Ring outer = null;
                outerLoader.Load(loaders, debugger, outerName, outerExpr.Type, out traits, out outer);
                if (outer == null)
                    return;

                List<Geometry.Ring> inners = new List<Geometry.Ring>();

                int innersCount = innersLoader.LoadSize(debugger, innersName);
                foreach (string n in innersLoader.GetElementsContainer(innersName, innersCount))
                {
                    ExpressionDrawer.Ring inner = null;
                    // TODO: Do this differently
                    // and traits are not needed here
                    outerLoader.Load(loaders, debugger, n, outerExpr.Type, out traits, out inner);
                    if (inner == null)
                        return;
                    inners.Add(inner);
                }

                result = new ExpressionDrawer.Polygon(outer, inners);
            }
        }

        class BGMultiPolygon : RangeLoader<ExpressionDrawer.MultiPolygon>
        {
            public BGMultiPolygon() : base(ExpressionLoader.Kind.MultiPolygon) { }

            public override string Id() { return "boost::geometry::model::multi_polygon"; }

            public override void Load(Loaders loaders, Debugger debugger, string name, string type,
                                      out Geometry.Traits traits,
                                      out ExpressionDrawer.MultiPolygon result)
            {
                traits = null;
                result = null;

                List<string> tparams = Util.Tparams(type);
                if (tparams.Count < 2)
                    return;

                string polyType = tparams[0];
                PolygonLoader polyLoader = (PolygonLoader)loaders.FindByType(ExpressionLoader.Kind.Polygon, polyType);
                if (polyLoader == null)
                    return;

                string containerType = tparams[1];
                ContainerLoader containerLoader = (ContainerLoader)loaders.FindByType(ExpressionLoader.Kind.Container, containerType);
                if (containerLoader == null)
                    return;

                // TODO: Calculate point traits only once, now it's calculated for each linestring

                result = new ExpressionDrawer.MultiPolygon();

                int size = containerLoader.LoadSize(debugger, name);
                foreach (string n in containerLoader.GetElementsContainer(name, size))
                {
                    ExpressionDrawer.Polygon poly = null;
                    polyLoader.Load(loaders, debugger, n, polyType, out traits, out poly);
                    if (poly == null)
                    {
                        result = null;
                        return;
                    }
                    result.Add(poly);
                }
            }
        }

        class BGBufferedRing : RangeLoader<ExpressionDrawer.Ring>
        {
            public BGBufferedRing() : base(ExpressionLoader.Kind.Ring) { }

            public override string Id() { return "boost::geometry::detail::buffer::buffered_ring"; }

            public override void Load(Loaders loaders, Debugger debugger, string name, string type,
                                      out Geometry.Traits traits,
                                      out ExpressionDrawer.Ring result)
            {
                traits = null;
                result = null;

                List<string> tparams = Util.Tparams(type);
                if (tparams.Count < 1)
                    return;

                string ringType = tparams[0];
                RangeLoader<ExpressionDrawer.Ring> ringLoader = (RangeLoader<ExpressionDrawer.Ring>)loaders.FindByType(ExpressionLoader.Kind.Ring, ringType);
                if (ringLoader == null)
                    return;

                ringLoader.Load(loaders, debugger, name, ringType, out traits, out result);
            }
        }

        // NOTE: There is no MultiRing concept so use MultiPolygon for now
        class BGBufferedRingCollection : RangeLoader<ExpressionDrawer.MultiPolygon>
        {
            public BGBufferedRingCollection() : base(ExpressionLoader.Kind.MultiPolygon) { }

            public override string Id() { return "boost::geometry::detail::buffer::buffered_ring_collection"; }

            public override void Load(Loaders loaders, Debugger debugger, string name, string type,
                                      out Geometry.Traits traits,
                                      out ExpressionDrawer.MultiPolygon result)
            {
                traits = null;
                result = null;

                List<string> tparams = Util.Tparams(type);
                if (tparams.Count < 1)
                    return;

                string ringType = tparams[0];
                RangeLoader<ExpressionDrawer.Ring> ringLoader = (RangeLoader<ExpressionDrawer.Ring>)loaders.FindByType(ExpressionLoader.Kind.Ring, ringType);
                if (ringLoader == null)
                    return;

                ContainerLoader containerLoader = (ContainerLoader)loaders.FindById(ExpressionLoader.Kind.Container, "std::vector");
                if (containerLoader == null)
                    return;

                result = new ExpressionDrawer.MultiPolygon();

                int size = containerLoader.LoadSize(debugger, name);
                foreach (string n in containerLoader.GetElementsContainer(name, size))
                {
                    ExpressionDrawer.Ring ring = null;
                    ringLoader.Load(loaders, debugger, n, ringType, out traits, out ring);
                    if (ring == null)
                    {
                        result = null;
                        return;
                    }
                    result.Add(new ExpressionDrawer.Polygon(ring));
                }
            }
        }

        class BPPoint : PointLoader
        {
            public override string Id() { return "boost::polygon::point_data"; }

            public override Geometry.Traits LoadTraits(string type)
            {
                return new Geometry.Traits(2, Geometry.CoordinateSystem.Cartesian, Geometry.Unit.None);
            }

            public override ExpressionDrawer.Point LoadPoint(Debugger debugger, string name, string type)
            {
                return LoadPoint(debugger, name, type, name + ".coords_", 2);
            }

            public override MemoryReader.Converter GetMemoryConverter(Debugger debugger, string name, string type)
            {
                List<string> tparams = Util.Tparams(type);
                if (tparams.Count < 1)
                    return null;
                string coordType = tparams[0];
                // Just in case, offset should be 0
                long byteOffset = MemoryReader.GetAddressDifference(debugger, name, name + ".coords_");
                return MemoryReader.GetNumericConverter(debugger, name + ".coords_", coordType, 2, (int)byteOffset);
            }
        }

        class BPSegment : SegmentLoader
        {
            public override string Id() { return "boost::polygon::segment_data"; }

            public override void Load(Loaders loaders, Debugger debugger, string name, string type,
                                      out Geometry.Traits traits,
                                      out ExpressionDrawer.Segment result)
            {
                traits = new Geometry.Traits(2, Geometry.CoordinateSystem.Cartesian, Geometry.Unit.None);
                result = null;

                bool okx0 = false, oky0 = false, okx1 = false, oky1 = false;
                double x0 = LoadAsDouble(debugger, name + ".points_[0].coords_[0]", out okx0);
                double y0 = LoadAsDouble(debugger, name + ".points_[0].coords_[1]", out oky0);
                double x1 = LoadAsDouble(debugger, name + ".points_[1].coords_[0]", out okx1);
                double y1 = LoadAsDouble(debugger, name + ".points_[1].coords_[1]", out oky1);

                if (!IsOk(okx0, oky0, okx1, oky1))
                    return;

                Geometry.Point first_p = new Geometry.Point(x0, y0);
                Geometry.Point second_p = new Geometry.Point(x1, y1);

                result = new ExpressionDrawer.Segment(first_p, second_p);
            }
        }

        class BPBox : BoxLoader
        {
            public override string Id() { return "boost::polygon::rectangle_data"; }

            public override void Load(Loaders loaders, Debugger debugger, string name, string type,
                                      out Geometry.Traits traits,
                                      out ExpressionDrawer.Box result)
            {
                traits = new Geometry.Traits(2, Geometry.CoordinateSystem.Cartesian, Geometry.Unit.None);
                result = null;

                bool okxl = false, okxh = false, okyl = false, okyh = false;
                double xl = LoadAsDouble(debugger, name + ".ranges_[0].coords_[0]", out okxl);
                double xh = LoadAsDouble(debugger, name + ".ranges_[0].coords_[1]", out okxh);
                double yl = LoadAsDouble(debugger, name + ".ranges_[1].coords_[0]", out okyl);
                double yh = LoadAsDouble(debugger, name + ".ranges_[1].coords_[1]", out okyh);

                if (!IsOk(okxl, okxh, okyl, okyh))
                    return;

                Geometry.Point first_p = new Geometry.Point(xl, yl);
                Geometry.Point second_p = new Geometry.Point(xh, yh);

                result = new ExpressionDrawer.Box(first_p, second_p);
            }
        }

        class BPRing : RangeLoader<ExpressionDrawer.Ring>
        {
            public BPRing() : base(ExpressionLoader.Kind.Ring) { }

            public override string Id() { return "boost::polygon::polygon_data"; }

            public override void Load(Loaders loaders, Debugger debugger, string name, string type,
                                      out Geometry.Traits traits,
                                      out ExpressionDrawer.Ring result)
            {
                traits = new Geometry.Traits(2, Geometry.CoordinateSystem.Cartesian, Geometry.Unit.None);
                result = null;

                BPPoint pointLoader = (BPPoint)loaders.FindById(ExpressionLoader.Kind.Point, "boost::polygon::point_data");
                if (pointLoader == null)
                    return;

                ContainerLoader containerLoader = (ContainerLoader)loaders.FindById(ExpressionLoader.Kind.Container, "std::vector");
                if (containerLoader == null)
                    return;

                List<string> tparams = Util.Tparams(type);
                if (tparams.Count < 1)
                    return;
                // TODO: Space before > only if T has > at the end?
                string pointType = "boost::polygon::point_data<" + tparams[0] + " >";

                result = new ExpressionDrawer.Ring();

                int size = containerLoader.LoadSize(debugger, name + ".coords_");
                foreach (string n in containerLoader.GetElementsContainer(name + ".coords_", size))
                {
                    ExpressionDrawer.Point p = pointLoader.LoadPoint(debugger, n, pointType);
                    if (p == null)
                    {
                        result = null;
                        return;
                    }
                    result.Add(p);
                }
            }
        }

        class BPPolygon : PolygonLoader
        {
            public override string Id() { return "boost::polygon::polygon_with_holes_data"; }

            public override void Load(Loaders loaders, Debugger debugger, string name, string type,
                                      out Geometry.Traits traits,
                                      out ExpressionDrawer.Polygon result)
            {
                traits = new Geometry.Traits(2, Geometry.CoordinateSystem.Cartesian, Geometry.Unit.None);
                result = null;

                PointLoader pointLoader = (PointLoader)loaders.FindById(ExpressionLoader.Kind.Point, "boost::polygon::point_data");
                if (pointLoader == null)
                    return;

                BPRing outerLoader = (BPRing)loaders.FindById(ExpressionLoader.Kind.Ring, "boost::polygon::polygon_data");
                if (outerLoader == null)
                    return;

                ContainerLoader holesLoader = (ContainerLoader)loaders.FindById(ExpressionLoader.Kind.Container, "std::list");
                if (holesLoader == null)
                    return;

                ExpressionDrawer.Ring outer = null;
                outerLoader.Load(loaders, debugger, name + ".self_", "boost::polygon::polygon_data<T>", out traits, out outer);
                if (outer == null)
                    return;

                // TODO: avoid loading traits

                int size = holesLoader.LoadSize(debugger, name + ".holes_");
                List<Geometry.Ring> holes = new List<Geometry.Ring>();
                foreach (string n in holesLoader.GetElementsContainer(name + ".holes_", size))
                {
                    ExpressionDrawer.Ring hole = null;
                    outerLoader.Load(loaders, debugger, n, "boost::polygon::polygon_data<T>", out traits, out hole);
                    if (hole == null)
                        return;
                    holes.Add(hole);
                }

                result = new ExpressionDrawer.Polygon(outer, holes);
            }
        }

        class RandomAccessEnumerator : IEnumerator<string>
        {
            public RandomAccessEnumerator(string name, int size)
            {
                this.name = name;
                this.index = -1;
                this.size = size;
            }

            public void Dispose() { }

            object IEnumerator.Current { get { return CurrentString; } }
            public string Current { get { return CurrentString; } }

            private string CurrentString
            {
                get
                {
                    if (index < 0 || index >= size)
                        throw new InvalidOperationException();

                    return name + "[" + index + "]";
                }
            }

            public bool MoveNext() { return ++index < size; }
            public void Reset() { index = -1; }
            public int Count { get { return size; } }

            private string name;
            private int index;
            private int size;
        }

        class StdListEnumerator : IEnumerator<string>
        {
            public StdListEnumerator(string name, int size)
            {
                this.name = name;
                this.index = -1;
                this.size = size;
                this.nextNode = name + "._Mypair._Myval2._Myhead";
            }

            public void Dispose() { }

            object IEnumerator.Current { get { return CurrentString; } }
            public string Current { get { return CurrentString; } }

            private string CurrentString
            {
                get
                {
                    if (index < 0 || index >= size)
                        throw new InvalidOperationException();

                    return nextNode + "->_Myval";
                }
            }

            public bool MoveNext()
            {
                nextNode = nextNode + "->_Next";
                return ++index < size;
            }

            public void Reset()
            {
                index = -1;
                if (nextNode != null)
                    nextNode = name + "._Mypair._Myval2._Myhead";
            }

            public int Count { get { return size; } }

            private string name;
            private int index;
            private int size;
            private string nextNode;
        }

        abstract class ValuesContainer : ContainerLoader
        {
            public override void Load(Loaders loaders, Debugger debugger, string name, string type, out Geometry.Traits traits, out ExpressionDrawer.ValuesContainer result)
            {
                traits = null;
                LoadGeneric(debugger, name, out result);
            }

            protected void LoadContiguous(Debugger debugger, string name, string ptrName, string valType, out ExpressionDrawer.ValuesContainer result)
            {
                // TODO: Move this option checking outside, get it once for all loaded drawables and pass here
                GeneralOptionPage optionPage = Util.GetDialogPage<GeneralOptionPage>();
                bool enableDirectMemoryAccess = true;
                if (optionPage != null)
                {
                    enableDirectMemoryAccess = optionPage.EnableDirectMemoryAccess;
                }

                result = null;
                if (enableDirectMemoryAccess)
                {
                    int size = this.LoadSize(debugger, name);
                    double[] values = new double[size];
                    if (MemoryReader.ReadNumericArray(debugger, ptrName, valType, values))
                    {
                        List<double> list = new List<double>(size);
                        for (int i = 0; i < values.Length; ++i)
                            list.Add(values[i]);
                        result = new ExpressionDrawer.ValuesContainer(list);
                    }
                }

                if (result == null)
                    LoadGeneric(debugger, name, out result);
            }

            protected void LoadGeneric(Debugger debugger, string name, out ExpressionDrawer.ValuesContainer result)
            {                
                result = null;
                int size = this.LoadSize(debugger, name);
                List<double> values = new List<double>();
                foreach (string n in this.GetElementsContainer(debugger, name))
                {
                    bool ok = false;
                    double value = LoadAsDouble(debugger, n, out ok);
                    if (ok == false)
                        return;
                    values.Add(value);
                }
                result = new ExpressionDrawer.ValuesContainer(values);
            }
        }

        class StdArray : ValuesContainer
        {
            public override string Id() { return "std::array"; }

            public override void Load(Loaders loaders, Debugger debugger, string name, string type, out Geometry.Traits traits, out ExpressionDrawer.ValuesContainer result)
            {
                traits = null;
                LoadContiguous(debugger, name, name + "._Elems", null, out result);
            }

            public override string ElementPtrName(string name)
            {
                return name + "._Elems";
            }

            public override MemoryReader.Converter GetMemoryConverter(Debugger debugger, string name, MemoryReader.Converter elementConverter, string elementType)
            {
                return GetContiguousMemoryConverter(debugger, name, elementConverter, elementType);
            }

            public override int LoadSize(Debugger debugger, string name)
            {
                Expression expr = debugger.GetExpression(name);
                return expr.IsValidValue
                     ? Math.Max(ParseInt(Util.Tparams(expr.Type)[1]), 0)
                     : 0;
            }

            public override IEnumerator<string> GetEnumerator(string name, int size)
            {
                return new RandomAccessEnumerator(name, size);
            }
        }

        class BoostArray : StdArray
        {
            public override string Id() { return "boost::array"; }

            public override void Load(Loaders loaders, Debugger debugger, string name, string type, out Geometry.Traits traits, out ExpressionDrawer.ValuesContainer result)
            {
                traits = null;
                LoadContiguous(debugger, name, name + ".elems", null, out result);
            }

            public override string ElementPtrName(string name)
            {
                return name + ".elems";
            }

            public override MemoryReader.Converter GetMemoryConverter(Debugger debugger, string name, MemoryReader.Converter elementConverter, string elementType)
            {
                return GetContiguousMemoryConverter(debugger, name, elementConverter, elementType);
            }
        }

        class BoostContainerVector : ValuesContainer
        {
            public override string Id() { return "boost::container::vector"; }

            public override void Load(Loaders loaders, Debugger debugger, string name, string type, out Geometry.Traits traits, out ExpressionDrawer.ValuesContainer result)
            {
                traits = null;
                LoadContiguous(debugger, name, name + ".m_holder.m_start", null, out result);
            }

            public override string ElementPtrName(string name)
            {
                return name + ".m_holder.m_start";
            }

            public override MemoryReader.Converter GetMemoryConverter(Debugger debugger, string name, MemoryReader.Converter elementConverter, string elementType)
            {
                return GetContiguousMemoryConverter(debugger, name, elementConverter, elementType);
            }

            public override int LoadSize(Debugger debugger, string name)
            {
                Expression expr = debugger.GetExpression(name + ".m_holder.m_size");
                return expr.IsValidValue
                     ? Math.Max(ParseInt(expr.Value), 0)
                     : 0;
            }

            public override IEnumerator<string> GetEnumerator(string name, int size)
            {
                return new RandomAccessEnumerator(name, size);
            }
        }

        class BoostContainerStaticVector : BoostContainerVector
        {
            public override string Id() { return "boost::container::static_vector"; }

            public override void Load(Loaders loaders, Debugger debugger, string name, string type, out Geometry.Traits traits, out ExpressionDrawer.ValuesContainer result)
            {
                traits = null;
                string valueType = Util.Tparams(type)[0];
                LoadContiguous(debugger, name, name + ".m_holder.storage.data", valueType, out result);
            }

            public override string ElementPtrName(string name)
            {
                return name + ".m_holder.storage.data";
            }

            public override MemoryReader.Converter GetMemoryConverter(Debugger debugger, string name, MemoryReader.Converter elementConverter, string elementType)
            {
                return GetContiguousMemoryConverter(debugger, name, elementConverter, elementType);
            }
        }

        class BGVarray : ValuesContainer
        {
            public override string Id() { return "boost::geometry::index::detail::varray"; }

            public override void Load(Loaders loaders, Debugger debugger, string name, string type, out Geometry.Traits traits, out ExpressionDrawer.ValuesContainer result)
            {
                traits = null;
                string valueType = Util.Tparams(type)[0];
                LoadContiguous(debugger, name, name + ".m_storage.data_.buf", valueType, out result);
            }

            public override string ElementPtrName(string name)
            {
                return name + ".m_storage.data_.buf";
            }

            public override MemoryReader.Converter GetMemoryConverter(Debugger debugger, string name, MemoryReader.Converter elementConverter, string elementType)
            {
                return GetContiguousMemoryConverter(debugger, name, elementConverter, elementType);
            }

            public override int LoadSize(Debugger debugger, string name)
            {
                Expression expr = debugger.GetExpression(name + ".m_size");
                return expr.IsValidValue
                     ? Math.Max(ParseInt(expr.Value), 0)
                     : 0;
            }

            public override IEnumerator<string> GetEnumerator(string name, int size)
            {
                return new RandomAccessEnumerator(name, size);
            }
        }

        class StdVector : ValuesContainer
        {
            public override string Id() { return "std::vector"; }

            public override void Load(Loaders loaders, Debugger debugger, string name, string type, out Geometry.Traits traits, out ExpressionDrawer.ValuesContainer result)
            {
                traits = null;
                LoadContiguous(debugger, name, name + "._Mypair._Myval2._Myfirst", null, out result);
            }

            public override string ElementPtrName(string name)
            {
                return name + "._Mypair._Myval2._Myfirst";
            }

            public override MemoryReader.Converter GetMemoryConverter(Debugger debugger, string name, MemoryReader.Converter elementConverter, string elementType)
            {
                return GetContiguousMemoryConverter(debugger, name, elementConverter, elementType);
            }

            public override int LoadSize(Debugger debugger, string name)
            {
                Expression expr = debugger.GetExpression(name + "._Mypair._Myval2._Mylast-" + name + "._Mypair._Myval2._Myfirst");
                return expr.IsValidValue
                     ? Math.Max(ParseInt(expr.Value), 0)
                     : 0;
            }

            public override IEnumerator<string> GetEnumerator(string name, int size)
            {
                return new RandomAccessEnumerator(name, size);
            }
        }

        class StdDeque : ValuesContainer
        {
            public override string Id() { return "std::deque"; }

            public override string ElementPtrName(string name)
            {
                return null;
            }

            public override MemoryReader.Converter GetMemoryConverter(Debugger debugger, string name, MemoryReader.Converter elementConverter, string elementType)
            {
                return null;
            }

            public override int LoadSize(Debugger debugger, string name)
            {
                Expression expr = debugger.GetExpression(name + "._Mypair._Myval2._Mysize");
                return expr.IsValidValue
                     ? Math.Max(ParseInt(expr.Value), 0)
                     : 0;
            }

            public override IEnumerator<string> GetEnumerator(string name, int size)
            {
                return new RandomAccessEnumerator(name, size);
            }
        }

        class StdList : ValuesContainer
        {
            public override string Id() { return "std::list"; }

            public override string ElementPtrName(string name)
            {
                return null;
            }

            public override MemoryReader.Converter GetMemoryConverter(Debugger debugger, string name, MemoryReader.Converter elementConverter, string elementType)
            {
                return null;
            }

            public override int LoadSize(Debugger debugger, string name)
            {
                Expression expr = debugger.GetExpression(name + "._Mypair._Myval2._Mysize");
                return expr.IsValidValue
                     ? Math.Max(ParseInt(expr.Value), 0)
                     : 0;
            }

            public override IEnumerator<string> GetEnumerator(string name, int size)
            {
                return new StdListEnumerator(name, size);
            }
        }

        class BVariant : Loader
        {
            public override string Id() { return "boost::variant"; }
            public override ExpressionLoader.Kind Kind() { return ExpressionLoader.Kind.Variant; }

            public override void Load(Loaders loaders, Debugger debugger, string name, string type,
                                      out Geometry.Traits traits,
                                      out ExpressionDrawer.IDrawable result)
            {
                traits = null;
                result = null;

                Expression whichExpr = debugger.GetExpression(name + ".which_");
                if (!whichExpr.IsValidValue)
                    return;

                int which = ParseInt(whichExpr.Value);
                List<string> tparams = Util.Tparams(type);
                if (which < 0 || tparams.Count <= which)
                    return;

                string storedType = tparams[which];
                string storedName = "(*(" + storedType + "*)" + name + ".storage_.data_.buf)";

                Loader loader = loaders.FindByType(storedType);
                loader.Load(loaders, debugger, storedName, storedType, out traits, out result);
            }
        }

        class BGTurn : GeometryLoader<ExpressionDrawer.Turn>
        {
            public BGTurn(string id)
                : base(ExpressionLoader.Kind.Turn)
            {
                this.id = id;
            }

            public override string Id() { return id; }

            public override void Load(Loaders loaders, Debugger debugger, string name, string type,
                                      out Geometry.Traits traits,
                                      out ExpressionDrawer.Turn result)
            {
                traits = null;
                result = null;

                List<string> tparams = Util.Tparams(type);
                if (tparams.Count < 1)
                    return;

                string pointType = tparams[0];
                PointLoader pointLoader = (PointLoader)loaders.FindByType(ExpressionLoader.Kind.Point, pointType);
                if (pointLoader == null)
                    return;

                ExpressionDrawer.Point p = null;
                pointLoader.Load(loaders, debugger, name + ".point", pointType, out traits, out p);
                if (p == null)
                    return;

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

                result = new ExpressionDrawer.Turn(p, method, op0, op1);
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

            string id;
        }

        class BGTurnContainer : GeometryLoader<ExpressionDrawer.TurnsContainer>
        {
            public BGTurnContainer(string id)
                : base(ExpressionLoader.Kind.TurnsContainer)
            {
                this.id = id;
            }

            public override string Id() { return id; }
            public override TypeConstraint Constraint() { return new TparamKindConstraint(0, ExpressionLoader.Kind.Turn); }

            public override void Load(Loaders loaders, Debugger debugger, string name, string type,
                                      out Geometry.Traits traits,
                                      out ExpressionDrawer.TurnsContainer result)
            {
                traits = null;
                result = null;

                ContainerLoader containerLoader = (ContainerLoader)loaders.FindByType(ExpressionLoader.Kind.Container, type);
                if (containerLoader == null)
                    return;

                List<string> tparams = Util.Tparams(type);
                if (tparams.Count < 1)
                    return;

                // TODO: Get element type from ContainerLoader instead?
                string turnType = tparams[0];
                BGTurn turnLoader = (BGTurn)loaders.FindByType(ExpressionLoader.Kind.Turn, turnType);
                if (turnLoader == null)
                    return;

                List<ExpressionDrawer.Turn> turns = new List<ExpressionDrawer.Turn>();

                int size = containerLoader.LoadSize(debugger, name);                
                foreach (string n in containerLoader.GetElementsContainer(name, size))
                {
                    ExpressionDrawer.Turn t = null;
                    turnLoader.Load(loaders, debugger, n, turnType, out traits, out t);
                    if (t == null)
                        return;
                    turns.Add(t);
                }

                result = new ExpressionDrawer.TurnsContainer(turns);
            }

            string id;
        }

        Loaders loaders = new Loaders();
    }
}
