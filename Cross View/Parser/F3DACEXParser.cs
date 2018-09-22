using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using Cross_View.GL;
using GCNToolKit.Formats.Colors;
using GCNToolKit.Formats.Images;
using SharpGL;
using Matrix = SharpGL.SceneGraph.Matrix;

namespace Cross_View.Parser
{
    internal partial class F3DACEXParser
    {
        private readonly BinaryReader _reader;
        private OpenGL _gl;
        private int _baseIndex;

        private Point3D[] _vertices;
        private VertexAC[] _acVertices;
        private TextureAC _texture;
        private TextureModes _textureModes;
        private bool _endList;
        private uint _vtxOffset;
        private Color _primitiveColor;
        private int _environmentColor;
        private int _blendColor;
        private int _fogColor;
        private int _fillColor;
        private List<ushort> _palette;
        private int _textureIndex;

        private Matrix _matrix;
        private Matrix[] _matrixStack;


        private enum OtherModeH
        {
            CycleTypeLen = 0x02,
            CycleTypeSft = 0x14
        }

        private enum CycleType
        {
            OneCycle = 0,
            TwoCycle = 1,
            Copy = 2,
            Fill = 3
        }

        public F3DACEXParser(OpenGL gl, BinaryReader fileReader)
        {
            _reader = fileReader;
            _gl = gl;

            _baseIndex = 0;
            _endList = false;
            _vtxOffset = 0;
            _environmentColor = 0;
            _blendColor = 0;
            _fogColor = 0;
            _fillColor = 0;
            _palette = DefaultPalette;
            _textureIndex = 1;
            _textureModes = new TextureModes();
        }

        private int RunModelRoutine(byte uCode, int index, in byte[] data)
        {
            Debug.WriteLine("uCode: " + uCodeOpCodes[uCode]);
            switch (uCode)
            {
                case 0x00:
                    return NoOp();
                case 0x01:
                    return SetVertices(data, index, _reader);
                case 0x02:
                    return ModifyVertex(data, index);
                case 0x06:
                    return Draw2Triangles(data, index);
                case 0x09:
                    return DrawTriangle(data, index / 4, _reader) - index;
                case 0x0A:
                    return DrawTriangleIndependent(data, index / 4, _reader) - index;
                case 0xD2:
                    return SetTileDolphin(data, index);
                case 0xD7:
                    return SetTextureInfo(data, index);
                case 0xD9:
                    return SetGeometryMode(data, index);
                case 0xDE:
                    return DrawList(data, index);
                case 0xDF:
                    return EndDisplayList();
                case 0xE2:
                    return SetOtherModeLow(data, index);
                case 0xF0:
                    return LoadTextureLookUpTable(data, index, _reader);
                case 0xF7:
                    return SetFillColor(data, index);
                case 0xF8:
                    return SetFogColor(data, index);
                case 0xF9:
                    return SetBlendColor(data, index);
                case 0xFA:
                    return SetPrimativeColor(data, index);
                case 0xFB:
                    return SetEnvironmentColor(data, index);
                case 0xFC:
                    return SetColorCombinerMode(data, index);
                case 0xFD:
                    return SetTextureImage(data, index, _reader); // Technically the one responsible for drawing it is 0xD2 (dl_G_SETTILE_DOLPHIN)
                case 0xFE:
                case 0xFF:
                    return 8;
                default:
                    return 8;
            }
        }

        private int NoOp() => 8;

        private int ModifyVertex(byte[] Data, int Index)
        {
            int VertexToModify = Data[Index + 1];
            int VertexBufferIndex = ((Data[Index + 2] << 8) | Data[Index + 3]) / 2;
            int NewValue = (Data[Index + 4] << 24) | (Data[Index + 5] << 16) | (Data[Index + 6] << 8) | Data[Index + 7];

            return 8;
        }

