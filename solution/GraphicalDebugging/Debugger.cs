using EnvDTE;
using Microsoft.VisualStudio.Shell;

namespace GraphicalDebugging
{
    public class Expression
    {
        public string Name;
        public string Type;
        public string Value;
        public bool IsValid;        
    }

    class Debugger
    {
        public Debugger(DTE dte)
        {
            this.debugger = dte.Debugger;
        }

        // TODO: return uint
        public bool TryLoadUInt(string name, out int result)
        {
            return TryLoadInt(name, out result)
                && result >= 0;
        }

        public bool TryLoadInt(string name, out int result)
        {
            result = 0;
            var expr = debugger.GetExpression(name);
            return expr.IsValidValue
                && Util.TryParseInt(expr.Value, debugger.HexDisplayMode, out result);
        }

        public bool TryLoadDouble(string name, out double result)
        {
            result = 0.0;
            string castedName = !IsLanguageBasic
                              ? "(double)(" + name + ")"
                              : "CType(" + name + ", Double)";
            var expr = debugger.GetExpression(castedName);
            return expr.IsValidValue
                && Util.TryParseDouble(expr.Value, out result);
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

        // TODO: take size to check against out of bounds
        // TODO: return ulong?
        // Difference of addresses of variables valName1 and valName2
        // Returns false if addresses cannot be loaded, parsed, any of them is equal to 0 or offset is < 0
        // In these cases result is negative
        public bool GetAddressOffset(string valName1, string valName2, out long result)
        {
            result = long.MinValue;
            if (GetValueAddress(valName1, out ulong addr1)
             && GetValueAddress(valName2, out ulong addr2)
             && addr2 >= addr1)
            {
                result = (long)(addr2 - addr1);
                return true;
            }
            return false;
        }

        // TODO: take size to check against out of bounds
        // TODO: return ulong?
        // Returns false if addresses cannot be loaded, parsed, any of them is equal to 0 or offset is < 0
        // In these cases result is negative
        public bool GetPointerOffset(string pointerName1, string pointerName2, out long result)
        {
            result = long.MinValue;
            if (GetPointer(pointerName1, out ulong addr1)
             && GetPointer(pointerName2, out ulong addr2)
             && addr2 >= addr1)
            {
                result = (long)(addr2 - addr1);
                return true;
            }
            return false;
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

        // Value of pointer, aka address pointed to
        // Returns false if address cannot be loaded, parsed or if it is equal to 0
        public bool GetPointer(string pointerName, out ulong result)
        {
            var genericPtrExpr = debugger.GetExpression("(void*)(" + pointerName + ")");
            if (genericPtrExpr.IsValidValue)
            {
                return Util.TryParseULong(genericPtrExpr.Value, out result) && result != 0;
            }

            // fallback for c#
            var elementPtrExpr = debugger.GetExpression(pointerName);
            if (elementPtrExpr.IsValidValue)
            {
                return Util.TryParseULong(elementPtrExpr.Value, out result) && result != 0;
            }

            result = 0;
            return false;
        }

        // Address of variable
        // Returns false if address cannot be loaded, parsed or if it is equal to 0
        public bool GetValueAddress(string valName, out ulong result)
        {
            return GetPointer("&(" + valName + ")", out result);
        }

        // NOTE: In C++ the actual byte size depends on CHAR_BIT
        // Returns false if size of variable cannot be loaded, parsed or if it is <= 0
        public bool GetValueSizeof(string valName, out int result)
        {
            result = 0;
            string typeName = valName; // In C++ value and type is interchangeable when passed into sizeof
            //if (!IsLanguageCpp)
            if (IsLanguageCs) // Change this when getting address in Basic works
            {
                var valExpr = debugger.GetExpression(valName);
                if (!valExpr.IsValidValue)
                    return false;
                typeName = valExpr.Type;
            }
            return GetTypeSizeof(typeName, out result);
        }

        // Returns false if size of type cannot be loaded, parsed or if it is <= 0
        public bool GetTypeSizeof(string valType, out int result)
        {
            result = 0;
            if (IsLanguageBasic) // Change this when getting address in Basic works
                //sizeOfStr = "System.Runtime.InteropServices.Marshal.SizeOf(GetType(" + valType + "))";
                return false;
            string sizeOfStr = "sizeof(" + valType + ")";
            var valSizeExpr = debugger.GetExpression(sizeOfStr);
            return valSizeExpr.IsValidValue
                && Util.TryParseInt(valSizeExpr.Value, debugger.HexDisplayMode, out result)
                && result > 0;
        }

        // Returns false if size of type cannot be loaded, parsed or if it is <= 0
        public bool GetCppSizeof(string valNameOrType, out int result)
        {
            result = 0;
            string sizeOfStr = "sizeof(" + valNameOrType + ")";
            var valSizeExpr = debugger.GetExpression(sizeOfStr);
            return valSizeExpr.IsValidValue
                && Util.TryParseInt(valSizeExpr.Value, debugger.HexDisplayMode, out result)
                && result > 0;
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
            Expression result = new Expression
            {
                Name = expr.Name,
                Value = expr.Value,
                IsValid = expr.IsValidValue
            };
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

        public static bool IsInvalidOffset(long size, long offset)
        {
            return offset < 0 || offset >= size;
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
