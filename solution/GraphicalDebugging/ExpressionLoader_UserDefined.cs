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
            public class LoaderCreator : ExpressionLoader.ILoaderCreator
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
                    string elemType = debugger.GetValueType(elemName);

                    if (Util.Empty(elemType))
                        return null;

                    return new UserArray(exprPointer.DeepCopy(),
                                         exprSize.DeepCopy(),
                                         elemType);
                }

                readonly ITypeMatcher typeMatcher;
                readonly ClassScopeExpression exprPointer;
                readonly ClassScopeExpression exprSize;
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
                elemType = this.elemType ?? "";
                elemName = exprPointer.GetString(name) + "[0]";
            }

            public override string RandomAccessElementName(string rawName, int i)
            {
                return exprPointer.GetString(rawName) + "[" + i + "]";
            }

            public override int LoadSize(Debugger debugger, string name)
            {
                return debugger.TryLoadUInt(exprSize.GetString(name), out int size) ? size : 0;
            }

            public override ulong MemoryBegin(MemoryReader mreader, ulong address)
            {
                // TODO
                return 0;
            }

            public override Size LoadSize(MemoryReader mreader, ulong address)
            {
                // TODO
                return new Size();
            }

            public override bool ForEachMemoryBlock<T>(MemoryReader mreader, Debugger debugger,
                                                       string name, string type, ulong dummyAddress,
                                                       MemoryReader.Converter<T> elementConverter,
                                                       MemoryBlockPredicate<T> memoryBlockPredicate)
            {
                if (mreader == null)
                    return false;

                int size = LoadSize(debugger, name);
                if (size <= 0)
                    return true;

                ElementInfo(name, type, out string elemName, out string _);
                if (!debugger.GetValueAddress(elemName, out ulong beginAddress))
                    return false;

                var blockConverter = new MemoryReader.ArrayConverter<T>(elementConverter, size);
                T[] values = new T[blockConverter.ValueCount()];
                if (!mreader.Read(beginAddress, values, blockConverter))
                    return false;
                return memoryBlockPredicate(values);
            }

            readonly ClassScopeExpression exprPointer;
            readonly ClassScopeExpression exprSize;
            readonly string elemType;
        }

        // TODO: The code is similar to std::list loader, unify if possible
        class UserLinkedList : ContainerLoader
        {
            public class LoaderCreator : ExpressionLoader.ILoaderCreator
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
                    string headType = debugger.GetValueType(headName);
                    exprNextPointer.Reinitialize(debugger, headName, headType);
                    exprValue.Reinitialize(debugger, headName, headType);

                    string elemName = exprValue.GetString(headName);
                    string elemType = debugger.GetValueType(elemName);

                    if (Util.Empty(elemType))
                        return null;

                    return new UserLinkedList(exprHeadPointer.DeepCopy(),
                                              exprNextPointer.DeepCopy(),
                                              exprValue.DeepCopy(),
                                              exprSize.DeepCopy(),
                                              elemType);
                }

                readonly ITypeMatcher typeMatcher;
                readonly ClassScopeExpression exprHeadPointer;
                readonly ClassScopeExpression exprNextPointer;
                readonly ClassScopeExpression exprValue;
                readonly ClassScopeExpression exprSize;
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
                elemType = this.elemType ?? "";
                // TODO: Only C++ ?
                elemName = exprValue.GetString('*' + exprHeadPointer.GetString(name));
            }

            public override int LoadSize(Debugger debugger, string name)
            {
                return debugger.TryLoadUInt(exprSize.GetString(name), out int result) ? result : 0;
            }

            public override Size LoadSize(MemoryReader mreader, ulong address)
            {
                // TODO
                return new Size();
            }

            public override bool ForEachMemoryBlock<T>(MemoryReader mreader, Debugger debugger,
                                                       string name, string type, ulong dummyAddress,
                                                       MemoryReader.Converter<T> elementConverter,
                                                       MemoryBlockPredicate<T> memoryBlockPredicate)
            {
                if (mreader == null)
                    return false;

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

                if (!debugger.GetAddressOffset(headName, nextPointerName, out long nextDiff)
                 || !debugger.GetAddressOffset(headName, valName, out long valDiff)
                 || !debugger.GetValueAddress(headName, out ulong nodeAddress))
                    return false;

                for (int i = 0; i < size; ++i)
                {
                    T[] values = new T[elementConverter.ValueCount()];
                    if (!mreader.Read(nodeAddress + (ulong)valDiff, values, elementConverter))
                        return false;

                    if (!memoryBlockPredicate(values))
                        return false;

                    ulong[] nextTmp = new ulong[1];
                    if (!mreader.Read(nodeAddress + (ulong)nextDiff, nextTmp, nextConverter))
                        return false;
                    nodeAddress = nextTmp[0];
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

            readonly ClassScopeExpression exprHeadPointer;
            readonly ClassScopeExpression exprNextPointer;
            readonly ClassScopeExpression exprValue;
            readonly ClassScopeExpression exprSize;
            readonly string elemType;
        }

        class UserValue
        {
            public UserValue(double value) { Value = value; }
            public double Value;
        }

        class UserValueMember
        {
            public UserValueMember(Debugger debugger,
                                   string parentName, string parentType, int parentSizeOf,
                                   ClassScopeExpression memberExpr)
            {
                IsValid = false;

                expr = memberExpr;
                expr.Reinitialize(debugger, parentName, parentType);
                
                string name = expr.GetString(parentName);
                type = debugger.GetValueType(name);

                IsValid = !Debugger.IsInvalidType(type)
                       && debugger.GetTypeSizeof(type, out sizeOf)
                       && debugger.GetAddressOffset(parentName, name, out offset)
                       // offset + size > sizeOf
                       && !Debugger.IsInvalidOffset(parentSizeOf, offset);
            }

            public UserValue Load(MemoryReader mreader, Debugger debugger,
                                  string parentName)
            {
                UserValue result = null;
                if (mreader != null)
                    result = LoadMemory(mreader, debugger, parentName);
                if (result == null)
                    result = LoadParsed(debugger, parentName);
                return result;
            }

            public UserValue LoadParsed(Debugger debugger,
                                         string parentName)
            {
                string name = expr.GetString(parentName);
                return debugger.TryLoadDouble(name, out double val)
                     ? new UserValue(val)
                     : null;
            }

            public UserValue LoadMemory(MemoryReader mreader, Debugger debugger,
                                         string parentName)
            {
                MemoryReader.ValueConverter<double> converter = mreader.GetNumericConverter(type, sizeOf);
                if (converter == null)
                    return null;

                string name = expr.GetString(parentName);
                if (!debugger.GetValueAddress(name, out ulong address))
                    return null;

                double[] values = new double[1];
                if (!mreader.Read(address, values, converter))
                    return null;

                return new UserValue(values[0]);
            }

            public MemoryReader.Member<double> GetMemoryConverter(MemoryReader mreader,
                                                                  Debugger debugger, // TODO - remove
                                                                  string parentName)
            {
                MemoryReader.ValueConverter<double> converter = mreader.GetNumericConverter(type, sizeOf);
                if (converter == null)
                    return null;
                return new MemoryReader.Member<double>(converter, (int)offset);
            }

            public bool IsValid;

            readonly ClassScopeExpression expr;
            // For memory loading:
            readonly string type;
            readonly int sizeOf;
            readonly long offset;
        }

        class UserPointMember
        {
            public UserPointMember(Loaders loaders, Debugger debugger,
                                   string parentName, string parentType, int parentSizeOf,
                                   ClassScopeExpression memberExpr)
            {
                IsValid = false;

                expr = memberExpr;
                expr.Reinitialize(debugger, parentName, parentType);

                string name = expr.GetString(parentName);
                type = debugger.GetValueType(name);
                if (Debugger.IsInvalidType(type))
                    return;

                loader = loaders.FindByType(ExpressionLoader.Kind.Point, name, type) as PointLoader;
                if (loader == null)
                    return;

                // offset + size > sizeOf
                IsValid = debugger.GetAddressOffset(parentName, name, out offset)
                       && !Debugger.IsInvalidOffset(parentSizeOf, offset);
            }

            public Geometry.Traits GetTraits(MemoryReader mreader, Debugger debugger, string parentName)
            {
                string name = expr.GetString(parentName);
                return loader.GetTraits(mreader, debugger, name);
            }

            public Geometry.Point Load(MemoryReader mreader, Debugger debugger, string parentName)
            {
                string name = expr.GetString(parentName);
                return loader.LoadPoint(mreader, debugger, name, type);
            }

            public Geometry.Point LoadParsed(Debugger debugger, string parentName)
            {
                string name = expr.GetString(parentName);
                return loader.LoadPointParsed(debugger, name, type);
            }

            public Geometry.Point LoadMemory(MemoryReader mreader, Debugger debugger, string parentName)
            {
                string name = expr.GetString(parentName);
                return loader.LoadPointMemory(mreader, debugger, name, type);
            }

            public MemoryReader.Member<double> GetMemoryConverter(MemoryReader mreader,
                                                                  Debugger debugger, // TODO - remove
                                                                  string parentName)
            {
                string name = expr.GetString(parentName);                
                MemoryReader.Converter<double> converter = loader.GetMemoryConverter(mreader, debugger, name, type);
                if (converter == null)
                    return null;

                return new MemoryReader.Member<double>(converter, (int)offset);
            }

            public bool IsValid;

            readonly ClassScopeExpression expr;
            readonly PointLoader loader;
            // For memory loading:
            readonly string type;
            readonly long offset;
        }

        class UserSimpleGeometryMembers
        {
            public Geometry.Point[] Points;
            public double[] Values;
        }

        class UserSimpleGeometry
        {
            public UserSimpleGeometry(Loaders loaders, Debugger debugger,
                                      string name, string type,
                                      ClassScopeExpression[] pointExprs,
                                      ClassScopeExpression[] valueExprs,
                                      Geometry.Traits traits)
            {
                IsValid = false;

                if (!debugger.GetTypeSizeof(type, out sizeOf))
                    return;

                bool allValid = true;
                if (pointExprs != null)
                {
                    pointMembers = new UserPointMember[pointExprs.Length];
                    for (int i = 0; i < pointExprs.Length; ++i)
                    {
                        pointMembers[i] = new UserPointMember(loaders, debugger,
                                                              name, type,
                                                              sizeOf, pointExprs[i]);
                        if (!pointMembers[i].IsValid)
                            allValid = false;
                    }
                }
                
                if (valueExprs != null)
                {
                    valueMembers = new UserValueMember[valueExprs.Length];
                    for (int i = 0; i < valueExprs.Length; ++i)
                    {
                        valueMembers[i] = new UserValueMember(debugger,
                                                              name, type,
                                                              sizeOf, valueExprs[i]);
                        if (!valueMembers[i].IsValid)
                            allValid = false;
                    }
                }
                
                this.traits = traits;

                IsValid = allValid;
            }
            
            public Geometry.Traits GetTraits(MemoryReader mreader, Debugger debugger,
                                             string name)
            {
                if (traits != null)
                    return traits;
                if (pointMembers != null && pointMembers.Length > 0 && pointMembers[0].IsValid)
                    return pointMembers[0].GetTraits(mreader, debugger, name);
                return null;
            }
            
            public UserSimpleGeometryMembers Load(MemoryReader mreader, Debugger debugger,
                                                  string name)
            {
                UserSimpleGeometryMembers result = new UserSimpleGeometryMembers();

                if (pointMembers != null)
                {
                    Geometry.Point[] points = new Geometry.Point[pointMembers.Length];
                    for (int i = 0; i < pointMembers.Length; ++i)
                    {
                        points[i] = pointMembers[i].Load(mreader, debugger, name);
                        if (points[i] == null)
                            return null;
                    }
                    result.Points = points;
                }

                if (valueMembers != null)
                {
                    double[] values = new double[valueMembers.Length];
                    for (int i = 0; i < valueMembers.Length; ++i)
                    {
                        UserValue v = valueMembers[i].Load(mreader, debugger, name);
                        if (v == null)
                            return null;
                        values[i] = v.Value;
                    }
                    result.Values = values;
                }

                return result;
            }

            public UserSimpleGeometryMembers LoadParsed(Debugger debugger, string name)
            {
                UserSimpleGeometryMembers result = new UserSimpleGeometryMembers();

                if (pointMembers != null)
                {
                    Geometry.Point[] points = new Geometry.Point[pointMembers.Length];
                    for (int i = 0; i < pointMembers.Length; ++i)
                    {
                        points[i] = pointMembers[i].LoadParsed(debugger, name);
                        if (points[i] == null)
                            return null;
                    }
                    result.Points = points;
                }

                if (valueMembers != null)
                {
                    double[] values = new double[valueMembers.Length];
                    for (int i = 0; i < valueMembers.Length; ++i)
                    {
                        UserValue v = valueMembers[i].LoadParsed(debugger, name);
                        if (v == null)
                            return null;
                        values[i] = v.Value;
                    }
                    result.Values = values;
                }

                return result;
            }

            public UserSimpleGeometryMembers LoadMemory(MemoryReader mreader, Debugger debugger,
                                                        string name)
            {
                UserSimpleGeometryMembers result = new UserSimpleGeometryMembers();

                if (pointMembers != null)
                {
                    Geometry.Point[] points = new Geometry.Point[pointMembers.Length];
                    for (int i = 0; i < pointMembers.Length; ++i)
                    {
                        points[i] = pointMembers[i].LoadMemory(mreader, debugger, name);
                        if (points[i] == null)
                            return null;
                    }
                    result.Points = points;
                }

                if (valueMembers != null)
                {
                    double[] values = new double[valueMembers.Length];
                    for (int i = 0; i < valueMembers.Length; ++i)
                    {
                        UserValue v = valueMembers[i].LoadMemory(mreader, debugger, name);
                        if (v == null)
                            return null;
                        values[i] = v.Value;
                    }
                    result.Values = values;
                }

                return result;
            }

            public MemoryReader.Converter<double> GetMemoryConverter(MemoryReader mreader,
                                                                     Debugger debugger,
                                                                     string name, string type)
            {
                int pLength = pointMembers != null ? pointMembers.Length : 0;
                int vLength = valueMembers != null ? valueMembers.Length : 0;
                int length = pLength + vLength;
                MemoryReader.Member<double>[] converters = new MemoryReader.Member<double>[length];
                for (int i = 0; i < pLength; ++i)
                {
                    converters[i] = pointMembers[i].GetMemoryConverter(mreader, debugger, name);
                    if (converters[i] == null)
                        return null;
                }
                for (int i = 0; i < vLength; ++i)
                {
                    int j = i + pLength;
                    converters[j] = valueMembers[i].GetMemoryConverter(mreader, debugger, name);
                    if (converters[i] == null)
                        return null;
                }
                return length > 0
                     ? new MemoryReader.StructConverter<double>(sizeOf, converters)
                     : null;
            }

            public bool IsValid;

            readonly UserPointMember[] pointMembers;
            readonly UserValueMember[] valueMembers;
            readonly Geometry.Traits traits;
            // For memory loading:
            readonly int sizeOf;
        }

        class UserPoint : PointLoader
        {
            public class LoaderCreator : ExpressionLoader.ILoaderCreator
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

                    var exprs = new ClassScopeExpression[] { exprX.DeepCopy(), exprY.DeepCopy() };
                    UserSimpleGeometry simpleGeometry = new UserSimpleGeometry(loaders, debugger,
                                                                name, type,
                                                                null,
                                                                exprs,
                                                                new Geometry.Traits(2, cs, unit));

                    return simpleGeometry.IsValid
                         ? new UserPoint(simpleGeometry)
                         : null;
                }

                readonly ITypeMatcher typeMatcher;
                readonly ClassScopeExpression exprX;
                readonly ClassScopeExpression exprY;
                readonly Geometry.CoordinateSystem cs;
                readonly Geometry.Unit unit;
            }

            private UserPoint(UserSimpleGeometry simpleGeometry)
            {
                this.simpleGeometry = simpleGeometry;
            }

            public override Geometry.Traits GetTraits(MemoryReader mreader, Debugger debugger, string name)
            {
                return simpleGeometry.GetTraits(mreader, debugger, name);
            }

            public override ExpressionDrawer.Point LoadPointParsed(Debugger debugger, string name, string type)
            {
                UserSimpleGeometryMembers members = simpleGeometry.LoadParsed(debugger, name);
                return members != null
                     ? new ExpressionDrawer.Point(members.Values[0], members.Values[1])
                     : null;
            }

            public override ExpressionDrawer.Point LoadPointMemory(MemoryReader mreader, Debugger debugger,
                                                                   string name, string type)
            {
                UserSimpleGeometryMembers members = simpleGeometry.LoadMemory(mreader, debugger, name);
                return members != null
                     ? new ExpressionDrawer.Point(members.Values[0], members.Values[1])
                     : null;
            }

            public override MemoryReader.Converter<double> GetMemoryConverter(MemoryReader mreader,
                                                                              Debugger debugger,
                                                                              string name, string type)
            {
                return simpleGeometry.GetMemoryConverter(mreader, debugger, name, type);
            }

            readonly UserSimpleGeometry simpleGeometry;
        }

        class UserBoxPoints : BoxLoader
        {
            public class LoaderCreator : ExpressionLoader.ILoaderCreator
            {
                public LoaderCreator(ITypeMatcher typeMatcher,
                                     ClassScopeExpression exprMin,
                                     ClassScopeExpression exprMax)
                {
                    this.typeMatcher = typeMatcher;
                    this.exprMin = exprMin;
                    this.exprMax = exprMax;
                }

                public bool IsUserDefined() { return true; }
                public Kind Kind() { return ExpressionLoader.Kind.Box; }
                public Loader Create(Loaders loaders, Debugger debugger, string name, string type, string id)
                {
                    if (!typeMatcher.MatchType(type, id))
                        return null;

                    var exprs = new ClassScopeExpression[] { exprMin.DeepCopy(), exprMax.DeepCopy() };
                    UserSimpleGeometry simpleGeometry = new UserSimpleGeometry(loaders, debugger,
                                                                name, type,
                                                                exprs, null, null);

                    return simpleGeometry.IsValid
                         ? new UserBoxPoints(simpleGeometry)
                         : null;
                }

                readonly ITypeMatcher typeMatcher;
                readonly ClassScopeExpression exprMin;
                readonly ClassScopeExpression exprMax;
            }

            private UserBoxPoints(UserSimpleGeometry simpleGeometry)
            {
                this.simpleGeometry = simpleGeometry;
            }

            public override Geometry.Traits GetTraits(MemoryReader mreader, Debugger debugger,
                                                      string name)
            {
                return simpleGeometry.GetTraits(mreader, debugger, name);
            }

            public override ExpressionDrawer.IDrawable Load(MemoryReader mreader, Debugger debugger,
                                                            string name, string type,
                                                            LoadCallback callback) // dummy callback
            {
                UserSimpleGeometryMembers members = simpleGeometry.Load(mreader, debugger, name);
                return members != null
                     ? new ExpressionDrawer.Box(members.Points[0], members.Points[1])
                     : null;
            }


            public override MemoryReader.Converter<double> GetMemoryConverter(MemoryReader mreader,
                                                                              Debugger debugger, // TODO - remove
                                                                              string name, string type)
            {
                return simpleGeometry.GetMemoryConverter(mreader, debugger, name, type);
            }

            readonly UserSimpleGeometry simpleGeometry;
        }

        class UserBoxCoords : BoxLoader
        {
            public enum CoordsX { LR, LW, WR };
            public enum CoordsY { BT, BH, HT };

            public class LoaderCreator : ExpressionLoader.ILoaderCreator
            {
                public LoaderCreator(ITypeMatcher typeMatcher,
                                     ClassScopeExpression exprX1, // left  left  right
                                     ClassScopeExpression exprX2, // right width width
                                     CoordsX coordsX,
                                     ClassScopeExpression exprY1, // bottom bottom top
                                     ClassScopeExpression exprY2, // top    height height
                                     CoordsY coordsY,
                                     Geometry.CoordinateSystem cs,
                                     Geometry.Unit unit)
                {
                    this.typeMatcher = typeMatcher;
                    this.exprX1 = exprX1;
                    this.exprX2 = exprX2;
                    this.exprY1 = exprY1;
                    this.exprY2 = exprY2;
                    this.coordsX = coordsX;
                    this.coordsY = coordsY;
                    this.traits = new Geometry.Traits(2, cs, unit);
                    
                }

                public bool IsUserDefined() { return true; }
                public Kind Kind() { return ExpressionLoader.Kind.Box; }
                public Loader Create(Loaders loaders, Debugger debugger, string name, string type, string id)
                {
                    if (!typeMatcher.MatchType(type, id))
                        return null;

                    var exprs = new ClassScopeExpression[] { exprX1.DeepCopy(),
                                                             exprY1.DeepCopy(),
                                                             exprX2.DeepCopy(),
                                                             exprY2.DeepCopy() };
                    UserSimpleGeometry simpleGeometry = new UserSimpleGeometry(loaders, debugger,
                                                                name, type,
                                                                null, exprs, traits);

                    return simpleGeometry.IsValid
                         ? new UserBoxCoords(simpleGeometry, coordsX, coordsY)
                         : null;
                }

                readonly ITypeMatcher typeMatcher;
                readonly ClassScopeExpression exprX1;
                readonly ClassScopeExpression exprX2;
                readonly ClassScopeExpression exprY1;
                readonly ClassScopeExpression exprY2;
                readonly CoordsX coordsX;
                readonly CoordsY coordsY;
                readonly Geometry.Traits traits;
            }

            private UserBoxCoords(UserSimpleGeometry simpleGeometry,
                                  CoordsX coordsX,
                                  CoordsY coordsY)
            {
                this.simpleGeometry = simpleGeometry;
                this.coordsX = coordsX;
                this.coordsY = coordsY;
            }

            public override Geometry.Traits GetTraits(MemoryReader mreader, Debugger debugger,
                                                      string name)
            {
                return simpleGeometry.GetTraits(mreader, debugger, name);
            }

            public override ExpressionDrawer.IDrawable Load(MemoryReader mreader, Debugger debugger,
                                                            string name, string type,
                                                            LoadCallback callback) // dummy callback
            {
                UserSimpleGeometryMembers members = simpleGeometry.Load(mreader, debugger, name);
                if (members == null)
                    return null;

                double x1 = members.Values[0];
                double y1 = members.Values[1];
                double x2 = members.Values[2];
                double y2 = members.Values[3];
                
                ExpressionDrawer.Point minP = new ExpressionDrawer.Point(x1, y1); // LB
                ExpressionDrawer.Point maxP = new ExpressionDrawer.Point(x2, y2); // RT
                if (coordsX == CoordsX.LW)
                    maxP[0] = x1 + x2; // r = l + w
                else if (coordsX == CoordsX.WR)
                    minP[0] = x2 - x1; // l = r - w
                if (coordsY == CoordsY.BH)
                    maxP[1] = y1 + y2; // t = b + h
                else if (coordsY == CoordsY.HT)
                    minP[1] = y2 - y1; // b = t - h

                return new ExpressionDrawer.Box(minP, maxP);
            }

            public override MemoryReader.Converter<double> GetMemoryConverter(MemoryReader mreader,
                                                                              Debugger debugger, // TODO - remove
                                                                              string name, string type)
            {
                MemoryReader.Converter<double> converter = simpleGeometry.GetMemoryConverter(mreader, debugger, name, type);
                if (converter == null)
                    return null;

                return new MemoryReader.TransformingConverter<double>(
                            converter,
                            delegate(double[] values, int offset)
                            {
                                // convert to LBRT
                                if (coordsX == CoordsX.LW) // LXWX
                                    values[offset + 2] = values[offset + 0]
                                                       + values[offset + 2]; // r = l + w
                                else if (coordsX == CoordsX.WR) // WXRX
                                    values[offset + 0] = values[offset + 2]
                                                       - values[offset + 0]; // l = r - w
                                if (coordsY == CoordsY.BH) // XBXH
                                    values[offset + 3] = values[offset + 1]
                                                       + values[offset + 3]; // t = b + h
                                else if (coordsY == CoordsY.HT) // XHXT
                                    values[offset + 1] = values[offset + 3]
                                                       - values[offset + 1]; // b = t - h
                            });
            }

            readonly UserSimpleGeometry simpleGeometry;
            readonly CoordsX coordsX;
            readonly CoordsY coordsY;
        }

        class UserSegmentCoords : SegmentLoader
        {
            public class LoaderCreator : ExpressionLoader.ILoaderCreator
            {
                public LoaderCreator(ITypeMatcher typeMatcher,
                                     ClassScopeExpression exprFirstX,
                                     ClassScopeExpression exprFirstY,
                                     ClassScopeExpression exprSecondX,
                                     ClassScopeExpression exprSecondY,
                                     Geometry.CoordinateSystem cs,
                                     Geometry.Unit unit)
                {
                    this.typeMatcher = typeMatcher;
                    this.exprFirstX = exprFirstX;
                    this.exprFirstY = exprFirstY;
                    this.exprSecondX = exprSecondX;
                    this.exprSecondY = exprSecondY;
                    this.traits = new Geometry.Traits(2, cs, unit);
                }

                public bool IsUserDefined() { return true; }
                public Kind Kind() { return ExpressionLoader.Kind.Segment; }
                public Loader Create(Loaders loaders, Debugger debugger, string name, string type, string id)
                {
                    if (!typeMatcher.MatchType(type, id))
                        return null;

                    var exprs = new ClassScopeExpression[] { exprFirstX.DeepCopy(),
                                                             exprFirstY.DeepCopy(),
                                                             exprSecondX.DeepCopy(),
                                                             exprSecondY.DeepCopy() };
                    UserSimpleGeometry simpleGeometry = new UserSimpleGeometry(loaders, debugger,
                                                                name, type,
                                                                null, exprs, traits);
                    return simpleGeometry.IsValid
                         ? new UserSegmentCoords(simpleGeometry)
                         : null;
                }

                readonly ITypeMatcher typeMatcher;
                readonly ClassScopeExpression exprFirstX;
                readonly ClassScopeExpression exprFirstY;
                readonly ClassScopeExpression exprSecondX;
                readonly ClassScopeExpression exprSecondY;
                readonly Geometry.Traits traits;
            }

            private UserSegmentCoords(UserSimpleGeometry simpleGeometry)
            {
                this.simpleGeometry = simpleGeometry;
            }

            public override Geometry.Traits GetTraits(MemoryReader mreader, Debugger debugger,
                                                      string name)
            {
                return simpleGeometry.GetTraits(mreader, debugger, name);
            }

            public override ExpressionDrawer.IDrawable Load(MemoryReader mreader, Debugger debugger,
                                                            string name, string type,
                                                            LoadCallback callback) // dummy callback
            {
                UserSimpleGeometryMembers members = simpleGeometry.Load(mreader, debugger, name);
                if (members == null)
                    return null;
                ExpressionDrawer.Point first = new ExpressionDrawer.Point(members.Values[0],
                                                                          members.Values[1]);
                ExpressionDrawer.Point second = new ExpressionDrawer.Point(members.Values[2],
                                                                           members.Values[3]);
                return new ExpressionDrawer.Segment(first, second);
            }

            public override MemoryReader.Converter<double> GetMemoryConverter(MemoryReader mreader,
                                                                              Debugger debugger, // TODO - remove
                                                                              string name, string type)
            {
                return simpleGeometry.GetMemoryConverter(mreader, debugger, name, type);
            }

            readonly UserSimpleGeometry simpleGeometry;
        }

        class UserRayCoords : RayLoader
        {
            public class LoaderCreator : ExpressionLoader.ILoaderCreator
            {
                public LoaderCreator(ITypeMatcher typeMatcher,
                                     ClassScopeExpression exprOrigX,
                                     ClassScopeExpression exprOrigY,
                                     ClassScopeExpression exprDirX,
                                     ClassScopeExpression exprDirY)
                {
                    this.typeMatcher = typeMatcher;
                    this.exprOrigX = exprOrigX;
                    this.exprOrigY = exprOrigY;
                    this.exprDirX = exprDirX;
                    this.exprDirY = exprDirY;
                    this.traits = new Geometry.Traits(2, Geometry.CoordinateSystem.Cartesian, Geometry.Unit.None);
                }

                public bool IsUserDefined() { return true; }
                public Kind Kind() { return ExpressionLoader.Kind.Ray; }
                public Loader Create(Loaders loaders, Debugger debugger, string name, string type, string id)
                {
                    if (!typeMatcher.MatchType(type, id))
                        return null;

                    var exprs = new ClassScopeExpression[] { exprOrigX.DeepCopy(),
                                                             exprOrigY.DeepCopy(),
                                                             exprDirX.DeepCopy(),
                                                             exprDirY.DeepCopy() };
                    UserSimpleGeometry simpleGeometry = new UserSimpleGeometry(loaders, debugger,
                                                                name, type,
                                                                null, exprs, traits);
                    return simpleGeometry.IsValid
                         ? new UserRayCoords(simpleGeometry)
                         : null;
                }

                readonly ITypeMatcher typeMatcher;
                readonly ClassScopeExpression exprOrigX;
                readonly ClassScopeExpression exprOrigY;
                readonly ClassScopeExpression exprDirX;
                readonly ClassScopeExpression exprDirY;
                readonly Geometry.Traits traits;
            }

            private UserRayCoords(UserSimpleGeometry simpleGeometry)
            {
                this.simpleGeometry = simpleGeometry;
            }

            public override Geometry.Traits GetTraits(MemoryReader mreader, Debugger debugger,
                                                      string name)
            {
                return simpleGeometry.GetTraits(mreader, debugger, name);
            }

            public override ExpressionDrawer.IDrawable Load(MemoryReader mreader, Debugger debugger,
                                                            string name, string type,
                                                            LoadCallback callback) // dummy callback
            {
                UserSimpleGeometryMembers members = simpleGeometry.Load(mreader, debugger, name);
                if (members == null)
                    return null;
                ExpressionDrawer.Point orig = new ExpressionDrawer.Point(members.Values[0],
                                                                         members.Values[1]);
                ExpressionDrawer.Point dir = new ExpressionDrawer.Point(members.Values[2],
                                                                        members.Values[3]);
                return new ExpressionDrawer.Ray(orig, dir);
            }

            public override MemoryReader.Converter<double> GetMemoryConverter(MemoryReader mreader,
                                                                              Debugger debugger, // TODO - remove
                                                                              string name, string type)
            {
                return simpleGeometry.GetMemoryConverter(mreader, debugger, name, type);
            }

            readonly UserSimpleGeometry simpleGeometry;
        }

        class UserLineCoords : LineLoader
        {
            public class LoaderCreator : ExpressionLoader.ILoaderCreator
            {
                public LoaderCreator(ITypeMatcher typeMatcher,
                                     ClassScopeExpression exprFirstX,
                                     ClassScopeExpression exprFirstY,
                                     ClassScopeExpression exprSecondX,
                                     ClassScopeExpression exprSecondY)
                {
                    this.typeMatcher = typeMatcher;
                    this.exprFirstX = exprFirstX;
                    this.exprFirstY = exprFirstY;
                    this.exprSecondX = exprSecondX;
                    this.exprSecondY = exprSecondY;
                    this.traits = new Geometry.Traits(2, Geometry.CoordinateSystem.Cartesian, Geometry.Unit.None);
                }

                public bool IsUserDefined() { return true; }
                public Kind Kind() { return ExpressionLoader.Kind.Line; }
                public Loader Create(Loaders loaders, Debugger debugger, string name, string type, string id)
                {
                    if (!typeMatcher.MatchType(type, id))
                        return null;

                    var exprs = new ClassScopeExpression[] { exprFirstX.DeepCopy(),
                                                             exprFirstY.DeepCopy(),
                                                             exprSecondX.DeepCopy(),
                                                             exprSecondY.DeepCopy() };
                    UserSimpleGeometry simpleGeometry = new UserSimpleGeometry(loaders, debugger,
                                                                name, type,
                                                                null, exprs, traits);
                    return simpleGeometry.IsValid
                         ? new UserLineCoords(simpleGeometry)
                         : null;
                }

                readonly ITypeMatcher typeMatcher;
                readonly ClassScopeExpression exprFirstX;
                readonly ClassScopeExpression exprFirstY;
                readonly ClassScopeExpression exprSecondX;
                readonly ClassScopeExpression exprSecondY;
                readonly Geometry.Traits traits;
            }

            private UserLineCoords(UserSimpleGeometry simpleGeometry)
            {
                this.simpleGeometry = simpleGeometry;
            }

            public override Geometry.Traits GetTraits(MemoryReader mreader, Debugger debugger,
                                                      string name)
            {
                return simpleGeometry.GetTraits(mreader, debugger, name);
            }

            public override ExpressionDrawer.IDrawable Load(MemoryReader mreader, Debugger debugger,
                                                            string name, string type,
                                                            LoadCallback callback) // dummy callback
            {
                UserSimpleGeometryMembers members = simpleGeometry.Load(mreader, debugger, name);
                if (members == null)
                    return null;
                ExpressionDrawer.Point first = new ExpressionDrawer.Point(members.Values[0],
                                                                          members.Values[1]);
                ExpressionDrawer.Point second = new ExpressionDrawer.Point(members.Values[2],
                                                                           members.Values[3]);
                return new ExpressionDrawer.Line(first, second);
            }

            public override MemoryReader.Converter<double> GetMemoryConverter(MemoryReader mreader,
                                                                              Debugger debugger, // TODO - remove
                                                                              string name, string type)
            {
                return simpleGeometry.GetMemoryConverter(mreader, debugger, name, type);
            }

            readonly UserSimpleGeometry simpleGeometry;
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
                string containerType = debugger.GetValueType(containerName);
                if (containerType == null)
                    return null;

                ContainerLoader containerLoader = loaders.FindByType(ExpressionLoader.Kind.Container, containerName, containerType) as ContainerLoader;
                if (containerLoader == null)
                    return null;

                containerLoader.ElementInfo(containerName, containerType,
                                            out string elementName, out string elementType);
                ElementLoader elementLoader = loaders.FindByType(elementKindConstraint,
                                                                 elementName,
                                                                 elementType) as ElementLoader;
                if (elementLoader == null)
                    return null;

                UserContainerLoaders<ElementLoader> result = new UserContainerLoaders<ElementLoader>
                {
                    ContainerLoader = containerLoader,
                    ElementLoader = elementLoader,
                    ContainerNameExpr = exprContainerName.DeepCopy(),
                    ContainerType = containerType,
                    ElementType = elementType
                };
                return result;
            }

            readonly ClassScopeExpression exprContainerName;
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

                containerLoader.ElementInfo(name, type,
                                            out string elementName, out string elementType);
                ElementLoader elementLoader = loaders.FindByType(elementKindConstraint,
                                                                 elementName,
                                                                 elementType) as ElementLoader;
                if (elementLoader == null)
                    return null;

                UserContainerLoaders<ElementLoader> result = new UserContainerLoaders<ElementLoader>
                {
                    ContainerLoader = containerLoader,
                    ElementLoader = elementLoader,
                    ContainerNameExpr = new ClassScopeExpression(new ClassScopeExpression.NamePart()),
                    ContainerType = type,
                    ElementType = elementType
                };
                return result;
            }

            readonly ClassScopeExpression exprPointer;
            readonly ClassScopeExpression exprSize;
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

                containerLoader.ElementInfo(name, type,
                                            out string elementName, out string elementType);
                ElementLoader elementLoader = loaders.FindByType(elementKindConstraint,
                                                                 elementName,
                                                                 elementType) as ElementLoader;
                if (elementLoader == null)
                    return null;

                UserContainerLoaders<ElementLoader> result = new UserContainerLoaders<ElementLoader>
                {
                    ContainerLoader = containerLoader,
                    ElementLoader = elementLoader,
                    ContainerNameExpr = new ClassScopeExpression(new ClassScopeExpression.NamePart()),
                    ContainerType = type,
                    ElementType = elementType
                };
                return result;
            }

            readonly ClassScopeExpression exprHeadPointer;
            readonly ClassScopeExpression exprNextPointer;
            readonly ClassScopeExpression exprValue;
            readonly ClassScopeExpression exprSize;
        }

        class UserPointRange<ResultType> : PointRange<ResultType>
            where ResultType : class
                             , ExpressionDrawer.IDrawable
                             , Geometry.IContainer<Geometry.Point>
                             , new()
        {
            public class LoaderCreator : ExpressionLoader.ILoaderCreator
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

                readonly ExpressionLoader.Kind kind;
                readonly ITypeMatcher typeMatcher;
                readonly IUserContainerEntry containerEntry;
                readonly DerivedConstructor derivedConstructor;
            }

            protected UserPointRange(UserContainerLoaders<PointLoader> containerLoaders)
            {
                this.containerLoaders = containerLoaders;
            }

            public override Geometry.Traits GetTraits(MemoryReader mreader, Debugger debugger,
                                                      string name)
            {
                return containerLoaders.ElementLoader.GetTraits(mreader, debugger, name);
            }

            public override ExpressionDrawer.IDrawable Load(MemoryReader mreader, Debugger debugger,
                                                            string name, string type,
                                                            LoadCallback callback)
            {
                ResultType result = null;

                string containerName = containerLoaders.ContainerNameExpr.GetString(name);
                if (mreader != null)
                {
                    containerLoaders.ContainerLoader.ElementInfo(containerName,
                                                                 containerLoaders.ContainerType,
                                                                 out string elementName, out string elementType);

                    result = LoadMemory(mreader, debugger,
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

                return result;
            }

            readonly UserContainerLoaders<PointLoader> containerLoaders;
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
            public class LoaderCreator : ExpressionLoader.ILoaderCreator
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

                readonly ITypeMatcher typeMatcher;
                readonly IUserContainerEntry containerEntry;
            }

            private UserMultiLinestring(UserContainerLoaders<RangeLoader<ExpressionDrawer.Linestring>> containerLoaders)
            {
                this.containerLoaders = containerLoaders;
            }

            public override Geometry.Traits GetTraits(MemoryReader mreader, Debugger debugger,
                                                      string name)
            {
                return containerLoaders.ElementLoader.GetTraits(mreader, debugger, name);
            }

            public override ExpressionDrawer.IDrawable Load(MemoryReader mreader, Debugger debugger,
                                                            string name, string type,
                                                            LoadCallback callback)
            {
                ExpressionDrawer.MultiLinestring mls = new ExpressionDrawer.MultiLinestring();
                string containerName = containerLoaders.ContainerNameExpr.GetString(name);
                bool ok = containerLoaders.ContainerLoader.ForEachElement(
                    debugger, containerName,
                    delegate (string elName)
                    {
                        ExpressionDrawer.Linestring ls = containerLoaders.ElementLoader.Load(
                                                            mreader, debugger,
                                                            elName, containerLoaders.ElementType,
                                                            callback) as ExpressionDrawer.Linestring;
                        if (ls == null)
                            return false;
                        mls.Add(ls);
                        //return callback();
                        return true;
                    });
                return ok ? mls : null;
            }

            readonly UserContainerLoaders<RangeLoader<ExpressionDrawer.Linestring>> containerLoaders;
        }

        class UserPolygon : PolygonLoader
        {
            public class LoaderCreator : ExpressionLoader.ILoaderCreator
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
                    string outerType = debugger.GetValueType(outerName);
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

                readonly ITypeMatcher typeMatcher;
                readonly ClassScopeExpression exprOuter;
                readonly IUserContainerEntry innersContEntry;
                readonly int innersOffset;
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

            public override Geometry.Traits GetTraits(MemoryReader mreader, Debugger debugger,
                                                      string name)
            {
                return outerLoader.GetTraits(mreader, debugger, name);
            }

            public override ExpressionDrawer.IDrawable Load(MemoryReader mreader, Debugger debugger,
                                                            string name, string type,
                                                            LoadCallback callback)
            {
                ExpressionDrawer.Polygon result = null;

                string outerName = exprOuter.GetString(name);
                //string outerType = ExpressionParser.GetValueType(debugger, outerName);
                ExpressionDrawer.Ring outer = outerLoader.Load(mreader, debugger, outerName, outerType,
                                                               callback) as ExpressionDrawer.Ring;
                if (outer == null)
                    return null;

                // If there is no definition of inner rings, return
                if (innersLoaders == null)
                {
                    result = new ExpressionDrawer.Polygon(outer, new List<Geometry.Ring>());
                    return null;
                }

                // However if inner rings are defined then load them and return
                // them only if they are properly loaded.
                int i = 0;
                List<Geometry.Ring> inners = new List<Geometry.Ring>();
                string innersName = innersLoaders.ContainerNameExpr.GetString(name);
                bool ok = innersLoaders.ContainerLoader.ForEachElement(
                    debugger, innersName,
                    delegate (string elName)
                    {
                        if (i++ < innersOffset)
                            return true;

                        ExpressionDrawer.Ring inner = innersLoaders.ElementLoader.Load(
                                                         mreader, debugger,
                                                         elName, innersLoaders.ElementType,
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

            readonly ClassScopeExpression exprOuter;
            readonly string outerType;
            readonly LoaderR<ExpressionDrawer.Ring> outerLoader;
            readonly UserContainerLoaders<LoaderR<ExpressionDrawer.Ring>> innersLoaders;
            readonly int innersOffset;
        }

        class UserMultiPolygon : RangeLoader<ExpressionDrawer.MultiPolygon>
        {
            public class LoaderCreator : ExpressionLoader.ILoaderCreator
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

                readonly ITypeMatcher typeMatcher;
                readonly IUserContainerEntry containerEntry;
            }

            public UserMultiPolygon(UserContainerLoaders<PolygonLoader> containerLoaders)
            {
                this.containerLoaders = containerLoaders;
            }

            public override Geometry.Traits GetTraits(MemoryReader mreader, Debugger debugger,
                                                      string name)
            {
                return containerLoaders.ElementLoader.GetTraits(mreader, debugger, name);
            }

            public override ExpressionDrawer.IDrawable Load(MemoryReader mreader, Debugger debugger,
                                                            string name, string type,
                                                            LoadCallback callback)
            {
                ExpressionDrawer.MultiPolygon mpoly = new ExpressionDrawer.MultiPolygon();
                string containerName = containerLoaders.ContainerNameExpr.GetString(name);
                bool ok = containerLoaders.ContainerLoader.ForEachElement(
                    debugger, containerName,
                    delegate (string elName)
                    {
                        ExpressionDrawer.Polygon poly = containerLoaders.ElementLoader.Load(
                                                            mreader, debugger,
                                                            elName, containerLoaders.ElementType,
                                                            callback) as ExpressionDrawer.Polygon;
                        if (poly == null)
                            return false;
                        mpoly.Add(poly);
                        //return callback();
                        return true;
                    });
                return ok ? mpoly : null;
            }

            readonly UserContainerLoaders<PolygonLoader> containerLoaders;
        }

        // TODO: If possible use one implementation for MultiLinestring, MultiPolygon and MultiGeometry
        class UserMultiGeometry : RangeLoader<ExpressionDrawer.DrawablesContainer>
        {
            public class LoaderCreator : ExpressionLoader.ILoaderCreator
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

                readonly ITypeMatcher typeMatcher;
                readonly IUserContainerEntry containerEntry;
            }

            public UserMultiGeometry(UserContainerLoaders<DrawableLoader> containerLoaders)
            {
                this.containerLoaders = containerLoaders;
            }

            public override Geometry.Traits GetTraits(MemoryReader mreader, Debugger debugger,
                                                      string name)
            {
                return containerLoaders.ElementLoader.GetTraits(mreader, debugger, name);
            }

            public override ExpressionDrawer.IDrawable Load(MemoryReader mreader, Debugger debugger,
                                                            string name, string type,
                                                            LoadCallback callback)
            {
                ExpressionDrawer.DrawablesContainer drawables = new ExpressionDrawer.DrawablesContainer();
                string containerName = containerLoaders.ContainerNameExpr.GetString(name);
                bool ok = containerLoaders.ContainerLoader.ForEachElement(
                    debugger, containerName,
                    delegate (string elName)
                    {
                        ExpressionDrawer.IDrawable drawable = containerLoaders.ElementLoader.Load(
                                                                mreader, debugger,
                                                                elName, containerLoaders.ElementType,
                                                                callback);
                        if (drawable == null)
                            return false;
                        drawables.Add(drawable);
                        //return callback();
                        return true;
                    });
                return ok ? drawables : null;
            }

            readonly UserContainerLoaders<DrawableLoader> containerLoaders;
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
                                    GetCSAndUnit(elEntry, out Geometry.CoordinateSystem cs, out Geometry.Unit unit);

                                    ClassScopeExpression exprX = new ClassScopeExpression(elX.InnerText);
                                    ClassScopeExpression exprY = new ClassScopeExpression(elY.InnerText);
                                    loaders.Add(new UserPoint.LoaderCreator(
                                                    typeMatcher, exprX, exprY, cs, unit));
                                }
                            }
                        }
                        else if (elEntry.Name == "Box")
                        {
                            var elPoints = Util.GetXmlElementByTagName(elEntry, "Points");
                            var elCoordinates = Util.GetXmlElementByTagName(elEntry, "Coordinates");
                            var elCoordinatesDimensions = Util.GetXmlElementByTagName(elEntry, "CoordinatesDimensions");
                            if (elPoints != null)
                            {
                                var elMin = Util.GetXmlElementByTagName(elPoints, "Min");
                                var elMax = Util.GetXmlElementByTagName(elPoints, "Max");
                                if (elMin != null && elMax != null)
                                {
                                    ClassScopeExpression exprMin = new ClassScopeExpression(elMin.InnerText);
                                    ClassScopeExpression exprMax = new ClassScopeExpression(elMax.InnerText);
                                    loaders.Add(new UserBoxPoints.LoaderCreator(
                                                    typeMatcher, exprMin, exprMax));
                                }
                            }
                            else if (elCoordinates != null || elCoordinatesDimensions != null)
                            {
                                GetCSAndUnit(elEntry, out Geometry.CoordinateSystem cs, out Geometry.Unit unit);

                                var elParent = elCoordinates ?? elCoordinatesDimensions;

                                var elLeft = Util.GetXmlElementByTagName(elParent, "MinX");
                                var elBottom = Util.GetXmlElementByTagName(elParent, "MinY");
                                var elRight = Util.GetXmlElementByTagName(elParent, "MaxX");
                                var elTop = Util.GetXmlElementByTagName(elParent, "MaxY");
                                if (elLeft == null)
                                    elLeft = Util.GetXmlElementByTagName(elParent, "Left");
                                if (elBottom == null)
                                    elBottom = Util.GetXmlElementByTagName(elParent, "Bottom");
                                if (elRight == null)
                                    elRight = Util.GetXmlElementByTagName(elParent, "Right");
                                if (elTop == null)
                                    elTop = Util.GetXmlElementByTagName(elParent, "Top");
                                var elWidth = Util.GetXmlElementByTagName(elParent, "Width");
                                var elHeight = Util.GetXmlElementByTagName(elParent, "Height");

                                string exprX1 = null;
                                string exprX2 = null;
                                string exprY1 = null;
                                string exprY2 = null;
                                UserBoxCoords.CoordsX coordsX = UserBoxCoords.CoordsX.LR;
                                UserBoxCoords.CoordsY coordsY = UserBoxCoords.CoordsY.BT;
                                if (elCoordinates != null)
                                {
                                    if (elLeft != null && elRight != null)
                                    {
                                        exprX1 = elLeft.InnerText;
                                        exprX2 = elRight.InnerText;
                                        coordsX = UserBoxCoords.CoordsX.LR;
                                    }

                                    if (elBottom != null && elTop != null)
                                    {
                                        exprY1 = elBottom.InnerText;
                                        exprY2 = elTop.InnerText;
                                        coordsY = UserBoxCoords.CoordsY.BT;
                                    }
                                }
                                else // elCoordinatesDimensions != null
                                {
                                    if (elLeft != null && elWidth != null)
                                    {
                                        exprX1 = elLeft.InnerText;
                                        exprX2 = elWidth.InnerText;
                                        coordsX = UserBoxCoords.CoordsX.LW;
                                    }
                                    else if (elRight != null && elWidth != null)
                                    {
                                        exprX1 = elWidth.InnerText;
                                        exprX2 = elRight.InnerText;
                                        coordsX = UserBoxCoords.CoordsX.WR;
                                    }

                                    if (elBottom != null && elHeight != null)
                                    {
                                        exprY1 = elBottom.InnerText;
                                        exprY2 = elHeight.InnerText;
                                        coordsY = UserBoxCoords.CoordsY.BH;
                                    }
                                    else if (elTop != null && elHeight != null)
                                    {
                                        exprY1 = elHeight.InnerText;
                                        exprY2 = elTop.InnerText;
                                        coordsY = UserBoxCoords.CoordsY.HT;
                                    }
                                }

                                if (exprX1 != null && exprX2 != null && exprY1 != null && exprY2 != null)
                                {
                                    loaders.Add(new UserBoxCoords.LoaderCreator(
                                                    typeMatcher,
                                                    new ClassScopeExpression(exprX1),
                                                    new ClassScopeExpression(exprX2),
                                                    coordsX,
                                                    new ClassScopeExpression(exprY1),
                                                    new ClassScopeExpression(exprY2),
                                                    coordsY,
                                                    cs,
                                                    unit));
                                }
                            }
                        }
                        else if (elEntry.Name == "Segment")
                        {
                            var elCoords = Util.GetXmlElementByTagName(elEntry, "Coordinates");
                            if (elCoords != null)
                            {
                                var firstX = Util.GetXmlElementByTagName(elCoords, "FirstX");
                                var firstY = Util.GetXmlElementByTagName(elCoords, "FirstY");
                                var secondX = Util.GetXmlElementByTagName(elCoords, "SecondX");
                                var secondY = Util.GetXmlElementByTagName(elCoords, "SecondY");
                                if (firstX != null && firstY != null && secondX != null && secondY != null)
                                {
                                    GetCSAndUnit(elEntry, out Geometry.CoordinateSystem cs, out Geometry.Unit unit);

                                    ClassScopeExpression exprFirstX = new ClassScopeExpression(firstX.InnerText);
                                    ClassScopeExpression exprFirstY = new ClassScopeExpression(firstY.InnerText);
                                    ClassScopeExpression exprSecondX = new ClassScopeExpression(secondX.InnerText);
                                    ClassScopeExpression exprSecondY = new ClassScopeExpression(secondY.InnerText);
                                    loaders.Add(new UserSegmentCoords.LoaderCreator(typeMatcher,
                                                        exprFirstX, exprFirstY, exprSecondX, exprSecondY,
                                                        cs, unit));
                                }
                            }
                        }
                        else if (elEntry.Name == "Ray")
                        {
                            var elCoords = Util.GetXmlElementByTagName(elEntry, "Coordinates");
                            if (elCoords != null)
                            {
                                var origX = Util.GetXmlElementByTagName(elCoords, "OriginX");
                                var origY = Util.GetXmlElementByTagName(elCoords, "OriginY");
                                var dirX = Util.GetXmlElementByTagName(elCoords, "DirectionX");
                                var dirY = Util.GetXmlElementByTagName(elCoords, "DirectionY");
                                if (origX != null && origY != null && dirX != null && dirY != null)
                                {
                                    ClassScopeExpression exprOrigX = new ClassScopeExpression(origX.InnerText);
                                    ClassScopeExpression exprOrigY = new ClassScopeExpression(origY.InnerText);
                                    ClassScopeExpression exprDirX = new ClassScopeExpression(dirX.InnerText);
                                    ClassScopeExpression exprDirY = new ClassScopeExpression(dirY.InnerText);
                                    loaders.Add(new UserRayCoords.LoaderCreator(typeMatcher,
                                                        exprOrigX, exprOrigY, exprDirX, exprDirY));
                                }
                            }
                        }
                        else if (elEntry.Name == "Line")
                        {
                            var elCoords = Util.GetXmlElementByTagName(elEntry, "Coordinates");
                            if (elCoords != null)
                            {
                                var firstX = Util.GetXmlElementByTagName(elCoords, "FirstX");
                                var firstY = Util.GetXmlElementByTagName(elCoords, "FirstY");
                                var secondX = Util.GetXmlElementByTagName(elCoords, "SecondX");
                                var secondY = Util.GetXmlElementByTagName(elCoords, "SecondY");
                                if (firstX != null && firstY != null && secondX != null && secondY != null)
                                {
                                    ClassScopeExpression exprFirstX = new ClassScopeExpression(firstX.InnerText);
                                    ClassScopeExpression exprFirstY = new ClassScopeExpression(firstY.InnerText);
                                    ClassScopeExpression exprSecondX = new ClassScopeExpression(secondX.InnerText);
                                    ClassScopeExpression exprSecondY = new ClassScopeExpression(secondY.InnerText);
                                    loaders.Add(new UserLineCoords.LoaderCreator(typeMatcher,
                                                        exprFirstX, exprFirstY, exprSecondX, exprSecondY));
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
                                IUserContainerEntry innersContEntry = GetContainerEntry(elEntry, "InteriorRings") ?? new UserEmptyEntry();
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

            if (ReloadUserTypes(Instance.loadersCpp,
                                Instance.loadersCacheCpp,
                                options.UserTypesPathCpp,
                                options.isUserTypesPathCppChanged,
                                options.userTypesCppWriteTime,
                                out DateTime wtCpp))
            {
                options.isUserTypesPathCppChanged = false;
                options.userTypesCppWriteTime = wtCpp;
            }

            if (ReloadUserTypes(Instance.loadersCS,
                                Instance.loadersCacheCS,
                                options.UserTypesPathCS,
                                options.isUserTypesPathCSChanged,
                                options.userTypesCSWriteTime,
                                out DateTime wtCS))
            {
                options.isUserTypesPathCSChanged = false;
                options.userTypesCSWriteTime = wtCS;
            }
        }
    }
}
