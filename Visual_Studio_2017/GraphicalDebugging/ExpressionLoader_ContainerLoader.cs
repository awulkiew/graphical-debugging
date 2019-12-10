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

namespace GraphicalDebugging
{
    partial class ExpressionLoader
    {
        abstract class ContainerLoader : Loader
        {
            public override ExpressionLoader.Kind Kind() { return ExpressionLoader.Kind.Container; }

            // TODO: This method should probably return ulong
            abstract public int LoadSize(Debugger debugger, string name);

            public delegate bool ElementPredicate(string elementName);
            abstract public bool ForEachElement(Debugger debugger, string name, ElementPredicate elementPredicate);

            // ForEachMemoryBlock calling ReadArray taking ElementLoader returned by ContainerLoader
            // With ReadArray knowing which memory copying optimizations can be made based on ElementLoader's type
            // Or not

            // TODO: Move from here into specific classes
            virtual public string ElementType(string type)
            {
                List<string> tparams = Util.Tparams(type);
                return tparams.Count > 0 ? tparams[0] : "";
            }

            abstract public string ElementName(string name, string elemType);
            public delegate bool MemoryBlockPredicate(double[] values);
            abstract public bool ForEachMemoryBlock(MemoryReader mreader, string name, string type,
                                                    MemoryReader.Converter<double> elementConverter,
                                                    MemoryBlockPredicate memoryBlockPredicate);
        }

        abstract class RandomAccessContainer : ContainerLoader
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

        class BGVarray : BoostContainerVector // TODO: ContiguousContainer
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

        class BoostCircularBuffer : RandomAccessContainer
        {
            public override string Id() { return "boost::circular_buffer"; }

            public override int LoadSize(Debugger debugger, string name)
            {
                return LoadSizeParsed(debugger, name + ".m_size");
            }

            public override string ElementName(string name, string elType)
            {
                return "(*(" + name + ".m_first))";
            }

            public override bool ForEachElement(Debugger debugger, string name, ElementPredicate elementPredicate)
            {
                int size1, size2;
                LoadSizes(debugger, name, out size1, out size2);
                
                for (int i = 0; i < size1; ++i)
                {
                    string elName = "(*(" + name + ".m_first + " + i + "))";
                    if (!elementPredicate(elName))
                        return false;
                }

                for (int i = 0; i < size2; ++i)
                {
                    string elName = "(*(" + name + ".m_buff + " + i + "))";
                    if (!elementPredicate(elName))
                        return false;
                }

                return true;
            }

            public override bool ForEachMemoryBlock(MemoryReader mreader, string name, string type,
                                                    MemoryReader.Converter<double> elementConverter,
                                                    MemoryBlockPredicate memoryBlockPredicate)
            {
                if (elementConverter == null)
                    return false;

                int size1, size2;
                LoadSizes(mreader.Debugger, name, out size1, out size2);
                if (size1 <= 0)
                    return false;

                {
                    var blockConverter = new MemoryReader.ArrayConverter<double>(elementConverter, size1);
                    double[] values = new double[blockConverter.ValueCount()];
                    if (!mreader.Read("(*(" + name + ".m_first))", values, blockConverter))
                        return false;
                    if (!memoryBlockPredicate(values))
                        return false;
                }

                if (size2 > 0)
                {
                    var blockConverter = new MemoryReader.ArrayConverter<double>(elementConverter, size2);
                    double[] values = new double[blockConverter.ValueCount()];
                    if (!mreader.Read("(*(" + name + ".m_buff))", values, blockConverter))
                        return false;
                    if (!memoryBlockPredicate(values))
                        return false;
                }

                return true;
            }

            private void LoadSizes(Debugger debugger, string name, out int size1, out int size2)
            {
                int size = LoadSize(debugger, name);
                int size_fe = LoadSizeParsed(debugger, "(" + name + ".m_end - " + name + ".m_first)");

                size1 = size;
                size2 = 0;
                if (size > size_fe)
                {
                    size1 = size_fe;
                    size2 = size - size_fe;
                }
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

        class StdList : ContainerLoader
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
    }
}
