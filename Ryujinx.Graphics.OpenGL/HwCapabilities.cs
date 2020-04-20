using OpenTK.Graphics.OpenGL;
using System;

namespace Ryujinx.Graphics.OpenGL
{
    static class HwCapabilities
    {
        private static readonly Lazy<bool> _supportsAstcCompression = new Lazy<bool>(() => HasExtension("GL_KHR_texture_compression_astc_ldr"));

        private static readonly Lazy<int> _maximumComputeSharedMemorySize = new Lazy<int>(() => GetLimit(All.MaxComputeSharedMemorySize));
        private static readonly Lazy<int> _storageBufferOffsetAlignment   = new Lazy<int>(() => GetLimit(All.ShaderStorageBufferOffsetAlignment));

        public enum GpuVendor
        {
            Unknown,
            Amd,
            Intel,
            Nvidia
        }

        private static readonly Lazy<GpuVendor> _gpuVendor = new Lazy<GpuVendor>(GetGpuVendor);

        public static GpuVendor Vendor => _gpuVendor.Value;

        private static Lazy<float> _maxSupportedAnisotropy = new Lazy<float>(GL.GetFloat((GetPName)All.MaxTextureMaxAnisotropy));

        public static bool SupportsAstcCompression          => _supportsAstcCompression.Value;
        public static bool SupportsNonConstantTextureOffset => _gpuVendor.Value == GpuVendor.Nvidia;

        public static int MaximumComputeSharedMemorySize => _maximumComputeSharedMemorySize.Value;
        public static int StorageBufferOffsetAlignment   => _storageBufferOffsetAlignment.Value;

        public static float MaxSupportedAnisotropy => _maxSupportedAnisotropy.Value;

        private static bool HasExtension(string name)
        {
            int numExtensions = GL.GetInteger(GetPName.NumExtensions);

            for (int extension = 0; extension < numExtensions; extension++)
            {
                if (GL.GetString(StringNameIndexed.Extensions, extension) == name)
                {
                    return true;
                }
            }

            return false;
        }

        private static int GetLimit(All name)
        {
            return GL.GetInteger((GetPName)name);
        }

        private static GpuVendor GetGpuVendor()
        {
            string vendor = GL.GetString(StringName.Vendor).ToLower();

            if (vendor == "nvidia corporation")
            {
                return GpuVendor.Nvidia;
            }
            else if (vendor == "intel")
            {
                return GpuVendor.Intel;
            }
            else if (vendor == "ati technologies inc." || vendor == "advanced micro devices, inc.")
            {
                return GpuVendor.Amd;
            }
            else
            {
                return GpuVendor.Unknown;
            }
        }
    }
}