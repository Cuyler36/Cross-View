using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using Cross_View.GL;
using GCNToolKit.Formats.Images;
using SharpGL;
using SharpGL.Enumerations;
using SharpGL.SceneGraph.Assets;
using SharpGL.Textures;
using Matrix = SharpGL.SceneGraph.Matrix;

namespace Cross_View.Parser
{
    public sealed class F3DZEX2
    {
        public F3DZEX2()
        {
            _commands = new List<GfxCmd>
            {
                new GfxCmd("NOOP", NoOp, Command.NOOP)
            };
        }

        private sealed class GfxCmd
        {
            public readonly string Name;
            public readonly Action<int, int> Method;
            public readonly Command Command;

            public GfxCmd(string name, Action<int, int> method, Command cmd)
            {
                Name = name;
                Method = method;
                Command = cmd;
            }
        }

        /// <summary>
        /// A mask used for converting GameCube RAM addresses to file addresses.
        /// </summary>
        private const int AddressMask = 0x017FFFFF;

        private enum Command : byte
        {
            NOOP = 0x00,
            VTX = 0x01,
            MODIFYVTX = 0x02,
            CULLDL = 0x03,
            BRANCH = 0x04,
            TRI1 = 0x05,
            TRI2 = 0x06,
            QUAD = 0x07,
            LINE3D = 0x08,
            TRIN = 0x09,
            TRIN_INDEPEND = 0x0A,
            // NOOP x2
            QUADN = 0x0D,
            SETTEXEDGEALPHA = 0xCE,
            SETCOMBINE_NOTEV = 0xCF,
            SETCOMBINE_TEV = 0xD0,
            // NOOP
            SETTILE_DOLPHIN =  0xD2,
            // NOOP x2
            SPECIAL = 0xD5,
            // NOOP
            TEXTURE = 0xD7,
            POPMTX = 0xD8,
            GEOMETRYMODE = 0xD9,
            MTX = 0xDA,
            MOVEWORD = 0xDB,
            MOVEMEM = 0xDC,
            LOAD = 0xDD,
            DL = 0xDE,
            ENDDL = 0xDF,
            SPNOOP = 0xE0,
            RDPHALF = 0xE1,
            SETOTHERMODE_L = 0xE2,
            SETOTHERMODE_H = 0xE3,
            TEXRECT = 0xE4,
            // NOOP
            RDPLOADSYNC = 0xE6,
            RDPPIPESYNC = 0xE7,
            RDPTILESYNC = 0xE8,
            RDPFULLSYNC = 0xE9,
            // NOOP x3
            SETSCISSOR = 0xED,
            SETPRIMDEPTH = 0xEE,
            RDPSETOTHERMODE = 0xEF,
            LOADTLUT = 0xF0,
            // NOOP
            SETTILESIZE = 0xF2,
            LOADBLOCK = 0xF3,
            LOADTILE = 0xF4,
            SETTILE = 0xF5,
            FILLRECT = 0xF6,
            SETFILLCOLOR = 0xF7,
            SETFOGCOLOR = 0xF8,
            SETBLENDCOLOR = 0xF9,
            SETPRIMCOLOR = 0xFA,
            SETENVCOLOR = 0xFB,
            SETCOMBINE = 0xFC,
            SETTIMG = 0xFD,
            SETZIMG = 0xFE,
            SETCIMG = 0xFF
        }

        private readonly List<GfxCmd> _commands;

        // MoveWord mem address types
        private const int G_MW_MATRIX = 0x00;
        private const int G_MW_NUMLIGHT = 0x02;
        private const int G_MW_CLIP = 0x04;
        private const int G_MW_SEGMENT = 0x06;
        private const int G_MW_FOG = 0x08;
        private const int G_MW_LIGHTCOL = 0x0A;
        private const int G_MW_FORCEMTX = 0x0C;
        private const int G_MW_PERSPNORM = 0x0E;

        // OtherModeHigh Shift Counts
        // TODO: Check that these are the same in the GC emulator compared to the N64.
        private const int G_MDSFT_ALPHADITHER = 4;
        private const int G_MDSFT_RGBDITHER = 6;
        private const int G_MDSFT_COMBKEY = 8;
        private const int G_MDSFT_TEXCONV = 9;
        private const int G_MDSFT_TEXTFILT = 12;
        private const int G_MDSFT_TEXTLUT = 14;
        private const int G_MDSFT_TEXTLOD = 16;
        private const int G_MDSFT_TEXTDETAIL = 17;
        private const int G_MDSFT_TEXTPERSP = 19;
        private const int G_MDSFT_CYCLETYPE = 20;
        private const int G_MDSFT_COLORDITHER = 22;
        private const int G_MDSFT_PIPELINE = 23;


