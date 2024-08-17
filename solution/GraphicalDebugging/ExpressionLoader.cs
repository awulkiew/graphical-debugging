//------------------------------------------------------------------------------
// <copyright file="ExpressionLoader.cs">
//     Copyright (c) Adam Wulkiewicz.
// </copyright>
//------------------------------------------------------------------------------

using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;

namespace GraphicalDebugging
{
    partial class ExpressionLoader
    {
        private readonly DTE2 dte;
        private readonly Debugger debugger;
        private readonly DebuggerEvents debuggerEvents; // debuggerEvents member is needed for the events to fire properly
        private readonly Loaders loadersCpp;
        private readonly Loaders loadersCS;
        private readonly Loaders loadersBasic;
        private readonly LoadersCache loadersCacheCpp;
        private readonly LoadersCache loadersCacheCS;
        private readonly LoadersCache loadersCacheBasic;

        // TODO: It's not clear what to do with Variant
        // At the initial stage it's not known what is stored in Variant
        // so currently the kind of the stored object is not filtered properly.

        public enum Kind
        {
            Container = 0, MultiPoint, TurnsContainer, ValuesContainer, GeometriesContainer,
            Point, Segment, Box, NSphere, Ray, Line, Linestring, Ring, Polygon,
            MultiLinestring, MultiPolygon, MultiGeometry, Turn, OtherGeometry,
            Variant, Image, Value
        };

        public delegate void BreakModeEnteredEventHandler();
        public static event BreakModeEnteredEventHandler BreakModeEntered;

        private void DebuggerEvents_OnEnterBreakMode(dbgEventReason Reason, ref dbgExecutionAction ExecutionAction)
        {
            loadersCacheCpp.Clear();
            loadersCacheCS.Clear();

            BreakModeEntered?.Invoke();
        }

        private void DebuggerEvents_OnContextChanged(Process NewProcess, Program NewProgram, Thread NewThread, StackFrame NewStackFrame)
        {
            if (NewStackFrame != null && this.debugger.IsBreakMode)
            {
                BreakModeEntered?.Invoke();
            }
        }

        public static bool IsBreakMode
        {
            get => Instance.debugger.IsBreakMode;
        }

        private static ExpressionLoader Instance { get; set; }

        public static async Task InitializeAsync(GraphicalDebuggingPackage package)
        {
            DTE2 dte = await package.GetServiceAsync(typeof(DTE)) as DTE2;

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            Instance = new ExpressionLoader(dte);
        }

        private ExpressionLoader(DTE2 dte)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            this.dte = dte;
            this.debugger = new Debugger(dte);
            this.debuggerEvents = this.dte.Events.DebuggerEvents;
            this.debuggerEvents.OnEnterBreakMode += DebuggerEvents_OnEnterBreakMode;
            this.debuggerEvents.OnContextChanged += DebuggerEvents_OnContextChanged;

            loadersCpp = new Loaders();

            loadersCpp.Add(new BGPoint.LoaderCreator());
            loadersCpp.Add(new BGPointXY.LoaderCreator());
            loadersCpp.Add(new BGSegment.LoaderCreator());
            loadersCpp.Add(new BGReferringSegment.LoaderCreator());
            loadersCpp.Add(new BGBox.LoaderCreator());
            loadersCpp.Add(new BGNSphere.LoaderCreator());
            loadersCpp.Add(new BGMultiPoint.LoaderCreator());
            loadersCpp.Add(new BGLinestring.LoaderCreator());
            loadersCpp.Add(new BGMultiLinestring.LoaderCreator());
            loadersCpp.Add(new BGRing.LoaderCreator());
            loadersCpp.Add(new BGPolygon.LoaderCreator());
            loadersCpp.Add(new BGMultiPolygon.LoaderCreator());
            loadersCpp.Add(new BGBufferedRing.LoaderCreator());
            loadersCpp.Add(new BGBufferedRingCollection.LoaderCreator());

            loadersCpp.Add(new BGIRtree.LoaderCreator());

            loadersCpp.Add(new BPPoint.LoaderCreator());
            loadersCpp.Add(new BPSegment.LoaderCreator());
            loadersCpp.Add(new BPBox.LoaderCreator());
            loadersCpp.Add(new BPRing.LoaderCreator());
            loadersCpp.Add(new BPPolygon.LoaderCreator());

            loadersCpp.Add(new StdPairPoint.LoaderCreator());
            loadersCpp.Add(new StdComplexPoint.LoaderCreator());

            loadersCpp.Add(new StdChronoDuration.LoaderCreator());

            loadersCpp.Add(new BVariant.LoaderCreator());

            loadersCpp.Add(new StdArray.LoaderCreator());
            loadersCpp.Add(new BoostArray.LoaderCreator());
            loadersCpp.Add(new BGVarray.LoaderCreator());
            loadersCpp.Add(new BoostContainerVector.LoaderCreator());
            loadersCpp.Add(new BoostContainerStaticVector.LoaderCreator());
            loadersCpp.Add(new BoostCircularBuffer.LoaderCreator());
            loadersCpp.Add(new StdVector.LoaderCreator());
            loadersCpp.Add(new StdDeque.LoaderCreator());
            loadersCpp.Add(new StdList.LoaderCreator());
            loadersCpp.Add(new StdSet.LoaderCreator());
            loadersCpp.Add(new CArray.LoaderCreator());

