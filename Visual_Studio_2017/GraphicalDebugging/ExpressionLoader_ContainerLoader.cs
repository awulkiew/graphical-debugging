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
        // TODO: The size should probably be stored as ulong
        class Size
        {
            public Size() { }
            public Size(int value) { this.value = value; IsValid = true; }
            public static implicit operator int(Size s) { return s.value; }

            public bool IsValid { get; } = false;
            private int value = 0;
        }

        abstract class ContainerLoader : Loader
        {
            abstract public void ElementInfo(string name, string type,
                                             out string elemName, out string elemType);

            // TODO: This method should probably return ulong
            abstract public int LoadSize(Debugger debugger, string name);

            public delegate bool ElementPredicate(string elementName);
            abstract public bool ForEachElement(Debugger debugger, string name, ElementPredicate elementPredicate);

            // ForEachMemoryBlock calling ReadArray taking ElementLoader returned by ContainerLoader
            // With ReadArray knowing which memory copying optimizations can be made based on ElementLoader's type
            // Or not

            abstract public Size LoadSize(MemoryReader mreader, ulong address);

            public delegate bool MemoryBlockPredicate<T>(T[] values);
            abstract public bool ForEachMemoryBlock<T>(MemoryReader mreader, ulong address, int size,
                                                       MemoryReader.Converter<T> elementConverter,
                                                       MemoryBlockPredicate<T> memoryBlockPredicate)
                where T : struct;

            // TEMP helpers

            virtual public int LoadSize(MemoryReader mreader, Debugger debugger,
                                        string name, string type, ulong address)
            {
                if (mreader != null)
                {
                    if (address == 0)
                        address = ExpressionParser.GetValueAddress(debugger, name);

                    if (address != 0)
                    {
                        Size size = LoadSize(mreader, address);
                        if (size.IsValid)
                            return size;
                    }
                }

                return LoadSize(debugger, name);
            }

            virtual public bool ForEachMemoryBlock<T>(MemoryReader mreader, Debugger debugger,
                                                      string name, string type, ulong address,
                                                      MemoryReader.Converter<T> elementConverter,
                                                      MemoryBlockPredicate<T> memoryBlockPredicate)
                where T : struct
            {
                if (mreader == null)
                    return false;

                if (address == 0)
                {
                    address = ExpressionParser.GetValueAddress(debugger, name);
                    if (address == 0)
                        return false;
                }

                Size s = LoadSize(mreader, address);
                int size = s.IsValid
                         ? s
                         : LoadSize(debugger, name);

                return ForEachMemoryBlock(mreader, address, size, elementConverter, memoryBlockPredicate);
            }
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
            abstract public ulong MemoryBegin(MemoryReader mreader, ulong address);

            public override bool ForEachMemoryBlock<T>(MemoryReader mreader, ulong address, int sizeExt,
                                                       MemoryReader.Converter<T> elementConverter,
                                                       MemoryBlockPredicate<T> memoryBlockPredicate)
            {
                // TODO: This is second call of LoadSize
                /*Size s = LoadSize(mreader, address);
                int size = s.IsValid ? s : sizeExt;*/
                int size = sizeExt;
                if (size <= 0)
                    return true;

                ulong beginAddress = MemoryBegin(mreader, address);
                if (beginAddress == 0)
                    return false;

                var blockConverter = new MemoryReader.ArrayConverter<T>(elementConverter, size);
                T[] values = new T[blockConverter.ValueCount()];
                if (! mreader.Read(beginAddress, values, blockConverter))
                    return false;
                return memoryBlockPredicate(values);
            }
        }

        class CArray : ContiguousContainer
        {
            public class LoaderCreator : ExpressionLoader.LoaderCreator
            {
                public bool IsUserDefined() { return false; }
                public Kind Kind() { return ExpressionLoader.Kind.Container; }
                public Loader Create(Loaders loaders, Debugger debugger, string name, string type, string id)
                {
                    string elemType;
                    int size;
                    if (!NameSizeFromType(type, out elemType, out size))
                        return null;

                    return new CArray(elemType, size);
                }
            }

            private CArray(string elemType, int size)
            {
                this.elemType = elemType;
                this.size = size;
            }

            public override void ElementInfo(string name, string type,
                                             out string elemName, out string elemType)
            {
                elemName = RawNameFromName(name) + "[0]";
                elemType = this.elemType;
            }

            public override string RandomAccessName(string name)
            {
                return RawNameFromName(name);
            }

            public override int LoadSize(Debugger debugger, string name)
            {
                return size;
            }

            public override ulong MemoryBegin(MemoryReader mreader, ulong address)
            {
                return address;
            }

            public override Size LoadSize(MemoryReader mreader, ulong address)
            {
                return new Size(size);
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

            string elemType;
            int size;
        }

        class StdArray : ContiguousContainer
        {
            public class LoaderCreator : ExpressionLoader.LoaderCreator
            {
                public bool IsUserDefined() { return false; }
                public Kind Kind() { return ExpressionLoader.Kind.Container; }
                public Loader Create(Loaders loaders, Debugger debugger, string name, string type, string id)
                {
                    if (id != "std::array")
                        return null;
                    // TODO: 1 call to Util.Tparam
                    string elemType = Util.Tparam(type, 0);
                    int size = Math.Max(int.Parse(Util.Tparam(type, 1)), 0);

                    long elemOffset = ExpressionParser.GetAddressDifference(debugger, name, name + "._Elems[0]");
                    if (elemOffset < 0)
                        return null;

                    return new StdArray(elemType, elemOffset, size);
                }
            }

            protected StdArray(string elemType, long elemOffset, int size)
            {
                this.elemType = elemType;
                this.elemOffset = elemOffset;
                this.size = size;
            }

            public override void ElementInfo(string name, string type,
                                             out string elemName, out string elemType)
            {
                elemName = name + "._Elems[0]";
                elemType = this.elemType;
            }

            public override int LoadSize(Debugger debugger, string name)
            {
                return size;
            }

            public override ulong MemoryBegin(MemoryReader mreader, ulong address)
            {
                return address != 0
                     ? address + (ulong)elemOffset
                     : 0;
            }

            public override Size LoadSize(MemoryReader mreader, ulong address)
            {
                return new Size(size);
            }

            protected string elemType;
            protected long elemOffset;
            protected int size;
        }

        class BoostArray : StdArray
        {
            public new class LoaderCreator : ExpressionLoader.LoaderCreator
            {
                public bool IsUserDefined() { return false; }
                public Kind Kind() { return ExpressionLoader.Kind.Container; }
                public Loader Create(Loaders loaders, Debugger debugger, string name, string type, string id)
                {
                    if (id != "boost::array")
                        return null;
                    // TODO: 1 call to Util.Tparam
                    string elemType = Util.Tparam(type, 0);
                    int size = Math.Max(int.Parse(Util.Tparam(type, 1)), 0);

                    long elemOffset = ExpressionParser.GetAddressDifference(debugger, name, name + ".elems[0]");
                    if (elemOffset < 0)
                        return null;

                    return new BoostArray(elemType, elemOffset, size);
                }
            }

            private BoostArray(string elemType, long elemOffset, int size)
                : base(elemType, elemOffset, size)
            { }

            public override void ElementInfo(string name, string type,
                                             out string elemName, out string elemType)
            {
                elemName = name + ".elems[0]";
                elemType = base.elemType;
            }
        }

        // TODO: Instead of loading one member at a time the whole object could be loaded at once
        //   and all members extracted afterwards from the memory block

        class SizeMember
        {
            // sizeMember has to start with '.', e.g. ".m_size"
            public SizeMember(Debugger debugger, string name, string sizeMember)
            {
                this.sizeMember = sizeMember;
                this.sizeType = ExpressionParser.GetValueType(debugger, name + sizeMember);
                this.sizeSizeOf = ExpressionParser.GetTypeSizeof(debugger, sizeType);
                long sizeOffset = ExpressionParser.GetAddressDifference(debugger, name, name + sizeMember);
                this.sizeOffset = (ulong)sizeOffset;
                this.memoryOk = !Util.Empty(sizeType)
                             && !ExpressionParser.IsInvalidSize(sizeSizeOf)
                             && !ExpressionParser.IsInvalidAddressDifference(sizeOffset)
                             && sizeOffset >= 0;
            }

            public int LoadParsed(Debugger debugger, string name)
            {
                return ExpressionParser.LoadSize(debugger, name + sizeMember);
            }

            // TODO: Return -1 or int.Min on failure
            // TODO: Use ulong instead of int?
            public Size LoadMemory(MemoryReader mreader, ulong address)
            {
                if (!memoryOk)
                    return new Size();
                var converter = mreader.GetValueConverter<ulong>(sizeType, sizeSizeOf);
                if (converter == null)
                    return new Size();
                ulong[] size = new ulong[1];
                return mreader.Read(address + sizeOffset, size, converter)
                     ? new Size((int)size[0])
                     : new Size();
            }

            string sizeMember;
            string sizeType;
            int sizeSizeOf;
            ulong sizeOffset;
            bool memoryOk;
        }

        class PointerMember
        {
            public PointerMember(Debugger debugger, string name, string ptrMember)
            {
                this.ptrMember = ptrMember;
                this.ptrMemberType = ExpressionParser.GetValueType(debugger, name + ptrMember);
                this.ptrMemberSizeOf = ExpressionParser.GetTypeSizeof(debugger, ptrMemberType);
                long ptrMemberOffset = ExpressionParser.GetAddressDifference(debugger, name, name + ptrMember);
                this.ptrMemberOffset = (ulong)ptrMemberOffset;

                this.memoryOk = !Util.Empty(ptrMemberType)
                             && !ExpressionParser.IsInvalidSize(ptrMemberSizeOf)
                             && !ExpressionParser.IsInvalidAddressDifference(ptrMemberOffset)
                             && ptrMemberOffset >= 0;
            }

            public ulong LoadParsed(Debugger debugger, string name)
            {
                return ExpressionParser.GetPointer(debugger, '(' + name + ptrMember + ')');
            }

            public ulong LoadMemory(MemoryReader mreader, ulong address)
            {
                if (!memoryOk)
                    return 0;
                var converter = mreader.GetPointerConverter(ptrMemberType, ptrMemberSizeOf);
                if (converter == null)
                    return 0;
                ulong[] addr = new ulong[1];
                return mreader.Read(address + ptrMemberOffset, addr, converter)
                     ? addr[0]
                     : 0;
            }

            string ptrMember;
            string ptrMemberType;
            ulong ptrMemberOffset;
            int ptrMemberSizeOf;
            bool memoryOk;
        }

        class PointerMembersDistance
        {
            public PointerMembersDistance(Debugger debugger, string name,
                                          string firstMember, string lastMember)
            {
                this.firstMember = firstMember;
                this.lastMember = lastMember;
                this.first = new PointerMember(debugger, name, firstMember);
                this.last = new PointerMember(debugger, name , lastMember);
                string type = ExpressionParser.GetValueType(debugger, "*(" + name + firstMember + ")");
                sizeOf = ExpressionParser.GetTypeSizeof(debugger, type);
            }

            public int LoadParsed(Debugger debugger, string name)
            {
                return ExpressionParser.LoadSize(debugger, '(' + name + lastMember + '-' + name + firstMember + ')');
            }

            // TODO: Return ulong?
            // TODO: Return -1 or int.Min on failure
            public Size LoadMemory(MemoryReader mreader, ulong address)
            {
                ulong first = this.first.LoadMemory(mreader, address);
                ulong last = this.last.LoadMemory(mreader, address);
                if (first != 0 && last != 0 && first <= last && sizeOf > 0)
                {
                    int byteSize = (int)(last - first);
                    if (byteSize % sizeOf == 0)
                        return new Size(byteSize / sizeOf);
                }
                return new Size();
            }

            string firstMember;
            string lastMember;
            PointerMember first;
            PointerMember last;
            int sizeOf;
        }

        class BoostContainerVector : ContiguousContainer
        {
            public class LoaderCreator : ExpressionLoader.LoaderCreator
            {
                public bool IsUserDefined() { return false; }
                public Kind Kind() { return ExpressionLoader.Kind.Container; }
                public Loader Create(Loaders loaders, Debugger debugger, string name, string type, string id)
                {
                    if (id != "boost::container::vector")
                        return null;

                    return new BoostContainerVector(Util.Tparam(type, 0),
                                                    new PointerMember(debugger, name, ".m_holder.m_start"),
                                                    new SizeMember(debugger, name, ".m_holder.m_size"));
                }
            }

            protected BoostContainerVector(string elemType, PointerMember start, SizeMember size)
            {
                this.elemType = elemType;
                this.start = start;
                this.size = size;
            }

            public override void ElementInfo(string name, string type,
                                             out string elemName, out string elemType)
            {
                elemName = name + ".m_holder.m_start[0]";
                elemType = this.elemType;
            }

            public override int LoadSize(Debugger debugger, string name)
            {
                return size.LoadParsed(debugger, name);
            }

            public override ulong MemoryBegin(MemoryReader mreader, ulong address)
            {
                return start.LoadMemory(mreader, address);
            }

            public override Size LoadSize(MemoryReader mreader, ulong address)
            {
                return size.LoadMemory(mreader, address);
            }

            protected string elemType;
            protected PointerMember start;
            protected SizeMember size;
        }

        class BoostContainerStaticVector : BoostContainerVector
        {
            public new class LoaderCreator : ExpressionLoader.LoaderCreator
            {
                public bool IsUserDefined() { return false; }
                public Kind Kind() { return ExpressionLoader.Kind.Container; }
                public Loader Create(Loaders loaders, Debugger debugger, string name, string type, string id)
                {
                    if (id != "boost::container::static_vector")
                        return null;

                    return new BoostContainerStaticVector(Util.Tparam(type, 0),
                                                          new PointerMember(debugger, name, ".m_holder.storage.data"),
                                                          new SizeMember(debugger, name, ".m_holder.m_size"));
                }
            }

            private BoostContainerStaticVector(string elemType, PointerMember start, SizeMember size)
                : base(elemType, start, size)
            { }

            public override void ElementInfo(string name, string type,
                                             out string elemName, out string elemType)
            {
                elemType = base.elemType;
                // TODO: The type-cast is needed here!!!
                // Although it's possible it will be ok since this is used only to pass a value starting the memory block
                // into the memory reader and type is not really important
                // and in other places like PointRange or BGRange correct type is passed with it
                // and based on this type the data is processed
                // It needs testing
                elemName = "((" + elemType + "*)" + name + ".m_holder.storage.data)[0]";
            }
        }

        class BGVarray : ContiguousContainer
        {
            public class LoaderCreator : ExpressionLoader.LoaderCreator
            {
                public bool IsUserDefined() { return false; }
                public Kind Kind() { return ExpressionLoader.Kind.Container; }
                public Loader Create(Loaders loaders, Debugger debugger, string name, string type, string id)
                {
                    if (id != "boost::geometry::index::detail::varray")
                        return null;

                    string elemType = Util.Tparam(type, 0);

                    long elemOffset = ExpressionParser.GetAddressDifference(debugger, name, name + ".m_storage.data_.buf[0]");
                    if (elemOffset < 0)
                        return null;

                    return new BGVarray(elemType, elemOffset, new SizeMember(debugger, name, ".m_size"));
                }
            }

            private BGVarray(string elemType, long elemOffset, SizeMember sizeMember)
            {
                this.elemType = elemType;
                this.elemOffset = elemOffset;
                this.sizeMember = sizeMember;
            }

            public override void ElementInfo(string name, string type,
                                             out string elemName, out string elemType)
            {
                elemType = this.elemType;
                // TODO: Check if type-cast is needed here
                elemName = "((" + elemType + "*)" + name + ".m_storage.data_.buf)[0]";
            }

            public override Size LoadSize(MemoryReader mreader, ulong address)
            {
                return sizeMember.LoadMemory(mreader, address);
            }

            public override ulong MemoryBegin(MemoryReader mreader, ulong address)
            {
                return address != 0
                     ? address + (ulong)elemOffset
                     : 0;
            }

            public override int LoadSize(Debugger debugger, string name)
            {
                return sizeMember.LoadParsed(debugger, name);
            }

            string elemType;
            long elemOffset;
            SizeMember sizeMember;
        }

        class BoostCircularBuffer : RandomAccessContainer
        {
            public class LoaderCreator : ExpressionLoader.LoaderCreator
            {
                public bool IsUserDefined() { return false; }
                public Kind Kind() { return ExpressionLoader.Kind.Container; }
                public Loader Create(Loaders loaders, Debugger debugger, string name, string type, string id)
                {
                    return id == "boost::circular_buffer"
                         ? new BoostCircularBuffer(new SizeMember(debugger, name, ".m_size"),
                                                   new PointerMember(debugger, name, ".m_first"),
                                                   new PointerMember(debugger, name, ".m_buff"),
                                                   new PointerMembersDistance(debugger, name, ".m_first", ".m_end"))
                         : null;
                }
            }

            private BoostCircularBuffer(SizeMember sizeMember,
                                        PointerMember firstMember,
                                        PointerMember buffMember,
                                        PointerMembersDistance firstEndDistance)
            {
                this.sizeMember = sizeMember;
                this.firstMember = firstMember;
                this.buffMember = buffMember;
                this.firstEndDistance = firstEndDistance;
            }

            public override int LoadSize(Debugger debugger, string name)
            {
                return sizeMember.LoadParsed(debugger, name);
            }

            public override Size LoadSize(MemoryReader mreader, ulong address)
            {
                return sizeMember.LoadMemory(mreader, address);
            }

            public override void ElementInfo(string name, string type,
                                             out string elemName, out string elemType)
            {
                elemType = Util.Tparam(type, 0);
                elemName = "(*(" + name + ".m_first))";
            }

            public override bool ForEachElement(Debugger debugger, string name, ElementPredicate elementPredicate)
            {
                int size = sizeMember.LoadParsed(debugger, name);
                int size_fe = firstEndDistance.LoadParsed(debugger, name);
                int size1, size2;
                CalculateSizes(size, size_fe, out size1, out size2);
                
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

            public override bool ForEachMemoryBlock<T>(MemoryReader mreader, ulong address, int sizeExt,
                                                       MemoryReader.Converter<T> elementConverter,
                                                       MemoryBlockPredicate<T> memoryBlockPredicate)
            {
                Size s = this.sizeMember.LoadMemory(mreader, address);
                int size = s.IsValid ? s : sizeExt;
                if (size <= 0)
                    return true;

                int size_fe = firstEndDistance.LoadMemory(mreader, address);
                // TODO: Check sizes at this point?
                int size1, size2;
                CalculateSizes(size, size_fe, out size1, out size2);
                if (size1 <= 0)
                    return false;

                {
                    ulong firstAddress = firstMember.LoadMemory(mreader, address);
                    if (firstAddress == 0)
                        return false;
                    var blockConverter = new MemoryReader.ArrayConverter<T>(elementConverter, size1);
                    T[] values = new T[blockConverter.ValueCount()];
                    if (!mreader.Read(firstAddress, values, blockConverter))
                        return false;
                    if (!memoryBlockPredicate(values))
                        return false;
                }

                if (size2 > 0)
                {
                    ulong buffAddress = buffMember.LoadMemory(mreader, address);
                    if (buffAddress == 0)
                        return false;
                    var blockConverter = new MemoryReader.ArrayConverter<T>(elementConverter, size2);
                    T[] values = new T[blockConverter.ValueCount()];
                    if (!mreader.Read(buffAddress, values, blockConverter))
                        return false;
                    if (!memoryBlockPredicate(values))
                        return false;
                }

                return true;
            }

            private void CalculateSizes(int size, int size_fe, out int size1, out int size2)
            {
                size1 = size;
                size2 = 0;
                if (size > size_fe)
                {
                    size1 = size_fe;
                    size2 = size - size_fe;
                }
            }

            SizeMember sizeMember;
            PointerMember firstMember;
            PointerMember buffMember;
            PointerMembersDistance firstEndDistance;
        }

        class StdVector : ContiguousContainer
        {
            public class LoaderCreator : ExpressionLoader.LoaderCreator
            {
                public bool IsUserDefined() { return false; }
                public Kind Kind() { return ExpressionLoader.Kind.Container; }
                public Loader Create(Loaders loaders, Debugger debugger, string name, string type, string id)
                {
                    return id == "std::vector"
                         ? new StdVector(debugger, name)
                         : null;
                }
            }

            private StdVector(Debugger debugger, string name)
            {
                string name12 = name + "._Myfirst";
                //string name14_15 = name + "._Mypair._Myval2._Myfirst";

                if (debugger.GetExpression(name12).IsValidValue)
                    version = Version.Msvc12;

                if (version == Version.Msvc12)
                {
                    first = new PointerMember(debugger, name, "._Myfirst");
                    firstLastDist = new PointerMembersDistance(debugger, name, "._Myfirst", "._Mylast");
                }
                else
                {
                    first = new PointerMember(debugger, name, "._Mypair._Myval2._Myfirst");
                    firstLastDist = new PointerMembersDistance(debugger, name, "._Mypair._Myval2._Myfirst", "._Mypair._Myval2._Mylast");
                }
            }

            public override string RandomAccessElementName(string rawName, int i)
            {
                return FirstStr(rawName) + "[" + i + "]";
            }

            public override void ElementInfo(string name, string type,
                                             out string elemName, out string elemType)
            {
                elemType = Util.Tparam(type, 0);
                elemName = FirstStr(name) + "[0]";
            }

            public override int LoadSize(Debugger debugger, string name)
            {
                return firstLastDist.LoadParsed(debugger, name);
            }

            public override ulong MemoryBegin(MemoryReader mreader, ulong address)
            {
                return first.LoadMemory(mreader, address);
            }

            public override Size LoadSize(MemoryReader mreader, ulong address)
            {
                return firstLastDist.LoadMemory(mreader, address);
            }

            private string FirstStr(string name)
            {
                return version == Version.Msvc12
                     ? name + "._Myfirst"
                     : name + "._Mypair._Myval2._Myfirst";
            }

            private enum Version { Unknown, Msvc12, Msvc14_15 };
            private Version version = Version.Msvc14_15;

            PointerMember first;
            PointerMembersDistance firstLastDist;
        }

        class StdDeque : RandomAccessContainer
        {
            public class LoaderCreator : ExpressionLoader.LoaderCreator
            {
                public bool IsUserDefined() { return false; }
                public Kind Kind() { return ExpressionLoader.Kind.Container; }
                public Loader Create(Loaders loaders, Debugger debugger, string name, string type, string id)
                {
                    return id == "std::deque"
                         ? new StdDeque(debugger, name)
                         : null;
                }
            }

            public StdDeque(Debugger debugger, string name)
            {
                string name12 = name + "._Mysize";
                //string name14_15 = name + "._Mypair._Myval2._Mysize";

                if (debugger.GetExpression(name12).IsValidValue)
                    version = Version.Msvc12;

                // Block size
                if (!ExpressionParser.TryLoadInt(debugger, "((int)" + name + "._EEN_DS)", out dequeSize))
                    dequeSize = 0;

                if (version == Version.Msvc12)
                {
                    mapInfo = new TypeInfo(debugger, "(*(" + name + "._Map" + "))");
                    mapSize = new SizeMember(debugger, name, "._Mapsize");
                    map = new PointerMember(debugger, name, "._Map");
                    offset = new SizeMember(debugger, name, "._Myoff");
                    size = new SizeMember(debugger, name, "._Mysize");
                }
                else
                {
                    mapInfo = new TypeInfo(debugger, "(*(" + name + "._Mypair._Myval2._Map" + "))");
                    mapSize = new SizeMember(debugger, name, "._Mypair._Myval2._Mapsize");
                    map = new PointerMember(debugger, name, "._Mypair._Myval2._Map");
                    offset = new SizeMember(debugger, name, "._Mypair._Myval2._Myoff");
                    size = new SizeMember(debugger, name, "._Mypair._Myval2._Mysize");
                }
            }

            public override void ElementInfo(string name, string type,
                                             out string elemName, out string elemType)
            {
                elemType = Util.Tparam(type, 0);
                elemName = ElementStr(name, 0);
            }

            public override bool ForEachMemoryBlock<T>(MemoryReader mreader, ulong address, int sizeExt,
                                                    MemoryReader.Converter<T> elementConverter,
                                                    MemoryBlockPredicate<T> memoryBlockPredicate)
            {
                Size s = this.size.LoadMemory(mreader, address);
                int size = s.IsValid ? s : sizeExt;
                if (size <= 0)
                    return true;

                int mapSize = this.mapSize.LoadMemory(mreader, address);
                if (mapSize == 0)
                    return true; // false?

                ulong mapAddress = this.map.LoadMemory(mreader, address);
                if (mapAddress == 0)
                    return false;

                if (!mapInfo.IsValid)
                    return false;

                // Map - array of pointers                
                ulong[] pointers = new ulong[mapSize];
                if (! mreader.ReadPointerArray(mapAddress, mapInfo.Type, mapInfo.Size, pointers))
                    return false;

                if (this.dequeSize == 0)
                    return false;

                // Offset
                int offset = this.offset.LoadMemory(mreader, address);
                if (offset < 0)
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
                    ulong ptr = pointers[blockIndex];
                    if (ptr != 0) // just in case
                    {
                        int elemIndex = (i == 0)
                                        ? firstElement
                                        : 0;
                        int blockSize = dequeSize - elemIndex;
                        if (i == blocksCount - 1) // last block
                            blockSize -= dequeSize - (backElement + 1);
                            
                        if (blockSize > 0) // just in case
                        {
                            MemoryReader.ArrayConverter<T>
                                arrayConverter = new MemoryReader.ArrayConverter<T>(elementConverter, blockSize);
                            if (arrayConverter == null)
                                return false;

                            int valuesCount = elementConverter.ValueCount() * blockSize;
                            ulong firstAddress = ptr + (ulong)(elemIndex * elementConverter.ByteSize());

                            T[] values = new T[valuesCount];
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
                return size.LoadParsed(debugger, name);
            }

            public override Size LoadSize(MemoryReader mreader, ulong address)
            {
                return size.LoadMemory(mreader, address);
            }

            private string ElementStr(string name, int i)
            {
                return version == Version.Msvc12
                     ? name + "._Map[((" + i + " + " + name + "._Myoff) / " + name + "._EEN_DS) % " + name + "._Mapsize][(" + i + " + " + name + "._Myoff) % " + name + "._EEN_DS]"
                     : name + "._Mypair._Myval2._Map[((" + i + " + " + name + "._Mypair._Myval2._Myoff) / " + name + "._EEN_DS) % " + name + "._Mypair._Myval2._Mapsize][(" + i + " + " + name + "._Mypair._Myval2._Myoff) % " + name + "._EEN_DS]";
            }

            TypeInfo mapInfo;
            SizeMember mapSize;
            PointerMember map;
            SizeMember offset;
            SizeMember size;
            int dequeSize;

            private enum Version { Unknown, Msvc12, Msvc14_15 };
            private Version version = Version.Msvc14_15;
        }

        class StdList : ContainerLoader
        {
            public class LoaderCreator : ExpressionLoader.LoaderCreator
            {
                public bool IsUserDefined() { return false; }
                public Kind Kind() { return ExpressionLoader.Kind.Container; }
                public Loader Create(Loaders loaders, Debugger debugger, string name, string type, string id)
                {
                    return id == "std::list"
                         ? new StdList(debugger, name)
                         : null;
                }
            }

            public StdList(Debugger debugger, string name)
            {
                string name12 = name + "._Mysize";
                //string name14_15 = name + "._Mypair._Myval2._Mysize";

                if (debugger.GetExpression(name12).IsValidValue)
                    version = Version.Msvc12;

                if (version == Version.Msvc12)
                {
                    size = new SizeMember(debugger, name, "._Mysize");
                    head = new PointerMember(debugger, name, "._Myhead");
                }
                else
                {
                    size = new SizeMember(debugger, name, "._Mypair._Myval2._Mysize");
                    head = new PointerMember(debugger, name, "._Mypair._Myval2._Myhead");
                }

                string headStr = HeadStr(name);
                string headNodeName = "(*(" + headStr + "->_Next))";
                next = new PointerMember(debugger, headNodeName, "._Next");
                nextDiff = ExpressionParser.GetAddressDifference(debugger,
                                                                 headNodeName,
                                                                 headStr + "->_Next->_Next");
                valDiff = ExpressionParser.GetAddressDifference(debugger,
                                                                headNodeName,
                                                                headStr + "->_Next->_Myval");

            }

            public override void ElementInfo(string name, string type,
                                             out string elemName, out string elemType)
            {
                elemType = Util.Tparam(type, 0);
                elemName = HeadStr(name) + "->_Next->_Myval";
            }

            public override bool ForEachMemoryBlock<T>(MemoryReader mreader, ulong address, int sizeExt,
                                                       MemoryReader.Converter<T> elementConverter,
                                                       MemoryBlockPredicate<T> memoryBlockPredicate)
            {
                Size s = this.size.LoadMemory(mreader, address);
                int size = s.IsValid ? s : sizeExt;
                if (size <= 0)
                    return true;

                ulong headAddr = this.head.LoadMemory(mreader, address);
                if (headAddr == 0)
                    return false;

                if (ExpressionParser.IsInvalidAddressDifference(nextDiff)
                 || ExpressionParser.IsInvalidAddressDifference(valDiff)
                 || nextDiff < 0
                 || valDiff < 0)
                    return false;

                ulong next = 0;
                for (int i = 0; i < size; ++i)
                {
                    ulong ptr = next == 0
                              ? headAddr
                              : next + (ulong)nextDiff;
                    next = this.next.LoadMemory(mreader, ptr);
                    if (next == 0)
                        return false;

                    T[] values = new T[elementConverter.ValueCount()];
                    if (!mreader.Read(next + (ulong)valDiff, values, elementConverter))
                        return false;

                    if (!memoryBlockPredicate(values))
                        return false;
                }

                return true;
            }

            public override int LoadSize(Debugger debugger, string name)
            {
                return size.LoadParsed(debugger, name);
            }

            public override Size LoadSize(MemoryReader mreader, ulong address)
            {
                return size.LoadMemory(mreader, address);
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

            SizeMember size;
            PointerMember head;
            PointerMember next;
            long nextDiff;
            long valDiff;

            private enum Version { Unknown, Msvc12, Msvc14_15 };
            private Version version = Version.Msvc14_15;
        }

        // TODO: limit number of processed values with size
        //       in case some pointers were invalid
        class StdSet : ContainerLoader
        {
            public class LoaderCreator : ExpressionLoader.LoaderCreator
            {
                public bool IsUserDefined() { return false; }
                public Kind Kind() { return ExpressionLoader.Kind.Container; }
                public Loader Create(Loaders loaders, Debugger debugger, string name, string type, string id)
                {
                    return id == "std::set"
                         ? new StdSet(debugger, name)
                         : null;
                }
            }

            public StdSet(Debugger debugger, string name)
            {
                string name12 = name + "._Mysize";
                //string name14_15 = name + "._Mypair._Myval2._Myval2._Mysize";

                if (debugger.GetExpression(name12).IsValidValue)
                    version = Version.Msvc12;

                string headStr;
                if (version == Version.Msvc12)
                {
                    headStr = name + "._Myhead";
                    size = new SizeMember(debugger, name, "._Mysize");
                    head = new PointerMember(debugger, name, "._Myhead");
                }
                else
                {
                    headStr = name + "._Mypair._Myval2._Myval2._Myhead";
                    size = new SizeMember(debugger, name, "._Mypair._Myval2._Myval2._Mysize");
                    head = new PointerMember(debugger, name, "._Mypair._Myval2._Myval2._Myhead");
                }

                string nodeName = headStr + "->_Parent";
                string leftName = nodeName + "->_Left";
                string rightName = nodeName + "->_Right";
                string isNilName = nodeName + "->_Isnil";
                string valName = nodeName + "->_Myval";

                nodePtrInfo = new TypeInfo(debugger, nodeName);

                parentDiff = ExpressionParser.GetAddressDifference(debugger, "(*" + headStr + ")", nodeName);
                leftDiff = ExpressionParser.GetAddressDifference(debugger, "(*" + nodeName + ")", leftName);
                rightDiff = ExpressionParser.GetAddressDifference(debugger, "(*" + nodeName + ")", rightName);
                isNilDiff = ExpressionParser.GetAddressDifference(debugger, "(*" + nodeName + ")", isNilName);
                valDiff = ExpressionParser.GetAddressDifference(debugger, "(*" + nodeName + ")", valName);
                
            }

            public override void ElementInfo(string name, string type,
                                             out string elemName, out string elemType)
            {
                elemType = Util.Tparam(type, 0);
                elemName = HeadStr(name) + "->_Myval";
            }

            public override bool ForEachMemoryBlock<T>(MemoryReader mreader, ulong address, int sizeExt,
                                                       MemoryReader.Converter<T> elementConverter,
                                                       MemoryBlockPredicate<T> memoryBlockPredicate)
            {
                Size s = this.size.LoadMemory(mreader, address);
                int size = s.IsValid ? s : sizeExt;
                if (size <= 0)
                    return true;

                if (!nodePtrInfo.IsValid)
                    return false;

                MemoryReader.ValueConverter<byte, byte> boolConverter = new MemoryReader.ValueConverter<byte, byte>();
                MemoryReader.ValueConverter<ulong> ptrConverter = mreader.GetPointerConverter(nodePtrInfo.Type, nodePtrInfo.Size);
                if (ptrConverter == null)
                    return false;

                if (ExpressionParser.IsInvalidAddressDifference(parentDiff)
                 || ExpressionParser.IsInvalidAddressDifference(leftDiff)
                 || ExpressionParser.IsInvalidAddressDifference(rightDiff)
                 || ExpressionParser.IsInvalidAddressDifference(isNilDiff)
                 || ExpressionParser.IsInvalidAddressDifference(valDiff)
                 || parentDiff < 0 || leftDiff < 0 || rightDiff < 0 || isNilDiff < 0 || valDiff < 0)
                    return false;

                ulong headAddr = this.head.LoadMemory(mreader, address);
                if (headAddr == 0)
                    return false;

                ulong[] nodeAddr = new ulong[1];
                if (!mreader.Read(headAddr + (ulong)parentDiff, nodeAddr, ptrConverter))
                    return false;

                return ForEachMemoryBlockRecursive(mreader, elementConverter, memoryBlockPredicate,
                                                   boolConverter, ptrConverter,
                                                   nodeAddr[0], leftDiff, rightDiff, isNilDiff, valDiff);
            }

            private bool ForEachMemoryBlockRecursive<T>(MemoryReader mreader,
                                                        MemoryReader.Converter<T> elementConverter,
                                                        MemoryBlockPredicate<T> memoryBlockPredicate,
                                                        MemoryReader.ValueConverter<byte, byte> boolConverter,
                                                        MemoryReader.ValueConverter<ulong> ptrConverter,
                                                        ulong nodeAddr,
                                                        long leftDiff, long rightDiff,
                                                        long isNilDiff, long valDiff)
                where T : struct
            {
                byte[] isNil = new byte[1];
                if (!mreader.Read(nodeAddr + (ulong)isNilDiff, isNil, boolConverter))
                    return false;
                if (isNil[0] == 0) // _Isnil == false
                {
                    ulong[] leftAddr = new ulong[1];
                    ulong[] rightAddr = new ulong[1];
                    T[] values = new T[elementConverter.ValueCount()];

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
                return size.LoadParsed(debugger, name);
            }

            public override Size LoadSize(MemoryReader mreader, ulong address)
            {
                return size.LoadMemory(mreader, address);
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

            SizeMember size;
            PointerMember head;
            TypeInfo nodePtrInfo;
            long parentDiff;
            long leftDiff;
            long rightDiff;
            long isNilDiff;
            long valDiff;

            private enum Version { Unknown, Msvc12, Msvc14_15 };
            private Version version = Version.Msvc14_15;
        }

        class CSArray : ContiguousContainer
        {
            public class LoaderCreator : ExpressionLoader.LoaderCreator
            {
                public bool IsUserDefined() { return false; }
                public Kind Kind() { return ExpressionLoader.Kind.Container; }
                public Loader Create(Loaders loaders, Debugger debugger, string name, string type, string id)
                {
                    return IsCSArrayType(type)
                         ? new CSArray()
                         : null;
                }
            }

            public override void ElementInfo(string name, string type,
                                             out string elemName, out string elemType)
            {
                elemType = ElemTypeFromType(type);
                elemName = name + "[0]";
            }

            public override int LoadSize(Debugger debugger, string name)
            {
                return ExpressionParser.LoadSize(debugger, name + ".Length");
            }

            public override ulong MemoryBegin(MemoryReader mreader, ulong address)
            {
                // TODO
                return 0;
            }

            public override Size LoadSize(MemoryReader mreader, ulong address)
            {
                return new Size();
            }

            static public bool IsCSArrayType(string type)
            {
                return type.EndsWith("[]");
            }

            // type -> name[]
            static private string ElemTypeFromType(string type)
            {
                return type.EndsWith("[]")
                     ? type.Substring(0, type.Length - 2)
                     : "";
            }
        }

        class CSList : ContiguousContainer
        {
            public class LoaderCreator : ExpressionLoader.LoaderCreator
            {
                public bool IsUserDefined() { return false; }
                public Kind Kind() { return ExpressionLoader.Kind.Container; }
                public Loader Create(Loaders loaders, Debugger debugger, string name, string type, string id)
                {
                    return id == "System.Collections.Generic.List"
                         ? new CSList()
                         : null;
                }
            }

            public override int LoadSize(Debugger debugger, string name)
            {
                return ExpressionParser.LoadSize(debugger, name + ".Count");
            }

            public override ulong MemoryBegin(MemoryReader mreader, ulong address)
            {
                // TODO
                return 0;
            }

            public override Size LoadSize(MemoryReader mreader, ulong address)
            {
                return new Size();
            }

            public override string RandomAccessElementName(string rawName, int i)
            {
                return rawName + "._items[" + i + "]";
            }

            public override void ElementInfo(string name, string type,
                                             out string elemName, out string elemType)
            {
                elemType = Util.Tparam(type, 0);
                elemName = name + "._items[0]";
            }

            public override bool ForEachMemoryBlock<T>(MemoryReader mreader, Debugger debugger,
                                                       string name, string type, ulong address,
                                                       MemoryReader.Converter<T> elementConverter,
                                                       MemoryBlockPredicate<T> memoryBlockPredicate)
            {
                Expression expr = debugger.GetExpression(name + "._items");
                if (!expr.IsValidValue || !CSArray.IsCSArrayType(expr.Type))
                {
                    return false;
                }

                return base.ForEachMemoryBlock(mreader, debugger,
                                               name, type, address,
                                               elementConverter, memoryBlockPredicate);
            }
        }

        // This approach would work for any C# base class
        class CSIList : ContiguousContainer
        {
            public class LoaderCreator : ExpressionLoader.LoaderCreator
            {
                public bool IsUserDefined() { return false; }
                public Kind Kind() { return ExpressionLoader.Kind.Container; }
                public Loader Create(Loaders loaders, Debugger debugger, string name, string type, string id)
                {
                    if (id != "System.Collections.Generic.IList")
                        return null;

                    string derivedType = Util.CSDerivedType(type);

                    if (Util.Empty(derivedType))
                        return null;

                    ContainerLoader loader = loaders.FindByType(ExpressionLoader.Kind.Container,
                                                                DerivedName(derivedType, name),
                                                                derivedType) as ContainerLoader;
                    return loader != null
                         ? new CSIList(loader, derivedType)
                         : null;
                }
            }

            private CSIList(ContainerLoader loader, string derivedType)
            {
                this.loader = loader;
                this.derivedType = derivedType;
            }

            public override int LoadSize(Debugger debugger, string name)
            {
                return loader.LoadSize(debugger, DerivedName(derivedType, name));
            }

            public override ulong MemoryBegin(MemoryReader mreader, ulong address)
            {
                ContiguousContainer l = loader as ContiguousContainer;
                return l != null
                     ? l.MemoryBegin(mreader, address)
                     : 0;
            }

            public override Size LoadSize(MemoryReader mreader, ulong address)
            {
                return loader.LoadSize(mreader, address);
            }

            public override string RandomAccessElementName(string rawName, int i)
            {
                // TODO: derived name?
                // TODO: What to do with other kinds of containers?
                RandomAccessContainer l = loader as RandomAccessContainer;
                return l != null
                     ? l.RandomAccessElementName(DerivedName(derivedType, rawName), i)
                     : "";
            }

            public override void ElementInfo(string name, string type,
                                             out string elemName, out string elemType)
            {
                loader.ElementInfo(DerivedName(derivedType, name),
                                   derivedType,
                                   out elemName, out elemType);
            }

            public override bool ForEachMemoryBlock<T>(MemoryReader mreader, Debugger debugger,
                                                       string name, string type, ulong address,
                                                       MemoryReader.Converter<T> elementConverter,
                                                       MemoryBlockPredicate<T> memoryBlockPredicate)
            {
                // TODO: What to do with other kinds of containers?
                ContiguousContainer l = loader as ContiguousContainer;
                return l != null
                     ? l.ForEachMemoryBlock(mreader, debugger,
                                            // TODO: is the name and type ok below?
                                            DerivedName(derivedType, name), derivedType,
                                            // TODO: what about the address?
                                            0,
                                            elementConverter, memoryBlockPredicate)
                     : false;
            }

            static string DerivedName(string derivedType, string name)
            {
                return "((" + derivedType + ")" + name + ")";
            }

            string derivedType = "";
            ContainerLoader loader = null;
        }

        class CSLinkedList : ContainerLoader
        {
            public class LoaderCreator : ExpressionLoader.LoaderCreator
            {
                public bool IsUserDefined() { return false; }
                public Kind Kind() { return ExpressionLoader.Kind.Container; }
                public Loader Create(Loaders loaders, Debugger debugger, string name, string type, string id)
                {
                    return id == "System.Collections.Generic.LinkedList"
                         ? new CSLinkedList()
                         : null;
                }
            }

            public override void ElementInfo(string name, string type,
                                             out string elemName, out string elemType)
            {
                elemType = Util.Tparam(type, 0);
                elemName = name + ".head.item";
            }

            public override int LoadSize(Debugger debugger, string name)
            {
                return ExpressionParser.LoadSize(debugger, name + ".count");
            }

            public override Size LoadSize(MemoryReader mreader, ulong address)
            {
                return new Size();
            }

            public override bool ForEachMemoryBlock<T>(MemoryReader mreader, Debugger debugger,
                                                       string name, string type, ulong dummyAddress,
                                                       MemoryReader.Converter<T> elementConverter,
                                                       MemoryBlockPredicate<T> memoryBlockPredicate)
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
                    T[] values = new T[elementConverter.ValueCount()];
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

            public override bool ForEachMemoryBlock<T>(MemoryReader mreader, ulong address, int size,
                                                       MemoryReader.Converter<T> elementConverter,
                                                       MemoryBlockPredicate<T> memoryBlockPredicate)
            {
                // TODO
                return false;
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
