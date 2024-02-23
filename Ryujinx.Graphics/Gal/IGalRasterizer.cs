using System;

namespace Ryujinx.Graphics.Gal
{
    public interface IGalRasterizer
    {
        void LockCaches();
        void UnlockCaches();

        void ClearBuffers(
            GalClearBufferFlags Flags,
            int Attachment,
            float Red, float Green, float Blue, float Alpha,
            float Depth,
            int Stencil);

        bool IsVboCached(long Key, long DataSize);

        bool IsIboCached(long Key, long DataSize);

        void CreateVbo(long Key, int DataSize, IntPtr HostAddress);

        void CreateIbo(long Key, int DataSize, IntPtr HostAddress);
        void CreateIbo(long Key, int DataSize, byte[] Buffer);

        void SetIndexArray(int Size, GalIndexFormat Format);

        void DrawArrays(int First, int Count, GalPrimitiveType PrimType);

        void DrawElements(long IboKey, int First, int VertexBase, GalPrimitiveType PrimType);
    }
}