            loadersCpp.Add(new BGTurn.LoaderCreator("boost::geometry::detail::overlay::turn_info"));
            loadersCpp.Add(new BGTurn.LoaderCreator("boost::geometry::detail::overlay::traversal_turn_info"));
            loadersCpp.Add(new BGTurn.LoaderCreator("boost::geometry::detail::buffer::buffer_turn_info"));

            loadersCpp.Add(new BGTurnContainer.LoaderCreator());
            loadersCpp.Add(new PointContainer.LoaderCreator());
            loadersCpp.Add(new ValuesContainer.LoaderCreator());
            loadersCpp.Add(new GeometryContainer.LoaderCreator());

            loadersCpp.Add(new BoostGilImage.LoaderCreator());

            loadersCS = new Loaders();

            loadersCS.Add(new CSLinkedList.LoaderCreator());
            loadersCS.Add(new CSList.LoaderCreator());
            loadersCS.Add(new CSArray.LoaderCreator());
            loadersCS.Add(new CSContainerBase.LoaderCreator());

            loadersCS.Add(new PointContainer.LoaderCreator());
            loadersCS.Add(new ValuesContainer.LoaderCreator());
            loadersCS.Add(new GeometryContainer.LoaderCreator());

            loadersBasic = new Loaders();

            loadersBasic.Add(new BasicList.LoaderCreator());
            loadersBasic.Add(new BasicArray.LoaderCreator());

            loadersBasic.Add(new ValuesContainer.LoaderCreator());

            loadersCacheCpp = new LoadersCache();
            loadersCacheCS = new LoadersCache();
            loadersCacheBasic = new LoadersCache();
        }

        // Expressions utilities

        public static Expression[] GetExpressions(string name, char separator = ';')
        {
            var expr = Instance.debugger.GetExpression(name);
            if (expr.IsValid)
                return new Expression[] { expr };

            string[] subnames = name.Split(separator);
            Expression[] exprs = new Expression[subnames.Length];
            for (int i = 0; i < subnames.Length; ++i)
            {
                exprs[i] = Instance.debugger.GetExpression(subnames[i]);
            }

            return exprs;
        }

        public static bool AllValidValues(Expression[] exprs)
        {
            foreach(Expression e in exprs)
                if (!e.IsValid)
                    return false;
            return true;
        }
        public static string ErrorFromExpressions(Expression[] exprs)
        {
            foreach (Expression e in exprs)
                if (!e.IsValid)
                    return e.Value;
            return "";
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

        public interface IKindConstraint
        {
            bool Check(Kind kind);
        }

        public class KindConstraint : IKindConstraint
        {
            public KindConstraint(Kind kind)
            {
                mKind = kind;
            }

            public bool Check(Kind kind)
            {
                return mKind == kind;
            }

            public Kind Kind { get { return mKind; } }

            private readonly Kind mKind;
        }

        public class NonValueContainerKindConstraint : IKindConstraint
        {
            public bool Check(Kind kind)
            {
                return kind != Kind.Container
                    && kind != Kind.ValuesContainer;
            }
        }

        public class DrawableKindConstraint : IKindConstraint
        {
            public bool Check(Kind kind)
            {
                return kind != Kind.Container
                    && kind != Kind.Value;
            }
        }

        // IMPORTANT: GeometriesContainer cannot be a Geometry,
        //   otherwise infinite recursion may occur
        public class GeometryKindConstraint : IKindConstraint
        {
            public bool Check(Kind kind)
            {
                return kind != Kind.Container
                    && kind != Kind.ValuesContainer
                    && kind != Kind.GeometriesContainer
                    && kind != Kind.Image
                    && kind != Kind.Value;
            }
        }

        public class GeometryOrGeometryContainerKindConstraint : IKindConstraint
        {
            public bool Check(Kind kind)
            {
                return kind != Kind.Container
                    && kind != Kind.ValuesContainer
                    && kind != Kind.Image
                    && kind != Kind.Value;
            }
        }

        public class IndexableKindConstraint : IKindConstraint
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
        public static GeometryOrGeometryContainerKindConstraint OnlyGeometriesOrGeometryContainer { get; } = new GeometryOrGeometryContainerKindConstraint();
        public static KindConstraint OnlyValuesContainers { get; } = new KindConstraint(Kind.ValuesContainer);
        public static KindConstraint OnlyMultiPoints { get; } = new KindConstraint(Kind.MultiPoint);
        public static IndexableKindConstraint OnlyIndexables { get; } = new IndexableKindConstraint();
        public static NonValueContainerKindConstraint OnlyNonValueContainers { get; } = new NonValueContainerKindConstraint();

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
                                IKindConstraint kindConstraint,
                                out Geometry.Traits traits,
                                out ExpressionDrawer.IDrawable result)
        {
            traits = null;
            result = null;

            Expression[] exprs = GetExpressions(name);
            if (exprs.Length < 1 || ! AllValidValues(exprs))
                return;

            Loaders loaders = Instance.debugger.IsLanguageCpp ? Instance.loadersCpp
                            : Instance.debugger.IsLanguageCs ? Instance.loadersCS
                            : Instance.debugger.IsLanguageBasic ? Instance.loadersBasic
                            : null;

            if (loaders == null)
                return;

            MemoryReader mreader = null;
            GeneralOptionPage optionPage = Util.GetDialogPage<GeneralOptionPage>();
            if (optionPage == null || optionPage.EnableDirectMemoryAccess)
            {
                mreader = new MemoryReader(Instance.debugger);
            }

            LoadTimeGuard timeGuard = new LoadTimeGuard(name, 3000);
            
            if (exprs.Length == 1)
            {
                DrawableLoader loader = loaders.FindByType(kindConstraint, exprs[0].Name, exprs[0].Type)
                                            as DrawableLoader;
                if (loader == null)
                    return;

                traits = loader.GetTraits(mreader, Instance.debugger,
                                          exprs[0].Name);

                result = loader.Load(mreader, Instance.debugger,
                                     exprs[0].Name, exprs[0].Type,
                                     delegate ()
                                     {
                                        timeGuard.ThrowOnCancel();
                                     });
            }
            else //if (exprs.Length > 1)
            {
                // For now there is only one loader which can handle this case
                var creator = new CoordinatesContainers.LoaderCreator();
                CoordinatesContainers loader = creator.Create(loaders, exprs);

                traits = loader.GetTraits();

                result = loader.Load(mreader, Instance.debugger,
                                     exprs,
                                     delegate ()
                                     {
                                        timeGuard.ThrowOnCancel();
                                     });
            }

            timeGuard.Reset();
        }

