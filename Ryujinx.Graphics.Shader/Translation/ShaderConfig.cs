using System;

namespace Ryujinx.Graphics.Shader.Translation
{
    struct ShaderConfig
    {
        public ShaderStage Stage { get; }

        public OutputTopology OutputTopology { get; }

        public int MaxOutputVertices { get; }

        public int LocalMemorySize { get; }

        public ImapPixelType[] ImapTypes { get; }

        public OmapTarget[] OmapTargets    { get; }
        public bool         OmapSampleMask { get; }
        public bool         OmapDepth      { get; }

        public IGpuAccessor GpuAccessor { get; }

        public TranslationFlags Flags { get; }

        public FeatureFlags UsedFeatures { get; set; }

        public ShaderConfig(IGpuAccessor gpuAccessor, TranslationFlags flags)
        {
            Stage             = ShaderStage.Compute;
            OutputTopology    = OutputTopology.PointList;
            MaxOutputVertices = 0;
            LocalMemorySize   = 0;
            ImapTypes         = null;
            OmapTargets       = null;
            OmapSampleMask    = false;
            OmapDepth         = false;
            GpuAccessor       = gpuAccessor;
            Flags             = flags;
            UsedFeatures      = FeatureFlags.None;
        }

        public ShaderConfig(ShaderHeader header, IGpuAccessor gpuAccessor, TranslationFlags flags)
        {
            Stage             = header.Stage;
            OutputTopology    = header.OutputTopology;
            MaxOutputVertices = header.MaxOutputVertexCount;
            LocalMemorySize   = header.ShaderLocalMemoryLowSize + header.ShaderLocalMemoryHighSize;
            ImapTypes         = header.ImapTypes;
            OmapTargets       = header.OmapTargets;
            OmapSampleMask    = header.OmapSampleMask;
            OmapDepth         = header.OmapDepth;
            GpuAccessor       = gpuAccessor;
            Flags             = flags;
            UsedFeatures      = FeatureFlags.None;
        }

        public int GetDepthRegister()
        {
            int count = 0;

            for (int index = 0; index < OmapTargets.Length; index++)
            {
                for (int component = 0; component < 4; component++)
                {
                    if (OmapTargets[index].ComponentEnabled(component))
                    {
                        count++;
                    }
                }
            }

            // The depth register is always two registers after the last color output.
            return count + 1;
        }

        public TextureFormat GetTextureFormat(int handle)
        {
            // When the formatted load extension is supported, we don't need to
            // specify a format, we can just declare it without a format and the GPU will handle it.
            if (GpuAccessor.QuerySupportsImageLoadFormatted())
            {
                return TextureFormat.Unknown;
            }

            var format = GpuAccessor.QueryTextureFormat(handle);

            if (format == TextureFormat.Unknown)
            {
                GpuAccessor.Log($"Unknown format for texture {handle}.");

                format = TextureFormat.R8G8B8A8Unorm;
            }

            return format;
        }
    }
}