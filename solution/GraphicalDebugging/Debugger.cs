using EnvDTE;
using System;
using static GraphicalDebugging.MemoryReader;

namespace GraphicalDebugging
{
    public class Expression
    {
        public string Name;
        public string Type;
        public bool IsValid;
    }

    class Debugger
    {
        public Debugger(DTE dte)
        {
            this.debugger = dte.Debugger;
        }

        public int LoadSize(string name)
        {
            var expr = debugger.GetExpression(name);
            return expr.IsValidValue
                 ? Math.Max(Util.ParseInt(expr.Value, debugger.HexDisplayMode), 0)
                 : 0;
        }
        public int LoadInt(string name, int defaultValue = 0)
        {
            var expr = debugger.GetExpression(name);
            return expr.IsValidValue
                 ? Util.ParseInt(expr.Value, debugger.HexDisplayMode)
                 : defaultValue;
        }

        public bool TryLoadInt(string name, out int result)
        {
            result = 0;
            var expr = debugger.GetExpression(name);
            if (!expr.IsValidValue)
                return false;
            result = Util.ParseInt(expr.Value, debugger.HexDisplayMode);
            return true;
        }

        public bool TryLoadDouble(string name, out double result)
        {
            result = 0.0;
            string castedName = "(double)" + name;
            if (IsLanguageBasic)
                castedName = "CType(" + name + ", Double)";
            var expr = debugger.GetExpression(castedName);
            if (!expr.IsValidValue)
                return false;
            result = Util.ParseDouble(expr.Value);
            return true;
        }

        public bool TryLoadBool(string name, out bool result)
        {
            result = false;
            var expr = debugger.GetExpression("(" + name + ") == true)");
            if (!expr.IsValidValue)
                return false;
            result = (expr.Value == "true" || expr.Value == "1");
            return true;
        }

        /*struct AddressDifference
        {
            long Value;
            bool IsValid;
        }*/

        // Valid difference of addresses of variables valName1 and valName2
        // or long.MinValue
        // detect invalid address difference with IsInvalidAddressDifference()
        public long GetAddressDifference(string valName1, string valName2)
        {
            ulong addr1 = GetValueAddress(valName1);
            ulong addr2 = GetValueAddress(valName2);
            if (addr1 == 0 || addr2 == 0)
                return long.MinValue;
            return (addr2 >= addr1)
                 ? (long)(addr2 - addr1)
                 : -(long)(addr1 - addr2);
        }

        public long GetPointerDifference(string pointerName1, string pointerName2)
        {
            ulong addr1 = GetPointer(pointerName1);
            ulong addr2 = GetPointer(pointerName2);
            if (addr1 == 0 || addr2 == 0)
                return long.MinValue;
            return (addr2 >= addr1)
                 ? (long)(addr2 - addr1)
                 : -(long)(addr1 - addr2);
        }

        public static long InvalidAddressDifference()
        {
            return long.MinValue;
        }

        public static bool IsInvalidAddressDifference(long diff)
        {
            return diff == long.MinValue;
        }

        // C++ and C# only!

        // TODO: C# classes
        // For value-types, structs, etc.
        // "typeof(" + type + ").IsValueType" == "true"
        // "&(" + name + ")" is of type SomeType*
        // - address: "&(" + name + ")"
        // - size: "sizeof(" + type + ")"
        // For non-value-types, e.g. classes
        // "typeof(" + type + ").IsValueType" == "false"
        // "&(" + name + ")" is of type IntPtr*
        // - address: "*(&(" + name + "))"
        // - size: "System.Runtime.InteropServices.Marshal.ReadInt32(typeof(" + type + ").TypeHandle.Value, 4)"
        // - size: "*(((int*)(void*)typeof(" + type + ").TypeHandle.Value) + 1)"

        public ulong GetPointer(string pointerName)
        {
            var ptrExpr = debugger.GetExpression("(void*)(" + pointerName + ")");
            if (!ptrExpr.IsValidValue)
                return 0;
            string addr = ptrExpr.Value;

            // NOTE: Hexadecimal value is automatically detected, this is probably not needed.
            // But automatically detect the format just in case of various versions
            // of VS displayed it differently regardless of debugger mode.
            return Util.ParseULong(addr/*, true*/);
        }

