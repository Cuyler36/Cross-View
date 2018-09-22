using System;
using System.IO;
using System.Windows.Media.Media3D;

namespace Cross_View.Parser
{
    internal partial class F3DACEXParser
    {
        private (Point3D[], VertexAC[]) LoadVertices(BinaryReader reader, long vtxOffset, int count)
        {
            var vectors = new VertexAC[count];
            var points = new Point3D[count];

            // Seek to the vertex begin offset.
            reader.BaseStream.Seek(vtxOffset, SeekOrigin.Begin);

            // Read vectors from file.
            for (var i = 0; i < count; i++)
            {
                vectors[i] = new VertexAC(reader.ReadInt16().Reverse(), reader.ReadInt16().Reverse(),
                    reader.ReadInt16().Reverse(), reader.ReadInt16().Reverse(), reader.ReadInt16().Reverse(),
                    reader.ReadInt16().Reverse(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte(),
                    reader.ReadByte());

                points[i] = vectors[i].ToPoint3D();
            }

            return (points, vectors);
        }
    }
}
