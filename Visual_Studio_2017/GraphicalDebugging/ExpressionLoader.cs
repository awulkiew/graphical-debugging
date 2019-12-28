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
    partial class ExpressionLoader
    {
        private DTE2 dte;
        private Debugger debugger;
        private DebuggerEvents debuggerEvents;
        private Loaders loadersCpp;
        private Loaders loadersCS;

        // TODO: It's not clear what to do with Variant
        // At the initial stage it's not known what is stored in Variant
        // so currently the kind of the stored object is not filtered properly.

        public enum Kind
        {
            Container = 0, MultiPoint, TurnsContainer, ValuesContainer,
            Point, Segment, Box, NSphere, Linestring, Ring, Polygon, MultiLinestring, MultiPolygon, Turn, OtherGeometry,
            Variant, Image
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

            //loadersCpp.Add(new BGIRtree());

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
            loadersCpp.Add(new BoostCircularBuffer());
            loadersCpp.Add(new StdVector());
            loadersCpp.Add(new StdDeque());
            loadersCpp.Add(new StdList());
            loadersCpp.Add(new StdSet());
            loadersCpp.Add(new CArray());

            loadersCpp.Add(new BGTurn("boost::geometry::detail::overlay::turn_info"));
            loadersCpp.Add(new BGTurn("boost::geometry::detail::overlay::traversal_turn_info"));
            loadersCpp.Add(new BGTurn("boost::geometry::detail::buffer::buffer_turn_info"));

            loadersCpp.Add(new BGTurnContainer());
            loadersCpp.Add(new PointContainer());
            loadersCpp.Add(new ValuesContainer());

            loadersCpp.Add(new BoostGilImage());

            loadersCS = new Loaders();

            loadersCS.Add(new CSList());
            loadersCS.Add(new CSArray());

            loadersCS.Add(new PointContainer());
            loadersCS.Add(new ValuesContainer());
        }

        public static Debugger Debugger
        {
            get { return Instance.debugger; }
        }

        public static DebuggerEvents DebuggerEvents
        {
            get { return Instance.debuggerEvents; }
        }

        // Expressions utilities

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

        // Kind Constraints

        public interface KindConstraint
        {
            bool Check(Kind kind);
        }

        public class DrawableKindConstraint : KindConstraint
        {
            public bool Check(Kind kind) { return kind != Kind.Container; }
        }

        public class GeometryKindConstraint : KindConstraint
        {
            public bool Check(Kind kind)
            {
                return kind != Kind.Container
                    && kind != Kind.ValuesContainer
                    && kind != Kind.Image;
            }
        }

        public class ValuesContainerKindConstraint : KindConstraint
        {
            public bool Check(Kind kind) { return kind == Kind.ValuesContainer; }
        }

        public class MultiPointKindConstraint : KindConstraint
        {
            public bool Check(Kind kind) { return kind == Kind.MultiPoint; }
        }

        public class IndexableKindConstraint : KindConstraint
        {
            public bool Check(Kind kind)
            {
                return kind == Kind.Point
                    || kind == Kind.Box
                    || kind == Kind.Segment;
            }
        }

        public static DrawableKindConstraint AllDrawables { get; } = new DrawableKindConstraint();
        public static GeometryKindConstraint OnlyGeometries { get; } = new GeometryKindConstraint();
        public static ValuesContainerKindConstraint OnlyValuesContainers { get; } = new ValuesContainerKindConstraint();
        public static MultiPointKindConstraint OnlyMultiPoints { get; } = new MultiPointKindConstraint();
        public static IndexableKindConstraint OnlyIndexables { get; } = new IndexableKindConstraint();

        // Load

        /// <summary>
        /// Loads debugged variable into ExpressionDrawer.IDrawable and additional
        /// geometrical information into Geometry.Traits. These classes then
        /// can be passed into ExpressionDrawer.Draw() in order to draw them
        /// on Graphics surface.
        /// </summary>
        /// <param name="name">Name of variable or actual expression added to watch</param>
        /// <param name="traits">Geometrical traits</param>
        /// <param name="result">An object that can be drawn by ExpressionDrawer</param>
        public static void Load(string name,
                                out Geometry.Traits traits,
                                out ExpressionDrawer.IDrawable result)
        {
            Load(name, AllDrawables, out traits, out result);
        }

        /// <summary>
        /// Loads debugged variable into ExpressionDrawer.IDrawable and additional
        /// geometrical information into Geometry.Traits. These classes then
        /// can be passed into ExpressionDrawer.Draw() in order to draw them
        /// on Graphics surface. This version loads only those kinds of variables
        /// that are defined by KindConstraint.
        /// </summary>
        /// <param name="name">Name of variable or actual expression added to watch</param>
        /// <param name="kindConstraint">Predicate defining the kind of debugged variable</param>
        /// <param name="traits">Geometrical traits</param>
        /// <param name="result">An object that can be drawn by ExpressionDrawer</param>
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
                DrawableLoader loader = loaders.FindByType(kindConstraint, exprs[0].Name, exprs[0].Type)
                                            as DrawableLoader;
                if (loader == null)
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

        // Loaders container

        /// <summary>
        /// The container of Loaders providing utilities to add and find
        /// Loaders based on Kind.
        /// </summary>
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
                lists[i].Add(loader);
            }

            /// <summary>
            /// Finds loader by given Kind and C++ or C# qualified identifier.
            /// </summary>
            /// <param name="kind">Kind of Loader</param>
            /// <param name="name">Name of variable or actual expression added to watch</param>
            /// <param name="id">C++ or C# qualified name of the type of variable</param>
            /// <returns>Loader object or null if not found</returns>
            public Loader FindById(Kind kind, string name, string id)
            {
                foreach (Loader l in lists[(int)kind])
                {
                    if (id == l.Id())
                    {
                        l.Initialize(ExpressionLoader.Instance.debugger, name);
                        return l;
                    }
                }
                return null;
            }

            /// <summary>
            /// Finds loader by given Kind and C++ or C# type.
            /// </summary>
            /// <param name="kind">Kind of Loader</param>
            /// <param name="name">Name of variable or actual expression added to watch</param>
            /// <param name="type">C++ or C# type of variable</param>
            /// <returns>Loader object or null if not found</returns>
            public Loader FindByType(Kind kind, string name, string type)
            {
                string id = Util.BaseType(type);
                foreach (Loader l in lists[(int)kind])
                {
                    if (l.MatchType(this, name, type, id))
                    {
                        l.Initialize(ExpressionLoader.Instance.debugger, name);
                        return l;
                    }
                }
                return null;
            }

            /// <summary>
            /// Finds loader by given KindConstraint and C++ or C# type.
            /// </summary>
            /// <param name="kindConstraint">Predicate defining the kind of Loader</param>
            /// <param name="name">Name of variable or actual expression added to watch</param>
            /// <param name="type">C++ or C# type of variable</param>
            /// <returns>Loader object or null if not found</returns>
            public Loader FindByType(KindConstraint kindConstraint, string name, string type)
            {
                string id = Util.BaseType(type);
                for (int i = 0; i < lists.Length; ++i)
                {
                    if (kindConstraint.Check((Kind)i))
                    {
                        foreach (Loader l in lists[i])
                        {
                            if (l.MatchType(this, name, type, id))
                            {
                                l.Initialize(ExpressionLoader.Instance.debugger, name);
                                return l;
                            }
                        }
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

        /// <summary>
        /// The base Loader class from which all Loaders has to be derived.
        /// </summary>
        abstract class Loader
        {
            /// <summary>
            /// Returns true for user-defined Loaders which has to be reloaded
            /// before loading variables.
            /// </summary>
            virtual public bool IsUserDefined()
            {
                return false;
            }

            /// <summary>
            /// Returns kind of Loader.
            /// </summary>
            abstract public ExpressionLoader.Kind Kind();

            /// <summary>
            /// Returns C++ or C# qualified identifier.
            /// </summary>
            abstract public string Id();

            /// <summary>
            /// Matches type and/or qualified identifier.
            /// Type and identifier can both receove the same value e.g. unsigned char[4]
            /// </summary>
            /// <param name="type">Full type</param>
            /// <param name="id">Qualified idenifier of type</param>
            virtual public bool MatchType(Loaders loaders, string name, string type, string id)
            {
                return id == Id();
            }

            /// <summary>
            /// Initializes the Loader before it's used to load a debugged
            /// variable.
            /// </summary>
            virtual public void Initialize(Debugger debugger, string name)
            { }
        }

        /// <summary>
        /// The base class of loaders which can load variables that can be drawn.
        /// </summary>
        abstract class DrawableLoader : Loader
        {
            /// <summary>
            /// Loads debugged variable into ExpressionDrawer.IDrawable and additional
            /// geometrical information into Geometry.Traits. These classes then
            /// can be passed into ExpressionDrawer.Draw() in order to draw them
            /// on Graphics surface.
            /// </summary>
            abstract public void Load(Loaders loaders,
                                      MemoryReader mreader, Debugger debugger,
                                      string name, string type,
                                      out Geometry.Traits traits,
                                      out ExpressionDrawer.IDrawable result);

            /// <summary>
            /// Returns MemoryReader.Converter object defining conversion
            /// from raw memory containing variables of various types,
            /// structures, arrays, etc. into an array of doubles.
            /// This object then can be used to convert blocks of memory
            /// while e.g. loading variables of a given type from a container.
            /// </summary>
            virtual public MemoryReader.Converter<double> GetMemoryConverter(Loaders loaders,
                                                                             MemoryReader mreader,
                                                                             Debugger debugger, // TODO - remove
                                                                             string name,
                                                                             string type)
            {
                return null;
            }
        }

        /// <summary>
        /// The base class of loaders which can load variables that can be drawn.
        /// It's more convenient to derive from this class than from DrawableLoader
        /// since it allows to define the exact ResultType.
        /// </summary>
        abstract class LoaderR<ResultType> : DrawableLoader
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
                    result = LoadPointMemory(mreader, debugger, name, type);
                if (result == null)
                    result = LoadPointParsed(debugger, name, type);
                return result;
            }
            abstract protected ExpressionDrawer.Point LoadPointParsed(Debugger debugger, string name, string type);
            abstract protected ExpressionDrawer.Point LoadPointMemory(MemoryReader mreader, Debugger debugger, string name, string type);
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

        abstract class BXPoint : PointLoader
        {
            protected ExpressionDrawer.Point LoadPointParsed(Debugger debugger, string name, string type, string ptrName, int count)
            {
                bool okx = true, oky = true;
                double x = 0, y = 0;
                if (count > 0)
                    okx = ExpressionParser.TryLoadAsDoubleParsed(debugger, ptrName + "[0]", out x);
                if (count > 1)
                    oky = ExpressionParser.TryLoadAsDoubleParsed(debugger, ptrName + "[1]", out y);
                return Util.IsOk(okx, oky)
                     ? new ExpressionDrawer.Point(x, y)
                     : null;
            }

            protected ExpressionDrawer.Point LoadPointMemory(MemoryReader mreader, Debugger debugger,
                                                             string name, string type, string ptrName, int count)
            {
                double[] values = new double[count];
                if (mreader.ReadNumericArray(debugger, ptrName + "[0]", values))
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

            protected MemoryReader.Converter<double> GetMemoryConverter(MemoryReader mreader,
                                                                        Debugger debugger,
                                                                        string name, string memberArray, string elemType, int count)
            {
                string elemName = memberArray + "[0]";
                MemoryReader.Converter<double> arrayConverter
                    = mreader.GetNumericArrayConverter(debugger, elemName, elemType, count);
                int byteSize = (new ExpressionParser(debugger)).GetValueSizeof(name);
                if (byteSize == 0)
                    return null;
                long byteOffset = ExpressionParser.GetAddressDifference(debugger, name, elemName);
                if (ExpressionParser.IsInvalidAddressDifference(byteOffset))
                    return null;
                return new MemoryReader.StructConverter<double>(byteSize,
                            new MemoryReader.Member<double>(arrayConverter, (int)byteOffset));
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

            protected override ExpressionDrawer.Point LoadPointMemory(MemoryReader mreader, Debugger debugger,
                                                                      string name, string type)
            {
                List<string> tparams = Util.Tparams(type);
                if (tparams.Count < 2)
                    return null;
                int dimension = int.Parse(tparams[1]);
                int count = Math.Min(dimension, 2);
                return LoadPointMemory(mreader, debugger, name, type, name + ".m_values", count);
            }

            public override MemoryReader.Converter<double> GetMemoryConverter(Loaders loaders,
                                                                              MemoryReader mreader,
                                                                              Debugger debugger, // TODO - remove
                                                                              string name, string type)
            {
                List<string> tparams = Util.Tparams(type);
                if (tparams.Count < 2)
                    return null;
                string coordType = tparams[0];
                int dimension = int.Parse(tparams[1]);
                int count = Math.Min(dimension, 2);
                return GetMemoryConverter(mreader, debugger, name, name + ".m_values", coordType, count);
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

            protected override ExpressionDrawer.Point LoadPointMemory(MemoryReader mreader, Debugger debugger,
                                                                      string name, string type)
            {
                return LoadPointMemory(mreader, debugger, name, type, name + ".m_values", 2);
            }

            public override MemoryReader.Converter<double> GetMemoryConverter(Loaders loaders,
                                                                              MemoryReader mreader,
                                                                              Debugger debugger, // TODO - remove
                                                                              string name, string type)
            {
                List<string> tparams = Util.Tparams(type);
                if (tparams.Count < 1)
                    return null;
                string coordType = tparams[0];

                return GetMemoryConverter(mreader, debugger, name, name + ".m_values", coordType, 2);
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

            public override MemoryReader.Converter<double> GetMemoryConverter(Loaders loaders,
                                                                              MemoryReader mreader,
                                                                              Debugger debugger, // TODO - remove
                                                                              string name, string type)
            {
                List<string> tparams = Util.Tparams(type);
                if (tparams.Count < 1)
                    return null;

                string m_min_corner = name + ".m_min_corner";
                string m_max_corner = name + ".m_max_corner";

                string pointType = tparams[0];
                PointLoader pointLoader = loaders.FindByType(ExpressionLoader.Kind.Point,
                                                             m_min_corner,
                                                             pointType) as PointLoader;
                if (pointLoader == null)
                    return null;

                MemoryReader.Converter<double> pointConverter = pointLoader.GetMemoryConverter(loaders, mreader, debugger, m_min_corner, pointType);

                int size = (new ExpressionParser(debugger)).GetValueSizeof(name);
                long minDiff = ExpressionParser.GetAddressDifference(debugger, name, m_min_corner);
                long maxDiff = ExpressionParser.GetAddressDifference(debugger, name, m_max_corner);

                if (size <= 0
                    || ExpressionParser.IsInvalidAddressDifference(minDiff)
                    || ExpressionParser.IsInvalidAddressDifference(maxDiff)
                    || minDiff < 0
                    || maxDiff < 0
                    || minDiff >= size
                    || maxDiff >= size)
                    return null;

                return new MemoryReader.StructConverter<double>(size,
                            new MemoryReader.Member<double>(pointConverter, (int)minDiff),
                            new MemoryReader.Member<double>(pointConverter, (int)maxDiff));
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
                bool ok = ExpressionParser.TryLoadAsDoubleParsed(debugger, m_radius, out radius);

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

            protected ResultType LoadMemory(Loaders loaders,
                                            MemoryReader mreader,
                                            Debugger debugger,
                                            string name, string type,
                                            string pointType,
                                            PointLoader pointLoader,
                                            ContainerLoader containerLoader)
            {
                ResultType result = null;

                string pointName = containerLoader.ElementName(name, pointType);
                if (pointName != null)
                {
                    MemoryReader.Converter<double> pointConverter = pointLoader.GetMemoryConverter(loaders, mreader, debugger,
                                                                                                   pointName, pointType);
                    if (pointConverter != null)
                    {
                        int dimension = pointConverter.ValueCount();
                        result = new ResultType();
                        bool ok = containerLoader.ForEachMemoryBlock(mreader, debugger, name, type, pointConverter,
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
            protected BGRange(ExpressionLoader.Kind kind, string id, int pointTIndex, int containerTIndex)
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
                string containerType = tparams[containerTIndex];
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
                    result = LoadMemory(loaders, mreader, debugger, name, type,
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

        // NOTE: Technically R-tree could be treated as a Container of Points or MultiPoint
        //       and displayed in a PlotWatch.
        // TODO: Consider this.

        class BGIRtree : GeometryLoader<ExpressionDrawer.DrawablesContainer>
        {
            public BGIRtree() : base(ExpressionLoader.Kind.OtherGeometry) { }

            public override string Id() { return "boost::geometry::index::rtree"; }

            public override void Load(Loaders loaders, MemoryReader mreader, Debugger debugger,
                                      string name, string type,
                                      out Geometry.Traits traits,
                                      out ExpressionDrawer.DrawablesContainer result)
            {
                traits = null;
                result = null;

                int size = LoadSize(debugger, name);
                if (size <= 0)
                    return;

                string valueType, indexableMember, indexableType;
                DrawableLoader indexableLoader = IndexableLoader(loaders, name, type,
                                                                 out valueType,
                                                                 out indexableMember,
                                                                 out indexableType);
                if (indexableLoader == null)
                    return;

                string nodePtrName = RootNodePtr(name);
                string nodeVariantName = "*" + nodePtrName;
                Expression rootExpr = debugger.GetExpression(nodeVariantName);
                if (!rootExpr.IsValidValue)
                    return;
                string nodeVariantType = rootExpr.Type;

                string leafType, internalNodeType;
                if (!Util.Tparams(nodeVariantType, out leafType, out internalNodeType))
                    return;

                string leafElemsName, leafElemsType;
                ContainerLoader leafElementsLoader = ElementsLoader(loaders, debugger,
                                                                    nodePtrName, leafType,
                                                                    out leafElemsName, out leafElemsType);
                if (leafElementsLoader == null)
                    return;

                string internalNodeElemsName, internalNodeElemsType;
                ContainerLoader internalNodeElementsLoader = ElementsLoader(loaders, debugger,
                                                                            nodePtrName, internalNodeType,
                                                                            out internalNodeElemsName, out internalNodeElemsType);
                if (internalNodeElementsLoader == null)
                    return;

                //if (mreader != null)
                //{
                //    long leafElemsDiff = mreader.GetAddressDifference(nodeVariantName,
                //                                                      NodeElements(nodePtrName, leafType));
                //    long internalNodeElemsDiff = mreader.GetAddressDifference(nodeVariantName,
                //                                                              NodeElements(nodePtrName, internalNodeType));

                //    if (MemoryReader.IsInvalidAddressDifference(leafElemsDiff)
                //        || MemoryReader.IsInvalidAddressDifference(internalNodeElemsDiff))
                //        return;

                //    string leafElemName = internalNodeElementsLoader.ElementName(leafElemsName,
                //                                                                 leafElementsLoader.ElementType(leafElemsType));
                //    string internalElemName = internalNodeElementsLoader.ElementName(internalNodeElemsName,
                //                                                                     internalNodeElementsLoader.ElementType(internalNodeElemsType));

                //    long indexableDiff = mreader.GetAddressDifference(leafElemName,
                //                                                      leafElemName + indexableMember);
                //    long nodePtrDiff = mreader.GetAddressDifference(internalElemName,
                //                                                    internalElemName + ".second");

                //    string whichName = "(" + nodeVariantName + ").which_";
                //    long whichDiff = mreader.GetAddressDifference(nodeVariantName,
                //                                                  whichName);

                //    if (MemoryReader.IsInvalidAddressDifference(indexableDiff)
                //        || MemoryReader.IsInvalidAddressDifference(nodePtrDiff)
                //        || MemoryReader.IsInvalidAddressDifference(whichDiff))
                //        return;

                //    MemoryReader.Converter<int> whichConverter = mreader.GetNumericConverter<int>(whichName);
                    
                //    if (whichConverter == null)
                //        return; // TODO

                //    int nodeVariantSizeof = mreader.GetValueSizeof(nodeVariantName);

                //    if (nodeVariantSizeof <= 0)
                //        return; // TODO

                //    MemoryReader.StructConverter<int> nodeVariantConverter = new MemoryReader.StructConverter<int>(
                //        nodeVariantSizeof,
                //        new MemoryReader.Member<int>(whichConverter, (int)whichDiff));

                //    ulong rootAddr = mreader.GetValueAddress(nodeVariantName);

                //    MemoryReader.ValueConverter<ulong> nodePtrConverter = mreader.GetPointerConverter(nodePtrName);

                //    if (nodePtrConverter == null)
                //        return; // TODO

                //    int nodePtrPairSizeof = mreader.GetValueSizeof(internalElemName);
                //    MemoryReader.Converter<ulong> nodePtrPairConverter = new MemoryReader.StructConverter<ulong>(
                //                                nodePtrPairSizeof,
                //                                new MemoryReader.Member<ulong>(nodePtrConverter, (int)nodePtrDiff));

                //    MemoryReader.Converter<double> indexableConverter = indexableLoader.GetMemoryConverter(loaders,
                //                                                                                           mreader,
                //                                                                                           leafElemName + indexableMember,
                //                                                                                           indexableType);
                //    if (indexableConverter == null)
                //        return; // TODO

                //    MemoryReader.Converter<double> valueConverter = indexableConverter;

                //    if (indexableMember != "" /*&& indexableDiff > 0*/)
                //    {
                //        int valueSizeof = mreader.GetValueSizeof(leafElemName);

                //        valueConverter = new MemoryReader.StructConverter<double>(
                //                                valueSizeof,
                //                                new MemoryReader.Member<double>(indexableConverter, (int)indexableDiff));
                //    }

                //    ExpressionDrawer.DrawablesContainer resMem = new ExpressionDrawer.DrawablesContainer();
                //    if (LoadMemoryRecursive(mreader, rootAddr,
                //                            nodeVariantConverter,
                //                            indexableLoader,
                //                            leafElementsLoader,
                //                            leafElemsName,
                //                            leafElemsType,
                //                            valueConverter,
                //                            internalNodeElementsLoader,
                //                            internalNodeElemsName,
                //                            internalNodeElemsType,
                //                            nodePtrPairConverter,
                //                            resMem))
                //    {
                //        // TODO traits
                //        //result = resMem;
                //        int a = 10;
                //    }
                //}

                Geometry.Traits tr = null;
                ExpressionDrawer.DrawablesContainer res = new ExpressionDrawer.DrawablesContainer();
                if (LoadParsedRecursive(loaders, mreader, debugger,
                                        nodePtrName,
                                        leafElementsLoader, internalNodeElementsLoader, indexableLoader,
                                        leafType, internalNodeType, indexableMember, indexableType,
                                        out tr, res))
                {
                    traits = tr;
                    result = res;
                }
            }

            //private bool LoadMemoryRecursive(MemoryReader mreader,
            //                                 ulong nodeAddr,
            //                                 MemoryReader.StructConverter<int> nodeVariantConverter,
            //                                 DrawableLoader indexableLoader,
            //                                 ContainerLoader leafElementsLoader,
            //                                 MemoryReader.Converter<double> valueConverter,
            //                                 ContainerLoader internalNodeElementsLoader,
            //                                 MemoryReader.Converter<ulong> nodePtrPairConverter,
            //                                 ExpressionDrawer.DrawablesContainer result)
            //{
            //    int[] which = new int[1];
            //    if (!mreader.Read(nodeAddr, which, nodeVariantConverter))
            //        return false;

            //    bool isLeaf = (which[0] == 0);
            //    if (isLeaf)
            //    {
            //        string leafElementsName = NodeElements(nodePtrName, leafType);

            //        // TODO: currently this method may still use Dubugger and parse size
            //        if (! leafElementsLoader.ForEachMemoryBlock(mreader,
            //                                                    leafElementsName, // THIS
            //                                                    leafElementsType,
            //                                                    valueConverter,
            //                delegate (double[] values)
            //                {
            //                    if (indexableLoader is PointLoader)
            //                    {
            //                        result.Add(new ExpressionDrawer.Point(values[0], values[1]));
            //                        return true;
            //                    }
            //                    else if (indexableLoader is BoxLoader)
            //                    {
            //                        result.Add(new ExpressionDrawer.Box(
            //                                    new ExpressionDrawer.Point(values[0], values[1]),
            //                                    new ExpressionDrawer.Point(values[2], values[3])));
            //                        return true;
            //                    }
            //                    else if (indexableLoader is SegmentLoader)
            //                    {
            //                        result.Add(new ExpressionDrawer.Segment(
            //                                    new ExpressionDrawer.Point(values[0], values[1]),
            //                                    new ExpressionDrawer.Point(values[2], values[3])));
            //                        return true;
            //                    }
            //                    else
            //                        return false;
            //                }))
            //            return false;
            //    }
            //    else
            //    {
            //        string internalNodeElementsName = NodeElements(nodePtrName, internalNodeType);

            //        // TODO: currently this method may still use Dubugger and parse size
            //        if (! internalNodeElementsLoader.ForEachMemoryBlock(mreader,
            //                internalNodeElementsName, internalNodeElementsType, nodePtrPairConverter,
            //                delegate (ulong[] ptrs)
            //                {
            //                    foreach(ulong addr in ptrs)
            //                    {
            //                        if (!LoadMemoryRecursive(mreader, addr,
            //                                                 nodeVariantConverter,
            //                                                 indexableLoader,
            //                                                 leafElementsLoader,
            //                                                 valueConverter,
            //                                                 internalNodeElementsLoader,
            //                                                 nodePtrPairConverter,                                                             
            //                                                 result))
            //                        {
            //                            return false;
            //                        }
            //                    }
            //                    return true;
            //                }))
            //            return false;
            //    }

            //    return true;
            //}

            private bool LoadParsedRecursive(Loaders loaders, MemoryReader mreader, Debugger debugger,
                                             string nodePtrName,
                                             ContainerLoader leafElementsLoader,
                                             ContainerLoader internalNodeElementsLoader,
                                             DrawableLoader indexableLoader,
                                             string leafType,
                                             string internalNodeType,
                                             string indexableMember,
                                             string indexableType,
                                             out Geometry.Traits traits,
                                             ExpressionDrawer.DrawablesContainer result)
            {
                traits = null;

                bool isLeaf;
                if (!IsLeaf(debugger, nodePtrName, out isLeaf))
                    return false;

                string nodeType = leafType;
                ContainerLoader elementsLoader = leafElementsLoader;
                if (!isLeaf)
                {
                    nodeType = internalNodeType;
                    elementsLoader = internalNodeElementsLoader;
                }
                string elementsName = NodeElements(nodePtrName, nodeType);

                Geometry.Traits tr = null;
                bool ok = elementsLoader.ForEachElement(debugger, elementsName, delegate (string elName)
                {
                    if (isLeaf)
                    {
                        ExpressionDrawer.IDrawable indexable = null;

                        indexableLoader.Load(loaders, mreader, debugger,
                                             elName + indexableMember, indexableType,
                                             out tr, out indexable);

                        if (tr == null || indexable == null)
                            return false;

                        result.Add(indexable);
                    }
                    else
                    {
                        string nextNodePtrName = elName + ".second";
                        if (!LoadParsedRecursive(loaders, mreader, debugger,
                                                 nextNodePtrName,
                                                 leafElementsLoader, internalNodeElementsLoader, indexableLoader,
                                                 leafType, internalNodeType, indexableMember, indexableType,
                                                 out tr, result))
                            return false;
                    }

                    return true;
                });

                traits = tr;

                return ok;
            }

            DrawableLoader IndexableLoader(Loaders loaders, string name, string type,
                                           out string valueType,
                                           out string indexableMember,
                                           out string indexableType)
            {
                indexableMember = "";
                indexableType = "";

                if (!Util.Tparams(type, out valueType))
                    return null;

                string valueId = Util.BaseType(valueType);
                DrawableLoader indexableLoader = null;
                indexableMember = "";
                indexableType = valueType;
                if (valueId == "std::pair" || valueId == "std::tuple" || valueId == "boost::tuple")
                {
                    string firstType;
                    if (!Util.Tparams(valueType, out firstType))
                        return null;                    

                    // C++ so dereferencing nullptr is probably enough as a name
                    indexableLoader = loaders.FindByType(OnlyIndexables,
                                                         "(*((" + firstType + "*)0))",
                                                         firstType) as DrawableLoader;

                    // The first type of pair/tuple is an Indexable
                    // so assume the pair/tuple is not a Geometry itself
                    if (indexableLoader != null)
                    {
                        indexableType = firstType;
                        if (valueId == "std::pair")
                            indexableMember = ".first";
                        else if (valueId == "std::tuple")
                            indexableMember = "._Myfirst._Val";
                        else // boost::tuple
                            indexableMember = ".head";
                    }
                }

                if (indexableLoader == null)
                {
                    // C++ so dereferencing nullptr is probably enough as a name
                    indexableLoader = loaders.FindByType(OnlyIndexables,
                                                         "(*((" + indexableType + "*)0))",
                                                         indexableType) as DrawableLoader;
                }

                return indexableLoader;
            }

            ContainerLoader ElementsLoader(Loaders loaders, Debugger debugger,
                                           string nodePtrName, string castedNodeType,
                                           out string elementsName, out string containerType)
            {
                elementsName = NodeElements(nodePtrName, castedNodeType);
                containerType = "";

                Expression expr = debugger.GetExpression(elementsName);
                if (!expr.IsValidValue)
                    return null;

                elementsName = expr.Name;
                containerType = expr.Type;

                return loaders.FindByType(ExpressionLoader.Kind.Container,
                                          expr.Name, expr.Type) as ContainerLoader;
            }

            int LoadSize(Debugger debugger, string name)
            {
                return ExpressionParser.LoadSizeParsed(debugger, name + ".m_members.values_count");
            }

            string RootNodePtr(string name)
            {
                return name + ".m_members.root";
            }

            string NodeElements(string nodePtrName, string castedNodeType)
            {
                return "((" + castedNodeType + "*)" + nodePtrName + "->storage_.data_.buf)->elements";
            }

            bool IsLeaf(Debugger debugger, string nodePtrName, out bool result)
            {
                result = false;

                int which = 0;
                if (!ExpressionParser.TryLoadIntParsed(debugger, nodePtrName + "->which_", out which))
                    return false;

                result = (which == 0);
                return true;
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

            protected override ExpressionDrawer.Point LoadPointMemory(MemoryReader mreader, Debugger debugger, string name, string type)
            {
                return LoadPointMemory(mreader, debugger, name, type, name + ".coords_", 2);
            }

            public override MemoryReader.Converter<double> GetMemoryConverter(Loaders loaders,
                                                                              MemoryReader mreader,
                                                                              Debugger debugger, // TODO - remove
                                                                              string name, string type)
            {
                List<string> tparams = Util.Tparams(type);
                if (tparams.Count < 1)
                    return null;
                string coordType = tparams[0];

                return GetMemoryConverter(mreader, debugger, name, name + ".coords_", coordType, 2);
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
                bool okx0 = ExpressionParser.TryLoadAsDoubleParsed(debugger, name + ".points_[0].coords_[0]", out x0);
                bool oky0 = ExpressionParser.TryLoadAsDoubleParsed(debugger, name + ".points_[0].coords_[1]", out y0);
                bool okx1 = ExpressionParser.TryLoadAsDoubleParsed(debugger, name + ".points_[1].coords_[0]", out x1);
                bool oky1 = ExpressionParser.TryLoadAsDoubleParsed(debugger, name + ".points_[1].coords_[1]", out y1);

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
                bool okxl = ExpressionParser.TryLoadAsDoubleParsed(debugger, name + ".ranges_[0].coords_[0]", out xl);
                bool okxh = ExpressionParser.TryLoadAsDoubleParsed(debugger, name + ".ranges_[0].coords_[1]", out xh);
                bool okyl = ExpressionParser.TryLoadAsDoubleParsed(debugger, name + ".ranges_[1].coords_[0]", out yl);
                bool okyh = ExpressionParser.TryLoadAsDoubleParsed(debugger, name + ".ranges_[1].coords_[1]", out yh);

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
                    result = LoadMemory(loaders, mreader, debugger,
                                        containerName, type,
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

        class BVariant : DrawableLoader
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
                if (!ExpressionParser.TryLoadIntParsed(debugger, name + ".which_", out which))
                    return;

                List<string> tparams = Util.Tparams(type);
                if (which < 0 || tparams.Count <= which)
                    return;

                string storedType = tparams[which];
                string storedName = "(*(" + storedType + "*)" + name + ".storage_.data_.buf)";

                DrawableLoader loader = loaders.FindByType(AllDrawables, storedName, storedType)
                                            as DrawableLoader;
                if (loader == null)
                    return;

                loader.Load(loaders, mreader, debugger, storedName, storedType, out traits, out result);
            }
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
                bool okx = ExpressionParser.TryLoadAsDoubleParsed(debugger, name + ".first", out x);
                bool oky = ExpressionParser.TryLoadAsDoubleParsed(debugger, name + ".second", out y);
                return Util.IsOk(okx, oky)
                     ? new ExpressionDrawer.Point(x, y)
                     : null;
            }

            protected override ExpressionDrawer.Point LoadPointMemory(MemoryReader mreader, Debugger debugger,
                                                                      string name, string type)
            {
                MemoryReader.Converter<double> converter = GetMemoryConverter(mreader, debugger, name, type);
                if (converter != null)
                {
                    if (converter.ValueCount() != 2)
                        throw new ArgumentOutOfRangeException("converter.ValueCount()");

                    ulong address = ExpressionParser.GetValueAddress(debugger, name);
                    if (address == 0)
                        return null;

                    double[] values = new double[2];
                    if (mreader.Read(address, values, converter))
                    {
                        return new ExpressionDrawer.Point(values[0], values[1]);
                    }
                }
                return null;
            }

            public override MemoryReader.Converter<double> GetMemoryConverter(Loaders loaders,
                                                                              MemoryReader mreader,
                                                                              Debugger debugger, // TODO - remove
                                                                              string name, string type)
            {
                return GetMemoryConverter(mreader, debugger, name, type);
            }

            protected MemoryReader.Converter<double> GetMemoryConverter(MemoryReader mreader, Debugger debugger,
                                                                        string name, string type)
            {
                List<string> tparams = Util.Tparams(type);
                if (tparams.Count < 2)
                    return null;
                string firstType = tparams[0];
                string secondType = tparams[1];
                string first = name + ".first";
                string second = name + ".second";
                long firstOffset = ExpressionParser.GetAddressDifference(debugger, name, first);
                long secondOffset = ExpressionParser.GetAddressDifference(debugger, name, second);
                if (ExpressionParser.IsInvalidAddressDifference(firstOffset)
                 || ExpressionParser.IsInvalidAddressDifference(secondOffset))
                    return null;
                MemoryReader.Converter<double> firstConverter = mreader.GetNumericArrayConverter(debugger, first, firstType, 1);
                MemoryReader.Converter<double> secondConverter = mreader.GetNumericArrayConverter(debugger, second, secondType, 1);
                if (firstConverter == null || secondConverter == null)
                    return null;
                int sizeOfPair = ExpressionParser.GetTypeSizeof(debugger, type);
                if (sizeOfPair == 0)
                    return null;
                return new MemoryReader.StructConverter<double>(
                            sizeOfPair,
                            new MemoryReader.Member<double>(firstConverter, (int)firstOffset),
                            new MemoryReader.Member<double>(secondConverter, (int)secondOffset));
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

            protected override ExpressionDrawer.Point LoadPointMemory(MemoryReader mreader, Debugger debugger, string name, string type)
            {
                return LoadPointMemory(mreader, debugger, name, type, name + "._Val", 2);
            }

            public override MemoryReader.Converter<double> GetMemoryConverter(Loaders loaders,
                                                                              MemoryReader mreader,
                                                                              Debugger debugger, // TODO - remove
                                                                              string name, string type)
            {
                List<string> tparams = Util.Tparams(type);
                if (tparams.Count < 1)
                    return null;
                string coordType = tparams[0];

                return GetMemoryConverter(mreader, debugger, name, name + "._Val", coordType, 2);
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
            public BGTurnContainer()
                : base(ExpressionLoader.Kind.TurnsContainer)
            {}

            public override string Id() { return null; }

            public override bool MatchType(Loaders loaders, string name, string type, string id)
            {
                // Is a Container
                ContainerLoader loader = loaders.FindByType(ExpressionLoader.Kind.Container, name, type) as ContainerLoader;
                if (loader == null)
                    return false;

                // Element is a Turn
                string elType = loader.ElementType(type);
                string elName = loader.ElementName(name, elType);
                // WARNING: Potentially recursive call, search for Turns only
                return loaders.FindByType(ExpressionLoader.Kind.Turn, elName, elType) != null;
            }

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

                string turnType = containerLoader.ElementType(type);
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
        }

        class PointContainer : PointRange<ExpressionDrawer.MultiPoint>
        {
            public PointContainer()
                : base(ExpressionLoader.Kind.MultiPoint)
            {}

            public override string Id() { return null; }

            public override bool MatchType(Loaders loaders, string name, string type, string id)
            {
                // Is a Container
                ContainerLoader loader = loaders.FindByType(ExpressionLoader.Kind.Container, name, type) as ContainerLoader;
                if (loader == null)
                    return false;

                // Element is a Point
                string elType = loader.ElementType(type);
                string elName = loader.ElementName(name, elType);
                // WARNING: Potentially recursive call, search for Points only
                return loaders.FindByType(ExpressionLoader.Kind.Point, elName, elType) != null;
            }

            public override void Load(Loaders loaders, MemoryReader mreader, Debugger debugger,
                                      string name, string type,
                                      out Geometry.Traits traits,
                                      out ExpressionDrawer.MultiPoint result)
            {
                traits = null;
                result = null;
                
                ContainerLoader containerLoader = loaders.FindByType(ExpressionLoader.Kind.Container, name, type) as ContainerLoader;
                if (containerLoader == null)
                    return;

                string pointType = containerLoader.ElementType(type);
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
                    result = LoadMemory(loaders, mreader, debugger,
                                        name, type,
                                        pointType, pointLoader, containerLoader);
                }

                if (result == null)
                {
                    result = LoadParsed(mreader, debugger,
                                        name, type,
                                        pointType, pointLoader, containerLoader);
                }
            }
        }

        class ValuesContainer : LoaderR<ExpressionDrawer.ValuesContainer>
        {
            public override ExpressionLoader.Kind Kind() { return ExpressionLoader.Kind.ValuesContainer; }

            public override string Id() { return null; }

            public override bool MatchType(Loaders loaders, string name, string type, string id)
            {
                // Is a Container
                ContainerLoader loader = loaders.FindByType(ExpressionLoader.Kind.Container, name, type) as ContainerLoader;
                if (loader == null)
                    return false;

                // Element is not a Geometry
                string elType = loader.ElementType(type);
                string elName = loader.ElementName(name, elType);
                // WARNING: Potentially recursive call, avoid searching for ValuesContainers
                return loaders.FindByType(OnlyGeometries, elName, elType) == null;
            }

            public override void Load(Loaders loaders, MemoryReader mreader, Debugger debugger,
                                      string name, string type,
                                      out Geometry.Traits traits,
                                      out ExpressionDrawer.ValuesContainer result)
            {
                traits = null;
                result = null;

                List<double> values = null;
                Load(loaders, mreader, debugger, name, type, out values);

                if (values != null)
                    result = new ExpressionDrawer.ValuesContainer(values);
            }

            public void Load(Loaders loaders, MemoryReader mreader, Debugger debugger,
                             string name, string type,
                             out List<double> result)
            {
                result = null;

                ContainerLoader loader = loaders.FindByType(ExpressionLoader.Kind.Container,
                                                            name,
                                                            type) as ContainerLoader;
                if (loader == null)
                    return;

                if (mreader != null)
                    LoadMemory(mreader, debugger, name, type, loader, out result);

                if (result == null)
                    LoadParsed(debugger, name, loader, out result);
            }

            protected void LoadMemory(MemoryReader mreader, Debugger debugger,
                                      string name, string type,
                                      ContainerLoader loader,
                                      out List<double> result)
            {
                result = null;

                string elemName = loader.ElementName(name, loader.ElementType(type));

                MemoryReader.ValueConverter<double>
                    valueConverter = mreader.GetNumericConverter<double>(debugger, elemName, null);
                if (valueConverter == null)
                    return;

                List<double> list = new List<double>();
                bool ok = loader.ForEachMemoryBlock(mreader, debugger, name, type, valueConverter,
                    delegate (double[] values)
                    {
                        foreach (double v in values)
                            list.Add(v);
                        return true;
                    });

                if (ok)
                    result = list;
            }

            protected void LoadParsed(Debugger debugger, string name,
                                      ContainerLoader loader,
                                      out List<double> result)
            {                
                result = null;
                int size = loader.LoadSize(debugger, name);
                List<double> values = new List<double>();
                bool ok = loader.ForEachElement(debugger, name, delegate (string elName)
                {
                    double value = 0;
                    bool okV = ExpressionParser.TryLoadAsDoubleParsed(debugger, elName, out value);
                    if (okV)
                        values.Add(value);
                    return okV;
                });

                if (ok)
                    result = values;
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
                    ValuesContainer containerLoader = loaders.FindByType(ExpressionLoader.Kind.ValuesContainer,
                                                                         exprs[i].Name,
                                                                         exprs[i].Type)
                                                            as ValuesContainer;
                    if (containerLoader == null)
                        return;

                    containerLoader.Load(loaders, mreader, debugger,
                                         exprs[i].Name, exprs[i].Type,
                                         out coords[i]);
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
                    sizeOf = ExpressionParser.GetTypeSizeof(debugger, e.Type);
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
                bool okx = ExpressionParser.TryLoadAsDoubleParsed(debugger, name + "." + member_x, out x);
                bool oky = ExpressionParser.TryLoadAsDoubleParsed(debugger, name + "." + member_y, out y);
                return Util.IsOk(okx, oky)
                     ? new ExpressionDrawer.Point(x, y)
                     : null;
            }

            protected override ExpressionDrawer.Point LoadPointMemory(MemoryReader mreader, Debugger debugger,
                                                                      string name, string type)
            {
                MemoryReader.Converter<double> converter = GetMemoryConverter(mreader, debugger, name, type);
                if (converter != null)
                {
                    if (converter.ValueCount() != 2)
                        throw new ArgumentOutOfRangeException("converter.ValueCount()");

                    ulong address = ExpressionParser.GetValueAddress(debugger, name);
                    if (address == 0)
                        return null;

                    double[] values = new double[2];
                    if (mreader.Read(address, values, converter))
                    {
                        return new ExpressionDrawer.Point(values[0], values[1]);
                    }
                }
                return null;
            }

            public override MemoryReader.Converter<double> GetMemoryConverter(Loaders loaders,
                                                                              MemoryReader mreader,
                                                                              Debugger debugger,
                                                                              string name, string type)
            {
                return GetMemoryConverter(mreader, debugger, name, type);
            }

            protected MemoryReader.Converter<double> GetMemoryConverter(MemoryReader mreader, Debugger debugger,
                                                                        string name, string type)
            {
                if (sizeOf == 0 /*|| member_type_x == null || member_type_y == null*/)
                    return null;

                string firstType = member_type_x;
                string secondType = member_type_y;
                string first = name + "." + member_x;
                string second = name + "." + member_y;
                // TODO: This could be done once, in Initialize
                long firstOffset = ExpressionParser.GetAddressDifference(debugger, name, first);
                long secondOffset = ExpressionParser.GetAddressDifference(debugger, name, second);
                if (ExpressionParser.IsInvalidAddressDifference(firstOffset)
                 || ExpressionParser.IsInvalidAddressDifference(secondOffset)
                 || firstOffset < 0
                 || secondOffset < 0
                 || firstOffset > sizeOf
                 || secondOffset > sizeOf)
                    return null;

                MemoryReader.Converter<double> firstConverter = mreader.GetNumericArrayConverter(debugger, first, firstType, 1);
                MemoryReader.Converter<double> secondConverter = mreader.GetNumericArrayConverter(debugger, second, secondType, 1);
                if (firstConverter == null || secondConverter == null)
                    return null;

                return new MemoryReader.StructConverter<double>(
                            sizeOf,
                            new MemoryReader.Member<double>(firstConverter, (int)firstOffset),
                            new MemoryReader.Member<double>(secondConverter, (int)secondOffset));
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
    }
}