        // Valid address of variable valName or 0
        public ulong GetValueAddress(string valName)
        {
            return GetPointer("&(" + valName + ")");
        }

        // Valid size or 0
        // NOTE: In C++ the actual byte size depends on CHAR_BIT
        public int GetValueSizeof(string valName)
        {
            string typeName = valName; // In C++ value and type is interchangeable when passed into sizeof
            //if (!IsLanguageCpp(debugger))
            if (IsLanguageCs) // Change this when getting address in Basic works
            {
                var valExpr = debugger.GetExpression(valName);
                if (!valExpr.IsValidValue)
                    return 0;
                typeName = valExpr.Type;
            }
            return GetTypeSizeof(typeName);
        }

        // Valid size or 0
        public int GetTypeSizeof(string valType)
        {
            if (IsLanguageBasic) // Change this when getting address in Basic works
                //sizeOfStr = "System.Runtime.InteropServices.Marshal.SizeOf(GetType(" + valType + "))";
                return 0;

            string sizeOfStr = "sizeof(" + valType + ")";
            var valSizeExpr = debugger.GetExpression(sizeOfStr);
            return valSizeExpr.IsValidValue
                 ? Util.ParseInt(valSizeExpr.Value, debugger.HexDisplayMode)
                 : 0;
        }

        public int GetCppSizeof(string valNameOrType)
        {
            string sizeOfStr = "sizeof(" + valNameOrType + ")";
            var valSizeExpr = debugger.GetExpression(sizeOfStr);
            return valSizeExpr.IsValidValue
                 ? Util.ParseInt(valSizeExpr.Value, debugger.HexDisplayMode)
                 : 0;
        }

        // Valid type name or null
        public string GetValueType(string valName)
        {
            var valExpr = debugger.GetExpression(valName);
            if (!valExpr.IsValidValue)
                return null;
            if (IsLanguageCpp)
                return Util.CppNormalizeType(valExpr.Type);
            else
                return valExpr.Type;
        }

        // Valid value or null
        public string GetValue(string valName)
        {
            var valExpr = debugger.GetExpression(valName);
            return valExpr.IsValidValue
                 ? valExpr.Value
                 : null;
        }

        public Expression GetExpression(string valName)
        {
            var expr = debugger.GetExpression(valName);
            Expression result = new Expression { IsValid = expr.IsValidValue, Name = expr.Name };
            if (IsLanguageCpp)
                result.Type = Util.CppNormalizeType(expr.Type);
            else
                result.Type = expr.Type;
            return result;
        }

        public bool ValueExists(string valName)
        {
            return debugger.GetExpression(valName).IsValidValue;
        }

        public static bool IsInvalidType(string type)
        {
            return string.IsNullOrEmpty(type);
        }

        public static bool IsInvalidType(string type1, string type2)
        {
            return string.IsNullOrEmpty(type1) || string.IsNullOrEmpty(type2);
        }

        public static bool IsInvalidSize(int size)
        {
            return size <= 0;
        }

        public static bool IsInvalidSize(int size1, int size2)
        {
            return size1 <= 0 || size2 <= 0;
        }

        public static bool IsInvalidOffset(long size, long offset)
        {
            return IsInvalidAddressDifference(offset)
                || offset < 0
                || offset >= size;
        }

        public static bool IsInvalidOffset(long size, long offset1, long offset2)
        {
            return IsInvalidOffset(size, offset1)
                || IsInvalidOffset(size, offset2);
        }

        public string CurrentProcessName
        {
            get { return debugger.CurrentProcess.Name; }
        }

        public bool IsBreakMode
        {
            get { return debugger.CurrentMode == dbgDebugMode.dbgBreakMode; }
        }

        public bool IsLanguageCpp
        {
            get { return debugger.CurrentStackFrame.Language == "C++"; }
        }

        public bool IsLanguageCs
        {
            get { return debugger.CurrentStackFrame.Language == "C#"; }
        }

        public bool IsLanguageBasic
        {
            get { return debugger.CurrentStackFrame.Language == "Basic"; }
        }

        readonly EnvDTE.Debugger debugger;
    }
}
