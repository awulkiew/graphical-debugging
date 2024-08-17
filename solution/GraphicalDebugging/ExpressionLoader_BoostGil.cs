//------------------------------------------------------------------------------
// <copyright file="ExpressionLoader_BoostGil.cs">
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
            public class LoaderCreator : ExpressionLoader.ILoaderCreator
            {
                public bool IsUserDefined() { return false; }
                public Kind Kind() { return ExpressionLoader.Kind.Image; }
                public Loader Create(Loaders loaders, Debugger debugger, string name, string type, string id)
                {
                    return id == "boost::gil::image"
                         ? new BoostGilImage()
                         : null;
                }
            }

            public override Geometry.Traits GetTraits(MemoryReader mreader, Debugger debugger,
                                                      string name)
            {
                return null;
            }

            public override ExpressionDrawer.IDrawable Load(MemoryReader mreader, Debugger debugger,
                                                            string name, string type,
                                                            LoadCallback callback)
            {
                // NOTE: If the image is not created at the point of debugging, so the variable is
                // uninitialized, the size may be out of bounds of int32 range. In this case the
                // exception is thrown here and this is ok. However if there is some garbage in
                // memory random size could be loaded here. Then also the memory probably points
                // to some random place in memory (maybe protected?) so the result will probably
                // be another exception which is fine or an image containing noise from memory.
                if (!debugger.TryLoadUInt(name + "._view._dimensions.x", out int width)
                 || !debugger.TryLoadUInt(name + "._view._dimensions.y", out int height)
                 || width < 1 || height < 1)
                    return null;

                if (! Util.Tparams(type, out string pixelType, out string isPlanarStr))
                    return null;

                string pixelId = Util.TypeId(pixelType);
                if (pixelId != "boost::gil::pixel")
                    return null;

                bool isPlanar = (isPlanarStr == "1");

                if (! Util.Tparams(pixelType, out string channelValueType, out string layoutType))
                    return null;

                string layoutId = Util.TypeId(layoutType);
                if (layoutId != "boost::gil::layout")
                    return null;

                if (! Util.Tparams(layoutType, out string colorSpaceType, out string channelMappingType))
                    return null;

                ParseChannelValue(debugger, channelValueType, out ChannelValueKind channelValueKind, out int channelValueSize);
                if (channelValueKind == ChannelValueKind.Unknown || channelValueSize == 0)
                    return null;

                //string colorSpaceId = Util.TypeId(colorSpaceType);
                ColorSpace colorSpace = ParseColorSpace(colorSpaceType);
                int colorSpaceSize = ColorSpaceSize(colorSpace);

                if (colorSpace == ColorSpace.Unknown || colorSpaceSize == 0)
                    return null;

                Layout layout = ParseChannelMapping(colorSpace, channelMappingType);
                if (layout == Layout.Unknown)
                    return null;

                if (channelValueSize != 1
                    && channelValueSize != 2
                    && channelValueSize != 4
                    && channelValueSize != 8)
                    return null;

                // TODO: size_t? ulong?
                int bytesCount = width * height * colorSpaceSize * channelValueSize;

                byte[] memory = new byte[bytesCount];
                bool isLoaded = false;
                if (mreader != null)
                {
                     if (!debugger.GetValueAddress(name + "._memory[0]", out ulong address))
                        return null;

                    isLoaded = mreader.ReadBytes(address, memory);
                }

                if (!isLoaded)
                {
                    // Parsing the memory byte by byte may take very long time
                    // even for small images. So don't do it.
                    return null;
                }

                LayoutMapper layoutMapper = GetLayoutMapper(layout);
                if (layoutMapper == null)
                    return null;

                // Use Pixel format native to Gil Image?
                System.Drawing.Bitmap bmp = new System.Drawing.Bitmap(width, height);
                for (int j = 0; j < height; ++j)
                {
                    for (int i = 0; i < width; ++i)
                    {
                        int pixelIndex = (j * width + i);

                        // The raw bytes are converted into channel values pixel by pixel.
                        //  It could be more efficient to convert all channel values at once first
                        //  and only create pixels from array of channels in this loop.
                        // Another thing is that when channels are always converted to byte
                        //  the information is lost. This information could be used during potential
                        //  conversion in GetColor() below (cmyk->rgb). Channels could be returned
                        //  as float[]. In practice the eye will probably not notice the difference.
                        byte[] channels = isPlanar
                                        ? GetChannelsPlanar(memory,
                                                            pixelIndex,
                                                            channelValueKind, channelValueSize, colorSpaceSize)
                                        : GetChannelsInterleaved(memory,
                                                                 pixelIndex,
                                                                 channelValueKind, channelValueSize, colorSpaceSize);
                        if (channels == null)
                            return null;

                        System.Drawing.Color c = layoutMapper.GetColor(channels);
                        bmp.SetPixel(i, j, c);

                        // TODO: Checked per pixel. Too often?
                        //   But it's the same for geometries (ForEachMemoryBlock).
                        callback();
                    }
                }

                return new ExpressionDrawer.Image(bmp);
            }

            private enum ChannelValueKind { Unknown, UnsignedIntegral, SignedIntegral, FloatingPoint, ScopedFloatingPoint };

            private void ParseChannelValue(Debugger debugger, string type,
                                           out ChannelValueKind channelValueKind,
                                           out int channelValueSize)
            {
                channelValueKind = ChannelValueKind.Unknown;
                
                string rawType = type;

                if (type == "unsigned char"
                    || type == "unsigned short"
                    || type == "unsigned int"
                    || type == "unsigned long"
                    || type == "unsigned __int64")
                {
                    channelValueKind = ChannelValueKind.UnsignedIntegral;
                }
                else if (type == "char" // TODO: this could actually depend on compiler flags
                        || type == "signed char"
                        || type == "short"
                        || type == "signed short"
                        || type == "int"
                        || type == "signed int"
                        || type == "long"
                        || type == "signed long"
                        || type == "__int64"
                        || type == "signed __int64")
                {
                    channelValueKind = ChannelValueKind.SignedIntegral;
                }
                else if (type == "float"
                        || type == "double")
                {
                    channelValueKind = ChannelValueKind.FloatingPoint;
                }
                else if (Util.TypeId(type) == "boost::gil::scoped_channel_value")
                {
                    List<string> tparams = Util.Tparams(type);
                    if (tparams.Count >= 1)
                    {
                        rawType = tparams[0];
                        if (rawType == "float"
                            || rawType == "double")
                        {
                            // NOTE: Assuming scope [0, 1]
                            channelValueKind = ChannelValueKind.ScopedFloatingPoint;
                        }
                    }
                }

                channelValueSize = 0;
                if (channelValueKind != ChannelValueKind.Unknown)
                    debugger.GetCppSizeof(rawType, out channelValueSize);
            }

            enum ColorSpace { Unknown, Rgb, Rgba, Cmyk, Gray, GrayAlpha };

            ColorSpace ParseColorSpace(string colorSpaceType)
            {
                //string colorSpaceId = Util.TypeId(colorSpaceType);
                List<string> tparams = Util.Tparams(colorSpaceType);
                // NOTE: Do not check the Util.BaseType(colorSpaceType)
                //  to avoid checking all MPL and MP11 vectors and lists
                //  Besides the check below should already be strong enough.
                if (tparams.Count == 3)
                {
                    if (tparams[0] == "boost::gil::red_t"
                        && tparams[1] == "boost::gil::green_t"
                        && tparams[2] == "boost::gil::blue_t")
                    {
                        return ColorSpace.Rgb;
                    }
                }
                else if (tparams.Count == 4)
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
                else if (tparams.Count == 1)
                {
                    if (tparams[0] == "boost::gil::gray_color_t")
                    {
                        return ColorSpace.Gray;
                    }
                }
                else if (tparams.Count == 2)
                {
                    if (tparams[0] == "boost::gil::gray_color_t"
                        && tparams[1] == "boost::gil::alpha_t")
                    {
                        return ColorSpace.GrayAlpha;
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
                       colorSpace == ColorSpace.GrayAlpha ? 2 :
                       0;
            }

            enum Layout { Unknown, Rgb, Bgr, Rgba, Bgra, Argb, Abgr, Cmyk, Gray, GrayAlpha, AlphaGray };

            Layout ParseChannelMapping(ColorSpace colorSpace, string channelMappingType)
            {
                string channelMappingId = Util.TypeId(channelMappingType);
                List<string> tparams = Util.Tparams(channelMappingType);

                bool isRange = false;
                if (channelMappingId == "boost::mpl::range_c")
                {
                    tparams.RemoveAt(0);
                    isRange = true;
                }
                else if (channelMappingId == "boost::mpl::vector3_c"
                        || channelMappingId == "boost::mpl::vector4_c")
                {
                    tparams.RemoveAt(0);
                }
                else if (channelMappingId == "boost::mp11::mp_list")
                {
                    for (int i = 0; i < tparams.Count; ++i)
                    {
                        // integral_constant<T, N>
                        List<string> tparams2 = Util.Tparams(tparams[i]);
                        if (tparams2.Count >= 2)
                            tparams[i] = tparams2[1];
                    }
                }
                else
                    return Layout.Unknown;

                if (colorSpace == ColorSpace.Rgb)
                {
                    if (isRange && tparams.Count == 2
                        && tparams[0] == "0" && tparams[1] == "3")
                    {
                        return Layout.Rgb;
                    }
                    else if (!isRange && tparams.Count == 3
                        && tparams[0] == "0" && tparams[1] == "1" && tparams[2] == "2")
                    {
                        return Layout.Rgb;
                    }
                    else if (!isRange && tparams.Count == 3
                        && tparams[0] == "2" && tparams[1] == "1" && tparams[2] == "0")
                    {
                        return Layout.Bgr;
                    }
                }
                else if (colorSpace == ColorSpace.Rgba)
                {
                    if (isRange && tparams.Count == 2
                        && tparams[0] == "0" && tparams[1] == "4")
                    {
                        return Layout.Rgba;
                    }
                    else if (!isRange && tparams.Count == 4
                        && tparams[0] == "0" && tparams[1] == "1" && tparams[2] == "2" && tparams[3] == "3")
                    {
                        return Layout.Rgba;
                    }
                    else if (!isRange && tparams.Count == 4
                        && tparams[0] == "2" && tparams[1] == "1" && tparams[2] == "0" && tparams[3] == "3")
                    {
                        return Layout.Bgra;
                    }
                    else if (!isRange && tparams.Count == 4
                        && tparams[0] == "3" && tparams[1] == "0" && tparams[2] == "1" && tparams[3] == "2")
                    {
                        return Layout.Argb;
                    }
                    else if (!isRange && tparams.Count == 4
                        && tparams[0] == "3" && tparams[1] == "2" && tparams[2] == "1" && tparams[3] == "0")
                    {
                        return Layout.Abgr;
                    }
                }
                else if (colorSpace == ColorSpace.Cmyk)
                {
                    if (isRange && tparams.Count == 2
                        && tparams[0] == "0" && tparams[1] == "4")
                    {
                        return Layout.Cmyk;
                    }
                    else if (!isRange && tparams.Count == 4
                        && tparams[0] == "0" && tparams[1] == "1" && tparams[2] == "2" && tparams[3] == "3")
                    {
                        return Layout.Cmyk;
                    }
                }
                else if (colorSpace == ColorSpace.Gray)
                {
                    if (isRange && tparams.Count == 2
                        && tparams[0] == "0" && tparams[1] == "1")
                    {
                        return Layout.Gray;
                    }
                    else if (!isRange && tparams.Count == 1
                        && tparams[0] == "0")
                    {
                        return Layout.Gray;
                    }
                }
                else if (colorSpace == ColorSpace.GrayAlpha)
                {
                    if (isRange && tparams.Count == 2
                        && tparams[0] == "0" && tparams[1] == "2")
                    {
                        return Layout.GrayAlpha;
                    }
                    else if (!isRange && tparams.Count == 2
                        && tparams[0] == "0" && tparams[1] == "1")
                    {
                        return Layout.GrayAlpha;
                    }
                    else if (!isRange && tparams.Count == 2
                        && tparams[0] == "1" && tparams[1] == "0")
                    {
                        return Layout.AlphaGray;
                    }
                }
                return Layout.Unknown;
            }

            byte[] GetChannelsPlanar(byte[] memory, int pixelIndex, ChannelValueKind channelValueKind, int channelSize, int channelsCount)
            {
                byte[] result = new byte[channelsCount];

                int rowSize = memory.Length / channelsCount;
                int offset = pixelIndex * channelSize;

                if (channelSize == 1)
                {
                    for (int i = 0; i < channelsCount; ++i)
                        //Buffer.BlockCopy(memory, i * rowSize + offset, result, i, 1);
                        result[i] = memory[i * rowSize + offset];
                    if (channelValueKind == ChannelValueKind.SignedIntegral)
                        SignedToUnsigned(result);
                }
                else if (channelSize == 2)
                {
                    ushort[] tmp = new ushort[channelsCount];
                    for (int i = 0; i < channelsCount; ++i)
                        Buffer.BlockCopy(memory, i * rowSize + offset, tmp, 2 * i, 2);
                    if (channelValueKind == ChannelValueKind.SignedIntegral)
                        SignedToUnsigned(tmp);
                    ConvertChannels(tmp, result);
                }
                else if (channelSize == 4)
                {
                    if (channelValueKind == ChannelValueKind.UnsignedIntegral
                        || channelValueKind == ChannelValueKind.SignedIntegral)
                    {
                        uint[] tmp = new uint[channelsCount];
                        for (int i = 0; i < channelsCount; ++i)
                            Buffer.BlockCopy(memory, i * rowSize + offset, tmp, 4 * i, 4);
                        if (channelValueKind == ChannelValueKind.SignedIntegral)
                            SignedToUnsigned(tmp);
                        ConvertChannels(tmp, result);
                    }
                    else
                    {
                        float[] tmp = new float[channelsCount];
                        for (int i = 0; i < channelsCount; ++i)
                            Buffer.BlockCopy(memory, i * rowSize + offset, tmp, 4 * i, 4);
                        if (channelValueKind == ChannelValueKind.FloatingPoint)
                            ConvertChannels(tmp, result);
                        else // ScopedFloatingPoint
                            ConvertChannelsScoped(tmp, result);
                    }
                }
                else if (channelSize == 8)
                {
                    if (channelValueKind == ChannelValueKind.UnsignedIntegral
                        || channelValueKind == ChannelValueKind.SignedIntegral)
                    {
                        ulong[] tmp = new ulong[channelsCount];
                        for (int i = 0; i < channelsCount; ++i)
                            Buffer.BlockCopy(memory, i * rowSize + offset, tmp, 8 * i, 8);
                        if (channelValueKind == ChannelValueKind.SignedIntegral)
                            SignedToUnsigned(tmp);
                        ConvertChannels(tmp, result);
                    }
                    else
                    {
                        double[] tmp = new double[channelsCount];
                        for (int i = 0; i < channelsCount; ++i)
                            Buffer.BlockCopy(memory, i * rowSize + offset, tmp, 8 * i, 8);
                        if (channelValueKind == ChannelValueKind.FloatingPoint)
                            ConvertChannels(tmp, result);
                        else // ScopedFloatingPoint
                            ConvertChannelsScoped(tmp, result);
                    }
                }
                else
                {
                    result = null;
                }
                return result;
            }

            byte[] GetChannelsInterleaved(byte[] memory, int pixelIndex, ChannelValueKind channelValueKind, int channelSize, int channelsCount)
            {
                byte[] result = new byte[channelsCount];

                int offset = pixelIndex * channelSize * channelsCount;

                if (channelSize == 1)
                {
                    Buffer.BlockCopy(memory, offset, result, 0, channelsCount);
                    if (channelValueKind == ChannelValueKind.SignedIntegral)
                        SignedToUnsigned(result);
                }
                else if (channelSize == 2)
                {
                    ushort[] tmp = new ushort[channelsCount];
                    Buffer.BlockCopy(memory, offset, tmp, 0, 2 * channelsCount);
                    if (channelValueKind == ChannelValueKind.SignedIntegral)
                        SignedToUnsigned(tmp);
                    ConvertChannels(tmp, result);
                }
                else if (channelSize == 4)
                {
                    if (channelValueKind == ChannelValueKind.UnsignedIntegral
                        || channelValueKind == ChannelValueKind.SignedIntegral)
                    {
                        uint[] tmp = new uint[channelsCount];
                        Buffer.BlockCopy(memory, offset, tmp, 0, 4 * channelsCount);
                        if (channelValueKind == ChannelValueKind.SignedIntegral)
                            SignedToUnsigned(tmp);
                        ConvertChannels(tmp, result);
                    }
                    else
                    {
                        float[] tmp = new float[channelsCount];
                        Buffer.BlockCopy(memory, offset, tmp, 0, 4 * channelsCount);
                        if (channelValueKind == ChannelValueKind.FloatingPoint)
                            ConvertChannels(tmp, result);
                        else // ScopedFloatingPoint
                            ConvertChannelsScoped(tmp, result);
                    }
                }
                else if (channelSize == 8)
                {
                    if (channelValueKind == ChannelValueKind.UnsignedIntegral
                        || channelValueKind == ChannelValueKind.SignedIntegral)
                    {
                        ulong[] tmp = new ulong[channelsCount];
                        Buffer.BlockCopy(memory, offset, tmp, 0, 8 * channelsCount);
                        if (channelValueKind == ChannelValueKind.SignedIntegral)
                            SignedToUnsigned(tmp);
                        ConvertChannels(tmp, result);
                    }
                    else
                    {
                        double[] tmp = new double[channelsCount];
                        Buffer.BlockCopy(memory, offset, tmp, 0, 8 * channelsCount);
                        if (channelValueKind == ChannelValueKind.FloatingPoint)
                            ConvertChannels(tmp, result);
                        else // ScopedFloatingPoint
                            ConvertChannelsScoped(tmp, result);
                    }
                }
                else
                {
                    result = null;
                }
                return result;
            }

            void SignedToUnsigned(byte[] buffer)
            {
                for (int i = 0; i < buffer.Length; ++i)
                {
                    if (buffer[i] <= 127)
                        buffer[i] += 128;
                    else
                        buffer[i] -= 128;
                }
            }

            void SignedToUnsigned(ushort[] buffer)
            {
                for (int i = 0; i < buffer.Length; ++i)
                {
                    if (buffer[i] <= 32767)
                        buffer[i] += 32768;
                    else
                        buffer[i] -= 32768;
                }
            }

            void SignedToUnsigned(uint[] buffer)
            {
                for (int i = 0; i < buffer.Length; ++i)
                {
                    if (buffer[i] <= 2147483647)
                        buffer[i] += 2147483648;
                    else
                        buffer[i] -= 2147483648;
                }
            }

            void SignedToUnsigned(ulong[] buffer)
            {
                for (int i = 0; i < buffer.Length; ++i)
                {
                    if (buffer[i] <= 9223372036854775807)
                        buffer[i] += 9223372036854775808;
                    else
                        buffer[i] -= 9223372036854775808;
                }
            }

            void ConvertChannels(ushort[] src, byte[] dst)
            {
                for (int i = 0; i < src.Length; ++i)
                    dst[i] = (byte)((float)src[i] / ushort.MaxValue * byte.MaxValue);
            }

            void ConvertChannels(uint[] src, byte[] dst)
            {
                for (int i = 0; i < src.Length; ++i)
                    dst[i] = (byte)((double)src[i] / uint.MaxValue * byte.MaxValue);
            }

            void ConvertChannels(ulong[] src, byte[] dst)
            {
                for (int i = 0; i < src.Length; ++i)
                    dst[i] = (byte)((double)src[i] / ulong.MaxValue * byte.MaxValue);
            }

            void ConvertChannels(float[] src, byte[] dst)
            {
                for (int i = 0; i < src.Length; ++i)
                    dst[i] = (byte)(src[i] / float.MaxValue * byte.MaxValue);
            }

            void ConvertChannels(double[] src, byte[] dst)
            {
                for (int i = 0; i < src.Length; ++i)
                    dst[i] = (byte)(src[i] / double.MaxValue * byte.MaxValue);
            }

            // NOTE: Assuming that the channel's value is in [0, 1]
            //  though GIL allows different ranges too
            void ConvertChannelsScoped(float[] src, byte[] dst)
            {
                for (int i = 0; i < src.Length; ++i)
                    dst[i] = (byte)(src[i] * byte.MaxValue);
            }

            // TODO: Assuming that the channel's value is in [0, 1]
            //  though GIL allows different ranges too
            void ConvertChannelsScoped(double[] src, byte[] dst)
            {
                 for (int i = 0; i < src.Length; ++i)
                    dst[i] = (byte)(src[i] * byte.MaxValue);
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

            class GrayAlphaMapper : LayoutMapper
            {
                public override System.Drawing.Color GetColor(byte[] channels)
                {
                    return System.Drawing.Color.FromArgb(channels[1],
                                                         channels[0],
                                                         channels[0],
                                                         channels[0]);
                }
            }

            class AlphaGrayMapper : LayoutMapper
            {
                public override System.Drawing.Color GetColor(byte[] channels)
                {
                    return System.Drawing.Color.FromArgb(channels[0],
                                                         channels[1],
                                                         channels[1],
                                                         channels[1]);
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
                else if (layout == Layout.GrayAlpha)
                    layoutMapper = new GrayAlphaMapper();
                else if (layout == Layout.AlphaGray)
                    layoutMapper = new AlphaGrayMapper();
                return layoutMapper;
            }
        }
    }
}