        // OtherModeHigh values
        private const int CYCLETYPE_LEN = 0x02;
        private const int CYCLETYPE_SFT = 0x14;

        // OtherModeLow values
        private const int G_CYC_1CYCLE = 0 << G_MDSFT_CYCLETYPE;
        private const int G_CYC_2CYCLE = 1 << G_MDSFT_CYCLETYPE;
        private const int G_CYC_COPY   = 2 << G_MDSFT_CYCLETYPE;
        private const int G_CYC_FILL   = 3 << G_MDSFT_CYCLETYPE;

        // OtherModeLow states
        private const int Z_CMP = 0x0010;
        private const int Z_UPD = 0x0020;
        private const int ZMODE_DEC = 0x0C00;
        private const int CVG_X_ALPHA = 0x1000;
        private const int ALPHA_CVG_SEL = 0x2000;
        private const int FORCE_BL = 0x4000;

        // GeometryMode states
        private const int CULL_FRONT = 0x0200;
        private const int CULL_BACK = 0x0400;
        private const int LIGTING = 0x020000;

        // Matrix Flags
        private const int G_MTX_PUSH = 0x00;
        private const int G_MTX_NOPUSH = 0x01;

        private const int G_MTX_MUL = 0x00;
        private const int G_MTX_LOAD = 0x02;

        private const int G_MTX_MODELVIEW = 0x00;
        private const int G_MTX_PROJECTION = 0x04;

        // External objects
        private readonly OpenGL _gl;
        private readonly BinaryReader _reader;

        // Parser state objects
        private readonly List<Action<SceneRenderer>> _commandList = new List<Action<SceneRenderer>>();
        private readonly List<VertexAC> _vertexData = new List<VertexAC>();

        // The state of the emulator. !! NEEDS CONFIRMATION !!
        private byte _emuState;

        // If true, the draw list has finished executing.
        private bool _endDrawList;

        // Matrix
        private Matrix _mtx;
        private Matrix _modelViewMatrix;
        private Matrix _projectionMatrix; // no stack for the projection matrix.
        private readonly Matrix[] _positionMatrices = new Matrix[8];
        private readonly Stack<Matrix> _mtxStack = new Stack<Matrix>();

        // Vertices
        private readonly VertexAC[] _vertices = new VertexAC[0x80];
        private List<VertexAC> _verticesList = new List<VertexAC>();
        
        // Modes
        private int _geometryMode = 0;
        private Combiners _combiners = new Combiners();
        private int _combinerHigh;
        private int _combinerLow;
        private int _otherModeLow = 0;
        private int _otherModeHigh = 0; //G_CYC_2CYCLE << CYCLETYPE_SFT;

        // Colors
        private Color _primitiveColor = Color.FromArgb(255, 255, 255, 255);
        private Color _environmentColor = Color.FromArgb(0, 0, 0, 127);
        private Color _fillColor = Color.FromArgb(0, 0, 0, 0);
        private Color _fogColor = Color.FromArgb(0, 0, 0, 0);
        private Color _blendColor = Color.FromArgb(0, 0, 0, 0);

        // Texture
        private TextureAC _currentTexture;
        private int _currentTextureWidth;
        private int _currentTextureHeight;
        private Dictionary<int, TextureAC> _textures = new Dictionary<int, TextureAC>();
        private ushort[] _palette;
        private readonly KeyValuePair<uint, ushort[]>[] _savedPalettes = new KeyValuePair<uint, ushort[]>[16];

        // Move Word stack
        private readonly uint[] _moveWordStack = new uint[16];

        // Gfx* DrawList Stack
        private readonly Stack<uint> _drawListStack = new Stack<uint>();

        // Current Gfx Pointer
        private uint _currentGfxPointer;

        // Debug Flag
        private byte _debugFlags = 0;

        private void NoOp(int wordA, int wordB)
        {
            switch ((wordA >> 16) & 0xFF)
            {
                case 5:
                    _debugFlags = (byte) (wordA & 0xFFFF);
                    break;
                case 9:
                    // TODO: Figure out what this is used for
                    // emu64 + 0x28 += 1;
                    break;
            }
        }

