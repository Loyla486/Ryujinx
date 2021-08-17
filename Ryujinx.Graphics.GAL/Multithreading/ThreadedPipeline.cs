﻿using Ryujinx.Graphics.GAL.Multithreading.Commands;
using Ryujinx.Graphics.GAL.Multithreading.Model;
using Ryujinx.Graphics.GAL.Multithreading.Resources;
using Ryujinx.Graphics.Shader;
using System;
using System.Linq;

namespace Ryujinx.Graphics.GAL.Multithreading
{
    public class ThreadedPipeline : IPipeline
    {
        private ThreadedRenderer _renderer;
        private IPipeline _impl;

        private SetGenericBuffersDelegate _setStorageBuffers;
        private SetGenericBuffersDelegate _setTransformFeedbackBuffers;
        private SetGenericBuffersDelegate _setUniformBuffers;

        public ThreadedPipeline(ThreadedRenderer renderer, IPipeline impl)
        {
            _renderer = renderer;
            _impl = impl;

            _setStorageBuffers = impl.SetStorageBuffers;
            _setTransformFeedbackBuffers = impl.SetTransformFeedbackBuffers;
            _setUniformBuffers = impl.SetUniformBuffers;
        }

        private TableRef<T> Ref<T>(T reference)
        {
            return new TableRef<T>(_renderer, reference);
        }

        public void Barrier()
        {
            _renderer.New<BarrierCommand>();
            _renderer.QueueCommand();
        }

        public void BeginTransformFeedback(PrimitiveTopology topology)
        {
            _renderer.New<BeginTransformFeedbackCommand>().Set(topology);
            _renderer.QueueCommand();
        }

        public void ClearBuffer(BufferHandle destination, int offset, int size, uint value)
        {
            _renderer.New<ClearBufferCommand>().Set(destination, offset, size, value);
            _renderer.QueueCommand();
        }

        public void ClearRenderTargetColor(int index, uint componentMask, ColorF color)
        {
            _renderer.New<ClearRenderTargetColorCommand>().Set(index, componentMask, color);
            _renderer.QueueCommand();
        }

        public void ClearRenderTargetDepthStencil(float depthValue, bool depthMask, int stencilValue, int stencilMask)
        {
            _renderer.New<ClearRenderTargetDepthStencilCommand>().Set(depthValue, depthMask, stencilValue, stencilMask);
            _renderer.QueueCommand();
        }

        public void CopyBuffer(BufferHandle source, BufferHandle destination, int srcOffset, int dstOffset, int size)
        {
            _renderer.New<CopyBufferCommand>().Set(source, destination, srcOffset, dstOffset, size);
            _renderer.QueueCommand();
        }

        public void DispatchCompute(int groupsX, int groupsY, int groupsZ)
        {
            _renderer.New<DispatchComputeCommand>().Set(groupsX, groupsY, groupsZ);
            _renderer.QueueCommand();
        }

        public void Draw(int vertexCount, int instanceCount, int firstVertex, int firstInstance)
        {
            _renderer.New<DrawCommand>().Set(vertexCount, instanceCount, firstVertex, firstInstance);
            _renderer.QueueCommand();
        }

        public void DrawIndexed(int indexCount, int instanceCount, int firstIndex, int firstVertex, int firstInstance)
        {
            _renderer.New<DrawIndexedCommand>().Set(indexCount, instanceCount, firstIndex, firstVertex, firstInstance);
            _renderer.QueueCommand();
        }

        public void EndHostConditionalRendering()
        {
            _renderer.New<EndHostConditionalRenderingCommand>();
            _renderer.QueueCommand();
        }

        public void EndTransformFeedback()
        {
            _renderer.New<EndTransformFeedbackCommand>();
            _renderer.QueueCommand();
        }

        public void SetAlphaTest(bool enable, float reference, CompareOp op)
        {
            _renderer.New<SetAlphaTestCommand>().Set(enable, reference, op);
            _renderer.QueueCommand();
        }

