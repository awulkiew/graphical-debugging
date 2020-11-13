//------------------------------------------------------------------------------
// <copyright file="ExpressionLoader_Util.cs">
//     Copyright (c) Adam Wulkiewicz.
// </copyright>
//------------------------------------------------------------------------------

using EnvDTE;

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

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
                if (stopWatch.ElapsedMilliseconds > 10000)
                {
                    if (! windowCreated)
                    {
                        windowCreated = true;
                        System.Threading.Thread thread = new System.Threading.Thread(() =>
                        {
                            LoadingWindow w = new LoadingWindow();
                            w.Title = "Loading takes much time.";
                            w.Show();
                            w.Closed += (sender2, e2) => w.Dispatcher.InvokeShutdown();
                            window = w;
                            System.Windows.Threading.Dispatcher.Run();
                        });
                        thread.SetApartmentState(System.Threading.ApartmentState.STA);
                        thread.Start();
                    }

                    // Is mutex needed here to check the reference?
                    if (windowCreated && window != null)
                    {
                        long elapsedSeconds = stopWatch.ElapsedMilliseconds / 1000;
                        string messageBoxText = "Loading of expression \"" + name + "\" takes " + elapsedSeconds + " sec.\r\n"
                                                + "Loading can be stopped by clicking the button below.";

                        bool result = true;
                        try
                        {
                            window.Dispatcher.Invoke(() =>
                            {
                                if (window.IsClosed) // just in case
                                    result = false;
                                else
                                    window.LoadingTextBlock.Text = messageBoxText;
                            });
                        }
                        catch(System.Threading.Tasks.TaskCanceledException e)
                        {
                            result = false;
                        }

                        if (result == false)
                        {
                            windowCreated = false;
                            window = null;
                            // This also means that the thread already finished
                            // or will do it in the near future because window
                            // closing shuts down the dispatcher.
                        }

                        return result;
                    }
                }

                return true;
            }

            public void Reset()
            {
                if (windowCreated && window != null)
                    window.Dispatcher.Invoke(() => window.Close());
                windowCreated = false;
                window = null;
            }

            bool windowCreated = false;
            LoadingWindow window = null;

            System.Diagnostics.Stopwatch stopWatch = new System.Diagnostics.Stopwatch();
        }

        class TypeInfo
        {
            public TypeInfo(Debugger debugger, string name)
            {
                Type = ExpressionParser.GetValueType(debugger, name);
                if (Type == null)
                    return;
                Size = ExpressionParser.GetTypeSizeof(debugger, Type);
                if (Size <= 0)
                    return;
                IsValid = true;
            }

            public string Type = null;
            public int Size = 0;
            public bool IsValid = false;
        }

        class MemberInfo : TypeInfo
        {
            public MemberInfo(Debugger debugger, string baseName, string memberName)
                : base(debugger, memberName)
            {
                if (!IsValid)
                    return;
                Offset = ExpressionParser.GetAddressDifference(debugger, baseName, memberName);
                if (ExpressionParser.IsInvalidAddressDifference(Offset))
                    IsValid = false;
            }

            public long Offset;
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

        class DummyMatcher : ITypeMatcher
        {
            public bool MatchType(string type, string id)
            {
                return true;
            }
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
