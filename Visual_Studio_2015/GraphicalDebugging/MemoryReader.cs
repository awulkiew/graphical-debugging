//------------------------------------------------------------------------------
// <copyright file="MemoryReader.cs">
//     Copyright (c) Adam Wulkiewicz.
// </copyright>
//------------------------------------------------------------------------------

using EnvDTE;
using Microsoft.VisualStudio.Debugger;

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Globalization;

namespace GraphicalDebugging
{
    class MemoryReader
    {
        public abstract class Converter<ValueType>
            where ValueType : struct
        {
            abstract public int ValueCount();
            abstract public int ByteSize();
            abstract public void Copy(byte[] bytes, int bytesOffset, ValueType[] result, int resultOffset);
            public void Copy(byte[] bytes, ValueType[] result)
            {
                this.Copy(bytes, 0, result, 0);
            }

            virtual public bool IsValueConverter()
            {
                return false;
            }
        }

        public abstract class ValueConverter<ValueType> : Converter<ValueType>
            where ValueType : struct
        {
            abstract public void Copy(byte[] bytes, int bytesOffset, ValueType[] result, int resultOffset, int resultCount);

            public override int ValueCount()
            {
                return 1;
            }

            public override void Copy(byte[] bytes, int bytesOffset, ValueType[] result, int resultOffset)
            {
                this.Copy(bytes, bytesOffset, result, resultOffset, 1);
            }

            public override bool IsValueConverter()
            {
                return true;
            }
        }

        public class ValueConverter<ValueType, T> : ValueConverter<ValueType>
            where ValueType : struct
            where T : struct
        {
            private static int sizeOfT = Marshal.SizeOf(default(T));
            
            public override int ByteSize() { return sizeOfT; }

            public override void Copy(byte[] bytes, int bytesOffset, ValueType[] result, int resultOffset, int resultCount)
            {
                if (typeof(T) == typeof(ValueType))
                {
                    Buffer.BlockCopy(bytes, bytesOffset, result, resultOffset * sizeOfT, resultCount * sizeOfT);
                }
                else
                {
                    T[] tmp = new T[resultCount];
                    Buffer.BlockCopy(bytes, bytesOffset, tmp, 0, resultCount * sizeOfT);
                    Array.Copy(tmp, 0, result, resultOffset, resultCount);
                }
            }
        }

        public class ArrayConverter<ValueType> : Converter<ValueType>
            where ValueType : struct
        {
            public ArrayConverter(Converter<ValueType> elementConverter, int count)
            {
                this.count = count;
                this.elementConverter = elementConverter;
            }

            public override int ValueCount()
            {
                return count * elementConverter.ValueCount();
            }

            public override int ByteSize()
            {
                return count * elementConverter.ByteSize();
            }

            public override void Copy(byte[] bytes, int byteOffset, ValueType[] result, int resultOffset)
            {
                if (elementConverter.IsValueConverter())
                {
                    var valueConverter = elementConverter as ValueConverter<ValueType>;
                    valueConverter.Copy(bytes, byteOffset, result, resultOffset, count);
                }
                else
                {
                    int bOff = byteOffset;
                    int vOff = resultOffset;
                    int bOffStep = elementConverter.ByteSize();
                    int vOffStep = elementConverter.ValueCount();
                    for (int i = 0; i < count; ++i)
                    {
                        elementConverter.Copy(bytes, bOff, result, vOff);
                        bOff += bOffStep;
                        vOff += vOffStep;
                    }
                }
            }

            protected int count = 0;
            protected Converter<ValueType> elementConverter = null;
        }
        
        public class Member
        {
            public Member(Converter<double> converter)
            {
                this.Converter = converter;
                this.ByteOffset = 0;
            }

            public Member(Converter<double> converter, int byteOffset)
            {
                this.Converter = converter;
                this.ByteOffset = byteOffset;
            }

            public Converter<double> Converter { get; private set; }
            public int ByteOffset { get; private set; }
        }

        public class StructConverter : Converter<double>
        {
            public StructConverter(int byteSize, Member member)
            {
                this.members = new[] {member};
                initialize(byteSize);               
            }

            public StructConverter(int byteSize, Member member1, Member member2)
            {
                this.members = new[] {member1, member2};
                initialize(byteSize);
            }

            public override int ValueCount()
            {
                return internalValueCount;
            }

            public override int ByteSize()
            {
                return byteSize;
            }
            
