using OpenTK.Graphics.OpenGL;
using Ryujinx.Graphics.GAL;
using System;

namespace Ryujinx.Graphics.OpenGL.Image
{
    class TextureView : TextureBase, ITexture, ITextureInfo
    {
        private readonly Renderer _renderer;

        private readonly TextureStorage _parent;

        private TextureView _incompatibleFormatView;

        public ITextureInfo Storage => _parent;

        public int FirstLayer { get; private set; }
        public int FirstLevel { get; private set; }

        public TextureView(
            Renderer          renderer,
            TextureStorage    parent,
            TextureCreateInfo info,
            int               firstLayer,
            int               firstLevel) : base(info, parent.ScaleFactor)
        {
            _renderer = renderer;
            _parent   = parent;

            FirstLayer = firstLayer;
            FirstLevel = firstLevel;

            CreateView();
        }

        private void CreateView()
        {
            TextureTarget target = Target.Convert();

            FormatInfo format = FormatTable.GetFormatInfo(Info.Format);

            PixelInternalFormat pixelInternalFormat;

            if (format.IsCompressed)
            {
                pixelInternalFormat = (PixelInternalFormat)format.PixelFormat;
            }
            else
            {
                pixelInternalFormat = format.PixelInternalFormat;
            }

            GL.TextureView(
                Handle,
                target,
                _parent.Handle,
                pixelInternalFormat,
                FirstLevel,
                Info.Levels,
                FirstLayer,
                Info.GetLayers());

            GL.ActiveTexture(TextureUnit.Texture0);

            GL.BindTexture(target, Handle);

            int[] swizzleRgba = new int[]
            {
                (int)Info.SwizzleR.Convert(),
                (int)Info.SwizzleG.Convert(),
                (int)Info.SwizzleB.Convert(),
                (int)Info.SwizzleA.Convert()
            };

            if (Info.Format.IsBgra8())
            {
                // Swap B <-> R for BGRA formats, as OpenGL has no support for them
                // and we need to manually swap the components on read/write on the GPU.
                int temp = swizzleRgba[0];
                swizzleRgba[0] = swizzleRgba[2];
                swizzleRgba[2] = temp;
            }

            GL.TexParameter(target, TextureParameterName.TextureSwizzleRgba, swizzleRgba);

            int maxLevel = Info.Levels - 1;

            if (maxLevel < 0)
            {
                maxLevel = 0;
            }

            GL.TexParameter(target, TextureParameterName.TextureMaxLevel, maxLevel);
            GL.TexParameter(target, TextureParameterName.DepthStencilTextureMode, (int)Info.DepthStencilMode.Convert());
        }

        public ITexture CreateView(TextureCreateInfo info, int firstLayer, int firstLevel)
        {
            firstLayer += FirstLayer;
            firstLevel += FirstLevel;

            return _parent.CreateView(info, firstLayer, firstLevel);
        }

        public int GetIncompatibleFormatViewHandle()
        {
            // AMD and Intel have a bug where the view format is always ignored;
            // they use the parent format instead.
            // As a workaround we create a new texture with the correct
            // format, and then do a copy after the draw.
            if (_parent.Info.Format != Format)
            {
                if (_incompatibleFormatView == null)
                {
                    _incompatibleFormatView = (TextureView)_renderer.CreateTexture(Info, ScaleFactor);
                }

                _renderer.TextureCopy.CopyUnscaled(_parent, _incompatibleFormatView, FirstLayer, 0, FirstLevel, 0);

                return _incompatibleFormatView.Handle;
            }

            return Handle;
        }

        public void SignalModified()
        {
            if (_incompatibleFormatView != null)
            {
                _renderer.TextureCopy.CopyUnscaled(_incompatibleFormatView, _parent, 0, FirstLayer, 0, FirstLevel);
            }
        }

        public void CopyTo(ITexture destination, int firstLayer, int firstLevel)
        {
            TextureView destinationView = (TextureView)destination;

            _renderer.TextureCopy.CopyUnscaled(this, destinationView, 0, firstLayer, 0, firstLevel);
        }

