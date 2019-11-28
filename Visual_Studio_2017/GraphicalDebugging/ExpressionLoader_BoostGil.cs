//------------------------------------------------------------------------------
// <copyright file="ExpressionLoader.cs">
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
using Task = System.Threading.Tasks.Task;

namespace GraphicalDebugging
{
    partial class ExpressionLoader
    {
        class BoostGilImage : LoaderR<ExpressionDrawer.Image>
        {
            public override ExpressionLoader.Kind Kind() { return ExpressionLoader.Kind.Image; }

            public override string Id() { return "boost::gil::image"; }

            public override void Load(Loaders loaders, MemoryReader mreader, Debugger debugger,
                                      string name, string type,
                                      out Geometry.Traits traits,
                                      out ExpressionDrawer.Image result)
            {
                traits = null;
                result = null;

                int width = LoadSizeParsed(debugger, name + "._view._dimensions.x");
                int height = LoadSizeParsed(debugger, name + "._view._dimensions.y");
                if (width < 1 || height < 1)
                    return;

                string pixelType, isPlanar;
                if (! Util.Tparams(type, out pixelType, out isPlanar))
                    return;

                string pixelId = Util.BaseType(pixelType);
                if (pixelId != "boost::gil::pixel")
                    return;

                string channelValueType, layoutType;
                if (! Util.Tparams(pixelType, out channelValueType, out layoutType))
                    return;

                string layoutId = Util.BaseType(layoutType);
                if (layoutId != "boost::gil::layout")
                    return;

                string colorSpaceType, channelMappingType;
                if (! Util.Tparams(layoutType, out colorSpaceType, out channelMappingType))
                    return;

                int channelValueSize = 0;
                // TODO: only unsigned integral channels supported for now
                if (IsUnsignedIntegralType(channelValueType))
                    channelValueSize = GetSizeOfType(debugger, channelValueType);

                string colorSpaceId = Util.BaseType(colorSpaceType);
                ColorSpace colorSpace = ParseColorSpace(colorSpaceType);
                int colorSpaceSize = ColorSpaceSize(colorSpace);

                if (colorSpace == ColorSpace.Unknown || colorSpaceSize == 0)
                    return;

                Layout layout = ParseChannelMapping(colorSpace, channelMappingType);
                if (layout == Layout.Unknown)
                    return;

                // TODO: only 8-bit channels supported for now
                if (channelValueSize != 1)
                    return;

                LayoutMapper layoutMapper = GetLayoutMapper(layout);
                if (layoutMapper == null)
                    return;

                int colorBytesCount = colorSpaceSize * channelValueSize;
                // TODO: size_t? ulong?
                int bytesCount = width * height * colorBytesCount;

                byte[] memory = new byte[bytesCount];
                bool isLoaded = false;
                if (mreader != null)
                {
                    isLoaded = mreader.ReadBytes(name + "._memory[0]", memory);
                }

                if (!isLoaded)
                {
                    // Parsing the memory byte by byte may take very long time
                    // even for small images. So don't do it.
                    return;
                }

                // Use Pixel format native to Gil Image?
                System.Drawing.Bitmap bmp = new System.Drawing.Bitmap(width, height);
                for (int j = 0; j < height; ++j)
                {
                    for (int i = 0; i < width; ++i)
                    {
                        int b = (j * width + i) * colorBytesCount;
                        System.Drawing.Color c = layoutMapper.GetColor(memory, b);
                        bmp.SetPixel(i, j, c);
                    }
                }

                result = new ExpressionDrawer.Image(bmp);
            }

            private bool IsUnsignedIntegralType(string type)
            {
                return type == "unsigned char"
                    || type == "unsigned short"
                    || type == "unsigned int"
                    || type == "unsigned long"
                    || type == "unsigned __int64";
            }

            private int GetSizeOfType(Debugger debugger, string type)
            {
                Expression sizeExpr = debugger.GetExpression("sizeof(" + type + ")");
                return sizeExpr.IsValidValue
                     ? Util.ParseInt(sizeExpr.Value, debugger.HexDisplayMode)
                     : 0;
            }
            
            enum ColorSpace { Unknown, Rgb, Rgba, Cmyk, Gray };

