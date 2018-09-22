using GCNToolKit.Formats.Colors;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cross_View.Parser
{
    internal partial class F3DACEXParser
    {
        private static readonly List<ushort> DefaultPalette = new List<ushort>
        {
            RGB5A3.ToRGB5A3(0xFF000000),
            RGB5A3.ToRGB5A3(0xFF111111),
            RGB5A3.ToRGB5A3(0xFF222222),
            RGB5A3.ToRGB5A3(0xFF333333),
            RGB5A3.ToRGB5A3(0xFF444444),
            RGB5A3.ToRGB5A3(0xFF555555),
            RGB5A3.ToRGB5A3(0xFF666666),
            RGB5A3.ToRGB5A3(0xFF777777),
            RGB5A3.ToRGB5A3(0xFF888888),
            RGB5A3.ToRGB5A3(0xFF999999),
            RGB5A3.ToRGB5A3(0xFFAAAAAA),
            RGB5A3.ToRGB5A3(0xFFBBBBBB),
            RGB5A3.ToRGB5A3(0xFFCCCCCC),
            RGB5A3.ToRGB5A3(0xFFDDDDDD),
            RGB5A3.ToRGB5A3(0xFFEEEEEE),
            RGB5A3.ToRGB5A3(0xFFFFFFFF)
        };

        private static readonly string ImageOutputDirectory = Directory.CreateDirectory("C:\\Users\\olsen\\Documents\\Animal Crossing Model Images").FullName;

        private static readonly Dictionary<byte, string> uCodeOpCodes = new Dictionary<byte, string>
        {
            { 0x00, "NOOP" },
            { 0x01, "VTX" },
            { 0x02, "MODIFYVTX" },
            { 0x03, "CULLDL" },
            { 0x04, "BRANCH" },
            { 0x05, "TRI1" },
            { 0x06, "TRI2" },
            { 0x07, "QUAD" },
            { 0x08, "LINE3D" },
            { 0x09, "TRIN" },
            { 0x0A, "TRIN" },
            { 0x0B, "NOOP" },
            { 0x0C, "NOOP" },
            { 0x0D, "QUADN" },
            { 0xCE, "SETTEXEDGEALPHA" },
            { 0xCF, "SETCOMBINE" },
            { 0xD0, "SETCOMBINE" },
            { 0xD1, "NOOP" },
            { 0xD2, "SETTILE_DOLPHIN" },
            { 0xD3, "NOOP" },
            { 0xD4, "NOOP" },
            { 0xD5, "SPECIAL" },
            { 0xD6, "NOOP" },
            { 0xD7, "TEXTURE" },
            { 0xD8, "POPMTX" },
            { 0xD9, "GEOMETRYMODE" },
            { 0xDA, "MTX" },
            { 0xDB, "MOVEWORD" },
            { 0xDC, "MOVEMEM" },
            { 0xDD, "LOAD" },
            { 0xDE, "DL" },
            { 0xDF, "ENDDL" },
            { 0xE0, "SPNOOP" },
            { 0xE1, "RDPHALF" },
            { 0xE2, "SETOTHERMODE" },
            { 0xE3, "SETOTHERMODE" },
            { 0xE4, "TEXRECT" },
            { 0xE5, "NOOP" },
            { 0xE6, "RDPLOADSYNC" },
            { 0xE7, "RDPPIPESYNC" },
            { 0xE8, "RDPTILESYNC" },
            { 0xE9, "RDPFULLSYNC" },
            { 0xEA, "NOOP" },
            { 0xEB, "NOOP" },
            { 0xEC, "NOOP" },
            { 0xED, "SETSCISSOR" },
            { 0xEE, "SETPRIMDEPTH" },
            { 0xEF, "RDPSETOTHERMODE" },
            { 0xF0, "LOADTLUT" },
            { 0xF1, "NOOP" },
            { 0xF2, "SETTILESIZE" },
            { 0xF3, "LOADBLOCK" },
            { 0xF4, "LOADTILE" },
            { 0xF5, "SETTILE" },
            { 0xF6, "FILLRECT" },
            { 0xF7, "SETFILLCOLOR" },
            { 0xF8, "SETFOGCOLOR" },
            { 0xF9, "SETBLENDCOLOR" },
            { 0xFA, "SETPRIMCOLOR" },
            { 0xFB, "SETENVCOLOR" },
            { 0xFC, "SETCOMBINE" },
            { 0xFD, "SETTIMG" },
            { 0xFE, "SETZIMG" },
            { 0xFF, "SETCIMG" }
        };
    }
}