        class LoadersCache
        {
            private class Entry
            {
                public Entry(Kind kind, Loader loader)
                {
                    Kind = kind;
                    Loader = loader;
                }

                public Kind Kind;
                public Loader Loader;
            }

            public LoadersCache()
            {
                dict = new Dictionary<string, List<Entry>>();
            }

            public void Add(string type, Kind kind, Loader loader)
            {
                if (!dict.TryGetValue(type, out List<Entry> list))
                {
                    list = new List<Entry>();
                    dict.Add(type, list);
                }
                // TODO: check if the type/kind is already stored and throw an exception
                //   to detect duplication?
                list.Add(new Entry(kind, loader));
            }

            public Loader Find(string type, IKindConstraint kindConstraint)
            {
                if (dict.TryGetValue(type, out List<Entry> list))
                {
                    foreach (Entry e in list)
                    {
                        if (kindConstraint.Check(e.Kind))
                            return e.Loader;
                    }
                }
                return null;
            }

            public void Clear()
            {
                dict.Clear();
            }

            private readonly Dictionary<string, List<Entry>> dict;
        }

        /// <summary>
        /// The container of Loaders providing utilities to add and find
        /// Loaders based on Kind.
        /// </summary>
        class Loaders
        {
            static readonly int KindsCount = Enum.GetValues(typeof(Kind)).Length;

            public Loaders()
            {
                lists = new List<ILoaderCreator>[KindsCount];
                for (int i = 0; i < KindsCount; ++i)
                    lists[i] = new List<ILoaderCreator>();
            }

            public void Add(ILoaderCreator loaderCreator)
            {
                int i = (int)loaderCreator.Kind();
                System.Diagnostics.Debug.Assert(0 <= i && i < KindsCount);
                lists[i].Add(loaderCreator);
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
                return FindByType(new KindConstraint(kind), name, type);
            }

            /// <summary>
            /// Finds loader by given KindConstraint and C++ or C# type.
            /// </summary>
            /// <param name="kindConstraint">Predicate defining the kind of Loader</param>
            /// <param name="name">Name of variable or actual expression added to watch</param>
            /// <param name="type">C++ or C# type of variable</param>
            /// <returns>Loader object or null if not found</returns>
            public Loader FindByType(IKindConstraint kindConstraint, string name, string type)
            {
                // Check if a Loader is cached for this type
                var loadersCache = Instance.debugger.IsLanguageCpp ? Instance.loadersCacheCpp
                                 : Instance.debugger.IsLanguageCs ? Instance.loadersCacheCS
                                 : Instance.debugger.IsLanguageBasic ? Instance.loadersCacheBasic
                                 : null;

                if (Instance.debugger.IsLanguageCpp)
                {
                    type = Util.CppRemoveCVRef(type);
                }

                if (loadersCache != null)
                {
                    Loader loader = loadersCache.Find(type, kindConstraint);
                    if (loader != null)
                        return loader;
                }

                // Parse type for qualified identifier
                string id = Util.TypeId(type);

                // Look for loader creator on the list(s)
                if (kindConstraint is KindConstraint)
                {
                    // Single kind required, check only one list
                    Kind kind = (kindConstraint as KindConstraint).Kind;
                    int kindIndex = (int)kind;
                    foreach (ILoaderCreator creator in lists[kindIndex])
                    {
                        Loader loader = creator.Create(this, Instance.debugger, name, type, id);
                        if (loader != null)
                        {
                            loadersCache?.Add(type, kind, loader);
                            return loader;
                        }
                    }
                }
                else
                {
                    // Multiple kinds may be required, check all of the lists
                    for (int i = 0; i < lists.Length; ++i)
                    {
                        Kind kind = (Kind)i;
                        if (kindConstraint.Check(kind))
                        {
                            foreach (ILoaderCreator creator in lists[i])
                            {
                                Loader loader = creator.Create(this, Instance.debugger, name, type, id);
                                if (loader != null)
                                {
                                    loadersCache?.Add(type, kind, loader);
                                    return loader;
                                }
                            }
                        }
                    }
                }
                return null;
            }

