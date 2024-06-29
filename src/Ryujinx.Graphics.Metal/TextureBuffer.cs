using Ryujinx.Graphics.GAL;
using SharpMetal.Metal;
using System;
using System.Buffers;
using System.Runtime.Versioning;

namespace Ryujinx.Graphics.Metal
{
    [SupportedOSPlatform("macos")]
    class TextureBuffer : TextureBase, ITexture
    {
        private MTLTextureDescriptor _descriptor;
        private BufferHandle _bufferHandle;
        private int _offset;
        private int _size;

        private int _bufferCount;

        public TextureBuffer(MTLDevice device, MetalRenderer renderer, Pipeline pipeline, TextureCreateInfo info) : base(device, renderer, pipeline, info)
        {
            MTLPixelFormat pixelFormat = FormatTable.GetFormat(Info.Format);

            _descriptor = new MTLTextureDescriptor
            {
                PixelFormat = pixelFormat,
                Usage = MTLTextureUsage.Unknown,
                TextureType = MTLTextureType.TextureBuffer,
                Width = (ulong)Info.Width,
                Height = (ulong)Info.Height,
            };

            MtlFormat = pixelFormat;
        }

        private void RebuildStorage()
        {
            // Find the parent buffer, and try to build a texture from it.

            // TODO: texture uses should register read/write usage on the assigned buffer.
            Auto<DisposableBuffer> bufferAuto = _renderer.BufferManager.GetBuffer(_bufferHandle, false);

            if (_mtlTexture.NativePtr != 0)
            {
                _mtlTexture.Dispose();
            }

            if (bufferAuto == null)
            {
                _mtlTexture = default;
            }
            else
            {
                DisposableBuffer buffer = bufferAuto.Get(_pipeline.Cbs, _offset, _size);

                _descriptor.Width = (uint)(_size / Info.BytesPerPixel);
                _mtlTexture = buffer.Value.NewTexture(_descriptor, (ulong)_offset, (ulong)_size);
            }
        }

        public void CopyTo(ITexture destination, int firstLayer, int firstLevel)
        {
            throw new NotSupportedException();
        }

        public void CopyTo(ITexture destination, int srcLayer, int dstLayer, int srcLevel, int dstLevel)
        {
            throw new NotSupportedException();
        }

        public void CopyTo(ITexture destination, Extents2D srcRegion, Extents2D dstRegion, bool linearFilter)
        {
            throw new NotSupportedException();
        }

        public ITexture CreateView(TextureCreateInfo info, int firstLayer, int firstLevel)
        {
            throw new NotSupportedException();
        }

        public PinnedSpan<byte> GetData()
        {
            return _renderer.GetBufferData(_bufferHandle, _offset, _size);
        }

        public PinnedSpan<byte> GetData(int layer, int level)
        {
            return GetData();
        }

        public void CopyTo(BufferRange range, int layer, int level, int stride)
        {
            throw new NotImplementedException();
        }

        public void SetData(IMemoryOwner<byte> data)
        {
            _renderer.SetBufferData(_bufferHandle, _offset, data.Memory.Span);
            data.Dispose();
        }

        public void SetData(IMemoryOwner<byte> data, int layer, int level)
        {
            throw new NotSupportedException();
        }

        public void SetData(IMemoryOwner<byte> data, int layer, int level, Rectangle<int> region)
        {
            throw new NotSupportedException();
        }

        public void SetStorage(BufferRange buffer)
        {
            if (_bufferHandle == buffer.Handle &&
                _offset == buffer.Offset &&
                _size == buffer.Size &&
                _bufferCount == _renderer.BufferManager.BufferCount)
            {
                return;
            }

            _bufferHandle = buffer.Handle;
            _offset = buffer.Offset;
            _size = buffer.Size;
            _bufferCount = _renderer.BufferManager.BufferCount;

            RebuildStorage();
        }

        public override void Release()
        {
            _descriptor.Dispose();

            base.Release();
        }
    }
}