        private void ReadVertices(long address, int bufferIndex, int count)
        {
            _reader.BaseStream.Seek(address, SeekOrigin.Begin);

            for (var i = bufferIndex; i < bufferIndex + count; i++)
            {
                _vertices[i] = new VertexAC(_reader.ReadInt16().Reverse(), _reader.ReadInt16().Reverse(),
                    _reader.ReadInt16().Reverse(),
                    _reader.ReadInt16().Reverse(),
                    (short) (_reader.ReadInt16().Reverse() * 0.03125), (short) (_reader.ReadInt16().Reverse() * 0.03125),
                    _reader.ReadByte(), _reader.ReadByte(), _reader.ReadByte(), _reader.ReadByte());
            }
        }

        private void Vtx(int wordA, int wordB)
        {
            var count = (wordA >> 12) & 0xFF;
            var startBufferIndex = ((wordA >> 1) & 0x7F) - count;
            var address = wordB & AddressMask;

            ReadVertices(address, startBufferIndex, count);
        }

        private void OpenGLFlushDraw()
        {
            
        }

        private (int, int, int) GetTriangleVertices(int word) =>
            ((word >> 17) & 0x7F, (word >> 9) & 0x7F, (word >> 1) & 0x7F);

        private void Tri1(int wordA, int wordB)
        {
            // Flush texture?
            var (vertexA, vertexB, vertexC) = GetTriangleVertices(wordA);
            _verticesList.Add(_vertices[vertexA]);
            _verticesList.Add(_vertices[vertexB]);
            _verticesList.Add(_vertices[vertexC]);
        }

        private void Tri2(int wordA, int wordB)
        {
            // Flush texture?
            var (vertexA, vertexB, vertexC) = GetTriangleVertices(wordA);
            _verticesList.Add(_vertices[vertexA]);
            _verticesList.Add(_vertices[vertexB]);
            _verticesList.Add(_vertices[vertexC]);

            var (vertexD, vertexE, vertexF) = GetTriangleVertices(wordB);
            _verticesList.Add(_vertices[vertexD]);
            _verticesList.Add(_vertices[vertexE]);
            _verticesList.Add(_vertices[vertexF]);
        }

        private void GeometryMode(int wordA, int wordB)
        {
            // Flush draw?
            _geometryMode = (_geometryMode & (~wordA & 0x00FFFFFF)) | wordB;

            _commandList.Add(delegate(SceneRenderer renderer)
            {
                // TODO: Apply flags
            });
        }

        private void SetOtherModeLow(int wordA, int wordB)
        {
            // Flush draw

            var len = (wordA & 0xFF) + 1;
            var sft = Math.Max(0, 32 - ((wordA >> 8) & 0xFF) - len);
            var mask = ((1 << len) - 1) << sft;

            _otherModeLow = (_otherModeLow & ~mask) | (wordB & mask);

            _commandList.Add(delegate(SceneRenderer renderer)
            {
                // TODO: Apply flags

                if ((_otherModeLow & ZMODE_DEC) != 0)
                {
                    renderer.GL.Enable(OpenGL.GL_POLYGON_OFFSET_FILL);
                    renderer.GL.PolygonOffset(-0.5f, -0.5f);
                }
                else
                {
                    renderer.GL.Disable(OpenGL.GL_POLYGON_OFFSET_FILL);
                }
            });
        }

        private void SetOtherModeHigh(int wordA, int wordB)
        {
            // Flush draw

            var len = (wordA & 0xFF) + 1;
            var sft = Math.Max(0, 32 - ((wordA >> 8) & 0xFF) - len);
            var mask = ((1 << len) - 1) << sft;

            _otherModeHigh = (_otherModeHigh & ~mask) | (wordB & mask);
        }

        private void DrawList(int wordA, int wordB)
        {
            var address = GetAddressFromSegment(wordB);
            switch ((wordA >> 16) & 0xFF)
            {
                case 0:
                    // if (printDisplayListCall) Console.WriteLine($"gsSPDisplayList({address}),\n");
                    if (_drawListStack.Count >= 0x12)
                    {
                        Console.WriteLine("*** DL stack overflow ***");
                    }
                    else
                    {
                        _drawListStack.Push(_currentGfxPointer);
                    }

                    _currentGfxPointer = address - 8; // We subtract 8 so when this instruction finishes, the first one of the new list isn't skipped.
                    break;

                case 1: // Set the current Gfx* to this draw list and do not return to the currently executing one.
                    _currentGfxPointer = address - 8;
                    break;

                default: // If the emulator's state is 0 (what is this?), then execute the draw list in GC format.
                    if (_emuState == 0)
                    {
                        // GXCallDisplayList(address, (wordA >> 8) & 0xFFFF);
                    }

                    break;
            }
        }

