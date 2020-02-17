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
            public UserPoint(string id,
                             ClassScopeExpression exprX,
                             ClassScopeExpression exprY)
            {
                this.id = id;
                this.exprX = exprX;
                this.exprY = exprY;
                this.typeX = null;
                this.typeY = null;
                this.sizeOf = 0;
            }

            public override bool IsUserDefined() { return true; }

            public override string Id() { return id; }

            public override void Initialize(Debugger debugger, string name)
            {
                string type = ExpressionParser.GetValueType(debugger, name);
                exprX.Initialize(debugger, name, type);
                exprY.Initialize(debugger, name, type);

                sizeOf = ExpressionParser.GetTypeSizeof(debugger, type);
                if (ExpressionParser.IsInvalidSize(sizeOf))
                {
                    sizeOf = 0;
                    return;
                }

                string nameX = exprX.GetString(name);
                string nameY = exprY.GetString(name);
                typeX = ExpressionParser.GetValueType(debugger, nameX);
                typeY = ExpressionParser.GetValueType(debugger, nameY);
                if (ExpressionParser.IsInvalidType(typeX, typeY))
                {
                    sizeOf = 0;
                    return;
                }

                sizeX = ExpressionParser.GetTypeSizeof(debugger, typeX);
                sizeY = ExpressionParser.GetTypeSizeof(debugger, typeY);
                if (ExpressionParser.IsInvalidSize(sizeX, sizeY))
                {
                    sizeOf = 0;
                    return;
                }

                offsetX = ExpressionParser.GetAddressDifference(debugger, name, nameX);
                offsetY = ExpressionParser.GetAddressDifference(debugger, name, nameY);
                if (ExpressionParser.IsInvalidOffset(sizeOf, offsetX, offsetY))
                    // offsetX + sizeX > sizeOf
                    // offsetY + sizeY > sizeOf
                {
                    sizeOf = 0;
                    return;
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
                bool okx = ExpressionParser.TryLoadDouble(debugger, exprX.GetString(name), out x);
                bool oky = ExpressionParser.TryLoadDouble(debugger, exprY.GetString(name), out y);
                return Util.IsOk(okx, oky)
                     ? new ExpressionDrawer.Point(x, y)
                     : null;
            }

            protected override ExpressionDrawer.Point LoadPointMemory(MemoryReader mreader, Debugger debugger,
                                                                      string name, string type)
            {
                MemoryReader.Converter<double> converter = GetMemoryConverter(mreader, debugger, name, type);
                if (converter == null)
                    return null;

                if (converter.ValueCount() != 2)
                    throw new ArgumentOutOfRangeException("converter.ValueCount()");

                ulong address = ExpressionParser.GetValueAddress(debugger, name);
                if (address == 0)
                    return null;

                double[] values = new double[2];
                if (!mreader.Read(address, values, converter))
                    return null;

                return new ExpressionDrawer.Point(values[0], values[1]);
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
                if (sizeOf == 0)
                    return null;

                MemoryReader.ValueConverter<double> converterX = mreader.GetNumericConverter(typeX, sizeX);
                MemoryReader.ValueConverter<double> converterY = mreader.GetNumericConverter(typeY, sizeY);
                if (converterX == null || converterY == null)
                    return null;

                return new MemoryReader.StructConverter<double>(
                            sizeOf,
                            new MemoryReader.Member<double>(converterX, (int)offsetX),
                            new MemoryReader.Member<double>(converterY, (int)offsetY));
            }

            string id;
            ClassScopeExpression exprX;
            ClassScopeExpression exprY;
            // For memory loading:
            string typeX;
            string typeY;
            long offsetX;
            long offsetY;
            int sizeOf;
            int sizeX;
            int sizeY;
        }

        class UserContainerLoaders<ElementLoaderT>
        {
            public ContainerLoader ContainerLoader;
            public ElementLoaderT ElementLoader;
            public string ContainerName;
            public string ContainerType;
            public string ElementType;
        }

        interface IUserContainerEntry
        {
            void Initialize(Debugger debugger, string name);
            UserContainerLoaders<ElementLoader> GetLoaders<ElementLoader>(Loaders loaders, Kind elementKind,
                                                                          Debugger debugger, string name)
                where ElementLoader : Loader;
        }

        class UserEmptyEntry : IUserContainerEntry
        {
            public void Initialize(Debugger debugger, string name) { }
            public UserContainerLoaders<ElementLoader> GetLoaders<ElementLoader>(Loaders loaders, Kind elementKind,
                                                                                 Debugger debugger, string name)
                where ElementLoader : Loader
            {
                return null;
            }
        }

        class UserContainerEntry : IUserContainerEntry
        {
            public UserContainerEntry(string strContainerName)
            {
                this.exprContainerName = new ClassScopeExpression(strContainerName);
            }

            public void Initialize(Debugger debugger, string name)
            {
                string type = ExpressionParser.GetValueType(debugger, name);
                exprContainerName.Initialize(debugger, name, type);
            }

            public UserContainerLoaders<ElementLoader> GetLoaders<ElementLoader>(Loaders loaders, Kind elementKind,
                                                                                 Debugger debugger, string name)
                where ElementLoader : Loader
            {
                string containerName = exprContainerName.GetString(name);
                string containerType = ExpressionParser.GetValueType(debugger, containerName);
                if (containerType == null)
                    return null;
                ContainerLoader containerLoader = loaders.FindByType(ExpressionLoader.Kind.Container, containerName, containerType) as ContainerLoader;
                if (containerLoader == null)
                    return null;

                string elementType = containerLoader.ElementType(containerType);
                ElementLoader elementLoader = loaders.FindByType(elementKind,
                                                                 containerLoader.ElementName(containerName, elementType),
                                                                 elementType) as ElementLoader;
                if (elementLoader == null)
                    return null;

                UserContainerLoaders<ElementLoader> result = new UserContainerLoaders<ElementLoader>();
                result.ContainerLoader = containerLoader;
                result.ElementLoader = elementLoader;
                result.ContainerName = containerName;
                result.ContainerType = containerType;
                result.ElementType = elementType;
                return result;
            }

            ClassScopeExpression exprContainerName;
        }

        class UserArrayEntry : IUserContainerEntry
        {
            public UserArrayEntry(string strPointer, string strSize)
            {
                this.exprPointer = new ClassScopeExpression(strPointer);
                this.exprSize = new ClassScopeExpression(strSize);
            }

            public void Initialize(Debugger debugger, string name)
            {
                string type = ExpressionParser.GetValueType(debugger, name);
                exprPointer.Initialize(debugger, name, type);
                exprSize.Initialize(debugger, name, type);
            }

            public UserContainerLoaders<ElementLoader> GetLoaders<ElementLoader>(Loaders loaders, Kind elementKind,
                                                                                 Debugger debugger, string name)
                where ElementLoader : Loader
            {
                // TODO: If the pointer is an array, then strip the array from its size
                //       This may not be needed because below elementType is retrieved
                //       by dereferencing the pointer/array.

                string pointerName = exprPointer.GetString(name);
                string elementType = ExpressionParser.GetValueType(debugger, '*' + pointerName);
                if (elementType == null)
                    return null;

                string sizeName = exprSize.GetString(name);
                int size = ExpressionParser.LoadSize(debugger, sizeName);
                if (size <= 0)
                    return null;

                // TODO: This has sense only in C++

                string arrName = pointerName + ',' + size;
                string arrType = elementType + '[' + size + ']';

                ContainerLoader containerLoader = loaders.FindByType(ExpressionLoader.Kind.Container, arrName, arrType) as ContainerLoader;
                if (containerLoader == null)
                    return null;

                ElementLoader elementLoader = loaders.FindByType(elementKind,
                                                                 containerLoader.ElementName(arrName, elementType),
                                                                 elementType) as ElementLoader;
                if (elementLoader == null)
                    return null;

                UserContainerLoaders<ElementLoader> result = new UserContainerLoaders<ElementLoader>();
                result.ContainerLoader = containerLoader;
                result.ElementLoader = elementLoader;
                result.ContainerName = arrName;
                result.ContainerType = arrType;
                result.ElementType = elementType;
                return result;
            }

            ClassScopeExpression exprPointer;
            ClassScopeExpression exprSize;
        }

        class UserRange<ResultType> : PointRange<ResultType>
            where ResultType : class
                             , ExpressionDrawer.IDrawable
                             , Geometry.IContainer<Geometry.Point>
                             , new()
        {
            protected UserRange(ExpressionLoader.Kind kind, string id,
                                IUserContainerEntry containerEntry)
                : base(kind)
            {
                this.id = id;
                this.containerEntry = containerEntry;
            }

            public override bool IsUserDefined() { return true; }

            public override string Id() { return id; }

            public override void Initialize(Debugger debugger, string name)
            {
                containerEntry.Initialize(debugger, name);
            }

            public override void Load(Loaders loaders, MemoryReader mreader, Debugger debugger,
                                      string name, string type,
                                      out Geometry.Traits traits,
                                      out ResultType result,
                                      LoadCallback callback)
            {
                traits = null;
                result = null;

                UserContainerLoaders<PointLoader> containerLoaders
                    = containerEntry.GetLoaders<PointLoader>(loaders, ExpressionLoader.Kind.Point,
                                                             debugger, name);
                if (containerLoaders == null)
                    return;

                traits = containerLoaders.ElementLoader.LoadTraits(containerLoaders.ElementType);
                if (traits == null)
                    return;

                if (mreader != null)
                {
                    result = LoadMemory(loaders, mreader, debugger,
                                        containerLoaders.ContainerName,
                                        containerLoaders.ContainerType,
                                        containerLoaders.ElementType,
                                        containerLoaders.ElementLoader,
                                        containerLoaders.ContainerLoader,
                                        callback);
                }

                if (result == null)
                {
                    result = LoadParsed(mreader, debugger,
                                        containerLoaders.ContainerName,
                                        containerLoaders.ContainerType,
                                        containerLoaders.ElementType,
                                        containerLoaders.ElementLoader,
                                        containerLoaders.ContainerLoader,
                                        callback);
                }
            }

            string id;
            IUserContainerEntry containerEntry;
        }

        class UserLinestring : UserRange<ExpressionDrawer.Linestring>
        {
            public UserLinestring(string id, IUserContainerEntry containerEntry)
                : base(ExpressionLoader.Kind.Linestring, id, containerEntry)
            { }
        }

        class UserRing : UserRange<ExpressionDrawer.Ring>
        {
            public UserRing(string id, IUserContainerEntry containerEntry)
                : base(ExpressionLoader.Kind.Ring, id, containerEntry)
            { }
        }

        class UserMultiPoint : UserRange<ExpressionDrawer.MultiPoint>
        {
            public UserMultiPoint(string id, IUserContainerEntry containerEntry)
                : base(ExpressionLoader.Kind.MultiPoint, id, containerEntry)
            { }
        }

        class UserMultiLinestring : RangeLoader<ExpressionDrawer.MultiLinestring>
        {
            public UserMultiLinestring(string id, IUserContainerEntry containerEntry)
                : base(ExpressionLoader.Kind.MultiLinestring)
            {
                this.id = id;
                this.containerEntry = containerEntry;
            }

            public override bool IsUserDefined() { return true; }

            public override string Id() { return id; }

            public override void Initialize(Debugger debugger, string name)
            {
                containerEntry.Initialize(debugger, name);
            }

            public override void Load(Loaders loaders, MemoryReader mreader, Debugger debugger,
                                      string name, string type,
                                      out Geometry.Traits traits,
                                      out ExpressionDrawer.MultiLinestring result,
                                      LoadCallback callback)
            {
                traits = null;
                result = null;

                UserContainerLoaders<RangeLoader<ExpressionDrawer.Linestring>> containerLoaders
                    = containerEntry.GetLoaders<RangeLoader<ExpressionDrawer.Linestring>>(
                        loaders, ExpressionLoader.Kind.Linestring, debugger, name);
                if (containerLoaders == null)
                    return;

                Geometry.Traits t = null;
                ExpressionDrawer.MultiLinestring mls = new ExpressionDrawer.MultiLinestring();
                bool ok = containerLoaders.ContainerLoader.ForEachElement(
                    debugger, containerLoaders.ContainerName,
                    delegate (string elName)
                    {
                        ExpressionDrawer.Linestring ls = null;
                        containerLoaders.ElementLoader.Load(loaders, mreader,
                                                            debugger, elName,
                                                            containerLoaders.ElementType,
                                                            out t, out ls, callback);
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
            IUserContainerEntry containerEntry;
        }

        class UserPolygon : PolygonLoader
        {
            public UserPolygon(string id,
                               ClassScopeExpression exprOuter,
                               IUserContainerEntry innersContEntry,
                               int innersOffset)
            {
                this.id = id;
                this.exprOuter = exprOuter;
                this.innersContEntry = innersContEntry;
                this.innersOffset = innersOffset;
            }

            public override bool IsUserDefined() { return true; }

            public override string Id() { return id; }

            public override void Initialize(Debugger debugger, string name)
            {
                string type = ExpressionParser.GetValueType(debugger, name);
                exprOuter.Initialize(debugger, name, type);
                // TODO: GetValueType() called internally second time
                innersContEntry.Initialize(debugger, name);
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

                ExpressionDrawer.Ring outer = null;
                outerLoader.Load(loaders, mreader, debugger, outerName, outerType,
                                 out traits, out outer,
                                 callback);
                if (outer == null)
                    return;

                UserContainerLoaders<LoaderR<ExpressionDrawer.Ring>> innersLoaders
                    = innersContEntry.GetLoaders<LoaderR<ExpressionDrawer.Ring>>(loaders, ExpressionLoader.Kind.Ring,
                                                                                 debugger, name);
                // If there is no definition of inner rings, return
                if (innersLoaders == null)
                {
                    result = new ExpressionDrawer.Polygon(outer, new List<Geometry.Ring>());
                    return;
                }

                // However if inner rings are defined then load them and return
                // them only if they are properly loaded.
                int i = 0;
                Geometry.Traits t = null;
                List<Geometry.Ring> inners = new List<Geometry.Ring>();
                bool ok = innersLoaders.ContainerLoader.ForEachElement(
                    debugger, innersLoaders.ContainerName,
                    delegate (string elName)
                    {
                        if (i++ < innersOffset)
                            return true;

                        ExpressionDrawer.Ring inner = null;
                        innersLoaders.ElementLoader.Load(loaders, mreader, debugger,
                                                            elName, innersLoaders.ElementType,
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
            IUserContainerEntry innersContEntry;
            int innersOffset;
        }

        class UserMultiPolygon : RangeLoader<ExpressionDrawer.MultiPolygon>
        {
            public UserMultiPolygon(string id, IUserContainerEntry containerEntry)
                : base(ExpressionLoader.Kind.MultiPolygon)
            {
                this.id = id;
                this.containerEntry = containerEntry;
            }

            public override bool IsUserDefined() { return true; }

            public override string Id() { return id; }

            public override void Initialize(Debugger debugger, string name)
            {
                containerEntry.Initialize(debugger, name);
            }

            public override void Load(Loaders loaders, MemoryReader mreader, Debugger debugger,
                                      string name, string type,
                                      out Geometry.Traits traits,
                                      out ExpressionDrawer.MultiPolygon result,
                                      LoadCallback callback)
            {
                traits = null;
                result = null;

                UserContainerLoaders<PolygonLoader> containerLoaders
                    = containerEntry.GetLoaders<PolygonLoader>(loaders, ExpressionLoader.Kind.Polygon,
                                                               debugger, name);
                if (containerLoaders == null)
                    return;

                Geometry.Traits t = null;
                ExpressionDrawer.MultiPolygon mpoly = new ExpressionDrawer.MultiPolygon();
                bool ok = containerLoaders.ContainerLoader.ForEachElement(
                    debugger, containerLoaders.ContainerName,
                    delegate (string elName)
                    {
                        ExpressionDrawer.Polygon poly = null;
                        containerLoaders.ElementLoader.Load(loaders, mreader,
                                                            debugger, elName,
                                                            containerLoaders.ElementType,
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
            IUserContainerEntry containerEntry;
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
                foreach (System.Xml.XmlNode elRoot in doc.GetElementsByTagName("GraphicalDebugging"))
                {
                    foreach (System.Xml.XmlNode el in elRoot.ChildNodes)
                    {
                        if (!(el is System.Xml.XmlElement))
                            continue;
                        System.Xml.XmlElement elDrawable = el as System.Xml.XmlElement;

                        string id = elDrawable.GetAttribute("Id");
                        if (id == null || id == "")
                            continue;

                        if (elDrawable.Name == "Point")
                        {
                            var elCoords = Util.GetXmlElementByTagName(elDrawable, "Coordinates");
                            if (elCoords != null)
                            {
                                var elX = Util.GetXmlElementByTagName(elCoords, "X");
                                var elY = Util.GetXmlElementByTagName(elCoords, "Y");
                                if (elX != null && elY != null)
                                {
                                    ClassScopeExpression exprX = new ClassScopeExpression(elX.InnerText);
                                    ClassScopeExpression exprY = new ClassScopeExpression(elY.InnerText);
                                    loaders.Add(new UserPoint(id, exprX, exprY));
                                }
                            }
                        }
                        else if (elDrawable.Name == "Linestring")
                        {
                            IUserContainerEntry contEntry = GetContainerEntry(elDrawable, "Points");
                            if (contEntry != null)
                                loaders.Add(new UserLinestring(id, contEntry));
                        }
                        else if (elDrawable.Name == "Ring")
                        {
                            IUserContainerEntry contEntry = GetContainerEntry(elDrawable, "Points");
                            if (contEntry != null)
                                loaders.Add(new UserRing(id, contEntry));
                        }
                        else if (elDrawable.Name == "MultiPoint")
                        {
                            IUserContainerEntry contEntry = GetContainerEntry(elDrawable, "Points");
                            if (contEntry != null)
                                loaders.Add(new UserMultiPoint(id, contEntry));
                        }
                        else if (elDrawable.Name == "MultiLinestring")
                        {
                            IUserContainerEntry contEntry = GetContainerEntry(elDrawable, "Linestrings");
                            if (contEntry != null)
                                loaders.Add(new UserMultiLinestring(id, contEntry));
                        }
                        else if (elDrawable.Name == "Polygon")
                        {
                            var elOuterName = Util.GetXmlElementByTagNames(elDrawable, "ExteriorRing", "Name");
                            if (elOuterName != null)
                            {
                                ClassScopeExpression classExprOuter = new ClassScopeExpression(elOuterName.InnerText);
                                IUserContainerEntry innersContEntry = GetContainerEntry(elDrawable, "InteriorRings");
                                if (innersContEntry == null)
                                    innersContEntry = new UserEmptyEntry();
                                // TODO: InteriorRings searched the second time
                                var elInners = Util.GetXmlElementByTagName(elDrawable, "InteriorRings");
                                var elInnersOffset = Util.GetXmlElementByTagName(elInners, "Offset");

                                int innersOffset = 0;
                                if (elInnersOffset != null)
                                    Util.TryParseInt(elInnersOffset.InnerText, out innersOffset);

                                loaders.Add(new UserPolygon(id, classExprOuter, innersContEntry, innersOffset));
                            }
                        }
                        else if (elDrawable.Name == "MultiPolygon")
                        {
                            IUserContainerEntry contEntry = GetContainerEntry(elDrawable, "Polygons");
                            if (contEntry != null)
                                loaders.Add(new UserMultiPolygon(id, contEntry));
                        }
                    }
                }
            }

            return update;
        }

        static IUserContainerEntry GetContainerEntry(System.Xml.XmlElement elDrawable,
                                                     string elementsKind)
        {
            var elElements = Util.GetXmlElementByTagName(elDrawable, elementsKind);
            if (elElements != null)
            {
                var elContName = Util.GetXmlElementByTagNames(elElements, "Container", "Name");
                if (elContName != null)
                {
                    return new UserContainerEntry(elContName.InnerText);
                }
                else
                {
                    var elArray = Util.GetXmlElementByTagName(elElements, "Array");
                    if (elArray != null)
                    {
                        var elPointer = Util.GetXmlElementByTagName(elArray, "Pointer");
                        var elSize = Util.GetXmlElementByTagName(elArray, "Size");
                        if (elPointer != null && elSize != null)
                        {
                            return new UserArrayEntry(elPointer.InnerText, elSize.InnerText);
                        }
                    }
                }
            }
            return null;
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
