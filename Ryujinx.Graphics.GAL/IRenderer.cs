using Ryujinx.Graphics.Shader;
using System;

namespace Ryujinx.Graphics.GAL
{
    public interface IRenderer : IDisposable
    {
        IPipeline Pipeline { get; }

        IWindow Window { get; }

        IShader CompileShader(ShaderProgram shader);

        IBuffer CreateBuffer(int size);

        IProgram CreateProgram(IShader[] shaders);
        IProgram CreateProgramFromGpuBinary(ReadOnlySpan<byte> data);

        ISampler CreateSampler(SamplerCreateInfo info);
        ITexture CreateTexture(TextureCreateInfo info);

        Capabilities GetCapabilities();

        ulong GetCounter(CounterType type);

        void Initialize();

        void ResetCounter(CounterType type);
    }
}
