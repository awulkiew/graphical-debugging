using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GraphicalDebugging
{
    partial class ExpressionLoader
    {
        class StdPairPoint : PointLoader
        {
            public class LoaderCreator : ExpressionLoader.ILoaderCreator
            {
                public bool IsUserDefined() { return false; }
                public Kind Kind() { return ExpressionLoader.Kind.Point; }
                public Loader Create(Loaders loaders, Debugger debugger, string name, string type, string id)
                {
                    if (id != "std::pair")
                        return null;

                    List<string> tparams = Util.Tparams(type);
                    if (tparams.Count < 2)
                        return null;

                    return new StdPairPoint(tparams[0], tparams[1]);
                }
            }

            private StdPairPoint(string firstType, string secondType)
            {
                this.firstType = firstType;
                this.secondType = secondType;
            }

            public override Geometry.Traits GetTraits(MemoryReader mreader, Debugger debugger,
                                                      string name)
            {
                return new Geometry.Traits(2, Geometry.CoordinateSystem.Cartesian, Geometry.Unit.None);
            }

            public override ExpressionDrawer.Point LoadPointParsed(Debugger debugger, string name, string type)
            {
                return debugger.TryLoadDouble(name + ".first", out double x)
                    && debugger.TryLoadDouble(name + ".second", out double y)
                     ? new ExpressionDrawer.Point(x, y)
                     : null;
            }

            public override ExpressionDrawer.Point LoadPointMemory(MemoryReader mreader, Debugger debugger,
                                                                      string name, string type)
            {
                MemoryReader.Converter<double> converter = GetMemoryConverter(mreader, debugger, name, type);
                if (converter == null)
                    return null;

                if (converter.ValueCount() != 2)
                    throw new ArgumentOutOfRangeException("converter.ValueCount()");

                if (!debugger.GetValueAddress(name, out ulong address))
                    return null;

                double[] values = new double[2];
                if (mreader.Read(address, values, converter))
                {
                    return new ExpressionDrawer.Point(values[0], values[1]);
                }

                return null;
            }

            public override MemoryReader.Converter<double> GetMemoryConverter(MemoryReader mreader,
                                                                              Debugger debugger, // TODO - remove
                                                                              string name, string type)
            {
                string first = name + ".first";
                string second = name + ".second";
                if (!debugger.GetAddressOffset(name, first, out long firstOffset)
                 || !debugger.GetAddressOffset(name, second, out long secondOffset)
                 || !debugger.GetTypeSizeof(firstType, out int firstSize)
                 || !debugger.GetTypeSizeof(secondType, out int secondSize))
                    return null;
                MemoryReader.ValueConverter<double> firstConverter = mreader.GetNumericConverter(firstType, firstSize);
                MemoryReader.ValueConverter<double> secondConverter = mreader.GetNumericConverter(secondType, secondSize);
                return firstConverter != null
                    && secondConverter != null
                    && debugger.GetTypeSizeof(type, out int sizeOfPair)
                    && !Debugger.IsInvalidOffset(sizeOfPair, firstOffset, secondOffset)
                     ? new MemoryReader.StructConverter<double>(
                            sizeOfPair,
                            new MemoryReader.Member<double>(firstConverter, (int)firstOffset),
                            new MemoryReader.Member<double>(secondConverter, (int)secondOffset))
                     : null;
            }

            readonly string firstType;
            readonly string secondType;
        }

        class StdComplexPoint : BXPoint
        {
            public class LoaderCreator : ExpressionLoader.ILoaderCreator
            {
                public bool IsUserDefined() { return false; }
                public Kind Kind() { return ExpressionLoader.Kind.Point; }
                public Loader Create(Loaders loaders, Debugger debugger, string name, string type, string id)
                {
                    if (id != "std::complex")
                        return null;

                    List<string> tparams = Util.Tparams(type);
                    if (tparams.Count < 1)
                        return null;

                    return new StdComplexPoint(tparams[0]);
                }
            }

            private StdComplexPoint(string coordType)
                : base("._Val", coordType,
                       new Geometry.Traits(2, Geometry.CoordinateSystem.Complex, Geometry.Unit.None))
            { }
        }

        class StdChronoDuration : Value
        {
            public class LoaderCreator : ExpressionLoader.ILoaderCreator
            {
                public bool IsUserDefined() { return false; }
                public Kind Kind() { return ExpressionLoader.Kind.Value; }
                public Loader Create(Loaders loaders, Debugger debugger, string name, string type, string id)
                {
                    if (id != "std::chrono::duration")
                        return null;

                    List<string> tparams = Util.Tparams(type);
                    if (tparams.Count < 1)
                        return null;

                    return new StdChronoDuration("_MyRep", tparams[0]);
                }
            }

            private StdChronoDuration(string memberName, string memberType) : base(memberName, memberType)
            {}
        }
    }
}
