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
using System.Globalization;
using System.Threading;
using Task = System.Threading.Tasks.Task;

// TODO: Experiment with loading in DispatcherTimer

namespace GraphicalDebugging
{
    class ExpressionLoader
    {
        private DTE2 dte;
        private Debugger debugger;
        private DebuggerEvents debuggerEvents;
        private Loaders loaders;

        // Because containers of points (including c-arrays) are treated as MultiPoints
        //   they have to be searched first (Loaders class traverses the list of kinds),
        //   so put this kind at the beginning and the rest of containers too
        public enum Kind
        {
            MultiPoint = 0, Container, TurnsContainer,
            Point, Segment, Box, NSphere, Linestring, Ring, Polygon, MultiLinestring, MultiPolygon, Turn,
            Variant
        };

        private static ExpressionLoader Instance { get; set; }

        public static void Initialize(GraphicalWatchPackage package)
        {
            DTE2 dte = package.GetService(typeof(DTE)) as DTE2;
            
            Instance = new ExpressionLoader(dte);
        }

        private ExpressionLoader(DTE2 dte)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            this.dte = dte;
            this.debugger = dte.Debugger;
            this.debuggerEvents = this.dte.Events.DebuggerEvents;

            loaders = new Loaders();

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

            loaders.Add(new StdPairPoint());
            loaders.Add(new StdComplexPoint());

            loaders.Add(new BVariant());

            loaders.Add(new StdArray());
            loaders.Add(new BoostArray());
            loaders.Add(new BGVarray());
            loaders.Add(new BoostContainerVector());
            loaders.Add(new BoostContainerStaticVector());
            loaders.Add(new StdVector());
            loaders.Add(new StdDeque());
            loaders.Add(new StdList());

            loaders.Add(new CArray());
            loaders.Add(new PointCArray());

            loaders.Add(new PointContainer("std::array"));
            loaders.Add(new PointContainer("boost::array"));
            loaders.Add(new PointContainer("boost::geometry::index::detail::varray"));
            loaders.Add(new PointContainer("boost::container::vector"));
            loaders.Add(new PointContainer("boost::container::static_vector"));
            loaders.Add(new PointContainer("std::vector"));
            loaders.Add(new PointContainer("std::deque"));
            loaders.Add(new PointContainer("std::list"));

            loaders.Add(new BGTurn("boost::geometry::detail::overlay::turn_info"));
            loaders.Add(new BGTurn("boost::geometry::detail::overlay::traversal_turn_info"));
            loaders.Add(new BGTurn("boost::geometry::detail::buffer::buffer_turn_info"));
            loaders.Add(new BGTurnContainer("std::vector"));
            loaders.Add(new BGTurnContainer("std::deque"));
        }

        public static Debugger Debugger
        {
            get { return Instance.debugger; }
        }

        public static DebuggerEvents DebuggerEvents
        {
            get { return Instance.debuggerEvents; }
        }

        public interface KindConstraint
        {
            bool Check(Kind kind);
        }

        public class AllKindsConstraint : KindConstraint
        {
            public bool Check(Kind kind) { return true; }
        }

        public class GeometryKindConstraint : KindConstraint
        {
            public bool Check(Kind kind) { return kind != Kind.Container; }
        }

        public class ContainerKindConstraint : KindConstraint
        {
            public bool Check(Kind kind) { return kind == Kind.Container; }
        }

        public class MultiPointKindConstraint : KindConstraint
        {
            public bool Check(Kind kind) { return kind == Kind.MultiPoint; }
        }

        private static AllKindsConstraint allKinds = new AllKindsConstraint();
        public static AllKindsConstraint AllKinds { get { return allKinds; } }
        private static GeometryKindConstraint onlyGeometries = new GeometryKindConstraint();
        public static GeometryKindConstraint OnlyGeometries { get { return onlyGeometries; } }
        private static ContainerKindConstraint onlyContainers = new ContainerKindConstraint();
        public static ContainerKindConstraint OnlyContainers { get { return onlyContainers; } }
        private static MultiPointKindConstraint onlyMultiPoints = new MultiPointKindConstraint();
        public static MultiPointKindConstraint OnlyMultiPoints { get { return onlyMultiPoints; } }

        public static void Load(string name,
                                out Geometry.Traits traits,
                                out ExpressionDrawer.IDrawable result)
        {

            Load(name, AllKinds, out traits, out result);
        }

        public static void Load(string name,
                                KindConstraint kindConstraint,
                                out Geometry.Traits traits,
                                out ExpressionDrawer.IDrawable result)
        {
            traits = null;
            result = null;

            Expression expr = Instance.debugger.GetExpression(name);
            if (!expr.IsValidValue)
                return;

            Loader loader = Instance.loaders.FindByType(expr.Name, expr.Type);
            if (loader == null)
                return;

            if (!kindConstraint.Check(loader.Kind()))
                return;

            GeneralOptionPage optionPage = Util.GetDialogPage<GeneralOptionPage>();
            bool accessMemory = true;
            if (optionPage != null)
            {
                accessMemory = optionPage.EnableDirectMemoryAccess;
            }

            loader.Load(Instance.loaders, accessMemory,
                        Instance.debugger, expr.Name, expr.Type,
                        out traits, out result);
        }

        static int ParseInt(string s)
        {
            return int.Parse(s);
        }

        static double ParseDouble(string s)
        {
            return double.Parse(s, CultureInfo.InvariantCulture);
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

            public Loader FindById(Kind kind, string name, string id)
            {
                foreach (Loader l in lists[(int)kind])
                    if (id == l.Id())
                    {
                        l.Initialize(ExpressionLoader.Instance.debugger, name);
                        return l;
                    }
                return null;
            }

            public Loader FindById(string name, string id)
            {
                foreach (List<Loader> li in lists)
                    foreach (Loader l in li)
                        if (id == l.Id())
                        {
                            l.Initialize(ExpressionLoader.Instance.debugger, name);
                            return l;
                        }
                return null;
            }

            public Loader FindByType(Kind kind, string name, string type)
            {
                string id = Util.BaseType(type);
                foreach (Loader l in lists[(int)kind])
                {
                    TypeConstraint tc = l.Constraint();
                    if (l.MatchType(type, id) && (tc == null || tc.Ok(this, name, type)))
                    {
                        l.Initialize(ExpressionLoader.Instance.debugger, name);
                        return l;
                    }
                }
                return null;
            }

            public Loader FindByType(string name, string type)
            {
                string id = Util.BaseType(type);
                foreach (List<Loader> li in lists)
                    foreach (Loader l in li)
                    {
                        TypeConstraint tc = l.Constraint();
                        if (l.MatchType(type, id) && (tc == null || tc.Ok(this, name, type)))
                        {
                            l.Initialize(ExpressionLoader.Instance.debugger, name);
                            return l;
                        }
                    }
                return null;
            }

            List<Loader>[] lists;
        }

        abstract class TypeConstraint
        {
            abstract public bool Ok(Loaders loaders, string name, string type);
        }

        abstract class TparamConstraint : TypeConstraint
        {
            abstract public bool Ok(Loaders loaders, string name, List<string> tparams);

            public override bool Ok(Loaders loaders, string name, string type)
            {
                return Ok(loaders, name, Util.Tparams(type));
            }
        }

        class TparamKindConstraint : TparamConstraint
        {
            public TparamKindConstraint(int i, Kind kind)
            {
                this.i = i;
                this.kind = kind;
            }

