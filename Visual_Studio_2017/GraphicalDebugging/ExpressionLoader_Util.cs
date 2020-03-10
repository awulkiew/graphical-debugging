//------------------------------------------------------------------------------
// <copyright file="ExpressionLoader_Util.cs">
//     Copyright (c) Adam Wulkiewicz.
// </copyright>
//------------------------------------------------------------------------------

using EnvDTE;

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
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

        interface ITypeMatcher
        {
            bool MatchType(string type, string id);
        }

        class IdMatcher : ITypeMatcher
        {
            public IdMatcher(string id) { this.id = id; }

            public bool MatchType(string type, string id)
            {
                return id == this.id;
            }

            string id;
        }

        class TypeMatcher : ITypeMatcher
        {
            public TypeMatcher(string type) { this.type = type; }

            public bool MatchType(string type, string id)
            {
                return type == this.type;
            }

            string type;
        }

        class TypePatternMatcher : ITypeMatcher
        {
            public TypePatternMatcher(string pattern)
            {
                string tmp = PreprocessPattern(pattern);
                this.pattern = tmp.Replace("*", "(.+)");
                if (this.pattern != tmp)
                    this.regex = new Regex(this.pattern, RegexOptions.CultureInvariant);
            }

            public bool MatchType(string type, string id)
            {
                return regex != null
                     ? regex.IsMatch(type)
                     : type == pattern;
            }

            // From
            // A::B < C::D < E< F>>>
            // To
            // A::B<C::D<E<F> > >
            static string PreprocessPattern(string str)
            {
                string result = "";
                char lastNonSpace = ' ';
                for (int i = 0; i < str.Length; ++i)
                {
                    char c = str[i];
                    if (c != ' ' && c != '\t')
                    {
                        if (lastNonSpace == '>' && c == '>')
                            result += ' ';
                        result += c;
                        lastNonSpace = c;
                    }
                }
                return result;
            }

            string pattern;
            Regex regex;
        }

        static string StdContainerType(string containerId, string elementType, string allocatorId)
        {
            return Util.TemplateType(containerId,
                                     elementType,
                                     Util.TemplateType(allocatorId,
                                                       elementType));
        }

        static void GetBGContainerInfo(string type,
                                       int pointTIndex,
                                       int containerTIndex,
                                       int allocatorTIndex,
                                       out string elementType,
                                       out string containerType)
        {
            elementType = "";
            containerType = "";

            List<string> tparams = Util.Tparams(type);
            if (tparams.Count <= Math.Max(Math.Max(pointTIndex, containerTIndex), allocatorTIndex))
                return;

            elementType = tparams[pointTIndex];
            containerType = StdContainerType(tparams[containerTIndex],
                                             elementType,
                                             tparams[allocatorTIndex]);
        }
    }
}
