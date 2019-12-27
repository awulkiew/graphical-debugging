//------------------------------------------------------------------------------
// <copyright file="ExpressionDrawer.cs">
//     Copyright (c) Adam Wulkiewicz.
// </copyright>
//------------------------------------------------------------------------------

using EnvDTE;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GraphicalDebugging
{
    class ExpressionParser
    {
        // TODO: change names - remove "Parsed"

        public int LoadSizeParsed(string name)
        {
            return LoadSizeParsed(debugger, name);
        }

        public static int LoadSizeParsed(Debugger debugger, string name)
        {
            Expression expr = debugger.GetExpression(name);
            return expr.IsValidValue
                 ? Math.Max(Util.ParseInt(expr.Value, debugger.HexDisplayMode), 0)
                 : 0;
        }

        public bool TryLoadIntParsed(string name, out int result)
        {
            return TryLoadIntParsed(debugger, name, out result);
        }

        public static bool TryLoadIntParsed(Debugger debugger, string name, out int result)
        {
            result = 0;
            Expression expr = debugger.GetExpression(name);
            if (!expr.IsValidValue)
                return false;
            result = Util.ParseInt(expr.Value, debugger.HexDisplayMode);
            return true;
        }

        public bool TryLoadAsDoubleParsed(string name, out double result)
        {
            return TryLoadAsDoubleParsed(debugger, name, out result);
        }

        public static bool TryLoadAsDoubleParsed(Debugger debugger, string name, out double result)
        {
            result = 0.0;
            Expression expr = debugger.GetExpression("(double)" + name);
            if (!expr.IsValidValue)
                return false;
            result = Util.ParseDouble(expr.Value);
            return true;
        }

        public long GetAddressDifference(string valName1, string valName2)
        {
            return GetAddressDifference(debugger, valName1, valName2);
        }

        /*struct AddressDifference
        {
            long Value;
            bool IsValid;
        }*/

        // Valid difference of addresses of variables valName1 and valName2
        // or long.MinValue
        // detect invalid address difference with IsInvalidAddressDifference()
        public static long GetAddressDifference(Debugger debugger, string valName1, string valName2)
        {
            ulong addr1 = GetValueAddress(debugger, valName1);
            ulong addr2 = GetValueAddress(debugger, valName2);
            if (addr1 == 0 || addr2 == 0)
                return long.MinValue;
            return (addr2 >= addr1)
                 ? (long)(addr2 - addr1)
                 : -(long)(addr1 - addr2);
        }

        public static bool IsInvalidAddressDifference(long diff)
        {
            return diff == long.MinValue;
        }

        public ulong GetValueAddress(string valName)
        {
            return GetValueAddress(debugger, valName);
        }

        // Valid address of variable valName or 0
        public static ulong GetValueAddress(Debugger debugger, string valName)
        {
            Expression ptrExpr = debugger.GetExpression("((void*)&(" + valName + "))");
            if (!ptrExpr.IsValidValue)
                return 0;
            string addr = ptrExpr.Value;

            // NOTE: Hexadecimal value is automatically detected, this is probably not needed.
            // But automatically detect the format just in case of various versions
            // of VS displayed it differently regardless of debugger mode.
            return Util.ParseULong(addr/*, true*/);
        }

        // Valid size or 0
        // NOTE: In C++ the actual byte size depends on CHAR_BIT
        public int GetValueSizeof(string valName)
        {
            string sizeOfStr = "sizeof(" + valName + ")";
            if (language == Language.CS)
            {
                Expression valExpr = debugger.GetExpression(valName);
                if (!valExpr.IsValidValue)
                    return 0;
                sizeOfStr = "sizeof(" + valExpr.Type + ")";
            }

            Expression valSizeExpr = debugger.GetExpression(sizeOfStr);
            return valSizeExpr.IsValidValue
                 ? Util.ParseInt(valSizeExpr.Value, debugger.HexDisplayMode)
                 : 0;
        }

        // Valid size or 0
        public int GetTypeSizeof(string valType)
        {
            return GetTypeSizeof(debugger, valType);
        }

        // Valid size or 0
        public static int GetTypeSizeof(Debugger debugger, string valType)
        {
            Expression valSizeExpr = debugger.GetExpression("sizeof(" + valType + ")");
            return valSizeExpr.IsValidValue
                 ? Util.ParseInt(valSizeExpr.Value, debugger.HexDisplayMode)
                 : 0;
        }

        // Valid name or null
        public string GetValueType(string valName)
        {
            return GetValueType(debugger, valName);
        }

        // Valid name or null
        public static string GetValueType(Debugger debugger, string valName)
        {
            Expression valExpr = debugger.GetExpression(valName);
            return valExpr.IsValidValue
                 ? valExpr.Type
                 : null;
        }

        public ExpressionParser(Debugger debugger)
        {
            string language = debugger.CurrentStackFrame.Language;
            this.language = language == "C#" ? Language.CS : Language.Cpp;

            this.debugger = debugger;
        }

        enum Language { Cpp, CS };

        Language language;
        Debugger debugger;
    }
}