        public void CopyTo(ITexture destination, int srcLayer, int dstLayer, int srcLevel, int dstLevel)
        {
             TextureView destinationView = (TextureView)destination;

            _renderer.TextureCopy.CopyUnscaled(this, destinationView, srcLayer, dstLayer, srcLevel, dstLevel, 1, 1);
        }

        public void CopyTo(ITexture destination, Extents2D srcRegion, Extents2D dstRegion, bool linearFilter)
        {
            _renderer.TextureCopy.Copy(this, (TextureView)destination, srcRegion, dstRegion, linearFilter);
        }

        public byte[] GetData()
        {
            int size = 0;

            for (int level = 0; level < Info.Levels; level++)
            {
                size += Info.GetMipSize(level);
            }

            byte[] data = new byte[size];

            unsafe
            {
                fixed (byte* ptr = data)
                {
                    WriteTo((IntPtr)ptr);
                }
            }

            return data;
        }

        public void WriteToPbo(int offset, bool forceBgra)
        {
            WriteTo(IntPtr.Zero + offset, forceBgra);
        }

        public int WriteToPbo2D(int offset, int layer, int level)
        {
            return WriteTo2D(IntPtr.Zero + offset, layer, level);
        }

        private int WriteTo2D(IntPtr data, int layer, int level)
        {
            TextureTarget target = Target.Convert();

            Bind(target, 0);

            FormatInfo format = FormatTable.GetFormatInfo(Info.Format);

            PixelFormat pixelFormat = format.PixelFormat;
            PixelType pixelType = format.PixelType;

            if (target == TextureTarget.TextureCubeMap || target == TextureTarget.TextureCubeMapArray)
            {
                target = TextureTarget.TextureCubeMapPositiveX + (layer % 6);
            }

            int mipSize = Info.GetMipSize2D(level);

            // The GL function returns all layers. Must return the offset of the layer we're interested in.
            int resultOffset = target switch
            {
                TextureTarget.TextureCubeMapArray => (layer / 6) * mipSize,
                TextureTarget.Texture1DArray => layer * mipSize,
                TextureTarget.Texture2DArray => layer * mipSize,
                _ => 0
            };

            if (format.IsCompressed)
            {
                GL.GetCompressedTexImage(target, level, data);
            }
            else
            {
                GL.GetTexImage(target, level, pixelFormat, pixelType, data);
            }

            return resultOffset;
        }

        private void WriteTo(IntPtr data, bool forceBgra = false)
        {
            TextureTarget target = Target.Convert();

            Bind(target, 0);

            FormatInfo format = FormatTable.GetFormatInfo(Info.Format);

            PixelFormat pixelFormat = format.PixelFormat;
            PixelType   pixelType   = format.PixelType;

            if (forceBgra)
            {
                pixelFormat = PixelFormat.Bgra;
            }

            int faces = 1;

            if (target == TextureTarget.TextureCubeMap)
            {
                target = TextureTarget.TextureCubeMapPositiveX;

                faces = 6;
            }

            for (int level = 0; level < Info.Levels; level++)
            {
                for (int face = 0; face < faces; face++)
                {
                    int faceOffset = face * Info.GetMipSize2D(level);

                    if (format.IsCompressed)
                    {
                        GL.GetCompressedTexImage(target + face, level, data + faceOffset);
                    }
                    else
                    {
                        GL.GetTexImage(target + face, level, pixelFormat, pixelType, data + faceOffset);
                    }
                }

                data += Info.GetMipSize(level);
            }
        }

        public void SetData(ReadOnlySpan<byte> data)
        {
            unsafe
            {
                fixed (byte* ptr = data)
                {
                    ReadFrom((IntPtr)ptr, data.Length);
                }
            }
        }

