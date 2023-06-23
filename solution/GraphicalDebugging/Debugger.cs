using EnvDTE;
using System;
using static GraphicalDebugging.MemoryReader;

namespace GraphicalDebugging
{
    class Debugger
    {
        public Debugger(DTE dte)
        {
            this.debugger = new DebuggerWrapper(dte);
        }

        public int LoadSize(string name)
        {
            Expression expr = debugger.GetExpression(name);
            return expr.IsValidValue
                 ? Math.Max(Util.ParseInt(expr.Value, debugger.HexDisplayMode), 0)
                 : 0;
        }
        public int LoadInt(string name, int defaultValue = 0)
        {
            Expression expr = debugger.GetExpression(name);
            return expr.IsValidValue
                 ? Util.ParseInt(expr.Value, debugger.HexDisplayMode)
                 : defaultValue;
        }

        public bool TryLoadInt(string name, out int result)
        {
            result = 0;
            Expression expr = debugger.GetExpression(name);
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
            Expression expr = debugger.GetExpression(castedName);
            if (!expr.IsValidValue)
                return false;
            result = Util.ParseDouble(expr.Value);
            return true;
        }

        public bool TryLoadBool(string name, out bool result)
        {
            result = false;
            Expression expr = debugger.GetExpression("(" + name + ") == true)");
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
            Expression ptrExpr = debugger.GetExpression("(void*)(" + pointerName + ")");
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
                Expression valExpr = debugger.GetExpression(valName);
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
            Expression valSizeExpr = debugger.GetExpression(sizeOfStr);
            return valSizeExpr.IsValidValue
                 ? Util.ParseInt(valSizeExpr.Value, debugger.HexDisplayMode)
                 : 0;
        }

        public int GetCppSizeof(string valNameOrType)
        {
            string sizeOfStr = "sizeof(" + valNameOrType + ")";
            Expression valSizeExpr = debugger.GetExpression(sizeOfStr);
            return valSizeExpr.IsValidValue
                 ? Util.ParseInt(valSizeExpr.Value, debugger.HexDisplayMode)
                 : 0;
        }

        // Valid type name or null
        public string GetValueType(string valName)
        {
            Expression valExpr = debugger.GetExpression(valName);
            return valExpr.IsValidValue
                 ? valExpr.Type
                 : null;
        }

        // Valid value or null
        public string GetValue(string valName)
        {
            Expression valExpr = debugger.GetExpression(valName);
            return valExpr.IsValidValue
                 ? valExpr.Value
                 : null;
        }

        public Expression GetExpression(string valName)
        {
            return debugger.GetExpression(valName);
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

        DebuggerWrapper debugger;
    }

    class DebuggerWrapper : EnvDTE.Debugger
    {
        public DebuggerWrapper(DTE dte)
        {
            this.debugger = dte.Debugger;
        }
        public Expression GetExpression(string ExpressionText, bool UseAutoExpandRules = false, int Timeout = -1)
        {
            Expression expr = debugger.GetExpression(ExpressionText, UseAutoExpandRules, Timeout);
            return CurrentStackFrame.Language == "C++" ? new CppExpression(expr) : expr;
        }

        public void DetachAll()
        {
            debugger.DetachAll();
        }

        public void StepInto(bool WaitForBreakOrEnd = true)
        {
            debugger.StepInto(WaitForBreakOrEnd);
        }

        public void StepOver(bool WaitForBreakOrEnd = true)
        {
            debugger.StepOver(WaitForBreakOrEnd);
        }

        public void StepOut(bool WaitForBreakOrEnd = true)
        {
            debugger.StepOut(WaitForBreakOrEnd);
        }

        public void Go(bool WaitForBreakOrEnd = true)
        {
            debugger.Go(WaitForBreakOrEnd);
        }

        public void Break(bool WaitForBreakMode = true)
        {
            debugger.Break(WaitForBreakMode);
        }

        public void Stop(bool WaitForDesignMode = true)
        {
            debugger.Stop(WaitForDesignMode);
        }

        public void SetNextStatement()
        {
            debugger.SetNextStatement();
        }

        public void RunToCursor(bool WaitForBreakOrEnd = true)
        {
            debugger.RunToCursor(WaitForBreakOrEnd);
        }

        public void ExecuteStatement(string Statement, int Timeout = -1, bool TreatAsExpression = false)
        {
            debugger.ExecuteStatement(Statement, Timeout, TreatAsExpression);
        }

        public void TerminateAll()
        {
            debugger.TerminateAll();
        }

        public Breakpoints Breakpoints => debugger.Breakpoints;

        public Languages Languages => debugger.Languages;

        public dbgDebugMode CurrentMode => debugger.CurrentMode;

        public Process CurrentProcess { get => debugger.CurrentProcess; set => debugger.CurrentProcess = value; }
        public Program CurrentProgram { get => debugger.CurrentProgram; set => debugger.CurrentProgram = value; }
        public Thread CurrentThread { get => debugger.CurrentThread; set => debugger.CurrentThread = value; }
        public StackFrame CurrentStackFrame { get => debugger.CurrentStackFrame; set => debugger.CurrentStackFrame = value; }
        public bool HexDisplayMode { get => debugger.HexDisplayMode; set => debugger.HexDisplayMode = value; }
        public bool HexInputMode { get => debugger.HexInputMode; set => debugger.HexInputMode = value; }

        public dbgEventReason LastBreakReason => debugger.LastBreakReason;

        public Breakpoint BreakpointLastHit => debugger.BreakpointLastHit;

        public Breakpoints AllBreakpointsLastHit => debugger.AllBreakpointsLastHit;

        public Processes DebuggedProcesses => debugger.DebuggedProcesses;

        public Processes LocalProcesses => debugger.LocalProcesses;

        public DTE DTE => debugger.DTE;

        public DTE Parent => debugger.Parent;

        private EnvDTE.Debugger debugger;
    }

    public class CppExpression : EnvDTE.Expression
    {
        public CppExpression(Expression expression)
        {
            this.expression = expression;
        }

        public string Name => expression.Name;

        public string Type
        {
            get
            {
                if (string.IsNullOrEmpty(this.normalizedType))
                {
                    this.normalizedType = Util.CppNormalizeType(expression.Type);
                }
                return this.normalizedType;
            }
        }

        public Expressions DataMembers => expression.DataMembers;

        public string Value { get => expression.Value; set => expression.Value = value; }

        public bool IsValidValue => expression.IsValidValue;

        public DTE DTE => expression.DTE;

        public EnvDTE.Debugger Parent => expression.Parent;

        public Expressions Collection => expression.Collection;

        private Expression expression;
        private string normalizedType;
    }
}
