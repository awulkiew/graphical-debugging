//------------------------------------------------------------------------------
// <copyright file="MemoryReader.cs">
//     Copyright (c) Adam Wulkiewicz.
// </copyright>
//------------------------------------------------------------------------------

using EnvDTE;
using Microsoft.VisualStudio.Debugger;

using System;
using System.Collections.Generic;

namespace GraphicalDebugging
{
    class MemoryReader
    {
        public static bool ReadBytes(Debugger debugger, string ptrName, byte[] buffer)
        {
            if (buffer.Length < 1)
                return true;

            Expression ptrExpr = debugger.GetExpression("(void*)&(*(" + ptrName + "))");
            string t1 = ptrExpr.Type;
            string t2 = ptrExpr.Value;
            if (!ptrExpr.IsValidValue)
                return false;

            //Process process = debugger.CurrentProcess;
            DkmProcess[] procs = DkmProcess.GetProcesses();
            if (procs.Length != 1)
                return false;
            DkmProcess proc = procs[0];
            // Alternatively somehow detect the correct process.
            // One possibility: debugger.CurrentProcess.Name == proc.Path

            ulong address = ParseAddress(ptrExpr.Value);

            int bytesRead = proc.ReadMemory(address, DkmReadMemoryFlags.None, buffer);
            return bytesRead == buffer.Length;
        }

        public static bool ReadDoubles(Debugger debugger, string ptrName, double[] buffer)
        {
            int bufferSize = buffer.Length;
            if (bufferSize < 1)
                return true;

            Expression valExpr = debugger.GetExpression("*(" + ptrName + ")");
            if (!valExpr.IsValidValue)
                return false;
            string valType = valExpr.Type;
            if (valType != "double")
                return false;

            Expression valSizeExpr = debugger.GetExpression("sizeof(*(" + ptrName + "))");
            if (!valSizeExpr.IsValidValue)
                return false;
            int valSize = int.Parse(valSizeExpr.Value);
            if (valSize != sizeof(double))
                return false;

            int byteBufferSize = bufferSize * valSize;
            byte[] byteBuffer = new byte[byteBufferSize];
            if (!ReadBytes(debugger, ptrName, byteBuffer))
                return false;

            Convert(byteBuffer, buffer);

            return true;
        }

        private static ulong ParseAddress(string str)
        {
            if (str.StartsWith("0x", StringComparison.CurrentCultureIgnoreCase))
                return ulong.Parse(str.Substring(2), System.Globalization.NumberStyles.HexNumber);
            else
                return ulong.Parse(str);
        }
        
        private static void Convert(byte[] byteBuffer, double[] doubleBuffer)
        {
            System.Diagnostics.Debug.Assert(byteBuffer.Length == doubleBuffer.Length * sizeof(double));

            Buffer.BlockCopy(byteBuffer, 0, doubleBuffer, 0, byteBuffer.Length);
        }

        private static void Convert(byte[] byteBuffer, List<double> doubleBuffer)
        {
            System.Diagnostics.Debug.Assert(byteBuffer.Length == doubleBuffer.Count * sizeof(double));

            for (int i = 0; i < doubleBuffer.Count; ++i)
                doubleBuffer[i] = BitConverter.ToDouble(byteBuffer, i * sizeof(double));
        }
    }
}