        public void SetData(ReadOnlySpan<byte> data, int layer, int level)
        {
            unsafe
            {
                fixed (byte* ptr = data)
                {
                    int width = Math.Max(Info.Width >> level, 1);
                    int height = Math.Max(Info.Height >> level, 1);

                    ReadFrom2D((IntPtr)ptr, layer, level, width, height);
                }
            }
        }

        public void ReadFromPbo(int offset, int size)
        {
            ReadFrom(IntPtr.Zero + offset, size);
        }

        public void ReadFromPbo2D(int offset, int layer, int level, int width, int height)
        {
            ReadFrom2D(IntPtr.Zero + offset, layer, level, width, height);
        }

        private void ReadFrom2D(IntPtr data, int layer, int level, int width, int height)
        {
            TextureTarget target = Target.Convert();

            int mipSize = Info.GetMipSize2D(level);

            Bind(target, 0);

            FormatInfo format = FormatTable.GetFormatInfo(Info.Format);

            switch (Target)
            {
                case Target.Texture1D:
                    if (format.IsCompressed)
                    {
                        GL.CompressedTexSubImage1D(
                            target,
                            level,
                            0,
                            width,
                            format.PixelFormat,
                            mipSize,
                            data);
                    }
                    else
                    {
                        GL.TexSubImage1D(
                            target,
                            level,
                            0,
                            width,
                            format.PixelFormat,
                            format.PixelType,
                            data);
                    }
                    break;

                case Target.Texture1DArray:
                    if (format.IsCompressed)
                    {
                        GL.CompressedTexSubImage2D(
                            target,
                            level,
                            0,
                            layer,
                            width,
                            1,
                            format.PixelFormat,
                            mipSize,
                            data);
                    }
                    else
                    {
                        GL.TexSubImage2D(
                            target,
                            level,
                            0,
                            layer,
                            width,
                            1,
                            format.PixelFormat,
                            format.PixelType,
                            data);
                    }
                    break;

                case Target.Texture2D:
                    if (format.IsCompressed)
                    {
                        GL.CompressedTexSubImage2D(
                            target,
                            level,
                            0,
                            0,
                            width,
                            height,
                            format.PixelFormat,
                            mipSize,
                            data);
                    }
                    else
                    {
                        GL.TexSubImage2D(
                            target,
                            level,
                            0,
                            0,
                            width,
                            height,
                            format.PixelFormat,
                            format.PixelType,
                            data);
                    }
                    break;

                case Target.Texture2DArray:
                case Target.Texture3D:
                case Target.CubemapArray:
                    if (format.IsCompressed)
                    {
                        GL.CompressedTexSubImage3D(
                            target,
                            level,
                            0,
                            0,
                            layer,
                            width,
                            height,
                            1,
                            format.PixelFormat,
                            mipSize,
                            data);
                    }
                    else
                    {
                        GL.TexSubImage3D(
                            target,
                            level,
                            0,
                            0,
                            layer,
                            width,
                            height,
                            1,
                            format.PixelFormat,
                            format.PixelType,
                            data);
                    }
                    break;

                case Target.Cubemap:
                    if (format.IsCompressed)
                    {
                        GL.CompressedTexSubImage2D(
                            TextureTarget.TextureCubeMapPositiveX + layer,
                            level,
                            0,
                            0,
                            width,
                            height,
                            format.PixelFormat,
                            mipSize,
                            data);
                    }
                    else
                    {
                        GL.TexSubImage2D(
                            TextureTarget.TextureCubeMapPositiveX + layer,
                            level,
                            0,
                            0,
                            width,
                            height,
                            format.PixelFormat,
                            format.PixelType,
                            data);
                    }
                    break;
            }
        }

