using Ryujinx.Graphics.Shader;
using System;

namespace Ryujinx.Graphics.GAL
{
    public interface IPipeline
    {
        void Barrier();

        void ClearRenderTargetColor(int index, uint componentMask, ColorF color);

        void ClearRenderTargetDepthStencil(
            float depthValue,
            bool  depthMask,
            int   stencilValue,
            int   stencilMask);

        void CopyBuffer(BufferHandle source, BufferHandle destination, int srcOffset, int dstOffset, int size);

        void DispatchCompute(int groupsX, int groupsY, int groupsZ);

        void Draw(int vertexCount, int instanceCount, int firstVertex, int firstInstance);
        void DrawIndexed(
            int indexCount,
            int instanceCount,
            int firstIndex,
            int firstVertex,
            int firstInstance);

        void SetBlendState(int index, BlendDescriptor blend);

        void SetDepthBias(PolygonModeMask enables, float factor, float units, float clamp);
        void SetDepthClamp(bool clamp);
        void SetDepthMode(DepthMode mode);
        void SetDepthTest(DepthTestDescriptor depthTest);

        void SetFaceCulling(bool enable, Face face);

        void SetFrontFace(FrontFace frontFace);

        void SetIndexBuffer(BufferRange buffer, IndexType type);

        void SetImage(int index, ShaderStage stage, ITexture texture);

        void SetOrigin(Origin origin);

        void SetPointSize(float size);

        void SetPrimitiveRestart(bool enable, int index);

        void SetPrimitiveTopology(PrimitiveTopology topology);

        void SetProgram(IProgram program);

        void SetRasterizerDiscard(bool discard);

        void SetRenderTargetColorMasks(ReadOnlySpan<uint> componentMask);

        void SetRenderTargets(ITexture[] colors, ITexture depthStencil);

        void SetSampler(int index, ShaderStage stage, ISampler sampler);

        void SetScissorEnable(int index, bool enable);
        void SetScissor(int index, int x, int y, int width, int height);

        void SetStencilTest(StencilTestDescriptor stencilTest);

        void SetStorageBuffer(int index, ShaderStage stage, BufferRange buffer);

        void SetTexture(int index, ShaderStage stage, ITexture texture);

        void SetUniformBuffer(int index, ShaderStage stage, BufferRange buffer);

        void SetUserClipDistance(int index, bool enableClip);

        void SetVertexAttribs(ReadOnlySpan<VertexAttribDescriptor> vertexAttribs);
        void SetVertexBuffers(ReadOnlySpan<VertexBufferDescriptor> vertexBuffers);

        void SetViewports(int first, ReadOnlySpan<Viewport> viewports);

        void TextureBarrier();
        void TextureBarrierTiled();

        bool TryHostConditionalRendering(ICounterEvent value, ulong compare, bool isEqual);
        bool TryHostConditionalRendering(ICounterEvent value, ICounterEvent compare, bool isEqual);
        void EndHostConditionalRendering();
    }
}
