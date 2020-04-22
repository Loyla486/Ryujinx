using Ryujinx.Graphics.Gpu.State;
using Ryujinx.Graphics.Gpu.Image;

namespace Ryujinx.Graphics.Gpu.Engine
{
    partial class Methods
    {
        private bool _drawIndexed;

        private int _firstIndex;
        private int _indexCount;

        private bool _instancedDrawPending;
        private bool _instancedIndexed;

        private int _instancedFirstIndex;
        private int _instancedFirstVertex;
        private int _instancedFirstInstance;
        private int _instancedIndexCount;
        private int _instancedDrawStateFirst;
        private int _instancedDrawStateCount;

        private int _instanceIndex;

        /// <summary>
        /// Primitive type of the current draw.
        /// </summary>
        public PrimitiveType PrimitiveType { get; private set; }

        /// <summary>
        /// Finishes draw call.
        /// This draws geometry on the bound buffers based on the current GPU state.
        /// </summary>
        /// <param name="state">Current GPU state</param>
        /// <param name="argument">Method call argument</param>
        private void DrawEnd(GpuState state, int argument)
        {
            bool renderEnable = GetRenderEnable(state);

            if (!renderEnable || _instancedDrawPending)
            {
                if (!renderEnable)
                {
                    PerformDeferredDraws();
                }

                _drawIndexed = false;

                return;
            }

            UpdateState(state);

            bool instanced = _vsUsesInstanceId || _isAnyVbInstanced;

            if (instanced)
            {
                _instancedDrawPending = true;

                _instancedIndexed = _drawIndexed;

                _instancedFirstIndex    = _firstIndex;
                _instancedFirstVertex   = state.Get<int>(MethodOffset.FirstVertex);
                _instancedFirstInstance = state.Get<int>(MethodOffset.FirstInstance);

                _instancedIndexCount = _indexCount;

                var drawState = state.Get<VertexBufferDrawState>(MethodOffset.VertexBufferDrawState);

                _instancedDrawStateFirst = drawState.First;
                _instancedDrawStateCount = drawState.Count;

                _drawIndexed = false;

                return;
            }

            int firstInstance = state.Get<int>(MethodOffset.FirstInstance);

            if (_drawIndexed)
            {
                _drawIndexed = false;

                int firstVertex = state.Get<int>(MethodOffset.FirstVertex);

                _context.Renderer.Pipeline.DrawIndexed(
                    _indexCount,
                    1,
                    _firstIndex,
                    firstVertex,
                    firstInstance);
            }
            else
            {
                var drawState = state.Get<VertexBufferDrawState>(MethodOffset.VertexBufferDrawState);

                _context.Renderer.Pipeline.Draw(
                    drawState.Count,
                    1,
                    drawState.First,
                    firstInstance);
            }
        }

        /// <summary>
        /// Starts draw.
        /// This sets primitive type and instanced draw parameters.
        /// </summary>
        /// <param name="state">Current GPU state</param>
        /// <param name="argument">Method call argument</param>
        private void DrawBegin(GpuState state, int argument)
        {
            if ((argument & (1 << 26)) != 0)
            {
                _instanceIndex++;
            }
            else if ((argument & (1 << 27)) == 0)
            {
                PerformDeferredDraws();

                _instanceIndex = 0;
            }

            PrimitiveType type = (PrimitiveType)(argument & 0xffff);

            _context.Renderer.Pipeline.SetPrimitiveTopology(type.Convert());

            PrimitiveType = type;
        }

        /// <summary>
        /// Sets the index buffer count.
        /// This also sets internal state that indicates that the next draw is an indexed draw.
        /// </summary>
        /// <param name="state">Current GPU state</param>
        /// <param name="argument">Method call argument</param>
        private void SetIndexBufferCount(GpuState state, int argument)
        {
            _drawIndexed = true;
        }

        /// <summary>
        /// Perform any deferred draws.
        /// This is used for instanced draws.
        /// Since each instance is a separate draw, we defer the draw and accumulate the instance count.
        /// Once we detect the last instanced draw, then we perform the host instanced draw,
        /// with the accumulated instance count.
        /// </summary>
        public void PerformDeferredDraws()
        {
            // Perform any pending instanced draw.
            if (_instancedDrawPending)
            {
                _instancedDrawPending = false;

                if (_instancedIndexed)
                {
                    _context.Renderer.Pipeline.DrawIndexed(
                        _instancedIndexCount,
                        _instanceIndex + 1,
                        _instancedFirstIndex,
                        _instancedFirstVertex,
                        _instancedFirstInstance);
                }
                else
                {
                    _context.Renderer.Pipeline.Draw(
                        _instancedDrawStateCount,
                        _instanceIndex + 1,
                        _instancedDrawStateFirst,
                        _instancedFirstInstance);
                }
            }
        }
    }
}