        public void SetBlendState(int index, BlendDescriptor blend)
        {
            _renderer.New<SetBlendStateCommand>().Set(index, blend);
            _renderer.QueueCommand();
        }

        public void SetDepthBias(PolygonModeMask enables, float factor, float units, float clamp)
        {
            _renderer.New<SetDepthBiasCommand>().Set(enables, factor, units, clamp);
            _renderer.QueueCommand();
        }

        public void SetDepthClamp(bool clamp)
        {
            _renderer.New<SetDepthClampCommand>().Set(clamp);
            _renderer.QueueCommand();
        }

        public void SetDepthMode(DepthMode mode)
        {
            _renderer.New<SetDepthModeCommand>().Set(mode);
            _renderer.QueueCommand();
        }

        public void SetDepthTest(DepthTestDescriptor depthTest)
        {
            _renderer.New<SetDepthTestCommand>().Set(depthTest);
            _renderer.QueueCommand();
        }

        public void SetFaceCulling(bool enable, Face face)
        {
            _renderer.New<SetFaceCullingCommand>().Set(enable, face);
            _renderer.QueueCommand();
        }

        public void SetFrontFace(FrontFace frontFace)
        {
            _renderer.New<SetFrontFaceCommand>().Set(frontFace);
            _renderer.QueueCommand();
        }

        public void SetImage(int binding, ITexture texture, Format imageFormat)
        {
            _renderer.New<SetImageCommand>().Set(binding, Ref((ThreadedTexture)texture), imageFormat);
            _renderer.QueueCommand();
        }

        public void SetIndexBuffer(BufferRange buffer, IndexType type)
        {
            _renderer.New<SetIndexBufferCommand>().Set(buffer, type);
            _renderer.QueueCommand();
        }

        public void SetLogicOpState(bool enable, LogicalOp op)
        {
            _renderer.New<SetLogicOpStateCommand>().Set(enable, op);
            _renderer.QueueCommand();
        }

        public void SetPointParameters(float size, bool isProgramPointSize, bool enablePointSprite, Origin origin)
        {
            _renderer.New<SetPointParametersCommand>().Set(size, isProgramPointSize, enablePointSprite, origin);
            _renderer.QueueCommand();
        }

        public void SetPrimitiveRestart(bool enable, int index)
        {
            _renderer.New<SetPrimitiveRestartCommand>().Set(enable, index);
            _renderer.QueueCommand();
        }

        public void SetPrimitiveTopology(PrimitiveTopology topology)
        {
            _renderer.New<SetPrimitiveTopologyCommand>().Set(topology);
            _renderer.QueueCommand();
        }

        public void SetProgram(IProgram program)
        {
            _renderer.New<SetProgramCommand>().Set(Ref((ThreadedProgram)program));
            _renderer.QueueCommand();
        }

        public void SetRasterizerDiscard(bool discard)
        {
            _renderer.New<SetRasterizerDiscardCommand>().Set(discard);
            _renderer.QueueCommand();
        }

        public void SetRenderTargetColorMasks(ReadOnlySpan<uint> componentMask)
        {
            _renderer.New<SetRenderTargetColorMasksCommand>().Set(_renderer.CopySpan(componentMask));
            _renderer.QueueCommand();
        }

        public void SetRenderTargets(ITexture[] colors, ITexture depthStencil)
        {
            _renderer.New<SetRenderTargetsCommand>().Set(Ref(colors.Select(color => (ThreadedTexture)color).ToArray()), Ref((ThreadedTexture)depthStencil));
            _renderer.QueueCommand();
        }

        public void SetRenderTargetScale(float scale)
        {
            _renderer.New<SetRenderTargetScaleCommand>().Set(scale);
            _renderer.QueueCommand();
        }

        public void SetSampler(int binding, ISampler sampler)
        {
            _renderer.New<SetSamplerCommand>().Set(binding, Ref((ThreadedSampler)sampler));
            _renderer.QueueCommand();
        }

        public void SetScissor(int index, bool enable, int x, int y, int width, int height)
        {
            _renderer.New<SetScissorCommand>().Set(index, enable, x, y, width, height);
            _renderer.QueueCommand();
        }

