﻿using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.Shader;
using Ryujinx.Graphics.Shader.Translation;
using Ryujinx.Graphics.Vulkan.Shaders;
using Silk.NET.Vulkan;
using System;
using VkFormat = Silk.NET.Vulkan.Format;

namespace Ryujinx.Graphics.Vulkan
{
    class HelperShader : IDisposable
    {
        private readonly PipelineHelperShader _pipeline;
        private readonly ISampler _samplerLinear;
        private readonly ISampler _samplerNearest;
        private readonly IProgram _programColorBlit;
        private readonly IProgram _programColorBlitClearAlpha;
        private readonly IProgram _programColorClear;

        public HelperShader(VulkanGraphicsDevice gd, Device device)
        {
            _pipeline = new PipelineHelperShader(gd, device);

            static GAL.SamplerCreateInfo GetSamplerCreateInfo(MinFilter minFilter, MagFilter magFilter)
            {
                return new GAL.SamplerCreateInfo(
                    minFilter,
                    magFilter,
                    false,
                    AddressMode.ClampToEdge,
                    AddressMode.ClampToEdge,
                    AddressMode.ClampToEdge,
                    CompareMode.None,
                    GAL.CompareOp.Always,
                    new ColorF(0f, 0f, 0f, 0f),
                    0f,
                    0f,
                    0f,
                    1f);
            }

            _samplerLinear = gd.CreateSampler(GetSamplerCreateInfo(MinFilter.Linear, MagFilter.Linear));
            _samplerNearest = gd.CreateSampler(GetSamplerCreateInfo(MinFilter.Nearest, MagFilter.Nearest));

            var vertexBindings = new ShaderBindings(
                new[] { 1 },
                Array.Empty<int>(),
                Array.Empty<int>(),
                Array.Empty<int>());

            var fragmentBindings = new ShaderBindings(
                Array.Empty<int>(),
                Array.Empty<int>(),
                new[] { 0 },
                Array.Empty<int>());

            _programColorBlit = gd.CreateProgramWithMinimalLayout(new[]
            {
                new ShaderSource(ShaderBinaries.ColorBlitVertexShaderSource, vertexBindings, ShaderStage.Vertex, TargetLanguage.Glsl),
                new ShaderSource(ShaderBinaries.ColorBlitFragmentShaderSource, fragmentBindings, ShaderStage.Fragment, TargetLanguage.Glsl),
            });

            _programColorBlitClearAlpha = gd.CreateProgramWithMinimalLayout(new[]
            {
                new ShaderSource(ShaderBinaries.ColorBlitVertexShaderSource, vertexBindings, ShaderStage.Vertex, TargetLanguage.Glsl),
                new ShaderSource(ShaderBinaries.ColorBlitClearAlphaFragmentShaderSource, fragmentBindings, ShaderStage.Fragment, TargetLanguage.Glsl),
            });

            var fragmentBindings2 = new ShaderBindings(
                Array.Empty<int>(),
                Array.Empty<int>(),
                Array.Empty<int>(),
                Array.Empty<int>());

            _programColorClear = gd.CreateProgramWithMinimalLayout(new[]
            {
                new ShaderSource(ShaderBinaries.ColorClearVertexShaderSource, vertexBindings, ShaderStage.Vertex, TargetLanguage.Glsl),
                new ShaderSource(ShaderBinaries.ColorClearFragmentShaderSource, fragmentBindings2, ShaderStage.Fragment, TargetLanguage.Glsl),
            });
        }

        public void Blit(
            VulkanGraphicsDevice gd,
            TextureView src,
            Auto<DisposableImageView> dst,
            int dstWidth,
            int dstHeight,
            VkFormat dstFormat,
            Extents2D srcRegion,
            Extents2D dstRegion,
            bool linearFilter,
            bool clearAlpha = false)
        {
            gd.FlushAllCommands();

            using var cbs = gd.CommandBufferPool.Rent();

            Blit(gd, cbs, src, dst, dstWidth, dstHeight, dstFormat, srcRegion, dstRegion, linearFilter, clearAlpha);
        }