        private void EndDrawList(int wordA, int wordB)
        {
            if (_drawListStack.Count > 0)
            {
                _currentGfxPointer = _drawListStack.Pop() - 8; // Subtract 8 so we don't skip the first command after returning execution to this list.
            }
            else
            {
                _endDrawList = true;
            }
        }

        private void Mtx(int wordA, int wordB)
        {
            const float mtxMultiplier = 1.0f / 65536.0f;
            var address = GetAddressFromSegment(wordB);
            var mtxFlags = wordA & 0xFF;

            var offset = address & AddressMask;

            // TODO: Looks like these are stored as 3x4 matricies. The last X values are skipped. Size is 0x30, and there are 12 stfs under MODELVIEW codepath.
            // Interestingly, the debug code for these still print the entire 4x4.
            var mtx = new Matrix(4, 4);

            // Load new matrix TODO: Figure out if this is right. Right now it's loading the matrix in reverse of what it *should* be.
            for (var col = 0; col < mtx.Columns; col++)
            {
                for (var row = 0; row < mtx.Rows; row++)
                {
                    _reader.BaseStream.Seek(offset, SeekOrigin.Begin);
                    var val1 = _reader.ReadInt16().Reverse();
                    _reader.BaseStream.Seek(offset + 0x20, SeekOrigin.Begin);
                    var val2 = _reader.ReadUInt16().Reverse();
                    mtx[row, col] = val1 + val2 * mtxMultiplier;
                    offset += 2;
                }
            }

            if ((mtxFlags & G_MTX_PROJECTION) == 0) // Modelview
            {
                if ((mtxFlags & G_MTX_NOPUSH) == 0 && _mtxStack.Count < 9)
                    _mtxStack.Push(_mtx);
                

                // Multiply the matrix by the current matrix if G_MTX_MUL flag is set
                // Technically only a 3x3 matrix is multiplied.
                _mtx = (mtxFlags & G_MTX_MUL) != 0 ? Matrix.Multiply(_mtx, mtx) : mtx;

                _modelViewMatrix = new Matrix(3, 3);
                for (var col = 0; col < 3; col++)
                {
                    for (var row = 0; row < 3; row++)
                    {
                        _modelViewMatrix[row, col] = _mtx[row, col];
                    }
                }
            }
            else // Projection
            {
                // TODO: A lot more is going on here. Finish it.
                var mtx2 = new Matrix(mtx);
                if ((mtxFlags & G_MTX_MUL) == 0)
                {
                    _projectionMatrix = mtx2;
                }
                else
                {
                    _projectionMatrix = new Matrix(Matrix.Identity(3)); // This generates a 3x3 matrix. In emu64, it's a 4x3.
                }
            }

            _positionMatrices[_mtxStack.Count - 1] = Matrix.Multiply(_projectionMatrix, _mtx);
        }

        private void PopMtx(int wordA, int wordB)
        {
            // Technically the matrix stack's counter is just decremented by wordB >> 6.
            for (var i = 0; i < wordB >> 6; i++)
            {
                _mtx = _mtxStack.Pop();
            }

            // TODO: Refresh position matrix & redraw.
        }

        private void Texture(int wordA, int wordB)
        {
            var scaleS = ((float) (wordB >> 16) + 1) / 0x10000;
            var scaleT = ((float) wordB + 1) / 0x10000;

            //if (_currentTexture)
        }

        private static Command GetOpCode(int wordA) => (Command) ((wordA >> 24) & 0xFF);

        private void SetCombine(int wordA, int wordB)
        {
            var high = _combinerHigh ^ wordA;
            var low = _combinerLow ^ wordB;

            if ((high | low) != 0)
            {
                _combinerHigh = high;
                _combinerLow = low;
            }

            // TODO: This pulls from _emu64_class->currentDLOpCode. Do I want to emulate this behavior? SETCOMBINE_NOTEV modifies its instruction to be SETCOMINE.
            if (GetOpCode(wordA) != Command.SETCOMBINE_NOTEV)
            {
                // TODO: emu64::replace_combine_to_tev(Gfx* currentDrawList);
            }

            /*
            _combiners.ColorCombiner[0].SubA = (wordA >> 20) & 0xF;
            _combiners.ColorCombiner[0].SubB = (wordB >> 28) & 0xF;
            _combiners.ColorCombiner[0].Mul = (wordA >> 15) & 0x1F;
            _combiners.ColorCombiner[0].Add = (wordB >> 15) & 0x7;

            _combiners.ColorCombiner[1].SubA = (wordA >> 5) & 0xF;
            _combiners.ColorCombiner[1].SubB = (wordB >> 24) & 0xF;
            _combiners.ColorCombiner[1].Mul = wordA & 0x1F;
            _combiners.ColorCombiner[1].Add = (wordB >> 6) & 7;

            _combiners.AlphaCombiner[0].SubA = (wordA >> 12) & 7;
            _combiners.AlphaCombiner[0].SubB = (wordB >> 12) & 7;
            _combiners.AlphaCombiner[0].Mul = (wordA >> 9) & 7;
            _combiners.AlphaCombiner[0].Add = (wordB >> 9) & 7;

            _combiners.AlphaCombiner[1].SubA = (wordB >> 21) & 7;
            _combiners.AlphaCombiner[1].SubB = (wordB >> 3) & 7;
            _combiners.AlphaCombiner[1].Mul = (wordB >> 18) & 7;
            _combiners.AlphaCombiner[1].Add = wordB & 7;
            */
        }