            public override void Copy(byte[] bytes, int bytesOffset, double[] result, int resultOffset)
            {
                // TODO: Copy in one block if possible
                // if offsets and sizes defined in valueConverters create contigeous block

                int vOff = 0;
                foreach (Member member in members)
                {
                    member.Converter.Copy(bytes, bytesOffset + member.ByteOffset,
                                          result, resultOffset + vOff);
                    vOff += member.Converter.ValueCount();
                }
            }

            private void initialize(int byteSize)
            {
                this.byteSize = byteSize;

                this.internalValueCount = 0;
                foreach (Member m in members)
                {
                    this.internalValueCount += m.Converter.ValueCount();
                }
            }

            Member[] members = null;
            int byteSize = 0;

            int internalValueCount = 0;
        }

        public static ValueConverter<double> GetNumericConverter(Debugger debugger, string ptrName, string valType)
        {
            if (valType == null)
                valType = GetValueType(debugger, ptrName);
            int valSize = GetValueTypeSizeof(debugger, valType);

            if (valType == null || valSize == 0)
                return null;

            if (valType == "double")
            {
                return new ValueConverter<double, double>();
            }
            else if (valType == "float")
            {
                return new ValueConverter<double, float>();
            }
            else if (valType == "int"
                  || valType == "long"
                  || valType == "__int64"
                  || valType == "short"
                  || valType == "char")
            {
                if (valSize == 4)
                    return new ValueConverter<double, int>();
                else if (valSize == 8)
                    return new ValueConverter<double, long>();
                else if (valSize == 2)
                    return new ValueConverter<double, short>();
                else if (valSize == 1)
                    return new ValueConverter<double, sbyte>();
            }
            else if (valType == "unsigned short"
                  || valType == "unsigned int"
                  || valType == "unsigned long"
                  || valType == "unsigned __int64"
                  || valType == "unsigned char")
            {
                if (valSize == 4 && sizeof(uint) == 4)
                    return new ValueConverter<double, uint>();
                else if (valSize == 8 && sizeof(ulong) == 8)
                    return new ValueConverter<double, ulong>();
                else if (valSize == 2)
                    return new ValueConverter<double, ushort>();
                else if (valSize == 1)
                    return new ValueConverter<double, byte>();
            }

            return null;
        }

        public static ValueConverter<ulong> GetPointerConverter(Debugger debugger, string ptrName, string valType)
        {
            if (valType == null)
                valType = GetValueType(debugger, ptrName);
            int valSize = GetValueTypeSizeof(debugger, valType);

            if (valType == null || valSize == 0)
                return null;

            if (! valType.EndsWith("*"))
                return null;

            if (valSize == 4)
                return new ValueConverter<ulong, uint>();
            else if (valSize == 8)
                return new ValueConverter<ulong, ulong>();

            return null;
        }

        public static ArrayConverter<double> GetNumericArrayConverter(Debugger debugger, string ptrName, string valType, int size)
        {
            ValueConverter<double> valueConverter = GetNumericConverter(debugger, ptrName, valType);
            return valueConverter == null
                 ? null
                 : new ArrayConverter<double>(valueConverter, size);
        }

        public static ArrayConverter<ulong> GetPointerArrayConverter(Debugger debugger, string ptrName, string valType, int size)
        {
            ValueConverter<ulong> pointerConverter = GetPointerConverter(debugger, ptrName, valType);
            return pointerConverter == null
                 ? null
                 : new ArrayConverter<ulong>(pointerConverter, size);
        }

        public static bool Read<ValueType>(Debugger debugger, string ptrName,
                                           ValueType[] values,
                                           Converter<ValueType> converter)
            where ValueType : struct
        {
            if (converter.ValueCount() != values.Length)
                throw new ArgumentOutOfRangeException("values.Length");

            int byteSize = converter.ByteSize();
            if (byteSize < 1)
                return true;
            byte[] bytes = new byte[byteSize];
            bool ok = ReadBytes(debugger, ptrName, bytes);
            if (!ok)
                return false;

            converter.Copy(bytes, values);

            return true;
        }

        public static bool Read<ValueType>(Debugger debugger, ulong address,
                                           ValueType[] values,
                                           Converter<ValueType> converter)
            where ValueType : struct
        {
            if (converter.ValueCount() != values.Length)
                throw new ArgumentOutOfRangeException("values.Length");

            int byteSize = converter.ByteSize();
            if (byteSize < 1)
                return true;
            byte[] bytes = new byte[byteSize];
            bool ok = ReadBytes(debugger, address, bytes);
            if (!ok)
                return false;

            converter.Copy(bytes, values);

            return true;
        }

