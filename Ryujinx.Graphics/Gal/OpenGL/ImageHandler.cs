﻿using Ryujinx.Graphics.Texture;

namespace Ryujinx.Graphics.Gal.OpenGL
{
    class ImageHandler : Resource
    {
        public GalImage Image { get; private set; }

        public int Width  => Image.Width;
        public int Height => Image.Height;

        public GalImageFormat Format => Image.Format;

        public int Handle { get; private set; }

        public bool HasColor   => ImageUtils.HasColor(Image.Format);
        public bool HasDepth   => ImageUtils.HasDepth(Image.Format);
        public bool HasStencil => ImageUtils.HasStencil(Image.Format);

        public ImageHandler(int Handle, GalImage Image)
        {
            this.Handle = Handle;
            this.Image  = Image;
        }
    }
}
