﻿using OpenTK.Graphics.OpenGL;
using Ryujinx.Common.Logging;
using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.OpenGL.Image;
using Ryujinx.Graphics.OpenGL.Queries;
using Ryujinx.Graphics.Shader;
using System;

namespace Ryujinx.Graphics.OpenGL
{
    public sealed class Renderer : IRenderer
    {
        private readonly Pipeline _pipeline;

        public IPipeline Pipeline => _pipeline;

        private readonly Counters _counters;

        private readonly Window _window;

        public IWindow Window => _window;

        internal TextureCopy TextureCopy { get; }

        public string GpuVendor { get; private set; }
        public string GpuRenderer { get; private set; }
        public string GpuVersion { get; private set; }

        public Renderer()
        {
            _pipeline = new Pipeline();
            _counters = new Counters();
            _window = new Window(this);
            TextureCopy = new TextureCopy(this);
        }

        public IShader CompileShader(ShaderProgram shader)
        {
            return new Shader(shader);
        }

        public BufferHandle CreateBuffer(int size)
        {
            return Buffer.Create(size);
        }

        public IProgram CreateProgram(IShader[] shaders)
        {
            return new Program(shaders);
        }

        public ISampler CreateSampler(SamplerCreateInfo info)
        {
            return new Sampler(info);
        }

        public ITexture CreateTexture(TextureCreateInfo info, float scaleFactor)
        {
            return info.Target == Target.TextureBuffer ? new TextureBuffer(info) : new TextureStorage(this, info, scaleFactor).CreateDefaultView();
        }

        public void DeleteBuffer(BufferHandle buffer)
        {
            Buffer.Delete(buffer);
        }

        public byte[] GetBufferData(BufferHandle buffer, int offset, int size)
        {
            return Buffer.GetData(buffer, offset, size);
        }

        public Capabilities GetCapabilities()
        {
            return new Capabilities(
                HwCapabilities.SupportsAstcCompression,
                HwCapabilities.SupportsImageLoadFormatted,
                HwCapabilities.SupportsNonConstantTextureOffset,
                HwCapabilities.SupportsViewportSwizzle,
                HwCapabilities.MaximumComputeSharedMemorySize,
                HwCapabilities.MaximumSupportedAnisotropy,
                HwCapabilities.StorageBufferOffsetAlignment);
        }

        public void SetBufferData(BufferHandle buffer, int offset, ReadOnlySpan<byte> data)
        {
            Buffer.SetData(buffer, offset, data);
        }

        public void UpdateCounters()
        {
            _counters.Update();
        }

        public ICounterEvent ReportCounter(CounterType type, EventHandler<ulong> resultHandler)
        {
            return _counters.QueueReport(type, resultHandler);
        }

        public void Initialize()
        {
            PrintGpuInformation();

            _counters.Initialize();
        }

        private void PrintGpuInformation()
        {
            GpuVendor   = GL.GetString(StringName.Vendor);
            GpuRenderer = GL.GetString(StringName.Renderer);
            GpuVersion  = GL.GetString(StringName.Version);

            Logger.PrintInfo(LogClass.Gpu, $"{GpuVendor} {GpuRenderer} ({GpuVersion})");
        }

        public void ResetCounter(CounterType type)
        {
            _counters.QueueReset(type);
        }

        public void Dispose()
        {
            TextureCopy.Dispose();
            _pipeline.Dispose();
            _window.Dispose();
            _counters.Dispose();
        }
    }
}