        public static bool ReadNumericArray(Debugger debugger, string ptrName, double[] values)
        {
            int count = values.Length;
            if (count < 1)
                return true;

            ArrayConverter<double> converter = GetNumericArrayConverter(debugger, ptrName, null, count);
            if (converter == null)
                return false;

            return Read(debugger, ptrName, values, converter);
        }

        public static bool ReadPointerArray(Debugger debugger, string ptrName, ulong[] values)
        {
            int count = values.Length;
            if (count < 1)
                return true;

            ArrayConverter<ulong> converter = GetPointerArrayConverter(debugger, ptrName, null, count);
            if (converter == null)
                return false;

            return Read(debugger, ptrName, values, converter);
        }

        public static bool ReadBytes(Debugger debugger, string ptrName, byte[] buffer)
        {
            ulong address = GetValueAddress(debugger, ptrName);
            if (address == 0)
                return false;

            return ReadBytes(debugger, address, buffer);
        }

        public static bool ReadBytes(Debugger debugger, ulong address, byte[] buffer)
        {
            if (buffer.Length < 1)
                return true;

            DkmProcess proc = GetDebuggedProcess(debugger);
            if (proc == null)
                return false;

            int bytesRead = proc.ReadMemory(address, DkmReadMemoryFlags.None, buffer);
            return bytesRead == buffer.Length;
        }

        public static DkmProcess GetDebuggedProcess(Debugger debugger)
        {
            DkmProcess[] procs = DkmProcess.GetProcesses();
            if (procs.Length == 1)
            {
                return procs[0];
            }
            else if (procs.Length > 1)
            {
                foreach (DkmProcess proc in procs)
                {
                    if (proc.Path == debugger.CurrentProcess.Name)
                        return proc;
                }
            }
            return null;
        }

        public static bool IsInvalidAddressDifference(long diff)
        {
            return diff == long.MinValue;
        }

        public static long GetAddressDifference(Debugger debugger, string ptrName1, string ptrName2)
        {
            ulong addr1 = GetValueAddress(debugger, ptrName1);
            ulong addr2 = GetValueAddress(debugger, ptrName2);
            if (addr1 == 0 || addr2 == 0)
                return long.MinValue;
            return (addr2 >= addr1)
                 ? (long)(addr2 - addr1)
                 : -(long)(addr1 - addr2);
        }

        public static ulong GetValueAddress(Debugger debugger, string ptrName)
        {
            Expression ptrExpr = debugger.GetExpression("(void*)&(*(" + ptrName + "))");
            if (!ptrExpr.IsValidValue)
                return 0;
            string addr = ptrExpr.Value;

            return addr.StartsWith("0x", StringComparison.CurrentCultureIgnoreCase)
                 ? ulong.Parse(addr.Substring(2), NumberStyles.HexNumber)
                 : ulong.Parse(addr);
        }

        // Valid size or 0
        // NOTE: The actual byte size depends on sizeof(char)
        public static int GetValueSizeof(Debugger debugger, string ptrName)
        {
            Expression valSizeExpr = debugger.GetExpression("sizeof(*(" + ptrName + "))");
            return valSizeExpr.IsValidValue
                 ? int.Parse(valSizeExpr.Value)
                 : 0;
        }

        // Valid size or 0
        public static int GetValueTypeSizeof(Debugger debugger, string valType)
        {
            Expression valSizeExpr = debugger.GetExpression("sizeof(" + valType + ")");
            return valSizeExpr.IsValidValue
                 ? int.Parse(valSizeExpr.Value)
                 : 0;
        }

        // Valid name or null
        public static string GetValueType(Debugger debugger, string ptrName)
        {
            Expression valExpr = debugger.GetExpression("*(" + ptrName + ")");
            return valExpr.IsValidValue
                 ? valExpr.Type
                 : null;
        }

        /*
        private static void Convert(byte[] byteBuffer, double[] doubleBuffer)
        {
            System.Diagnostics.Debug.Assert(byteBuffer.Length == doubleBuffer.Length * sizeof(double));

            System.Buffer.BlockCopy(byteBuffer, 0, doubleBuffer, 0, byteBuffer.Length);
        }

        private static void Convert(byte[] byteBuffer, List<double> doubleBuffer)
        {
            System.Diagnostics.Debug.Assert(byteBuffer.Length == doubleBuffer.Count * sizeof(double));

            for (int i = 0; i < doubleBuffer.Count; ++i)
                doubleBuffer[i] = BitConverter.ToDouble(byteBuffer, i * sizeof(double));
        }
        */
    }
}
