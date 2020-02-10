//------------------------------------------------------------------------------
// <copyright file="ExpressionLoader_UserDefined.cs">
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

namespace GraphicalDebugging
{
    partial class ExpressionLoader
    {
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
                bool okx = ExpressionParser.TryLoadDouble(debugger, name + "." + member_x, out x);
                bool oky = ExpressionParser.TryLoadDouble(debugger, name + "." + member_y, out y);
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

                int firstSize = ExpressionParser.GetTypeSizeof(debugger, firstType);
                int secondSize = ExpressionParser.GetTypeSizeof(debugger, secondType);
                if (firstSize == 0 || secondSize == 0)
                    return null;

                MemoryReader.ValueConverter<double> firstConverter = mreader.GetNumericConverter(firstType, firstSize);
                MemoryReader.ValueConverter<double> secondConverter = mreader.GetNumericConverter(secondType, secondSize);
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

        class UserRange<ResultType> : PointRange<ResultType>
            where ResultType : class
                             , ExpressionDrawer.IDrawable
                             , Geometry.IContainer<Geometry.Point>
                             , new()
        {
            protected UserRange(ExpressionLoader.Kind kind, string id,
                                ClassScopeExpression exprContainer)
                : base(kind)
            {
                this.id = id;
                this.exprContainer = exprContainer;
            }

            public override bool IsUserDefined() { return true; }

            public override string Id() { return id; }

            public override void Initialize(Debugger debugger, string name)
            {
                string type = ExpressionParser.GetValueType(debugger, name);
                exprContainer.Initialize(debugger, name, type);
            }

            public override void Load(Loaders loaders, MemoryReader mreader, Debugger debugger,
                                      string name, string type,
                                      out Geometry.Traits traits,
                                      out ResultType result,
                                      LoadCallback callback)
            {
                traits = null;
                result = null;

                string containerName = exprContainer.GetString(name);
                string containerType = ExpressionParser.GetValueType(debugger, containerName);
                if (containerType == null)
                    return;

                ContainerLoader containerLoader = loaders.FindByType(ExpressionLoader.Kind.Container, containerName, containerType) as ContainerLoader;
                if (containerLoader == null)
                    return;

                string pointType = containerLoader.ElementType(containerType);
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
                                        pointType, pointLoader, containerLoader,
                                        callback);
                }

                if (result == null)
                {
                    result = LoadParsed(mreader, debugger, name, type,
                                        pointType, pointLoader, containerLoader,
                                        callback);
                }
            }

