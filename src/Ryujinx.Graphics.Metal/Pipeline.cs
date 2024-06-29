using Ryujinx.Common.Logging;
using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.Shader;
using SharpMetal.Foundation;
using SharpMetal.Metal;
using SharpMetal.QuartzCore;
using System;
using System.Runtime.Versioning;

namespace Ryujinx.Graphics.Metal
{
    public enum EncoderType
    {
        Blit,
        Compute,
        Render,
        None
    }

    [SupportedOSPlatform("macos")]
    class Pipeline : IPipeline, IDisposable
    {
        private const ulong MinByteWeightForFlush = 256 * 1024 * 1024; // MiB

        private readonly MTLDevice _device;
        private readonly MetalRenderer _renderer;
        private EncoderStateManager _encoderStateManager;
        private ulong _byteWeight;

        public readonly Action EndRenderPassDelegate;
        public MTLCommandBuffer CommandBuffer;

        public IndexBufferPattern QuadsToTrisPattern;
        public IndexBufferPattern TriFanToTrisPattern;

        internal CommandBufferScoped? PreloadCbs { get; private set; }
        internal CommandBufferScoped Cbs { get; private set; }
        internal MTLCommandEncoder? CurrentEncoder { get; private set; }
        internal EncoderType CurrentEncoderType { get; private set; } = EncoderType.None;
        internal bool RenderPassActive { get; private set; }

        public Pipeline(MTLDevice device, MetalRenderer renderer)
        {
            _device = device;
            _renderer = renderer;

            EndRenderPassDelegate = EndCurrentPass;

            CommandBuffer = (Cbs = _renderer.CommandBufferPool.Rent()).CommandBuffer;
        }

        internal void InitEncoderStateManager(BufferManager bufferManager)
        {
            _encoderStateManager = new EncoderStateManager(_device, bufferManager, this);

            QuadsToTrisPattern = new IndexBufferPattern(_renderer, 4, 6, 0, [0, 1, 2, 0, 2, 3], 4, false);
            TriFanToTrisPattern = new IndexBufferPattern(_renderer, 3, 3, 2, [int.MinValue, -1, 0], 1, true);
        }

        public EncoderState SwapState(EncoderState state, DirtyFlags flags = DirtyFlags.All)
        {
            return _encoderStateManager.SwapState(state, flags);
        }

        public PredrawState SavePredrawState()
        {
            return _encoderStateManager.SavePredrawState();
        }

        public void RestorePredrawState(PredrawState state)
        {
            _encoderStateManager.RestorePredrawState(state);
        }

        public void SetClearLoadAction(bool clear)
        {
            _encoderStateManager.SetClearLoadAction(clear);
        }

        public MTLRenderCommandEncoder GetOrCreateRenderEncoder(bool forDraw = false)
        {
            MTLRenderCommandEncoder renderCommandEncoder;
            if (CurrentEncoder == null || CurrentEncoderType != EncoderType.Render)
            {
                renderCommandEncoder = BeginRenderPass();
            }
            else
            {
                renderCommandEncoder = new MTLRenderCommandEncoder(CurrentEncoder.Value);
            }

            if (forDraw)
            {
                _encoderStateManager.RebindRenderState(renderCommandEncoder);
            }

            return renderCommandEncoder;
        }

        public MTLBlitCommandEncoder GetOrCreateBlitEncoder()
        {
            if (CurrentEncoder != null)
            {
                if (CurrentEncoderType == EncoderType.Blit)
                {
                    return new MTLBlitCommandEncoder(CurrentEncoder.Value);
                }
            }

            return BeginBlitPass();
        }

        public MTLComputeCommandEncoder GetOrCreateComputeEncoder(bool forDispatch = false)
        {
            MTLComputeCommandEncoder computeCommandEncoder;
            if (CurrentEncoder == null || CurrentEncoderType != EncoderType.Compute)
            {
                computeCommandEncoder = BeginComputePass();
            }
            else
            {
                computeCommandEncoder = new MTLComputeCommandEncoder(CurrentEncoder.Value);
            }

            if (forDispatch)
            {
                _encoderStateManager.RebindComputeState(computeCommandEncoder);
            }

            return computeCommandEncoder;
        }