        private int SetVertices(in byte[] data, int index, BinaryReader reader = null)
        {
            int NumVertices = ((data[index + 1] & 0x0F) << 4) | ((data[index + 2] & 0xF0) >> 4); // The total number of vertices loaded (past the specified start vertex)
            int VertexBufferIndex = data[index + 3]; // To Decode it: (data[index +3] >> 1) - NumVertices; (This is useless for decoding)
            int VertexOffset = BitConverter.ToInt32(data, 4).Reverse();

            Debug.WriteLine($"Verticies: {NumVertices} | Vertex Buffer index: {VertexBufferIndex} | Vertex Address: {VertexOffset:X8}");

            if (reader != null)
            {
                _vtxOffset = (uint)(VertexOffset & ~0x80000000);
                _baseIndex = 0;
                
                var (vertices, acVectors) = LoadVertices(reader, _vtxOffset, NumVertices);
                _vertices = vertices;
                _acVertices = acVectors;
            }
            else
            {
                _baseIndex = VertexOffset; // The offset into the vertex table of the start vertex
            }
            return 8;
        }

        private int Draw2Triangles(in byte[] data, int index)
        {
            var (coordsT, coordsS) = GetTextureCoordinates(data[1], data[2], data[3]);
            ModelRenderer.DrawSection(_gl, _vertices[_baseIndex + data[1]], _vertices[_baseIndex + data[2]],
                _vertices[_baseIndex + data[3]], coordsT, coordsS, _texture, _textureModes, _primitiveColor);

            (coordsT, coordsS) = GetTextureCoordinates(data[5], data[6], data[7]);
            ModelRenderer.DrawSection(_gl, _vertices[_baseIndex + data[5]], _vertices[_baseIndex + data[6]],
                _vertices[_baseIndex + data[7]], coordsT, coordsS, _texture, _textureModes, _primitiveColor);

            return 8;
        }

        private int SetTileDolphin(in byte[] data, int index)
        {
            var tileDescriptorIndex = data[index + 1] & 0x07;
            var modeT = (data[index + 2] >> 2) & 3; // Check these
            var modeS = data[index + 2] & 3;

            _textureModes.MirrorX = (modeT & 1) == 1;
            _textureModes.MirrorY = (modeS & 1) == 1;

            _textureModes.ClampX = (modeT & 2) == 1;
            _textureModes.ClampY = (modeS & 2) == 1;

            var instruction = BitConverter.ToInt64(data, index).Reverse();
            Debug.WriteLine($"Tile Descriptor: {tileDescriptorIndex} | Mode T: {GetTextureWrapMode(modeT)} | Mode S: {GetTextureWrapMode(modeS)} | Instruction: {instruction:X16}");
            return 8;
        }

        private int SetTextureInfo(byte[] Data, int Index)
        {
            int MaximumMipmapLevels = (Data[Index + 2] >> 3) & 0x07; // Excludes the actual texture
            int TileDescriptorNumber = Data[Index + 2] & 0x07;
            int Enabled = Data[Index + 3]; // "on" or "off"
            int XScaleFactor = (Data[Index + 4] << 8) | Data[Index + 5];
            int YScaleFactor = (Data[Index + 6] << 8) | Data[Index + 7];

            Debug.WriteLine(
                $"Texture: Mipmap Levels: {MaximumMipmapLevels} | Tile Descriptor: {TileDescriptorNumber} | Enabled: {Enabled} | Scale X: {XScaleFactor} | Scale Y: {YScaleFactor}");

            return 8;
        }

        private int SetGeometryMode(byte[] Data, int Index)
        {
            int ClearBits = ~((Data[Index + 1] << 16) | (Data[Index + 2] << 8) | Data[Index + 3]);
            int SetBits = (Data[Index + 4] << 24) | (Data[Index + 5] << 16) | (Data[Index + 6] << 8) | Data[Index + 7];

            return 8;
        }

