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

                // NOTE: If the image is not created at the point of debugging, so the variable is
                // uninitialized, the size may be out of bounds of int32 range. In this case the
                // exception is thrown here and this is ok. However if there is some garbage in
                // memory random size could be loaded here. Then also the memory probably points
                // to some random place in memory (maybe protected?) so the result will probably
                // be another exception which is fine or an image containing noise from memory.
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

                // TODO: only integral channels supported for now
                bool isUnsignedIntegral = IsUnsignedIntegralType(channelValueType);
                bool isSignedIntegral = IsSignedIntegralType(channelValueType);
                bool isIntegral = isUnsignedIntegral || isSignedIntegral;

                if (!isIntegral)
                    return;

                int channelValueSize = isIntegral
                                     ? GetSizeOfType(debugger, channelValueType)
                                     : 0;

                string colorSpaceId = Util.BaseType(colorSpaceType);
                ColorSpace colorSpace = ParseColorSpace(colorSpaceType);
                int colorSpaceSize = ColorSpaceSize(colorSpace);

                if (colorSpace == ColorSpace.Unknown || colorSpaceSize == 0)
                    return;

                Layout layout = ParseChannelMapping(colorSpace, channelMappingType);
                if (layout == Layout.Unknown)
                    return;

                if (channelValueSize != 1 && channelValueSize != 2 && channelValueSize != 4)
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

                LayoutMapper layoutMapper = GetLayoutMapper(layout);
                if (layoutMapper == null)
                    return;

                // Use Pixel format native to Gil Image?
                System.Drawing.Bitmap bmp = new System.Drawing.Bitmap(width, height);
                for (int j = 0; j < height; ++j)
                {
                    for (int i = 0; i < width; ++i)
                    {
                        int offset = (j * width + i) * colorBytesCount;

                        // The raw bytes are converted into channel values pixel by pixel.
                        //  It could be more efficient to convert all channel values at once first
                        //  and only create pixels from array of channels in this loop.
                        // Another thing is that when channels are always converted to byte
                        //  the information is lost. This information could be used during potential
                        //  conversion in GetColor() below (cmyk->rgb). Channels could be returned
                        //  as float[]. In practice the eye will probably not notice the difference.
                        byte[] channels = GetChannels(memory, offset, channelValueSize, colorSpaceSize, isSignedIntegral);
                        if (channels == null)
                            return;

                        System.Drawing.Color c = layoutMapper.GetColor(channels);
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

            private bool IsSignedIntegralType(string type)
            {
                return type == "char"
                    || type == "signed char"
                    || type == "short"
                    || type == "signed short"
                    || type == "int"
                    || type == "signed int"
                    || type == "long"
                    || type == "signed long"
                    || type == "__int64"
                    || type == "signed __int64";
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

            byte[] GetChannels(byte[] memory, int offset, int channelSize, int channelsCount, bool isSigned)
            {
                byte[] result = new byte[channelsCount];

                if (channelSize == 1)
                {
                    Buffer.BlockCopy(memory, offset, result, 0, channelsCount);
                    if (isSigned)
                    {
                        for (int i = 0; i < channelsCount; ++i)
                        {
                            if (result[i] <= 127)
                                result[i] += 128;
                            else
                                result[i] -= 128;
                        }
                    }
                }
                else if (channelSize == 2)
                {
                    ushort[] tmp = new ushort[channelsCount];
                    Buffer.BlockCopy(memory, offset, tmp, 0, 2 * channelsCount);
                    if (isSigned)
                    {
                        for (int i = 0; i < channelsCount; ++i)
                        {
                            if (tmp[i] <= 32767)
                                tmp[i] += 32768;
                            else
                                tmp[i] -= 32768;
                        }
                    }
                    for (int i = 0; i < channelsCount; ++i)
                        result[i] = (byte)((float)tmp[i] / ushort.MaxValue * byte.MaxValue);
                }
                else if (channelSize == 4)
                {
                    uint[] tmp = new uint[channelsCount];
                    Buffer.BlockCopy(memory, offset, tmp, 0, 4 * channelsCount);
                    if (isSigned)
                    {
                        for (int i = 0; i < channelsCount; ++i)
                        {
                            if (tmp[i] <= 2147483647)
                                tmp[i] += 2147483648;
                            else
                                tmp[i] -= 2147483648;
                        }
                    }
                    for (int i = 0; i < channelsCount; ++i)
                        result[i] = (byte)((float)tmp[i] / uint.MaxValue * byte.MaxValue);
                }
                else
                {
                    result = null;
                }
                return result;
            }

            abstract class LayoutMapper
            {
                public abstract System.Drawing.Color GetColor(byte[] channels);
            }

            class RgbMapper : LayoutMapper
            {
                public override System.Drawing.Color GetColor(byte[] channels)
                {
                    return System.Drawing.Color.FromArgb(channels[0],
                                                         channels[1],
                                                         channels[2]);
                }
            }

            class BgrMapper : LayoutMapper
            {
                public override System.Drawing.Color GetColor(byte[] channels)
                {
                    return System.Drawing.Color.FromArgb(channels[2],
                                                         channels[1],
                                                         channels[0]);
                }
            }

            class RgbaMapper : LayoutMapper
            {
                public override System.Drawing.Color GetColor(byte[] channels)
                {
                    return System.Drawing.Color.FromArgb(channels[3],
                                                         channels[0],
                                                         channels[1],
                                                         channels[2]);
                }
            }

            class BgraMapper : LayoutMapper
            {
                public override System.Drawing.Color GetColor(byte[] channels)
                {
                    return System.Drawing.Color.FromArgb(channels[3],
                                                         channels[2],
                                                         channels[1],
                                                         channels[0]);
                }
            }

            class ArgbMapper : LayoutMapper
            {
                public override System.Drawing.Color GetColor(byte[] channels)
                {
                    return System.Drawing.Color.FromArgb(channels[0],
                                                         channels[1],
                                                         channels[2],
                                                         channels[3]);
                }
            }

            class AbgrMapper : LayoutMapper
            {
                public override System.Drawing.Color GetColor(byte[] channels)
                {
                    return System.Drawing.Color.FromArgb(channels[0],
                                                         channels[3],
                                                         channels[2],
                                                         channels[1]);
                }
            }

            class CmykMapper : LayoutMapper
            {
                public override System.Drawing.Color GetColor(byte[] channels)
                {
                    float c = channels[0] / 255.0f;
                    float m = channels[1] / 255.0f;
                    float y = channels[2] / 255.0f;
                    float k = channels[3] / 255.0f;
                    
                    int r = Convert.ToInt32(255 * (1.0f - Math.Min(1.0f, c * (1 - k) + k)));
                    int g = Convert.ToInt32(255 * (1.0f - Math.Min(1.0f, m * (1 - k) + k)));
                    int b = Convert.ToInt32(255 * (1.0f - Math.Min(1.0f, y * (1 - k) + k)));

                    return System.Drawing.Color.FromArgb(r, g, b);
                }
            }

            class GrayMapper : LayoutMapper
            {
                public override System.Drawing.Color GetColor(byte[] channels)
                {
                    return System.Drawing.Color.FromArgb(channels[0],
                                                         channels[0],
                                                         channels[0]);
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