        public void EndCurrentPass()
        {
            if (CurrentEncoder != null)
            {
                switch (CurrentEncoderType)
                {
                    case EncoderType.Blit:
                        new MTLBlitCommandEncoder(CurrentEncoder.Value).EndEncoding();
                        CurrentEncoder = null;
                        break;
                    case EncoderType.Compute:
                        new MTLComputeCommandEncoder(CurrentEncoder.Value).EndEncoding();
                        CurrentEncoder = null;
                        break;
                    case EncoderType.Render:
                        new MTLRenderCommandEncoder(CurrentEncoder.Value).EndEncoding();
                        CurrentEncoder = null;
                        RenderPassActive = false;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                CurrentEncoderType = EncoderType.None;
            }
        }

        private MTLRenderCommandEncoder BeginRenderPass()
        {
            EndCurrentPass();

            var renderCommandEncoder = _encoderStateManager.CreateRenderCommandEncoder();

            CurrentEncoder = renderCommandEncoder;
            CurrentEncoderType = EncoderType.Render;
            RenderPassActive = true;

            return renderCommandEncoder;
        }

        private MTLBlitCommandEncoder BeginBlitPass()
        {
            EndCurrentPass();

            var descriptor = new MTLBlitPassDescriptor();
            var blitCommandEncoder = Cbs.CommandBuffer.BlitCommandEncoder(descriptor);

            CurrentEncoder = blitCommandEncoder;
            CurrentEncoderType = EncoderType.Blit;
            return blitCommandEncoder;
        }

        private MTLComputeCommandEncoder BeginComputePass()
        {
            EndCurrentPass();

            var computeCommandEncoder = _encoderStateManager.CreateComputeCommandEncoder();

            CurrentEncoder = computeCommandEncoder;
            CurrentEncoderType = EncoderType.Compute;
            return computeCommandEncoder;
        }

        public void Present(CAMetalDrawable drawable, Texture src, Extents2D srcRegion, Extents2D dstRegion, bool isLinear)
        {
            // TODO: Clean this up
            var textureInfo = new TextureCreateInfo((int)drawable.Texture.Width, (int)drawable.Texture.Height, (int)drawable.Texture.Depth, (int)drawable.Texture.MipmapLevelCount, (int)drawable.Texture.SampleCount, 0, 0, 0, Format.B8G8R8A8Unorm, 0, Target.Texture2D, SwizzleComponent.Red, SwizzleComponent.Green, SwizzleComponent.Blue, SwizzleComponent.Alpha);
            var dst = new Texture(_device, _renderer, this, textureInfo, drawable.Texture, 0, 0);

            _renderer.HelperShader.BlitColor(Cbs, src, dst, srcRegion, dstRegion, isLinear, true);

            EndCurrentPass();

            Cbs.CommandBuffer.PresentDrawable(drawable);

            CommandBuffer = (Cbs = _renderer.CommandBufferPool.ReturnAndRent(Cbs)).CommandBuffer;

            // TODO: Auto flush counting
            _renderer.SyncManager.GetAndResetWaitTicks();

            // Cleanup
            dst.Dispose();
        }

        public void FlushCommandsIfWeightExceeding(IAuto disposedResource, ulong byteWeight)
        {
            bool usedByCurrentCb = disposedResource.HasCommandBufferDependency(Cbs);

            if (PreloadCbs != null && !usedByCurrentCb)
            {
                usedByCurrentCb = disposedResource.HasCommandBufferDependency(PreloadCbs.Value);
            }

            if (usedByCurrentCb)
            {
                // Since we can only free memory after the command buffer that uses a given resource was executed,
                // keeping the command buffer might cause a high amount of memory to be in use.
                // To prevent that, we force submit command buffers if the memory usage by resources
                // in use by the current command buffer is above a given limit, and those resources were disposed.
                _byteWeight += byteWeight;

                if (_byteWeight >= MinByteWeightForFlush)
                {
                    FlushCommandsImpl();
                }
            }
        }

        public void FlushCommandsImpl()
        {
            EndCurrentPass();

            _byteWeight = 0;

            if (PreloadCbs != null)
            {
                PreloadCbs.Value.Dispose();
                PreloadCbs = null;
            }

            CommandBuffer = (Cbs = _renderer.CommandBufferPool.ReturnAndRent(Cbs)).CommandBuffer;
            _renderer.RegisterFlush();
        }

        public void Blit(
            ITexture src,
            ITexture dst,
            Extents2D srcRegion,
            Extents2D dstRegion,
            bool isDepthOrStencil,
            bool linearFilter)
        {
            if (isDepthOrStencil)
            {
                // TODO: Depth & stencil blit!
                Logger.Warning?.PrintMsg(LogClass.Gpu, "Requested a depth or stencil blit!");
            }
            else
            {
                _renderer.HelperShader.BlitColor(Cbs, src, dst, srcRegion, dstRegion, linearFilter);
            }
        }

        public void Barrier()
        {
            switch (CurrentEncoderType)
            {
                case EncoderType.Render:
                    {
                        var renderCommandEncoder = GetOrCreateRenderEncoder();

                        var scope = MTLBarrierScope.Buffers | MTLBarrierScope.Textures | MTLBarrierScope.RenderTargets;
                        MTLRenderStages stages = MTLRenderStages.RenderStageVertex | MTLRenderStages.RenderStageFragment;
                        renderCommandEncoder.MemoryBarrier(scope, stages, stages);
                        break;
                    }
                case EncoderType.Compute:
                    {
                        var computeCommandEncoder = GetOrCreateComputeEncoder();

                        var scope = MTLBarrierScope.Buffers | MTLBarrierScope.Textures | MTLBarrierScope.RenderTargets;;
                        computeCommandEncoder.MemoryBarrier(scope);
                        break;
                    }
            }
        }

        public void ClearBuffer(BufferHandle destination, int offset, int size, uint value)
        {
            var blitCommandEncoder = GetOrCreateBlitEncoder();

            var mtlBuffer = _renderer.BufferManager.GetBuffer(destination, offset, size, true).Get(Cbs, offset, size, true).Value;

            // Might need a closer look, range's count, lower, and upper bound
            // must be a multiple of 4
            blitCommandEncoder.FillBuffer(mtlBuffer,
                new NSRange
                {
                    location = (ulong)offset,
                    length = (ulong)size
                },
                (byte)value);
        }

        public void ClearRenderTargetColor(int index, int layer, int layerCount, uint componentMask, ColorF color)
        {
            float[] colors = [color.Red, color.Green, color.Blue, color.Alpha];
            var dst = _encoderStateManager.RenderTargets[index];

            // TODO: Remove workaround for Wonder which has an invalid texture due to unsupported format
            if (dst == null)
            {
                Logger.Warning?.PrintMsg(LogClass.Gpu, "Attempted to clear invalid render target!");
                return;
            }

            _renderer.HelperShader.ClearColor(index, colors, componentMask, dst.Width, dst.Height);
        }

        public void ClearRenderTargetDepthStencil(int layer, int layerCount, float depthValue, bool depthMask, int stencilValue, int stencilMask)
        {
            var depthStencil = _encoderStateManager.DepthStencil;

            // TODO: Remove workaround for Wonder which has an invalid texture due to unsupported format
            if (depthStencil == null)
            {
                Logger.Warning?.PrintMsg(LogClass.Gpu, "Attempted to clear invalid depth stencil!");
                return;
            }

            _renderer.HelperShader.ClearDepthStencil(depthValue, depthMask, stencilValue, stencilMask, depthStencil.Width, depthStencil.Height);
        }

        public void CommandBufferBarrier()
        {
            Barrier();
        }

        public void CopyBuffer(BufferHandle src, BufferHandle dst, int srcOffset, int dstOffset, int size)
        {
            var srcBuffer = _renderer.BufferManager.GetBuffer(src, srcOffset, size, false);
            var dstBuffer = _renderer.BufferManager.GetBuffer(dst, dstOffset, size, true);

            BufferHolder.Copy(this, Cbs, srcBuffer, dstBuffer, srcOffset, dstOffset, size);
        }

        public void DispatchCompute(int groupsX, int groupsY, int groupsZ)
        {
            var computeCommandEncoder = GetOrCreateComputeEncoder(true);

            ComputeSize localSize = _encoderStateManager.ComputeLocalSize;

            computeCommandEncoder.DispatchThreadgroups(
                new MTLSize { width = (ulong)groupsX, height = (ulong)groupsY, depth = (ulong)groupsZ },
                new MTLSize { width = (ulong)localSize.X, height = (ulong)localSize.Y, depth = (ulong)localSize.Z });
        }

        public void Draw(int vertexCount, int instanceCount, int firstVertex, int firstInstance)
        {
            if (vertexCount == 0)
            {
                return;
            }

            var renderCommandEncoder = GetOrCreateRenderEncoder(true);

            if (TopologyUnsupported(_encoderStateManager.Topology))
            {
                var pattern = GetIndexBufferPattern();

                BufferHandle handle = pattern.GetRepeatingBuffer(vertexCount, out int indexCount);
                var buffer = _renderer.BufferManager.GetBuffer(handle, false);
                var mtlBuffer = buffer.Get(Cbs, 0, indexCount * sizeof(int)).Value;

                var primitiveType = TopologyRemap(_encoderStateManager.Topology).Convert();

                renderCommandEncoder.DrawIndexedPrimitives(
                    primitiveType,
                    (ulong)indexCount,
                    MTLIndexType.UInt32,
                    mtlBuffer,
                    0);
            }
            else
            {
                var primitiveType = TopologyRemap(_encoderStateManager.Topology).Convert();

                renderCommandEncoder.DrawPrimitives(
                    primitiveType,
                    (ulong)firstVertex,
                    (ulong)vertexCount,
                    (ulong)instanceCount,
                    (ulong)firstInstance);
            }
        }

        private IndexBufferPattern GetIndexBufferPattern()
        {
            return _encoderStateManager.Topology switch
            {
                PrimitiveTopology.Quads => QuadsToTrisPattern,
                PrimitiveTopology.TriangleFan or PrimitiveTopology.Polygon => TriFanToTrisPattern,
                _ => throw new NotSupportedException($"Unsupported topology: {_encoderStateManager.Topology}"),
            };
        }

        private PrimitiveTopology TopologyRemap(PrimitiveTopology topology)
        {
            return topology switch
            {
                PrimitiveTopology.Quads => PrimitiveTopology.Triangles,
                PrimitiveTopology.QuadStrip => PrimitiveTopology.TriangleStrip,
                PrimitiveTopology.TriangleFan or PrimitiveTopology.Polygon => PrimitiveTopology.Triangles,
                _ => topology,
            };
        }

        private bool TopologyUnsupported(PrimitiveTopology topology)
        {
            return topology switch
            {
                PrimitiveTopology.Quads or PrimitiveTopology.TriangleFan or PrimitiveTopology.Polygon => true,
                _ => false,
            };
        }

        public void DrawIndexed(int indexCount, int instanceCount, int firstIndex, int firstVertex, int firstInstance)
        {
            if (indexCount == 0)
            {
                return;
            }

            var renderCommandEncoder = GetOrCreateRenderEncoder(true);

            // TODO: Reindex unsupported topologies
            if (TopologyUnsupported(_encoderStateManager.Topology))
            {
                Logger.Warning?.Print(LogClass.Gpu, $"Drawing indexed with unsupported topology: {_encoderStateManager.Topology}");
            }

            var primitiveType = TopologyRemap(_encoderStateManager.Topology).Convert();

            var indexBuffer = _encoderStateManager.IndexBuffer;

            renderCommandEncoder.DrawIndexedPrimitives(
                primitiveType,
                (ulong)indexCount,
                _encoderStateManager.IndexType,
                indexBuffer.Get(Cbs, 0, indexCount * sizeof(int)).Value,
                _encoderStateManager.IndexBufferOffset,
                (ulong)instanceCount,
                firstVertex,
                (ulong)firstInstance);
        }

        public void DrawIndexedIndirect(BufferRange indirectBuffer)
        {
            // var renderCommandEncoder = GetOrCreateRenderEncoder(true);

            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        public void DrawIndexedIndirectCount(BufferRange indirectBuffer, BufferRange parameterBuffer, int maxDrawCount, int stride)
        {
            // var renderCommandEncoder = GetOrCreateRenderEncoder(true);

            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        public void DrawIndirect(BufferRange indirectBuffer)
        {
            // var renderCommandEncoder = GetOrCreateRenderEncoder(true);

            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        public void DrawIndirectCount(BufferRange indirectBuffer, BufferRange parameterBuffer, int maxDrawCount, int stride)
        {
            // var renderCommandEncoder = GetOrCreateRenderEncoder(true);

            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        public void DrawTexture(ITexture texture, ISampler sampler, Extents2DF srcRegion, Extents2DF dstRegion)
        {
            _renderer.HelperShader.DrawTexture(texture, sampler, srcRegion, dstRegion);
        }

        public void SetAlphaTest(bool enable, float reference, CompareOp op)
        {
            // This is currently handled using shader specialization, as Metal does not support alpha test.
            // In the future, we may want to use this to write the reference value into the support buffer,
            // to avoid creating one version of the shader per reference value used.
        }

        public void SetBlendState(AdvancedBlendDescriptor blend)
        {
            // Metal does not support advanced blend.
        }

        public void SetBlendState(int index, BlendDescriptor blend)
        {
            _encoderStateManager.UpdateBlendDescriptors(index, blend);
        }

        public void SetDepthBias(PolygonModeMask enables, float factor, float units, float clamp)
        {
            if (enables == 0)
            {
                _encoderStateManager.UpdateDepthBias(0, 0, 0);
            }
            else
            {
                _encoderStateManager.UpdateDepthBias(units, factor, clamp);
            }
        }

        public void SetDepthClamp(bool clamp)
        {
            _encoderStateManager.UpdateDepthClamp(clamp);
        }

        public void SetDepthMode(DepthMode mode)
        {
            // Metal does not support depth clip control.
        }

        public void SetDepthTest(DepthTestDescriptor depthTest)
        {
            _encoderStateManager.UpdateDepthState(depthTest);
        }

        public void SetFaceCulling(bool enable, Face face)
        {
            _encoderStateManager.UpdateCullMode(enable, face);
        }

        public void SetFrontFace(FrontFace frontFace)
        {
            _encoderStateManager.UpdateFrontFace(frontFace);
        }

        public void SetIndexBuffer(BufferRange buffer, IndexType type)
        {
            _encoderStateManager.UpdateIndexBuffer(buffer, type);
        }

        public void SetImage(ShaderStage stage, int binding, ITexture texture, Format imageFormat)
        {
            if (texture is TextureBase tex)
            {
                _encoderStateManager.UpdateTexture(stage, (ulong)binding, tex);
            }
        }

        public void SetImageArray(ShaderStage stage, int binding, IImageArray array)
        {
            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        public void SetImageArraySeparate(ShaderStage stage, int setIndex, IImageArray array)
        {
            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        public void SetLineParameters(float width, bool smooth)
        {
            // Metal does not support wide-lines.
        }

        public void SetLogicOpState(bool enable, LogicalOp op)
        {
            // Metal does not support logic operations.
        }

        public void SetMultisampleState(MultisampleDescriptor multisample)
        {
            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        public void SetPatchParameters(int vertices, ReadOnlySpan<float> defaultOuterLevel, ReadOnlySpan<float> defaultInnerLevel)
        {
            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        public void SetPointParameters(float size, bool isProgramPointSize, bool enablePointSprite, Origin origin)
        {
            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        public void SetPolygonMode(PolygonMode frontMode, PolygonMode backMode)
        {
            // Metal does not support polygon mode.
        }

        public void SetPrimitiveRestart(bool enable, int index)
        {
            // TODO: Supported for LineStrip and TriangleStrip
            // https://github.com/gpuweb/gpuweb/issues/1220#issuecomment-732483263
            // https://developer.apple.com/documentation/metal/mtlrendercommandencoder/1515520-drawindexedprimitives
            // https://stackoverflow.com/questions/70813665/how-to-render-multiple-trianglestrips-using-metal
            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        public void SetPrimitiveTopology(PrimitiveTopology topology)
        {
            _encoderStateManager.UpdatePrimitiveTopology(topology);
        }

        public void SetProgram(IProgram program)
        {
            _encoderStateManager.UpdateProgram(program);
        }

        public void SetRasterizerDiscard(bool discard)
        {
            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        public void SetRenderTargetColorMasks(ReadOnlySpan<uint> componentMask)
        {
            _encoderStateManager.UpdateRenderTargetColorMasks(componentMask);
        }

        public void SetRenderTargets(ITexture[] colors, ITexture depthStencil)
        {
            _encoderStateManager.UpdateRenderTargets(colors, depthStencil);
        }

        public void SetScissors(ReadOnlySpan<Rectangle<int>> regions)
        {
            _encoderStateManager.UpdateScissors(regions);
        }

        public void SetStencilTest(StencilTestDescriptor stencilTest)
        {
            _encoderStateManager.UpdateStencilState(stencilTest);
        }

        public void SetUniformBuffers(ReadOnlySpan<BufferAssignment> buffers)
        {
            _encoderStateManager.UpdateUniformBuffers(buffers);
        }

        public void SetStorageBuffers(ReadOnlySpan<BufferAssignment> buffers)
        {
            _encoderStateManager.UpdateStorageBuffers(buffers);
        }

        internal void SetStorageBuffers(int first, ReadOnlySpan<Auto<DisposableBuffer>> buffers)
        {
            _encoderStateManager.UpdateStorageBuffers(first, buffers);
        }

        public void SetTextureAndSampler(ShaderStage stage, int binding, ITexture texture, ISampler sampler)
        {
            if (texture is TextureBase tex)
            {
                if (sampler == null || sampler is Sampler)
                {
                    var index = (ulong)binding;

                    _encoderStateManager.UpdateTextureAndSampler(stage, index, tex, (Sampler)sampler);
                }
            }
        }

        public void SetTextureArray(ShaderStage stage, int binding, ITextureArray array)
        {
            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        public void SetTextureArraySeparate(ShaderStage stage, int setIndex, ITextureArray array)
        {
            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        public void SetUserClipDistance(int index, bool enableClip)
        {
            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        public void SetVertexAttribs(ReadOnlySpan<VertexAttribDescriptor> vertexAttribs)
        {
            _encoderStateManager.UpdateVertexAttribs(vertexAttribs);
        }

        public void SetVertexBuffers(ReadOnlySpan<VertexBufferDescriptor> vertexBuffers)
        {
            _encoderStateManager.UpdateVertexBuffers(vertexBuffers);
        }

        public void SetViewports(ReadOnlySpan<Viewport> viewports)
        {
            _encoderStateManager.UpdateViewports(viewports);
        }

        public void TextureBarrier()
        {
            if (CurrentEncoderType == EncoderType.Render)
            {
                var renderCommandEncoder = GetOrCreateRenderEncoder();

                renderCommandEncoder.MemoryBarrier(MTLBarrierScope.Textures, MTLRenderStages.RenderStageFragment, MTLRenderStages.RenderStageFragment);
            }
        }

        public void TextureBarrierTiled()
        {
            TextureBarrier();
        }

        public bool TryHostConditionalRendering(ICounterEvent value, ulong compare, bool isEqual)
        {
            // TODO: Implementable via indirect draw commands
            return false;
        }

        public bool TryHostConditionalRendering(ICounterEvent value, ICounterEvent compare, bool isEqual)
        {
            // TODO: Implementable via indirect draw commands
            return false;
        }

        public void EndHostConditionalRendering()
        {
            // TODO: Implementable via indirect draw commands
        }

        public void BeginTransformFeedback(PrimitiveTopology topology)
        {
            // Metal does not support transform feedback.
        }

        public void EndTransformFeedback()
        {
            // Metal does not support transform feedback.
        }

        public void SetTransformFeedbackBuffers(ReadOnlySpan<BufferRange> buffers)
        {
            // Metal does not support transform feedback.
        }

        public void Dispose()
        {
            EndCurrentPass();
            _encoderStateManager.Dispose();
        }
    }
}
