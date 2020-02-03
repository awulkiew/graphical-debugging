//------------------------------------------------------------------------------
// <copyright file="ExpressionLoader_UserDefined.cs">
//     Copyright (c) Adam Wulkiewicz.
// </copyright>
//------------------------------------------------------------------------------

using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;

namespace GraphicalDebugging
{
    partial class ExpressionLoader
    {
        class UserPoint : PointLoader
        {
            public UserPoint(string id, string x, string y)
            {
                this.id = id;
                this.member_x = x;
                this.member_y = y;
                this.member_type_x = null;
                this.member_type_y = null;
                this.sizeOf = 0;
            }

            public override bool IsUserDefined() { return true; }

            public override string Id() { return id; }

            public override void Initialize(Debugger debugger, string name)
            {
                Expression e = debugger.GetExpression(name);
                Expression ex = debugger.GetExpression(name + "." + member_x);
                Expression ey = debugger.GetExpression(name + "." + member_y);
                if (e.IsValidValue && ex.IsValidValue && ey.IsValidValue)
                {
                    sizeOf = ExpressionParser.GetTypeSizeof(debugger, e.Type);
                    member_type_x = ex.Type;
                    member_type_y = ey.Type;
                }
            }

            public override Geometry.Traits LoadTraits(string type)
            {
                // TODO: dimension, CS and Units defined by the user
                return new Geometry.Traits(2, Geometry.CoordinateSystem.Cartesian, Geometry.Unit.None);
            }

            protected override ExpressionDrawer.Point LoadPointParsed(Debugger debugger, string name, string type)
            {
                double x = 0, y = 0;
                bool okx = ExpressionParser.TryLoadDouble(debugger, name + "." + member_x, out x);
                bool oky = ExpressionParser.TryLoadDouble(debugger, name + "." + member_y, out y);
                return Util.IsOk(okx, oky)
                     ? new ExpressionDrawer.Point(x, y)
                     : null;
            }

            protected override ExpressionDrawer.Point LoadPointMemory(MemoryReader mreader, Debugger debugger,
                                                                      string name, string type)
            {
                MemoryReader.Converter<double> converter = GetMemoryConverter(mreader, debugger, name, type);
                if (converter != null)
                {
                    if (converter.ValueCount() != 2)
                        throw new ArgumentOutOfRangeException("converter.ValueCount()");

                    ulong address = ExpressionParser.GetValueAddress(debugger, name);
                    if (address == 0)
                        return null;

                    double[] values = new double[2];
                    if (mreader.Read(address, values, converter))
                    {
                        return new ExpressionDrawer.Point(values[0], values[1]);
                    }
                }
                return null;
            }

            public override MemoryReader.Converter<double> GetMemoryConverter(Loaders loaders,
                                                                              MemoryReader mreader,
                                                                              Debugger debugger,
                                                                              string name, string type)
            {
                return GetMemoryConverter(mreader, debugger, name, type);
            }

            protected MemoryReader.Converter<double> GetMemoryConverter(MemoryReader mreader, Debugger debugger,
                                                                        string name, string type)
            {
                if (sizeOf == 0 /*|| member_type_x == null || member_type_y == null*/)
                    return null;

                string firstType = member_type_x;
                string secondType = member_type_y;
                string first = name + "." + member_x;
                string second = name + "." + member_y;
                // TODO: This could be done once, in Initialize
                long firstOffset = ExpressionParser.GetAddressDifference(debugger, name, first);
                long secondOffset = ExpressionParser.GetAddressDifference(debugger, name, second);
                if (ExpressionParser.IsInvalidAddressDifference(firstOffset)
                 || ExpressionParser.IsInvalidAddressDifference(secondOffset)
                 || firstOffset < 0
                 || secondOffset < 0
                 || firstOffset > sizeOf
                 || secondOffset > sizeOf)
                    return null;

                int firstSize = ExpressionParser.GetTypeSizeof(debugger, firstType);
                int secondSize = ExpressionParser.GetTypeSizeof(debugger, secondType);
                if (firstSize == 0 || secondSize == 0)
                    return null;

                MemoryReader.ValueConverter<double> firstConverter = mreader.GetNumericConverter(firstType, firstSize);
                MemoryReader.ValueConverter<double> secondConverter = mreader.GetNumericConverter(secondType, secondSize);
                if (firstConverter == null || secondConverter == null)
                    return null;

                return new MemoryReader.StructConverter<double>(
                            sizeOf,
                            new MemoryReader.Member<double>(firstConverter, (int)firstOffset),
                            new MemoryReader.Member<double>(secondConverter, (int)secondOffset));
            }

            string id;
            string member_x;
            string member_y;
            string member_type_x;
            string member_type_y;
            int sizeOf;
        }

        private static bool ReloadUserTypes(Loaders loaders,
                                            string userTypesPath,
                                            bool isChanged,
                                            DateTime lastWriteTime,
                                            out DateTime newWriteTime)
        {
            newWriteTime = new DateTime(0);

            bool fileExists = System.IO.File.Exists(userTypesPath);
            bool newerFile = false;
            if (fileExists)
            {
                newWriteTime = (new System.IO.FileInfo(userTypesPath)).LastWriteTime;
                newerFile = newWriteTime > lastWriteTime;
            }
            bool update = isChanged || newerFile;

            if (update)
                loaders.RemoveUserDefined();

            if (update && fileExists)
            {
                System.Xml.XmlDocument doc = new System.Xml.XmlDocument();
                doc.Load(userTypesPath);
                foreach (System.Xml.XmlElement elRoot in doc.GetElementsByTagName("GraphicalDebugging"))
                {
                    foreach (System.Xml.XmlElement elPoint in elRoot.GetElementsByTagName("Point"))
                    {
                        var elCoords = Util.GetXmlElementByTagName(elPoint, "Coordinates");
                        if (elCoords != null)
                        {
                            var elX = Util.GetXmlElementByTagName(elCoords, "X");
                            var elY = Util.GetXmlElementByTagName(elCoords, "Y");
                            if (elX != null && elY != null)
                            {
                                string x = elX.InnerText;
                                string y = elY.InnerText;
                                string id = elPoint.GetAttribute("Id");
                                //string name = elPoint.GetAttribute("Type");
                                loaders.Add(new UserPoint(id, x, y));
                            }
                        }
                    }
                }
            }

            return update;
        }

        public static void ReloadUserTypes(GeneralOptionPage options)
        {
            if (options == null)
                return;

            DateTime wtCpp;
            if (ReloadUserTypes(Instance.loadersCpp,
                                options.UserTypesPathCpp,
                                options.isUserTypesPathCppChanged,
                                options.userTypesCppWriteTime,
                                out wtCpp))
            {
                options.isUserTypesPathCppChanged = false;
                options.userTypesCppWriteTime = wtCpp;
            }

            DateTime wtCS;
            if (ReloadUserTypes(Instance.loadersCS,
                                options.UserTypesPathCS,
                                options.isUserTypesPathCSChanged,
                                options.userTypesCSWriteTime,
                                out wtCS))
            {
                options.isUserTypesPathCSChanged = false;
                options.userTypesCSWriteTime = wtCS;
            }
        }
    }
}