        public void SetStencilTest(StencilTestDescriptor stencilTest)
        {
            _renderer.New<SetStencilTestCommand>().Set(stencilTest);
            _renderer.QueueCommand();
        }

        public void SetStorageBuffers(ReadOnlySpan<BufferRange> buffers)
        {
            _renderer.New<SetGenericBuffersCommand>().Set(_renderer.CopySpan(buffers), Ref(_setStorageBuffers));
            _renderer.QueueCommand();
        }

        public void SetTexture(int binding, ITexture texture)
        {
            _renderer.New<SetTextureCommand>().Set(binding, Ref((ThreadedTexture)texture));
            _renderer.QueueCommand();
        }

        public void SetTransformFeedbackBuffers(ReadOnlySpan<BufferRange> buffers)
        {
            _renderer.New<SetGenericBuffersCommand>().Set(_renderer.CopySpan(buffers), Ref(_setTransformFeedbackBuffers));
            _renderer.QueueCommand();
        }

        public void SetUniformBuffers(ReadOnlySpan<BufferRange> buffers)
        {
            _renderer.New<SetGenericBuffersCommand>().Set(_renderer.CopySpan(buffers), Ref(_setUniformBuffers));
            _renderer.QueueCommand();
        }

        public void SetUserClipDistance(int index, bool enableClip)
        {
            _renderer.New<SetUserClipDistanceCommand>().Set(index, enableClip);
            _renderer.QueueCommand();
        }

        public void SetVertexAttribs(ReadOnlySpan<VertexAttribDescriptor> vertexAttribs)
        {
            _renderer.New<SetVertexAttribsCommand>().Set(_renderer.CopySpan(vertexAttribs));
            _renderer.QueueCommand();
        }

        public void SetVertexBuffers(ReadOnlySpan<VertexBufferDescriptor> vertexBuffers)
        {
            _renderer.New<SetVertexBuffersCommand>().Set(_renderer.CopySpan(vertexBuffers));
            _renderer.QueueCommand();
        }

        public void SetViewports(int first, ReadOnlySpan<Viewport> viewports)
        {
            _renderer.New<SetViewportsCommand>().Set(first, _renderer.CopySpan(viewports));
            _renderer.QueueCommand();
        }

        public void TextureBarrier()
        {
            _renderer.New<TextureBarrierCommand>();
            _renderer.QueueCommand();
        }

        public void TextureBarrierTiled()
        {
            _renderer.New<TextureBarrierTiledCommand>();
            _renderer.QueueCommand();
        }

        public bool TryHostConditionalRendering(ICounterEvent value, ulong compare, bool isEqual)
        {
            var evt = value as ThreadedCounterEvent;
            if (evt != null)
            {
                if (compare == 0 && evt.Type == CounterType.SamplesPassed && evt.ClearCounter)
                {
                    _renderer.New<TryHostConditionalRenderingCommand>().Set(Ref(evt), compare, isEqual);
                    _renderer.QueueCommand();
                    return true;
                }
            }

            _renderer.New<TryHostConditionalRenderingFlushCommand>().Set(Ref(evt), Ref<ThreadedCounterEvent>(null), isEqual);
            _renderer.QueueCommand();
            return false;
        }

        public bool TryHostConditionalRendering(ICounterEvent value, ICounterEvent compare, bool isEqual)
        {
            _renderer.New<TryHostConditionalRenderingFlushCommand>().Set(Ref(value as ThreadedCounterEvent), Ref(compare as ThreadedCounterEvent), isEqual);
            _renderer.QueueCommand();
            return false;
        }

        public void UpdateRenderScale(ShaderStage stage, ReadOnlySpan<float> scales, int textureCount, int imageCount)
        {
            _renderer.New<UpdateRenderScaleCommand>().Set(stage, _renderer.CopySpan(scales.Slice(0, textureCount + imageCount)), textureCount, imageCount);
            _renderer.QueueCommand();
        }
    }
}