            public void RemoveUserDefined()
            {
                foreach (List<ILoaderCreator> li in lists)
                {
                    for (int i = li.Count - 1; i >= 0; --i)
                        if (li[i].IsUserDefined())
                            li.RemoveAt(i);
                }                
            }

            private readonly List<ILoaderCreator>[] lists;
        }

        /// <summary>
        /// The interface of a loader creator.
        /// </summary>
        interface ILoaderCreator
        {
            /// <summary>
            /// Returns true for user-defined Loaders which has to be reloaded
            /// before loading variables.
            /// </summary>
            bool IsUserDefined(); // Instead of this make 2 containers?

            /// <summary>
            /// Returns kind of created Loader.
            /// </summary>
            Kind Kind();

            /// <summary>
            /// Matches type and/or qualified identifier, then creates and initializes
            /// the Loader before it's used to load a debugged variable.
            /// Type and identifier can both receove the same value e.g. unsigned char[4].
            /// </summary>
            /// <param name="type">Full type</param>
            /// <param name="id">Qualified idenifier of type</param>
            Loader Create(Loaders loaders, Debugger debugger, string name, string type, string id);
        }

        /// <summary>
        /// The base Loader class from which all Loaders has to be derived.
        /// </summary>
        abstract class Loader
        {
        }

        /// <summary>
        /// The base class of loaders which can load variables that can be drawn.
        /// </summary>
        abstract class DrawableLoader : Loader
        {
            /// <summary>
            /// Callback function allowing to break loading of variable.
            /// </summary>
            public delegate void LoadCallback();

            /// <summary>
            /// Returns geometrical information of a Drawable.
            /// It then can be passed into ExpressionDrawer.Draw().
            /// </summary>
            abstract public Geometry.Traits GetTraits(MemoryReader mreader, Debugger debugger,
                                                      string name);

            /// <summary>
            /// Loads debugged variable into ExpressionDrawer.IDrawable.
            /// It then can be passed into ExpressionDrawer.Draw()
            /// in order to draw a Drawable on Graphics surface.
            /// </summary>
            abstract public ExpressionDrawer.IDrawable Load(MemoryReader mreader, Debugger debugger,
                                                            string name, string type,
                                                            LoadCallback callback);

            /// <summary>
            /// Returns MemoryReader.Converter object defining conversion
            /// from raw memory containing variables of various types,
            /// structures, arrays, etc. into an array of doubles.
            /// This object then can be used to convert blocks of memory
            /// while e.g. loading variables of a given type from a container.
            /// </summary>
            virtual public MemoryReader.Converter<double> GetMemoryConverter(MemoryReader mreader,
                                                                             Debugger debugger, // TODO - remove
                                                                             string name,
                                                                             string type)
            {
                return null;
            }

            /// <summary>
            /// Returns Drawable created from array of values generated by
            /// MemoryReader.Read() with MemoryReader.Converter.
            /// </summary>
            virtual public ExpressionDrawer.IDrawable DrawableFromMemory(MemoryReader.Converter<double> converter,
                                                                         double[] values, int offset)
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
        { }

        abstract class GeometryLoader<ResultType> : LoaderR<ResultType>
            where ResultType : ExpressionDrawer.IDrawable
        { }

        abstract class PointLoader : GeometryLoader<ExpressionDrawer.Point>
        {
            public override ExpressionDrawer.IDrawable Load(MemoryReader mreader, Debugger debugger,
                                                            string name, string type,
                                                            LoadCallback callback) // dummy callback
            {
                return LoadPoint(mreader, debugger, name, type);
            }
            
            virtual public ExpressionDrawer.Point LoadPoint(MemoryReader mreader, Debugger debugger, string name, string type)
            {
                ExpressionDrawer.Point result = null;
                if (mreader != null)
                    result = LoadPointMemory(mreader, debugger, name, type);
                if (result == null)
                    result = LoadPointParsed(debugger, name, type);
                return result;
            }
            abstract public ExpressionDrawer.Point LoadPointParsed(Debugger debugger, string name, string type);
            abstract public ExpressionDrawer.Point LoadPointMemory(MemoryReader mreader, Debugger debugger, string name, string type);

            public override ExpressionDrawer.IDrawable DrawableFromMemory(MemoryReader.Converter<double> converter,
                                                                          double[] values, int offset)
            {
                int valCount = converter.ValueCount();
                return valCount >= 2 ? new ExpressionDrawer.Point(values[offset], values[offset + 1])
                     : valCount == 1 ? new ExpressionDrawer.Point(values[offset], 0)
                     : null;
            }
        }

