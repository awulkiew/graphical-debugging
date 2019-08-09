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
        private Loaders loadersCpp;
        private Loaders loadersCS;

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

            loadersCpp = new Loaders();

            loadersCpp.Add(new BGPoint());
            loadersCpp.Add(new BGPointXY());
            loadersCpp.Add(new BGSegment());
            loadersCpp.Add(new BGReferringSegment());
            loadersCpp.Add(new BGBox());
            loadersCpp.Add(new BGNSphere());
            loadersCpp.Add(new BGMultiPoint());
            loadersCpp.Add(new BGLinestring());
            loadersCpp.Add(new BGMultiLinestring());
            loadersCpp.Add(new BGRing());
            loadersCpp.Add(new BGPolygon());
            loadersCpp.Add(new BGMultiPolygon());
            loadersCpp.Add(new BGBufferedRing());
            loadersCpp.Add(new BGBufferedRingCollection());

            loadersCpp.Add(new BPPoint());
            loadersCpp.Add(new BPSegment());
            loadersCpp.Add(new BPBox());
            loadersCpp.Add(new BPRing());
            loadersCpp.Add(new BPPolygon());

            loadersCpp.Add(new StdPairPoint());
            loadersCpp.Add(new StdComplexPoint());

            loadersCpp.Add(new BVariant());

            loadersCpp.Add(new StdArray());
            loadersCpp.Add(new BoostArray());
            loadersCpp.Add(new BGVarray());
            loadersCpp.Add(new BoostContainerVector());
            loadersCpp.Add(new BoostContainerStaticVector());
            loadersCpp.Add(new StdVector());
            loadersCpp.Add(new StdDeque());
            loadersCpp.Add(new StdList());

            loadersCpp.Add(new CArray());
            loadersCpp.Add(new PointCArray());

            loadersCpp.Add(new PointContainer("std::array"));
            loadersCpp.Add(new PointContainer("boost::array"));
            loadersCpp.Add(new PointContainer("boost::geometry::index::detail::varray"));
            loadersCpp.Add(new PointContainer("boost::container::vector"));
            loadersCpp.Add(new PointContainer("boost::container::static_vector"));
            loadersCpp.Add(new PointContainer("std::vector"));
            loadersCpp.Add(new PointContainer("std::deque"));
            loadersCpp.Add(new PointContainer("std::list"));

            loadersCpp.Add(new BGTurn("boost::geometry::detail::overlay::turn_info"));
            loadersCpp.Add(new BGTurn("boost::geometry::detail::overlay::traversal_turn_info"));
            loadersCpp.Add(new BGTurn("boost::geometry::detail::buffer::buffer_turn_info"));
            loadersCpp.Add(new BGTurnContainer("std::vector"));
            loadersCpp.Add(new BGTurnContainer("std::deque"));

            loadersCS = new Loaders();

            loadersCS.Add(new CSList());

            loadersCS.Add(new CSArray());
            loadersCS.Add(new PointCSArray());

            loadersCS.Add(new PointContainer("System.Collections.Generic.List"));
        }

        public static Debugger Debugger
        {
            get { return Instance.debugger; }
        }

        public static DebuggerEvents DebuggerEvents
        {
            get { return Instance.debuggerEvents; }
        }

        public static Expression[] GetExpressions(string name, char separator = ';')
        {
            var expr = Debugger.GetExpression(name);
            if (expr.IsValidValue)
                return new Expression[] { expr };

            string[] subnames = name.Split(separator);
            Expression[] exprs = new Expression[subnames.Length];
            for (int i = 0; i < subnames.Length; ++i)
            {
                exprs[i] = Debugger.GetExpression(subnames[i]);
            }

            return exprs;
        }

        public static bool AllValidValues(Expression[] exprs)
        {
            foreach(Expression e in exprs)
                if (!e.IsValidValue)
                    return false;
            return true;
        }

        public static bool AnyValidValue(Expression[] exprs)
        {
            foreach (Expression e in exprs)
                if (e.IsValidValue)
                    return true;
            return false;
        }

        public static string TypeFromExpressions(Expression[] exprs)
        {
            string result = "";
            bool first = true;
            foreach (Expression e in exprs)
            {
                if (first)
                    first = false;
                else
                    result += " ; ";
                result += e.Type;
            }
            return result;
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

        /// <summary>
        /// Loads object into watch to visualize.
        /// </summary>
        /// <param name="name">Name of variable or actual expression added to watch</param>
        /// <param name="traits"></param>
        /// <param name="result"></param>
        public static void Load(string name,
                                out Geometry.Traits traits,
                                out ExpressionDrawer.IDrawable result)
        {
            Load(name, AllKinds, out traits, out result);
        }

        /// <summary>
        /// Loads object into watch to visualize.
        /// </summary>
        /// <param name="name">Name of variable or actual expression added to watch</param>
        /// <param name="kindConstraint"></param>
        /// <param name="traits"></param>
        /// <param name="result"></param>
        public static void Load(string name,
                                KindConstraint kindConstraint,
                                out Geometry.Traits traits,
                                out ExpressionDrawer.IDrawable result)
        {
            traits = null;
            result = null;

            Expression[] exprs = GetExpressions(name);
            if (exprs.Length < 1 || ! AllValidValues(exprs))
                return;

            string language = Instance.debugger.CurrentStackFrame.Language;
            Loaders loaders = language == "C#" ? Instance.loadersCS : Instance.loadersCpp;

            MemoryReader mreader = null;
            GeneralOptionPage optionPage = Util.GetDialogPage<GeneralOptionPage>();
            if (optionPage == null || optionPage.EnableDirectMemoryAccess)
            {
                mreader = new MemoryReader(Instance.debugger);
            }

            if (exprs.Length == 1)
            {
                Loader loader = loaders.FindByType(exprs[0].Name, exprs[0].Type);
                if (loader == null)
                    return;

                if (!kindConstraint.Check(loader.Kind()))
                    return;

                loader.Load(loaders, mreader, Instance.debugger,
                            exprs[0].Name, exprs[0].Type,
                            out traits, out result);
            }
            else //if (exprs.Length > 1)
            {
                // For now there is only one loader which can handle this case
                CoordinatesContainers loader = new CoordinatesContainers();

                loader.Load(loaders, mreader, Instance.debugger,
                            exprs,
                            out traits, out result);
            }
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

            /// <summary>
            /// Finds loader by given C++ or C# type.
            /// </summary>
            /// <param name="name">Name of variable or actual expression added to watch</param>
            /// <param name="type">C++ or C# type of variable</param>
            /// <returns></returns>
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

            public void RemoveUserDefined()
            {
                foreach (List<Loader> li in lists)
                {
                    List<Loader> removeList = new List<Loader>();
                    foreach (Loader l in li)
                        if (l.IsUserDefined())
                            removeList.Add(l);
                    foreach (Loader l in removeList)
                        li.Remove(l);
                }                
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
            virtual public bool IsUserDefined()
            {
                return false;
            }

            abstract public string Id();

            /// <summary>
            /// Matches type by type ID.
            /// Type and identifier can both receove the same value e.g. unsigned char[4]
            /// </summary>
            /// <param name="type">Name of type</param>
            /// <param name="id">Idenifier of type</param>
            /// <returns></returns>
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

            abstract public void Load(Loaders loaders,
                                      MemoryReader mreader, Debugger debugger,
                                      string name, string type,
                                      out Geometry.Traits traits,
                                      out ExpressionDrawer.IDrawable result);

            virtual public MemoryReader.Converter<double> GetMemoryConverter(MemoryReader mreader,
                                                                             string name, string type)
            {
                return null;
            }
        }

        abstract class LoaderR<ResultType> : Loader
            where ResultType : ExpressionDrawer.IDrawable
        {
            public override void Load(Loaders loaders,
                                      MemoryReader mreader, Debugger debugger,
                                      string name, string type,
                                      out Geometry.Traits traits,
                                      out ExpressionDrawer.IDrawable result)
            {
                ResultType res = default(ResultType);
                this.Load(loaders, mreader, debugger, name, type, out traits, out res);
                result = res;
            }

            abstract public void Load(Loaders loaders,
                                      MemoryReader mreader, Debugger debugger,
                                      string name, string type,
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
            
            public override void Load(Loaders loaders, MemoryReader mreader,
                                      Debugger debugger, string name, string type,
                                      out Geometry.Traits traits,
                                      out ExpressionDrawer.Point point)
            {
                traits = LoadTraits(type);
                point = traits != null
                      ? LoadPoint(mreader, debugger, name, type)
                      : null;
            }

            abstract public Geometry.Traits LoadTraits(string type);

            virtual public ExpressionDrawer.Point LoadPoint(MemoryReader mreader, Debugger debugger, string name, string type)
            {
                ExpressionDrawer.Point result = null;
                if (mreader != null)
                    result = LoadPointMemory(mreader, name, type);
                if (result == null)
                    result = LoadPointParsed(debugger, name, type);
                return result;
            }
            abstract protected ExpressionDrawer.Point LoadPointParsed(Debugger debugger, string name, string type);
            abstract protected ExpressionDrawer.Point LoadPointMemory(MemoryReader mreader, string name, string type);
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

            // TODO: This method should probably return ulong
            abstract public int LoadSize(Debugger debugger, string name);

            public delegate bool ElementPredicate(string elementName);
            abstract public bool ForEachElement(Debugger debugger, string name, ElementPredicate elementPredicate);

            // ForEachMemoryBlock calling ReadArray taking ElementLoader returned by ContainerLoader
            // With ReadArray knowing which memory copying optimizations can be made based on ElementLoader's type
            // Or not

            abstract public string ElementName(string name, string elemType);
            public delegate bool MemoryBlockPredicate(double[] values);
            abstract public bool ForEachMemoryBlock(MemoryReader mreader, string name, string type,
                                                    MemoryReader.Converter<double> elementConverter,
                                                    MemoryBlockPredicate memoryBlockPredicate);
        }

        abstract class BXPoint : PointLoader
        {
            protected ExpressionDrawer.Point LoadPointParsed(Debugger debugger, string name, string type, string ptrName, int count)
            {
                bool okx = true, oky = true;
                double x = 0, y = 0;
                if (count > 0)
                    okx = TryLoadAsDoubleParsed(debugger, ptrName + "[0]", out x);
                if (count > 1)
                    oky = TryLoadAsDoubleParsed(debugger, ptrName + "[1]", out y);
                return Util.IsOk(okx, oky)
                     ? new ExpressionDrawer.Point(x, y)
                     : null;
            }

            protected ExpressionDrawer.Point LoadPointMemory(MemoryReader mreader, string name, string type, string ptrName, int count)
            {
                double[] values = new double[count];
                if (mreader.ReadNumericArray(ptrName + "[0]", values))
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

            protected MemoryReader.Converter<double> GetMemoryConverter(MemoryReader mreader, string name, string memberArray, string elemType, int count)
            {
                string elemName = memberArray + "[0]";
                MemoryReader.Converter<double> arrayConverter
                    = mreader.GetNumericArrayConverter(elemName, elemType, count);
                int byteSize = mreader.GetValueSizeof(name);
                if (byteSize == 0)
                    return null;
                long byteOffset = mreader.GetAddressDifference(name, elemName);
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
                    int dimension = int.Parse(tparams[1]);
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
                int dimension = int.Parse(tparams[1]);
                int count = Math.Min(dimension, 2);
                return LoadPointParsed(debugger, name, type, name + ".m_values", count);
            }

            protected override ExpressionDrawer.Point LoadPointMemory(MemoryReader mreader, string name, string type)
            {
                List<string> tparams = Util.Tparams(type);
                if (tparams.Count < 2)
                    return null;
                int dimension = int.Parse(tparams[1]);
                int count = Math.Min(dimension, 2);
                return LoadPointMemory(mreader, name, type, name + ".m_values", count);
            }

            public override MemoryReader.Converter<double> GetMemoryConverter(MemoryReader mreader, string name, string type)
            {
                List<string> tparams = Util.Tparams(type);
                if (tparams.Count < 2)
                    return null;
                string coordType = tparams[0];
                int dimension = int.Parse(tparams[1]);
                int count = Math.Min(dimension, 2);
                return GetMemoryConverter(mreader, name, name + ".m_values", coordType, count);
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

            protected override ExpressionDrawer.Point LoadPointMemory(MemoryReader mreader, string name, string type)
            {
                return LoadPointMemory(mreader, name, type, name + ".m_values", 2);
            }

            public override MemoryReader.Converter<double> GetMemoryConverter(MemoryReader mreader, string name, string type)
            {
                List<string> tparams = Util.Tparams(type);
                if (tparams.Count < 1)
                    return null;
                string coordType = tparams[0];

                return GetMemoryConverter(mreader, name, name + ".m_values", coordType, 2);
            }
        }

        class BGBox : BoxLoader
        {
            public override string Id() { return "boost::geometry::model::box"; }

            public override void Load(Loaders loaders, MemoryReader mreader, Debugger debugger,
                                      string name, string type,
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

                Geometry.Point fp = pointLoader.LoadPoint(mreader, debugger, m_min_corner, pointType);
                Geometry.Point sp = pointLoader.LoadPoint(mreader, debugger, m_max_corner, pointType);

                result = Util.IsOk(fp, sp)
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

            public override void Load(Loaders loaders, MemoryReader mreader, Debugger debugger,
                                      string name, string type,
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

                Geometry.Point fp = pointLoader.LoadPoint(mreader, debugger, first, pointType);
                Geometry.Point sp = pointLoader.LoadPoint(mreader, debugger, second, pointType);

                segment = Util.IsOk(fp, sp)
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

            public override void Load(Loaders loaders, MemoryReader mreader, Debugger debugger,
                                      string name, string type,
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

                Geometry.Point center = pointLoader.LoadPoint(mreader, debugger,
                                                              m_center, pointType);
                double radius = 0;
                bool ok = TryLoadAsDoubleParsed(debugger, m_radius, out radius);

                result = Util.IsOk(center, ok)
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

            protected ResultType LoadParsed(MemoryReader mreader, // should this be passed?
                                            Debugger debugger, string name, string type,
                                            string pointType,
                                            PointLoader pointLoader,
                                            ContainerLoader containerLoader)
            {
                ResultType result = new ResultType();
                containerLoader.ForEachElement(debugger, name, delegate (string elName)
                {
                    ExpressionDrawer.Point p = pointLoader.LoadPoint(mreader, debugger, elName, pointType);
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

            protected ResultType LoadMemory(MemoryReader mreader,
                                            string name, string type,
                                            string pointType,
                                            PointLoader pointLoader,
                                            ContainerLoader containerLoader)
            {
                ResultType result = null;

                string pointName = containerLoader.ElementName(name, pointType);
                if (pointName != null)
                {
                    MemoryReader.Converter<double> pointConverter = pointLoader.GetMemoryConverter(mreader, pointName, pointType);
                    if (pointConverter != null)
                    {
                        int dimension = pointConverter.ValueCount();
                        result = new ResultType();
                        bool ok = containerLoader.ForEachMemoryBlock(mreader, name, type, pointConverter,
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

            /// <summary>
            /// Qualified name as identifier of C++ or C# type (e.g. std::vector)
            /// </summary>
            public override string Id() { return id; }

            public override void Load(Loaders loaders, MemoryReader mreader, Debugger debugger,
                                      string name, string type,
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
                                                             containerLoader.ElementName(name, pointType),
                                                             pointType) as PointLoader;
                if (pointLoader == null)
                    return;

                traits = pointLoader.LoadTraits(pointType);
                if (traits == null)
                    return;

                if (mreader != null)
                {
                    result = LoadMemory(mreader, name, type,
                                        pointType, pointLoader, containerLoader);
                }

                if (result == null)
                {
                    result = LoadParsed(mreader, debugger, name, type,
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

            public override void Load(Loaders loaders, MemoryReader mreader, Debugger debugger,
                                      string name, string type,
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
                                                  containerLoader.ElementName(name, lsType),
                                                  lsType) as RangeLoader<ExpressionDrawer.Linestring>;
                if (lsLoader == null)
                    return;

                Geometry.Traits t = null;
                ExpressionDrawer.MultiLinestring mls = new ExpressionDrawer.MultiLinestring();
                bool ok = containerLoader.ForEachElement(debugger, name, delegate (string elName)
                {
                    ExpressionDrawer.Linestring ls = null;
                    lsLoader.Load(loaders, mreader, debugger, elName, lsType, out t, out ls);
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

            public override void Load(Loaders loaders, MemoryReader mreader, Debugger debugger,
                                      string name, string type,
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
                outerLoader.Load(loaders, mreader, debugger, outerName, outerExpr.Type, out traits, out outer);
                if (outer == null)
                    return;

                Geometry.Traits t = null;
                List<Geometry.Ring> inners = new List<Geometry.Ring>();
                bool ok = innersLoader.ForEachElement(debugger, innersName, delegate (string elName)
                {
                    ExpressionDrawer.Ring inner = null;
                    outerLoader.Load(loaders, mreader, debugger, elName, outerExpr.Type, out t, out inner);
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

            public override void Load(Loaders loaders, MemoryReader mreader, Debugger debugger,
                                      string name, string type,
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
                                                              containerLoader.ElementName(name, polyType),
                                                              polyType) as PolygonLoader;
                if (polyLoader == null)
                    return;

                Geometry.Traits t = null;
                ExpressionDrawer.MultiPolygon mpoly = new ExpressionDrawer.MultiPolygon();
                bool ok = containerLoader.ForEachElement(debugger, name, delegate (string elName)
                {
                    ExpressionDrawer.Polygon poly = null;
                    polyLoader.Load(loaders, mreader, debugger, elName, polyType, out t, out poly);
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

            public override void Load(Loaders loaders, MemoryReader mreader, Debugger debugger,
                                      string name, string type,
                                      out Geometry.Traits traits,
                                      out ExpressionDrawer.Ring result)
            {
                traits = null;
                result = null;

                List<string> tparams = Util.Tparams(type);
                if (tparams.Count < 1)
                    return;

                string ringType = tparams[0];
                RangeLoader<ExpressionDrawer.Ring>
                    ringLoader = loaders.FindByType(ExpressionLoader.Kind.Ring,
                                                    name,
                                                    ringType) as RangeLoader<ExpressionDrawer.Ring>;
                if (ringLoader == null)
                    return;

                ringLoader.Load(loaders, mreader, debugger, name, ringType, out traits, out result);
            }
        }

        // NOTE: There is no MultiRing concept so use MultiPolygon for now
        class BGBufferedRingCollection : RangeLoader<ExpressionDrawer.MultiPolygon>
        {
            public BGBufferedRingCollection() : base(ExpressionLoader.Kind.MultiPolygon) { }

            public override string Id() { return "boost::geometry::detail::buffer::buffered_ring_collection"; }

            public override void Load(Loaders loaders, MemoryReader mreader, Debugger debugger,
                                      string name, string type,
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
                                                    containerLoader.ElementName(name, ringType),
                                                    ringType) as RangeLoader<ExpressionDrawer.Ring>;
                if (ringLoader == null)
                    return;
                
                Geometry.Traits t = null;
                ExpressionDrawer.MultiPolygon mpoly = new ExpressionDrawer.MultiPolygon();
                bool ok = containerLoader.ForEachElement(debugger, name, delegate (string elName)
                {
                    ExpressionDrawer.Ring ring = null;
                    ringLoader.Load(loaders, mreader, debugger, elName, ringType, out t, out ring);
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

            protected override ExpressionDrawer.Point LoadPointMemory(MemoryReader mreader, string name, string type)
            {
                return LoadPointMemory(mreader, name, type, name + ".coords_", 2);
            }

            public override MemoryReader.Converter<double> GetMemoryConverter(MemoryReader mreader, string name, string type)
            {
                List<string> tparams = Util.Tparams(type);
                if (tparams.Count < 1)
                    return null;
                string coordType = tparams[0];

                return GetMemoryConverter(mreader, name, name + ".coords_", coordType, 2);
            }
        }

        class BPSegment : SegmentLoader
        {
            public override string Id() { return "boost::polygon::segment_data"; }

            public override void Load(Loaders loaders, MemoryReader mreader, Debugger debugger,
                                      string name, string type,
                                      out Geometry.Traits traits,
                                      out ExpressionDrawer.Segment result)
            {
                traits = new Geometry.Traits(2, Geometry.CoordinateSystem.Cartesian, Geometry.Unit.None);
                result = null;

                double x0 = 0, y0 = 0, x1 = 0, y1 = 0;
                bool okx0 = TryLoadAsDoubleParsed(debugger, name + ".points_[0].coords_[0]", out x0);
                bool oky0 = TryLoadAsDoubleParsed(debugger, name + ".points_[0].coords_[1]", out y0);
                bool okx1 = TryLoadAsDoubleParsed(debugger, name + ".points_[1].coords_[0]", out x1);
                bool oky1 = TryLoadAsDoubleParsed(debugger, name + ".points_[1].coords_[1]", out y1);

                if (! Util.IsOk(okx0, oky0, okx1, oky1))
                    return;

                Geometry.Point first_p = new Geometry.Point(x0, y0);
                Geometry.Point second_p = new Geometry.Point(x1, y1);

                result = new ExpressionDrawer.Segment(first_p, second_p);
            }
        }

        class BPBox : BoxLoader
        {
            public override string Id() { return "boost::polygon::rectangle_data"; }

            public override void Load(Loaders loaders, MemoryReader mreader, Debugger debugger,
                                      string name, string type,
                                      out Geometry.Traits traits,
                                      out ExpressionDrawer.Box result)
            {
                traits = new Geometry.Traits(2, Geometry.CoordinateSystem.Cartesian, Geometry.Unit.None);
                result = null;

                double xl = 0, xh = 0, yl = 0, yh = 0;
                bool okxl = TryLoadAsDoubleParsed(debugger, name + ".ranges_[0].coords_[0]", out xl);
                bool okxh = TryLoadAsDoubleParsed(debugger, name + ".ranges_[0].coords_[1]", out xh);
                bool okyl = TryLoadAsDoubleParsed(debugger, name + ".ranges_[1].coords_[0]", out yl);
                bool okyh = TryLoadAsDoubleParsed(debugger, name + ".ranges_[1].coords_[1]", out yh);

                if (! Util.IsOk(okxl, okxh, okyl, okyh))
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

            public override void Load(Loaders loaders, MemoryReader mreader, Debugger debugger,
                                      string name, string type,
                                      out Geometry.Traits traits,
                                      out ExpressionDrawer.Ring result)
            {
                traits = new Geometry.Traits(2, Geometry.CoordinateSystem.Cartesian, Geometry.Unit.None);
                result = null;

                List<string> tparams = Util.Tparams(type);
                if (tparams.Count < 1)
                    return;

                string tparam = tparams[0].EndsWith(">") ? tparams[0] + ' ' : tparams[0];
                string pointType = "boost::polygon::point_data<" + tparam + ">";

                ContainerLoader containerLoader = loaders.FindById(ExpressionLoader.Kind.Container,
                                                                   name,
                                                                   "std::vector") as ContainerLoader;
                if (containerLoader == null)
                    return;

                BPPoint pointLoader = loaders.FindById(ExpressionLoader.Kind.Point,
                                                       containerLoader.ElementName(name, pointType),
                                                       "boost::polygon::point_data") as BPPoint;
                if (pointLoader == null)
                    return;

                string containerName = name + ".coords_";

                if (mreader != null)
                {
                    result = LoadMemory(mreader, containerName, type,
                                        pointType, pointLoader, containerLoader);
                }

                if (result == null)
                {
                    result = LoadParsed(mreader, debugger, containerName, type,
                                        pointType, pointLoader, containerLoader);
                }
            }
        }

        class BPPolygon : PolygonLoader
        {
            public override string Id() { return "boost::polygon::polygon_with_holes_data"; }

            public override void Load(Loaders loaders, MemoryReader mreader, Debugger debugger,
                                      string name, string type,
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
                outerLoader.Load(loaders, mreader, debugger,
                                 member_self_, polygonType,
                                 out traits, out outer);
                if (outer == null)
                    return;

                Geometry.Traits t = null;
                List<Geometry.Ring> holes = new List<Geometry.Ring>();
                bool ok = holesLoader.ForEachElement(debugger, member_holes_, delegate (string elName)
                {
                    ExpressionDrawer.Ring hole = null;
                    outerLoader.Load(loaders, mreader, debugger, elName, polygonType, out t, out hole);
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

            public override void Load(Loaders loaders, MemoryReader mreader, Debugger debugger,
                                      string name, string type,
                                      out Geometry.Traits traits,
                                      out ExpressionDrawer.ValuesContainer result)
            {
                traits = null;
                result = null;

                List<double> values = null;
                Load(loaders, mreader, debugger, name, type, out traits, out values);

                if (values != null)
                    result = new ExpressionDrawer.ValuesContainer(values);
            }

            public void Load(Loaders loaders, MemoryReader mreader, Debugger debugger,
                             string name, string type,
                             out Geometry.Traits traits,
                             out List<double> result)
            {
                traits = null;
                result = null;

                if (mreader != null)
                    LoadMemory(mreader, name, type, out result);

                if (result == null)
                    LoadParsed(debugger, name, out result);
            }

            protected void LoadMemory(MemoryReader mreader, string name, string type,
                                      out List<double> result)
            {
                result = null;

                string elemName = this.ElementName(name, ElementType(type));

                MemoryReader.ValueConverter<double>
                    valueConverter = mreader.GetNumericConverter(elemName, null);
                if (valueConverter == null)
                    return;

                List<double> list = new List<double>();
                bool ok = this.ForEachMemoryBlock(mreader, name, type, valueConverter,
                    delegate (double[] values)
                    {
                        foreach (double v in values)
                            list.Add(v);
                        return true;
                    });

                if (ok)
                    result = list;
            }

            protected void LoadParsed(Debugger debugger, string name, out List<double> result)
            {                
                result = null;
                int size = this.LoadSize(debugger, name);
                List<double> values = new List<double>();
                bool ok = this.ForEachElement(debugger, name, delegate (string elName)
                {
                    double value = 0;
                    bool okV = TryLoadAsDoubleParsed(debugger, elName, out value);
                    if (okV)
                        values.Add(value);
                    return okV;
                });

                if (ok)
                    result = values;
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
            public override bool ForEachMemoryBlock(MemoryReader mreader, string name, string type,
                                                    MemoryReader.Converter<double> elementConverter,
                                                    MemoryBlockPredicate memoryBlockPredicate)
            {
                return this.ForEachMemoryBlock(mreader,
                                               name, type,
                                               ElementName(name, ElementType(type)),
                                               elementConverter, memoryBlockPredicate);
            }

            protected bool ForEachMemoryBlock(MemoryReader mreader,
                                              string name, string type, string blockName,
                                              MemoryReader.Converter<double> elementConverter,
                                              MemoryBlockPredicate memoryBlockPredicate)
            {
                if (elementConverter == null)
                    return false;
                int size = LoadSize(mreader.Debugger, name);
                var blockConverter = new MemoryReader.ArrayConverter<double>(elementConverter, size);
                double[] values = new double[blockConverter.ValueCount()];
                if (! mreader.Read(blockName, values, blockConverter))
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

            public override string ElementName(string name, string elType)
            {
                return this.RandomAccessName(name) + "[0]";
            }

            public override string RandomAccessName(string name)
            {
                return RawNameFromName(name);
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
                // Detect Hex in case various versions displayed sizes differently
                return Util.TryParseInt(strSize, out size);
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
                    // Detect Hex in case various versions displayed sizes differently
                    if (Util.TryParseInt(strSize, out size))
                        result = name.Substring(0, commaPos);
                }
                return result;
            }
        }

        class StdArray : ContiguousContainer
        {
            public override string Id() { return "std::array"; }

            public override string ElementName(string name, string elType)
            {
                return name + "._Elems[0]";
            }

            public override int LoadSize(Debugger debugger, string name)
            {
                Expression expr = debugger.GetExpression(name);
                return expr.IsValidValue
                     ? Math.Max(int.Parse(Util.Tparams(expr.Type)[1]), 0)
                     : 0;
            }
        }

        class BoostArray : StdArray
        {
            public override string Id() { return "boost::array"; }

            public override string ElementName(string name, string elType)
            {
                return name + ".elems[0]";
            }
        }

        class BoostContainerVector : ContiguousContainer
        {
            public override string Id() { return "boost::container::vector"; }

            public override string ElementName(string name, string elType)
            {
                return name + ".m_holder.m_start[0]";
            }

            public override int LoadSize(Debugger debugger, string name)
            {
                return LoadSizeParsed(debugger, name + ".m_holder.m_size");
            }
        }

        class BoostContainerStaticVector : BoostContainerVector
        {
            public override string Id() { return "boost::container::static_vector"; }

            public override string ElementName(string name, string elType)
            {
                // TODO: The type-cast is needed here!!!
                // Although it's possible it will be ok since this is used only to pass a value starting the memory block
                // into the memory reader and type is not really important
                // and in other places like PointRange or BGRange correct type is passed with it
                // and based on this type the data is processed
                // It needs testing
                return "((" + elType + "*)" + name + ".m_holder.storage.data)[0]";
            }
        }

        class BGVarray : BoostContainerVector
        {
            public override string Id() { return "boost::geometry::index::detail::varray"; }

            public override string ElementName(string name, string elType)
            {
                // TODO: Check if type-cast is needed here
                return "((" + elType + "*)" + name + ".m_storage.data_.buf)[0]";
            }

            public override int LoadSize(Debugger debugger, string name)
            {
                return LoadSizeParsed(debugger, name + ".m_size");
            }
        }

        class StdVector : ContiguousContainer
        {
            public override string Id() { return "std::vector"; }

            public override string RandomAccessElementName(string rawName, int i)
            {
                return FirstStr(rawName) + "[" + i + "]";
            }

            public override string ElementName(string name, string elType)
            {
                return FirstStr(name) + "[0]";
            }

            public override int LoadSize(Debugger debugger, string name)
            {
                return LoadSizeParsed(debugger, SizeStr(name));
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

            public override string ElementName(string name, string elType)
            {
                return ElementStr(name, 0);
            }

            public override bool ForEachMemoryBlock(MemoryReader mreader, string name, string type,
                                                    MemoryReader.Converter<double> elementConverter,
                                                    MemoryBlockPredicate memoryBlockPredicate)
            {
                int size = LoadSize(mreader.Debugger, name);
                if (size == 0)
                    return true;

                // Map size
                int mapSize = 0;
                if (! TryLoadIntParsed(mreader.Debugger, MapSizeStr(name), out mapSize))
                    return false;

                // Map - array of pointers                
                ulong[] pointers = new ulong[mapSize];
                if (! mreader.ReadPointerArray(MapStr(name) + "[0]", pointers))
                    return false;

                // Block size
                int dequeSize = 0;
                if (! TryLoadIntParsed(mreader.Debugger, "((int)" + name + "._EEN_DS)", out dequeSize))
                    return false;

                // Offset
                int offset = 0;
                if (! TryLoadIntParsed(mreader.Debugger, OffsetStr(name), out offset))
                    return false;
                    
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
                            if (! mreader.Read(firstAddress, values, arrayConverter))
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
                return LoadSizeParsed(debugger, SizeStr(name));
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

            public override string ElementName(string name, string elType)
            {
                return HeadStr(name) + "->_Next->_Myval";
            }

            public override bool ForEachMemoryBlock(MemoryReader mreader, string name, string type,
                                                    MemoryReader.Converter<double> elementConverter,
                                                    MemoryBlockPredicate memoryBlockPredicate)
            {
                int size = LoadSize(mreader.Debugger, name);
                if (size <= 0)
                    return true;

                string nextName = HeadStr(name) + "->_Next";
                string nextNextName = HeadStr(name) + "->_Next->_Next";
                string nextValName = HeadStr(name) + "->_Next->_Myval";

                MemoryReader.ValueConverter<ulong> nextConverter = mreader.GetPointerConverter(nextName, null);
                if (nextConverter == null)
                    return false;

                long nextDiff = mreader.GetAddressDifference("(*" + nextName + ")", nextNextName);
                long valDiff = mreader.GetAddressDifference("(*" + nextName + ")", nextValName);
                if (MemoryReader.IsInvalidAddressDifference(nextDiff)
                 || MemoryReader.IsInvalidAddressDifference(valDiff)
                 || nextDiff < 0 || valDiff < 0)
                    return false;

                ulong[] nextTmp = new ulong[1];
                ulong next = 0;

                for (int i = 0; i < size; ++i)
                {
                    bool ok = next == 0
                            ? mreader.Read(nextName, nextTmp, nextConverter)
                            : mreader.Read(next + (ulong)nextDiff, nextTmp, nextConverter);
                    if (!ok)
                        return false;
                    next = nextTmp[0];

                    double[] values = new double[elementConverter.ValueCount()];
                    if (!mreader.Read(next + (ulong)valDiff, values, elementConverter))
                        return false;

                    if (!memoryBlockPredicate(values))
                        return false;
                }

                return true;
            }

            public override int LoadSize(Debugger debugger, string name)
            {
                return LoadSizeParsed(debugger, SizeStr(name));
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

            public override void Load(Loaders loaders, MemoryReader mreader, Debugger debugger,
                                      string name, string type,
                                      out Geometry.Traits traits,
                                      out ExpressionDrawer.IDrawable result)
            {
                traits = null;
                result = null;

                int which = 0;
                if (! TryLoadIntParsed(debugger, name + ".which_", out which))
                    return;

                List<string> tparams = Util.Tparams(type);
                if (which < 0 || tparams.Count <= which)
                    return;

                string storedType = tparams[which];
                string storedName = "(*(" + storedType + "*)" + name + ".storage_.data_.buf)";

                Loader loader = loaders.FindByType(storedName, storedType);
                if (loader == null)
                    return;

                loader.Load(loaders, mreader, debugger, storedName, storedType, out traits, out result);
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

            public override void Load(Loaders loaders, MemoryReader mreader, Debugger debugger,
                                      string name, string type,
                                      out Geometry.Traits traits,
                                      out ExpressionDrawer.Turn result)
            {
                traits = null;
                result = null;

                List<string> tparams = Util.Tparams(type);
                if (tparams.Count < 1)
                    return;

                string pointType = tparams[0];
                PointLoader pointLoader = loaders.FindByType(ExpressionLoader.Kind.Point,
                                                             name + ".point",
                                                             pointType) as PointLoader;
                if (pointLoader == null)
                    return;

                ExpressionDrawer.Point p = null;
                pointLoader.Load(loaders, mreader, debugger, name + ".point", pointType, out traits, out p);
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

            public override void Load(Loaders loaders, MemoryReader mreader, Debugger debugger,
                                      string name, string type,
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
                                                       containerLoader.ElementName(name, turnType),
                                                       turnType) as BGTurn;
                if (turnLoader == null)
                    return;

                Geometry.Traits t = null;
                List<ExpressionDrawer.Turn> turns = new List<ExpressionDrawer.Turn>();
                bool ok = containerLoader.ForEachElement(debugger, name, delegate (string elName)
                {
                    ExpressionDrawer.Turn turn = null;
                    turnLoader.Load(loaders, mreader, debugger, elName, turnType, out t, out turn);
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
                double x = 0, y = 0;
                bool okx = TryLoadAsDoubleParsed(debugger, name + ".first", out x);
                bool oky = TryLoadAsDoubleParsed(debugger, name + ".second", out y);
                return Util.IsOk(okx, oky)
                     ? new ExpressionDrawer.Point(x, y)
                     : null;
            }

            protected override ExpressionDrawer.Point LoadPointMemory(MemoryReader mreader, string name, string type)
            {
                MemoryReader.Converter<double> converter = GetMemoryConverter(mreader, name, type);
                if (converter != null)
                {
                    if (converter.ValueCount() != 2)
                        throw new ArgumentOutOfRangeException("converter.ValueCount()");

                    double[] values = new double[2];
                    if (mreader.Read(name, values, converter))
                    {
                        return new ExpressionDrawer.Point(values[0], values[1]);
                    }
                }
                return null;
            }

            public override MemoryReader.Converter<double> GetMemoryConverter(MemoryReader mreader, string name, string type)
            {
                List<string> tparams = Util.Tparams(type);
                if (tparams.Count < 2)
                    return null;
                string firstType = tparams[0];
                string secondType = tparams[1];
                string first = name + ".first";
                string second = name + ".second";
                long firstOffset = mreader.GetAddressDifference(name, first);
                long secondOffset = mreader.GetAddressDifference(name, second);
                if (MemoryReader.IsInvalidAddressDifference(firstOffset)
                 || MemoryReader.IsInvalidAddressDifference(secondOffset))
                    return null;
                MemoryReader.Converter<double> firstConverter = mreader.GetNumericArrayConverter(first, firstType, 1);
                MemoryReader.Converter<double> secondConverter = mreader.GetNumericArrayConverter(second, secondType, 1);
                if (firstConverter == null || secondConverter == null)
                    return null;
                int sizeOfPair = mreader.GetValueTypeSizeof(type);
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

            protected override ExpressionDrawer.Point LoadPointMemory(MemoryReader mreader, string name, string type)
            {
                return LoadPointMemory(mreader, name, type, name + "._Val", 2);
            }

            public override MemoryReader.Converter<double> GetMemoryConverter(MemoryReader mreader, string name, string type)
            {
                List<string> tparams = Util.Tparams(type);
                if (tparams.Count < 1)
                    return null;
                string coordType = tparams[0];

                return GetMemoryConverter(mreader, name, name + "._Val", coordType, 2);
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

            public override void Load(Loaders loaders, MemoryReader mreader,
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

                if (mreader != null)
                {
                    result = LoadMemory(mreader, name, type,
                                        pointType, pointLoader, containerLoader);
                }

                if (result == null)
                {
                    result = LoadParsed(mreader, debugger, name, type,
                                        pointType, pointLoader, containerLoader);
                }
            }
        }

        // This is the only one loader created manually right now
        // so the overrides below are not used
        class CoordinatesContainers : PointRange<ExpressionDrawer.MultiPoint>
        {
            public CoordinatesContainers()
                : base(ExpressionLoader.Kind.MultiPoint)
            { }

            public override string Id() { return null; }

            public override void Load(Loaders loaders,
                                      MemoryReader mreader, Debugger debugger,
                                      string name, string type,
                                      out Geometry.Traits traits,
                                      out ExpressionDrawer.MultiPoint result)
            {
                traits = null;
                result = null;
            }

            public void Load(Loaders loaders,
                             MemoryReader mreader, Debugger debugger,
                             Expression[] exprs,
                             out Geometry.Traits traits,
                             out ExpressionDrawer.IDrawable result)
            {
                ExpressionDrawer.MultiPoint res = default(ExpressionDrawer.MultiPoint);
                this.Load(loaders, mreader, debugger, exprs, out traits, out res);
                result = res;
            }

            public void Load(Loaders loaders,
                             MemoryReader mreader, Debugger debugger,
                             Expression[] exprs,
                             out Geometry.Traits traits,
                             out ExpressionDrawer.MultiPoint result)
            {
                traits = null;
                result = null;

                int dimension = Math.Min(exprs.Length, 3); // 2 or 3

                traits = new Geometry.Traits(dimension, Geometry.CoordinateSystem.Cartesian, Geometry.Unit.None);

                List<double>[] coords = new List<double>[dimension];
                for ( int i = 0 ; i < dimension; ++i )
                {
                    ValuesContainer containerLoader = loaders.FindByType(ExpressionLoader.Kind.Container,
                                                                         exprs[i].Name, exprs[i].Type) as ValuesContainer;
                    if (containerLoader == null)
                        return;

                    Geometry.Traits foo = null;
                    containerLoader.Load(loaders, mreader, debugger,
                                         exprs[i].Name, exprs[i].Type,
                                         out foo, out coords[i]);
                }

                int maxSize = 0;
                foreach(var list in coords)
                {
                    maxSize = Math.Max(maxSize, list.Count);
                }

                result = new ExpressionDrawer.MultiPoint();
                for (int i = 0; i < maxSize; ++i)
                {
                    double[] coo = new double[dimension];
                    
                    for (int j = 0; j < dimension; ++j)
                    {
                        coo[j] = i < coords[j].Count ? coords[j][i] : 0;
                    }

                    Geometry.Point pt = (dimension >= 3)
                                      ? new Geometry.Point(coo[0], coo[1], coo[2])
                                      : new Geometry.Point(coo[0], coo[1]);
                    result.Add(pt);
                }
            }
        }


        class CSArray : ContiguousContainer
        {
            public override string Id() { return null; }

            public override bool MatchType(string type, string id)
            {
                return ElementType(type).Length > 0;
            }

            public override string ElementType(string type)
            {
                return ElemTypeFromType(type);
            }
            
            public override int LoadSize(Debugger debugger, string name)
            {
                return LoadSizeParsed(debugger, name + ".Length");
            }

            public override string ElementName(string name, string elType)
            {
                return name + "[0]";
            }

            // type -> name[]
            static public string ElemTypeFromType(string type)
            {
                string name = "";
                int begin = type.LastIndexOf('[');
                if (begin > 0 && begin + 1 < type.Length && type[begin + 1] == ']')
                    name = type.Substring(0, begin);
                return name;
            }
        }

        class CSList : ContiguousContainer
        {
            public override string Id() { return "System.Collections.Generic.List"; }

            public override int LoadSize(Debugger debugger, string name)
            {
                return LoadSizeParsed(debugger, name + ".Count");
            }

            public override string RandomAccessElementName(string rawName, int i)
            {
                return rawName + "._items[" + i + "]";
            }

            public override string ElementName(string name, string elType)
            {
                return name + "._items[0]";
            }

            public override bool ForEachMemoryBlock(MemoryReader mreader, string name, string type,
                                                    MemoryReader.Converter<double> elementConverter,
                                                    MemoryBlockPredicate memoryBlockPredicate)
            {
                Expression expr = mreader.Debugger.GetExpression(name + "._items");
                if (!expr.IsValidValue || CSArray.ElemTypeFromType(expr.Type).Length <= 0)
                    return false;

                return base.ForEachMemoryBlock(mreader, name, type, elementConverter, memoryBlockPredicate);
            }
        }

        class PointCSArray : PointRange<ExpressionDrawer.MultiPoint>
        {
            public class PointKindConstraint : TypeConstraint
            {
                public override bool Ok(Loaders loaders, string name, string type)
                {
                    string elementType = CSArray.ElemTypeFromType(type);
                    if (elementType != "")
                    {
                        Loader loader = loaders.FindByType(name + "[0]", elementType);
                        if (loader != null)
                            return loader.Kind() == ExpressionLoader.Kind.Point;
                    }
                    return false;
                }
            }

            public PointCSArray()
                : base(ExpressionLoader.Kind.MultiPoint)
            { }

            public override TypeConstraint Constraint() { return new PointKindConstraint(); }

            public override string Id() { return null; }

            public override bool MatchType(string type, string id)
            {
                return CSArray.ElemTypeFromType(type) != "";
            }

            public override void Load(Loaders loaders, MemoryReader mreader,
                                      Debugger debugger, string name, string type,
                                      out Geometry.Traits traits,
                                      out ExpressionDrawer.MultiPoint result)
            {
                traits = null;
                result = null;

                string pointType = CSArray.ElemTypeFromType(type);

                ContainerLoader containerLoader = loaders.FindByType(ExpressionLoader.Kind.Container,
                                                                     name,
                                                                     type) as ContainerLoader;
                if (containerLoader == null)
                    return;

                string pointName = containerLoader.ElementName(name, pointType);

                PointLoader pointLoader = loaders.FindByType(ExpressionLoader.Kind.Point,
                                                             pointName,
                                                             pointType) as PointLoader;
                if (pointLoader == null)
                    return;

                traits = pointLoader.LoadTraits(pointType);
                if (traits == null)
                    return;

                if (mreader != null)
                {
                    result = LoadMemory(mreader, name, type,
                                        pointType, pointLoader, containerLoader);
                }

                if (result == null)
                {
                    result = LoadParsed(mreader, debugger, name, type,
                                        pointType, pointLoader, containerLoader);
                }
            }
        }


        class UserPoint : PointLoader
        {
            public UserPoint(string id, string x, string y)
            {
                this.id = id;
                this.member_x = x;
                this.member_y = y;
                this.member_type_x = null;
                this.member_type_y = null;
                this.sizeOf = 0;
            }

            public override bool IsUserDefined() { return true; }

            public override string Id() { return id; }

            public override void Initialize(Debugger debugger, string name)
            {
                Expression e = debugger.GetExpression(name);
                Expression ex = debugger.GetExpression(name + "." + member_x);
                Expression ey = debugger.GetExpression(name + "." + member_y);
                if (e.IsValidValue && ex.IsValidValue && ey.IsValidValue)
                {
                    sizeOf = MemoryReader.GetValueTypeSizeof(debugger, e.Type);
                    member_type_x = ex.Type;
                    member_type_y = ey.Type;
                }
            }

            public override Geometry.Traits LoadTraits(string type)
            {
                // TODO: dimension, CS and Units defined by the user
                return new Geometry.Traits(2, Geometry.CoordinateSystem.Cartesian, Geometry.Unit.None);
            }

            protected override ExpressionDrawer.Point LoadPointParsed(Debugger debugger, string name, string type)
            {
                double x = 0, y = 0;
                bool okx = TryLoadAsDoubleParsed(debugger, name + "." + member_x, out x);
                bool oky = TryLoadAsDoubleParsed(debugger, name + "." + member_y, out y);
                return Util.IsOk(okx, oky)
                     ? new ExpressionDrawer.Point(x, y)
                     : null;
            }

            protected override ExpressionDrawer.Point LoadPointMemory(MemoryReader mreader, string name, string type)
            {
                MemoryReader.Converter<double> converter = GetMemoryConverter(mreader, name, type);
                if (converter != null)
                {
                    if (converter.ValueCount() != 2)
                        throw new ArgumentOutOfRangeException("converter.ValueCount()");

                    double[] values = new double[2];
                    if (mreader.Read(name, values, converter))
                    {
                        return new ExpressionDrawer.Point(values[0], values[1]);
                    }
                }
                return null;
            }

            public override MemoryReader.Converter<double> GetMemoryConverter(MemoryReader mreader, string name, string type)
            {
                if (sizeOf == 0 /*|| member_type_x == null || member_type_y == null*/)
                    return null;

                string firstType = member_type_x;
                string secondType = member_type_y;
                string first = name + "." + member_x;
                string second = name + "." + member_y;
                // TODO: This could be done once, in Initialize
                long firstOffset = mreader.GetAddressDifference(name, first);
                long secondOffset = mreader.GetAddressDifference(name, second);
                if (MemoryReader.IsInvalidAddressDifference(firstOffset)
                 || MemoryReader.IsInvalidAddressDifference(secondOffset)
                 || firstOffset < 0
                 || secondOffset < 0
                 || firstOffset > sizeOf
                 || secondOffset > sizeOf)
                    return null;

                MemoryReader.Converter<double> firstConverter = mreader.GetNumericArrayConverter(first, firstType, 1);
                MemoryReader.Converter<double> secondConverter = mreader.GetNumericArrayConverter(second, secondType, 1);
                if (firstConverter == null || secondConverter == null)
                    return null;

                return new MemoryReader.StructConverter(
                            sizeOf,
                            new MemoryReader.Member(firstConverter, (int)firstOffset),
                            new MemoryReader.Member(secondConverter, (int)secondOffset));
            }

            string id;
            string member_x;
            string member_y;
            string member_type_x;
            string member_type_y;
            int sizeOf;
        }

        private static bool ReloadUserTypes(Loaders loaders,
                                            string userTypesPath,
                                            bool isChanged,
                                            DateTime lastWriteTime,
                                            out DateTime newWriteTime)
        {
            newWriteTime = new DateTime(0);

            bool fileExists = System.IO.File.Exists(userTypesPath);
            bool newerFile = false;
            if (fileExists)
            {
                newWriteTime = (new System.IO.FileInfo(userTypesPath)).LastWriteTime;
                newerFile = newWriteTime > lastWriteTime;
            }
            bool update = isChanged || newerFile;

            if (update)
                loaders.RemoveUserDefined();

            if (update && fileExists)
            {
                System.Xml.XmlDocument doc = new System.Xml.XmlDocument();
                doc.Load(userTypesPath);
                foreach (System.Xml.XmlElement elRoot in doc.GetElementsByTagName("GraphicalDebugging"))
                {
                    foreach (System.Xml.XmlElement elPoint in elRoot.GetElementsByTagName("Point"))
                    {
                        var elCoords = Util.GetXmlElementByTagName(elPoint, "Coordinates");
                        if (elCoords != null)
                        {
                            var elX = Util.GetXmlElementByTagName(elCoords, "X");
                            var elY = Util.GetXmlElementByTagName(elCoords, "Y");
                            if (elX != null && elY != null)
                            {
                                string x = elX.InnerText;
                                string y = elY.InnerText;
                                string id = elPoint.GetAttribute("Id");
                                //string name = elPoint.GetAttribute("Type");
                                loaders.Add(new UserPoint(id, x, y));
                            }
                        }
                    }
                }
            }

            return update;
        }

        public static void ReloadUserTypes(GeneralOptionPage options)
        {
            if (options == null)
                return;

            DateTime wtCpp;
            if (ReloadUserTypes(Instance.loadersCpp,
                                options.UserTypesPathCpp,
                                options.isUserTypesPathCppChanged,
                                options.userTypesCppWriteTime,
                                out wtCpp))
            {
                options.isUserTypesPathCppChanged = false;
                options.userTypesCppWriteTime = wtCpp;
            }

            DateTime wtCS;
            if (ReloadUserTypes(Instance.loadersCS,
                                options.UserTypesPathCS,
                                options.isUserTypesPathCSChanged,
                                options.userTypesCSWriteTime,
                                out wtCS))
            {
                options.isUserTypesPathCSChanged = false;
                options.userTypesCSWriteTime = wtCS;
            }
        }

        private static int LoadSizeParsed(Debugger debugger, string name)
        {
            Expression expr = debugger.GetExpression(name);
            return expr.IsValidValue
                 ? Math.Max(Util.ParseInt(expr.Value, debugger.HexDisplayMode), 0)
                 : 0;
        }

        private static bool TryLoadIntParsed(Debugger debugger, string name, out int result)
        {
            result = 0;
            Expression expr = debugger.GetExpression(name);
            if (!expr.IsValidValue)
                return false;
            result = Util.ParseInt(expr.Value, debugger.HexDisplayMode);
            return true;
        }

        static bool TryLoadAsDoubleParsed(Debugger debugger, string name, out double result)
        {
            result = 0.0;
            Expression expr = debugger.GetExpression("(double)" + name);
            if (!expr.IsValidValue)
                return false;
            result = Util.ParseDouble(expr.Value);
            return true;
        }
    }
}
