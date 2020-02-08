//------------------------------------------------------------------------------
// <copyright file="ClassScopeExpression.cs">
//     Copyright (c) Adam Wulkiewicz.
// </copyright>
//------------------------------------------------------------------------------

using EnvDTE;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GraphicalDebugging
{
    class ClassScopeExpression
    {
        public ClassScopeExpression(string expression)
        {
            mParts = ParseImpl(expression);
        }

        public void Initialize(Debugger debugger, string name, string type)
        {
            foreach (IPart part in mParts)
                part.Initialize(debugger, name, type);
        }

        public string GetString(string name)
        {
            string result = "";
            foreach (IPart part in mParts)
                result += part.GetString(name);
            return result;
        }

        interface IPart
        {
            void Initialize(Debugger debugger, string name, string type);
            string GetString(string name);
        }

        class StringPart : IPart
        {
            public StringPart(string value) { mValue = value; }

            public void Initialize(Debugger debugger, string name, string type)
            { }

            public string GetString(string name)
            {
                return mValue;
            }

            string mValue;
        }

        // TODO: Possible members may be used multiple times in an expression.
        //       Consider keeping a dictionary to avoid multiple checks for
        //       the same identifier.
        class PossibleMemberPart : IPart
        {
            public PossibleMemberPart(string identifier)
            {
                mIdentifier = identifier;
                mValue = mIdentifier;
                mKind = Kind.Unknown;
            }

            public void Initialize(Debugger debugger, string name, string type)
            {
                mValue = mIdentifier;

                // The same check in both C++ and C#
                string memVar = "(" + name + ")." + mIdentifier;
                if (debugger.GetExpression(memVar).IsValidValue)
                {
                    mKind = Kind.MemberVariable;
                    return;
                }

                string memType = "";
                string memTypeCheck = "";
                if (debugger.CurrentStackFrame.Language == "C++")
                {
                    memType = type + "::" + mIdentifier;
                    memTypeCheck = "(" + memType + "*)0";
                }
                else if (debugger.CurrentStackFrame.Language == "C#")
                {
                    // TODO: TEST IT
                    memType = type + "." + mIdentifier;
                    memTypeCheck = "(" + memType + " is object)";
                }

                if (debugger.GetExpression(memTypeCheck).IsValidValue)
                {
                    // Type name is constant for breakpoint
                    mValue = memType;
                    mKind = Kind.MemberType;
                    return;
                }
            }

            public string GetString(string name)
            {
                return mKind == Kind.MemberVariable
                     ? "(" + name + ")." + mIdentifier
                     : mValue;
            }

            readonly string mIdentifier;

            string mValue; // member type or global variable/type
            enum Kind { Unknown, MemberVariable, MemberType };
            Kind mKind;
        }

        class ThisPart : IPart
        {
            public ThisPart() { }

            public void Initialize(Debugger debugger, string name, string type)
            {
                mIsCxx = debugger.CurrentStackFrame.Language == "C++";
            }

            public string GetString(string name)
            {
                return mIsCxx
                     ? "(&(" + name + "))"
                     : name;
            }

            bool mIsCxx = false;
        }

        class TParamPart : IPart
        {
            public TParamPart(int index)
            {
                this.tparam = "";
                this.index = index;
            }

            public void Initialize(Debugger debugger, string name, string type)
            {
                List<string> tparams = Util.Tparams(type);
                // Or throw an exception
                if (0 <= index && index < tparams.Count)
                    tparam = tparams[index];
            }

            public string GetString(string name)
            {
                return tparam;
            }

            string tparam;
            readonly int index;
        }

        // NOTE: This is not an implementation of a compiler.
        //   Expressions are analysed in a simplified way,
        //   i.e. the first identifier in a chain of members is analysed
        //   this means that things like this are not taken into account:
        //   - member.Base::base_member
        //       use ((Base &)member).base_member instead
        //   - u"abc"
        //       do not use
        //   - MemberTemplate<>
        //       do not use
        //       a quick test shows that they don't work in Watch window anyway
        //   - more?

        const string id = @"((\w|_)(\w|\d|_)*)";
        const string tpar = @"(\$T\d+)";
        const string op = @"((::)|(\.)|(->)|(\.\*)|(->\*))";
        const string opIdOrId = @"(" + op + @"\s*" + id + ")|" + id + "|" + tpar;
        static readonly Regex regex = new Regex(opIdOrId, RegexOptions.CultureInvariant);

        List<IPart> ParseImpl(string expression)
        {
            List<IPart> result = new List<IPart>();

            MatchCollection matches = regex.Matches(expression);

            int last = 0;

            foreach (Match match in matches)
            {
                // ignore matches with leading operators
                char c = expression[match.Index];
                if (c == ':' || c == '.' || c == '-')
                    continue;

                if (last < match.Index)
                {
                    string s = expression.Substring(last, match.Index - last);
                    result.Add(new StringPart(s));
                }

                {
                    string s = expression.Substring(match.Index, match.Length);
                    if (s == "this")
                        result.Add(new ThisPart());
                    else if (s.Length > 2 && s[0] == '$' && s[1] == 'T')
                        result.Add(new TParamPart(Util.ParseInt(s.Substring(2))));
                    else
                        result.Add(new PossibleMemberPart(s));
                }

                last = match.Index + match.Length;
            }

            if (last < expression.Length)
            {
                string s = expression.Substring(last);
                result.Add(new StringPart(s));
            }

            return result;
        }

        List<IPart> mParts;
    }
}