        private int DrawList(in byte[] data, int index)
        {
            var processType = data[index + 1];
            var address = BitConverter.ToInt32(data, index + 4).Reverse();

            Debug.WriteLine($"Draw List: Type: {processType} | Draw List Address: {address:X8}");

            if ((address & 0x80000000) == 0) return 8;

            _endList = ParseModel(address & ~0x8000000);
            if (processType != 1)
            {
                _endList = false;
            }

            return 8;
        }

        private int EndDisplayList()
        {
            _endList = true;
            return 8;
        }

        private int SetOtherModeLow(byte[] Data, int Index)
        {
            int Length = Data[Index + 3] + 1;
            int Shift = 32 - Length - Data[Index + 2];
            int Bits = (Data[Index + 4] << 24) | (Data[Index + 5] << 16) | (Data[Index + 6] << 8) | Data[Index + 7];

            return 8;
        }

        private int LoadTextureLookUpTable(byte[] Data, int Index, BinaryReader Reader = null)
        {
            int Type = (Data[1] >> 6) & 3;
            int Slot = Data[1] & 0xF; // Unsure about if this is a "slot"
            int PaletteCount = BitConverter.ToInt16(Data, 2).Reverse() & 0x3FF;
            int PaletteAddress = BitConverter.ToInt32(Data, 4).Reverse();

            Debug.WriteLine(string.Format("Load Texture Lookup Table: | Type: {0} | Slot: {1} | Palette Count: {2} | Palette Address: {3}",
                Type, Slot, PaletteCount, PaletteAddress.ToString("X8")));

            if (Reader != null && (PaletteAddress & 0x80000000) != 0)
            {
                Reader.BaseStream.Seek(PaletteAddress & ~0x80000000, SeekOrigin.Begin);
                _palette = new List<ushort>();
                for (var i = 0; i < PaletteCount; i++)
                {
                    _palette.Add(Reader.ReadUInt16().Reverse());
                }
            }

            return 8;
        }

        private int SetFillColor(byte[] Data, int Index)
        {
            _fillColor = (Data[Index + 7] << 24) | (Data[Index + 4] << 16) | (Data[Index + 5] << 8) | Data[Index + 6];
            Debug.WriteLine("Set Fill Color to: 0x" + _fillColor.ToString("X8"));

            return 8;
        }

        private int SetFogColor(byte[] Data, int Index)
        {
            _fogColor = (Data[Index + 7] << 24) | (Data[Index + 4] << 16) | (Data[Index + 5] << 8) | Data[Index + 6];
            Debug.WriteLine("Set Fog Color to: 0x" + _fogColor.ToString("X8"));

            return 8;
        }

        private int SetBlendColor(byte[] Data, int Index)
        {
            _blendColor = (Data[Index + 7] << 24) | (Data[Index + 4] << 16) | (Data[Index + 5] << 8) | Data[Index + 6];
            Debug.WriteLine("Set Blend Color to: 0x" + _blendColor.ToString("X8"));

            return 8;
        }

        private int SetPrimativeColor(in byte[] data, int index)
        {
            int minimumLevelOfDetail = data[index + 2];
            int levelOfDetailFraction = data[index + 3];
            var primitiveColor = (data[index + 7] << 24) | (data[index + 4] << 16) | (data[index + 5] << 8) | data[index + 6]; // R->G->B->A

            _primitiveColor = Color.FromArgb(data[index + 7], data[index + 4], data[index + 5], data[index + 6]);
            Debug.WriteLine("Set Primitive Color to: 0x" + primitiveColor.ToString("X8"));

            return 8;
        }

        private int SetEnvironmentColor(in byte[] data, int index)
        {
            _environmentColor = (data[index + 7] << 24) | (data[index + 4] << 16) | (data[index + 5] << 8) | data[index + 6];
            Debug.WriteLine("Set Environment Color to: 0x" + _environmentColor.ToString("X8"));

            return 8;
        }

        private int SetColorCombinerMode(byte[] Data, int Index)
        {
            // a0, c0, Aa0, Ac0, a1, c1, b0, b1, Aa1, Ac1, d0, Ab0, Ad0, d1, Ab1, Ad1
            return 8;
        }

