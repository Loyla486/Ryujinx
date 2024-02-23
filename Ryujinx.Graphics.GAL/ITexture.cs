using System;

namespace Ryujinx.Graphics.GAL
{
    public interface ITexture : IDisposable
    {
        int Width { get; }
        int Height { get; }
        float ScaleFactor { get; }

        void CopyTo(ITexture destination, int firstLayer, int firstLevel);
        void CopyTo(ITexture destination, Extents2D srcRegion, Extents2D dstRegion, bool linearFilter);

        ITexture CreateView(TextureCreateInfo info, int firstLayer, int firstLevel);

        byte[] GetData();

        void SetData(ReadOnlySpan<byte> data);
        void SetStorage(BufferRange buffer);
    }
}