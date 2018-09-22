using SharpGL.SceneGraph.Assets;
using SharpGL.Textures;

namespace Cross_View
{
    internal enum BitsPerPixel
    {
        Size4bpp = 0,
        Size8bpp = 1,
        Size16bpp = 2,
        Size32bpp = 3
    }

    internal enum TextureFormat
    {
        RGBA = 0,
        YUV = 1,
        CI = 2,
        IA = 3,
        I = 4
    }

    internal struct TextureAC
    {
        public int Width;
        public int Height;
        public byte[] TextureData;

        public byte UpperLeftS;
        public byte UpperLeftT;
        public byte LowerRightS;
        public byte LowerRightT;

        public byte MaskS;
        public byte MaskT;

        public byte LineSize;

        public byte WrapModeS;
        public byte WrapModeT;

        public TextureFormat Format;
        public BitsPerPixel Bpp;

        public Texture2D Texture;
    }
}
