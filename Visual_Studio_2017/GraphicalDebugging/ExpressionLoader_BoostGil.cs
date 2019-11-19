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

                List<string> tparams = Util.Tparams(type);
                if (tparams.Count < 2)
                    return;
                string pixelType = tparams[0];
                string isPlanar = tparams[1];
                string pixelId = Util.BaseType(pixelType);
                if (pixelId != "boost::gil::pixel")
                    return;

                tparams = Util.Tparams(pixelType);
                if (tparams.Count < 2)
                    return;
                string channelValueType = tparams[0];
                string layoutType = tparams[1];
                // TODO: only 8-bit channels supported for now
                if (channelValueType != "unsigned char")
                    return;
                string layoutId = Util.BaseType(layoutType);
                if (layoutId != "boost::gil::layout")
                    return;

                tparams = Util.Tparams(layoutType);
                if (tparams.Count < 2)
                    return;
                string colorSpaceType = tparams[0];
                string channelMappingType = tparams[1];
                // TODO: support more
                string colorSpaceId = Util.BaseType(colorSpaceType);
                if (colorSpaceId != "boost::mpl::vector3")
                    return;
                string channelMappingId = Util.BaseType(channelMappingType);
                if (channelMappingId != "boost::mpl::range_c")
                    return;
                // TODO: only RGB for now
                tparams = Util.Tparams(colorSpaceType);
                if (tparams.Count < 3)
                    return;
                if (tparams[0] != "boost::gil::red_t"
                    || tparams[1] != "boost::gil::green_t"
                    || tparams[2] != "boost::gil::blue_t")
                    return;
                // TODO: only one mapping for now
                tparams = Util.Tparams(channelMappingType);
                if (tparams.Count < 3)
                    return;
                if (tparams[1] != "0"
                    || tparams[2] != "3")
                    return;

                // TODO: 8-bit RGB channels for now
                byte[] memory = new byte[width * height * 3];
                bool isLoaded = false;
                if (mreader != null)
                {
                    isLoaded = mreader.ReadBytes(name + "._memory[0]", memory);
                }

                if (!isLoaded)
                {
                    // TODO: Parsing the memory byte by byte may take very long time
                    //       even for small images. So it probably doesn't have sense.
                    for (int i = 0; i < width * height * 3; ++i)
                    {
                        // In MSVS17 unsigned chars seems to have hex values like \u0088
                        // even when HexDisplayMode is not set, so cast them to unsigned int
                        Expression expr = debugger.GetExpression("(unsigned int)" + name + "._memory[" + i + "]");
                        if (!expr.IsValidValue)
                            return;
                        memory[i] = (byte)Util.ParseInt(expr.Value, debugger.HexDisplayMode);
                    }
                }

                // Use Pixel format native to Gil Image?
                System.Drawing.Bitmap bmp = new System.Drawing.Bitmap(width, height);
                for (int j = 0; j < height; ++j)
                {
                    for (int i = 0; i < width; ++i)
                    {
                        int b = (j * width + i) * 3; // RGB
                        System.Drawing.Color c = System.Drawing.Color.FromArgb(memory[b],
                                                                               memory[b + 1],
                                                                               memory[b + 2]);
                        bmp.SetPixel(i, j, c);
                    }
                }

                result = new ExpressionDrawer.Image(bmp);
            }
        }
    }
}
