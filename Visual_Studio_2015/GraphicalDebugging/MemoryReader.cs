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
        public interface Buffer
        {
            int ByteSize();
            void Set(byte[] bytes);
        }

        public interface ValuesBuffer : Buffer
        {
            void FillArray(double[] result);
        }

        public class NumericBuffer<T> : ValuesBuffer
            where T : struct
        {
            private static int sizeOfValue = Marshal.SizeOf(default(T));

            public NumericBuffer(int size)
            {
                values = new T[size];
            }

            public int ByteSize()
            {
                return values.Length * sizeOfValue;
            }

            public void Set(byte[] bytes)
            {
                System.Diagnostics.Debug.Assert(bytes.Length == values.Length * sizeOfValue);
                System.Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length);
            }

            public void FillArray(double[] result)
            {
                System.Diagnostics.Debug.Assert(values.Length == result.Length);
                Array.Copy(values, result, values.Length);
            }

            /*public List<double> ToList()
            {
                List<double> result = new List<double>(values.Length);
                for (int i = 0; i < values.Length; ++i)
                    result.Add((double)(object)values[i]);
                return result;
            }*/

            private T[] values;
        }

        public static ValuesBuffer GetValuesBuffer(Debugger debugger, string ptrName, string valType, int size)
        {
            if (valType == null)
                valType = GetValueType(debugger, ptrName);
            int valSize = GetValueSizeof(debugger, ptrName, valType);

            if (valType == null || valSize == 0)
                return null;
            
            if (valType == "double")
            {
                return new NumericBuffer<double>(size);
            }
            else if (valType == "float")
            {
                return new NumericBuffer<float>(size);
            }
            else if (valType == "int"
                  || valType == "long"
                  || valType == "__int64"
                  || valType == "short"
                  || valType == "char")
            {
                if (valSize == 4)
                    return new NumericBuffer<int>(size);
                else if (valSize == 8)
                    return new NumericBuffer<long>(size);
                else if (valSize == 2)
                    return new NumericBuffer<short>(size);
                else if (valSize == 1)
                    return new NumericBuffer<sbyte>(size);
            }
            else if (valType == "unsigned short"
                  || valType == "unsigned int"
                  || valType == "unsigned long"
                  || valType == "unsigned __int64"
                  || valType == "unsigned char")
            {
                if (valSize == 4 && sizeof(uint) == 4)
                    return new NumericBuffer<uint>(size);
                else if (valSize == 8 && sizeof(ulong) == 8)
                    return new NumericBuffer<ulong>(size);
                else if (valSize == 2)
                    return new NumericBuffer<ushort>(size);
                else if (valSize == 1)
                    return new NumericBuffer<byte>(size);
            }

            return null;
        }

        public static bool Read(Debugger debugger, string ptrName, Buffer buffer)
        {
            int byteSize = buffer.ByteSize();
            if (byteSize < 1)
                return true;
            byte[] bytes = new byte[byteSize];
            bool ok = ReadBytes(debugger, ptrName, bytes);
            if (!ok)
                return false;
            buffer.Set(bytes);
            return true;
        }

        public static bool ReadNumericArray(Debugger debugger, string ptrName, string valType, double[] values)
        {
            int count = values.Length;
            if (count < 1)
                return true;

            ValuesBuffer buffer = GetValuesBuffer(debugger, ptrName, valType, count);
            if (buffer == null)
                return false;

            if (!Read(debugger, ptrName, buffer))
                return false;

            buffer.FillArray(values);
            
            return true;
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

            int bytesRead = proc.ReadMemory(address, DkmReadMemoryFlags.None, buffer);
            return bytesRead == buffer.Length;
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