            string id;
            ClassScopeExpression exprContainer;
        }

        class UserLinestring : UserRange<ExpressionDrawer.Linestring>
        {
            public UserLinestring(string id, ClassScopeExpression exprContainer)
                : base(ExpressionLoader.Kind.Linestring, id, exprContainer)
            { }
        }

        class UserRing : UserRange<ExpressionDrawer.Ring>
        {
            public UserRing(string id, ClassScopeExpression exprContainer)
                : base(ExpressionLoader.Kind.Ring, id, exprContainer)
            { }
        }

        class UserMultiPoint : UserRange<ExpressionDrawer.MultiPoint>
        {
            public UserMultiPoint(string id, ClassScopeExpression exprContainer)
                : base(ExpressionLoader.Kind.MultiPoint, id, exprContainer)
            { }
        }

        class UserMultiLinestring : RangeLoader<ExpressionDrawer.MultiLinestring>
        {
            public UserMultiLinestring(string id, ClassScopeExpression exprContainer)
                : base(ExpressionLoader.Kind.MultiLinestring)
            {
                this.id = id;
                this.exprContainer = exprContainer;
            }

            public override bool IsUserDefined() { return true; }

            public override string Id() { return id; }

            public override void Initialize(Debugger debugger, string name)
            {
                string type = ExpressionParser.GetValueType(debugger, name);
                exprContainer.Initialize(debugger, name, type);
            }

            public override void Load(Loaders loaders, MemoryReader mreader, Debugger debugger,
                                      string name, string type,
                                      out Geometry.Traits traits,
                                      out ExpressionDrawer.MultiLinestring result,
                                      LoadCallback callback)
            {
                traits = null;
                result = null;

                string containerName = exprContainer.GetString(name);
                string containerType = ExpressionParser.GetValueType(debugger, containerName);
                if (containerType == null)
                    return;

                ContainerLoader containerLoader = loaders.FindByType(ExpressionLoader.Kind.Container,
                                                                     name,
                                                                     containerType) as ContainerLoader;
                if (containerLoader == null)
                    return;

                string lsType = containerLoader.ElementType(containerType);
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
                    lsLoader.Load(loaders, mreader, debugger, elName, lsType, out t, out ls, callback);
                    if (ls == null)
                        return false;
                    mls.Add(ls);
                    //return callback();
                    return true;
                });
                if (ok)
                {
                    traits = t;
                    result = mls;
                }
            }

            string id;
            ClassScopeExpression exprContainer;
        }

        class UserPolygon : PolygonLoader
        {
            public UserPolygon(string id,
                               ClassScopeExpression exprOuter,
                               ClassScopeExpression exprInnerContainer,
                               int innersOffset)
            {
                this.id = id;
                this.exprOuter = exprOuter;
                this.exprInnerContainer = exprInnerContainer;
                this.innersOffset = innersOffset;
            }

            public override bool IsUserDefined() { return true; }

            public override string Id() { return id; }

            public override void Initialize(Debugger debugger, string name)
            {
                string type = ExpressionParser.GetValueType(debugger, name);
                exprOuter.Initialize(debugger, name, type);
                exprInnerContainer.Initialize(debugger, name, type);
            }

            public override void Load(Loaders loaders, MemoryReader mreader, Debugger debugger,
                                      string name, string type,
                                      out Geometry.Traits traits,
                                      out ExpressionDrawer.Polygon result,
                                      LoadCallback callback)
            {
                traits = null;
                result = null;

                string outerName = exprOuter.GetString(name);
                string outerType = ExpressionParser.GetValueType(debugger, outerName);
                LoaderR<ExpressionDrawer.Ring> outerLoader = loaders.FindByType(ExpressionLoader.Kind.Ring,
                                                                                outerName, outerType) as LoaderR<ExpressionDrawer.Ring>;
                if (outerLoader == null)
                    return;

                string innersName = exprInnerContainer.GetString(name);
                string innersType = ExpressionParser.GetValueType(debugger, innersName);
                ContainerLoader innersLoader = loaders.FindByType(ExpressionLoader.Kind.Container,
                                                                  innersName, innersType) as ContainerLoader;
                if (innersLoader == null)
                    return;

                string innerType = innersLoader.ElementType(innersType);
                LoaderR<ExpressionDrawer.Ring> innerLoader = outerLoader;
                if (innerType != outerType)
                {
                    string innerName = innersLoader.ElementName(innersName, innerType);
                    innerLoader = loaders.FindByType(ExpressionLoader.Kind.Ring,
                                                     innerName, innerType) as LoaderR<ExpressionDrawer.Ring>;
                    if (innerLoader == null)
                        return;
                }

                ExpressionDrawer.Ring outer = null;
                outerLoader.Load(loaders, mreader, debugger, outerName, outerType,
                                 out traits, out outer,
                                 callback);
                if (outer == null)
                    return;

                int i = 0;
                Geometry.Traits t = null;
                List<Geometry.Ring> inners = new List<Geometry.Ring>();
                bool ok = innersLoader.ForEachElement(debugger, innersName, delegate (string elName)
                {
                    if (i++ < innersOffset)
                        return true;

                    ExpressionDrawer.Ring inner = null;
                    innerLoader.Load(loaders, mreader, debugger, elName, innerType,
                                     out t, out inner,
                                     callback);
                    if (inner == null)
                        return false;
                    inners.Add(inner);
                    //return callback();
                    return true;
                });
                if (ok)
                {
                    result = new ExpressionDrawer.Polygon(outer, inners);
                    if (traits == null) // assign only if it was not set for outer ring
                        traits = t;
                }
            }

            string id;
            ClassScopeExpression exprOuter;
            ClassScopeExpression exprInnerContainer;
            int innersOffset;
        }

        class UserMultiPolygon : RangeLoader<ExpressionDrawer.MultiPolygon>
        {
            public UserMultiPolygon(string id, ClassScopeExpression exprContainer)
                : base(ExpressionLoader.Kind.MultiPolygon)
            {
                this.id = id;
                this.exprContainer = exprContainer;
            }

            public override bool IsUserDefined() { return true; }

            public override string Id() { return id; }

            public override void Initialize(Debugger debugger, string name)
            {
                string type = ExpressionParser.GetValueType(debugger, name);
                exprContainer.Initialize(debugger, name, type);
            }

            public override void Load(Loaders loaders, MemoryReader mreader, Debugger debugger,
                                      string name, string type,
                                      out Geometry.Traits traits,
                                      out ExpressionDrawer.MultiPolygon result,
                                      LoadCallback callback)
            {
                traits = null;
                result = null;

                string containerName = exprContainer.GetString(name);
                string containerType = ExpressionParser.GetValueType(debugger, containerName);
                if (containerType == null)
                    return;

                ContainerLoader containerLoader = loaders.FindByType(ExpressionLoader.Kind.Container,
                                                                     containerName,
                                                                     containerType) as ContainerLoader;
                if (containerLoader == null)
                    return;

                string polyType = containerLoader.ElementType(containerType);
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
                    polyLoader.Load(loaders, mreader, debugger, elName, polyType,
                                    out t, out poly,
                                    callback);
                    if (poly == null)
                        return false;
                    mpoly.Add(poly);
                    //return callback();
                    return true;
                });
                if (ok)
                {
                    traits = t;
                    result = mpoly;
                }
            }

            string id;
            ClassScopeExpression exprContainer;
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
                    foreach (System.Xml.XmlElement elDrawable in elRoot.ChildNodes)
                    {
                        string id = elDrawable.GetAttribute("Id");

                        if (elDrawable.Name == "Point")
                        {
                            var elCoords = Util.GetXmlElementByTagName(elDrawable, "Coordinates");
                            if (elCoords != null)
                            {
                                var elX = Util.GetXmlElementByTagName(elCoords, "X");
                                var elY = Util.GetXmlElementByTagName(elCoords, "Y");
                                if (elX != null && elY != null)
                                {
                                    string x = elX.InnerText;
                                    string y = elY.InnerText;
                                    //string name = node.GetAttribute("Type");
                                    loaders.Add(new UserPoint(id, x, y));
                                }
                            }
                        }
                        else if (elDrawable.Name == "Linestring"
                              || elDrawable.Name == "Ring"
                              || elDrawable.Name == "MultiPoint")
                        {
                            var elPoints = Util.GetXmlElementByTagName(elDrawable, "Points");
                            if (elPoints != null)
                            {
                                var elCont = Util.GetXmlElementByTagName(elPoints, "Container");
                                if (elCont != null)
                                {
                                    var elName = Util.GetXmlElementByTagName(elCont, "Name");
                                    if (elName != null)
                                    {
                                        ClassScopeExpression classExpr = new ClassScopeExpression(elName.InnerText);
                                        loaders.Add(
                                            elDrawable.Name == "Linestring" ?
                                                (Loader)new UserLinestring(id, classExpr) :
                                            elDrawable.Name == "Ring" ?
                                                (Loader)new UserRing(id, classExpr) :
                                                // elDrawable.Name == "MultiPoint"
                                                (Loader)new UserMultiPoint(id, classExpr)
                                            );
                                    }
                                }
                            }
                        }
                        else if (elDrawable.Name == "MultiLinestring")
                        {
                            var elPoints = Util.GetXmlElementByTagName(elDrawable, "Linestrings");
                            if (elPoints != null)
                            {
                                var elCont = Util.GetXmlElementByTagName(elPoints, "Container");
                                if (elCont != null)
                                {
                                    var elName = Util.GetXmlElementByTagName(elCont, "Name");
                                    if (elName != null)
                                    {
                                        ClassScopeExpression classExpr = new ClassScopeExpression(elName.InnerText);
                                        loaders.Add(new UserMultiLinestring(id, classExpr));
                                    }
                                }
                            }
                        }
                        else if (elDrawable.Name == "Polygon")
                        {
                            var elOuter = Util.GetXmlElementByTagName(elDrawable, "ExteriorRing");
                            var elInners = Util.GetXmlElementByTagName(elDrawable, "InteriorRings");
                            if (elOuter != null && elInners != null)
                            {
                                var elOuterName = Util.GetXmlElementByTagName(elOuter, "Name");
                                var elCont = Util.GetXmlElementByTagName(elInners, "Container");
                                if (elOuterName != null && elCont != null)
                                {
                                    var elInnersName = Util.GetXmlElementByTagName(elCont, "Name");
                                    if (elInnersName != null)
                                    {
                                        var elInnersOffset = Util.GetXmlElementByTagName(elCont, "Offset");
                                        int innersOffset = 0;
                                        if (elInnersOffset != null)
                                            Util.TryParseInt(elInnersOffset.InnerText, out innersOffset);
                                        ClassScopeExpression classExprOuter = new ClassScopeExpression(elOuterName.InnerText);
                                        ClassScopeExpression classExprInners = new ClassScopeExpression(elInnersName.InnerText);
                                        loaders.Add(new UserPolygon(id, classExprOuter, classExprInners, innersOffset));
                                    }
                                }
                            }
                        }
                        else if (elDrawable.Name == "MultiPolygon")
                        {
                            var elPolygons = Util.GetXmlElementByTagName(elDrawable, "Polygons");
                            if (elPolygons != null)
                            {
                                var elCont = Util.GetXmlElementByTagName(elPolygons, "Container");
                                if (elCont != null)
                                {
                                    var elName = Util.GetXmlElementByTagName(elCont, "Name");
                                    if (elName != null)
                                    {
                                        ClassScopeExpression classExpr = new ClassScopeExpression(elName.InnerText);
                                        loaders.Add(new UserMultiPolygon(id, classExpr));
                                    }
                                }
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
