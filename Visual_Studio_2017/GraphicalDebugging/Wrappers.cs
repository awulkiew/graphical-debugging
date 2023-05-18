using EnvDTE;

namespace GraphicalDebugging
{
    class Wrappers
    {
        public class CppExpression : Expression
        {
            public CppExpression(Expression expression)
            {
                this.expression = expression;
                this.normalizedType = Util.CppNormalizeType(expression.Type);
            }

            private Expression expression;

            public string Name => expression.Name;

            public string Type { get { return this.normalizedType; } }

            public Expressions DataMembers => expression.DataMembers;

            public string Value { get => expression.Value; set => expression.Value = value; }

            public bool IsValidValue => expression.IsValidValue;

            public DTE DTE => expression.DTE;

            public Debugger Parent => expression.Parent;

            public Expressions Collection => expression.Collection;

            private string normalizedType;
        }

        public class DebuggerWrapper : Debugger
        {
            public DebuggerWrapper(Debugger debugger)
            {
                this.debugger = debugger;
            }
            private Debugger debugger;

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
        }
    }
}
