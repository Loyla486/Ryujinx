using OpenTK.Graphics.OpenGL;

namespace Ryujinx.Graphics.Gal.OpenGL
{
    class OGLTexture
    {
        private int[] Textures;

        public OGLTexture()
        {
            Textures = new int[80];
        }

        public void Set(int Index, GalTexture Texture)
        {
            GL.ActiveTexture(TextureUnit.Texture0 + Index);

            Bind(Index);

            const int Border = 0;

            if (IsCompressedTextureFormat(Texture.Format))
            {
                PixelInternalFormat InternalFmt = OGLEnumConverter.GetCompressedTextureFormat(Texture.Format);

                GL.CompressedTexImage2D(
                    TextureTarget.Texture2D,
                    0,
                    InternalFmt,
                    Texture.Width,
                    Texture.Height,
                    Border,
                    Texture.Data.Length,
                    Texture.Data);
            }
            else
            {
                const PixelInternalFormat InternalFmt = PixelInternalFormat.Rgba;

                (PixelFormat, PixelType) Format = OGLEnumConverter.GetTextureFormat(Texture.Format);

                GL.TexImage2D(
                    TextureTarget.Texture2D,
                    0,
                    InternalFmt,
                    Texture.Width,
                    Texture.Height,
                    Border,
                    Format.Item1,
                    Format.Item2,
                    Texture.Data);
            }
        }

        public void Bind(int Index)
        {
            int Handle = EnsureTextureInitialized(Index);

            GL.BindTexture(TextureTarget.Texture2D, Handle);
        }

        public void Set(GalTextureSampler Sampler)
        {
            int WrapS = (int)OGLEnumConverter.GetTextureWrapMode(Sampler.AddressU);
            int WrapT = (int)OGLEnumConverter.GetTextureWrapMode(Sampler.AddressV);

            int MinFilter = (int)OGLEnumConverter.GetTextureMinFilter(Sampler.MinFilter, Sampler.MipFilter);
            int MagFilter = (int)OGLEnumConverter.GetTextureMagFilter(Sampler.MagFilter);

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, WrapS);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, WrapT);

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, MinFilter);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, MagFilter);

            float[] Color = new float[]
            {
                Sampler.BorderColor.Red,
                Sampler.BorderColor.Green,
                Sampler.BorderColor.Blue,
                Sampler.BorderColor.Alpha
            };

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBorderColor, Color);
        }

        private static bool IsCompressedTextureFormat(GalTextureFormat Format)
        {
            switch (Format)
            {
                case GalTextureFormat.BC1:
                case GalTextureFormat.BC2:
                case GalTextureFormat.BC3:
                case GalTextureFormat.BC4:
                case GalTextureFormat.BC5:
                    return true;
            }

            return false;
        }

        private int EnsureTextureInitialized(int TexIndex)
        {
            int Handle = Textures[TexIndex];

            if (Handle == 0)
            {
                Handle = Textures[TexIndex] = GL.GenTexture();
            }

            return Handle;
        }
    }
}