//------------------------------------------------------------------------------
// <copyright file="MemoryReader.cs">
//     Copyright (c) Adam Wulkiewicz.
// </copyright>
//------------------------------------------------------------------------------

using EnvDTE;
using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Debugger.CallStack;

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
            private static readonly int sizeOfT = Marshal.SizeOf(default(T));
            
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

            public StructConverter(int byteSize, Member<ValueType> member1, Member<ValueType> member2,
                                   Member<ValueType> member3)
            {
                this.members = new[] { member1, member2, member3 };
                initialize(byteSize);
            }

            public StructConverter(int byteSize, Member<ValueType> member1, Member<ValueType> member2,
                                   Member<ValueType> member3, Member<ValueType> member4)
            {
                this.members = new[] { member1, member2, member3, member4 };
                initialize(byteSize);
            }

            public StructConverter(int byteSize, Member<ValueType>[] members)
            {
                this.members = members;
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

            readonly Member<ValueType>[] members = null;
            int byteSize = 0;

            int internalValueCount = 0;
        }

        public class TransformingConverter<ValueType> : Converter<ValueType>
            where ValueType : struct
        {
            public delegate void Transformer(ValueType[] values, int offset);

            public TransformingConverter(Converter<ValueType> baseConverter,
                                         Transformer transformer)
            {
                this.baseConverter = baseConverter;
                this.transformer = transformer;
            }

            public override int ValueCount()
            {
                return baseConverter.ValueCount();
            }

            public override int ByteSize()
            {
                return baseConverter.ByteSize();
            }

            public override void Copy(byte[] bytes, int bytesOffset, ValueType[] result, int resultOffset)
            {
                baseConverter.Copy(bytes, bytesOffset, result, resultOffset);
                transformer(result, resultOffset);
            }

            readonly Converter<ValueType> baseConverter;
            readonly Transformer transformer;
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
            else if (language == Language.Basic)
            {
                return valType == "Integer"
                    || valType == "Long"
                    || valType == "Short"
                    || valType == "SByte";
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
            else if (language == Language.Basic)
            {
                return valType == "UInteger"
                    || valType == "ULong"
                    || valType == "UShort"
                    || valType == "Char"
                    || valType == "Byte";
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

        public ValueConverter<ValueType> GetValueConverter<ValueType>(string valType, int valSize)
            where ValueType : struct
        {
            if (valType == null || valSize <= 0)
                return null;

            if (valType == "double" || valType == "Double")
            {
                return new ValueConverter<ValueType, double>();
            }
            else if (valType == "float" || valType == "Single")
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
                else
                    return null;
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
                else
                    return null;
            }
            else if (valType == "decimal" || valType == "Decimal") // C# and Basic only
            {
                return new ValueConverter<ValueType, decimal>();
            }

            return null;
        }

        public ValueConverter<double> GetNumericConverter(string valType, int valSize)
        {
            return GetValueConverter<double>(valType, valSize);
        }

        public ArrayConverter<double> GetNumericArrayConverter(string valType, int valSize, int size)
        {
            ValueConverter<double> valueConverter = GetNumericConverter(valType, valSize);
            return valueConverter != null
                 ? new ArrayConverter<double>(valueConverter, size)
                 : null;
        }

        // ptrType - pointer type, must end with *
        // ptrSize - pointer size (4 or 8)
        public ValueConverter<ulong> GetPointerConverter(string ptrType, int ptrSize)
        {
            if (ptrType == null || ptrSize <= 0)
                return null;

            if (! ptrType.EndsWith("*"))
                return null;

            if (ptrSize == 4)
                return new ValueConverter<ulong, uint>();
            else if (ptrSize == 8)
                return new ValueConverter<ulong, ulong>();

            return null;
        }

        // ptrType - pointer type, must end with *
        // ptrSize - pointer size (4 or 8)
        public ArrayConverter<ulong> GetPointerArrayConverter(string ptrType, int ptrSize, int size)
        {
            ValueConverter<ulong> pointerConverter = GetPointerConverter(ptrType, ptrSize);
            return pointerConverter == null
                 ? null
                 : new ArrayConverter<ulong>(pointerConverter, size);
        }

        public bool ReadNumericArray(ulong address, string valType, int valSize, double[] values)
        {
            int count = values.Length;
            if (count < 1)
                return true;

            ArrayConverter<double> converter = GetNumericArrayConverter(valType, valSize, count);
            if (converter == null)
                return false;

            return Read(address, values, converter);
        }

        // ptrType - pointer type, must end with *
        // ptrSize - pointer size (4 or 8)
        public bool ReadPointerArray(ulong address, string ptrType, int ptrSize, ulong[] values)
        {
            int count = values.Length;
            if (count < 1)
                return true;

            ArrayConverter<ulong> converter = GetPointerArrayConverter(ptrType, ptrSize, count);
            if (converter == null)
                return false;

            return Read(address, values, converter);
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
            this.language = debugger.IsLanguageCs ? Language.CS
                          : debugger.IsLanguageBasic ? Language.Basic
                          : Language.Cpp;

            this.process = GetDebuggedProcess(debugger);
        }

        // https://stackoverflow.com/questions/45570027/retrieving-data-from-arbitrary-memory-addresses-using-vsix
        // https://social.msdn.microsoft.com/Forums/en-US/030cef1c-ee79-46e9-8e40-bfc59f14cc34/how-can-i-send-a-custom-debug-event-to-my-idebugeventcallback2-handler?forum=vsdebug
        // https://macropolygon.wordpress.com/2012/12/16/evaluating-debugged-process-memory-in-a-visual-studio-extension/
        private static DkmProcess GetDebuggedProcess(Debugger debugger)
        {
            /*
            // EnvDTE90a
            StackFrame2 currentFrame2 = debugger.CurrentStackFrame as StackFrame2;
            if (currentFrame2 != null)
            {
                uint currentFrameDepth = currentFrame2.Depth - 1;
            }
            */
            /*
            DkmStackFrame dkmFrame = DkmStackFrame.ExtractFromDTEObject(debugger.CurrentStackFrame);
            if (dkmFrame != null)
            {
                var dkmThread = dkmFrame.Thread;
                var dkmProcess = dkmFrame.Process;
            }
            */
            /*
            DkmStackFrame frame = DkmStackFrame.ExtractFromDTEObject(debugger.CurrentStackFrame);
            if (frame == null)
                return null;
            return frame.Process;
            */

            DkmProcess[] procs = DkmProcess.GetProcesses();
            if (procs.Length == 1)
            {
                return procs[0];
            }
            else if (procs.Length > 1)
            {
                foreach (DkmProcess proc in procs)
                {
                    if (proc.Path == debugger.CurrentProcessName)
                        return proc;
                }
            }
            return null;
        }

        enum Language { Cpp, CS, Basic };

        readonly Language language;
        readonly DkmProcess process;
    }
}