            public override bool Ok(Loaders loaders, string name, List<string> tparams)
            {
                if (i < tparams.Count)
                {
                    Loader loader = loaders.FindByType(name, tparams[i]);
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
            virtual public bool MatchType(string type, string id)
            {
                return id == Id();
            }

            abstract public ExpressionLoader.Kind Kind();
            virtual public TypeConstraint Constraint()
            {
                return null;
            }

            virtual public void Initialize(Debugger debugger, string name)
            { }

            abstract public void Load(Loaders loaders, bool accessMemory,
                                      Debugger debugger, string name, string type,
                                      out Geometry.Traits traits,
                                      out ExpressionDrawer.IDrawable result);

            virtual public MemoryReader.Converter<double> GetMemoryConverter(Debugger debugger, string name, string type)
            {
                return null;
            }
        }

        abstract class LoaderR<ResultType> : Loader
            where ResultType : ExpressionDrawer.IDrawable
        {
            public override void Load(Loaders loaders, bool accessMemory,
                                      Debugger debugger, string name, string type,
                                      out Geometry.Traits traits,
                                      out ExpressionDrawer.IDrawable result)
            {
                ResultType res = default(ResultType);
                this.Load(loaders, accessMemory, debugger, name, type, out traits, out res);
                result = res;
            }

            abstract public void Load(Loaders loaders, bool accessMemory,
                                      Debugger debugger, string name, string type,
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
            
            public override void Load(Loaders loaders, bool accessMemory,
                                      Debugger debugger, string name, string type,
                                      out Geometry.Traits traits,
                                      out ExpressionDrawer.Point point)
            {
                traits = LoadTraits(type);
                point = traits != null
                      ? LoadPoint(accessMemory, debugger, name, type)
                      : null;
            }

            abstract public Geometry.Traits LoadTraits(string type);

            virtual public ExpressionDrawer.Point LoadPoint(bool accessMemory, Debugger debugger, string name, string type)
            {
                ExpressionDrawer.Point result = null;
                if (accessMemory)
                    result = LoadPointMemory(debugger, name, type);
                if (result == null)
                    result = LoadPointParsed(debugger, name, type);
                return result;
            }
            abstract protected ExpressionDrawer.Point LoadPointParsed(Debugger debugger, string name, string type);
            abstract protected ExpressionDrawer.Point LoadPointMemory(Debugger debugger, string name, string type);
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

            abstract public int LoadSize(Debugger debugger, string name);

            public delegate bool ElementPredicate(string elementName);
            abstract public bool ForEachElement(Debugger debugger, string name, ElementPredicate elementPredicate);

            // ForEachMemoryBlock calling ReadArray taking ElementLoader returned by ContainerLoader
            // With ReadArray knowing which memory copying optimizations can be made based on ElementLoader's type
            // Or not

            abstract public string ElementPtrName(string name);
            public delegate bool MemoryBlockPredicate(double[] values);
            abstract public bool ForEachMemoryBlock(Debugger debugger, string name, MemoryReader.Converter<double> elementConverter, MemoryBlockPredicate memoryBlockPredicate);

            virtual public string ElementName(string name)
            {
                return "(*(" + this.ElementPtrName(name) + "))";
            }
        }

        abstract class BXPoint : PointLoader
        {
            protected ExpressionDrawer.Point LoadPointParsed(Debugger debugger, string name, string type, string ptrName, int count)
            {
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

            protected ExpressionDrawer.Point LoadPointMemory(Debugger debugger, string name, string type, string ptrName, int count)
            {
                double[] values = new double[count];
                if (MemoryReader.ReadNumericArray(debugger, ptrName, values))
                {
                    if (count > 1)
                        return new ExpressionDrawer.Point(values[0], values[1]);
                    else if (count > 0)
                        return new ExpressionDrawer.Point(values[0], 0);
                    else
                        return new ExpressionDrawer.Point(0, 0);
                }

                return null;
            }

            protected MemoryReader.Converter<double> GetMemoryConverter(Debugger debugger, string name, string memberArray, string elemType, int count)
            {
                string ptrName = "(&" + name + ")";

                MemoryReader.Converter<double> arrayConverter
                    = MemoryReader.GetNumericArrayConverter(debugger, memberArray, elemType, count);
                int byteSize = MemoryReader.GetValueSizeof(debugger, ptrName);
                if (byteSize == 0)
                    return null;
                long byteOffset
                    = MemoryReader.GetAddressDifference(debugger, ptrName, memberArray);
                if (MemoryReader.IsInvalidAddressDifference(byteOffset))
                    return null;
                return new MemoryReader.StructConverter(byteSize,
                            new MemoryReader.Member(arrayConverter, (int)byteOffset));
            }
        }

        class BGPoint : BXPoint
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

            protected override ExpressionDrawer.Point LoadPointParsed(Debugger debugger, string name, string type)
            {
                List<string> tparams = Util.Tparams(type);
                if (tparams.Count < 2)
                    return null;
                int dimension = ParseInt(tparams[1]);
                int count = Math.Min(dimension, 2);
                return LoadPointParsed(debugger, name, type, name + ".m_values", count);
            }

            protected override ExpressionDrawer.Point LoadPointMemory(Debugger debugger, string name, string type)
            {
                List<string> tparams = Util.Tparams(type);
                if (tparams.Count < 2)
                    return null;
                int dimension = ParseInt(tparams[1]);
                int count = Math.Min(dimension, 2);
                return LoadPointMemory(debugger, name, type, name + ".m_values", count);
            }

            public override MemoryReader.Converter<double> GetMemoryConverter(Debugger debugger, string name, string type)
            {
                List<string> tparams = Util.Tparams(type);
                if (tparams.Count < 2)
                    return null;
                string coordType = tparams[0];
                int dimension = ParseInt(tparams[1]);
                int count = Math.Min(dimension, 2);
                return GetMemoryConverter(debugger, name, name + ".m_values", coordType, count);
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

            protected override ExpressionDrawer.Point LoadPointParsed(Debugger debugger, string name, string type)
            {
                return LoadPointParsed(debugger, name, type, name + ".m_values", 2);
            }

            protected override ExpressionDrawer.Point LoadPointMemory(Debugger debugger, string name, string type)
            {
                return LoadPointMemory(debugger, name, type, name + ".m_values", 2);
            }

            public override MemoryReader.Converter<double> GetMemoryConverter(Debugger debugger, string name, string type)
            {
                List<string> tparams = Util.Tparams(type);
                if (tparams.Count < 1)
                    return null;
                string coordType = tparams[0];

                return GetMemoryConverter(debugger, name, name + ".m_values", coordType, 2);
            }
        }

        class BGBox : BoxLoader
        {
            public override string Id() { return "boost::geometry::model::box"; }

            public override void Load(Loaders loaders, bool accessMemory,
                                      Debugger debugger, string name, string type,
                                      out Geometry.Traits traits,
                                      out ExpressionDrawer.Box result)
            {
                traits = null;
                result = null;

                List<string> tparams = Util.Tparams(type);
                if (tparams.Count < 1)
                    return;

                string m_min_corner = name + ".m_min_corner";
                string m_max_corner = name + ".m_max_corner";

                string pointType = tparams[0];
                PointLoader pointLoader = loaders.FindByType(ExpressionLoader.Kind.Point,
                                                             m_min_corner,
                                                             pointType) as PointLoader;
                if (pointLoader == null)
                    return;

                traits = pointLoader.LoadTraits(pointType);
                if (traits == null)
                    return;

                Geometry.Point fp = pointLoader.LoadPoint(accessMemory, debugger, m_min_corner, pointType);
                Geometry.Point sp = pointLoader.LoadPoint(accessMemory, debugger, m_max_corner, pointType);

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

            public override void Load(Loaders loaders, bool accessMemory,
                                      Debugger debugger, string name, string type,
                                      out Geometry.Traits traits,
                                      out ExpressionDrawer.Segment segment)
            {
                traits = null;
                segment = null;

                List<string> tparams = Util.Tparams(type);
                if (tparams.Count < 1)
                    return;

                string first = name + ".first";
                string second = name + ".second";

                string pointType = tparams[0];
                PointLoader pointLoader = loaders.FindByType(ExpressionLoader.Kind.Point,
                                                             first,
                                                             pointType) as PointLoader;
                if (pointLoader == null)
                    return;

                traits = pointLoader.LoadTraits(pointType);
                if (traits == null)
                    return;

                Geometry.Point fp = pointLoader.LoadPoint(accessMemory, debugger, first, pointType);
                Geometry.Point sp = pointLoader.LoadPoint(accessMemory, debugger, second, pointType);

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

            public override void Load(Loaders loaders, bool accessMemory,
                                      Debugger debugger, string name, string type,
                                      out Geometry.Traits traits,
                                      out ExpressionDrawer.NSphere result)
            {
                traits = null;
                result = null;

                List<string> tparams = Util.Tparams(type);
                if (tparams.Count < 1)
                    return;

                string m_center = name + ".m_center";
                string m_radius = name + ".m_radius";

                string pointType = tparams[0];
                PointLoader pointLoader = loaders.FindByType(ExpressionLoader.Kind.Point,
                                                             m_center, pointType) as PointLoader;
                if (pointLoader == null)
                    return;

                traits = pointLoader.LoadTraits(pointType);
                if (traits == null)
                    return;

                Geometry.Point center = pointLoader.LoadPoint(accessMemory, debugger,
                                                              m_center, pointType);
                bool ok = false;
                double radius = LoadAsDouble(debugger, m_radius, out ok);

                result = IsOk(center, ok)
                       ? new ExpressionDrawer.NSphere(center, radius)
                       : null;
            }
        }

        abstract class PointRange<ResultType> : RangeLoader<ResultType>
            where ResultType : class
                             , ExpressionDrawer.IDrawable
                             , Geometry.IContainer<Geometry.Point>
                             , new()
        {
            protected PointRange(ExpressionLoader.Kind kind)
                : base(kind)
            { }

            protected ResultType LoadParsed(bool accessMemory, // should this be passed?
                                            Debugger debugger, string name, string type,
                                            string pointType,
                                            PointLoader pointLoader,
                                            ContainerLoader containerLoader)
            {
                ResultType result = new ResultType();
                containerLoader.ForEachElement(debugger, name, delegate (string elName)
                {
                    ExpressionDrawer.Point p = pointLoader.LoadPoint(accessMemory, debugger, elName, pointType);
                    if (p == null)
                    {
                        result = null;
                        return false;
                    }
                    result.Add(p);
                    return true;
                });
                return result;
            }

            protected ResultType LoadMemory(Debugger debugger, string name, string type,
                                            string pointType,
                                            PointLoader pointLoader,
                                            ContainerLoader containerLoader)
            {
                ResultType result = null;

                string pointPtrName = containerLoader.ElementPtrName(name);
                if (pointPtrName != null)
                {
                    string pointName = "(*((" + pointType + "*)" + pointPtrName + "))";
                    MemoryReader.Converter<double> pointConverter = pointLoader.GetMemoryConverter(debugger, pointName, pointType);
                    if (pointConverter != null)
                    {
                        int dimension = pointConverter.ValueCount();
                        result = new ResultType();
                        bool ok = containerLoader.ForEachMemoryBlock(debugger, name, pointConverter,
                            delegate (double[] values)
                            {
                                if (dimension == 0 || values.Length % dimension != 0)
                                    return false;
                                int size = dimension > 0
                                         ? values.Length / dimension
                                         : 0;
                                for (int i = 0; i < size; ++i)
                                {
                                    double x = dimension > 0 ? values[i * dimension] : 0;
                                    double y = dimension > 1 ? values[i * dimension + 1] : 0;
                                    ExpressionDrawer.Point p = new ExpressionDrawer.Point(x, y);
                                    result.Add(p);
                                }
                                return true;
                            });
                        if (!ok)
                            result = null;
                    }
                }

                return result;
            }
        }

        class BGRange<ResultType> : PointRange<ResultType>
            where ResultType : class
                             , ExpressionDrawer.IDrawable
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

            public override void Load(Loaders loaders, bool accessMemory,
                                      Debugger debugger, string name, string type,
                                      out Geometry.Traits traits,
                                      out ResultType result)
            {
                traits = null;
                result = null;

                List<string> tparams = Util.Tparams(type);
                if (tparams.Count <= Math.Max(pointTIndex, containerTIndex))
                    return;

                // TODO: This is not fully correct since the tparam may be an id, not a type
                // however since FindByType() internaly uses the id/base-type it will work for both
                string containerType = containerTIndex >= 0 ? tparams[containerTIndex] : type;
                ContainerLoader containerLoader = loaders.FindByType(ExpressionLoader.Kind.Container, name, containerType) as ContainerLoader;
                if (containerLoader == null)
                    return;

                string pointType = tparams[pointTIndex];
                PointLoader pointLoader = loaders.FindByType(ExpressionLoader.Kind.Point,
                                                             containerLoader.ElementName(name),
                                                             pointType) as PointLoader;
                if (pointLoader == null)
                    return;

                traits = pointLoader.LoadTraits(pointType);
                if (traits == null)
                    return;

                if (accessMemory)
                {
                    result = LoadMemory(debugger, name, type,
                                        pointType, pointLoader, containerLoader);
                }

                if (result == null)
                {
                    result = LoadParsed(accessMemory, debugger, name, type,
                                        pointType, pointLoader, containerLoader);
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

            public override void Load(Loaders loaders, bool accessMemory,
                                      Debugger debugger, string name, string type,
                                      out Geometry.Traits traits,
                                      out ExpressionDrawer.MultiLinestring result)
            {
                traits = null;
                result = null;

                List<string> tparams = Util.Tparams(type);
                if (tparams.Count < 2)
                    return;

                string containerType = tparams[1];
                ContainerLoader containerLoader = loaders.FindByType(ExpressionLoader.Kind.Container,
                                                                     name,
                                                                     containerType) as ContainerLoader;
                if (containerLoader == null)
                    return;

                string lsType = tparams[0];
                RangeLoader<ExpressionDrawer.Linestring>
                    lsLoader = loaders.FindByType(ExpressionLoader.Kind.Linestring,
                                                  containerLoader.ElementName(name),
                                                  lsType) as RangeLoader<ExpressionDrawer.Linestring>;
                if (lsLoader == null)
                    return;

                Geometry.Traits t = null;
                ExpressionDrawer.MultiLinestring mls = new ExpressionDrawer.MultiLinestring();
                bool ok = containerLoader.ForEachElement(debugger, name, delegate (string elName)
                {
                    ExpressionDrawer.Linestring ls = null;
                    lsLoader.Load(loaders, accessMemory, debugger, elName, lsType, out t, out ls);
                    if (ls == null)
                        return false;
                    mls.Add(ls);
                    return true;
                });
                if (ok)
                {
                    traits = t;
                    result = mls;
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

            public override void Load(Loaders loaders, bool accessMemory,
                                      Debugger debugger, string name, string type,
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
                BGRing outerLoader = loaders.FindByType(ExpressionLoader.Kind.Ring,
                                                        outerName, outerType) as BGRing;
                if (outerLoader == null)
                    return;

                string innersType = innersExpr.Type;
                ContainerLoader innersLoader = loaders.FindByType(ExpressionLoader.Kind.Container,
                                                                  innersName, innersType) as ContainerLoader;
                if (innersLoader == null)
                    return;

                ExpressionDrawer.Ring outer = null;
                outerLoader.Load(loaders, accessMemory, debugger, outerName, outerExpr.Type, out traits, out outer);
                if (outer == null)
                    return;

                Geometry.Traits t = null;
                List<Geometry.Ring> inners = new List<Geometry.Ring>();
                bool ok = innersLoader.ForEachElement(debugger, innersName, delegate (string elName)
                {
                    ExpressionDrawer.Ring inner = null;
                    outerLoader.Load(loaders, accessMemory, debugger, elName, outerExpr.Type, out t, out inner);
                    if (inner == null)
                        return false;                    
                    inners.Add(inner);
                    return true;
                });
                if (ok)
                {
                    result = new ExpressionDrawer.Polygon(outer, inners);
                    if (traits == null) // assign only if it was not set for outer ring
                        traits = t;
                }
            }
        }

        class BGMultiPolygon : RangeLoader<ExpressionDrawer.MultiPolygon>
        {
            public BGMultiPolygon() : base(ExpressionLoader.Kind.MultiPolygon) { }

            public override string Id() { return "boost::geometry::model::multi_polygon"; }

            public override void Load(Loaders loaders, bool accessMemory,
                                      Debugger debugger, string name, string type,
                                      out Geometry.Traits traits,
                                      out ExpressionDrawer.MultiPolygon result)
            {
                traits = null;
                result = null;

                List<string> tparams = Util.Tparams(type);
                if (tparams.Count < 2)
                    return;

                string containerType = tparams[1];
                ContainerLoader containerLoader = loaders.FindByType(ExpressionLoader.Kind.Container,
                                                                     name,
                                                                     containerType) as ContainerLoader;
                if (containerLoader == null)
                    return;

                string polyType = tparams[0];
                PolygonLoader polyLoader = loaders.FindByType(ExpressionLoader.Kind.Polygon,
                                                              containerLoader.ElementName(name),
                                                              polyType) as PolygonLoader;
                if (polyLoader == null)
                    return;

                Geometry.Traits t = null;
                ExpressionDrawer.MultiPolygon mpoly = new ExpressionDrawer.MultiPolygon();
                bool ok = containerLoader.ForEachElement(debugger, name, delegate (string elName)
                {
                    ExpressionDrawer.Polygon poly = null;
                    polyLoader.Load(loaders, accessMemory, debugger, elName, polyType, out t, out poly);
                    if (poly == null)
                        return false;
                    mpoly.Add(poly);
                    return true;
                });
                if (ok)
                {
                    traits = t;
                    result = mpoly;
                }
            }
        }

        class BGBufferedRing : RangeLoader<ExpressionDrawer.Ring>
        {
            public BGBufferedRing() : base(ExpressionLoader.Kind.Ring) { }

            public override string Id() { return "boost::geometry::detail::buffer::buffered_ring"; }

            public override void Load(Loaders loaders, bool accessMemory,
                                      Debugger debugger, string name, string type,
                                      out Geometry.Traits traits,
                                      out ExpressionDrawer.Ring result)
            {
                traits = null;
                result = null;

                List<string> tparams = Util.Tparams(type);
                if (tparams.Count < 1)
                    return;

                string ringType = tparams[0];
                RangeLoader<ExpressionDrawer.Ring> ringLoader = loaders.FindByType(ExpressionLoader.Kind.Ring,
                                                                                   name,
                                                                                   ringType) as RangeLoader<ExpressionDrawer.Ring>;
                if (ringLoader == null)
                    return;

                ringLoader.Load(loaders, accessMemory, debugger, name, ringType, out traits, out result);
            }
        }

        // NOTE: There is no MultiRing concept so use MultiPolygon for now
        class BGBufferedRingCollection : RangeLoader<ExpressionDrawer.MultiPolygon>
        {
            public BGBufferedRingCollection() : base(ExpressionLoader.Kind.MultiPolygon) { }

            public override string Id() { return "boost::geometry::detail::buffer::buffered_ring_collection"; }

            public override void Load(Loaders loaders, bool accessMemory,
                                      Debugger debugger, string name, string type,
                                      out Geometry.Traits traits,
                                      out ExpressionDrawer.MultiPolygon result)
            {
                traits = null;
                result = null;

                List<string> tparams = Util.Tparams(type);
                if (tparams.Count < 1)
                    return;

                ContainerLoader containerLoader = loaders.FindById(ExpressionLoader.Kind.Container,
                                                                   name,
                                                                   "std::vector") as ContainerLoader;
                if (containerLoader == null)
                    return;

                string ringType = tparams[0];
                RangeLoader<ExpressionDrawer.Ring>
                    ringLoader = loaders.FindByType(ExpressionLoader.Kind.Ring,
                                                    containerLoader.ElementName(name),
                                                    ringType) as RangeLoader<ExpressionDrawer.Ring>;
                if (ringLoader == null)
                    return;
                
                Geometry.Traits t = null;
                ExpressionDrawer.MultiPolygon mpoly = new ExpressionDrawer.MultiPolygon();
                bool ok = containerLoader.ForEachElement(debugger, name, delegate (string elName)
                {
                    ExpressionDrawer.Ring ring = null;
                    ringLoader.Load(loaders, accessMemory, debugger, elName, ringType, out t, out ring);
                    if (ring == null)
                        return false;
                    mpoly.Add(new ExpressionDrawer.Polygon(ring));
                    return true;
                });
                if (ok)
                {
                    traits = t;
                    result = mpoly;
                }
            }
        }

        class BPPoint : BXPoint
        {
            public override string Id() { return "boost::polygon::point_data"; }

            public override Geometry.Traits LoadTraits(string type)
            {
                return new Geometry.Traits(2, Geometry.CoordinateSystem.Cartesian, Geometry.Unit.None);
            }

            protected override ExpressionDrawer.Point LoadPointParsed(Debugger debugger, string name, string type)
            {
                return LoadPointParsed(debugger, name, type, name + ".coords_", 2);
            }

            protected override ExpressionDrawer.Point LoadPointMemory(Debugger debugger, string name, string type)
            {
                return LoadPointMemory(debugger, name, type, name + ".coords_", 2);
            }

            public override MemoryReader.Converter<double> GetMemoryConverter(Debugger debugger, string name, string type)
            {
                List<string> tparams = Util.Tparams(type);
                if (tparams.Count < 1)
                    return null;
                string coordType = tparams[0];

                return GetMemoryConverter(debugger, name, name + ".coords_", coordType, 2);
            }
        }

        class BPSegment : SegmentLoader
        {
            public override string Id() { return "boost::polygon::segment_data"; }

            public override void Load(Loaders loaders, bool accessMemory,
                                      Debugger debugger, string name, string type,
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

            public override void Load(Loaders loaders, bool accessMemory,
                                      Debugger debugger, string name, string type,
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

        class BPRing : PointRange<ExpressionDrawer.Ring>
        {
            public BPRing() : base(ExpressionLoader.Kind.Ring) { }

            public override string Id() { return "boost::polygon::polygon_data"; }

            public override void Load(Loaders loaders, bool accessMemory,
                                      Debugger debugger, string name, string type,
                                      out Geometry.Traits traits,
                                      out ExpressionDrawer.Ring result)
            {
                traits = new Geometry.Traits(2, Geometry.CoordinateSystem.Cartesian, Geometry.Unit.None);
                result = null;

                ContainerLoader containerLoader = loaders.FindById(ExpressionLoader.Kind.Container,
                                                                   name,
                                                                   "std::vector") as ContainerLoader;
                if (containerLoader == null)
                    return;

                BPPoint pointLoader = loaders.FindById(ExpressionLoader.Kind.Point,
                                                       containerLoader.ElementName(name),
                                                       "boost::polygon::point_data") as BPPoint;
                if (pointLoader == null)
                    return;

                List<string> tparams = Util.Tparams(type);
                if (tparams.Count < 1)
                    return;

                string tparam = tparams[0].EndsWith(">") ? tparams[0] + ' ' : tparams[0];
                string pointType = "boost::polygon::point_data<" + tparam + ">";

                string containerName = name + ".coords_";

                if (accessMemory)
                {
                    result = LoadMemory(debugger, containerName, type,
                                        pointType, pointLoader, containerLoader);
                }

                if (result == null)
                {
                    result = LoadParsed(accessMemory, debugger, containerName, type,
                                        pointType, pointLoader, containerLoader);
                }
            }
        }

        class BPPolygon : PolygonLoader
        {
            public override string Id() { return "boost::polygon::polygon_with_holes_data"; }

            public override void Load(Loaders loaders, bool accessMemory,
                                      Debugger debugger, string name, string type,
                                      out Geometry.Traits traits,
                                      out ExpressionDrawer.Polygon result)
            {
                traits = new Geometry.Traits(2, Geometry.CoordinateSystem.Cartesian, Geometry.Unit.None);
                result = null;

                List<string> tparams = Util.Tparams(type);
                if (tparams.Count < 1)
                    return;

                string tparam = tparams[0].EndsWith(">") ? tparams[0] + ' ' : tparams[0];
                string pointType = "boost::polygon::point_data<" + tparam + ">";
                string polygonType = "boost::polygon::polygon_data<" + tparam + ">";

                string member_self_ = name + ".self_";
                string member_holes_ = name + ".holes_";

                PointLoader pointLoader = loaders.FindById(ExpressionLoader.Kind.Point,
                                                           // NOTE: use default ctor,
                                                           // initialization is not done for BPPoint anyway
                                                           pointType + "()",
                                                           "boost::polygon::point_data") as PointLoader;
                if (pointLoader == null)
                    return;

                BPRing outerLoader = loaders.FindById(ExpressionLoader.Kind.Ring,
                                                      member_self_,
                                                      "boost::polygon::polygon_data") as BPRing;
                if (outerLoader == null)
                    return;

                ContainerLoader holesLoader = loaders.FindById(ExpressionLoader.Kind.Container,
                                                               member_holes_,
                                                               "std::list") as ContainerLoader;
                if (holesLoader == null)
                    return;

                ExpressionDrawer.Ring outer = null;
                outerLoader.Load(loaders, accessMemory,
                                 debugger, member_self_, polygonType,
                                 out traits, out outer);
                if (outer == null)
                    return;

                Geometry.Traits t = null;
                List<Geometry.Ring> holes = new List<Geometry.Ring>();
                bool ok = holesLoader.ForEachElement(debugger, member_holes_, delegate (string elName)
                {
                    ExpressionDrawer.Ring hole = null;
                    outerLoader.Load(loaders, accessMemory, debugger, elName, polygonType, out t, out hole);
                    if (hole == null)
                        return false;
                    holes.Add(hole);
                    return true;
                });
                if (ok)
                {
                    result = new ExpressionDrawer.Polygon(outer, holes);
                    if (traits == null) // assign only if it was not set for outer ring
                        traits = t;
                }
            }
        }
        
        abstract class ValuesContainer : ContainerLoader
        {
            virtual public string ElementType(string type)
            {
                List<string> tparams = Util.Tparams(type);
                return tparams.Count > 0 ? tparams[0] : "";
            }

            public override void Load(Loaders loaders, bool accessMemory,
                                      Debugger debugger, string name, string type,
                                      out Geometry.Traits traits,
                                      out ExpressionDrawer.ValuesContainer result)
            {
                traits = null;
                result = null;

                if (accessMemory)
                    LoadMemory(debugger, name, type, out result);

                if (result == null)
                    LoadParsed(debugger, name, out result);
            }

            protected void LoadMemory(Debugger debugger, string name, string type,
                                      out ExpressionDrawer.ValuesContainer result)
            {
                result = null;

                string ptrName = "((" + this.ElementType(type) + "*)" + this.ElementPtrName(name) + ")";

                MemoryReader.ValueConverter<double>
                    valueConverter = MemoryReader.GetNumericConverter(debugger, ptrName, null);
                if (valueConverter == null)
                    return;

                List<double> list = new List<double>();
                bool ok = this.ForEachMemoryBlock(debugger, name, valueConverter,
                    delegate (double[] values)
                    {
                        foreach (double v in values)
                            list.Add(v);
                        return true;
                    });

                if (ok)
                    result = new ExpressionDrawer.ValuesContainer(list);
            }

            protected void LoadParsed(Debugger debugger, string name, out ExpressionDrawer.ValuesContainer result)
            {                
                result = null;
                int size = this.LoadSize(debugger, name);
                List<double> values = new List<double>();
                bool ok = this.ForEachElement(debugger, name, delegate (string elName)
                {
                    bool okV = false;
                    double value = LoadAsDouble(debugger, elName, out okV);
                    if (okV)
                        values.Add(value);
                    return okV;
                });
                if (ok)
                {
                    result = new ExpressionDrawer.ValuesContainer(values);
                }
            }
        }

        abstract class RandomAccessContainer : ValuesContainer
        {
            public override bool ForEachElement(Debugger debugger, string name, ElementPredicate elementPredicate)
            {
                int size = this.LoadSize(debugger, name);
                string rawName = this.RandomAccessName(name);
                for (int i = 0; i < size; ++i)
                {
                    string elName = this.RandomAccessElementName(rawName, i);
                    if (!elementPredicate(elName))
                        return false;
                }
                return true;
            }

            virtual public string RandomAccessName(string name)
            {
                return name;
            }

            virtual public string RandomAccessElementName(string rawName, int i)
            {
                return rawName + "[" + i + "]";
            }
        }

        abstract class ContiguousContainer : RandomAccessContainer
        {
            protected bool ForEachMemoryBlock(Debugger debugger, string name, string blockPtrName,
                                              MemoryReader.Converter<double> elementConverter,
                                              MemoryBlockPredicate memoryBlockPredicate)
            {
                if(elementConverter == null)
                    return false;
                int size = LoadSize(debugger, name);
                var blockConverter = new MemoryReader.ArrayConverter<double>(elementConverter, size);
                double[] values = new double[blockConverter.ValueCount()];
                if (!MemoryReader.Read(debugger, blockPtrName, values, blockConverter))
                    return false;
                return memoryBlockPredicate(values);
            }
        }

        class CArray : ContiguousContainer
        {
            public override string Id() { return null; }

            public override bool MatchType(string type, string id)
            {
                string foo;
                int bar;
                return NameSizeFromType(type, out foo, out bar);
            }

            public override string ElementType(string type)
            {
                string elemType;
                int size;
                NameSizeFromType(type, out elemType, out size);
                return elemType;
            }

            public override string ElementPtrName(string name)
            {
                // make raw pointer from the carray
                return "(&(" + this.RandomAccessName(name) + "[0]))";
            }

            public override string RandomAccessName(string name)
            {
                return RawNameFromName(name);
            }

            public override bool ForEachMemoryBlock(Debugger debugger, string name,
                                                    MemoryReader.Converter<double> elementConverter,
                                                    MemoryBlockPredicate memoryBlockPredicate)
            {
                return this.ForEachMemoryBlock(debugger,
                                               name,
                                               ElementPtrName(name),
                                               elementConverter, memoryBlockPredicate);
            }

            public override int LoadSize(Debugger debugger, string name)
            {
                Expression expr = debugger.GetExpression(name);
                if (!expr.IsValidValue)
                    return 0;
                string dummy;
                int size = 0;
                NameSizeFromType(expr.Type, out dummy, out size);
                return size;
            }

            // T[N]
            static public bool NameSizeFromType(string type, out string name, out int size)
            {
                name = "";
                size = 0;
                int end = type.LastIndexOf(']');
                if (end + 1 != type.Length)
                    return false;
                int begin = type.LastIndexOf('[');
                if (begin <= 0)
                    return false;
                name = type.Substring(0, begin);
                string strSize = type.Substring(begin + 1, end - begin - 1);
                return int.TryParse(strSize, out size);
            }

            // int a[5];    -> a
            // int * p = a; -> p,5
            static public string RawNameFromName(string name)
            {
                string result = name;
                int commaPos = name.LastIndexOf(',');
                if (commaPos >= 0)
                {
                    string strSize = name.Substring(commaPos + 1);
                    int size;
                    if (int.TryParse(strSize, out size))
                        result = name.Substring(0, commaPos);
                }
                return result;
            }
        }

        class StdArray : ContiguousContainer
        {
            public override string Id() { return "std::array"; }

            public override string ElementPtrName(string name)
            {
                return name + "._Elems";
            }

            public override bool ForEachMemoryBlock(Debugger debugger, string name,
                                                    MemoryReader.Converter<double> elementConverter,
                                                    MemoryBlockPredicate memoryBlockPredicate)
            {
                return this.ForEachMemoryBlock(debugger, name, name + "._Elems",
                                               elementConverter, memoryBlockPredicate);
            }

            public override int LoadSize(Debugger debugger, string name)
            {
                Expression expr = debugger.GetExpression(name);
                return expr.IsValidValue
                     ? Math.Max(ParseInt(Util.Tparams(expr.Type)[1]), 0)
                     : 0;
            }
        }

        class BoostArray : StdArray
        {
            public override string Id() { return "boost::array"; }

            public override string ElementPtrName(string name)
            {
                return name + ".elems";
            }

            public override bool ForEachMemoryBlock(Debugger debugger, string name,
                                                    MemoryReader.Converter<double> elementConverter,
                                                    MemoryBlockPredicate memoryBlockPredicate)
            {
                return this.ForEachMemoryBlock(debugger, name, name + ".elems",
                                               elementConverter, memoryBlockPredicate);
            }
        }

        class BoostContainerVector : ContiguousContainer
        {
            public override string Id() { return "boost::container::vector"; }

            public override string ElementPtrName(string name)
            {
                return name + ".m_holder.m_start";
            }

            public override bool ForEachMemoryBlock(Debugger debugger, string name,
                                                    MemoryReader.Converter<double> elementConverter,
                                                    MemoryBlockPredicate memoryBlockPredicate)
            {
                return this.ForEachMemoryBlock(debugger, name, name + ".m_holder.m_start",
                                               elementConverter, memoryBlockPredicate);
            }

            public override int LoadSize(Debugger debugger, string name)
            {
                Expression expr = debugger.GetExpression(name + ".m_holder.m_size");
                return expr.IsValidValue
                     ? Math.Max(ParseInt(expr.Value), 0)
                     : 0;
            }
        }

        class BoostContainerStaticVector : BoostContainerVector
        {
            public override string Id() { return "boost::container::static_vector"; }

            public override string ElementPtrName(string name)
            {
                return name + ".m_holder.storage.data";
            }

            public override bool ForEachMemoryBlock(Debugger debugger, string name,
                                                    MemoryReader.Converter<double> elementConverter,
                                                    MemoryBlockPredicate memoryBlockPredicate)
            {
                return this.ForEachMemoryBlock(debugger, name, this.ElementPtrName(name),
                                               elementConverter, memoryBlockPredicate);
            }
        }

        class BGVarray : BoostContainerVector
        {
            public override string Id() { return "boost::geometry::index::detail::varray"; }

            public override string ElementPtrName(string name)
            {
                return name + ".m_storage.data_.buf";
            }

            public override bool ForEachMemoryBlock(Debugger debugger, string name,
                                                    MemoryReader.Converter<double> elementConverter,
                                                    MemoryBlockPredicate memoryBlockPredicate)
            {
                return this.ForEachMemoryBlock(debugger, name, name + ".m_storage.data_.buf",
                                               elementConverter, memoryBlockPredicate);
            }

            public override int LoadSize(Debugger debugger, string name)
            {
                Expression expr = debugger.GetExpression(name + ".m_size");
                return expr.IsValidValue
                     ? Math.Max(ParseInt(expr.Value), 0)
                     : 0;
            }
        }

        class StdVector : ContiguousContainer
        {
            public override string Id() { return "std::vector"; }

            public override string RandomAccessElementName(string rawName, int i)
            {
                return FirstStr(rawName) + "[" + i + "]";
            }

            public override string ElementPtrName(string name)
            {
                return FirstStr(name);
            }

            public override bool ForEachMemoryBlock(Debugger debugger, string name,
                                                    MemoryReader.Converter<double> elementConverter,
                                                    MemoryBlockPredicate memoryBlockPredicate)
            {
                return this.ForEachMemoryBlock(debugger, name,
                                               ElementPtrName(name),
                                               elementConverter, memoryBlockPredicate);
            }

            public override int LoadSize(Debugger debugger, string name)
            {
                Expression expr = debugger.GetExpression(SizeStr(name));
                return expr.IsValidValue
                     ? Math.Max(ParseInt(expr.Value), 0)
                     : 0;
            }

            private string FirstStr(string name)
            {
                return version == Version.Msvc12
                     ? name + "._Myfirst"
                     : name + "._Mypair._Myval2._Myfirst";
            }

            private string SizeStr(string name)
            {
                return version == Version.Msvc12
                     ? name + "._Mylast-" + name + "._Myfirst"
                     : name + "._Mypair._Myval2._Mylast-" + name + "._Mypair._Myval2._Myfirst";
            }

            public override void Initialize(Debugger debugger, string name)
            {
                string name12 = name + "._Myfirst";
                //string name14_15 = name + "._Mypair._Myval2._Myfirst";

                if (debugger.GetExpression(name12).IsValidValue)
                    version = Version.Msvc12;
            }
            private enum Version { Unknown, Msvc12, Msvc14_15 };
            private Version version = Version.Msvc14_15;
        }

        class StdDeque : RandomAccessContainer
        {
            public override string Id() { return "std::deque"; }

            public override string ElementPtrName(string name)
            {
                return "(&(" + ElementStr(name, 0) + "))";
            }

            public override bool ForEachMemoryBlock(Debugger debugger, string name,
                                                    MemoryReader.Converter<double> elementConverter,
                                                    MemoryBlockPredicate memoryBlockPredicate)
            {
                int size = LoadSize(debugger, name);
                if (size == 0)
                    return true;

                // Map size                
                Expression mapSizeExpr = debugger.GetExpression(MapSizeStr(name));
                if (!mapSizeExpr.IsValidValue)
                    return false;
                int mapSize = int.Parse(mapSizeExpr.Value);

                // Map - array of pointers                
                ulong[] pointers = new ulong[mapSize];
                if (! MemoryReader.ReadPointerArray(debugger, MapStr(name), pointers))
                    return false;

                // Block size
                Expression dequeSizeExpr = debugger.GetExpression("((int)" + name + "._EEN_DS)");
                if (!dequeSizeExpr.IsValidValue)
                    return false;
                int dequeSize = int.Parse(dequeSizeExpr.Value);

                // Offset
                Expression offsetExpr = debugger.GetExpression(OffsetStr(name));
                if (!offsetExpr.IsValidValue)
                    return false;
                int offset = int.Parse(offsetExpr.Value);
                    
                // Initial indexes
                int firstBlock = ((0 + offset) / dequeSize) % mapSize;
                int firstElement = (0 + offset) % dequeSize;
                int backBlock = (((size - 1) + offset) / dequeSize) % mapSize;
                int backElement = ((size - 1) + offset) % dequeSize;
                int blocksCount = firstBlock <= backBlock
                                ? backBlock - firstBlock + 1
                                : mapSize - firstBlock + backBlock + 1;

                int globalIndex = 0;
                for (int i = 0; i < blocksCount; ++i)
                {
                    int blockIndex = (firstBlock + i) % mapSize;
                    ulong address = pointers[blockIndex];
                    if (address != 0) // just in case
                    {
                        int elemIndex = (i == 0)
                                        ? firstElement
                                        : 0;
                        int blockSize = dequeSize - elemIndex;
                        if (i == blocksCount - 1) // last block
                            blockSize -= dequeSize - (backElement + 1);
                            
                        if (blockSize > 0) // just in case
                        {
                            MemoryReader.ArrayConverter<double>
                                arrayConverter = new MemoryReader.ArrayConverter<double>(elementConverter, blockSize);
                            if (arrayConverter == null)
                                return false;

                            int valuesCount = elementConverter.ValueCount() * blockSize;
                            ulong firstAddress = address + (ulong)(elemIndex * elementConverter.ByteSize());

                            double[] values = new double[valuesCount];
                            if (!MemoryReader.Read(debugger, firstAddress, values, arrayConverter))
                                return false;

                            if (!memoryBlockPredicate(values))
                                return false;

                            globalIndex += blockSize;
                        }
                    }
                }

                return true;
            }

            public override int LoadSize(Debugger debugger, string name)
            {
                Expression expr = debugger.GetExpression(SizeStr(name));
                return expr.IsValidValue
                     ? Math.Max(ParseInt(expr.Value), 0)
                     : 0;
            }

            private string MapSizeStr(string name)
            {
                return version == Version.Msvc12
                     ? name + "._Mapsize"
                     : name + "._Mypair._Myval2._Mapsize";
            }

            private string MapStr(string name)
            {
                return version == Version.Msvc12
                     ? name + "._Map"
                     : name + "._Mypair._Myval2._Map";
            }

            private string OffsetStr(string name)
            {
                return version == Version.Msvc12
                     ? name + "._Myoff"
                     : name + "._Mypair._Myval2._Myoff";
            }

            private string ElementStr(string name, int i)
            {
                return version == Version.Msvc12
                     ? name + "._Map[((" + i + " + " + name + "._Myoff) / " + name + "._EEN_DS) % " + name + "._Mapsize][(" + i + " + " + name + "._Myoff) % " + name + "._EEN_DS]"
                     : name + "._Mypair._Myval2._Map[((" + i + " + " + name + "._Mypair._Myval2._Myoff) / " + name + "._EEN_DS) % " + name + "._Mypair._Myval2._Mapsize][(" + i + " + " + name + "._Mypair._Myval2._Myoff) % " + name + "._EEN_DS]";
            }

            private string SizeStr(string name)
            {
                return version == Version.Msvc12
                     ? name + "._Mysize"
                     : name + "._Mypair._Myval2._Mysize";
            }

            public override void Initialize(Debugger debugger, string name)
            {
                string name12 = name + "._Mysize";
                //string name14_15 = name + "._Mypair._Myval2._Mysize";

                if (debugger.GetExpression(name12).IsValidValue)
                    version = Version.Msvc12;
            }
            private enum Version { Unknown, Msvc12, Msvc14_15 };
            private Version version = Version.Msvc14_15;
        }

        class StdList : ValuesContainer
        {
            public override string Id() { return "std::list"; }

            public override string ElementPtrName(string name)
            {
                return "(&" + HeadStr(name) + "->_Next->_Myval" + ")";
            }

            public override bool ForEachMemoryBlock(Debugger debugger, string name,
                                                    MemoryReader.Converter<double> elementConverter,
                                                    MemoryBlockPredicate memoryBlockPredicate)
            {
                int size = LoadSize(debugger, name);
                if (size <= 0)
                    return true;

                string nextName = HeadStr(name) + "->_Next";
                string nextNextPtrName = "(&" + HeadStr(name) + "->_Next->_Next)";
                string nextValPtrName = "(&" + HeadStr(name) + "->_Next->_Myval)";

                string nextPtrName = "(&" + nextName + ")";
                MemoryReader.ValueConverter<ulong> nextConverter = MemoryReader.GetPointerConverter(debugger, nextPtrName, null);
                if (nextConverter == null)
                    return false;

                long nextDiff = MemoryReader.GetAddressDifference(debugger, nextName, nextNextPtrName);
                long valDiff = MemoryReader.GetAddressDifference(debugger, nextName, nextValPtrName);
                if (MemoryReader.IsInvalidAddressDifference(nextDiff)
                 || MemoryReader.IsInvalidAddressDifference(valDiff)
                 || nextDiff < 0 || valDiff < 0)
                    return false;

                ulong[] nextTmp = new ulong[1];
                ulong next = 0;

                for (int i = 0; i < size; ++i)
                {
                    bool ok = next == 0
                            ? MemoryReader.Read(debugger, nextPtrName, nextTmp, nextConverter)
                            : MemoryReader.Read(debugger, next + (ulong)nextDiff, nextTmp, nextConverter);
                    if (!ok)
                        return false;
                    next = nextTmp[0];

                    double[] values = new double[elementConverter.ValueCount()];
                    if (!MemoryReader.Read(debugger, next + (ulong)valDiff, values, elementConverter))
                        return false;

                    if (!memoryBlockPredicate(values))
                        return false;
                }

                return true;
            }

            public override int LoadSize(Debugger debugger, string name)
            {
                Expression expr = debugger.GetExpression(SizeStr(name));
                return expr.IsValidValue
                     ? Math.Max(ParseInt(expr.Value), 0)
                     : 0;
            }

            public override bool ForEachElement(Debugger debugger, string name, ElementPredicate elementPredicate)
            {
                int size = this.LoadSize(debugger, name);
                
                string nodeName = HeadStr(name) + "->_Next";
                for (int i = 0; i < size; ++i, nodeName += "->_Next")
                {
                    string elName = nodeName + "->_Myval";
                    if (!elementPredicate(elName))
                        return false;
                }
                return true;
            }

            private string HeadStr(string name)
            {
                return version == Version.Msvc12
                     ? name + "._Myhead"
                     : name + "._Mypair._Myval2._Myhead";
            }

            private string SizeStr(string name)
            {
                return version == Version.Msvc12
                     ? name + "._Mysize"
                     : name + "._Mypair._Myval2._Mysize";
            }

            public override void Initialize(Debugger debugger, string name)
            {
                string name12 = name + "._Mysize";
                //string name14_15 = name + "._Mypair._Myval2._Mysize";

                if (debugger.GetExpression(name12).IsValidValue)
                    version = Version.Msvc12;
            }
            private enum Version { Unknown, Msvc12, Msvc14_15 };
            private Version version = Version.Msvc14_15;
        }

        class BVariant : Loader
        {
            public override string Id() { return "boost::variant"; }
            public override ExpressionLoader.Kind Kind() { return ExpressionLoader.Kind.Variant; }

            public override void Load(Loaders loaders, bool accessMemory,
                                      Debugger debugger, string name, string type,
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

                Loader loader = loaders.FindByType(storedName, storedType);
                if (loader == null)
                    return;

                loader.Load(loaders, accessMemory, debugger, storedName, storedType, out traits, out result);
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

            public override void Load(Loaders loaders, bool accessMemory,
                                      Debugger debugger, string name, string type,
                                      out Geometry.Traits traits,
                                      out ExpressionDrawer.Turn result)
            {
                traits = null;
                result = null;

                List<string> tparams = Util.Tparams(type);
                if (tparams.Count < 1)
                    return;

                string pointType = tparams[0];
                PointLoader pointLoader = (PointLoader)loaders.FindByType(ExpressionLoader.Kind.Point,
                                                                          name + ".point",
                                                                          pointType);
                if (pointLoader == null)
                    return;

                ExpressionDrawer.Point p = null;
                pointLoader.Load(loaders, accessMemory, debugger, name + ".point", pointType, out traits, out p);
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

            public override void Load(Loaders loaders, bool accessMemory,
                                      Debugger debugger, string name, string type,
                                      out Geometry.Traits traits,
                                      out ExpressionDrawer.TurnsContainer result)
            {
                traits = null;
                result = null;

                ContainerLoader containerLoader = loaders.FindByType(ExpressionLoader.Kind.Container,
                                                                     name,
                                                                     type) as ContainerLoader;
                if (containerLoader == null)
                    return;

                List<string> tparams = Util.Tparams(type);
                if (tparams.Count < 1)
                    return;

                // TODO: Get element type from ContainerLoader instead?
                string turnType = tparams[0];
                BGTurn turnLoader = loaders.FindByType(ExpressionLoader.Kind.Turn,
                                                       containerLoader.ElementName(name),
                                                       turnType) as BGTurn;
                if (turnLoader == null)
                    return;

                Geometry.Traits t = null;
                List<ExpressionDrawer.Turn> turns = new List<ExpressionDrawer.Turn>();
                bool ok = containerLoader.ForEachElement(debugger, name, delegate (string elName)
                {
                    ExpressionDrawer.Turn turn = null;
                    turnLoader.Load(loaders, accessMemory, debugger, elName, turnType, out t, out turn);
                    if (turn == null)
                        return false;
                    turns.Add(turn);
                    return true;
                });
                if (ok)
                {
                    traits = t;
                    result = new ExpressionDrawer.TurnsContainer(turns);
                }
            }

            string id;
        }

        class StdPairPoint : PointLoader
        {
            public override string Id() { return "std::pair"; }

            public override Geometry.Traits LoadTraits(string type)
            {
                return new Geometry.Traits(2, Geometry.CoordinateSystem.Cartesian, Geometry.Unit.None);
            }

            protected override ExpressionDrawer.Point LoadPointParsed(Debugger debugger, string name, string type)
            {
                bool okx = true, oky = true;
                double x = 0, y = 0;
                x = LoadAsDouble(debugger, name + ".first", out okx);
                y = LoadAsDouble(debugger, name + ".second", out oky);
                return IsOk(okx, oky)
                     ? new ExpressionDrawer.Point(x, y)
                     : null;
            }

            protected override ExpressionDrawer.Point LoadPointMemory(Debugger debugger, string name, string type)
            {
                MemoryReader.Converter<double> converter = GetMemoryConverter(debugger, name, type);
                if (converter != null)
                {
                    if (converter.ValueCount() != 2)
                        throw new ArgumentOutOfRangeException("converter.ValueCount()");

                    double[] values = new double[2];
                    if (MemoryReader.Read(debugger, "(&" + name + ")", values, converter))
                    {
                        return new ExpressionDrawer.Point(values[0], values[1]);
                    }
                }
                return null;
            }

            public override MemoryReader.Converter<double> GetMemoryConverter(Debugger debugger, string name, string type)
            {
                List<string> tparams = Util.Tparams(type);
                if (tparams.Count < 2)
                    return null;
                string firstType = tparams[0];
                string secondType = tparams[1];
                string ptrName = "(&" + name + ")";
                string ptrFirst = "(&" + name + ".first)";
                string ptrSecond = "(&" + name + ".second)";
                long firstOffset = MemoryReader.GetAddressDifference(debugger, ptrName, ptrFirst);
                long secondOffset = MemoryReader.GetAddressDifference(debugger, ptrName, ptrSecond);
                if (MemoryReader.IsInvalidAddressDifference(firstOffset)
                 || MemoryReader.IsInvalidAddressDifference(secondOffset))
                    return null;
                MemoryReader.Converter<double> firstConverter = MemoryReader.GetNumericArrayConverter(debugger, ptrFirst, firstType, 1);
                MemoryReader.Converter<double> secondConverter = MemoryReader.GetNumericArrayConverter(debugger, ptrSecond, secondType, 1);
                if (firstConverter == null || secondConverter == null)
                    return null;
                int sizeOfPair = MemoryReader.GetValueTypeSizeof(debugger, type);
                if (sizeOfPair == 0)
                    return null;
                return new MemoryReader.StructConverter(
                            sizeOfPair,
                            new MemoryReader.Member(firstConverter, (int)firstOffset),
                            new MemoryReader.Member(secondConverter, (int)secondOffset));
            }
        }

        class StdComplexPoint : BXPoint
        {
            public override string Id() { return "std::complex"; }

            public override Geometry.Traits LoadTraits(string type)
            {
                return new Geometry.Traits(2, Geometry.CoordinateSystem.Complex, Geometry.Unit.None);
            }

            protected override ExpressionDrawer.Point LoadPointParsed(Debugger debugger, string name, string type)
            {
                return LoadPointParsed(debugger, name, type, name + "._Val", 2);
            }

            protected override ExpressionDrawer.Point LoadPointMemory(Debugger debugger, string name, string type)
            {
                return LoadPointMemory(debugger, name, type, name + "._Val", 2);
            }

            public override MemoryReader.Converter<double> GetMemoryConverter(Debugger debugger, string name, string type)
            {
                List<string> tparams = Util.Tparams(type);
                if (tparams.Count < 1)
                    return null;
                string coordType = tparams[0];

                return GetMemoryConverter(debugger, name, name + "._Val", coordType, 2);
            }
        }

        class PointContainer : BGRange<ExpressionDrawer.MultiPoint>
        {
            public PointContainer(string id)
                : base(ExpressionLoader.Kind.MultiPoint, id, 0, -1)
            {}

            public override TypeConstraint Constraint() { return new TparamKindConstraint(0, ExpressionLoader.Kind.Point); }
        }

        class PointCArray : PointRange<ExpressionDrawer.MultiPoint>
        {
            public class PointKindConstraint : TypeConstraint
            {
                public override bool Ok(Loaders loaders, string name, string type)
                {
                    string elementType;
                    int size = 0;
                    if (CArray.NameSizeFromType(type, out elementType, out size))
                    {
                        Loader loader = loaders.FindByType("(*(" + CArray.RawNameFromName(name) + "))",
                                                           elementType);
                        if (loader != null)
                            return loader.Kind() == ExpressionLoader.Kind.Point;
                    }
                    return false;
                }
            }

            public PointCArray()
                : base(ExpressionLoader.Kind.MultiPoint)
            {}

            public override TypeConstraint Constraint() { return new PointKindConstraint(); }

            public override string Id() { return null; }

            public override bool MatchType(string type, string id)
            {
                string dummy;
                int size = 0;
                return CArray.NameSizeFromType(type, out dummy, out size);
            }

            public override void Load(Loaders loaders, bool accessMemory,
                                      Debugger debugger, string name, string type,
                                      out Geometry.Traits traits,
                                      out ExpressionDrawer.MultiPoint result)
            {
                traits = null;
                result = null;

                string pointType;
                int size;
                if (!CArray.NameSizeFromType(type, out pointType, out size))
                    return;

                string pointName = "(*(" + CArray.RawNameFromName(name) + "))";

                PointLoader pointLoader = loaders.FindByType(ExpressionLoader.Kind.Point,
                                                             pointName,
                                                             pointType) as PointLoader;
                if (pointLoader == null)
                    return;

                traits = pointLoader.LoadTraits(pointType);
                if (traits == null)
                    return;

                ContainerLoader containerLoader = loaders.FindByType(ExpressionLoader.Kind.Container,
                                                                     name,
                                                                     type) as ContainerLoader;
                if (containerLoader == null)
                    return;

                if (accessMemory)
                {
                    result = LoadMemory(debugger, name, type,
                                        pointType, pointLoader, containerLoader);
                }

                if (result == null)
                {
                    result = LoadParsed(accessMemory, debugger, name, type,
                                        pointType, pointLoader, containerLoader);
                }
            }
        }
    }
}
