//------------------------------------------------------------------------------
// <copyright file="ExpressionLoader_ContainerLoader.cs">
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

            abstract public string ElementType(string type);
            abstract public string ElementName(string name, string elemType);
            public delegate bool MemoryBlockPredicate(double[] values);
            abstract public bool ForEachMemoryBlock(MemoryReader mreader, Debugger debugger,
                                                    string name, string type,
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
            public override bool ForEachMemoryBlock(MemoryReader mreader, Debugger debugger,
                                                    string name, string type,
                                                    MemoryReader.Converter<double> elementConverter,
                                                    MemoryBlockPredicate memoryBlockPredicate)
            {
                return this.ForEachMemoryBlock(mreader, debugger,
                                               name, type,
                                               ElementName(name, ElementType(type)),
                                               elementConverter, memoryBlockPredicate);
            }

            protected bool ForEachMemoryBlock(MemoryReader mreader, Debugger debugger,
                                              string name, string type, string blockName,
                                              MemoryReader.Converter<double> elementConverter,
                                              MemoryBlockPredicate memoryBlockPredicate)
            {
                if (elementConverter == null)
                    return false;
                int size = LoadSize(debugger, name);
                var blockConverter = new MemoryReader.ArrayConverter<double>(elementConverter, size);

                ulong address = ExpressionParser.GetValueAddress(debugger, blockName);
                if (address == 0)
                    return false;

                double[] values = new double[blockConverter.ValueCount()];
                if (! mreader.Read(address, values, blockConverter))
                    return false;
                return memoryBlockPredicate(values);
            }
        }

        class CArray : ContiguousContainer
        {
            public override bool MatchType(Loaders loaders, string name, string type, string id)
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
            public override bool MatchType(Loaders loaders, string name, string type, string id)
            {
                return id == "std::array";
            }

            public override string ElementType(string type)
            {
                List<string> tparams = Util.Tparams(type);
                return tparams.Count > 0 ? tparams[0] : "";
            }

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
            public override bool MatchType(Loaders loaders, string name, string type, string id)
            {
                return id == "boost::array";
            }

            public override string ElementName(string name, string elType)
            {
                return name + ".elems[0]";
            }
        }

        class BoostContainerVector : ContiguousContainer
        {
            public override bool MatchType(Loaders loaders, string name, string type, string id)
            {
                return id == "boost::container::vector";
            }

            public override string ElementType(string type)
            {
                List<string> tparams = Util.Tparams(type);
                return tparams.Count > 0 ? tparams[0] : "";
            }

            public override string ElementName(string name, string elType)
            {
                return name + ".m_holder.m_start[0]";
            }

            public override int LoadSize(Debugger debugger, string name)
            {
                return ExpressionParser.LoadSize(debugger, name + ".m_holder.m_size");
            }
        }

        class BoostContainerStaticVector : BoostContainerVector
        {
            public override bool MatchType(Loaders loaders, string name, string type, string id)
            {
                return id == "boost::container::static_vector";
            }

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
            public override bool MatchType(Loaders loaders, string name, string type, string id)
            {
                return id == "boost::geometry::index::detail::varray";
            }

            public override string ElementName(string name, string elType)
            {
                // TODO: Check if type-cast is needed here
                return "((" + elType + "*)" + name + ".m_storage.data_.buf)[0]";
            }

            public override int LoadSize(Debugger debugger, string name)
            {
                return ExpressionParser.LoadSize(debugger, name + ".m_size");
            }
        }

        class BoostCircularBuffer : RandomAccessContainer
        {
            public override bool MatchType(Loaders loaders, string name, string type, string id)
            {
                return id == "boost::circular_buffer";
            }

            public override int LoadSize(Debugger debugger, string name)
            {
                return ExpressionParser.LoadSize(debugger, name + ".m_size");
            }

            public override string ElementType(string type)
            {
                List<string> tparams = Util.Tparams(type);
                return tparams.Count > 0 ? tparams[0] : "";
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

            public override bool ForEachMemoryBlock(MemoryReader mreader, Debugger debugger,
                                                    string name, string type,
                                                    MemoryReader.Converter<double> elementConverter,
                                                    MemoryBlockPredicate memoryBlockPredicate)
            {
                if (elementConverter == null)
                    return false;

                int size1, size2;
                LoadSizes(debugger, name, out size1, out size2);
                if (size1 <= 0)
                    return false;

                {
                    ulong firstAddress = ExpressionParser.GetValueAddress(debugger, "(*(" + name + ".m_first))");
                    if (firstAddress == 0)
                        return false;
                    var blockConverter = new MemoryReader.ArrayConverter<double>(elementConverter, size1);
                    double[] values = new double[blockConverter.ValueCount()];
                    if (!mreader.Read(firstAddress, values, blockConverter))
                        return false;
                    if (!memoryBlockPredicate(values))
                        return false;
                }

                if (size2 > 0)
                {
                    ulong buffAddress = ExpressionParser.GetValueAddress(debugger, "(*(" + name + ".m_buff))");
                    if (buffAddress == 0)
                        return false;
                    var blockConverter = new MemoryReader.ArrayConverter<double>(elementConverter, size2);
                    double[] values = new double[blockConverter.ValueCount()];
                    if (!mreader.Read(buffAddress, values, blockConverter))
                        return false;
                    if (!memoryBlockPredicate(values))
                        return false;
                }

                return true;
            }

            private void LoadSizes(Debugger debugger, string name, out int size1, out int size2)
            {
                int size = LoadSize(debugger, name);
                int size_fe = ExpressionParser.LoadSize(debugger, "(" + name + ".m_end - " + name + ".m_first)");

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
            public override bool MatchType(Loaders loaders, string name, string type, string id)
            {
                return id == "std::vector";
            }

            public override string RandomAccessElementName(string rawName, int i)
            {
                return FirstStr(rawName) + "[" + i + "]";
            }

            public override string ElementType(string type)
            {
                List<string> tparams = Util.Tparams(type);
                return tparams.Count > 0 ? tparams[0] : "";
            }

            public override string ElementName(string name, string elType)
            {
                return FirstStr(name) + "[0]";
            }

            public override int LoadSize(Debugger debugger, string name)
            {
                return ExpressionParser.LoadSize(debugger, SizeStr(name));
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
            public override bool MatchType(Loaders loaders, string name, string type, string id)
            {
                return id == "std::deque";
            }

            public override string ElementType(string type)
            {
                List<string> tparams = Util.Tparams(type);
                return tparams.Count > 0 ? tparams[0] : "";
            }

            public override string ElementName(string name, string elType)
            {
                return ElementStr(name, 0);
            }

            public override bool ForEachMemoryBlock(MemoryReader mreader, Debugger debugger,
                                                    string name, string type,
                                                    MemoryReader.Converter<double> elementConverter,
                                                    MemoryBlockPredicate memoryBlockPredicate)
            {
                int size = LoadSize(debugger, name);
                if (size == 0)
                    return true;

                // Map size
                int mapSize = 0;
                if (! ExpressionParser.TryLoadInt(debugger, MapSizeStr(name), out mapSize))
                    return false;

                VariableInfo mapInfo = new VariableInfo(debugger, MapStr(name) + "[0]");
                if (! mapInfo.IsValid)
                    return false;

                // Map - array of pointers                
                ulong[] pointers = new ulong[mapSize];
                if (! mreader.ReadPointerArray(mapInfo.Address, mapInfo.Type, mapInfo.Size, pointers))
                    return false;

                // Block size
                int dequeSize = 0;
                if (! ExpressionParser.TryLoadInt(debugger, "((int)" + name + "._EEN_DS)", out dequeSize))
                    return false;

                // Offset
                int offset = 0;
                if (! ExpressionParser.TryLoadInt(debugger, OffsetStr(name), out offset))
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
                return ExpressionParser.LoadSize(debugger, SizeStr(name));
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
            public override bool MatchType(Loaders loaders, string name, string type, string id)
            {
                return id == "std::list";
            }

            public override string ElementType(string type)
            {
                List<string> tparams = Util.Tparams(type);
                return tparams.Count > 0 ? tparams[0] : "";
            }

            public override string ElementName(string name, string elType)
            {
                return HeadStr(name) + "->_Next->_Myval";
            }

            public override bool ForEachMemoryBlock(MemoryReader mreader, Debugger debugger,
                                                    string name, string type,
                                                    MemoryReader.Converter<double> elementConverter,
                                                    MemoryBlockPredicate memoryBlockPredicate)
            {
                int size = LoadSize(debugger, name);
                if (size <= 0)
                    return true;

                string nextName = HeadStr(name) + "->_Next";
                string nextNextName = HeadStr(name) + "->_Next->_Next";
                string nextValName = HeadStr(name) + "->_Next->_Myval";

                TypeInfo nextInfo = new TypeInfo(debugger, nextName);
                if (! nextInfo.IsValid)
                    return false;

                MemoryReader.ValueConverter<ulong> nextConverter = mreader.GetPointerConverter(nextInfo.Type, nextInfo.Size);
                if (nextConverter == null)
                    return false;

                long nextDiff = ExpressionParser.GetAddressDifference(debugger, "(*" + nextName + ")", nextNextName);
                long valDiff = ExpressionParser.GetAddressDifference(debugger, "(*" + nextName + ")", nextValName);
                if (ExpressionParser.IsInvalidAddressDifference(nextDiff)
                 || ExpressionParser.IsInvalidAddressDifference(valDiff)
                 || nextDiff < 0 || valDiff < 0)
                    return false;

                ulong[] nextTmp = new ulong[1];
                ulong next = 0;

                for (int i = 0; i < size; ++i)
                {
                    ulong address = 0;
                    if (next == 0)
                    {
                        address = ExpressionParser.GetValueAddress(debugger, nextName);
                        if (address == 0)
                            return false;
                    }
                    else
                    {
                        address = next + (ulong)nextDiff;
                    }

                    if (!mreader.Read(address, nextTmp, nextConverter))
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
                return ExpressionParser.LoadSize(debugger, SizeStr(name));
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

        // TODO: limit number of processed values with size
        //       in case some pointers were invalid
        class StdSet : ContainerLoader
        {
            public override bool MatchType(Loaders loaders, string name, string type, string id)
            {
                return id == "std::set";
            }

            public override string ElementType(string type)
            {
                List<string> tparams = Util.Tparams(type);
                return tparams.Count > 0 ? tparams[0] : "";
            }

            public override string ElementName(string name, string elType)
            {
                return HeadStr(name) + "->_Myval";
            }

            public override bool ForEachMemoryBlock(MemoryReader mreader, Debugger debugger,
                                                    string name, string type,
                                                    MemoryReader.Converter<double> elementConverter,
                                                    MemoryBlockPredicate memoryBlockPredicate)
            {
                int size = LoadSize(debugger, name);
                if (size <= 0)
                    return true;

                string headName = HeadStr(name);
                string leftName = headName + "->_Left";
                string rightName = headName + "->_Right";
                string isNilName = headName + "->_Isnil";
                string valName = headName + "->_Myval";

                TypeInfo headInfo = new TypeInfo(debugger, headName);
                if (! headInfo.IsValid)
                    return false;

                MemoryReader.ValueConverter<byte, byte> boolConverter = new MemoryReader.ValueConverter<byte, byte>();
                MemoryReader.ValueConverter<ulong> ptrConverter = mreader.GetPointerConverter(headInfo.Type, headInfo.Size);
                if (ptrConverter == null)
                    return false;

                long leftDiff = ExpressionParser.GetAddressDifference(debugger, "(*" + headName + ")", leftName);
                long rightDiff = ExpressionParser.GetAddressDifference(debugger, "(*" + headName + ")", rightName);
                long isNilDiff = ExpressionParser.GetAddressDifference(debugger, "(*" + headName + ")", isNilName);
                long valDiff = ExpressionParser.GetAddressDifference(debugger, "(*" + headName + ")", valName);
                if (ExpressionParser.IsInvalidAddressDifference(leftDiff)
                 || ExpressionParser.IsInvalidAddressDifference(rightDiff)
                 || ExpressionParser.IsInvalidAddressDifference(isNilDiff)
                 || ExpressionParser.IsInvalidAddressDifference(valDiff)
                 || leftDiff < 0 || rightDiff < 0 || isNilDiff < 0 || valDiff < 0)
                    return false;

                ulong address = ExpressionParser.GetValueAddress(debugger, headName);
                if (address == 0)
                    return false;

                ulong[] headAddr = new ulong[1];
                if (!mreader.Read(address, headAddr, ptrConverter))
                    return false;

                return ForEachMemoryBlockRecursive(mreader, elementConverter, memoryBlockPredicate,
                                                   boolConverter, ptrConverter,
                                                   headAddr[0], leftDiff, rightDiff, isNilDiff, valDiff);
            }

            private bool ForEachMemoryBlockRecursive(MemoryReader mreader,
                                                     MemoryReader.Converter<double> elementConverter,
                                                     MemoryBlockPredicate memoryBlockPredicate,
                                                     MemoryReader.ValueConverter<byte, byte> boolConverter,
                                                     MemoryReader.ValueConverter<ulong> ptrConverter,
                                                     ulong nodeAddr,
                                                     long leftDiff, long rightDiff,
                                                     long isNilDiff, long valDiff)
            {
                byte[] isNil = new byte[1];
                if (!mreader.Read(nodeAddr + (ulong)isNilDiff, isNil, boolConverter))
                    return false;
                if (isNil[0] == 0) // _Isnil == false
                {
                    ulong[] leftAddr = new ulong[1];
                    ulong[] rightAddr = new ulong[1];
                    double[] values = new double[elementConverter.ValueCount()];

                    return mreader.Read(nodeAddr + (ulong)leftDiff, leftAddr, ptrConverter)
                        && mreader.Read(nodeAddr + (ulong)rightDiff, rightAddr, ptrConverter)
                        && mreader.Read(nodeAddr + (ulong)valDiff, values, elementConverter)
                        && ForEachMemoryBlockRecursive(mreader, elementConverter, memoryBlockPredicate,
                                                       boolConverter, ptrConverter,
                                                       leftAddr[0], leftDiff, rightDiff, isNilDiff, valDiff)
                        && memoryBlockPredicate(values)
                        && ForEachMemoryBlockRecursive(mreader, elementConverter, memoryBlockPredicate,
                                                       boolConverter, ptrConverter,
                                                       rightAddr[0], leftDiff, rightDiff, isNilDiff, valDiff);
                }

                return true;
            }

            public override int LoadSize(Debugger debugger, string name)
            {
                return ExpressionParser.LoadSize(debugger, SizeStr(name));
            }

            public override bool ForEachElement(Debugger debugger, string name, ElementPredicate elementPredicate)
            {
                int size = LoadSize(debugger, name);
                if (size <= 0)
                    return true;

                string nodeName = HeadStr(name);
                return ForEachElementRecursive(debugger, nodeName, elementPredicate);
            }

            private bool ForEachElementRecursive(Debugger debugger, string nodeName, ElementPredicate elementPredicate)
            {
                Expression expr = debugger.GetExpression("(" + nodeName + "->_Isnil == false)");
                if (expr.IsValidValue && (expr.Value == "true" || expr.Value == "1"))
                {
                    return ForEachElementRecursive(debugger, nodeName + "->_Left", elementPredicate)
                        && elementPredicate(nodeName + "->_Myval")
                        && ForEachElementRecursive(debugger, nodeName + "->_Right", elementPredicate);
                }
                return true; // ignore leaf
            }

            private string HeadStr(string name)
            {
                return version == Version.Msvc12
                     ? name + "._Myhead->_Parent"
                     : name + "._Mypair._Myval2._Myval2._Myhead->_Parent";
            }

            private string SizeStr(string name)
            {
                return version == Version.Msvc12
                     ? name + "._Mysize"
                     : name + "._Mypair._Myval2._Myval2._Mysize";
            }

            public override void Initialize(Debugger debugger, string name)
            {
                string name12 = name + "._Mysize";
                //string name14_15 = name + "._Mypair._Myval2._Myval2._Mysize";

                if (debugger.GetExpression(name12).IsValidValue)
                    version = Version.Msvc12;
            }
            private enum Version { Unknown, Msvc12, Msvc14_15 };
            private Version version = Version.Msvc14_15;
        }

        class CSArray : ContiguousContainer
        {
            public override bool MatchType(Loaders loaders, string name, string type, string id)
            {
                return ElementType(type).Length > 0;
            }

            public override string ElementType(string type)
            {
                return ElemTypeFromType(type);
            }
            
            public override int LoadSize(Debugger debugger, string name)
            {
                return ExpressionParser.LoadSize(debugger, name + ".Length");
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
            public override bool MatchType(Loaders loaders, string name, string type, string id)
            {
                return id == "System.Collections.Generic.List";
            }

            public override int LoadSize(Debugger debugger, string name)
            {
                return ExpressionParser.LoadSize(debugger, name + ".Count");
            }

            public override string RandomAccessElementName(string rawName, int i)
            {
                return rawName + "._items[" + i + "]";
            }

            public override string ElementType(string type)
            {
                List<string> tparams = Util.Tparams(type);
                return tparams.Count > 0 ? tparams[0] : "";
            }

            public override string ElementName(string name, string elType)
            {
                return name + "._items[0]";
            }

            public override bool ForEachMemoryBlock(MemoryReader mreader, Debugger debugger,
                                                    string name, string type,
                                                    MemoryReader.Converter<double> elementConverter,
                                                    MemoryBlockPredicate memoryBlockPredicate)
            {
                Expression expr = debugger.GetExpression(name + "._items");
                if (!expr.IsValidValue || CSArray.ElemTypeFromType(expr.Type).Length <= 0)
                    return false;

                return base.ForEachMemoryBlock(mreader, debugger,
                                               name, type,
                                               elementConverter, memoryBlockPredicate);
            }
        }

        class CSLinkedList : ContainerLoader
        {
            public override bool MatchType(Loaders loaders, string name, string type, string id)
            {
                return id == "System.Collections.Generic.LinkedList";
            }

            public override string ElementType(string type)
            {
                List<string> tparams = Util.Tparams(type);
                return tparams.Count > 0 ? tparams[0] : "";
            }

            public override string ElementName(string name, string elType)
            {
                return name + ".head.item";
            }

            public override int LoadSize(Debugger debugger, string name)
            {
                return ExpressionParser.LoadSize(debugger, name + ".count");
            }

            public override bool ForEachMemoryBlock(MemoryReader mreader, Debugger debugger,
                                                    string name, string type,
                                                    MemoryReader.Converter<double> elementConverter,
                                                    MemoryBlockPredicate memoryBlockPredicate)
            {
                int size = LoadSize(debugger, name);
                if (size <= 0)
                    return true;

                // TODO: All of the debugger-related things should be done
                //   in Initialize().

                // TODO: Handle non-value types,
                //   It is not clear for now where the distinction should be made
                //   in the container or outside. When non-value types are stored
                //   the container effectively stores pointers to objects.
                //   So whether or not it's a pointer-container is defined by the
                //   element type in C# and by the container in C++.
                string elementType = debugger.GetExpression(name + ".head.item").Type;
                Expression isValueTypeExpr = debugger.GetExpression("typeof(" + elementType + ").IsValueType");
                if (!isValueTypeExpr.IsValidValue || isValueTypeExpr.Value != "true")
                    return false;

                //string headPointerPointerName = "(void*)&(" + name + ".head)"; //(void*)IntPtr*
                string headPointerName = "(void*)*(&(" + name + ".head))"; // (void*)IntPtr
                string nextPointerPointerName = "(void*)&(" + name + ".head.next)"; //(void*)IntPtr*
                string nextPointerName = "(void*)*(&(" + name + ".head.next))"; // (void*)IntPtr
                string valPointerName = "(void*)&(" + name + ".head.item)"; // (void*)IntPtr* or (void*)ValueType*

                TypeInfo nextPointerInfo = new TypeInfo(debugger, nextPointerPointerName);
                TypeInfo nextInfo = new TypeInfo(debugger, nextPointerName);
                if (!nextPointerInfo.IsValid || !nextInfo.IsValid)
                    return false;

                MemoryReader.ValueConverter<ulong> pointerConverter = mreader.GetPointerConverter(nextPointerInfo.Type, nextPointerInfo.Size);
                if (pointerConverter == null)
                    return false;

                long nextDiff = ExpressionParser.GetPointerDifference(debugger, headPointerName, nextPointerPointerName);
                long valDiff = ExpressionParser.GetPointerDifference(debugger, headPointerName, valPointerName);
                if (ExpressionParser.IsInvalidAddressDifference(nextDiff)
                 || ExpressionParser.IsInvalidAddressDifference(valDiff)
                 || nextDiff < 0 || valDiff < 0)
                    return false;

                ulong address = ExpressionParser.GetPointer(debugger, headPointerName);
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
                    if (!mreader.Read(address + (ulong)nextDiff, nextTmp, pointerConverter))
                        return false;
                    address = nextTmp[0];
                }
                return true;
            }

            public override bool ForEachElement(Debugger debugger, string name, ElementPredicate elementPredicate)
            {
                int size = this.LoadSize(debugger, name);

                string nodeName = name + ".head";
                for (int i = 0; i < size; ++i)
                {
                    string elName = nodeName + ".item";
                    if (!elementPredicate(elName))
                        return false;
                    nodeName = nodeName + ".next";
                }
                return true;
            }
        }
    }
}
