using System;

namespace Ryujinx.Graphics.Gal
{
    public struct GalVertexAttrib
    {
        public int    Index   { get; private set; }
        public bool   IsConst { get; private set; }
        public int    Offset  { get; private set; }
        public IntPtr Pointer { get; private set; }

        public GalVertexAttribSize Size { get; private set; }
        public GalVertexAttribType Type { get; private set; }

        public bool IsBgra { get; private set; }

        public GalVertexAttrib(
            int                 Index,
            bool                IsConst,
            int                 Offset,
            IntPtr              Pointer,
            GalVertexAttribSize Size,
            GalVertexAttribType Type,
            bool                IsBgra)
        {
            this.Index   = Index;
            this.IsConst = IsConst;
            this.Pointer = Pointer;
            this.Offset  = Offset;
            this.Size    = Size;
            this.Type    = Type;
            this.IsBgra  = IsBgra;
        }
    }
}