        private int SetTextureImage(in byte[] data, int index, BinaryReader reader = null)
        {
            var textureFormat = (data[index + 1] & 0xE0) >> 5;
            var bitsPerPixel = (data[index + 1] & 0x18) >> 3;
            var width = (((data[index + 2] << 8) | data[index + 3]) & 0x3FF) + 1;
            var height = (((BitConverter.ToInt32(data, 0).Reverse() >> 10) & 0xFF) + 1) * 4;
            var imageAddress = (data[index + 4] << 24) | (data[index + 5] << 16) | (data[index + 6] << 8) | data[index + 7];

            Debug.WriteLine(
                $"Set Texture #{_textureIndex} | Address: {imageAddress:X8} | Texture Format: {textureFormat} | Bits Per Pixel: {bitsPerPixel} | Width: {width} | Height: {height}");

            if (reader != null && (imageAddress & 0x80000000) != 0)
            {
                reader.BaseStream.Seek(imageAddress & ~0x80000000, SeekOrigin.Begin);
                var pixelData = C4.DecodeC4(reader.ReadBytes((width * height) / 2), _palette.ToArray(), width, height);
                var imgData = new byte[pixelData.Length * 4];
                Buffer.BlockCopy(pixelData, 0, imgData, 0, imgData.Length);

                // Set current texture
                _texture = new TextureAC
                {
                    Width = width,
                    Height = height,
                    TextureData = imgData
                };

                var img = Util.ToImage(imgData, width, height);
                using (var fStream = new FileStream(ImageOutputDirectory + "\\Image_" + imageAddress.ToString("X8") + ".png", FileMode.Create))
                {
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(img));
                    encoder.Save(fStream);
                }
            }

            _textureIndex++;

            return 8;
        }

        private int DrawTriangle(in byte[] modelData, int startPoint = 0, BinaryReader reader = null, bool firstPassDone = false, int initialFacesLeft = 0)
        {
            if (_vertices == null)
                return 0;

            if (startPoint >= modelData.Length)
                return 0;

            var faceCount = firstPassDone ? initialFacesLeft : (modelData[startPoint * 4 + 1] / 2) + 1; // Get the total number of faces in the model
            var facesLeft = faceCount;
            var endIndex = startPoint;

            var convertedData = new List<uint>();

            // Convert all data into uint types for handling
            for (var i = 0; i < modelData.Length; i += 4)
            {
                convertedData.Add((uint)((modelData[i] << 24) | (modelData[i + 1] << 16) | (modelData[i + 2] << 8) | modelData[i + 3]));
            }

            var data = convertedData.ToArray();

            for (var i = startPoint; i < data.Length; i += 2)
            {
                endIndex = i + 2;

                var currentFaceData = ((ulong)data[i] << 32) | data[i + 1]; // Combine the two sections into one 64 bit datatype

                GetFaceVertexSet(currentFaceData, 0, out var vIndex0, out var vIndex1, out var vIndex2);
                var (coordsT, coordsS) = GetTextureCoordinates(vIndex0, vIndex1, vIndex2);
                ModelRenderer.DrawSection(_gl, _vertices[_baseIndex + (int)vIndex0], _vertices[_baseIndex + (int)vIndex1],
                    _vertices[_baseIndex + (int)vIndex2], coordsT, coordsS, _texture, _textureModes, _primitiveColor);

                facesLeft--;
                if (facesLeft == 0) // Check to see if we're done with the faces
                    break;

                GetFaceVertexSet(currentFaceData, 1, out var vIndex3, out var vIndex4, out var vIndex5);
                (coordsT, coordsS) = GetTextureCoordinates(vIndex3, vIndex4, vIndex5);
                ModelRenderer.DrawSection(_gl, _vertices[_baseIndex + (int)vIndex3], _vertices[_baseIndex + (int)vIndex4],
                    _vertices[_baseIndex + (int)vIndex5], coordsT, coordsS, _texture, _textureModes, _primitiveColor);

                facesLeft--;
                if (facesLeft == 0)
                    break;

                GetFaceVertexSet(currentFaceData, 2, out var vIndex6, out var vIndex7, out var vIndex8);
                (coordsT, coordsS) = GetTextureCoordinates(vIndex6, vIndex7, vIndex8);
                ModelRenderer.DrawSection(_gl, _vertices[_baseIndex + (int)vIndex6], _vertices[_baseIndex + (int)vIndex7],
                    _vertices[_baseIndex + (int)vIndex8], coordsT, coordsS, _texture, _textureModes, _primitiveColor);

                facesLeft--;
                if (facesLeft == 0)
                    break;

                if (firstPassDone) // Only do this after the first 64 bit section (since the first byte is the section identifer (0x0A) and the second byte is the number of faces * 2 - 1)
                {
                    GetFaceVertexSet(currentFaceData, 3, out var vIndex9, out var vIndex10, out var vIndex11);
                    (coordsT, coordsS) = GetTextureCoordinates(vIndex9, vIndex10, vIndex11);
                    ModelRenderer.DrawSection(_gl, _vertices[_baseIndex + (int)vIndex9], _vertices[_baseIndex + (int)vIndex10],
                        _vertices[_baseIndex + (int)vIndex11], coordsT, coordsS, _texture, _textureModes, _primitiveColor);

                    facesLeft--;
                    if (facesLeft == 0)
                        break;
                }
                else
                {
                    firstPassDone = true;
                }

                if (reader != null && facesLeft > 0)
                {
                    return endIndex * 4 + DrawTriangle(reader.ReadBytes(8), 0, reader, true, facesLeft);
                }
            }

            return endIndex * 4;
        }