        abstract class BoxLoader : GeometryLoader<ExpressionDrawer.Box>
        {
            public override ExpressionDrawer.IDrawable DrawableFromMemory(MemoryReader.Converter<double> converter,
                                                                          double[] values, int offset)
            {
                int valCount = converter.ValueCount();
                if (valCount == 4)
                {
                    var mi = new ExpressionDrawer.Point(values[offset], values[offset + 1]);
                    var ma = new ExpressionDrawer.Point(values[offset + 2], values[offset + 3]);
                    return new ExpressionDrawer.Box(mi, ma);
                }
                else if (valCount == 2)
                {
                    var mi = new ExpressionDrawer.Point(values[offset], 0);
                    var ma = new ExpressionDrawer.Point(values[offset + 1], 0);
                    return new ExpressionDrawer.Box(mi, ma);
                }
                return null;
            }
        }

        abstract class RayLoader : GeometryLoader<ExpressionDrawer.Ray>
        {
            public override ExpressionDrawer.IDrawable DrawableFromMemory(MemoryReader.Converter<double> converter,
                                                                          double[] values, int offset)
            {
                int valCount = converter.ValueCount();
                if (valCount == 4)
                {
                    var o = new ExpressionDrawer.Point(values[offset], values[offset + 1]);
                    var d = new ExpressionDrawer.Point(values[offset + 2], values[offset + 3]);
                    return new ExpressionDrawer.Ray(o, d);
                }
                else if (valCount == 2)
                {
                    var o = new ExpressionDrawer.Point(values[offset], 0);
                    var d = new ExpressionDrawer.Point(values[offset + 1], 0);
                    return new ExpressionDrawer.Ray(o, d);
                }
                return null;
            }
        }

        abstract class LineLoader : GeometryLoader<ExpressionDrawer.Line>
        {
            public override ExpressionDrawer.IDrawable DrawableFromMemory(MemoryReader.Converter<double> converter,
                                                                          double[] values, int offset)
            {
                int valCount = converter.ValueCount();
                if (valCount == 4)
                {
                    var f = new ExpressionDrawer.Point(values[offset], values[offset + 1]);
                    var s = new ExpressionDrawer.Point(values[offset + 2], values[offset + 3]);
                    return new ExpressionDrawer.Line(f, s);
                }
                else if (valCount == 2)
                {
                    var f = new ExpressionDrawer.Point(values[offset], 0);
                    var s = new ExpressionDrawer.Point(values[offset + 1], 0);
                    return new ExpressionDrawer.Line(f, s);
                }
                return null;
            }
        }

        abstract class SegmentLoader : GeometryLoader<ExpressionDrawer.Segment>
        {
            public override ExpressionDrawer.IDrawable DrawableFromMemory(MemoryReader.Converter<double> converter,
                                                                          double[] values, int offset)
            {
                int valCount = converter.ValueCount();
                if (valCount == 4)
                {
                    var p0 = new ExpressionDrawer.Point(values[offset], values[offset + 1]);
                    var p1 = new ExpressionDrawer.Point(values[offset + 2], values[offset + 3]);
                    return new ExpressionDrawer.Segment(p0, p1);
                }
                else if (valCount == 2)
                {
                    var p0 = new ExpressionDrawer.Point(values[offset], 0);
                    var p1 = new ExpressionDrawer.Point(values[offset + 1], 0);
                    return new ExpressionDrawer.Segment(p0, p1);
                }
                return null;
            }
        }

        abstract class NSphereLoader : GeometryLoader<ExpressionDrawer.NSphere>
        {
            public override ExpressionDrawer.IDrawable DrawableFromMemory(MemoryReader.Converter<double> converter,
                                                                          double[] values, int offset)
            {
                int valCount = converter.ValueCount();
                if (valCount == 3)
                {
                    var c = new ExpressionDrawer.Point(values[offset], values[offset + 1]);
                    var r = values[offset + 2];
                    return new ExpressionDrawer.NSphere(c, r);
                }
                return null;
            }
        }

        abstract class RangeLoader<ResultType> : GeometryLoader<ResultType>
            where ResultType : ExpressionDrawer.IDrawable
        { }

        abstract class PolygonLoader : GeometryLoader<ExpressionDrawer.Polygon>
        { }

