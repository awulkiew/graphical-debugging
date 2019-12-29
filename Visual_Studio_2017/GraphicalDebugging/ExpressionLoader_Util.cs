//------------------------------------------------------------------------------
// <copyright file="ExpressionLoader_Util.cs">
//     Copyright (c) Adam Wulkiewicz.
// </copyright>
//------------------------------------------------------------------------------

using EnvDTE;

namespace GraphicalDebugging
{
    partial class ExpressionLoader
    {
        class TypeInfo
        {
            public TypeInfo(Debugger debugger, string name)
            {
                Type = ExpressionParser.GetValueType(debugger, name);
                if (Type == null)
                    return;
                Size = ExpressionParser.GetTypeSizeof(debugger, Type);
                if (Size == 0)
                    return;
                IsValid = true;
            }

            public string Type = null;
            public int Size = 0;
            public bool IsValid = false;
        }

        class VariableInfo : TypeInfo
        {
            public VariableInfo(Debugger debugger, string name)
                : base(debugger, name)
            {
                if (base.IsValid)
                {
                    Address = ExpressionParser.GetValueAddress(debugger, name);
                    if (Address == 0)
                        base.IsValid = false;
                }
            }

            public ulong Address;
        }
    }
}