            ColorSpace ParseColorSpace(string colorSpaceType)
            {
                string colorSpaceId = Util.BaseType(colorSpaceType);
                List<string> tparams = Util.Tparams(colorSpaceType);
                if (colorSpaceId == "boost::mpl::vector3" && tparams.Count == 3)
                {
                    if (tparams[0] == "boost::gil::red_t"
                        && tparams[1] == "boost::gil::green_t"
                        && tparams[2] == "boost::gil::blue_t")
                    {
                        return ColorSpace.Rgb;
                    }
                }
                else if (colorSpaceId == "boost::mpl::vector4" && tparams.Count == 4)
                {
                    if (tparams[0] == "boost::gil::red_t"
                        && tparams[1] == "boost::gil::green_t"
                        && tparams[2] == "boost::gil::blue_t"
                        && tparams[3] == "boost::gil::alpha_t")
                    {
                        return ColorSpace.Rgba;
                    }
                    else if (tparams[0] == "boost::gil::cyan_t"
                        && tparams[1] == "boost::gil::magenta_t"
                        && tparams[2] == "boost::gil::yellow_t"
                        && tparams[3] == "boost::gil::black_t")
                    {
                        return ColorSpace.Cmyk;
                    }
                }
                else if (colorSpaceId == "boost::mpl::vector1" && tparams.Count == 1)
                {
                    if (tparams[0] == "boost::gil::gray_color_t")
                    {
                        return ColorSpace.Gray;
                    }
                }
                return ColorSpace.Unknown;
            }

            int ColorSpaceSize(ColorSpace colorSpace)
            {
                return colorSpace == ColorSpace.Rgb ? 3 :
                       colorSpace == ColorSpace.Rgba ? 4 :
                       colorSpace == ColorSpace.Cmyk ? 4 :
                       colorSpace == ColorSpace.Gray ? 1 :
                       0;
            }

            enum Layout { Unknown, Rgb, Bgr, Rgba, Bgra, Argb, Abgr, Cmyk, Gray };

            Layout ParseChannelMapping(ColorSpace colorSpace, string channelMappingType)
            {
                string channelMappingId = Util.BaseType(channelMappingType);
                List<string> tparams = Util.Tparams(channelMappingType);
                if (colorSpace == ColorSpace.Rgb)
                {
                    if (channelMappingId == "boost::mpl::range_c" && tparams.Count == 3
                        && tparams[1] == "0" && tparams[2] == "3")
                    {
                        return Layout.Rgb;
                    }
                    else if (channelMappingId == "boost::mpl::vector3_c" && tparams.Count == 4
                        && tparams[1] == "2" && tparams[2] == "1" && tparams[3] == "0")
                    {
                        return Layout.Bgr;
                    }
                }
                else if (colorSpace == ColorSpace.Rgba)
                {
                    if (channelMappingId == "boost::mpl::range_c" && tparams.Count == 3
                        && tparams[1] == "0" && tparams[2] == "4")
                    {
                        return Layout.Rgba;
                    }
                    else if (channelMappingId == "boost::mpl::vector4_c" && tparams.Count == 5
                        && tparams[1] == "2" && tparams[2] == "1" && tparams[3] == "0" && tparams[4] == "3")
                    {
                        return Layout.Bgra;
                    }
                    else if (channelMappingId == "boost::mpl::vector4_c" && tparams.Count == 5
                        && tparams[1] == "3" && tparams[2] == "0" && tparams[3] == "1" && tparams[4] == "2")
                    {
                        return Layout.Argb;
                    }
                    else if (channelMappingId == "boost::mpl::vector4_c" && tparams.Count == 5
                        && tparams[1] == "3" && tparams[2] == "2" && tparams[3] == "1" && tparams[4] == "0")
                    {
                        return Layout.Abgr;
                    }
                }
                else if (colorSpace == ColorSpace.Cmyk)
                {
                    if (channelMappingId == "boost::mpl::range_c" && tparams.Count == 3
                        && tparams[1] == "0" && tparams[2] == "4")
                    {
                        return Layout.Cmyk;
                    }
                }
                else if (colorSpace == ColorSpace.Gray)
                {
                    if (channelMappingId == "boost::mpl::range_c" && tparams.Count == 3
                        && tparams[1] == "0" && tparams[2] == "1")
                    {
                        return Layout.Gray;
                    }
                }
                return Layout.Unknown;
            }

