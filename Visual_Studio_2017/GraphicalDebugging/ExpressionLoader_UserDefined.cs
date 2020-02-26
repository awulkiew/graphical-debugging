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
        // TODO: Storing elemType is not correct because the same loader
        //   can potentially be used for two different container types storing
        //   different elements.
        //   This is not a problem now because before that happens Initialize()
        //   is called each time the loader is retrieved from the list of loaders.
        //   This however should probably be changed because it's not efficient to
        //   initialize loaders and call the debugger multiple times for the same type.

        // TODO: The code is similar to std::vector loader, unify if possible
        class UserArray : ContiguousContainer
        {
            public UserArray(ITypeMatcher typeMatcher,
                             ClassScopeExpression exprPointer,
                             ClassScopeExpression exprSize)
            {
                this.typeMatcher = typeMatcher;
                this.exprPointer = exprPointer;
                this.exprSize = exprSize;
            }

            public override void Initialize(Debugger debugger, string name)
            {
                string type = ExpressionParser.GetValueType(debugger, name);
                exprPointer.Initialize(debugger, name, type);
                exprSize.Initialize(debugger, name, type);

                string elemName = exprPointer.GetString(name) + "[0]";
                elemType = ExpressionParser.GetValueType(debugger, elemName);
            }

            public override bool IsUserDefined() { return true; }

            public override string Id() { return null; }

            public override bool MatchType(Loaders loaders, string name, string type, string id)
            {
                return typeMatcher.MatchType(type, id);
            }

            public override string ElementType(string type)
            {
                return elemType == null ? "" : elemType;
            }

            public override string RandomAccessElementName(string rawName, int i)
            {
                return exprPointer.GetString(rawName) + "[" + i + "]";
            }

            public override string ElementName(string name, string elType)
            {
                return exprPointer.GetString(name) + "[0]";
            }

            public override int LoadSize(Debugger debugger, string name)
            {
                return ExpressionParser.LoadSize(debugger, exprSize.GetString(name));
            }

            ITypeMatcher typeMatcher;
            ClassScopeExpression exprPointer;
            ClassScopeExpression exprSize;
            string elemType;
        }

        // TODO: Storing elemType is not correct because the same loader
        //   can potentially be used for two different container types storing
        //   different elements.
        //   This is not a problem now because before that happens Initialize()
        //   is called each time the loader is retrieved from the list of loaders.
        //   This however should probably be changed because it's not efficient to
        //   initialize loaders and call the debugger multiple times for the same type.

        // TODO: The code is similar to std::list loader, unify if possible
        class UserLinkedList : ContainerLoader
        {
            public UserLinkedList(ITypeMatcher typeMatcher,
                                  ClassScopeExpression exprHeadPointer,
                                  ClassScopeExpression exprNextPointer,
                                  ClassScopeExpression exprValue,
                                  ClassScopeExpression exprSize)
            {
                this.typeMatcher = typeMatcher;
                this.exprHeadPointer = exprHeadPointer;
                this.exprNextPointer = exprNextPointer;
                this.exprValue = exprValue;
                this.exprSize = exprSize;
            }

            public override void Initialize(Debugger debugger, string name)
            {
                string type = ExpressionParser.GetValueType(debugger, name);
                exprHeadPointer.Initialize(debugger, name, type);
                exprSize.Initialize(debugger, name, type);
                string headName = '*' + exprHeadPointer.GetString(name);
                string headType = ExpressionParser.GetValueType(debugger, headName);
                exprNextPointer.Initialize(debugger, headName, headType);
                exprValue.Initialize(debugger, headName, headType);

                string elemName = exprValue.GetString(headName);
                elemType = ExpressionParser.GetValueType(debugger, elemName);
            }

            public override bool IsUserDefined() { return true; }

            public override string Id() { return null; }

            public override bool MatchType(Loaders loaders, string name, string type, string id)
            {
                return typeMatcher.MatchType(type, id);
            }

            public override string ElementType(string type)
            {
                return elemType == null ? "" : elemType;
            }

            public override string ElementName(string name, string elType)
            {
                // TODO: Only C++ ?
                return exprValue.GetString('*' + exprHeadPointer.GetString(name));
            }

            public override int LoadSize(Debugger debugger, string name)
            {
                return ExpressionParser.LoadSize(debugger, exprSize.GetString(name));
            }

            public override bool ForEachMemoryBlock(MemoryReader mreader, Debugger debugger,
                                                    string name, string type,
                                                    MemoryReader.Converter<double> elementConverter,
                                                    MemoryBlockPredicate memoryBlockPredicate)
            {
                int size = LoadSize(debugger, name);
                if (size <= 0)
                    return true;

                string headPointerName = exprHeadPointer.GetString(name);
                string headName = '*' + headPointerName;
                string nextPointerName = exprNextPointer.GetString(headName);
                string valName = exprValue.GetString(headName);

                TypeInfo nextInfo = new TypeInfo(debugger, nextPointerName);
                if (!nextInfo.IsValid)
                    return false;

                MemoryReader.ValueConverter<ulong> nextConverter = mreader.GetPointerConverter(nextInfo.Type, nextInfo.Size);
                if (nextConverter == null)
                    return false;

                long nextDiff = ExpressionParser.GetAddressDifference(debugger, headName, nextPointerName);
                long valDiff = ExpressionParser.GetAddressDifference(debugger, headName, valName);
                if (ExpressionParser.IsInvalidAddressDifference(nextDiff)
                 || ExpressionParser.IsInvalidAddressDifference(valDiff)
                 || nextDiff < 0 || valDiff < 0)
                    return false;

                ulong address = ExpressionParser.GetValueAddress(debugger, headName);
                if (address == 0)
                    return false;

                for (int i = 0; i < size; ++i)
                {
                    double[] values = new double[elementConverter.ValueCount()];
                    if (!mreader.Read(address + (ulong)valDiff, values, elementConverter))
                        return false;

                    if (!memoryBlockPredicate(values))
                        return false;

                    ulong[] nextTmp = new ulong[1];
                    if (!mreader.Read(address + (ulong)nextDiff, nextTmp, nextConverter))
                        return false;
                    address = nextTmp[0];
                }
                return true;
            }

            public override bool ForEachElement(Debugger debugger, string name, ElementPredicate elementPredicate)
            {
                int size = this.LoadSize(debugger, name);

                string nodeName = '*' + exprHeadPointer.GetString(name);
                for (int i = 0; i < size; ++i)
                {
                    string elName = exprValue.GetString(nodeName);
                    if (!elementPredicate(elName))
                        return false;
                    nodeName = '*' + exprNextPointer.GetString(nodeName);
                }
                return true;
            }

            ITypeMatcher typeMatcher;
            ClassScopeExpression exprHeadPointer;
            ClassScopeExpression exprNextPointer;
            ClassScopeExpression exprValue;
            ClassScopeExpression exprSize;
            string elemType;
        }

        class UserPoint : PointLoader
        {
            public UserPoint(ITypeMatcher typeMatcher,
                             ClassScopeExpression exprX,
                             ClassScopeExpression exprY)
            {
                this.typeMatcher = typeMatcher;
                this.exprX = exprX;
                this.exprY = exprY;
                this.typeX = null;
                this.typeY = null;
                this.sizeOf = 0;
            }

            public override bool IsUserDefined() { return true; }

            public override string Id() { return null; }

            public override bool MatchType(Loaders loaders, string name, string type, string id)
            {
                return typeMatcher.MatchType(type, id);
            }

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

            ITypeMatcher typeMatcher;
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
            UserContainerLoaders<ElementLoader> GetLoaders<ElementLoader>(Loaders loaders,
                                                                          IKindConstraint elementKindConstraint,
                                                                          Debugger debugger, string name)
                where ElementLoader : Loader;
        }

        class UserEmptyEntry : IUserContainerEntry
        {
            public void Initialize(Debugger debugger, string name) { }
            public UserContainerLoaders<ElementLoader> GetLoaders<ElementLoader>(Loaders loaders,
                                                                                 IKindConstraint elementKindConstraint,
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

            public UserContainerLoaders<ElementLoader> GetLoaders<ElementLoader>(Loaders loaders,
                                                                                 IKindConstraint elementKindConstraint,
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
                ElementLoader elementLoader = loaders.FindByType(elementKindConstraint,
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

            public UserContainerLoaders<ElementLoader> GetLoaders<ElementLoader>(Loaders loaders,
                                                                                 IKindConstraint elementKindConstraint,
                                                                                 Debugger debugger, string name)
                where ElementLoader : Loader
            {
                // TODO: If the pointer is an array, then strip the array from its size
                //       This may not be needed because below elementType is retrieved
                //       by dereferencing the pointer/array.

                string type = ExpressionParser.GetValueType(debugger, name);
                if (type == null)
                    return null;

                // NOTE: This could be done in Initialize(),however in all other places
                //   container is created an initialized during Loading.
                //   So do this in this case as well.
                UserArray containerLoader = new UserArray(new TypeMatcher(type), exprPointer, exprSize);
                containerLoader.Initialize(debugger, name);

                string elementType = containerLoader.ElementType(type);
                string elementName = containerLoader.ElementName(name, elementType);
                ElementLoader elementLoader = loaders.FindByType(elementKindConstraint,
                                                                 elementName,
                                                                 elementType) as ElementLoader;
                if (elementLoader == null)
                    return null;

                UserContainerLoaders<ElementLoader> result = new UserContainerLoaders<ElementLoader>();
                result.ContainerLoader = containerLoader;
                result.ElementLoader = elementLoader;
                result.ContainerName = name;
                result.ContainerType = type;
                result.ElementType = elementType;
                return result;
            }

            ClassScopeExpression exprPointer;
            ClassScopeExpression exprSize;
        }

        class UserLinkedListEntry : IUserContainerEntry
        {
            public UserLinkedListEntry(string strHeadPointer,
                                       string strNextPointer,
                                       string strValue,
                                       string strSize)
            {
                this.exprHeadPointer = new ClassScopeExpression(strHeadPointer);
                this.exprNextPointer = new ClassScopeExpression(strNextPointer);
                this.exprValue = new ClassScopeExpression(strValue);
                this.exprSize = new ClassScopeExpression(strSize);
            }

            public void Initialize(Debugger debugger, string name)
            {
            }

            public UserContainerLoaders<ElementLoader> GetLoaders<ElementLoader>(Loaders loaders,
                                                                                 IKindConstraint elementKindConstraint,
                                                                                 Debugger debugger, string name)
                where ElementLoader : Loader
            {
                string type = ExpressionParser.GetValueType(debugger, name);
                if (type == null)
                    return null;

                // NOTE: This could be done in Initialize(),however in all other places
                //   container is created an initialized during Loading.
                //   So do this in this case as well.
                UserLinkedList containerLoader = new UserLinkedList(new TypeMatcher(type),
                                                                    exprHeadPointer, exprNextPointer,
                                                                    exprValue, exprSize);
                containerLoader.Initialize(debugger, name);

                string elementType = containerLoader.ElementType(type);
                string elementName = containerLoader.ElementName(name, elementType);
                ElementLoader elementLoader = loaders.FindByType(elementKindConstraint,
                                                                 elementName,
                                                                 elementType) as ElementLoader;
                if (elementLoader == null)
                    return null;

                UserContainerLoaders<ElementLoader> result = new UserContainerLoaders<ElementLoader>();
                result.ContainerLoader = containerLoader;
                result.ElementLoader = elementLoader;
                result.ContainerName = name;
                result.ContainerType = type;
                result.ElementType = elementType;
                return result;
            }

            ClassScopeExpression exprHeadPointer;
            ClassScopeExpression exprNextPointer;
            ClassScopeExpression exprValue;
            ClassScopeExpression exprSize;
        }

        class UserRange<ResultType> : PointRange<ResultType>
            where ResultType : class
                             , ExpressionDrawer.IDrawable
                             , Geometry.IContainer<Geometry.Point>
                             , new()
        {
            protected UserRange(ExpressionLoader.Kind kind,
                                ITypeMatcher typeMatcher,
                                IUserContainerEntry containerEntry)
                : base(kind)
            {
                this.typeMatcher = typeMatcher;
                this.containerEntry = containerEntry;
            }

            public override bool IsUserDefined() { return true; }

            public override string Id() { return null; }

            public override bool MatchType(Loaders loaders, string name, string type, string id)
            {
                return typeMatcher.MatchType(type, id);
            }

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
                    = containerEntry.GetLoaders<PointLoader>(
                            loaders,
                            new KindConstraint(ExpressionLoader.Kind.Point),
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

            ITypeMatcher typeMatcher;
            IUserContainerEntry containerEntry;
        }

        class UserLinestring : UserRange<ExpressionDrawer.Linestring>
        {
            public UserLinestring(ITypeMatcher typeMatcher, IUserContainerEntry containerEntry)
                : base(ExpressionLoader.Kind.Linestring, typeMatcher, containerEntry)
            { }
        }

        class UserRing : UserRange<ExpressionDrawer.Ring>
        {
            public UserRing(ITypeMatcher typeMatcher, IUserContainerEntry containerEntry)
                : base(ExpressionLoader.Kind.Ring, typeMatcher, containerEntry)
            { }
        }

        class UserMultiPoint : UserRange<ExpressionDrawer.MultiPoint>
        {
            public UserMultiPoint(ITypeMatcher typeMatcher, IUserContainerEntry containerEntry)
                : base(ExpressionLoader.Kind.MultiPoint, typeMatcher, containerEntry)
            { }
        }

        class UserMultiLinestring : RangeLoader<ExpressionDrawer.MultiLinestring>
        {
            public UserMultiLinestring(ITypeMatcher typeMatcher, IUserContainerEntry containerEntry)
                : base(ExpressionLoader.Kind.MultiLinestring)
            {
                this.typeMatcher = typeMatcher;
                this.containerEntry = containerEntry;
            }

            public override bool IsUserDefined() { return true; }

            public override string Id() { return null; }

            public override bool MatchType(Loaders loaders, string name, string type, string id)
            {
                return typeMatcher.MatchType(type, id);
            }

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
                            loaders,
                            new KindConstraint(ExpressionLoader.Kind.Linestring),
                            debugger, name);
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

            ITypeMatcher typeMatcher;
            IUserContainerEntry containerEntry;
        }

        class UserPolygon : PolygonLoader
        {
            public UserPolygon(ITypeMatcher typeMatcher,
                               ClassScopeExpression exprOuter,
                               IUserContainerEntry innersContEntry,
                               int innersOffset)
            {
                this.typeMatcher = typeMatcher;
                this.exprOuter = exprOuter;
                this.innersContEntry = innersContEntry;
                this.innersOffset = innersOffset;
            }

            public override bool IsUserDefined() { return true; }

            public override string Id() { return null; }

            public override bool MatchType(Loaders loaders, string name, string type, string id)
            {
                return typeMatcher.MatchType(type, id);
            }

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
                    = innersContEntry.GetLoaders<LoaderR<ExpressionDrawer.Ring>>(
                            loaders,
                            new KindConstraint(ExpressionLoader.Kind.Ring),
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

            ITypeMatcher typeMatcher;
            ClassScopeExpression exprOuter;
            IUserContainerEntry innersContEntry;
            int innersOffset;
        }

        class UserMultiPolygon : RangeLoader<ExpressionDrawer.MultiPolygon>
        {
            public UserMultiPolygon(ITypeMatcher typeMatcher, IUserContainerEntry containerEntry)
                : base(ExpressionLoader.Kind.MultiPolygon)
            {
                this.typeMatcher = typeMatcher;
                this.containerEntry = containerEntry;
            }

            public override bool IsUserDefined() { return true; }

            public override string Id() { return null; }

            public override bool MatchType(Loaders loaders, string name, string type, string id)
            {
                return typeMatcher.MatchType(type, id);
            }

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
                    = containerEntry.GetLoaders<PolygonLoader>(
                            loaders,
                            new KindConstraint(ExpressionLoader.Kind.Polygon),
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

            ITypeMatcher typeMatcher;
            IUserContainerEntry containerEntry;
        }

        // TODO: If possible use one implementation for MultiLinestring, MultiPolygon and MultiGeometry
        class UserMultiGeometry : RangeLoader<ExpressionDrawer.DrawablesContainer>
        {
            public UserMultiGeometry(ITypeMatcher typeMatcher, IUserContainerEntry containerEntry)
                : base(ExpressionLoader.Kind.MultiGeometry)
            {
                this.typeMatcher = typeMatcher;
                this.containerEntry = containerEntry;
            }

            public override bool IsUserDefined() { return true; }

            public override string Id() { return null; }

            public override bool MatchType(Loaders loaders, string name, string type, string id)
            {
                return typeMatcher.MatchType(type, id);
            }

            public override void Initialize(Debugger debugger, string name)
            {
                containerEntry.Initialize(debugger, name);
            }

            public override void Load(Loaders loaders, MemoryReader mreader, Debugger debugger,
                                      string name, string type,
                                      out Geometry.Traits traits,
                                      out ExpressionDrawer.DrawablesContainer result,
                                      LoadCallback callback)
            {
                traits = null;
                result = null;

                UserContainerLoaders<DrawableLoader> containerLoaders
                    = containerEntry.GetLoaders<DrawableLoader>(
                            loaders, OnlyGeometries, debugger, name);
                if (containerLoaders == null)
                    return;

                Geometry.Traits t = null;
                ExpressionDrawer.DrawablesContainer drawables = new ExpressionDrawer.DrawablesContainer();
                bool ok = containerLoaders.ContainerLoader.ForEachElement(
                    debugger, containerLoaders.ContainerName,
                    delegate (string elName)
                    {
                        ExpressionDrawer.IDrawable drawable = null;
                        containerLoaders.ElementLoader.Load(loaders, mreader,
                                                            debugger, elName,
                                                            containerLoaders.ElementType,
                                                            out t, out drawable,
                                                            callback);
                        if (drawable == null)
                            return false;
                        drawables.Add(drawable);
                        //return callback();
                        return true;
                    });
                if (ok)
                {
                    traits = t;
                    result = drawables;
                }
            }

            ITypeMatcher typeMatcher;
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
                        System.Xml.XmlElement elEntry = el as System.Xml.XmlElement;

                        ITypeMatcher typeMatcher = null;
                        {
                            string type = elEntry.GetAttribute("Type");
                            if (! Util.Empty(type))
                            {
                                typeMatcher = new TypePatternMatcher(type);
                            }
                            else
                            {
                                string id = elEntry.GetAttribute("Id");
                                if (! Util.Empty(id))
                                {
                                    typeMatcher = new IdMatcher(id);
                                }
                            }
                        }
                        if (typeMatcher == null)
                            continue;

                        if (elEntry.Name == "Container")
                        {
                            var elArray = Util.GetXmlElementByTagName(elEntry, "Array");
                            var elLinkedList = Util.GetXmlElementByTagName(elEntry, "LinkedList");
                            if (elArray != null)
                            {
                                var elPointer = Util.GetXmlElementByTagName(elArray, "Pointer");
                                var elSize = Util.GetXmlElementByTagName(elArray, "Size");
                                if (elPointer != null && elSize != null)
                                {
                                    loaders.Add(new UserArray(typeMatcher,
                                                    new ClassScopeExpression(elPointer.InnerText),
                                                    new ClassScopeExpression(elSize.InnerText)));
                                }
                            }
                            else if (elLinkedList != null)
                            {
                                var elHeadPointer = Util.GetXmlElementByTagName(elLinkedList, "HeadPointer");
                                var elNextPointer = Util.GetXmlElementByTagName(elLinkedList, "NextPointer");
                                var elValue = Util.GetXmlElementByTagName(elLinkedList, "Value");
                                var elSize = Util.GetXmlElementByTagName(elLinkedList, "Size");
                                if (elHeadPointer != null && elNextPointer != null
                                        && elValue != null && elSize != null)
                                {
                                    loaders.Add(new UserLinkedList(typeMatcher,
                                                    new ClassScopeExpression(elHeadPointer.InnerText),
                                                    new ClassScopeExpression(elNextPointer.InnerText),
                                                    new ClassScopeExpression(elValue.InnerText),
                                                    new ClassScopeExpression(elSize.InnerText)));
                                }
                            }
                        }
                        else if (elEntry.Name == "Point")
                        {
                            var elCoords = Util.GetXmlElementByTagName(elEntry, "Coordinates");
                            if (elCoords != null)
                            {
                                var elX = Util.GetXmlElementByTagName(elCoords, "X");
                                var elY = Util.GetXmlElementByTagName(elCoords, "Y");
                                if (elX != null && elY != null)
                                {
                                    ClassScopeExpression exprX = new ClassScopeExpression(elX.InnerText);
                                    ClassScopeExpression exprY = new ClassScopeExpression(elY.InnerText);
                                    loaders.Add(new UserPoint(typeMatcher, exprX, exprY));
                                }
                            }
                        }
                        else if (elEntry.Name == "Linestring")
                        {
                            IUserContainerEntry contEntry = GetContainerEntry(elEntry, "Points");
                            if (contEntry != null)
                                loaders.Add(new UserLinestring(typeMatcher, contEntry));
                        }
                        else if (elEntry.Name == "Ring")
                        {
                            IUserContainerEntry contEntry = GetContainerEntry(elEntry, "Points");
                            if (contEntry != null)
                                loaders.Add(new UserRing(typeMatcher, contEntry));
                        }
                        else if (elEntry.Name == "MultiPoint")
                        {
                            IUserContainerEntry contEntry = GetContainerEntry(elEntry, "Points");
                            if (contEntry != null)
                                loaders.Add(new UserMultiPoint(typeMatcher, contEntry));
                        }
                        else if (elEntry.Name == "MultiLinestring")
                        {
                            IUserContainerEntry contEntry = GetContainerEntry(elEntry, "Linestrings");
                            if (contEntry != null)
                                loaders.Add(new UserMultiLinestring(typeMatcher, contEntry));
                        }
                        else if (elEntry.Name == "Polygon")
                        {
                            var elOuterName = Util.GetXmlElementByTagNames(elEntry, "ExteriorRing", "Name");
                            if (elOuterName != null)
                            {
                                ClassScopeExpression classExprOuter = new ClassScopeExpression(elOuterName.InnerText);
                                IUserContainerEntry innersContEntry = GetContainerEntry(elEntry, "InteriorRings");
                                if (innersContEntry == null)
                                    innersContEntry = new UserEmptyEntry();
                                // TODO: InteriorRings searched the second time
                                var elInners = Util.GetXmlElementByTagName(elEntry, "InteriorRings");
                                var elInnersOffset = Util.GetXmlElementByTagName(elInners, "Offset");

                                int innersOffset = 0;
                                if (elInnersOffset != null)
                                    Util.TryParseInt(elInnersOffset.InnerText, out innersOffset);

                                loaders.Add(new UserPolygon(typeMatcher, classExprOuter, innersContEntry, innersOffset));
                            }
                        }
                        else if (elEntry.Name == "MultiPolygon")
                        {
                            IUserContainerEntry contEntry = GetContainerEntry(elEntry, "Polygons");
                            if (contEntry != null)
                                loaders.Add(new UserMultiPolygon(typeMatcher, contEntry));
                        }
                        else if (elEntry.Name == "MultiGeometry")
                        {
                            IUserContainerEntry contEntry = GetContainerEntry(elEntry, "Geometries");
                            if (contEntry != null)
                                loaders.Add(new UserMultiGeometry(typeMatcher, contEntry));
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
                var elContainer = Util.GetXmlElementByTagName(elElements, "Container");
                if (elContainer != null)
                {
                    var elName = Util.GetXmlElementByTagName(elContainer, "Name");
                    if (elName != null)
                    {
                        return new UserContainerEntry(elName.InnerText);
                    }
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
                    else
                    {
                        var elLinkedList = Util.GetXmlElementByTagName(elElements, "LinkedList");
                        if (elLinkedList != null)
                        {
                            var elHeadPointer = Util.GetXmlElementByTagName(elLinkedList, "HeadPointer");
                            var elNextPointer = Util.GetXmlElementByTagName(elLinkedList, "NextPointer");
                            var elValue = Util.GetXmlElementByTagName(elLinkedList, "Value");
                            var elSize = Util.GetXmlElementByTagName(elLinkedList, "Size");
                            if (elHeadPointer != null && elNextPointer != null
                                    && elValue != null && elSize != null)
                            {
                                return new UserLinkedListEntry(elHeadPointer.InnerText,
                                                               elNextPointer.InnerText,
                                                               elValue.InnerText,
                                                               elSize.InnerText);
                            }
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