        private int DrawTriangleIndependent(in byte[] modelData, int startPoint = 0, BinaryReader reader = null)
        {
            return DrawTriangle(modelData, startPoint, reader);
        }

        // Non-Emulated code
        private void GetFaceVertexSet(ulong Data, int Index, out uint VertexA, out uint VertexB, out uint VertexC)
        {
            int BaseShiftCount = 4 + Index * 15;
            VertexA = (uint)(Data >> BaseShiftCount) & 0x1F;
            VertexB = (uint)(Data >> (BaseShiftCount + 5)) & 0x1F;
            VertexC = (uint)(Data >> (BaseShiftCount + 10)) & 0x1F;
        }

        private (short[], short[]) GetTextureCoordinates(uint vertexA, uint vertexB, uint vertexC)
            => (
                new[] { _acVertices[(int)vertexA].TextureXCoordinate, _acVertices[(int)vertexB].TextureXCoordinate, _acVertices[(int)vertexC].TextureXCoordinate },
                new[] { _acVertices[(int)vertexA].TextureYCoordinate, _acVertices[(int)vertexB].TextureYCoordinate, _acVertices[(int)vertexC].TextureYCoordinate });

        private string GetTextureWrapMode(int data)
        {
            switch (data)
            {
                case 0:
                    return "No Mirror & Wrap";
                case 1:
                    return "Mirror & Wrap";
                case 2:
                    return ("No Mirror & Clamp");
                case 3:
                    return ("Mirror & Clamp");
                default:
                    return ("Invalid setting!");
            }
        }

        public bool ParseModel(long modelAddress, OpenGL gl = null)
        {
            _endList = false;
            _reader.BaseStream.Seek(modelAddress, SeekOrigin.Begin);
            _gl = gl ?? _gl;

            while (!_endList)
            {
                var data = _reader.ReadBytes(8);
                var currentAddress = _reader.BaseStream.Position;
                var skipAmount = (uint)RunModelRoutine(data[0], 0, data) - 8;
                _reader.BaseStream.Seek(currentAddress + skipAmount, SeekOrigin.Begin);
            }

            return true;
        }
    }
}
