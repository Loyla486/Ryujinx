using Ryujinx.Graphics.GAL;
using SharpMetal.Metal;
using System.Linq;
using System.Runtime.Versioning;

namespace Ryujinx.Graphics.Metal
{
    struct DirtyFlags
    {
        public bool RenderPipeline = false;
        public bool ComputePipeline = false;
        public bool DepthStencil = false;

        public DirtyFlags() { }

        public void MarkAll()
        {
            RenderPipeline = true;
            ComputePipeline = true;
            DepthStencil = true;
        }
    }

    record struct BufferRef
    {
        public Auto<DisposableBuffer> Buffer;
        public int Index;
        public BufferRange? Range;

        public BufferRef(Auto<DisposableBuffer> buffer, int index)
        {
            Buffer = buffer;
            Index = index;
        }

        public BufferRef(Auto<DisposableBuffer> buffer, int index, ref BufferRange range)
        {
            Buffer = buffer;
            Index = index;
            Range = range;
        }
    }

    [SupportedOSPlatform("macos")]
    struct EncoderState
    {
        public MTLFunction? VertexFunction = null;
        public MTLFunction? FragmentFunction = null;
        public MTLFunction? ComputeFunction = null;

        public TextureBase[] FragmentTextures = new TextureBase[Constants.MaxTextures];
        public MTLSamplerState[] FragmentSamplers = new MTLSamplerState[Constants.MaxSamplers];

        public TextureBase[] VertexTextures = new TextureBase[Constants.MaxTextures];
        public MTLSamplerState[] VertexSamplers = new MTLSamplerState[Constants.MaxSamplers];

        public TextureBase[] ComputeTextures = new TextureBase[Constants.MaxTextures];
        public MTLSamplerState[] ComputeSamplers = new MTLSamplerState[Constants.MaxSamplers];

        public BufferRef[] UniformBuffers = [];
        public BufferRef[] StorageBuffers = [];

        public Auto<DisposableBuffer> IndexBuffer = default;
        public MTLIndexType IndexType = MTLIndexType.UInt16;
        public ulong IndexBufferOffset = 0;

        public MTLDepthStencilState? DepthStencilState = null;

        public MTLDepthClipMode DepthClipMode = MTLDepthClipMode.Clip;
        public MTLCompareFunction DepthCompareFunction = MTLCompareFunction.Always;
        public bool DepthWriteEnabled = false;

        public float DepthBias;
        public float SlopeScale;
        public float Clamp;

        public MTLStencilDescriptor BackFaceStencil = new();
        public MTLStencilDescriptor FrontFaceStencil = new();
        public int BackRefValue = 0;
        public int FrontRefValue = 0;
        public bool StencilTestEnabled = false;

        public PrimitiveTopology Topology = PrimitiveTopology.Triangles;
        public MTLCullMode CullMode = MTLCullMode.None;
        public MTLWinding Winding = MTLWinding.CounterClockwise;

        public MTLViewport[] Viewports = [];
        public MTLScissorRect[] Scissors = [];

        // Changes to attachments take recreation!
        public Texture DepthStencil = default;
        public Texture[] RenderTargets = new Texture[Constants.MaxColorAttachments];

        public MTLColorWriteMask[] RenderTargetMasks = Enumerable.Repeat(MTLColorWriteMask.All, Constants.MaxColorAttachments).ToArray();
        public BlendDescriptor?[] BlendDescriptors = new BlendDescriptor?[Constants.MaxColorAttachments];
        public ColorF BlendColor = new();

        public VertexBufferDescriptor[] VertexBuffers = [];
        public VertexAttribDescriptor[] VertexAttribs = [];

        // Dirty flags
        public DirtyFlags Dirty = new();

        // Only to be used for present
        public bool ClearLoadAction = false;

        public EncoderState() { }

        public readonly EncoderState Clone()
        {
            // Certain state (like viewport and scissor) doesn't need to be cloned, as it is always reacreated when assigned to
            EncoderState clone = this;
            clone.FragmentTextures = (TextureBase[])FragmentTextures.Clone();
            clone.FragmentSamplers = (MTLSamplerState[])FragmentSamplers.Clone();
            clone.VertexTextures = (TextureBase[])VertexTextures.Clone();
            clone.VertexSamplers = (MTLSamplerState[])VertexSamplers.Clone();
            clone.ComputeTextures = (TextureBase[])ComputeTextures.Clone();
            clone.ComputeSamplers = (MTLSamplerState[])ComputeSamplers.Clone();
            clone.BlendDescriptors = (BlendDescriptor?[])BlendDescriptors.Clone();
            clone.VertexBuffers = (VertexBufferDescriptor[])VertexBuffers.Clone();
            clone.VertexAttribs = (VertexAttribDescriptor[])VertexAttribs.Clone();

            return clone;
        }
    }
}