            abstract class LayoutMapper
            {
                public abstract System.Drawing.Color GetColor(byte[] memory, int offset);
            }

            class RgbMapper : LayoutMapper
            {
                public override System.Drawing.Color GetColor(byte[] memory, int offset)
                {
                    return System.Drawing.Color.FromArgb(memory[offset],
                                                         memory[offset + 1],
                                                         memory[offset + 2]);
                }
            }

            class BgrMapper : LayoutMapper
            {
                public override System.Drawing.Color GetColor(byte[] memory, int offset)
                {
                    return System.Drawing.Color.FromArgb(memory[offset + 2],
                                                         memory[offset + 1],
                                                         memory[offset]);
                }
            }

            class RgbaMapper : LayoutMapper
            {
                public override System.Drawing.Color GetColor(byte[] memory, int offset)
                {
                    return System.Drawing.Color.FromArgb(memory[offset + 3],
                                                         memory[offset],
                                                         memory[offset + 1],
                                                         memory[offset + 2]);
                }
            }

            class BgraMapper : LayoutMapper
            {
                public override System.Drawing.Color GetColor(byte[] memory, int offset)
                {
                    return System.Drawing.Color.FromArgb(memory[offset + 3],
                                                         memory[offset + 2],
                                                         memory[offset + 1],
                                                         memory[offset]);
                }
            }

            class ArgbMapper : LayoutMapper
            {
                public override System.Drawing.Color GetColor(byte[] memory, int offset)
                {
                    return System.Drawing.Color.FromArgb(memory[offset],
                                                         memory[offset + 1],
                                                         memory[offset + 2],
                                                         memory[offset + 3]);
                }
            }

            class AbgrMapper : LayoutMapper
            {
                public override System.Drawing.Color GetColor(byte[] memory, int offset)
                {
                    return System.Drawing.Color.FromArgb(memory[offset],
                                                         memory[offset + 3],
                                                         memory[offset + 2],
                                                         memory[offset + 1]);
                }
            }

            class CmykMapper : LayoutMapper
            {
                public override System.Drawing.Color GetColor(byte[] memory, int offset)
                {
                    float c = memory[offset] / 255.0f;
                    float m = memory[offset + 1] / 255.0f;
                    float y = memory[offset + 2] / 255.0f;
                    float k = memory[offset + 3] / 255.0f;
                    
                    int r = Convert.ToInt32(255 * (1.0f - Math.Min(1.0f, c * (1 - k) + k)));
                    int g = Convert.ToInt32(255 * (1.0f - Math.Min(1.0f, m * (1 - k) + k)));
                    int b = Convert.ToInt32(255 * (1.0f - Math.Min(1.0f, y * (1 - k) + k)));

                    return System.Drawing.Color.FromArgb(r, g, b);
                }
            }

            class GrayMapper : LayoutMapper
            {
                public override System.Drawing.Color GetColor(byte[] memory, int offset)
                {
                    return System.Drawing.Color.FromArgb(memory[offset],
                                                         memory[offset],
                                                         memory[offset]);
                }
            }

            LayoutMapper GetLayoutMapper(Layout layout)
            {
                LayoutMapper layoutMapper = null;
                if (layout == Layout.Rgb)
                    layoutMapper = new RgbMapper();
                else if (layout == Layout.Bgr)
                    layoutMapper = new BgrMapper();
                else if (layout == Layout.Rgba)
                    layoutMapper = new RgbaMapper();
                else if (layout == Layout.Bgra)
                    layoutMapper = new BgraMapper();
                else if (layout == Layout.Argb)
                    layoutMapper = new ArgbMapper();
                else if (layout == Layout.Abgr)
                    layoutMapper = new AbgrMapper();
                else if (layout == Layout.Cmyk)
                    layoutMapper = new CmykMapper();
                else if (layout == Layout.Gray)
                    layoutMapper = new GrayMapper();
                return layoutMapper;
            }
        }
    }
}