        private void ReadFrom(IntPtr data, int size)
        {
            TextureTarget target = Target.Convert();

            Bind(target, 0);

            FormatInfo format = FormatTable.GetFormatInfo(Info.Format);

            int width  = Info.Width;
            int height = Info.Height;
            int depth  = Info.Depth;

            int offset = 0;

            for (int level = 0; level < Info.Levels; level++)
            {
                int mipSize = Info.GetMipSize(level);

                int endOffset = offset + mipSize;

                if ((uint)endOffset > (uint)size)
                {
                    return;
                }

                switch (Info.Target)
                {
                    case Target.Texture1D:
                        if (format.IsCompressed)
                        {
                            GL.CompressedTexSubImage1D(
                                target,
                                level,
                                0,
                                width,
                                format.PixelFormat,
                                mipSize,
                                data);
                        }
                        else
                        {
                            GL.TexSubImage1D(
                                target,
                                level,
                                0,
                                width,
                                format.PixelFormat,
                                format.PixelType,
                                data);
                        }
                        break;

                    case Target.Texture1DArray:
                    case Target.Texture2D:
                        if (format.IsCompressed)
                        {
                            GL.CompressedTexSubImage2D(
                                target,
                                level,
                                0,
                                0,
                                width,
                                height,
                                format.PixelFormat,
                                mipSize,
                                data);
                        }
                        else
                        {
                            GL.TexSubImage2D(
                                target,
                                level,
                                0,
                                0,
                                width,
                                height,
                                format.PixelFormat,
                                format.PixelType,
                                data);
                        }
                        break;

                    case Target.Texture2DArray:
                    case Target.Texture3D:
                    case Target.CubemapArray:
                        if (format.IsCompressed)
                        {
                            GL.CompressedTexSubImage3D(
                                target,
                                level,
                                0,
                                0,
                                0,
                                width,
                                height,
                                depth,
                                format.PixelFormat,
                                mipSize,
                                data);
                        }
                        else
                        {
                            GL.TexSubImage3D(
                                target,
                                level,
                                0,
                                0,
                                0,
                                width,
                                height,
                                depth,
                                format.PixelFormat,
                                format.PixelType,
                                data);
                        }
                        break;

                    case Target.Cubemap:
                        int faceOffset = 0;

                        for (int face = 0; face < 6; face++, faceOffset += mipSize / 6)
                        {
                            if (format.IsCompressed)
                            {
                                GL.CompressedTexSubImage2D(
                                    TextureTarget.TextureCubeMapPositiveX + face,
                                    level,
                                    0,
                                    0,
                                    width,
                                    height,
                                    format.PixelFormat,
                                    mipSize / 6,
                                    data + faceOffset);
                            }
                            else
                            {
                                GL.TexSubImage2D(
                                    TextureTarget.TextureCubeMapPositiveX + face,
                                    level,
                                    0,
                                    0,
                                    width,
                                    height,
                                    format.PixelFormat,
                                    format.PixelType,
                                    data + faceOffset);
                            }
                        }
                        break;
                }

                data   += mipSize;
                offset += mipSize;

                width  = Math.Max(1, width  >> 1);
                height = Math.Max(1, height >> 1);

                if (Target == Target.Texture3D)
                {
                    depth = Math.Max(1, depth >> 1);
                }
            }
        }

        public void SetStorage(BufferRange buffer)
        {
            throw new NotSupportedException();
        }

        private void DisposeHandles()
        {
            if (_incompatibleFormatView != null)
            {
                _incompatibleFormatView.Dispose();

                _incompatibleFormatView = null;
            }

            if (Handle != 0)
            {
                GL.DeleteTexture(Handle);

                Handle = 0;
            }
        }

        /// <summary>
        /// Release the view without necessarily disposing the parent if we are the default view.
        /// This allows it to be added to the resource pool and reused later.
        /// </summary>
        public void Release()
        {
            bool hadHandle = Handle != 0;

            if (_parent.DefaultView != this)
            {
                DisposeHandles();
            }

            if (hadHandle)
            {
                _parent.DecrementViewsCount();
            }
        }

        public void Dispose()
        {
            if (_parent.DefaultView == this)
            {
                // Remove the default view (us), so that the texture cannot be released to the cache.
                _parent.DeleteDefault();
            }

            Release();
        }
    }
}