        // TODO: This is a hack. It essentially does the same thing, but without the check on the opcode. Do I want to make this its own function?
        private void SetCombineTextureEnvironment(int wordA, int wordB) => SetCombine(wordA, wordB);

        // TODO: This is a hack. It essentially does the same thing, but without the check on the opcode. It also sets its opcode to SETCOMBINE.
        // TODO: Do I want to make this its own function?
        private void SetCombineNoTextureEnvironment(int wordA, int wordB) => SetCombine(wordA, wordB);

        private void SetEnvironmentColor(int wordA, int wordB)
        {
            // Flush draw

            _environmentColor = Color.FromArgb((byte) wordB, (byte) (wordB >> 24), (byte) (wordB >> 16),
                (byte) (wordB >> 8));
        }

        private void SetPrimitiveColor(int wordA, int wordB)
        {
            // Flush draw

            _primitiveColor = Color.FromArgb((byte)wordB, (byte)(wordB >> 24), (byte)(wordB >> 16),
                (byte)(wordB >> 8));
        }

        private void MoveWord(int wordA, int wordB)
        {
            switch ((wordA >> 16) & 0xFF)
            {
                case G_MW_NUMLIGHT:
                    byte temp = 0; // TODO: Figure out where this byte is set initially.
                    var result = (uint) ((0xAAAAAAAB * (ulong) wordB) >> 32);
                    var b = (byte)(result >> 4);
                    if (b != temp)
                    {
                        temp = b;
                    }
                    Console.WriteLine("MOVEWORD: G_MW_NUMLIGHT was encountered!");
                    break;
                case G_MW_CLIP:
                    // _emu64_class->currentDLInstructionPointer += 0x18; // TODO: Does this need to be implemented?
                    Console.WriteLine("MOVEWORD: G_MW_CLIP was encountered!");
                    break;
                case G_MW_SEGMENT:
                    var offset = (ushort) wordA;
                    var argAddress = (uint) wordB;
                    var address = 0x80000000 | (uint) (wordB & 0x0FFFFFFF);
                    var index = offset >> 2;
                    _moveWordStack[index] = address;

                    if (index < 16)
                    {
                        if (argAddress != 0)
                        {
                            if (address < 0x80000000 || address > 0x83000000)
                            {
                                // TODO: Change this to a Console.WriteLine if this path needs implemented.
                                // TODO: implement emu64::segchk(ulong) ?
                                throw new Exception($"SPSegment found Illegal Address.\ngsSPSegmentA no={index}\nbase=Not Emulated\ngfxp={address}");
                            }
                        }
                    }

                    break;
                case G_MW_FOG:
                    // TODO: Does this need to be implemented?
                    var fogA = (ushort) (wordB >> 16);
                    var fogB = (ushort) wordB;

                    Console.WriteLine($"MOVEWORD: G_MW_FOG was encountered! fogA: {fogA:X4} | fogB: {fogB:X4}");
                    break;
                case G_MW_LIGHTCOL:
                    Console.WriteLine("MOVEWORD: G_MW_LIGHTCOL was encountered!");
                    break;
                default:
                    Console.WriteLine("An unknown instruction was encountered!");
                    break;
            }
        }

        private uint GetAddressFromSegment(int segment)
        {
            var segAddress = (uint) segment;
            uint address;
            if (segAddress >> 28 != 0)
            {
                address = segAddress;
            }
            else
            {
                if (segAddress < 0x03000000)
                {
                    throw new Exception($"The requested segment address {segAddress:X8} is invalid!");
                }

                address = _moveWordStack[(segAddress >> 24) & 0xF] + (segAddress & 0x00FFFFFF);
            }

            if (((address >> 31) & 1) == 0 || address < 0x80000000 || address >= 0x83000000)
            {
                throw new Exception($"Bad address. {segAddress:X8} -> {address:X8}");
            }

            return address;
        }

