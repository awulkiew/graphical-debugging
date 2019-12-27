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
        
        public class Member<ValueType>
            where ValueType : struct
        {
            public Member(Converter<ValueType> converter)
            {
                this.Converter = converter;
                this.ByteOffset = 0;
            }

            public Member(Converter<ValueType> converter, int byteOffset)
            {
                this.Converter = converter;
                this.ByteOffset = byteOffset;
            }

            public Converter<ValueType> Converter { get; private set; }
            public int ByteOffset { get; private set; }
        }

        public class StructConverter<ValueType> : Converter<ValueType>
            where ValueType : struct
        {
            public StructConverter(int byteSize, Member<ValueType> member)
            {
                this.members = new[] {member};
                initialize(byteSize);               
            }

            public StructConverter(int byteSize, Member<ValueType> member1, Member<ValueType> member2)
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
            
            public override void Copy(byte[] bytes, int bytesOffset, ValueType[] result, int resultOffset)
            {
                // TODO: Copy in one block if possible
                // if offsets and sizes defined in valueConverters create contigeous block

                int vOff = 0;
                foreach (Member<ValueType> member in members)
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
                foreach (Member<ValueType> m in members)
                {
                    this.internalValueCount += m.Converter.ValueCount();
                }
            }

            Member<ValueType>[] members = null;
            int byteSize = 0;

            int internalValueCount = 0;
        }

        private bool IsSignedIntegralType(string valType)
        {
            if (language == Language.CS)
            {
                return valType == "int"
                    || valType == "long"
                    || valType == "short"
                    || valType == "sbyte";
            }
            else
            {
                return valType == "int"
                    || valType == "long"
                    || valType == "__int64"
                    || valType == "short"
                    || valType == "char" // TODO: this could actually depend on compiler flags
                    || valType == "signed int"
                    || valType == "signed long"
                    || valType == "signed __int64"
                    || valType == "signed short"
                    || valType == "signed char";
            }
        }

        private bool IsUnsignedIntegralType(string valType)
        {
            if (language == Language.CS)
            {
                return valType == "uint"
                    || valType == "ulong"
                    || valType == "ushort"
                    || valType == "char"
                    || valType == "byte";
            }
            else
            {
                return valType == "unsigned int"
                    || valType == "unsigned long"
                    || valType == "unsigned __int64"
                    || valType == "unsigned short"
                    || valType == "unsigned char";
            }
        }

        public ValueConverter<double> GetNumericConverter(string valName, string valType = null)
        {
            return GetNumericConverter<double>(valName, valType);
        }

        // 
        public ValueConverter<ValueType> GetNumericConverter<ValueType>(string valName, string valType = null)
            where ValueType : struct
        {
            if (valType == null)
                valType = ExpressionParser.GetValueType(debugger, valName);
            int valSize = ExpressionParser.GetTypeSizeof(debugger, valType);

            if (valType == null || valSize == 0)
                return null;

            if (valType == "double")
            {
                return new ValueConverter<ValueType, double>();
            }
            else if (valType == "float")
            {
                return new ValueConverter<ValueType, float>();
            }
            else if (IsSignedIntegralType(valType))
            {
                if (valSize == 4)
                    return new ValueConverter<ValueType, int>();
                else if (valSize == 8)
                    return new ValueConverter<ValueType, long>();
                else if (valSize == 2)
                    return new ValueConverter<ValueType, short>();
                else if (valSize == 1)
                    return new ValueConverter<ValueType, sbyte>();
            }
            else if (IsUnsignedIntegralType(valType))
            {
                if (valSize == 4)
                    return new ValueConverter<ValueType, uint>();
                else if (valSize == 8)
                    return new ValueConverter<ValueType, ulong>();
                else if (valSize == 2)
                    return new ValueConverter<ValueType, ushort>();
                else if (valSize == 1)
                    return new ValueConverter<ValueType, byte>();
            }
            else if (valType == "decimal") // C# only
            {
                return new ValueConverter<ValueType, decimal>();
            }

            return null;
        }

        // valName - pointer name
        // valType - pointer type, must end with *
        public ValueConverter<ulong> GetPointerConverter(string valName, string valType = null)
        {
            if (valType == null)
                valType = ExpressionParser.GetValueType(debugger, valName);
            int valSize = ExpressionParser.GetTypeSizeof(debugger, valType);

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

        // valName - name of the first element in array
        public ArrayConverter<double> GetNumericArrayConverter(string valName, string valType, int size)
        {
            ValueConverter<double> valueConverter = GetNumericConverter(valName, valType);
            return valueConverter == null
                 ? null
                 : new ArrayConverter<double>(valueConverter, size);
        }

        // valName - name of the first pointer in array
        public ArrayConverter<ulong> GetPointerArrayConverter(string valName, string valType, int size)
        {
            ValueConverter<ulong> pointerConverter = GetPointerConverter(valName, valType);
            return pointerConverter == null
                 ? null
                 : new ArrayConverter<ulong>(pointerConverter, size);
        }

        // valName - name of the variable starting the block
        public bool Read<ValueType>(string valName,
                                    ValueType[] values,
                                    Converter<ValueType> converter)
            where ValueType : struct
        {
            // TODO: redundant

            if (converter.ValueCount() != values.Length)
                throw new ArgumentOutOfRangeException("values.Length");

            int byteSize = converter.ByteSize();
            if (byteSize < 1)
                return true;
            byte[] bytes = new byte[byteSize];
            bool ok = ReadBytes(valName, bytes);
            if (!ok)
                return false;

            converter.Copy(bytes, values);

            return true;
        }

        public bool Read<ValueType>(ulong address,
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
            bool ok = ReadBytes(address, bytes);
            if (!ok)
                return false;

            converter.Copy(bytes, values);

            return true;
        }

        // valName - first value in range
        public bool ReadNumericArray(string valName, double[] values)
        {
            int count = values.Length;
            if (count < 1)
                return true;

            ArrayConverter<double> converter = GetNumericArrayConverter(valName, null, count);
            if (converter == null)
                return false;

            return Read(valName, values, converter);
        }

        // valName - first pointer in range
        public bool ReadPointerArray(string valName, ulong[] values)
        {
            int count = values.Length;
            if (count < 1)
                return true;

            ArrayConverter<ulong> converter = GetPointerArrayConverter(valName, null, count);
            if (converter == null)
                return false;

            return Read(valName, values, converter);
        }

        // TODO: redundant
        public bool ReadBytes(string valName, byte[] buffer)
        {
            ulong address = ExpressionParser.GetValueAddress(debugger, valName);
            if (address == 0)
                return false;

            return ReadBytes(address, buffer);
        }

        public bool ReadBytes(ulong address, byte[] buffer)
        {
            if (buffer.Length < 1)
                return true;

            if (process == null)
                return false;

            int bytesRead = process.ReadMemory(address, DkmReadMemoryFlags.None, buffer);
            return bytesRead == buffer.Length;
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

        public MemoryReader(Debugger debugger)
        {
            string language = debugger.CurrentStackFrame.Language;
            this.language = language == "C#" ? Language.CS : Language.Cpp;

            this.process = GetDebuggedProcess(debugger);

            this.debugger = debugger;
        }

        private static DkmProcess GetDebuggedProcess(Debugger debugger)
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

        enum Language { Cpp, CS };

        Language language;
        DkmProcess process;

        Debugger debugger; // TEMP
    }
}