        public void Blit(
            VulkanGraphicsDevice gd,
            CommandBufferScoped cbs,
            TextureView src,
            Auto<DisposableImageView> dst,
            int dstWidth,
            int dstHeight,
            VkFormat dstFormat,
            Extents2D srcRegion,
            Extents2D dstRegion,
            bool linearFilter,
            bool clearAlpha = false)
        {
            _pipeline.SetCommandBuffer(cbs);

            const int RegionBufferSize = 16;

            var sampler = linearFilter ? _samplerLinear : _samplerNearest;

            _pipeline.SetTextureAndSampler(ShaderStage.Fragment, 0, src, sampler);

            Span<float> region = stackalloc float[RegionBufferSize / sizeof(float)];

            region[0] = (float)srcRegion.X1 / src.Width;
            region[1] = (float)srcRegion.X2 / src.Width;
            region[2] = (float)srcRegion.Y1 / src.Height;
            region[3] = (float)srcRegion.Y2 / src.Height;

            if (dstRegion.X1 > dstRegion.X2)
            {
                float temp = region[0];
                region[0] = region[1];
                region[1] = temp;
            }

            if (dstRegion.Y1 > dstRegion.Y2)
            {
                float temp = region[2];
                region[2] = region[3];
                region[3] = temp;
            }

            var bufferHandle = gd.BufferManager.CreateWithHandle(gd, RegionBufferSize, false);

            gd.BufferManager.SetData<float>(bufferHandle, 0, region);

            Span<BufferRange> bufferRanges = stackalloc BufferRange[1];

            bufferRanges[0] = new BufferRange(bufferHandle, 0, RegionBufferSize);

            _pipeline.SetUniformBuffers(1, bufferRanges);

            Span<GAL.Viewport> viewports = stackalloc GAL.Viewport[1];

            var rect = new Rectangle<float>(
                MathF.Min(dstRegion.X1, dstRegion.X2),
                MathF.Min(dstRegion.Y1, dstRegion.Y2),
                MathF.Abs(dstRegion.X2 - dstRegion.X1),
                MathF.Abs(dstRegion.Y2 - dstRegion.Y1));

            viewports[0] = new GAL.Viewport(
                rect,
                ViewportSwizzle.PositiveX,
                ViewportSwizzle.PositiveY,
                ViewportSwizzle.PositiveZ,
                ViewportSwizzle.PositiveW,
                0f,
                1f);

            Span<Rectangle<int>> scissors = stackalloc Rectangle<int>[1];

            scissors[0] = new Rectangle<int>(0, 0, dstWidth, dstHeight);

            _pipeline.SetProgram(clearAlpha ? _programColorBlitClearAlpha : _programColorBlit);
            _pipeline.SetRenderTarget(dst, (uint)dstWidth, (uint)dstHeight, false, dstFormat);
            _pipeline.SetRenderTargetColorMasks(new uint[] { 0xf });
            _pipeline.SetScissors(scissors);

            if (clearAlpha)
            {
                _pipeline.ClearRenderTargetColor(0, 0, new ColorF(0f, 0f, 0f, 1f));
            }

            _pipeline.SetViewports(0, viewports, false);
            _pipeline.SetPrimitiveTopology(GAL.PrimitiveTopology.TriangleStrip);
            _pipeline.Draw(4, 1, 0, 0);
            _pipeline.Finish();

            gd.BufferManager.Delete(bufferHandle);
        }

        public void Clear(
            VulkanGraphicsDevice gd,
            Auto<DisposableImageView> dst,
            ReadOnlySpan<float> clearColor,
            uint componentMask,
            int dstWidth,
            int dstHeight,
            VkFormat dstFormat,
            Rectangle<int> scissor)
        {
            gd.FlushAllCommands();

            using var cbs = gd.CommandBufferPool.Rent();

            _pipeline.SetCommandBuffer(cbs);

            const int ClearColorBufferSize = 16;

            var bufferHandle = gd.BufferManager.CreateWithHandle(gd, ClearColorBufferSize, false);

            gd.BufferManager.SetData<float>(bufferHandle, 0, clearColor);

            Span<BufferRange> bufferRanges = stackalloc BufferRange[1];

            bufferRanges[0] = new BufferRange(bufferHandle, 0, ClearColorBufferSize);

            _pipeline.SetUniformBuffers(1, bufferRanges);

            Span<GAL.Viewport> viewports = stackalloc GAL.Viewport[1];

            viewports[0] = new GAL.Viewport(
                new Rectangle<float>(0, 0, dstWidth, dstHeight),
                ViewportSwizzle.PositiveX,
                ViewportSwizzle.PositiveY,
                ViewportSwizzle.PositiveZ,
                ViewportSwizzle.PositiveW,
                0f,
                1f);

            Span<Rectangle<int>> scissors = stackalloc Rectangle<int>[1];

            scissors[0] = scissor;

            _pipeline.SetProgram(_programColorClear);
            _pipeline.SetRenderTarget(dst, (uint)dstWidth, (uint)dstHeight, false, dstFormat);
            _pipeline.SetRenderTargetColorMasks(new uint[] { componentMask });
            _pipeline.SetViewports(0, viewports, false);
            _pipeline.SetScissors(scissors);
            _pipeline.SetPrimitiveTopology(GAL.PrimitiveTopology.TriangleStrip);
            _pipeline.Draw(4, 1, 0, 0);
            _pipeline.Finish();

            gd.BufferManager.Delete(bufferHandle);
        }

