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
        public ClassScopeExpression()
        {
            mParts = new List<IPart>();
        }

        public ClassScopeExpression(IPart part)
        {
            mParts = new List<IPart>();
            Add(part);
        }

        public ClassScopeExpression(string expression)
        {
            mParts = ParseImpl(expression);
        }

        void Add(IPart part)
        {
            mParts.Add(part);
        }

        public void Reinitialize(Debugger debugger, string name, string type)
        {
            foreach (IPart part in mParts)
                part.Reinitialize(debugger, name, type);
        }

        public string GetString(string name)
        {
            string result = "(";
            foreach (IPart part in mParts)
                result += part.GetString(name);
            return result + ")";
        }

        public ClassScopeExpression DeepCopy()
        {
            ClassScopeExpression res = new ClassScopeExpression();
            res.mParts.Capacity = mParts.Count;
            foreach (IPart p in mParts)
                res.mParts.Add(p.DeepCopy());
            return res;
        }

        public interface IPart
        {
            void Reinitialize(Debugger debugger, string name, string type);
            string GetString(string name);
            IPart DeepCopy();
        }

        public class StringPart : IPart
        {
            public StringPart(string value) { mValue = value; }

            public void Reinitialize(Debugger debugger, string name, string type)
            { }

            public string GetString(string name)
            {
                return mValue;
            }

            public IPart DeepCopy()
            {
                return new StringPart(mValue);
            }

            readonly string mValue;
        }

        // TODO: Possible members may be used multiple times in an expression.
        //       Consider keeping a dictionary to avoid multiple checks for
        //       the same identifier.
        public class PossibleMemberPart : IPart
        {
            public PossibleMemberPart(string identifier)
            {
                mIdentifier = identifier;
                mValue = mIdentifier;
                mKind = Kind.Unknown;
            }

            public void Reinitialize(Debugger debugger, string name, string type)
            {
                mValue = mIdentifier;
                mKind = Kind.Unknown;

                // The same check in both C++ and C#
                string memVar = "(" + name + ")." + mIdentifier;
                if (debugger.ValueExists(memVar))
                {
                    mKind = Kind.MemberVariable;
                    return;
                }

                string memType = "";
                string memTypeCheck = "";
                if (debugger.IsLanguageCpp)
                {
                    memType = type + "::" + mIdentifier;
                    memTypeCheck = "(" + memType + "*)0";
                }
                else if (debugger.IsLanguageCs)
                {
                    // TODO: TEST IT
                    memType = type + "." + mIdentifier;
                    memTypeCheck = "(" + memType + " is object)";
                }

                if (debugger.ValueExists(memTypeCheck))
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

            public IPart DeepCopy()
            {
                PossibleMemberPart part = new PossibleMemberPart(mIdentifier)
                {
                    mValue = mValue,
                    mKind = mKind
                };
                return part;
            }

            readonly string mIdentifier;

            string mValue; // member type or global variable/type
            enum Kind { Unknown, MemberVariable, MemberType };
            Kind mKind;
        }

        public class ThisPart : IPart
        {
            public ThisPart() { }

            public void Reinitialize(Debugger debugger, string name, string type)
            {
                mIsCxx = debugger.IsLanguageCpp;
            }

            public string GetString(string name)
            {
                return mIsCxx
                     ? "(&(" + name + "))"
                     : name;
            }

            public IPart DeepCopy()
            {
                ThisPart part = new ThisPart
                {
                    mIsCxx = mIsCxx
                };
                return part;
            }

            bool mIsCxx = false;
        }

        public class NamePart : IPart
        {
            public NamePart() { }

            public void Reinitialize(Debugger debugger, string name, string type) { }

            public string GetString(string name)
            {
                return name;
            }

            public IPart DeepCopy()
            {
                return new NamePart();
            }
        }

        public class TParamPart : IPart
        {
            public TParamPart(int index)
            {
                this.tparam = "";
                this.index = index;
            }

            public void Reinitialize(Debugger debugger, string name, string type)
            {
                List<string> tparams = Util.Tparams(type);
                // Or throw an exception
                if (0 <= index && index < tparams.Count)
                    tparam = tparams[index];
                else
                    tparam = "";
            }

            public string GetString(string name)
            {
                return tparam;
            }

            public IPart DeepCopy()
            {
                TParamPart part = new TParamPart(index)
                {
                    tparam = tparam
                };
                return part;
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
                    {
                        if (Util.TryParseInt(s.Substring(2), out int index))
                            result.Add(new TParamPart(index));
                    }
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

        readonly List<IPart> mParts;
    }
}
