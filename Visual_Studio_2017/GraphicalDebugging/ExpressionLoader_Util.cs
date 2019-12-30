//------------------------------------------------------------------------------
// <copyright file="ExpressionLoader_Util.cs">
//     Copyright (c) Adam Wulkiewicz.
// </copyright>
//------------------------------------------------------------------------------

using EnvDTE;

using System;
using System.Windows;

namespace GraphicalDebugging
{
    partial class ExpressionLoader
    {
        class LoadTimeGuard
        {
            public LoadTimeGuard()
            {
                stopWatch.Start();
            }

            public bool CheckTimeAndDisplayMsg(string name)
            {
                if (stopWatch.ElapsedMilliseconds > timesMs[timeIndex])
                {
                    int timeIndexNext = Math.Min(timeIndex + 1, 2);

                    string messageBoxText = "Loading of expression \"" + name + "\" takes more than "
                                            + timesStr[timeIndex] + ". Do you want to wait "
                                            + timesStr[timeIndexNext] + " longer?";
                    var res = MessageBox.Show(messageBoxText,
                                              "Loading takes much time.",
                                              MessageBoxButton.YesNo,
                                              MessageBoxImage.Question);
                    if (res == MessageBoxResult.Yes)
                    {
                        timeIndex = timeIndexNext;
                        stopWatch.Restart();
                    }
                    else
                    {
                        return false;
                    }
                }

                return true;
            }

            System.Diagnostics.Stopwatch stopWatch = new System.Diagnostics.Stopwatch();
            long[] timesMs = new long[] { 10000, 60000, 600000 };
            string[] timesStr = new string[] { "10 seconds", "1 minute", "10 minutes" };
            int timeIndex = 0;
        }

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
