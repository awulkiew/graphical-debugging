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
            public class LoaderCreator : ExpressionLoader.LoaderCreator
            {
                public LoaderCreator(ITypeMatcher typeMatcher,
                                     ClassScopeExpression exprPointer,
                                     ClassScopeExpression exprSize)
                {
                    this.typeMatcher = typeMatcher;
                    this.exprPointer = exprPointer;
                    this.exprSize = exprSize;
                }

                public bool IsUserDefined() { return true; }
                public Kind Kind() { return ExpressionLoader.Kind.Container; }
                public Loader Create(Loaders loaders, Debugger debugger, string name, string type, string id)
                {
                    if (!typeMatcher.MatchType(type, id))
                        return null;

                    exprPointer.Reinitialize(debugger, name, type);
                    exprSize.Reinitialize(debugger, name, type);

                    string elemName = exprPointer.GetString(name) + "[0]";
                    string elemType = ExpressionParser.GetValueType(debugger, elemName);

                    if (Util.Empty(elemType))
                        return null;

                    return new UserArray(exprPointer.DeepCopy(),
                                         exprSize.DeepCopy(),
                                         elemType);
                }

                ITypeMatcher typeMatcher;
                ClassScopeExpression exprPointer;
                ClassScopeExpression exprSize;
            }

            private UserArray(ClassScopeExpression exprPointer,
                              ClassScopeExpression exprSize,
                              string elemType)
            {
                this.exprPointer = exprPointer;
                this.exprSize = exprSize;
                this.elemType = elemType;
            }

            public override void ElementInfo(string name, string type,
                                             out string elemName, out string elemType)
            {
                elemType = this.elemType == null ? "" : this.elemType;
                elemName = exprPointer.GetString(name) + "[0]";
            }

            public override string RandomAccessElementName(string rawName, int i)
            {
                return exprPointer.GetString(rawName) + "[" + i + "]";
            }

            public override int LoadSize(Debugger debugger, string name)
            {
                return ExpressionParser.LoadSize(debugger, exprSize.GetString(name));
            }

            ClassScopeExpression exprPointer;
            ClassScopeExpression exprSize;
            string elemType;
        }

        // TODO: The code is similar to std::list loader, unify if possible
        class UserLinkedList : ContainerLoader
        {
            public class LoaderCreator : ExpressionLoader.LoaderCreator
            {
                public LoaderCreator(ITypeMatcher typeMatcher,
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

                public bool IsUserDefined() { return true; }
                public Kind Kind() { return ExpressionLoader.Kind.Container; }
                public Loader Create(Loaders loaders, Debugger debugger, string name, string type, string id)
                {
                    if (!typeMatcher.MatchType(type, id))
                        return null;

                    exprHeadPointer.Reinitialize(debugger, name, type);
                    exprSize.Reinitialize(debugger, name, type);
                    string headName = '*' + exprHeadPointer.GetString(name);
                    string headType = ExpressionParser.GetValueType(debugger, headName);
                    exprNextPointer.Reinitialize(debugger, headName, headType);
                    exprValue.Reinitialize(debugger, headName, headType);

                    string elemName = exprValue.GetString(headName);
                    string elemType = ExpressionParser.GetValueType(debugger, elemName);

                    if (Util.Empty(elemType))
                        return null;

                    return new UserLinkedList(exprHeadPointer.DeepCopy(),
                                              exprNextPointer.DeepCopy(),
                                              exprValue.DeepCopy(),
                                              exprSize.DeepCopy(),
                                              elemType);
                }

                ITypeMatcher typeMatcher;
                ClassScopeExpression exprHeadPointer;
                ClassScopeExpression exprNextPointer;
                ClassScopeExpression exprValue;
                ClassScopeExpression exprSize;
            }

            private UserLinkedList(ClassScopeExpression exprHeadPointer,
                                   ClassScopeExpression exprNextPointer,
                                   ClassScopeExpression exprValue,
                                   ClassScopeExpression exprSize,
                                   string elemType)
            {
                this.exprHeadPointer = exprHeadPointer;
                this.exprNextPointer = exprNextPointer;
                this.exprValue = exprValue;
                this.exprSize = exprSize;
                this.elemType = elemType;
            }

            public override void ElementInfo(string name, string type,
                                             out string elemName, out string elemType)
            {
                elemType = this.elemType == null ? "" : this.elemType;
                // TODO: Only C++ ?
                elemName = exprValue.GetString('*' + exprHeadPointer.GetString(name));
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

            ClassScopeExpression exprHeadPointer;
            ClassScopeExpression exprNextPointer;
            ClassScopeExpression exprValue;
            ClassScopeExpression exprSize;
            string elemType;
        }

        class UserPoint : PointLoader
        {
            public class LoaderCreator : ExpressionLoader.LoaderCreator
            {
                public LoaderCreator(ITypeMatcher typeMatcher,
                                     ClassScopeExpression exprX,
                                     ClassScopeExpression exprY,
                                     Geometry.CoordinateSystem cs,
                                     Geometry.Unit unit)
                {
                    this.typeMatcher = typeMatcher;
                    this.exprX = exprX;
                    this.exprY = exprY;
                    this.cs = cs;
                    this.unit = unit;
                }

                public bool IsUserDefined() { return true; }
                public Kind Kind() { return ExpressionLoader.Kind.Point; }
                public Loader Create(Loaders loaders, Debugger debugger, string name, string type, string id)
                {
                    if (!typeMatcher.MatchType(type, id))
                        return null;

                    exprX.Reinitialize(debugger, name, type);
                    exprY.Reinitialize(debugger, name, type);

                    UserPoint res = new UserPoint(exprX.DeepCopy(),
                                                  exprY.DeepCopy(),
                                                  cs,
                                                  unit);

                    res.sizeOf = ExpressionParser.GetTypeSizeof(debugger, type);
                    if (ExpressionParser.IsInvalidSize(res.sizeOf))
                        //res.sizeOf = 0;
                        return null;

                    string nameX = res.exprX.GetString(name);
                    string nameY = res.exprY.GetString(name);
                    res.typeX = ExpressionParser.GetValueType(debugger, nameX);
                    res.typeY = ExpressionParser.GetValueType(debugger, nameY);
                    if (ExpressionParser.IsInvalidType(res.typeX, res.typeY))
                        //res.sizeOf = 0;
                        return null;

                    res.sizeX = ExpressionParser.GetTypeSizeof(debugger, res.typeX);
                    res.sizeY = ExpressionParser.GetTypeSizeof(debugger, res.typeY);
                    if (ExpressionParser.IsInvalidSize(res.sizeX, res.sizeY))
                        //res.sizeOf = 0;
                        return null;

                    res.offsetX = ExpressionParser.GetAddressDifference(debugger, name, nameX);
                    res.offsetY = ExpressionParser.GetAddressDifference(debugger, name, nameY);
                    // offsetX + sizeX > sizeOf
                    // offsetY + sizeY > sizeOf
                    if (ExpressionParser.IsInvalidOffset(res.sizeOf, res.offsetX, res.offsetY))
                        //res.sizeOf = 0;
                        return null;

                    return res;
                }

                ITypeMatcher typeMatcher;
                ClassScopeExpression exprX;
                ClassScopeExpression exprY;
                Geometry.CoordinateSystem cs;
                Geometry.Unit unit;
            }

            private UserPoint(ClassScopeExpression exprX,
                              ClassScopeExpression exprY,
                              Geometry.CoordinateSystem cs,
                              Geometry.Unit unit)
            {
                this.exprX = exprX;
                this.exprY = exprY;
                this.cs = cs;
                this.unit = unit;

                this.typeX = null;
                this.typeY = null;
                this.sizeOf = 0;
            }

            public override Geometry.Traits LoadTraits(string type)
            {
                // NOTE: in the future dimension should also be loaded
                //   but for now set it to 2 since the extension ignores higher dimensions anyway
                return new Geometry.Traits(2, cs, unit);
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

            ClassScopeExpression exprX;
            ClassScopeExpression exprY;
            Geometry.CoordinateSystem cs;
            Geometry.Unit unit;
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
            public ClassScopeExpression ContainerNameExpr;
            public string ContainerType;
            public string ElementType;
        }

        interface IUserContainerEntry
        {
            UserContainerLoaders<ElementLoader> Create<ElementLoader>(
                    IKindConstraint elementKindConstraint,
                    Loaders loaders, Debugger debugger, string name, string type, string id)
                where ElementLoader : Loader;
        }

        class UserEmptyEntry : IUserContainerEntry
        {
            public UserContainerLoaders<ElementLoader> Create<ElementLoader>(
                    IKindConstraint elementKindConstraint,
                    Loaders loaders, Debugger debugger, string name, string type, string id)
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

            public UserContainerLoaders<ElementLoader> Create<ElementLoader>(
                    IKindConstraint elementKindConstraint,
                    Loaders loaders, Debugger debugger, string name, string type, string id)
                where ElementLoader : Loader
            {
                exprContainerName.Reinitialize(debugger, name, type);
                string containerName = exprContainerName.GetString(name);
                string containerType = ExpressionParser.GetValueType(debugger, containerName);
                if (containerType == null)
                    return null;

                ContainerLoader containerLoader = loaders.FindByType(ExpressionLoader.Kind.Container, containerName, containerType) as ContainerLoader;
                if (containerLoader == null)
                    return null;

                string elementName, elementType;
                containerLoader.ElementInfo(containerName, containerType,
                                            out elementName, out elementType);
                ElementLoader elementLoader = loaders.FindByType(elementKindConstraint,
                                                                 elementName,
                                                                 elementType) as ElementLoader;
                if (elementLoader == null)
                    return null;

                UserContainerLoaders<ElementLoader> result = new UserContainerLoaders<ElementLoader>();
                result.ContainerLoader = containerLoader;
                result.ElementLoader = elementLoader;
                result.ContainerNameExpr = exprContainerName.DeepCopy();
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

            public UserContainerLoaders<ElementLoader> Create<ElementLoader>(
                    IKindConstraint elementKindConstraint,
                    Loaders loaders, Debugger debugger, string name, string type, string id)
                where ElementLoader : Loader
            {
                var creator = new UserArray.LoaderCreator(new DummyMatcher(), exprPointer, exprSize);
                UserArray containerLoader = creator.Create(loaders, debugger, name, type, id) as UserArray;
                if (containerLoader == null)
                    return null;

                string elementName, elementType;
                containerLoader.ElementInfo(name, type,
                                            out elementName, out elementType);
                ElementLoader elementLoader = loaders.FindByType(elementKindConstraint,
                                                                 elementName,
                                                                 elementType) as ElementLoader;
                if (elementLoader == null)
                    return null;

                UserContainerLoaders<ElementLoader> result = new UserContainerLoaders<ElementLoader>();
                result.ContainerLoader = containerLoader;
                result.ElementLoader = elementLoader;
                result.ContainerNameExpr = new ClassScopeExpression(new ClassScopeExpression.NamePart());
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

            public UserContainerLoaders<ElementLoader> Create<ElementLoader>(
                    IKindConstraint elementKindConstraint,
                    Loaders loaders, Debugger debugger, string name, string type, string id)
                where ElementLoader : Loader
            {
                var creator = new UserLinkedList.LoaderCreator(new DummyMatcher(),
                                        exprHeadPointer, exprNextPointer, exprValue, exprSize);
                UserLinkedList containerLoader = creator.Create(loaders, debugger, name, type, id) as UserLinkedList;
                if (containerLoader == null)
                    return null;

                string elementName, elementType;
                containerLoader.ElementInfo(name, type,
                                            out elementName, out elementType);
                ElementLoader elementLoader = loaders.FindByType(elementKindConstraint,
                                                                 elementName,
                                                                 elementType) as ElementLoader;
                if (elementLoader == null)
                    return null;

                UserContainerLoaders<ElementLoader> result = new UserContainerLoaders<ElementLoader>();
                result.ContainerLoader = containerLoader;
                result.ElementLoader = elementLoader;
                result.ContainerNameExpr = new ClassScopeExpression(new ClassScopeExpression.NamePart());
                result.ContainerType = type;
                result.ElementType = elementType;
                return result;
            }

            ClassScopeExpression exprHeadPointer;
            ClassScopeExpression exprNextPointer;
            ClassScopeExpression exprValue;
            ClassScopeExpression exprSize;
        }

        class UserPointRange<ResultType> : PointRange<ResultType>
            where ResultType : class
                             , ExpressionDrawer.IDrawable
                             , Geometry.IContainer<Geometry.Point>
                             , new()
        {
            public class LoaderCreator : ExpressionLoader.LoaderCreator
            {
                public delegate Loader DerivedConstructor(UserContainerLoaders<PointLoader> containerLoaders);

                public LoaderCreator(ExpressionLoader.Kind kind,
                                     ITypeMatcher typeMatcher,
                                     IUserContainerEntry containerEntry,
                                     DerivedConstructor derivedConstructor)
                {
                    this.kind = kind;
                    this.typeMatcher = typeMatcher;
                    this.containerEntry = containerEntry;
                    this.derivedConstructor = derivedConstructor;
                }

                public bool IsUserDefined() { return true; }
                public Kind Kind() { return kind; }
                public Loader Create(Loaders loaders, Debugger debugger, string name, string type, string id)
                {
                    if (!typeMatcher.MatchType(type, id))
                        return null;

                    UserContainerLoaders<PointLoader> containerLoaders
                        = containerEntry.Create<PointLoader>(
                                new KindConstraint(ExpressionLoader.Kind.Point),
                                loaders, debugger, name, type, id);
                    if (containerLoaders == null)
                        return null;

                    return derivedConstructor(containerLoaders);
                }

                ExpressionLoader.Kind kind;
                ITypeMatcher typeMatcher;
                IUserContainerEntry containerEntry;
                DerivedConstructor derivedConstructor;
            }

            protected UserPointRange(UserContainerLoaders<PointLoader> containerLoaders)
            {
                this.containerLoaders = containerLoaders;
            }

            public override void Load(Loaders loaders, MemoryReader mreader, Debugger debugger,
                                      string name, string type,
                                      out Geometry.Traits traits,
                                      out ResultType result,
                                      LoadCallback callback)
            {
                traits = null;
                result = null;

                traits = containerLoaders.ElementLoader.LoadTraits(containerLoaders.ElementType);
                if (traits == null)
                    return;

                string containerName = containerLoaders.ContainerNameExpr.GetString(name);
                if (mreader != null)
                {
                    string elementName, elementType;
                    containerLoaders.ContainerLoader.ElementInfo(containerName,
                                                                 containerLoaders.ContainerType,
                                                                 out elementName, out elementType);

                    result = LoadMemory(loaders, mreader, debugger,
                                        containerName,
                                        containerLoaders.ContainerType,
                                        elementName,
                                        containerLoaders.ElementType,
                                        containerLoaders.ElementLoader,
                                        containerLoaders.ContainerLoader,
                                        callback);
                }

                if (result == null)
                {
                    result = LoadParsed(mreader, debugger,
                                        containerName,
                                        containerLoaders.ContainerType,
                                        containerLoaders.ElementType,
                                        containerLoaders.ElementLoader,
                                        containerLoaders.ContainerLoader,
                                        callback);
                }
            }

            UserContainerLoaders<PointLoader> containerLoaders;
        }

        class UserLinestring : UserPointRange<ExpressionDrawer.Linestring>
        {
            public new class LoaderCreator : UserPointRange<ExpressionDrawer.Linestring>.LoaderCreator
            {
                public LoaderCreator(ITypeMatcher typeMatcher,
                                     IUserContainerEntry containerEntry)
                    : base(ExpressionLoader.Kind.Linestring, typeMatcher, containerEntry,
                           delegate (UserContainerLoaders<PointLoader> containerLoaders)
                           {
                               return new UserLinestring(containerLoaders);
                           })
                { }
            }

            private UserLinestring(UserContainerLoaders<PointLoader> containerLoaders)
                : base(containerLoaders)
            { }
        }

        class UserRing : UserPointRange<ExpressionDrawer.Ring>
        {
            public new class LoaderCreator : UserPointRange<ExpressionDrawer.Ring>.LoaderCreator
            {
                public LoaderCreator(ITypeMatcher typeMatcher,
                                     IUserContainerEntry containerEntry)
                    : base(ExpressionLoader.Kind.Ring, typeMatcher, containerEntry,
                           delegate (UserContainerLoaders<PointLoader> containerLoaders)
                           {
                               return new UserRing(containerLoaders);
                           })
                { }
            }

            private UserRing(UserContainerLoaders<PointLoader> containerLoaders)
                : base(containerLoaders)
            { }
        }

        class UserMultiPoint : UserPointRange<ExpressionDrawer.MultiPoint>
        {
            public new class LoaderCreator : UserPointRange<ExpressionDrawer.MultiPoint>.LoaderCreator
            {
                public LoaderCreator(ITypeMatcher typeMatcher,
                                     IUserContainerEntry containerEntry)
                    : base(ExpressionLoader.Kind.MultiPoint, typeMatcher, containerEntry,
                           delegate (UserContainerLoaders<PointLoader> containerLoaders)
                           {
                               return new UserMultiPoint(containerLoaders);
                           })
                { }
            }

            private UserMultiPoint(UserContainerLoaders<PointLoader> containerLoaders)
                : base(containerLoaders)
            { }
        }

        class UserMultiLinestring : RangeLoader<ExpressionDrawer.MultiLinestring>
        {
            public class LoaderCreator : ExpressionLoader.LoaderCreator
            {
                public LoaderCreator(ITypeMatcher typeMatcher,
                                     IUserContainerEntry containerEntry)
                {
                    this.typeMatcher = typeMatcher;
                    this.containerEntry = containerEntry;
                }

                public bool IsUserDefined() { return true; }
                public Kind Kind() { return ExpressionLoader.Kind.MultiLinestring; }
                public Loader Create(Loaders loaders, Debugger debugger, string name, string type, string id)
                {
                    if (!typeMatcher.MatchType(type, id))
                        return null;

                    UserContainerLoaders<RangeLoader<ExpressionDrawer.Linestring>> containerLoaders
                        = containerEntry.Create<RangeLoader<ExpressionDrawer.Linestring>>(
                                new KindConstraint(ExpressionLoader.Kind.Linestring),
                                loaders, debugger, name, type, id);
                    if (containerLoaders == null)
                        return null;

                    return new UserMultiLinestring(containerLoaders);
                }

                ITypeMatcher typeMatcher;
                IUserContainerEntry containerEntry;
            }

            private UserMultiLinestring(UserContainerLoaders<RangeLoader<ExpressionDrawer.Linestring>> containerLoaders)
            {
                this.containerLoaders = containerLoaders;
            }

            public override void Load(Loaders loaders, MemoryReader mreader, Debugger debugger,
                                      string name, string type,
                                      out Geometry.Traits traits,
                                      out ExpressionDrawer.MultiLinestring result,
                                      LoadCallback callback)
            {
                traits = null;
                result = null;

                Geometry.Traits t = null;
                ExpressionDrawer.MultiLinestring mls = new ExpressionDrawer.MultiLinestring();
                string containerName = containerLoaders.ContainerNameExpr.GetString(name);
                bool ok = containerLoaders.ContainerLoader.ForEachElement(
                    debugger, containerName,
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

            UserContainerLoaders<RangeLoader<ExpressionDrawer.Linestring>> containerLoaders;
        }

        class UserPolygon : PolygonLoader
        {
            public class LoaderCreator : ExpressionLoader.LoaderCreator
            {
                public LoaderCreator(ITypeMatcher typeMatcher,
                                     ClassScopeExpression exprOuter,
                                     IUserContainerEntry innersContEntry,
                                     int innersOffset)
                {
                    this.typeMatcher = typeMatcher;
                    this.exprOuter = exprOuter;
                    this.innersContEntry = innersContEntry;
                    this.innersOffset = innersOffset;
                }

                public bool IsUserDefined() { return true; }
                public Kind Kind() { return ExpressionLoader.Kind.Polygon; }
                public Loader Create(Loaders loaders, Debugger debugger, string name, string type, string id)
                {
                    if (!typeMatcher.MatchType(type, id))
                        return null;

                    exprOuter.Reinitialize(debugger, name, type);

                    string outerName = exprOuter.GetString(name);
                    string outerType = ExpressionParser.GetValueType(debugger, outerName);
                    LoaderR<ExpressionDrawer.Ring> outerLoader = loaders.FindByType(ExpressionLoader.Kind.Ring,
                                                                                    outerName, outerType) as LoaderR<ExpressionDrawer.Ring>;
                    if (outerLoader == null)
                        return null;

                    UserContainerLoaders<LoaderR<ExpressionDrawer.Ring>> innersLoaders
                        = innersContEntry.Create<LoaderR<ExpressionDrawer.Ring>>(
                                new KindConstraint(ExpressionLoader.Kind.Ring),
                                loaders, debugger, name, type, id);
                    
                    // If there is no definition of inner rings pass null as it is

                    return new UserPolygon(exprOuter.DeepCopy(),
                                           outerType,
                                           outerLoader,
                                           innersLoaders,
                                           innersOffset);
                }

                ITypeMatcher typeMatcher;
                ClassScopeExpression exprOuter;
                IUserContainerEntry innersContEntry;
                int innersOffset;
            }

            private UserPolygon(ClassScopeExpression exprOuter,
                                string outerType,
                                LoaderR<ExpressionDrawer.Ring> outerLoader,
                                UserContainerLoaders<LoaderR<ExpressionDrawer.Ring>> innersLoaders,
                                int innersOffset)
            {
                this.exprOuter = exprOuter;
                this.outerType = outerType;
                this.outerLoader = outerLoader;
                this.innersLoaders = innersLoaders;
                this.innersOffset = innersOffset;
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
                //string outerType = ExpressionParser.GetValueType(debugger, outerName);
                ExpressionDrawer.Ring outer = null;
                outerLoader.Load(loaders, mreader, debugger, outerName, outerType,
                                 out traits, out outer,
                                 callback);
                if (outer == null)
                    return;

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
                string innersName = innersLoaders.ContainerNameExpr.GetString(name);
                bool ok = innersLoaders.ContainerLoader.ForEachElement(
                    debugger, innersName,
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

            ClassScopeExpression exprOuter;
            string outerType;
            LoaderR<ExpressionDrawer.Ring> outerLoader;
            UserContainerLoaders<LoaderR<ExpressionDrawer.Ring>> innersLoaders;
            int innersOffset;
        }

        class UserMultiPolygon : RangeLoader<ExpressionDrawer.MultiPolygon>
        {
            public class LoaderCreator : ExpressionLoader.LoaderCreator
            {
                public LoaderCreator(ITypeMatcher typeMatcher,
                                     IUserContainerEntry containerEntry)
                {
                    this.typeMatcher = typeMatcher;
                    this.containerEntry = containerEntry;
                }

                public bool IsUserDefined() { return true; }
                public Kind Kind() { return ExpressionLoader.Kind.MultiPolygon; }
                public Loader Create(Loaders loaders, Debugger debugger, string name, string type, string id)
                {
                    if (!typeMatcher.MatchType(type, id))
                        return null;

                    UserContainerLoaders<PolygonLoader> containerLoaders
                        = containerEntry.Create<PolygonLoader>(
                                new KindConstraint(ExpressionLoader.Kind.Polygon),
                                loaders, debugger, name, type, id);
                    if (containerLoaders == null)
                        return null;

                    return new UserMultiPolygon(containerLoaders);
                }

                ITypeMatcher typeMatcher;
                IUserContainerEntry containerEntry;
            }

            public UserMultiPolygon(UserContainerLoaders<PolygonLoader> containerLoaders)
            {
                this.containerLoaders = containerLoaders;
            }

            public override void Load(Loaders loaders, MemoryReader mreader, Debugger debugger,
                                      string name, string type,
                                      out Geometry.Traits traits,
                                      out ExpressionDrawer.MultiPolygon result,
                                      LoadCallback callback)
            {
                traits = null;
                result = null;

                Geometry.Traits t = null;
                ExpressionDrawer.MultiPolygon mpoly = new ExpressionDrawer.MultiPolygon();
                string containerName = containerLoaders.ContainerNameExpr.GetString(name);
                bool ok = containerLoaders.ContainerLoader.ForEachElement(
                    debugger, containerName,
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

            UserContainerLoaders<PolygonLoader> containerLoaders;
        }

        // TODO: If possible use one implementation for MultiLinestring, MultiPolygon and MultiGeometry
        class UserMultiGeometry : RangeLoader<ExpressionDrawer.DrawablesContainer>
        {
            public class LoaderCreator : ExpressionLoader.LoaderCreator
            {
                public LoaderCreator(ITypeMatcher typeMatcher,
                                     IUserContainerEntry containerEntry)
                {
                    this.typeMatcher = typeMatcher;
                    this.containerEntry = containerEntry;
                }

                public bool IsUserDefined() { return true; }
                public Kind Kind() { return ExpressionLoader.Kind.MultiGeometry; }
                public Loader Create(Loaders loaders, Debugger debugger, string name, string type, string id)
                {
                    if (!typeMatcher.MatchType(type, id))
                        return null;

                    UserContainerLoaders<DrawableLoader> containerLoaders
                        = containerEntry.Create<DrawableLoader>(
                            OnlyGeometries, loaders, debugger, name, type, id);
                    if (containerLoaders == null)
                        return null;

                    return new UserMultiGeometry(containerLoaders);
                }

                ITypeMatcher typeMatcher;
                IUserContainerEntry containerEntry;
            }

            public UserMultiGeometry(UserContainerLoaders<DrawableLoader> containerLoaders)
            {
                this.containerLoaders = containerLoaders;
            }

            public override void Load(Loaders loaders, MemoryReader mreader, Debugger debugger,
                                      string name, string type,
                                      out Geometry.Traits traits,
                                      out ExpressionDrawer.DrawablesContainer result,
                                      LoadCallback callback)
            {
                traits = null;
                result = null;

                Geometry.Traits t = null;
                ExpressionDrawer.DrawablesContainer drawables = new ExpressionDrawer.DrawablesContainer();
                string containerName = containerLoaders.ContainerNameExpr.GetString(name);
                bool ok = containerLoaders.ContainerLoader.ForEachElement(
                    debugger, containerName,
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

            UserContainerLoaders<DrawableLoader> containerLoaders;
        }

        private static bool ReloadUserTypes(Loaders loaders,
                                            LoadersCache loadersCache,
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
            {
                loaders.RemoveUserDefined();
                loadersCache.Clear();
            }

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
                                    loaders.Add(new UserArray.LoaderCreator(
                                                    typeMatcher,
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
                                    loaders.Add(new UserLinkedList.LoaderCreator(
                                                    typeMatcher,
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
                                    Geometry.CoordinateSystem cs = Geometry.CoordinateSystem.Cartesian;
                                    Geometry.Unit unit = Geometry.Unit.None;
                                    GetCSAndUnit(elEntry, out cs, out unit);

                                    ClassScopeExpression exprX = new ClassScopeExpression(elX.InnerText);
                                    ClassScopeExpression exprY = new ClassScopeExpression(elY.InnerText);
                                    loaders.Add(new UserPoint.LoaderCreator(
                                                    typeMatcher, exprX, exprY, cs, unit));
                                }
                            }
                        }
                        else if (elEntry.Name == "Linestring")
                        {
                            IUserContainerEntry contEntry = GetContainerEntry(elEntry, "Points");
                            if (contEntry != null)
                                loaders.Add(new UserLinestring.LoaderCreator(typeMatcher, contEntry));
                        }
                        else if (elEntry.Name == "Ring")
                        {
                            IUserContainerEntry contEntry = GetContainerEntry(elEntry, "Points");
                            if (contEntry != null)
                                loaders.Add(new UserRing.LoaderCreator(typeMatcher, contEntry));
                        }
                        else if (elEntry.Name == "MultiPoint")
                        {
                            IUserContainerEntry contEntry = GetContainerEntry(elEntry, "Points");
                            if (contEntry != null)
                                loaders.Add(new UserMultiPoint.LoaderCreator(typeMatcher, contEntry));
                        }
                        else if (elEntry.Name == "MultiLinestring")
                        {
                            IUserContainerEntry contEntry = GetContainerEntry(elEntry, "Linestrings");
                            if (contEntry != null)
                                loaders.Add(new UserMultiLinestring.LoaderCreator(typeMatcher, contEntry));
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

                                loaders.Add(new UserPolygon.LoaderCreator(typeMatcher, classExprOuter, innersContEntry, innersOffset));
                            }
                        }
                        else if (elEntry.Name == "MultiPolygon")
                        {
                            IUserContainerEntry contEntry = GetContainerEntry(elEntry, "Polygons");
                            if (contEntry != null)
                                loaders.Add(new UserMultiPolygon.LoaderCreator(typeMatcher, contEntry));
                        }
                        else if (elEntry.Name == "MultiGeometry")
                        {
                            IUserContainerEntry contEntry = GetContainerEntry(elEntry, "Geometries");
                            if (contEntry != null)
                                loaders.Add(new UserMultiGeometry.LoaderCreator(typeMatcher, contEntry));
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

        static void GetCSAndUnit(System.Xml.XmlElement el,
                                 out Geometry.CoordinateSystem cs,
                                 out Geometry.Unit unit)
        {
            cs = Geometry.CoordinateSystem.Cartesian;
            unit = Geometry.Unit.None;

            string csStr = el.GetAttribute("CoordinateSystem");
            if (!Util.Empty(csStr))
            {
                if (csStr == "Spherical" || csStr == "SphericalEquatorial")
                    cs = Geometry.CoordinateSystem.SphericalEquatorial;
                else if (csStr == "SphericalPolar")
                    cs = Geometry.CoordinateSystem.SphericalPolar;
                else if (csStr == "Geographic")
                    cs = Geometry.CoordinateSystem.Geographic;
                else if (csStr == "Complex")
                    cs = Geometry.CoordinateSystem.Complex;
            }

            string unitStr = el.GetAttribute("Unit");
            if (!Util.Empty(unitStr))
            {
                if (unitStr == "Radian")
                    unit = Geometry.Unit.Radian;
                else if (unitStr == "Degree")
                    unit = Geometry.Unit.Degree;
            }

            if (cs == Geometry.CoordinateSystem.SphericalEquatorial
                || cs == Geometry.CoordinateSystem.SphericalPolar
                || cs == Geometry.CoordinateSystem.Geographic)
            {
                if (unit == Geometry.Unit.None)
                    unit = Geometry.Unit.Degree;
            }
            else
            {
                unit = Geometry.Unit.None;
            }
        }

        public static void ReloadUserTypes(GeneralOptionPage options)
        {
            if (options == null)
                return;

            DateTime wtCpp;
            if (ReloadUserTypes(Instance.loadersCpp,
                                Instance.loadersCacheCpp,
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
                                Instance.loadersCacheCS,
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
