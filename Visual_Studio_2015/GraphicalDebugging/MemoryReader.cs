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

namespace GraphicalDebugging
{
    class MemoryReader
    {
        public abstract class Converter
        {
            abstract public int Count();
            abstract public int ValueCount();
            abstract public int ByteOffset();
            abstract public int ByteSize();
            abstract public void Copy(byte[] bytes, int bytesOffset, double[] result, int resultOffset);
            public void Copy(byte[] bytes, double[] result)
            {
                this.Copy(bytes, 0, result, 0);
            }
        }

        public class NumericConverter<T> : Converter
            where T : struct
        {
            private static int sizeOfValue = Marshal.SizeOf(default(T));

            public NumericConverter(int count, int byteOffset = 0)
            {
                if (byteOffset < 0)
                    throw new ArgumentOutOfRangeException("byteOffset");

                this.count = count;
                this.byteOffset = byteOffset;
            }

            public override int Count()
            {
                return count;
            }

            public override int ValueCount()
            {
                return count;
            }

            public override int ByteSize()
            {
                return count * sizeOfValue;
            }

            public override int ByteOffset()
            {
                return byteOffset;
            }

            public override void Copy(byte[] bytes, int baseOffset, double[] result, int resultOffset)
            {
                int offset = baseOffset + byteOffset;
                if (typeof(T) != typeof(double))
                {
                    T[] tmp = new T[count];
                    Buffer.BlockCopy(bytes, offset, tmp, 0, ByteSize());
                    Array.Copy(tmp, 0, result, resultOffset, count);
                }
                else
                {
                    Buffer.BlockCopy(bytes, offset, result, resultOffset * sizeOfValue, ByteSize());
                }
            }
            
            int count = 0;
            int byteOffset = 0;
        }

        public class WrappingConverter : Converter
        {
            // offset of wrapper == 0
            // internal offset set in converter
            public WrappingConverter(Converter converter, int count, int sizeOfWrapper)
            {
                if (sizeOfWrapper < 0
                 || sizeOfWrapper < converter.ByteOffset() + converter.ByteSize())
                    throw new ArgumentOutOfRangeException("sizeOfWrapper");

                this.converter = converter;
                this.count = count;
                this.sizeOfWrapper = sizeOfWrapper;
            }

            public override int Count()
            {
                return count;
            }

            public override int ValueCount()
            {
                return count * converter.ValueCount();
            }

            public override int ByteSize()
            {
                return count * sizeOfWrapper;
            }

            public override int ByteOffset()
            {
                return 0;
            }

            public override void Copy(byte[] bytes, int bytesOffset, double[] result, int resultOffset)
            {
                // TODO: Copy in one block if possible
                // second condition for safety
                //if (sizeOfWrapper == converter.ByteSize() && converter.ByteOffset() == 0)

                for (int i = 0; i < count; ++i)
                {
                    // internal offset set in converter
                    converter.Copy(bytes, bytesOffset + i * sizeOfWrapper, result, resultOffset + i * converter.Count());
                }
            }

            Converter converter;
            int count = 0;
            int sizeOfWrapper = 0;
        }

        public static Converter GetNumericConverter(Debugger debugger, string ptrName, string valType, int size, int byteOffset = 0)
        {
            if (valType == null)
                valType = GetValueType(debugger, ptrName);
            int valSize = GetValueTypeSizeof(debugger, valType);

            if (valType == null || valSize == 0)
                return null;
            
            if (valType == "double")
            {
                return new NumericConverter<double>(size, byteOffset);
            }
            else if (valType == "float")
            {
                return new NumericConverter<float>(size, byteOffset);
            }
            else if (valType == "int"
                  || valType == "long"
                  || valType == "__int64"
                  || valType == "short"
                  || valType == "char")
            {
                if (valSize == 4)
                    return new NumericConverter<int>(size, byteOffset);
                else if (valSize == 8)
                    return new NumericConverter<long>(size, byteOffset);
                else if (valSize == 2)
                    return new NumericConverter<short>(size, byteOffset);
                else if (valSize == 1)
                    return new NumericConverter<sbyte>(size, byteOffset);
            }
            else if (valType == "unsigned short"
                  || valType == "unsigned int"
                  || valType == "unsigned long"
                  || valType == "unsigned __int64"
                  || valType == "unsigned char")
            {
                if (valSize == 4 && sizeof(uint) == 4)
                    return new NumericConverter<uint>(size, byteOffset);
                else if (valSize == 8 && sizeof(ulong) == 8)
                    return new NumericConverter<ulong>(size, byteOffset);
                else if (valSize == 2)
                    return new NumericConverter<ushort>(size, byteOffset);
                else if (valSize == 1)
                    return new NumericConverter<byte>(size, byteOffset);
            }

            return null;
        }

        public static bool Read(Debugger debugger, string ptrName, double[] values, Converter converter)
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

        public static bool ReadNumericArray(Debugger debugger, string ptrName, string valType, double[] values)
        {
            int count = values.Length;
            if (count < 1)
                return true;

            Converter converter = GetNumericConverter(debugger, ptrName, valType, count);
            if (converter == null)
                return false;

            return Read(debugger, ptrName, values, converter);
        }

        public static bool ReadNumericArray(Debugger debugger, string ptrName, string valType, string innerPtrName, string innerValType, double[] values, int innerCount)
        {
            //System.Diagnostics.Debug.Assert(innerCount <= values.Length && values.Length % innerCount == 0);

            if (innerCount < 1)
                return true;

            int outerCount = values.Length / innerCount;
            if (outerCount < 1)
                return true;

            int addressOffset = (int)GetAddressDifference(debugger, ptrName, innerPtrName);
            if (addressOffset < 0)
                return false;

            Converter innerConverter = GetNumericConverter(debugger, innerPtrName, innerValType, innerCount, addressOffset);
            if (innerConverter == null)
                return false;

            int outerValSize = GetValueSizeof(debugger, ptrName, valType);

            Converter converter = new WrappingConverter(innerConverter, outerCount, outerValSize);

            return Read(debugger, ptrName, values, converter);
        }

        public static bool ReadBytes(Debugger debugger, string ptrName, byte[] buffer)
        {
            if (buffer.Length < 1)
                return true;

            //Process process = debugger.CurrentProcess;
            DkmProcess[] procs = DkmProcess.GetProcesses();
            if (procs.Length != 1)
                return false;
            DkmProcess proc = procs[0];
            // Alternatively somehow detect the correct process.
            // One possibility: debugger.CurrentProcess.Name == proc.Path

            ulong address = GetValueAddress(debugger, ptrName);
            if (address == 0)
                return false;

            int bytesRead = proc.ReadMemory(address, DkmReadMemoryFlags.None, buffer);
            return bytesRead == buffer.Length;
        }

        public static long GetAddressDifference(Debugger debugger, string ptrName1, string ptrName2)
        {
            ulong addr1 = GetValueAddress(debugger, ptrName1);
            ulong addr2 = GetValueAddress(debugger, ptrName2);
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
                 ? ulong.Parse(addr.Substring(2), System.Globalization.NumberStyles.HexNumber)
                 : ulong.Parse(addr);
        }

        // Valid size or 0
        // NOTE: The actual byte size depends on sizeof(char)
        public static int GetValueSizeof(Debugger debugger, string ptrName, string valType)
        {
            Expression valSizeExpr = valType != null
                                   ? debugger.GetExpression("sizeof(" + valType + ")")
                                   : debugger.GetExpression("sizeof(*(" + ptrName + "))");
            return valSizeExpr.IsValidValue
                 ? int.Parse(valSizeExpr.Value)
                 : 0;
        }

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
    }
}
