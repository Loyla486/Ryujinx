using System;
using System.Collections.Concurrent;

namespace Ryujinx.Graphics.Gal.OpenGL
{
    public class OpenGLRenderer : IGalRenderer
    {
        public IGalBlend Blend { get; private set; }

        public IGalFrameBuffer FrameBuffer { get; private set; }

        public IGalRasterizer Rasterizer { get; private set; }

        public IGalShader Shader { get; private set; }

        public IGalTexture Texture { get; private set; }

        private ConcurrentQueue<Action> ActionsQueue;

        public OpenGLRenderer()
        {
            Blend = new OGLBlend();

            FrameBuffer = new OGLFrameBuffer();

            Rasterizer = new OGLRasterizer();

            Shader = new OGLShader();

            Texture = new OGLTexture();

            ActionsQueue = new ConcurrentQueue<Action>();
        }

        public void QueueAction(Action ActionMthd)
        {
            ActionsQueue.Enqueue(ActionMthd);
        }

        public void RunActions()
        {
            int Count = ActionsQueue.Count;

            while (Count-- > 0 && ActionsQueue.TryDequeue(out Action RenderAction))
            {
                RenderAction();
            }
        }
    }
}