        private void SetTextureImage(int wordA, int wordB)
        {
            var width = ((wordA >> 8) & 0x3FF) + 1;
            var height = (((wordA >> 10) & 0xFF) + 1) * 4;
            var size = (BitsPerPixel)((wordA >> 19) & 3);
            var format = (TextureFormat)((wordA >> 21) & 7);

            var address = (int) GetAddressFromSegment(wordB);

            // Look up if we've already decoded this texture, and skip doing so again if we have.
            if (_textures.ContainsKey(address))
            {
                _currentTexture = _textures[address];
                _currentTextureHeight = _currentTexture.Height;
                _currentTextureWidth = _currentTexture.Width;

                // If the texture already exists, we're done.
                if (_currentTexture.Texture != null) return;
            }
            else
            {
                // Create a new texture and add it to the dictionary.
                _currentTextureHeight = height;
                _currentTextureWidth = width;
                _currentTexture = new TextureAC
                {
                    Width = width,
                    Height = height,
                    Bpp = size,
                    Format = format
                };

                _textures[address] = _currentTexture;
            }

            _reader.BaseStream.Seek(address & AddressMask, SeekOrigin.Begin);

            int[] pixelData;
            switch (format)
            {
                case TextureFormat.CI when size == BitsPerPixel.Size4bpp:
                    pixelData = C4.DecodeC4(_reader.ReadBytes((width * height) / 2), _palette.ToArray(), width, height);
                    break;
                case TextureFormat.CI when size == BitsPerPixel.Size8bpp:
                    pixelData = C8.DecodeC8(_reader.ReadBytes(width * height), _palette.ToArray(), width, height);
                    break;

                // TODO: I & IA format textures need to be combined with the current primitive color using color combiner settings.
                case TextureFormat.IA when size == BitsPerPixel.Size4bpp:
                    pixelData = IA4.DecodeIA4(_reader.ReadBytes(width * height), width, height);
                    break;
                case TextureFormat.IA when size == BitsPerPixel.Size8bpp:
                    pixelData = IA8.DecodeIA8(_reader.ReadBytes((width * height) * 2), width, height);
                    break;

                case TextureFormat.I when size == BitsPerPixel.Size4bpp:
                    pixelData = I4.DecodeI4(_reader.ReadBytes((width * height) / 2), width, height);
                    break;
                case TextureFormat.I when size == BitsPerPixel.Size8bpp:
                    pixelData = I8.DecodeI8(_reader.ReadBytes(width * height), width, height);
                    break;

                default:
                    throw new Exception($"The format {format.ToString()} with a bpp of {size.ToString()} isn't handled!");
            }

            var imgData = new byte[pixelData.Length * 4];
            Buffer.BlockCopy(pixelData, 0, imgData, 0, imgData.Length);
        }

        private void SetScissor(int wordA, int wordB)
        {
            var x0 = (wordA >> 14) & 0x3FF;
            var y0 = (wordA >> 02) & 0x3FF;

            var x1 = (wordB >> 14) & 0x3FF;
            var y1 = (wordB >> 02) & 0x3FF;

            _gl.Scissor(x0, y0, x1 - x0, y1 - y0); // TODO: Is this right? The third & fourth arguments are width and height respectively.
                                                   // TODO: In gsGSPSetScissor they're x1 and y1.
        }

        private void LoadTextureLookUpTable(int wordA, int wordB)
        {
            var type = (wordA >> 22) & 3;
            var slot = (wordA >> 16) & 0xF;
            var count = wordA & 0x3FFF;

            if (type == 2)
            {
                var calculatedAddress = GetAddressFromSegment(wordB);

                if (_savedPalettes[slot].Key == calculatedAddress)
                {
                    _palette = _savedPalettes[slot].Value;
                    return;
                }

                if ((calculatedAddress & 0x1F) != 0)
                {
                    Console.WriteLine($"Palette 0x{calculatedAddress:X8} is not aligned to 32 bytes!");
                    calculatedAddress &= 0xFFFFFFE0;
                }

                var palette = new ushort[count];
                    
                // only RGB5A3 palettes are used
                _reader.BaseStream.Seek(calculatedAddress & AddressMask, SeekOrigin.Begin);
                for (var i = 0; i < count; i++)
                {
                    palette[i] = _reader.ReadUInt16().Reverse();
                }

                _savedPalettes[slot] = new KeyValuePair<uint, ushort[]>(calculatedAddress, palette);
            }
            else
            {
                throw new Exception($"Unhandled Texture Lookup Table value of {type} was encountered!");
            }
        }

