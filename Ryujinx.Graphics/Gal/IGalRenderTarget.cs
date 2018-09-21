namespace Ryujinx.Graphics.Gal
{
    public interface IGalRenderTarget
    {
        void Bind();

        void BindColor(long Key, int Attachment);

        void UnbindColor(int Attachment);

        void BindZeta(long Key);

        void UnbindZeta();

        void Present(long Key);

        void SetMap(int[] Map);

        void SetTransform(bool FlipX, bool FlipY, int Top, int Left, int Right, int Bottom);

        void SetWindowSize(int Width, int Height);

        void SetViewport(int Attachment, int X, int Y, int Width, int Height);

        void Render();

        void Copy(
            long SrcKey,
            long DstKey,
            int  SrcX0,
            int  SrcY0,
            int  SrcX1,
            int  SrcY1,
            int  DstX0,
            int  DstY0,
            int  DstX1,
            int  DstY1);

        void Reinterpret(long Key, GalImage NewImage);
    }
}