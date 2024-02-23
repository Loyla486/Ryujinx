using Ryujinx.Common.Memory;
using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.Gpu.Engine.Threed;
using Ryujinx.Graphics.Shader;

namespace Ryujinx.Graphics.Gpu.Shader
{
    /// <summary>
    /// State used by the <see cref="GpuAccessor"/>.
    /// </summary>
    struct GpuChannelGraphicsState
    {
        // New fields should be added to the end of the struct to keep disk shader cache compatibility.

        /// <summary>
        /// Early Z force enable.
        /// </summary>
        public readonly bool EarlyZForce;

        /// <summary>
        /// Primitive topology of current draw.
        /// </summary>
        public readonly PrimitiveTopology Topology;

        /// <summary>
        /// Tessellation mode.
        /// </summary>
        public readonly TessMode TessellationMode;

        /// <summary>
        /// Indicates whether alpha-to-coverage is enabled.
        /// </summary>
        public readonly bool AlphaToCoverageEnable;

        /// <summary>
        /// Indicates whether alpha-to-coverage dithering is enabled.
        /// </summary>
        public readonly bool AlphaToCoverageDitherEnable;

        /// <summary>
        /// Indicates whether the viewport transform is disabled.
        /// </summary>
        public readonly bool ViewportTransformDisable;

        /// <summary>
        /// Depth mode zero to one or minus one to one.
        /// </summary>
        public readonly bool DepthMode;

        /// <summary>
        /// Indicates if the point size is set on the shader or is fixed.
        /// </summary>
        public readonly bool ProgramPointSizeEnable;

        /// <summary>
        /// Point size used if <see cref="ProgramPointSizeEnable" /> is false.
        /// </summary>
        public readonly float PointSize;

        /// <summary>
        /// Indicates whether alpha test is enabled.
        /// </summary>
        public readonly bool AlphaTestEnable;

        /// <summary>
        /// When alpha test is enabled, indicates the comparison that decides if the fragment should be discarded.
        /// </summary>
        public readonly CompareOp AlphaTestCompare;

        /// <summary>
        /// When alpha test is enabled, indicates the value to compare with the fragment output alpha.
        /// </summary>
        public readonly float AlphaTestReference;

        /// <summary>
        /// Type of the vertex attributes consumed by the shader.
        /// </summary>
        public Array32<AttributeType> AttributeTypes;

        /// <summary>
        /// Creates a new GPU graphics state.
        /// </summary>
        /// <param name="earlyZForce">Early Z force enable</param>
        /// <param name="topology">Primitive topology</param>
        /// <param name="tessellationMode">Tessellation mode</param>
        /// <param name="alphaToCoverageEnable">Indicates whether alpha-to-coverage is enabled</param>
        /// <param name="alphaToCoverageDitherEnable">Indicates whether alpha-to-coverage dithering is enabled</param>
        /// <param name="viewportTransformDisable">Indicates whether the viewport transform is disabled</param>
        /// <param name="depthMode">Depth mode zero to one or minus one to one</param>
        /// <param name="programPointSizeEnable">Indicates if the point size is set on the shader or is fixed</param>
        /// <param name="pointSize">Point size if not set from shader</param>
        /// <param name="alphaTestEnable">Indicates whether alpha test is enabled</param>
        /// <param name="alphaTestCompare">When alpha test is enabled, indicates the comparison that decides if the fragment should be discarded</param>
        /// <param name="alphaTestReference">When alpha test is enabled, indicates the value to compare with the fragment output alpha</param>
        /// <param name="attributeTypes">Type of the vertex attributes consumed by the shader</param>
        public GpuChannelGraphicsState(
            bool earlyZForce,
            PrimitiveTopology topology,
            TessMode tessellationMode,
            bool alphaToCoverageEnable,
            bool alphaToCoverageDitherEnable,
            bool viewportTransformDisable,
            bool depthMode,
            bool programPointSizeEnable,
            float pointSize,
            bool alphaTestEnable,
            CompareOp alphaTestCompare,
            float alphaTestReference,
            ref Array32<AttributeType> attributeTypes)
        {
            EarlyZForce = earlyZForce;
            Topology = topology;
            TessellationMode = tessellationMode;
            AlphaToCoverageEnable = alphaToCoverageEnable;
            AlphaToCoverageDitherEnable = alphaToCoverageDitherEnable;
            ViewportTransformDisable = viewportTransformDisable;
            DepthMode = depthMode;
            ProgramPointSizeEnable = programPointSizeEnable;
            PointSize = pointSize;
            AlphaTestEnable = alphaTestEnable;
            AlphaTestCompare = alphaTestCompare;
            AlphaTestReference = alphaTestReference;
            AttributeTypes = attributeTypes;
        }
    }
}