        public void DrawTexture(
            VulkanGraphicsDevice gd,
            PipelineBase pipeline,
            TextureView src,
            ISampler srcSampler,
            Extents2DF srcRegion,
            Extents2DF dstRegion)
        {
            const int RegionBufferSize = 16;

            pipeline.SetTextureAndSampler(ShaderStage.Fragment, 0, src, srcSampler);

            Span<float> region = stackalloc float[RegionBufferSize / sizeof(float)];

            region[0] = srcRegion.X1 / src.Width;
            region[1] = srcRegion.X2 / src.Width;
            region[2] = srcRegion.Y1 / src.Height;
            region[3] = srcRegion.Y2 / src.Height;

            if (dstRegion.X1 > dstRegion.X2)
            {
                float temp = region[0];
                region[0] = region[1];
                region[1] = temp;
            }

            if (dstRegion.Y1 > dstRegion.Y2)
            {
                float temp = region[2];
                region[2] = region[3];
                region[3] = temp;
            }

            var bufferHandle = gd.BufferManager.CreateWithHandle(gd, RegionBufferSize, false);

            gd.BufferManager.SetData<float>(bufferHandle, 0, region);

            Span<BufferRange> bufferRanges = stackalloc BufferRange[1];

            bufferRanges[0] = new BufferRange(bufferHandle, 0, RegionBufferSize);

            pipeline.SetUniformBuffers(1, bufferRanges);

            Span<GAL.Viewport> viewports = stackalloc GAL.Viewport[1];

            var rect = new Rectangle<float>(
                MathF.Min(dstRegion.X1, dstRegion.X2),
                MathF.Min(dstRegion.Y1, dstRegion.Y2),
                MathF.Abs(dstRegion.X2 - dstRegion.X1),
                MathF.Abs(dstRegion.Y2 - dstRegion.Y1));

            viewports[0] = new GAL.Viewport(
                rect,
                ViewportSwizzle.PositiveX,
                ViewportSwizzle.PositiveY,
                ViewportSwizzle.PositiveZ,
                ViewportSwizzle.PositiveW,
                0f,
                1f);

            Span<Rectangle<int>> scissors = stackalloc Rectangle<int>[1];

            pipeline.SetProgram(_programColorBlit);
            pipeline.SetViewports(0, viewports, false);
            pipeline.SetPrimitiveTopology(GAL.PrimitiveTopology.TriangleStrip);
            pipeline.Draw(4, 1, 0, 0);

            gd.BufferManager.Delete(bufferHandle);
        }

        public unsafe void ConvertI8ToI16(VulkanGraphicsDevice gd, CommandBufferScoped cbs, BufferHolder src, BufferHolder dst, int srcOffset, int size)
        {
            // TODO: Do this with a compute shader?
            var srcBuffer = src.GetBuffer().Get(cbs, srcOffset, size).Value;
            var dstBuffer = dst.GetBuffer().Get(cbs, 0, size * 2).Value;

            gd.Api.CmdFillBuffer(cbs.CommandBuffer, dstBuffer, 0, Vk.WholeSize, 0);

            var bufferCopy = new BufferCopy[size];

            for (ulong i = 0; i < (ulong)size; i++)
            {
                bufferCopy[i] = new BufferCopy((ulong)srcOffset + i, i * 2, 1);
            }

            BufferHolder.InsertBufferBarrier(
                gd,
                cbs.CommandBuffer,
                dstBuffer,
                BufferHolder.DefaultAccessFlags,
                AccessFlags.AccessTransferWriteBit,
                PipelineStageFlags.PipelineStageAllCommandsBit,
                PipelineStageFlags.PipelineStageTransferBit,
                0,
                size * 2);

            fixed (BufferCopy* pBufferCopy = bufferCopy)
            {
                gd.Api.CmdCopyBuffer(cbs.CommandBuffer, srcBuffer, dstBuffer, (uint)size, pBufferCopy);
            }

            BufferHolder.InsertBufferBarrier(
                gd,
                cbs.CommandBuffer,
                dstBuffer,
                AccessFlags.AccessTransferWriteBit,
                BufferHolder.DefaultAccessFlags,
                PipelineStageFlags.PipelineStageTransferBit,
                PipelineStageFlags.PipelineStageAllCommandsBit,
                0,
                size * 2);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _programColorBlitClearAlpha.Dispose();
                _programColorBlit.Dispose();
                _samplerNearest.Dispose();
                _samplerLinear.Dispose();
                _pipeline.Dispose();
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
    }
}
