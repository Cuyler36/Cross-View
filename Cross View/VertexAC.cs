
using System.Windows.Media.Media3D;

namespace Cross_View
{
    // ReSharper disable once InconsistentNaming
    internal class VertexAC
    {
        public double Scale = 0.01; // Scales the model down for ToPoint3D (1 = normal)

        public short X;
        public short Y;
        public short Z;
        public short Reserved; // Unknown or Unused. Usually 0x0001.
        public short TextureXCoordinate;
        public short TextureYCoordinate;

        // Vertex Normals \\
        public byte NormalX;
        public byte NormalY;
        public byte NormalZ;

        // Vertex Transparency \\
        public byte Alpha;

        public VertexAC(in short[] data)
        {
            X = (short)-data[0];
            Y = data[2];
            Z = data[1];

            Reserved = data[3];

            TextureXCoordinate = data[4];
            TextureYCoordinate = data[5];

            NormalX = (byte)((data[6] & 0xFF00) >> 8);
            NormalY = (byte)(data[6] & 0x00FF);
            NormalZ = (byte)((data[7] & 0xFF00) >> 8);

            Alpha = (byte)(data[7] & 0x00FF);
        }

        public VertexAC(short x, short y, short z, short reserved, short textureXCoord, short textureYCoord,
            byte normalX, byte normalY, byte normalZ, byte alpha)
        {
            X = (short) -x;
            Y = y;
            Z = z;

            Reserved = reserved;

            TextureXCoordinate = textureXCoord;
            TextureYCoordinate = textureYCoord;

            NormalX = normalX;
            NormalY = normalY;
            NormalZ = normalZ;

            Alpha = alpha;
        }

        public Point3D ToPoint3D()
        {
            return new Point3D(X * Scale, Y * Scale, Z * Scale);
        }
    }
}
