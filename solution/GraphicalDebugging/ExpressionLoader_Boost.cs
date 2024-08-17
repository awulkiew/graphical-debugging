using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GraphicalDebugging
{
    partial class ExpressionLoader
    {
        // Or ArrayPoint
        abstract class BXPoint : PointLoader
        {
            // memberArraySuffix has to start with '.'
            protected BXPoint(string memberArraySuffix, string coordType, Geometry.Traits traits)
            {
                this.memberArraySuffix = memberArraySuffix;
                this.coordType = coordType;
                this.traits = traits;
                this.count = Math.Min(traits.Dimension, 2);
            }

            public override Geometry.Traits GetTraits(MemoryReader mreader, Debugger debugger,
                                                      string name)
            {
                return traits;
            }

            public override ExpressionDrawer.Point LoadPointParsed(Debugger debugger, string name, string type)
            {
                bool okx = true, oky = true;
                double x = 0, y = 0;
                string ptrName = name + memberArraySuffix;
                if (count >= 1)
                    okx = debugger.TryLoadDouble(ptrName + "[0]", out x);
                if (count >= 2)
                    oky = debugger.TryLoadDouble(ptrName + "[1]", out y);
                return Util.IsOk(okx, oky)
                     ? new ExpressionDrawer.Point(x, y)
                     : null;
            }

            public override ExpressionDrawer.Point LoadPointMemory(MemoryReader mreader, Debugger debugger,
                                                                      string name, string type)
            {
                string ptrName = name + memberArraySuffix;
                VariableInfo info = new VariableInfo(debugger, ptrName + "[0]");
                if (! info.IsValid)
                    return null;

                double[] values = new double[count];
                if (mreader.ReadNumericArray(info.Address, info.Type, info.Size, values))
                {
                    if (count >= 2)
                        return new ExpressionDrawer.Point(values[0], values[1]);
                    else if (count == 1)
                        return new ExpressionDrawer.Point(values[0], 0);
                    else
                        return null;
                }

                return null;
            }

            public override MemoryReader.Converter<double> GetMemoryConverter(MemoryReader mreader,
                                                                              Debugger debugger, // TODO - remove
                                                                              string name, string type)
            {
                // TODO: byteSize and byteOffset could be created in LoaderCreator
                string ptrName = name + memberArraySuffix;
                string elemName = ptrName + "[0]";
                if (!debugger.GetTypeSizeof(coordType, out int elemSize))
                    return null;
                MemoryReader.Converter<double> arrayConverter
                    = mreader.GetNumericArrayConverter(coordType, elemSize, count);
                return arrayConverter != null
                    && debugger.GetValueSizeof(name, out int byteSize)
                    && debugger.GetAddressOffset(name, elemName, out long byteOffset)
                    && !Debugger.IsInvalidOffset(byteSize, byteOffset)
                     ? new MemoryReader.StructConverter<double>(byteSize,
                            new MemoryReader.Member<double>(arrayConverter, (int)byteOffset))
                     : null;
            }

            private readonly string memberArraySuffix;
            private readonly string coordType;
            private readonly Geometry.Traits traits;
            private readonly int count;
        }

        class BGPoint : BXPoint
        {
            public class LoaderCreator : ExpressionLoader.ILoaderCreator
            {
                public bool IsUserDefined() { return false; }
                public Kind Kind() { return ExpressionLoader.Kind.Point; }
                public Loader Create(Loaders loaders, Debugger debugger, string name, string type, string id)
                {
                    if (id != "boost::geometry::model::point")
                        return null;

                    List<string> tparams = Util.Tparams(type);
                    if (tparams.Count < 3)
                        return null;

                    string coordType = tparams[0];
                    int dimension = int.Parse(tparams[1]);
                    ParseCSAndUnit(tparams[2], out Geometry.CoordinateSystem cs, out Geometry.Unit unit);

                    return new BGPoint(coordType, new Geometry.Traits(dimension, cs, unit));
                }
            }

            protected BGPoint(string coordType, Geometry.Traits traits)
                : base(".m_values", coordType, traits)
            { }

            protected static void ParseCSAndUnit(string cs_type, out Geometry.CoordinateSystem cs, out Geometry.Unit unit)
            {
                cs = Geometry.CoordinateSystem.Cartesian;
                unit = Geometry.Unit.None;

                if (cs_type == "boost::geometry::cs::cartesian")
                {
                    return;
                }

                string cs_base_type = Util.TypeId(cs_type);
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
            public new class LoaderCreator : ExpressionLoader.ILoaderCreator
            {
                public bool IsUserDefined() { return false; }
                public Kind Kind() { return ExpressionLoader.Kind.Point; }
                public Loader Create(Loaders loaders, Debugger debugger, string name, string type, string id)
                {
                    if (id != "boost::geometry::model::d2::point_xy")
                        return null;

                    List<string> tparams = Util.Tparams(type);
                    if (tparams.Count < 2)
                        return null;

                    string coordType = tparams[0];
                    ParseCSAndUnit(tparams[1], out Geometry.CoordinateSystem cs, out Geometry.Unit unit);

                    return new BGPointXY(coordType, new Geometry.Traits(2, cs, unit));
                }
            }

            public BGPointXY(string coordType, Geometry.Traits traits)
                : base(coordType, traits)
            {}
        }

        class BGBox : BoxLoader
        {
            public class LoaderCreator : ExpressionLoader.ILoaderCreator
            {
                public bool IsUserDefined() { return false; }
                public Kind Kind() { return ExpressionLoader.Kind.Box; }
                public Loader Create(Loaders loaders, Debugger debugger, string name, string type, string id)
                {
                    if (id != "boost::geometry::model::box")
                        return null;

                    List<string> tparams = Util.Tparams(type);
                    if (tparams.Count < 1)
                        return null;

                    string m_min_corner = name + ".m_min_corner";
                    string m_max_corner = name + ".m_max_corner";

                    string pointType = tparams[0];
                    PointLoader pointLoader = loaders.FindByType(ExpressionLoader.Kind.Point,
                                                                 m_min_corner,
                                                                 pointType) as PointLoader;
                    return pointLoader != null
                        && debugger.GetTypeSizeof(type, out int sizeOf)
                        && debugger.GetAddressOffset(name, m_min_corner, out long minDiff)
                        && debugger.GetAddressOffset(name, m_max_corner, out long maxDiff)
                        && !Debugger.IsInvalidOffset(sizeOf, minDiff, maxDiff)
                         ? new BGBox(pointLoader, pointType, sizeOf, minDiff, maxDiff)
                         : null;
                }
            }

            private BGBox(PointLoader pointLoader, string pointType, int sizeOf, long minDiff, long maxDiff)
            {
                this.pointLoader = pointLoader;
                this.pointType = pointType;
                this.sizeOf = sizeOf;
                this.minDiff = minDiff;
                this.maxDiff = maxDiff;
            }

            public override Geometry.Traits GetTraits(MemoryReader mreader, Debugger debugger,
                                                      string name)
            {
                return pointLoader.GetTraits(mreader, debugger, name);
            }

            public override ExpressionDrawer.IDrawable Load(MemoryReader mreader, Debugger debugger,
                                                            string name, string type,
                                                            LoadCallback callback) // dummy callback
            {
                string m_min_corner = name + ".m_min_corner";
                string m_max_corner = name + ".m_max_corner";

                Geometry.Point fp = pointLoader.LoadPoint(mreader, debugger, m_min_corner, pointType);
                Geometry.Point sp = pointLoader.LoadPoint(mreader, debugger, m_max_corner, pointType);

                return Util.IsOk(fp, sp)
                     ? new ExpressionDrawer.Box(fp, sp)
                     : null;
            }

            public override MemoryReader.Converter<double> GetMemoryConverter(MemoryReader mreader,
                                                                              Debugger debugger, // TODO - remove
                                                                              string name, string type)
            {
                string m_min_corner = name + ".m_min_corner";
                //string m_max_corner = name + ".m_max_corner";

                MemoryReader.Converter<double> pointConverter = pointLoader.GetMemoryConverter(mreader, debugger, m_min_corner, pointType);
                if (pointConverter == null)
                    return null;

                return new MemoryReader.StructConverter<double>(sizeOf,
                            new MemoryReader.Member<double>(pointConverter, (int)minDiff),
                            new MemoryReader.Member<double>(pointConverter, (int)maxDiff));
            }

            private readonly PointLoader pointLoader;
            private readonly string pointType;
            private readonly long minDiff;
            private readonly long maxDiff;
            private readonly int sizeOf;
        }

        class BGSegment : SegmentLoader
        {
            public class LoaderCreator : ExpressionLoader.ILoaderCreator
            {
                public bool IsUserDefined() { return false; }
                public Kind Kind() { return ExpressionLoader.Kind.Segment; }
                public Loader Create(Loaders loaders, Debugger debugger, string name, string type, string id)
                {
                    if (id != "boost::geometry::model::segment")
                        return null;

                    List<string> tparams = Util.Tparams(type);
                    if (tparams.Count < 1)
                        return null;

                    string first = name + ".first";
                    string second = name + ".second";

                    string pointType = tparams[0];
                    PointLoader pointLoader = loaders.FindByType(ExpressionLoader.Kind.Point,
                                                                 first,
                                                                 pointType) as PointLoader;
                    return pointLoader != null
                        && debugger.GetTypeSizeof(type, out int sizeOf)
                        && debugger.GetAddressOffset(name, first, out long firstDiff)
                        && debugger.GetAddressOffset(name, second, out long secondDiff)
                        && !Debugger.IsInvalidOffset(sizeOf, firstDiff, secondDiff)
                         ? new BGSegment(pointLoader, pointType, sizeOf, firstDiff, secondDiff)
                         : null;
                }
            }

            protected BGSegment(PointLoader pointLoader, string pointType, int sizeOf, long firstDiff, long secondDiff)
            {
                this.pointLoader = pointLoader;
                this.pointType = pointType;
                this.sizeOf = sizeOf;
                this.firstDiff = firstDiff;
                this.secondDiff = secondDiff;
            }

            public override Geometry.Traits GetTraits(MemoryReader mreader, Debugger debugger,
                                                      string name)
            {
                return pointLoader.GetTraits(mreader, debugger, name);
            }

            public override ExpressionDrawer.IDrawable Load(MemoryReader mreader, Debugger debugger,
                                                            string name, string type,
                                                            LoadCallback callback) // dummy callback
            {
                string first = name + ".first";
                string second = name + ".second";

                Geometry.Point fp = pointLoader.LoadPoint(mreader, debugger, first, pointType);
                Geometry.Point sp = pointLoader.LoadPoint(mreader, debugger, second, pointType);

                return Util.IsOk(fp, sp)
                     ? new ExpressionDrawer.Segment(fp, sp)
                     : null;
            }

            public override MemoryReader.Converter<double> GetMemoryConverter(MemoryReader mreader,
                                                                              Debugger debugger, // TODO - remove
                                                                              string name, string type)
            {
                // NOTE: Because it can be created by derived class
                //   and these members can be set to invalid values
                //   e.g. BGReferringSegment
                if (sizeOf <= 0
                 || Debugger.IsInvalidOffset(sizeOf, firstDiff, secondDiff))
                    return null;

                string first = name + ".first";
                MemoryReader.Converter<double> pointConverter = pointLoader.GetMemoryConverter(mreader, debugger, first, pointType);
                if (pointConverter == null)
                    return null;

                return new MemoryReader.StructConverter<double>(sizeOf,
                            new MemoryReader.Member<double>(pointConverter, (int)firstDiff),
                            new MemoryReader.Member<double>(pointConverter, (int)secondDiff));
            }

            private readonly PointLoader pointLoader;
            private readonly string pointType;
            private readonly long firstDiff;
            private readonly long secondDiff;
            private readonly int sizeOf;
        }

        class BGReferringSegment : BGSegment
        {
            public new class LoaderCreator : ExpressionLoader.ILoaderCreator
            {
                public bool IsUserDefined() { return false; }
                public Kind Kind() { return ExpressionLoader.Kind.Segment; }
                public Loader Create(Loaders loaders, Debugger debugger, string name, string type, string id)
                {
                    if (id != "boost::geometry::model::referring_segment")
                        return null;

                    List<string> tparams = Util.Tparams(type);
                    if (tparams.Count < 1)
                        return null;

                    string first = name + ".first";
                    string pointType = tparams[0];
                    PointLoader pointLoader = loaders.FindByType(ExpressionLoader.Kind.Point,
                                                                 first,
                                                                 pointType) as PointLoader;

                    return new BGReferringSegment(pointLoader, pointType);
                }
            }

            private BGReferringSegment(PointLoader pointLoader, string pointType)
                : base(pointLoader, pointType, 0, -1, -1)
            { }
        }

        class BGNSphere : NSphereLoader
        {
            public class LoaderCreator : ExpressionLoader.ILoaderCreator
            {
                public bool IsUserDefined() { return false; }
                public Kind Kind() { return ExpressionLoader.Kind.NSphere; }
                public Loader Create(Loaders loaders, Debugger debugger, string name, string type, string id)
                {
                    if (id != "boost::geometry::model::nsphere")
                        return null;

                    List<string> tparams = Util.Tparams(type);
                    if (tparams.Count < 1)
                        return null;

                    string m_center = name + ".m_center";
                    string m_radius = name + ".m_radius";

                    string pointType = tparams[0];
                    PointLoader pointLoader = loaders.FindByType(ExpressionLoader.Kind.Point,
                                                                 m_center,
                                                                 pointType) as PointLoader;

                    string radiusType = tparams[1];
                    
                    return pointLoader != null
                        && debugger.GetTypeSizeof(type, out int sizeOf)
                        && debugger.GetAddressOffset(name, m_center, out long centerDiff)
                        && debugger.GetAddressOffset(name, m_radius, out long radiusDiff)
                        && debugger.GetCppSizeof(radiusType, out int radiusSize)
                        && !Debugger.IsInvalidOffset(sizeOf, centerDiff, radiusDiff)
                         ? new BGNSphere(pointLoader, pointType, radiusType, radiusSize, sizeOf, centerDiff, radiusDiff)
                         : null;
                }
            }

            private BGNSphere(PointLoader pointLoader, string pointType, string radiusType, int radiusSize, int sizeOf, long centerDiff, long radiusDiff)
            {
                this.pointLoader = pointLoader;
                this.pointType = pointType;
                this.radiusType = radiusType;
                this.radiusSize = radiusSize;
                this.sizeOf = sizeOf;
                this.centerDiff = centerDiff;
                this.radiusDiff = radiusDiff;
            }

            public override Geometry.Traits GetTraits(MemoryReader mreader, Debugger debugger,
                                                      string name)
            {
                return pointLoader.GetTraits(mreader, debugger, name);
            }

            public override ExpressionDrawer.IDrawable Load(MemoryReader mreader, Debugger debugger,
                                                            string name, string type,
                                                            LoadCallback callback) // dummy callback
            {
                string m_center = name + ".m_center";
                string m_radius = name + ".m_radius";

                Geometry.Point center = pointLoader.LoadPoint(mreader, debugger, m_center, pointType);
                bool ok = debugger.TryLoadDouble(m_radius, out double radius);

                return Util.IsOk(center, ok)
                     ? new ExpressionDrawer.NSphere(center, radius)
                     : null;
            }

            public override MemoryReader.Converter<double> GetMemoryConverter(MemoryReader mreader,
                                                                              Debugger debugger, // TODO - remove
                                                                              string name, string type)
            {
                // NOTE: In case it was created by derived class and these members set to invalid values
                if (sizeOf <= 0
                 || Debugger.IsInvalidOffset(sizeOf, centerDiff, radiusDiff))
                    return null;

                string m_center = name + ".m_center";
                MemoryReader.Converter<double> pointConverter = pointLoader.GetMemoryConverter(mreader, debugger, m_center, pointType);
                if (pointConverter == null)
                    return null;

                MemoryReader.Converter<double> radiusConverter = mreader.GetNumericConverter(radiusType, radiusSize);

                return new MemoryReader.StructConverter<double>(sizeOf,
                            new MemoryReader.Member<double>(pointConverter, (int)centerDiff),
                            new MemoryReader.Member<double>(radiusConverter, (int)radiusDiff));
            }

            private readonly PointLoader pointLoader;
            private readonly string pointType;
            private readonly string radiusType;
            private readonly int radiusSize;
            private readonly long centerDiff;
            private readonly long radiusDiff;
            private readonly int sizeOf;
        }

        class BGRange<ResultType> : PointRange<ResultType>
            where ResultType : class
                             , ExpressionDrawer.IDrawable
                             , Geometry.IContainer<Geometry.Point>
                             , new()
        {
            public class LoaderCreator : ExpressionLoader.ILoaderCreator
            {
                public delegate Loader DerivedConstructor(ContainerLoader containerLoader, string containerType,
                                                          PointLoader pointLoader, string pointType);

                public LoaderCreator(Kind kind, string id,
                                     int pointTIndex, int containerTIndex, int allocatorTIndex,
                                     DerivedConstructor derivedConstructor)
                {
                    this.kind = kind;
                    this.id = id;
                    this.pointTIndex = pointTIndex;
                    this.containerTIndex = containerTIndex;
                    this.allocatorTIndex = allocatorTIndex;
                    this.derivedConstructor = derivedConstructor;
                }
                public bool IsUserDefined() { return false; }
                public Kind Kind() { return kind; }
                public Loader Create(Loaders loaders, Debugger debugger, string name, string type, string id)
                {
                    if (id != this.id)
                        return null;

                    GetBGContainerInfo(type, pointTIndex, containerTIndex, allocatorTIndex,
                                       out string pointType, out string containerType);

                    ContainerLoader containerLoader = loaders.FindByType(ExpressionLoader.Kind.Container,
                                                                         name,
                                                                         containerType) as ContainerLoader;
                    if (containerLoader == null)
                        return null;

                    containerLoader.ElementInfo(name, containerType, out string pointName, out string _);

                    PointLoader pointLoader = loaders.FindByType(ExpressionLoader.Kind.Point,
                                                                 pointName,
                                                                 pointType) as PointLoader;
                    if (pointLoader == null)
                        return null;

                    return derivedConstructor(containerLoader, containerType, pointLoader, pointType);
                }

                private readonly Kind kind;
                private readonly string id;
                private readonly int pointTIndex;
                private readonly int containerTIndex;
                private readonly int allocatorTIndex;
                private readonly DerivedConstructor derivedConstructor;
            }

            protected BGRange(ContainerLoader containerLoader, string containerType,
                              PointLoader pointLoader, string pointType)
            {
                this.containerLoader = containerLoader;
                this.containerType = containerType;
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
                ResultType result = null;

                if (mreader != null)
                {
                    containerLoader.ElementInfo(name, containerType, out string pointName, out string _);

                    result = LoadMemory(mreader, debugger, name, type,
                                        pointName, pointType, pointLoader,
                                        containerLoader, callback);
                }

                if (result == null)
                {
                    result = LoadParsed(mreader, debugger, name, type,
                                        pointType, pointLoader,
                                        containerLoader, callback);
                }

                return result;
            }

            readonly ContainerLoader containerLoader;
            readonly string containerType;
            readonly PointLoader pointLoader;
            readonly string pointType;
        }

        class BGLinestring : BGRange<ExpressionDrawer.Linestring>
        {
            public new class LoaderCreator : BGRange<ExpressionDrawer.Linestring>.LoaderCreator
            {
                public LoaderCreator()
                    : base(ExpressionLoader.Kind.Linestring,
                           "boost::geometry::model::linestring",
                           0, 1, 2,
                           delegate (ContainerLoader containerLoader, string containerType,
                                     PointLoader pointLoader, string pointType)
                                     {
                                         return new BGLinestring(containerLoader, containerType,
                                                                 pointLoader, pointType);
                                     })
                { }
            }

            private BGLinestring(ContainerLoader containerLoader, string containerType,
                                 PointLoader pointLoader, string pointType)
                : base(containerLoader, containerType, pointLoader, pointType)
            { }
        }

        class BGMultiLinestring : RangeLoader<ExpressionDrawer.MultiLinestring>
        {
            public class LoaderCreator : ExpressionLoader.ILoaderCreator
            {
                public bool IsUserDefined() { return false; }
                public Kind Kind() { return ExpressionLoader.Kind.MultiLinestring; }
                public Loader Create(Loaders loaders, Debugger debugger, string name, string type, string id)
                {
                    if (id != "boost::geometry::model::multi_linestring")
                        return null;

                    GetBGContainerInfo(type, 0, 1, 2, out string lsType, out string containerType);

                    ContainerLoader containerLoader = loaders.FindByType(ExpressionLoader.Kind.Container,
                                                                         name,
                                                                         containerType) as ContainerLoader;
                    if (containerLoader == null)
                        return null;

                    containerLoader.ElementInfo(name, containerType, out string lsName, out string _);

                    RangeLoader<ExpressionDrawer.Linestring>
                        lsLoader = loaders.FindByType(ExpressionLoader.Kind.Linestring,
                                                      lsName,
                                                      lsType) as RangeLoader<ExpressionDrawer.Linestring>;
                    if (lsLoader == null)
                        return null;

                    return new BGMultiLinestring(containerLoader, lsLoader, lsType);
                }
            }

            private BGMultiLinestring(ContainerLoader containerLoader,
                                      RangeLoader<ExpressionDrawer.Linestring> lsLoader,
                                      string lsType)
            {
                this.containerLoader = containerLoader;
                this.lsLoader = lsLoader;
                this.lsType = lsType;
            }

            public override Geometry.Traits GetTraits(MemoryReader mreader, Debugger debugger,
                                                      string name)
            {
                return lsLoader.GetTraits(mreader, debugger, name);
            }

            public override ExpressionDrawer.IDrawable Load(MemoryReader mreader, Debugger debugger,
                                                            string name, string type,
                                                            LoadCallback callback)
            {
                ExpressionDrawer.MultiLinestring mls = new ExpressionDrawer.MultiLinestring();
                bool ok = containerLoader.ForEachElement(debugger, name, delegate (string elName)
                {
                    ExpressionDrawer.Linestring ls = lsLoader.Load(mreader, debugger,
                                                                   elName, lsType,
                                                                   callback) as ExpressionDrawer.Linestring;
                    if (ls == null)
                        return false;
                    mls.Add(ls);
                    //return callback();
                    return true;
                });
                return ok ? mls : null;
            }

            readonly ContainerLoader containerLoader;
            readonly RangeLoader<ExpressionDrawer.Linestring> lsLoader;
            readonly string lsType;
        }

        class BGRing : BGRange<ExpressionDrawer.Ring>
        {
            public new class LoaderCreator : BGRange<ExpressionDrawer.Ring>.LoaderCreator
            {
                public LoaderCreator()
                    : base(ExpressionLoader.Kind.Ring,
                           "boost::geometry::model::ring",
                           0, 3, 4,
                           delegate (ContainerLoader containerLoader, string containerType,
                                     PointLoader pointLoader, string pointType)
                                     {
                                         return new BGRing(containerLoader, containerType,
                                                           pointLoader, pointType);
                                     })
                { }
            }

            private BGRing(ContainerLoader containerLoader, string containerType,
                           PointLoader pointLoader, string pointType)
                : base(containerLoader, containerType, pointLoader, pointType)
            { }
        }

        class BGMultiPoint : BGRange<ExpressionDrawer.MultiPoint>
        {
            public new class LoaderCreator : BGRange<ExpressionDrawer.MultiPoint>.LoaderCreator
            {
                public LoaderCreator()
                    : base(ExpressionLoader.Kind.MultiPoint,
                           "boost::geometry::model::multi_point",
                           0, 1, 2,
                           delegate (ContainerLoader containerLoader, string containerType,
                                     PointLoader pointLoader, string pointType)
                                     {
                                         return new BGMultiPoint(containerLoader, containerType,
                                                                 pointLoader, pointType);
                                     })
                { }
            }

            private BGMultiPoint(ContainerLoader containerLoader, string containerType,
                                 PointLoader pointLoader, string pointType)
                : base(containerLoader, containerType, pointLoader, pointType)
            { }
        }

        class BGPolygon : PolygonLoader
        {
            public class LoaderCreator : ExpressionLoader.ILoaderCreator
            {
                public bool IsUserDefined() { return false; }
                public Kind Kind() { return ExpressionLoader.Kind.Polygon; }
                public Loader Create(Loaders loaders, Debugger debugger, string name, string type, string id)
                {
                    if (id != "boost::geometry::model::polygon")
                        return null;

                    string outerName = name + ".m_outer";
                    string innersName = name + ".m_inners";

                    string outerType = debugger.GetValueType(outerName);
                    if (outerType == null)
                        return null;
                    BGRing outerLoader = loaders.FindByType(ExpressionLoader.Kind.Ring,
                                                            outerName, outerType) as BGRing;
                    if (outerLoader == null)
                        return null;

                    string innersType = debugger.GetValueType(innersName);
                    if (innersType == null)
                        return null;
                    ContainerLoader innersLoader = loaders.FindByType(ExpressionLoader.Kind.Container,
                                                                      innersName, innersType) as ContainerLoader;
                    if (innersLoader == null)
                        return null;

                    return new BGPolygon(outerLoader, outerType, innersLoader);
                }
            }

            // TODO: Should this be BGRing or a generic ring Loader?
            private BGPolygon(BGRing outerLoader, string outerType,
                              ContainerLoader innersLoader)
            {
                this.outerLoader = outerLoader;
                this.outerType = outerType;
                this.innersLoader = innersLoader;
            }

            public override Geometry.Traits GetTraits(MemoryReader mreader, Debugger debugger,
                                                      string name)
            {
                return outerLoader.GetTraits(mreader, debugger, name);
            }

            public override ExpressionDrawer.IDrawable Load(MemoryReader mreader, Debugger debugger,
                                                            string name, string type,
                                                            LoadCallback callback)
            {
                string outerName = name + ".m_outer";
                string innersName = name + ".m_inners";

                ExpressionDrawer.Ring outer = outerLoader.Load(mreader, debugger,
                                                               outerName, outerType,
                                                               callback) as ExpressionDrawer.Ring;
                if (outer == null)
                    return null;

                List<Geometry.Ring> inners = new List<Geometry.Ring>();
                bool ok = innersLoader.ForEachElement(debugger, innersName, delegate (string elName)
                {
                    ExpressionDrawer.Ring inner = outerLoader.Load(mreader, debugger,
                                                                   elName, outerType,
                                                                   callback) as ExpressionDrawer.Ring;
                    if (inner == null)
                        return false;
                    inners.Add(inner);
                    //return callback();
                    return true;
                });

                return ok
                     ? new ExpressionDrawer.Polygon(outer, inners)
                     : null;
            }

            readonly BGRing outerLoader;
            readonly string outerType;
            readonly ContainerLoader innersLoader;
        }

        class BGMultiPolygon : RangeLoader<ExpressionDrawer.MultiPolygon>
        {
            public class LoaderCreator : ExpressionLoader.ILoaderCreator
            {
                public bool IsUserDefined() { return false; }
                public Kind Kind() { return ExpressionLoader.Kind.MultiPolygon; }
                public Loader Create(Loaders loaders, Debugger debugger, string name, string type, string id)
                {
                    if (id != "boost::geometry::model::multi_polygon")
                        return null;

                    GetBGContainerInfo(type, 0, 1, 2, out string polyType, out string containerType);

                    ContainerLoader containerLoader = loaders.FindByType(ExpressionLoader.Kind.Container,
                                                                         name,
                                                                         containerType) as ContainerLoader;
                    if (containerLoader == null)
                        return null;

                    containerLoader.ElementInfo(name, containerType, out string polyName, out string _);

                    PolygonLoader polyLoader = loaders.FindByType(ExpressionLoader.Kind.Polygon,
                                                                  polyName,
                                                                  polyType) as PolygonLoader;
                    if (polyLoader == null)
                        return null;

                    return new BGMultiPolygon(containerLoader, polyLoader, polyType);
                }
            }

            private BGMultiPolygon(ContainerLoader containerLoader,
                                   PolygonLoader polyLoader,
                                   string polyType)
            {
                this.containerLoader = containerLoader;
                this.polyLoader = polyLoader;
                this.polyType = polyType;
            }

            public override Geometry.Traits GetTraits(MemoryReader mreader, Debugger debugger,
                                                      string name)
            {
                return polyLoader.GetTraits(mreader, debugger, name);
            }

            public override ExpressionDrawer.IDrawable Load(MemoryReader mreader, Debugger debugger,
                                                            string name, string type,
                                                            LoadCallback callback)
            {
                ExpressionDrawer.MultiPolygon mpoly = new ExpressionDrawer.MultiPolygon();
                bool ok = containerLoader.ForEachElement(debugger, name, delegate (string elName)
                {
                    ExpressionDrawer.Polygon poly = polyLoader.Load(mreader, debugger,
                                                                    elName, polyType,
                                                                    callback) as ExpressionDrawer.Polygon;
                    if (poly == null)
                        return false;
                    mpoly.Add(poly);
                    //return callback();
                    return true;
                });
                return ok ? mpoly : null;
            }

            readonly ContainerLoader containerLoader;
            readonly PolygonLoader polyLoader;
            readonly string polyType;
        }

        class BGBufferedRing : RangeLoader<ExpressionDrawer.Ring>
        {
            public class LoaderCreator : ExpressionLoader.ILoaderCreator
            {
                public bool IsUserDefined() { return false; }
                public Kind Kind() { return ExpressionLoader.Kind.Ring; }
                public Loader Create(Loaders loaders, Debugger debugger, string name, string type, string id)
                {
                    if (id != "boost::geometry::detail::buffer::buffered_ring")
                        return null;

                    List<string> tparams = Util.Tparams(type);
                    if (tparams.Count < 1)
                        return null;

                    string ringType = tparams[0];
                    RangeLoader<ExpressionDrawer.Ring>
                        ringLoader = loaders.FindByType(ExpressionLoader.Kind.Ring,
                                                        name,
                                                        ringType) as RangeLoader<ExpressionDrawer.Ring>;
                    if (ringLoader == null)
                        return null;

                    return new BGBufferedRing(ringLoader, ringType);
                }
            }

            private BGBufferedRing(RangeLoader<ExpressionDrawer.Ring> ringLoader,
                                   string ringType)
            {
                this.ringLoader = ringLoader;
                this.ringType = ringType;
            }

            public override Geometry.Traits GetTraits(MemoryReader mreader, Debugger debugger,
                                                      string name)
            {
                return ringLoader.GetTraits(mreader, debugger, name);
            }

            public override ExpressionDrawer.IDrawable Load(MemoryReader mreader, Debugger debugger,
                                                            string name, string type,
                                                            LoadCallback callback)
            {
                return ringLoader.Load(mreader, debugger, name, ringType, callback);
            }

            readonly RangeLoader<ExpressionDrawer.Ring> ringLoader;
            readonly string ringType;
        }

        // NOTE: There is no MultiRing concept so use MultiPolygon for now
        class BGBufferedRingCollection : RangeLoader<ExpressionDrawer.MultiPolygon>
        {
            public class LoaderCreator : ExpressionLoader.ILoaderCreator
            {
                public bool IsUserDefined() { return false; }
                public Kind Kind() { return ExpressionLoader.Kind.MultiPolygon; } // Or MultiGeometry
                public Loader Create(Loaders loaders, Debugger debugger, string name, string type, string id)
                {
                    if (id != "boost::geometry::detail::buffer::buffered_ring_collection")
                        return null;

                    List<string> tparams = Util.Tparams(type);
                    if (tparams.Count < 1)
                        return null;

                    string ringType = tparams[0];
                    string containerType = StdContainerType("std::vector", ringType, "std::allocator");

                    ContainerLoader containerLoader = loaders.FindByType(ExpressionLoader.Kind.Container,
                                                                         name,
                                                                         containerType) as ContainerLoader;
                    if (containerLoader == null)
                        return null;

                    containerLoader.ElementInfo(name, containerType, out string ringName, out string _);

                    RangeLoader<ExpressionDrawer.Ring>
                        ringLoader = loaders.FindByType(ExpressionLoader.Kind.Ring,
                                                        ringName,
                                                        ringType) as RangeLoader<ExpressionDrawer.Ring>;
                    if (ringLoader == null)
                        return null;

                    return new BGBufferedRingCollection(containerLoader, ringLoader, ringType);
                }
            }

            private BGBufferedRingCollection(ContainerLoader containerLoader,
                                             RangeLoader<ExpressionDrawer.Ring> ringLoader, string ringType)
            {
                this.containerLoader = containerLoader;
                this.ringLoader = ringLoader;
                this.ringType = ringType;
            }

            public override Geometry.Traits GetTraits(MemoryReader mreader, Debugger debugger,
                                                      string name)
            {
                return ringLoader.GetTraits(mreader, debugger, name);
            }

            public override ExpressionDrawer.IDrawable Load(MemoryReader mreader, Debugger debugger,
                                                            string name, string type,
                                                            LoadCallback callback)
            {
                ExpressionDrawer.MultiPolygon mpoly = new ExpressionDrawer.MultiPolygon();
                bool ok = containerLoader.ForEachElement(debugger, name, delegate (string elName)
                {
                    ExpressionDrawer.Ring ring = ringLoader.Load(mreader, debugger,
                                                                 elName, ringType,
                                                                 callback) as ExpressionDrawer.Ring;
                    if (ring == null)
                        return false;
                    mpoly.Add(new ExpressionDrawer.Polygon(ring));
                    //return callback();
                    return true;
                });
                return ok ? mpoly : null;
            }

            readonly ContainerLoader containerLoader;
            readonly RangeLoader<ExpressionDrawer.Ring> ringLoader;
            readonly string ringType;
        }

        // NOTE: Technically R-tree could be treated as a Container of Points or MultiPoint
        //       and displayed in a PlotWatch.
        // TODO: Consider this.

        class BGIRtree : GeometryLoader<ExpressionDrawer.DrawablesContainer>
        {
            public class LoaderCreator : ExpressionLoader.ILoaderCreator
            {
                public bool IsUserDefined() { return false; }
                public Kind Kind() { return ExpressionLoader.Kind.OtherGeometry; }
                public Loader Create(Loaders loaders, Debugger debugger, string name, string type, string id)
                {
                    if (id != "boost::geometry::index::rtree")
                        return null;

                    try
                    {
                        return new BGIRtree(loaders, debugger, name, type);
                    }
                    catch(CreationException)
                    {
                        return null;
                    }
                }
            }

            private class CreationException : Exception
            { }

            private BGIRtree(Loaders loaders, Debugger debugger, string name, string type)
            {
                indexableLoader = IndexableLoader(loaders, debugger,
                                                  name, type,
                                                  out string _,
                                                  out indexableMember,
                                                  out indexableType);
                if (indexableLoader == null)
                    throw new CreationException();

                // TODO: This is not fully correct.
                // The traits can technically be dynamic as in case of BVariant.
                // So they should be gathered from the actual data.
                // NOTE mreader = null
                traits = indexableLoader.GetTraits(null, debugger,
                                                   "(*((" + indexableType + "*)((void*)0)))");
                if (traits == null)
                    throw new CreationException();

                string nodePtrName = RootNodePtr(name);
                string nodeVariantName = "*" + nodePtrName;
                string nodeVariantType = debugger.GetValueType(nodeVariantName);
                if (Util.Empty(nodeVariantType))
                    throw new CreationException();

                if (!Util.Tparams(nodeVariantType, out leafType, out internalNodeType))
                    throw new CreationException();

                leafElementsLoader = ElementsLoader(loaders, debugger,
                                                    nodePtrName, leafType,
                                                    out string leafElemsName, out leafElemsType);
                if (leafElementsLoader == null)
                    throw new CreationException();

                internalNodeElementsLoader = ElementsLoader(loaders, debugger,
                                                            nodePtrName, internalNodeType,
                                                            out string internalNodeElemsName, out internalNodeElemsType);
                if (internalNodeElementsLoader == null)
                    throw new CreationException();

                // For Memory Loading

                nodePtrType = debugger.GetValueType(nodePtrName);
                // TODO: Handle return values
                debugger.GetTypeSizeof(nodePtrType, out nodePtrSizeOf);                
                debugger.GetAddressOffset(nodeVariantName, NodeElements(nodePtrName, leafType), out leafElemsDiff);
                debugger.GetAddressOffset(nodeVariantName, NodeElements(nodePtrName, internalNodeType), out internalNodeElemsDiff);

                leafElementsLoader.ElementInfo(leafElemsName, leafElemsType,
                                               out string leafElemName, out string _);
                internalNodeElementsLoader.ElementInfo(internalNodeElemsName, internalNodeElemsType,
                                                       out string internalNodeElemName, out string _);

                // TODO: Handle return values
                debugger.GetAddressOffset(leafElemName, leafElemName + indexableMember, out indexableDiff);
                debugger.GetAddressOffset(internalNodeElemName, internalNodeElemName + ".second", out nodePtrDiff);

                string whichName = "(" + nodeVariantName + ").which_";
                whichType = debugger.GetValueType(whichName);

                // TODO: Handle return values
                debugger.GetTypeSizeof(whichType, out whichSizeOf);
                debugger.GetAddressOffset(nodeVariantName, whichName, out whichDiff);
                debugger.GetValueSizeof(nodeVariantName, out nodeVariantSizeof);
                debugger.GetValueSizeof(internalNodeElemName, out nodePtrPairSizeof);
                debugger.GetValueSizeof(leafElemName, out valueSizeof);
            }

            public override Geometry.Traits GetTraits(MemoryReader mreader, Debugger debugger,
                                                      string name)
            {
                return traits;
            }

            public override ExpressionDrawer.IDrawable Load(MemoryReader mreader, Debugger debugger,
                                                            string name, string type,
                                                            LoadCallback callback)
            {
                int size = LoadSize(debugger, name);
                if (size <= 0)
                    return null;

                string nodePtrName = RootNodePtr(name);

                LoadMemory(mreader, debugger, nodePtrName, out ExpressionDrawer.DrawablesContainer result, callback);

                if (result == null)
                {
                    ExpressionDrawer.DrawablesContainer res = new ExpressionDrawer.DrawablesContainer();
                    if (LoadParsedRecursive(mreader, debugger,
                                            nodePtrName,
                                            res,
                                            callback))
                    {
                        result = res;
                    }
                }

                return result;
            }

            private void LoadMemory(MemoryReader mreader, Debugger debugger,
                                    string nodePtrName,
                                    out ExpressionDrawer.DrawablesContainer result,
                                    LoadCallback callback)
            {
                result = null;

                string nodeVariantName = "*" + nodePtrName;
                //string whichName = "(" + nodeVariantName + ").which_";

                if (mreader == null)
                    return;

                // TODO: replace with something else
                if (leafElemsDiff < 0 || internalNodeElemsDiff < 0 || indexableDiff < 0 || nodePtrDiff < 0 || whichDiff < 0)
                    return;

                 if (!debugger.GetValueAddress(nodeVariantName, out ulong rootAddr))
                    return;

                MemoryReader.Converter<int> whichConverter = mreader.GetValueConverter<int>(whichType, whichSizeOf);
                if (whichConverter == null)
                    return;

                if (nodeVariantSizeof <= 0)
                    return;

                MemoryReader.StructConverter<int> nodeVariantConverter =
                    new MemoryReader.StructConverter<int>(
                        nodeVariantSizeof,
                        new MemoryReader.Member<int>(whichConverter, (int)whichDiff));

                MemoryReader.ValueConverter<ulong> nodePtrConverter =
                    mreader.GetPointerConverter(nodePtrType, nodePtrSizeOf);
                if (nodePtrConverter == null)
                    return;

                MemoryReader.Converter<ulong> nodePtrPairConverter =
                    new MemoryReader.StructConverter<ulong>(
                        nodePtrPairSizeof,
                        new MemoryReader.Member<ulong>(nodePtrConverter, (int)nodePtrDiff));

                string leafElemsName = NodeElements(nodePtrName, leafType);
                leafElementsLoader.ElementInfo(leafElemsName, leafElemsType,
                                               out string leafElemName, out string leafElemType);

                string indexableName = leafElemName + indexableMember;

                string internalNodeElemsName = NodeElements(nodePtrName, internalNodeType);
                internalNodeElementsLoader.ElementInfo(internalNodeElemsName, internalNodeElemsType,
                                                       out string internalNodeElemName, out string internalNodeElemType);

                MemoryReader.Converter<double> indexableConverter =
                    indexableLoader.GetMemoryConverter(mreader, debugger, indexableName, indexableType);
                if (indexableConverter == null)
                    return;

                MemoryReader.Converter<double> valueConverter = indexableConverter;

                if (indexableMember != "" /*&& indexableDiff > 0*/)
                {
                    valueConverter = new MemoryReader.StructConverter<double>(
                        valueSizeof,
                        new MemoryReader.Member<double>(indexableConverter, (int)indexableDiff));
                }

                ExpressionDrawer.DrawablesContainer res = new ExpressionDrawer.DrawablesContainer();
                if (LoadMemoryRecursive(mreader, debugger,
                                        rootAddr,
                                        nodeVariantConverter,
                                        valueConverter,
                                        nodePtrPairConverter,
                                        res,
                                        callback))
                {
                    result = res;                      
                }
            }

            private bool LoadMemoryRecursive(MemoryReader mreader, Debugger debugger,
                                             ulong nodeAddr,
                                             MemoryReader.StructConverter<int> nodeVariantConverter,
                                             MemoryReader.Converter<double> valueConverter,
                                             MemoryReader.Converter<ulong> nodePtrPairConverter,
                                             ExpressionDrawer.DrawablesContainer result,
                                             LoadCallback callback)
            {
                int[] which = new int[1];
                if (!mreader.Read(nodeAddr, which, nodeVariantConverter))
                    return false;

                if (which[0] == 0) // leaf
                {
                    ulong leafElemsAddress = nodeAddr + (ulong)leafElemsDiff;
                    if (!leafElementsLoader.ForEachMemoryBlock(mreader, debugger,
                            "", "",
                            leafElemsAddress,
                            valueConverter,
                            delegate (double[] values)
                            {
                                if (indexableLoader is PointLoader)
                                {
                                    if (values.Length % 2 != 0)
                                        return false;
                                    for (int i = 0; i < values.Length / 2; ++i)
                                        result.Add(new ExpressionDrawer.Point(values[i * 2],
                                                                              values[i * 2 + 1]));
                                }
                                else if (indexableLoader is BoxLoader)
                                {
                                    if (values.Length % 4 != 0)
                                        return false;
                                    for (int i = 0; i < values.Length / 4; ++i)
                                        result.Add(new ExpressionDrawer.Box(
                                            new ExpressionDrawer.Point(values[i * 4],
                                                                       values[i * 4 + 1]),
                                            new ExpressionDrawer.Point(values[i * 4 + 2],
                                                                       values[i * 4 + 3])));
                                }
                                else if (indexableLoader is SegmentLoader)
                                {
                                    if (values.Length % 4 != 0)
                                        return false;
                                    for (int i = 0; i < values.Length / 4; ++i)
                                        result.Add(new ExpressionDrawer.Segment(
                                            new ExpressionDrawer.Point(values[i * 4],
                                                                       values[i * 4 + 1]),
                                            new ExpressionDrawer.Point(values[i * 4 + 2],
                                                                       values[i * 4 + 3])));
                                }
                                else
                                    return false;
                                return true;
                            }))
                        return false;
                }
                else if (which[0] == 1) // internal node
                {
                    ulong internalNodeElemsAddress = nodeAddr + (ulong)internalNodeElemsDiff;
                    if (!internalNodeElementsLoader.ForEachMemoryBlock(mreader, debugger,
                            "", "",
                            internalNodeElemsAddress, nodePtrPairConverter,
                            delegate (ulong[] ptrs)
                            {
                                foreach (ulong addr in ptrs)
                                {
                                    if (!LoadMemoryRecursive(mreader, debugger,
                                                             addr,
                                                             nodeVariantConverter,
                                                             valueConverter,
                                                             nodePtrPairConverter,
                                                             result,
                                                             callback))
                                    {
                                        return false;
                                    }
                                }
                                return true;
                            }))
                        return false;
                }
                else
                    return false;

                callback();
                return true;
            }
            
            private bool LoadParsedRecursive(MemoryReader mreader, Debugger debugger,
                                             string nodePtrName,
                                             ExpressionDrawer.DrawablesContainer result,
                                             LoadCallback callback)
            {
                if (!IsLeaf(debugger, nodePtrName, out bool isLeaf))
                    return false;

                string nodeType = leafType;
                ContainerLoader elementsLoader = leafElementsLoader;
                if (!isLeaf)
                {
                    nodeType = internalNodeType;
                    elementsLoader = internalNodeElementsLoader;
                }
                string elementsName = NodeElements(nodePtrName, nodeType);

                bool ok = elementsLoader.ForEachElement(debugger, elementsName, delegate (string elName)
                {
                    if (isLeaf)
                    {
                        ExpressionDrawer.IDrawable indexable = indexableLoader.Load(
                            mreader, debugger,
                            elName + indexableMember, indexableType,
                            callback); // rather dummy callback

                        if (indexable == null)
                            return false;

                        result.Add(indexable);
                    }
                    else
                    {
                        string nextNodePtrName = elName + ".second";
                        if (!LoadParsedRecursive(mreader, debugger,
                                                 nextNodePtrName,
                                                 result,
                                                 callback))
                            return false;
                    }

                    callback();
                    return true;
                });

                return ok;
            }

            static DrawableLoader IndexableLoader(Loaders loaders, Debugger debugger,
                                                  string name, string type,
                                                  out string valueType,
                                                  out string indexableMember,
                                                  out string indexableType)
            {
                indexableMember = "";
                indexableType = "";

                if (!Util.Tparams(type, out valueType))
                    return null;

                // NOTE: Casting the address 0 is not correct because in some cases
                // addresses can be calculated on loader creation and address 0 is
                // currently reserved as invalid.
                // So below the address of the R-tree object is used.
                if (!debugger.GetValueAddress(name, out ulong address))
                    return null;
                string addressStr = address.ToString();

                string valueId = Util.TypeId(valueType);
                DrawableLoader indexableLoader = null;
                indexableMember = "";
                indexableType = valueType;
                if (valueId == "std::pair" || valueId == "std::tuple" || valueId == "boost::tuple")
                {
                    if (!Util.Tparams(valueType, out string firstType))
                        return null;
                    
                    indexableLoader = loaders.FindByType(OnlyIndexables,
                                                         "(*((" + firstType + "*)" + addressStr + "))",
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
                    // NOTE: Casting the address 0 is not correct because in some cases
                    // addresses can be calculated on loader creation and address 0 is
                    // currently reserved as invalid.
                    indexableLoader = loaders.FindByType(OnlyIndexables,
                                                         "(*((" + indexableType + "*)" + addressStr + "))",
                                                         indexableType) as DrawableLoader;
                }

                return indexableLoader;
            }

            static ContainerLoader ElementsLoader(Loaders loaders, Debugger debugger,
                                                  string nodePtrName, string castedNodeType,
                                                  out string elementsName, out string containerType)
            {
                elementsName = NodeElements(nodePtrName, castedNodeType);
                containerType = "";

                Expression expr = debugger.GetExpression(elementsName);
                if (!expr.IsValid)
                    return null;

                elementsName = expr.Name;
                containerType = expr.Type;

                return loaders.FindByType(ExpressionLoader.Kind.Container,
                                          expr.Name, expr.Type) as ContainerLoader;
            }

            int LoadSize(Debugger debugger, string name)
            {
                return debugger.TryLoadUInt(name + ".m_members.values_count", out int size) ? size : 0;
            }

            static string RootNodePtr(string name)
            {
                return name + ".m_members.root";
            }

            static string NodeElements(string nodePtrName, string castedNodeType)
            {
                return "((" + castedNodeType + "*)" + nodePtrName + "->storage_.data_.buf)->elements";
            }

            static bool IsLeaf(Debugger debugger, string nodePtrName, out bool result)
            {
                result = false;

                if (!debugger.TryLoadInt(nodePtrName + "->which_", out int which))
                    return false;

                result = (which == 0);
                return true;
            }

            readonly ContainerLoader leafElementsLoader;
            readonly ContainerLoader internalNodeElementsLoader;
            readonly DrawableLoader indexableLoader;

            readonly Geometry.Traits traits;

            readonly string leafType;
            readonly string internalNodeType;
            readonly string indexableMember;
            readonly string indexableType;
            readonly string nodePtrType; // pointer to root or a node in internal node
            readonly string leafElemsType; // type of container of values
            readonly string internalNodeElemsType; // type of container of ptr_pair<box, node_ptr>
            readonly string whichType; // type of which_ member

            readonly long leafElemsDiff; // offset of container of values in variant node which is a leaf
            readonly long internalNodeElemsDiff; // offset of container of ptr_pair<box, node_ptr> in variant node which is internal node
            readonly long indexableDiff; // offset of indexable in value
            readonly long nodePtrDiff; // offset of node_ptr in ptr_pair<box, node_ptr>
            readonly long whichDiff; // offset of which_ member of variant node

            readonly int nodePtrSizeOf; // size of pointer (4 or 8)
            readonly int whichSizeOf; // size of which_ member            
            readonly int nodeVariantSizeof; // size of variant node
            readonly int nodePtrPairSizeof; // size of ptr_pair<box, node_ptr>
            readonly int valueSizeof; // size of value
        }

        class BGTurn : GeometryLoader<ExpressionDrawer.Turn>
        {
            public class LoaderCreator : ExpressionLoader.ILoaderCreator
            {
                public LoaderCreator(string id)
                {
                    this.id = id;
                }

                public bool IsUserDefined() { return false; }
                public Kind Kind() { return ExpressionLoader.Kind.Turn; }
                public Loader Create(Loaders loaders, Debugger debugger, string name, string type, string id)
                {
                    if (id != this.id)
                        return null;

                    List<string> tparams = Util.Tparams(type);
                    if (tparams.Count < 1)
                        return null;

                    string pointType = tparams[0];
                    PointLoader pointLoader = loaders.FindByType(ExpressionLoader.Kind.Point,
                                                                 name + ".point",
                                                                 pointType) as PointLoader;
                    if (pointLoader == null)
                        return null;

                    return new BGTurn(pointLoader, pointType);
                }

                readonly string id;
            }

            private BGTurn(PointLoader pointLoader, string pointType)
            {
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
                                                            // rather dummy callback
                                                            LoadCallback callback)
            {
                ExpressionDrawer.Point p = pointLoader.Load(mreader, debugger,
                                                            name + ".point", pointType,
                                                            // rather dummy callback
                                                            callback) as ExpressionDrawer.Point;
                if (p == null)
                    return null;

                char method = '?';
                string methodValue = debugger.GetValue(name + ".method");
                if (methodValue != null)
                    method = MethodChar(methodValue);

                char op0 = '?';
                string op0Value = debugger.GetValue(name + ".operations[0].operation");
                if (op0Value != null)
                    op0 = OperationChar(op0Value);

                char op1 = '?';
                string op1Value = debugger.GetValue(name + ".operations[1].operation");
                if (op1Value != null)
                    op1 = OperationChar(op1Value);

                return new ExpressionDrawer.Turn(p, method, op0, op1);
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

            readonly PointLoader pointLoader;
            readonly string pointType;            
        }

        class BGTurnContainer : GeometryLoader<ExpressionDrawer.TurnsContainer>
        {
            public class LoaderCreator : ExpressionLoader.ILoaderCreator
            {
                public bool IsUserDefined() { return false; }
                public Kind Kind() { return ExpressionLoader.Kind.TurnsContainer; }
                public Loader Create(Loaders loaders, Debugger debugger, string name, string type, string id)
                {
                    // Any type/id, match all containers storing Turns

                    ContainerLoader containerLoader = loaders.FindByType(ExpressionLoader.Kind.Container,
                                                                         name,
                                                                         type) as ContainerLoader;
                    if (containerLoader == null)
                        return null;

                    containerLoader.ElementInfo(name, type, out string turnName, out string turnType);

                    // WARNING: Potentially recursive call, search for Turns only
                    BGTurn turnLoader = loaders.FindByType(ExpressionLoader.Kind.Turn,
                                                           turnName,
                                                           turnType) as BGTurn;
                    if (turnLoader == null)
                        return null;

                    return new BGTurnContainer(containerLoader, turnLoader, turnType);
                }
            }

            private BGTurnContainer(ContainerLoader containerLoader, BGTurn turnLoader, string turnType)
            {
                this.containerLoader = containerLoader;
                this.turnLoader = turnLoader;
                this.turnType = turnType;
            }

            public override Geometry.Traits GetTraits(MemoryReader mreader, Debugger debugger,
                                                      string name)
            {
                return turnLoader.GetTraits(mreader, debugger, name);
            }

            public override ExpressionDrawer.IDrawable Load(MemoryReader mreader, Debugger debugger,
                                                            string name, string type,
                                                            LoadCallback callback)
            {
                List<ExpressionDrawer.Turn> turns = new List<ExpressionDrawer.Turn>();
                bool ok = containerLoader.ForEachElement(debugger, name, delegate (string elName)
                {
                    ExpressionDrawer.Turn turn = turnLoader.Load(mreader, debugger,
                                                                 elName, turnType,
                                                                 // rather dummy callback
                                                                 callback) as ExpressionDrawer.Turn;
                    if (turn == null)
                        return false;
                    turns.Add(turn);
                    callback();
                    return true;
                });
                return ok
                     ? new ExpressionDrawer.TurnsContainer(turns)
                     : null;
            }

            readonly ContainerLoader containerLoader;
            readonly BGTurn turnLoader;
            readonly string turnType;
        }

        class BPPoint : BXPoint
        {
            public class LoaderCreator : ExpressionLoader.ILoaderCreator
            {
                public bool IsUserDefined() { return false; }
                public Kind Kind() { return ExpressionLoader.Kind.Point; }
                public Loader Create(Loaders loaders, Debugger debugger, string name, string type, string id)
                {
                    if (id != "boost::polygon::point_data")
                        return null;

                    List<string> tparams = Util.Tparams(type);
                    if (tparams.Count < 1)
                        return null;
                    string coordType = tparams[0];

                    return new BPPoint(coordType);
                }
            }

            private BPPoint(string coordType)
                : base(".coords_",
                       coordType,
                       new Geometry.Traits(2, Geometry.CoordinateSystem.Cartesian, Geometry.Unit.None))
            { }
        }

        class BPSegment : SegmentLoader
        {
            public class LoaderCreator : ExpressionLoader.ILoaderCreator
            {
                public bool IsUserDefined() { return false; }
                public Kind Kind() { return ExpressionLoader.Kind.Segment; }
                public Loader Create(Loaders loaders, Debugger debugger, string name, string type, string id)
                {
                    return id == "boost::polygon::segment_data"
                         ? new BPSegment()
                         : null;
                }
            }

            public override Geometry.Traits GetTraits(MemoryReader mreader, Debugger debugger,
                                                      string name)
            {
                return new Geometry.Traits(2, Geometry.CoordinateSystem.Cartesian, Geometry.Unit.None);
            }

            public override ExpressionDrawer.IDrawable Load(MemoryReader mreader, Debugger debugger,
                                                            string name, string type,
                                                            LoadCallback callback) // dummy callback
            {
                if (debugger.TryLoadDouble(name + ".points_[0].coords_[0]", out double x0)
                 && debugger.TryLoadDouble(name + ".points_[0].coords_[1]", out double y0)
                 && debugger.TryLoadDouble(name + ".points_[1].coords_[0]", out double x1)
                 && debugger.TryLoadDouble(name + ".points_[1].coords_[1]", out double y1))
                {
                    Geometry.Point first_p = new Geometry.Point(x0, y0);
                    Geometry.Point second_p = new Geometry.Point(x1, y1);

                    return new ExpressionDrawer.Segment(first_p, second_p);
                }
                return null;
            }
        }

        class BPBox : BoxLoader
        {
            public class LoaderCreator : ExpressionLoader.ILoaderCreator
            {
                public bool IsUserDefined() { return false; }
                public Kind Kind() { return ExpressionLoader.Kind.Box; }
                public Loader Create(Loaders loaders, Debugger debugger, string name, string type, string id)
                {
                    return id == "boost::polygon::rectangle_data"
                         ? new BPBox()
                         : null;
                }
            }

            public override Geometry.Traits GetTraits(MemoryReader mreader, Debugger debugger,
                                                      string name)
            {
                return new Geometry.Traits(2, Geometry.CoordinateSystem.Cartesian, Geometry.Unit.None);
            }

            public override ExpressionDrawer.IDrawable Load(MemoryReader mreader, Debugger debugger,
                                                            string name, string type,
                                                            LoadCallback callback) // dummy callback
            {
                if (debugger.TryLoadDouble(name + ".ranges_[0].coords_[0]", out double xl)
                 && debugger.TryLoadDouble(name + ".ranges_[0].coords_[1]", out double xh)
                 && debugger.TryLoadDouble(name + ".ranges_[1].coords_[0]", out double yl)
                 && debugger.TryLoadDouble(name + ".ranges_[1].coords_[1]", out double yh))
                {
                    Geometry.Point first_p = new Geometry.Point(xl, yl);
                    Geometry.Point second_p = new Geometry.Point(xh, yh);

                    return new ExpressionDrawer.Box(first_p, second_p);
                }
                return null;
            }
        }

        class BPRing : PointRange<ExpressionDrawer.Ring>
        {
            public class LoaderCreator : ExpressionLoader.ILoaderCreator
            {
                public bool IsUserDefined() { return false; }
                public Kind Kind() { return ExpressionLoader.Kind.Ring; }
                public Loader Create(Loaders loaders, Debugger debugger, string name, string type, string id)
                {
                    if (id != "boost::polygon::polygon_data")
                        return null;

                    List<string> tparams = Util.Tparams(type);
                    if (tparams.Count < 1)
                        return null;

                    string pointType = Util.TemplateType("boost::polygon::point_data", tparams[0]);
                    string containerType = StdContainerType("std::vector", pointType, "std::allocator");

                    ContainerLoader containerLoader = loaders.FindByType(ExpressionLoader.Kind.Container,
                                                                         name,
                                                                         containerType) as ContainerLoader;
                    if (containerLoader == null)
                        return null;

                    containerLoader.ElementInfo(name, containerType, out string pointName, out string _);

                    BPPoint pointLoader = loaders.FindByType(ExpressionLoader.Kind.Point,
                                                             pointName,
                                                             pointType) as BPPoint;
                    if (pointLoader == null)
                        return null;

                    return new BPRing(containerLoader, containerType, pointLoader, pointType);
                }
            }

            private BPRing(ContainerLoader containerLoader, string containerType,
                           BPPoint pointLoader, string pointType)
            {
                this.containerLoader = containerLoader;
                this.containerType = containerType;
                this.pointLoader = pointLoader;
                this.pointType = pointType;
            }

            public override Geometry.Traits GetTraits(MemoryReader mreader, Debugger debugger,
                                                      string name)
            {
                return new Geometry.Traits(2, Geometry.CoordinateSystem.Cartesian, Geometry.Unit.None);
            }

            public override ExpressionDrawer.IDrawable Load(MemoryReader mreader, Debugger debugger,
                                                            string name, string type,
                                                            LoadCallback callback)
            {
                ExpressionDrawer.Ring result = null;

                string containerName = name + ".coords_";

                if (mreader != null)
                {
                    containerLoader.ElementInfo(name, containerType, out string pointName, out _);

                    result = LoadMemory(mreader, debugger,
                                        containerName, type,
                                        pointName, pointType, pointLoader,
                                        containerLoader, callback);
                }

                if (result == null)
                {
                    result = LoadParsed(mreader, debugger, containerName, type,
                                        pointType, pointLoader, containerLoader,
                                        callback);
                }

                return result;
            }

            readonly ContainerLoader containerLoader;
            readonly string containerType;
            readonly BPPoint pointLoader;
            readonly string pointType;
        }

        class BPPolygon : PolygonLoader
        {
            public class LoaderCreator : ExpressionLoader.ILoaderCreator
            {
                public bool IsUserDefined() { return false; }
                public Kind Kind() { return ExpressionLoader.Kind.Polygon; }
                public Loader Create(Loaders loaders, Debugger debugger, string name, string type, string id)
                {
                    if (id != "boost::polygon::polygon_with_holes_data")
                        return null;

                    List<string> tparams = Util.Tparams(type);
                    if (tparams.Count < 1)
                        return null;

                    string polygonType = Util.TemplateType("boost::polygon::polygon_data", tparams[0]);
                    string containerType = Util.TemplateType("std::list",
                                                polygonType,
                                                Util.TemplateType("std::allocator",
                                                    polygonType));

                    string member_self_ = name + ".self_";
                    string member_holes_ = name + ".holes_";

                    BPRing outerLoader = loaders.FindByType(ExpressionLoader.Kind.Ring,
                                                            member_self_,
                                                            polygonType) as BPRing;
                    if (outerLoader == null)
                        return null;

                    ContainerLoader holesLoader = loaders.FindByType(ExpressionLoader.Kind.Container,
                                                                     member_holes_,
                                                                     containerType) as ContainerLoader;
                    if (holesLoader == null)
                        return null;

                    return new BPPolygon(outerLoader, polygonType, holesLoader);
                }
            }

            private BPPolygon(BPRing outerLoader, string polygonType,
                              ContainerLoader holesLoader)
            {
                this.outerLoader = outerLoader;
                this.polygonType = polygonType;
                this.holesLoader = holesLoader;
            }

            public override Geometry.Traits GetTraits(MemoryReader mreader, Debugger debugger,
                                                      string name)
            {
                return new Geometry.Traits(2, Geometry.CoordinateSystem.Cartesian, Geometry.Unit.None);
            }

            public override ExpressionDrawer.IDrawable Load(MemoryReader mreader, Debugger debugger,
                                                            string name, string type,
                                                            LoadCallback callback)
            {
                string member_self_ = name + ".self_";
                string member_holes_ = name + ".holes_";

                ExpressionDrawer.Ring outer = outerLoader.Load(mreader, debugger,
                                                               member_self_, polygonType,
                                                               callback) as ExpressionDrawer.Ring;
                if (outer == null)
                    return null;

                List<Geometry.Ring> holes = new List<Geometry.Ring>();
                bool ok = holesLoader.ForEachElement(debugger, member_holes_, delegate (string elName)
                {
                    ExpressionDrawer.Ring hole = outerLoader.Load(mreader, debugger,
                                                                  elName, polygonType,
                                                                  callback) as ExpressionDrawer.Ring;
                    if (hole == null)
                        return false;
                    holes.Add(hole);
                    return true;
                });

                return ok
                     ? new ExpressionDrawer.Polygon(outer, holes)
                     : null;
            }

            readonly BPRing outerLoader;
            readonly string polygonType;
            readonly ContainerLoader holesLoader;
        }

        class BVariant : DrawableLoader
        {
            public class LoaderCreator : ExpressionLoader.ILoaderCreator
            {
                public bool IsUserDefined() { return false; }
                public Kind Kind() { return ExpressionLoader.Kind.Variant; }
                public Loader Create(Loaders loaders, Debugger debugger, string name, string type, string id)
                {
                    if (id != "boost::variant")
                        return null;

                    List<string> tparams = Util.Tparams(type);

                    DrawableLoader[] drawableLoaders = new DrawableLoader[tparams.Count];
                    for (int i = 0; i < tparams.Count; ++i)
                    {
                        string storedType = tparams[i];
                        string storedName = "(*(" + storedType + "*)" + name + ".storage_.data_.buf)";

                        drawableLoaders[i] = loaders.FindByType(AllDrawables, storedName, storedType)
                                                    as DrawableLoader;
                    }

                    return new BVariant(tparams, drawableLoaders);
                }
            }

            private BVariant(List<string> tparams, DrawableLoader[] loaders)
            {
                this.tparams = tparams;
                this.loaders = loaders;
            }

            public override Geometry.Traits GetTraits(MemoryReader mreader, Debugger debugger,
                                                      string name)
            {
                if (!debugger.TryLoadInt(name + ".which_", out int which))
                    return null;

                if (which < 0 || tparams.Count <= which || tparams.Count != loaders.Length)
                    return null;

                if (loaders[which] == null)
                    return null;

                return loaders[which].GetTraits(mreader, debugger, name);
            }

            public override ExpressionDrawer.IDrawable Load(MemoryReader mreader, Debugger debugger,
                                                            string name, string type,
                                                            LoadCallback callback)
            {
                if (!debugger.TryLoadInt(name + ".which_", out int which))
                    return null;

                if (which < 0 || tparams.Count <= which || tparams.Count != loaders.Length)
                    return null;

                if (loaders[which] == null)
                    return null;

                string storedType = tparams[which];
                string storedName = "(*(" + storedType + "*)" + name + ".storage_.data_.buf)";

                return loaders[which].Load(mreader, debugger, storedName, storedType,
                                           callback);
            }

            readonly List<string> tparams;
            readonly DrawableLoader[] loaders;
        }
    }
}