        private void FillRectangle(float x0, float y0, float x1, float x2)
        {
            if ((_otherModeHigh & G_CYC_2CYCLE) != 0)
            {

            }
            else // One Cycle
            {

            }
        }

        private void FillRectangle(int wordA, int wordB)
        {
            var x0 = (float)((wordA >> 14) & 0x3FF);
            var y0 = (float)((wordA >> 02) & 0x3FF);

            var x1 = (float)((wordB >> 14) & 0x3FF);
            var y1 = (float)((wordB >> 02) & 0x3FF);

            FillRectangle(x0, y0, x1, y1);
        }

        private uint GetWrapMode(int wrapMode)
        {
            switch (wrapMode)
            {
                case 1: return OpenGL.GL_MIRRORED_REPEAT;
                case 2: return OpenGL.GL_CLAMP_TO_EDGE;
                case 3: return OpenGL.GL_CLAMP_TO_EDGE; // TODO: Is this right?
                default: return OpenGL.GL_REPEAT;
            }
        }

        private uint GetFormat(TextureFormat format)
        {
            switch (format)
            {
                case TextureFormat.RGBA: return OpenGL.GL_BGRA;
                case TextureFormat.YUV: return OpenGL.GL_BGRA;
                case TextureFormat.CI: return OpenGL.GL_BGRA;
                case TextureFormat.IA: return OpenGL.GL_LUMINANCE_ALPHA; // TODO: These are in ALLL (L = Luminance) format. Should it be LLLA? Might need to modify GCNToolKit.
                case TextureFormat.I: return OpenGL.GL_LUMINANCE_ALPHA; // Should this be OpenGL.GL_LUMINANCE?
                default: return OpenGL.GL_BGRA;
            }
        }

        private void TransferTexture(ref TextureAC textureInfo)
        {
            var texture = new Texture2D();
            texture.Bind(_gl);
            texture.SetParameter(_gl, OpenGL.GL_TEXTURE_MIN_FILTER, OpenGL.GL_LINEAR);
            texture.SetParameter(_gl, OpenGL.GL_TEXTURE_MAG_FILTER, OpenGL.GL_LINEAR);
            texture.SetParameter(_gl, OpenGL.GL_TEXTURE_WRAP_S, GetWrapMode(textureInfo.WrapModeS));
            texture.SetParameter(_gl, OpenGL.GL_TEXTURE_WRAP_T, GetWrapMode(textureInfo.WrapModeT));
            
            // Load the texture into OpenGL
            texture.SetPixels(_gl, textureInfo.TextureData, textureInfo.Width, textureInfo.Height, GetFormat(textureInfo.Format));

            // Set the texture in textureInfo
            textureInfo.Texture = texture;
        }

        private void AddVertexGroup5Bit(ulong data, int setIdx)
        {
            var (vertexA, vertexB, vertexC) = GetFaceVertexSet5Bit(data, setIdx);
            _verticesList.Add(vertexA);
            _verticesList.Add(vertexB);
            _verticesList.Add(vertexC);
        }

        private void AddVertexGroup7Bit(ulong data, int setIdx)
        {
            var (vertexA, vertexB, vertexC) = GetFaceVertexSet7Bit(data, setIdx);
            _verticesList.Add(vertexA);
            _verticesList.Add(vertexB);
            _verticesList.Add(vertexC);
        }

        private void DrawTriangle(int wordA, int wordB)
        {
            if (_vertices == null) return;

            var mtxIdx = (wordB >> 4) & 0x1F; // Not to sure how important this is.

            var faceCount = ((wordA >> 17) & 0x7F) + 1; // Get the total number of faces in the model
            var facesLeft = faceCount;
            var firstPassDone = false;
            var data = ((ulong) wordA << 32) | (uint) wordB;

            while (facesLeft > 0)
            {
                if ((data & 1) != 0) // Lowermost bit is "vertex bitsize" flag. If set, each vertex will be 7 bits instead of 5.
                {
                    AddVertexGroup7Bit(data, 0);
                    facesLeft--;
                    if (facesLeft == 0) return; // Check to see if we're done with the faces

                    AddVertexGroup7Bit(data, 1);
                    facesLeft--;
                    if (facesLeft == 0) return;

                    if (firstPassDone)
                    {
                        AddVertexGroup7Bit(data, 2);
                        facesLeft--;
                        if (facesLeft == 0) return;
                    }
                    else
                    {
                        firstPassDone = true;
                    }
                }
                else
                {
                    AddVertexGroup5Bit(data, 0);
                    facesLeft--;
                    if (facesLeft == 0) return; // Check to see if we're done with the faces

                    AddVertexGroup5Bit(data, 1);
                    facesLeft--;
                    if (facesLeft == 0) return;

                    AddVertexGroup5Bit(data, 2);
                    facesLeft--;
                    if (facesLeft == 0) return;

                    // Only do this after the first 64 bit section (since the first byte is the opcode (0x0A) and the second byte is (number of faces - 1) * 2)
                    if (firstPassDone)
                    {
                        AddVertexGroup5Bit(data, 3);
                        facesLeft--;
                        if (facesLeft == 0) return;
                    }
                    else
                    {
                        firstPassDone = true;
                    }
                }

                data = ((ulong) _reader.ReadUInt32().Reverse() << 32) | _reader.ReadUInt64().Reverse();
            }
        }

