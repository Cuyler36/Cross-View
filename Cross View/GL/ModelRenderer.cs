using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using SharpGL;
using SharpGL.SceneGraph.Assets;

namespace Cross_View.GL
{
    internal static class ModelRenderer
    {
        internal static void DrawSection(OpenGL gl, in Point3D vertexA, in Point3D vertexB, in Point3D vertexC, in short[] texCoordT,
            in short[] texCoordS, in TextureAC texture, in TextureModes textureModes, in Color primitiveColor)
        {
            // Enable 2D textures
            gl.Enable(OpenGL.GL_TEXTURE_2D);

            // Set the current texture
            gl.TexImage2D(OpenGL.GL_TEXTURE_2D, 0, OpenGL.GL_RGBA8, texture.Width, texture.Height, 0, OpenGL.GL_RGBA8,
                OpenGL.GL_UNSIGNED_BYTE, texture.TextureData);

            // Set filter modes
            gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MIN_FILTER, OpenGL.GL_LINEAR);
            gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MAG_FILTER, OpenGL.GL_LINEAR);

            // Set the correct texture modes
            if (textureModes.MirrorX)
                for (var i = 0; i < 3; i++)
                    texCoordT[i] = (short) -texCoordT[i];
            if (textureModes.MirrorY)
                for (var i = 0; i < 3; i++)
                    texCoordS[i] = (short)-texCoordS[i];

            gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_WRAP_T, textureModes.ClampX ? OpenGL.GL_CLAMP : OpenGL.GL_REPEAT);
            gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_WRAP_S, textureModes.ClampY ? OpenGL.GL_CLAMP : OpenGL.GL_REPEAT);

            // Begin drawing the current section
            gl.Begin(OpenGL.GL_TRIANGLES);

            // Set the primitive color
            gl.Color(primitiveColor.R, primitiveColor.G, primitiveColor.B);

            // Set the vertices & texture coordinates
            gl.TexCoord(texCoordT[0], texCoordS[0]);
            gl.Vertex(vertexA.X, vertexA.Y, vertexA.Z);
            gl.TexCoord(texCoordT[1], texCoordS[1]);
            gl.Vertex(vertexB.X, vertexB.Y, vertexB.Z);
            gl.TexCoord(texCoordT[2], texCoordS[2]);
            gl.Vertex(vertexC.X, vertexC.Y, vertexC.Z);

            gl.End();

            gl.Disable(OpenGL.GL_TEXTURE_2D);
        }
    }
}