        abstract class PointRange<ResultType> : RangeLoader<ResultType>
            where ResultType : class
                             , ExpressionDrawer.IDrawable
                             , Geometry.IContainer<Geometry.Point>
                             , new()
        {
            protected ResultType LoadParsed(MemoryReader mreader, // should this be passed?
                                            Debugger debugger, string name, string type,
                                            string pointType,
                                            PointLoader pointLoader,
                                            ContainerLoader containerLoader,
                                            LoadCallback callback)
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

                    callback();
                    return true;
                });
                return result;
            }

            protected ResultType LoadMemory(MemoryReader mreader,
                                            Debugger debugger,
                                            string name, string type,
                                            string pointName, string pointType,
                                            PointLoader pointLoader,
                                            ContainerLoader containerLoader,
                                            LoadCallback callback)
            {
                MemoryReader.Converter<double> pointConverter = pointLoader.GetMemoryConverter(
                    mreader, debugger, pointName, pointType);
                if (pointConverter == null)
                    return null;

                int dimension = pointConverter.ValueCount();
                ResultType result = new ResultType();
                if (containerLoader.ForEachMemoryBlock(mreader, debugger,
                        name, type, 0, pointConverter,
                        delegate (double[] values)
                        {
                            if (dimension == 0 || values.Length % dimension != 0)
                            {
                                result = null;
                                return false;
                            }
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

                            callback();
                            return true;
                        }))
                {
                    return result;
                }

                return null;
            }
        }

        // TODO: This implementation is very similar to any MultiGeometry,
        //   including User-Defined ones, so if possible unify all of them
        class GeometryContainer : GeometryLoader<ExpressionDrawer.DrawablesContainer>
        {
            public class LoaderCreator : ExpressionLoader.ILoaderCreator
            {
                public bool IsUserDefined() { return false; }
                public Kind Kind() { return ExpressionLoader.Kind.GeometriesContainer; }
                public Loader Create(Loaders loaders, Debugger debugger, string name, string type, string id)
                {
                    // Any type/id, match all containers storing Geometries

                    // TODO: This may match container of Turns instead of BGTurnContainer

                    ContainerLoader containerLoader = loaders.FindByType(ExpressionLoader.Kind.Container, name, type) as ContainerLoader;
                    if (containerLoader == null)
                        return null;

                    containerLoader.ElementInfo(name, type, out string geometryName, out string geometryType);

                    // WARNING: Potentially recursive call, search for Geometries only,
                    //   GeometryContainer cannot be treated as a Geometry in GeometryKindConstraint
                    DrawableLoader geometryLoader = loaders.FindByType(OnlyGeometries,
                                                                       geometryName,
                                                                       geometryType) as DrawableLoader;
                    if (geometryLoader == null)
                        return null;

                    return new GeometryContainer(containerLoader, geometryLoader, geometryType);
                }
            }

            private GeometryContainer(ContainerLoader containerLoader,
                                      DrawableLoader geometryLoader,
                                      string geometryType)
            {
                this.containerLoader = containerLoader;
                this.geometryLoader = geometryLoader;
                this.geometryType = geometryType;
            }

            public override Geometry.Traits GetTraits(MemoryReader mreader, Debugger debugger,
                                                      string name)
            {
                return geometryLoader.GetTraits(mreader, debugger, name);
            }

            public override ExpressionDrawer.IDrawable Load(MemoryReader mreader, Debugger debugger,
                                                            string name, string type,
                                                            LoadCallback callback)
            {
                ExpressionDrawer.DrawablesContainer result = null;

                if  (mreader != null)
                {
                    containerLoader.ElementInfo(name, type, out string geometryName, out string _);

                    result = LoadMemory(mreader, debugger,
                                        name, type,
                                        geometryName,
                                        callback);
                }

                if (result == null)
                {
                    result = LoadParsed(mreader, debugger, name, type, callback);
                }

                return result;
            }

            private ExpressionDrawer.DrawablesContainer LoadMemory(MemoryReader mreader, Debugger debugger,
                                                                   string name, string type,
                                                                   string geometryName, 
                                                                   LoadCallback callback)
            {
                MemoryReader.Converter<double> geometryConverter = geometryLoader.GetMemoryConverter(
                    mreader, debugger, geometryName, geometryType);

                if (geometryConverter == null)
                    return null;

                ExpressionDrawer.DrawablesContainer result = new ExpressionDrawer.DrawablesContainer();
                if (containerLoader.ForEachMemoryBlock(mreader, debugger,
                        name, type, 0, geometryConverter,
                        delegate (double[] values)
                        {
                            int valCount = geometryConverter.ValueCount();
                            if (valCount == 0 || values.Length % valCount != 0)
                                return false;
                            int geometriesCount = values.Length / valCount;

                            for (int i = 0; i < geometriesCount; ++i)
                            {
                                ExpressionDrawer.IDrawable d = geometryLoader.DrawableFromMemory(
                                    geometryConverter, values, i * valCount);

                                if (d == null)
                                    return false;

                                result.Add(d);
                            }

                            callback();
                            return true;
                        }))
                {
                    return result;
                }

                return null;
            }

            private ExpressionDrawer.DrawablesContainer LoadParsed(MemoryReader mreader, Debugger debugger,
                                                                   string name, string type,
                                                                   LoadCallback callback)
            {
                ExpressionDrawer.DrawablesContainer drawables = new ExpressionDrawer.DrawablesContainer();
                if (containerLoader.ForEachElement(debugger, name,
                        delegate (string elName)
                        {
                            ExpressionDrawer.IDrawable drawable = geometryLoader.Load(
                                mreader, debugger, elName, geometryType,callback);
                            if (drawable == null)
                                return false;
                            drawables.Add(drawable);
                            //return callback();
                            return true;
                        }))
                {
                    return drawables;
                }

                return null;
            }

            readonly ContainerLoader containerLoader;
            readonly DrawableLoader geometryLoader;
            readonly string geometryType;
        }

        class PointContainer : PointRange<ExpressionDrawer.MultiPoint>
        {
            public class LoaderCreator : ExpressionLoader.ILoaderCreator
            {
                public bool IsUserDefined() { return false; }
                public Kind Kind() { return ExpressionLoader.Kind.MultiPoint; }
                public Loader Create(Loaders loaders, Debugger debugger, string name, string type, string id)
                {
                    // Any type/id, match all containers storing Points

                    ContainerLoader containerLoader = loaders.FindByType(ExpressionLoader.Kind.Container,
                                                                         name, type) as ContainerLoader;
                    if (containerLoader == null)
                        return null;

                    containerLoader.ElementInfo(name, type, out string pointName, out string pointType);

                    // WARNING: Potentially recursive call, search for Points only
                    PointLoader pointLoader = loaders.FindByType(ExpressionLoader.Kind.Point,
                                                                 pointName,
                                                                 pointType) as PointLoader;
                    if (pointLoader == null)
                        return null;

                    return new PointContainer(containerLoader, pointLoader, pointType);
                }
            }

            private PointContainer(ContainerLoader containerLoader,
                                   PointLoader pointLoader,
                                   string pointType)
            {
                this.containerLoader = containerLoader;
                this.pointLoader = pointLoader;
                this.pointType = pointType;
            }

            public override Geometry.Traits GetTraits(MemoryReader mreader, Debugger debugger,
                                                      string name)
            {
                return pointLoader.GetTraits(mreader, debugger, name);
            }

            public override ExpressionDrawer.IDrawable Load(MemoryReader mreader, Debugger debugger,
                                                            string name, string type,
                                                            LoadCallback callback)
            {
                ExpressionDrawer.MultiPoint result = null;
                
                if (mreader != null)
                {
                    containerLoader.ElementInfo(name, type, out string pointName, out string _);

                    result = LoadMemory(mreader, debugger,
                                        name, type,
                                        pointName, pointType, pointLoader,
                                        containerLoader, callback);
                }

                if (result == null)
                {
                    result = LoadParsed(mreader, debugger,
                                        name, type,
                                        pointType, pointLoader, containerLoader,
                                        callback);
                }

                return result;
            }

            readonly ContainerLoader containerLoader;
            readonly PointLoader pointLoader;
            readonly string pointType;
        }

        // TODO: Add abstract class and derive from it here
        // TODO: Add user-defined Calue in ExpressionLoader_UserDefined
        class Value : Loader
        {
            public Value(string memberName, string memberType)
            {
                this.memberName = memberName;
                this.memberType = memberType;
            }

            public double Load(MemoryReader mreader, Debugger debugger, string name, string type)
            {
                // TODO: what if the actual value is NaN?
                return debugger.TryLoadDouble(name + "." + memberName, out double result)
                     ? result
                     : double.NaN;
            }

            public MemoryReader.Converter<double> GetMemoryConverter(MemoryReader mreader,
                                                                     Debugger debugger, // TODO - remove
                                                                     string name,
                                                                     string type)
            {
                string member = name + "." + memberName;
                if (!debugger.GetAddressOffset(name, member, out long memberOffset)
                 || !debugger.GetTypeSizeof(member, out int memberSize))
                    return null;
                MemoryReader.ValueConverter<double> memberConverter = mreader.GetNumericConverter(memberType, memberSize);
                return memberConverter != null
                    && debugger.GetTypeSizeof(type, out int sizeOfValue)
                    && !Debugger.IsInvalidOffset(sizeOfValue, memberOffset)
                     ? new MemoryReader.StructConverter<double>(
                            sizeOfValue,
                            new MemoryReader.Member<double>(memberConverter, (int)memberOffset))
                     : null;
            }

            private readonly string memberName;
            private readonly string memberType;
        }

        class ValuesContainer : LoaderR<ExpressionDrawer.ValuesContainer>
        {
            public class LoaderCreator : ExpressionLoader.ILoaderCreator
            {
                public bool IsUserDefined() { return false; }
                public Kind Kind() { return ExpressionLoader.Kind.ValuesContainer; }
                public Loader Create(Loaders loaders, Debugger debugger, string name, string type, string id)
                {
                    // Any type/id, match all containers storing unknown element types and assume
                    // they are values

                    ContainerLoader containerLoader = loaders.FindByType(ExpressionLoader.Kind.Container,
                                                                         name,
                                                                         type) as ContainerLoader;
                    if (containerLoader == null)
                        return null;

                    // Element is not a Geometry
                    containerLoader.ElementInfo(name, type, out string elName, out string elType);

                    // WARNING: Potentially recursive call, avoid searching for ValuesContainers
                    Loader l = loaders.FindByType(OnlyNonValueContainers, elName, elType);

                    return l == null ? new ValuesContainer(containerLoader, null, elType)
                         : l is Value ? new ValuesContainer(containerLoader, l as Value, elType)
                         : null;
                }
            }

            private ValuesContainer(ContainerLoader containerLoader, Value valueLoader, string valueType)
            {
                this.containerLoader = containerLoader;
                this.valueLoader = valueLoader;
                this.valueType = valueType;
            }

            public override Geometry.Traits GetTraits(MemoryReader mreader, Debugger debugger,
                                                      string name)
            {
                return null;
            }

            public override ExpressionDrawer.IDrawable Load(MemoryReader mreader, Debugger debugger,
                                                            string name, string type,
                                                            LoadCallback callback)
            {
                List<double> values = LoadValues(mreader, debugger, name, type, callback);

                return values != null
                     ? new ExpressionDrawer.ValuesContainer(values)
                     : null;
            }

            public List<double> LoadValues(MemoryReader mreader, Debugger debugger,
                                           string name, string type,
                                           LoadCallback callback)
            {
                List<double> result = null;

                if (mreader != null)
                    LoadMemory(mreader, debugger, name, type, out result, callback);

                if (result == null)
                    LoadParsed(mreader, debugger, name, type, out result, callback);

                return result;
            }

            private void LoadMemory(MemoryReader mreader, Debugger debugger,
                                    string name, string type,
                                    out List<double> result,
                                    LoadCallback callback)
            {
                result = null;

                containerLoader.ElementInfo(name, type, out string elemName, out string elemType);

                if (!debugger.GetTypeSizeof(elemType, out int valSize))
                    return;

                MemoryReader.Converter<double> valueConverter = null;

                if (valueLoader != null)
                    valueConverter = valueLoader.GetMemoryConverter(mreader, debugger, elemName, elemType);
                else
                    valueConverter = mreader.GetNumericConverter(elemType, valSize);

                if (valueConverter == null)
                    return;

                List<double> list = new List<double>();
                bool ok = containerLoader.ForEachMemoryBlock(mreader, debugger, name, type, 0, valueConverter,
                    delegate (double[] values)
                    {
                        foreach (double v in values)
                            list.Add(v);
                        callback();
                        return true;
                    });

                if (ok)
                    result = list;
            }

            private void LoadParsed(MemoryReader mreader, Debugger debugger,
                                    string name, string type,
                                    out List<double> result,
                                    LoadCallback callback)
            {                
                result = null;
                List<double> values = new List<double>();
                bool ok = containerLoader.ForEachElement(debugger, name, delegate (string elName)
                {
                    double value = double.NaN;
                    if (valueLoader != null)
                    {
                        // TODO: what if the actual value is NaN?
                        value = valueLoader.Load(mreader, debugger, elName, valueType);
                        if (double.IsNaN(value))
                            return false;
                    }
                    else // no value loader found, assume it's built-in numeric type
                    {
                        if (!debugger.TryLoadDouble(elName, out value))
                            return false;
                    }
                    values.Add(value);
                    callback();
                    return true;
                });

                if (ok)
                    result = values;
            }

            readonly ContainerLoader containerLoader;
            readonly Value valueLoader;
            readonly string valueType;
        }

        // This loader is created manually right now so LoaderCreator is not needed
        // TODO: Still, each time Load() is called ValuesContainers Loaders are created
        class CoordinatesContainers : PointRange<ExpressionDrawer.MultiPoint>
        {
            public class LoaderCreator
            {
                public CoordinatesContainers Create(Loaders loaders, Expression[] exprs)
                {
                    int dimension = Math.Min(exprs.Length, 3); // 2 or 3
                    if (dimension < 2)
                        return null;

                    ValuesContainer[] valueContainers = new ValuesContainer[dimension];

                    for (int i = 0; i < dimension; ++i)
                    {
                        valueContainers[i] = loaders.FindByType(ExpressionLoader.Kind.ValuesContainer,
                                                                exprs[i].Name,
                                                                exprs[i].Type) as ValuesContainer;
                        if (valueContainers[i] == null)
                            return null;
                    }

                    Geometry.Traits traits = new Geometry.Traits(dimension, Geometry.CoordinateSystem.Cartesian, Geometry.Unit.None);

                    return new CoordinatesContainers(valueContainers, traits);
                }
            }

            public CoordinatesContainers(ValuesContainer[] valueContainers,
                                         Geometry.Traits traits)
            {
                this.valueContainers = valueContainers;
                this.traits = traits;
            }

            public override Geometry.Traits GetTraits(MemoryReader mreader, Debugger debugger,
                                                      string name)
            {
                return null;
            }

            public override ExpressionDrawer.IDrawable Load(MemoryReader mreader, Debugger debugger,
                                                            string name, string type,
                                                            LoadCallback callback)
            {
                return null;
            }

            public Geometry.Traits GetTraits()
            {
                return traits;
            }

            public ExpressionDrawer.IDrawable Load(MemoryReader mreader, Debugger debugger,
                                                   Expression[] exprs,
                                                   LoadCallback callback)
            {
                int dimension = valueContainers.Length;

                List<double>[] coords = new List<double>[dimension];
                for ( int i = 0 ; i < dimension; ++i )
                {
                    coords[i] = valueContainers[i].LoadValues(mreader, debugger,
                                                              exprs[i].Name, exprs[i].Type,
                                                              callback);
                }

                int maxSize = 0;
                foreach(var list in coords)
                {
                    maxSize = Math.Max(maxSize, list.Count);
                }

                ExpressionDrawer.MultiPoint result = new ExpressionDrawer.MultiPoint();

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

                return result;
            }

            readonly ValuesContainer[] valueContainers;
            readonly Geometry.Traits traits;
        }
    }
}