        private (VertexAC, VertexAC, VertexAC) GetFaceVertexSet5Bit(ulong data, int index)
        {
            var baseShiftCount = 4 + index * 15;
            return (_vertices[(data >> baseShiftCount) & 0x1F], _vertices[(data >> (baseShiftCount + 5)) & 0x1F],
                _vertices[(data >> (baseShiftCount + 10)) & 0x1F]);
        }

        private (VertexAC, VertexAC, VertexAC) GetFaceVertexSet7Bit(ulong data, int index)
        {
            var baseShiftCount = 1 + index * 21;
            return (_vertices[(data >> baseShiftCount) & 0x7F], _vertices[(data >> (baseShiftCount + 7)) & 0x7F],
                _vertices[(data >> (baseShiftCount + 14)) & 0x7F]);
        }

        private void DrawTriangleIndependent(int wordA, int wordB) => DrawTriangle(wordA, wordB);

        private void ExecuteDrawList(long offset)
        {
            _reader.BaseStream.Seek(offset, SeekOrigin.Begin);
            _endDrawList = false;
            while (!_endDrawList)
            {
                var wordA = _reader.ReadInt32();
                var wordB = _reader.ReadInt32();
                var opCode = (Command) ((wordA >> 24) & 0xFF);
                switch (opCode)
                {
                    case Command.VTX:
                        Vtx(wordA, wordB);
                        break;
                    case Command.MOVEWORD:
                        MoveWord(wordA, wordB);
                        break;
                    case Command.MTX:
                        Mtx(wordA, wordB);
                        break;
                    case Command.LOADTLUT:
                        LoadTextureLookUpTable(wordA, wordB);
                        break;
                    case Command.POPMTX:
                        PopMtx(wordA, wordB);
                        break;
                    case Command.TRI1:
                        Tri1(wordA, wordB);
                        break;
                    case Command.TRI2:
                        Tri2(wordA, wordB);
                        break;
                    case Command.TEXTURE:
                        Texture(wordA, wordB);
                        break;
                    case Command.SETENVCOLOR:
                        SetEnvironmentColor(wordA, wordB);
                        break;
                    case Command.SETPRIMCOLOR:
                        SetPrimitiveColor(wordA, wordB);
                        break;
                    case Command.GEOMETRYMODE:
                        GeometryMode(wordA, wordB);
                        break;
                    case Command.SETOTHERMODE_L:
                        SetOtherModeLow(wordA, wordB);
                        break;
                    case Command.SETOTHERMODE_H:
                        SetOtherModeHigh(wordA, wordB);
                        break;
                    case Command.SETTIMG:
                        SetTextureImage(wordA, wordB);
                        break;
                    case Command.SETCOMBINE:
                        SetCombine(wordA, wordB);
                        break;
                    case Command.TRIN:
                        DrawTriangle(wordA, wordB);
                        break;
                    case Command.TRIN_INDEPEND:
                        DrawTriangleIndependent(wordA, wordB);
                        break;
                    case Command.DL:
                        DrawList(wordA, wordB);
                        break;
                    case Command.ENDDL:
                        EndDrawList(wordA, wordB);
                        break;
                    default:
                        Console.WriteLine(
                            $"uCode op code {opCode.ToString()} was encountered, but is not handled yet!");
                        break;
                }
            }
        }

        private bool ExecuteCommand(Command cmd, int w0, int w1)
        {
            var gfxcmd = _commands.FirstOrDefault(c => c.Command == cmd);
            if (gfxcmd == null) return false;

            try
            {
                gfxcmd.Method(w0, w1);
                return true;
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.StackTrace);
                return false;
            }
        }
    }
}
