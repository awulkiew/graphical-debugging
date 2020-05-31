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

                string elemName, elemType;
                ElementInfo(name, type, out elemName, out elemType);
                ulong beginAddress = ExpressionParser.GetValueAddress(debugger, elemName);
                if (beginAddress == 0)
                    return false;

                var blockConverter = new MemoryReader.ArrayConverter<T>(elementConverter, size);
                T[] values = new T[blockConverter.ValueCount()];
                if (!mreader.Read(beginAddress, values, blockConverter))
                    return false;
                return memoryBlockPredicate(values);
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

                long nextDiff = ExpressionParser.GetAddressDifference(debugger, headName, nextPointerName);
                long valDiff = ExpressionParser.GetAddressDifference(debugger, headName, valName);
                if (ExpressionParser.IsInvalidAddressDifference(nextDiff)
                 || ExpressionParser.IsInvalidAddressDifference(valDiff)
                 || nextDiff < 0 || valDiff < 0)
                    return false;

                ulong nodeAddress = ExpressionParser.GetValueAddress(debugger, headName);
                if (nodeAddress == 0)
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

            public override Geometry.Traits GetTraits(MemoryReader mreader, Debugger debugger,
                                                      string name)
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
                MemoryReader.Converter<double> converter = GetMemoryConverter(mreader, debugger,
                                                                              name, type);
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

            public override MemoryReader.Converter<double> GetMemoryConverter(MemoryReader mreader,
                                                                              Debugger debugger,
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

        class UserBoxPoints : BoxLoader
        {
            public class LoaderCreator : ExpressionLoader.LoaderCreator
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

                    exprMin.Reinitialize(debugger, name, type);
                    exprMax.Reinitialize(debugger, name, type);

                    UserBoxPoints res = new UserBoxPoints(exprMin.DeepCopy(),
                                                          exprMax.DeepCopy());

                    res.sizeOf = ExpressionParser.GetTypeSizeof(debugger, type);
                    if (ExpressionParser.IsInvalidSize(res.sizeOf))
                        //res.sizeOf = 0;
                        return null;

                    string nameMin = res.exprMin.GetString(name);
                    string nameMax = res.exprMax.GetString(name);
                    res.typeMin = ExpressionParser.GetValueType(debugger, nameMin);
                    res.typeMax = ExpressionParser.GetValueType(debugger, nameMax);
                    if (ExpressionParser.IsInvalidType(res.typeMin, res.typeMax))
                        //res.sizeOf = 0;
                        return null;

                    res.loaderMin = loaders.FindByType(ExpressionLoader.Kind.Point, nameMin, res.typeMin) as PointLoader;
                    res.loaderMax = loaders.FindByType(ExpressionLoader.Kind.Point, nameMax, res.typeMax) as PointLoader;
                    if (res.loaderMin == null || res.loaderMax == null)
                        return null;

                    res.offsetMin = ExpressionParser.GetAddressDifference(debugger, name, nameMin);
                    res.offsetMax = ExpressionParser.GetAddressDifference(debugger, name, nameMax);
                    // offsetX + sizeX > sizeOf
                    // offsetY + sizeY > sizeOf
                    if (ExpressionParser.IsInvalidOffset(res.sizeOf, res.offsetMin, res.offsetMax))
                        //res.sizeOf = 0;
                        return null;

                    return res;
                }

                ITypeMatcher typeMatcher;
                ClassScopeExpression exprMin;
                ClassScopeExpression exprMax;
            }

            private UserBoxPoints(ClassScopeExpression exprMin,
                                  ClassScopeExpression exprMax)
            {
                this.exprMin = exprMin;
                this.exprMax = exprMax;

                this.typeMin = null;
                this.typeMax = null;
                this.sizeOf = 0;
            }

            public override Geometry.Traits GetTraits(MemoryReader mreader, Debugger debugger,
                                                      string name)
            {
                return loaderMin.GetTraits(mreader, debugger, name);
            }

            public override ExpressionDrawer.IDrawable Load(MemoryReader mreader, Debugger debugger,
                                                            string name, string type,
                                                            LoadCallback callback) // dummy callback
            {
                string nameMin = exprMin.GetString(name);
                string nameMax = exprMax.GetString(name);

                Geometry.Point fp = loaderMin.LoadPoint(mreader, debugger, nameMin, typeMin);
                Geometry.Point sp = loaderMax.LoadPoint(mreader, debugger, nameMax, typeMax);

                return Util.IsOk(fp, sp)
                     ? new ExpressionDrawer.Box(fp, sp)
                     : null;
            }


            public override MemoryReader.Converter<double> GetMemoryConverter(MemoryReader mreader,
                                                                              Debugger debugger, // TODO - remove
                                                                              string name, string type)
            {
                string nameMin = exprMin.GetString(name);
                string nameMax = exprMax.GetString(name);

                MemoryReader.Converter<double> minConverter = loaderMin.GetMemoryConverter(mreader, debugger, nameMin, typeMin);
                MemoryReader.Converter<double> maxConverter = loaderMax.GetMemoryConverter(mreader, debugger, nameMax, typeMax);
                if (minConverter == null || maxConverter == null)
                    return null;

                return new MemoryReader.StructConverter<double>(sizeOf,
                            new MemoryReader.Member<double>(minConverter, (int)offsetMin),
                            new MemoryReader.Member<double>(maxConverter, (int)offsetMax));
            }

            ClassScopeExpression exprMin;
            ClassScopeExpression exprMax;
            PointLoader loaderMin;
            PointLoader loaderMax;
            // For memory loading:
            string typeMin;
            string typeMax;
            long offsetMin;
            long offsetMax;
            int sizeOf;
        }

        class UserBoxCoords : BoxLoader
        {
            public enum CoordsX { LR, LW, WR };
            public enum CoordsY { BT, BH, HT };

            public class LoaderCreator : ExpressionLoader.LoaderCreator
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

                    exprX1.Reinitialize(debugger, name, type);
                    exprX2.Reinitialize(debugger, name, type);
                    exprY1.Reinitialize(debugger, name, type);
                    exprY2.Reinitialize(debugger, name, type);

                    UserBoxCoords res = new UserBoxCoords(exprX1.DeepCopy(),
                                                          exprX2.DeepCopy(),
                                                          coordsX,
                                                          exprY1.DeepCopy(),
                                                          exprY2.DeepCopy(),
                                                          coordsY,
                                                          traits);

                    res.sizeOf = ExpressionParser.GetTypeSizeof(debugger, type);
                    if (ExpressionParser.IsInvalidSize(res.sizeOf))
                        //res.sizeOf = 0;
                        return null;

                    string nameX1 = res.exprX1.GetString(name);
                    string nameX2 = res.exprX2.GetString(name);
                    string nameY1 = res.exprY1.GetString(name);
                    string nameY2 = res.exprY2.GetString(name);

                    res.infoX1 = new MemberInfo(debugger, name, nameX1);
                    res.infoX2 = new MemberInfo(debugger, name, nameX2);
                    res.infoY1 = new MemberInfo(debugger, name, nameY1);
                    res.infoY2 = new MemberInfo(debugger, name, nameY2);
                    if (!res.infoX1.IsValid || !res.infoX2.IsValid
                        || !res.infoY1.IsValid || !res.infoY2.IsValid)
                        //res.sizeOf = 0;
                        return null;

                    return res;
                }

                ITypeMatcher typeMatcher;
                ClassScopeExpression exprX1;
                ClassScopeExpression exprX2;
                ClassScopeExpression exprY1;
                ClassScopeExpression exprY2;
                CoordsX coordsX;
                CoordsY coordsY;
                Geometry.Traits traits;
            }

            private UserBoxCoords(ClassScopeExpression exprX1,
                                  ClassScopeExpression exprX2,
                                  CoordsX coordsX,
                                  ClassScopeExpression exprY1,
                                  ClassScopeExpression exprY2,
                                  CoordsY coordsY,
                                  Geometry.Traits traits)
            {
                this.exprX1 = exprX1;
                this.exprX2 = exprX2;
                this.exprY1 = exprY1;
                this.exprY2 = exprY2;
                this.coordsX = coordsX;
                this.coordsY = coordsY;
                this.traits = traits;

                this.sizeOf = 0;
            }

            public override Geometry.Traits GetTraits(MemoryReader mreader, Debugger debugger,
                                                      string name)
            {
                return traits;
            }

            public override ExpressionDrawer.IDrawable Load(MemoryReader mreader, Debugger debugger,
                                                            string name, string type,
                                                            LoadCallback callback) // dummy callback
            {
                string nameX1 = exprX1.GetString(name);
                string nameX2 = exprX2.GetString(name);
                string nameY1 = exprY1.GetString(name);
                string nameY2 = exprY2.GetString(name);

                double x1 = 0, x2 = 0, y1 = 0, y2 = 0;
                bool ok = ExpressionParser.TryLoadDouble(debugger, nameX1, out x1)
                       && ExpressionParser.TryLoadDouble(debugger, nameX2, out x2)
                       && ExpressionParser.TryLoadDouble(debugger, nameY1, out y1)
                       && ExpressionParser.TryLoadDouble(debugger, nameY2, out y2);
                if (!ok)
                    return null;

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
                if (sizeOf == 0)
                    return null;

                MemoryReader.ValueConverter<double> converterX1 = mreader.GetNumericConverter(infoX1.Type, infoX1.Size);
                MemoryReader.ValueConverter<double> converterX2 = mreader.GetNumericConverter(infoX2.Type, infoX2.Size);
                MemoryReader.ValueConverter<double> converterY1 = mreader.GetNumericConverter(infoY1.Type, infoY1.Size);
                MemoryReader.ValueConverter<double> converterY2 = mreader.GetNumericConverter(infoY2.Type, infoY2.Size);
                
                if (converterX1 == null || converterX2 == null
                    || converterY1 == null || converterY2 == null)
                    return null;

                return new MemoryReader.TransformingConverter<double>(
                            new MemoryReader.StructConverter<double>(
                                sizeOf,
                                new MemoryReader.Member<double>(converterX1, (int)infoX1.Offset),
                                new MemoryReader.Member<double>(converterY1, (int)infoY1.Offset),
                                new MemoryReader.Member<double>(converterX2, (int)infoX2.Offset),
                                new MemoryReader.Member<double>(converterY2, (int)infoY2.Offset)),
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

            ClassScopeExpression exprX1;
            ClassScopeExpression exprX2;
            ClassScopeExpression exprY1;
            ClassScopeExpression exprY2;
            CoordsX coordsX;
            CoordsY coordsY;
            Geometry.Traits traits;
            // For memory loading:
            MemberInfo infoX1;
            MemberInfo infoX2;
            MemberInfo infoY1;
            MemberInfo infoY2;
            int sizeOf;
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
                    string elementName, elementType;
                    containerLoaders.ContainerLoader.ElementInfo(containerName,
                                                                 containerLoaders.ContainerType,
                                                                 out elementName, out elementType);

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
                                Geometry.CoordinateSystem cs = Geometry.CoordinateSystem.Cartesian;
                                Geometry.Unit unit = Geometry.Unit.None;
                                GetCSAndUnit(elEntry, out cs, out unit);

                                var elParent = elCoordinates != null
                                             ? elCoordinates
                                             : elCoordinatesDimensions;

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
