using Ryujinx.Graphics.Shader;
using System.Runtime.CompilerServices;

namespace Ryujinx.Graphics.Gpu.Memory
{
    internal enum BufferStage : byte
    {
        Compute,
        Vertex,
        TessellationControl,
        TessellationEvaluation,
        Geometry,
        Fragment,

        Indirect,
        VertexBuffer,
        IndexBuffer,
        Copy,
        TransformFeedback,
        Internal,
        None,

        StageMask = 0x3f,
        StorageMask = 0xc0,

        StorageRead = 0x40,
        StorageWrite = 0x80,

#pragma warning disable CA1069 // Enums values should not be duplicated
        StorageAtomic = 0xc0
#pragma warning restore CA1069 // Enums values should not be duplicated
    }

    internal static class BufferStageUtils
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BufferStage FromShaderStage(ShaderStage stage)
        {
            return (BufferStage)stage;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BufferStage FromShaderStage(int stageIndex)
        {
            return (BufferStage)(stageIndex + 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BufferStage FromUsage(BufferUsageFlags flags)
        {
            if (flags.HasFlag(BufferUsageFlags.Write))
            {
                return BufferStage.StorageWrite;
            }
            else
            {
                return BufferStage.StorageRead;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BufferStage FromUsage(TextureUsageFlags flags)
        {
            if (flags.HasFlag(TextureUsageFlags.ImageStore))
            {
                return BufferStage.StorageWrite;
            }
            else
            {
                return BufferStage.StorageRead;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BufferStage TextureBuffer(ShaderStage shaderStage, TextureUsageFlags flags)
        {
            return (BufferStage)shaderStage | FromUsage(flags);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BufferStage GraphicsStorage(int stageIndex, BufferUsageFlags flags)
        {
            return (BufferStage)(stageIndex + 1) | FromUsage(flags);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BufferStage ComputeStorage(BufferUsageFlags flags)
        {
            return BufferStage.Compute | FromUsage(flags);
        }
    }
}