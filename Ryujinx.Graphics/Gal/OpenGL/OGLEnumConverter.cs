using OpenTK.Graphics.OpenGL;
using System;

namespace Ryujinx.Graphics.Gal.OpenGL
{
    static class OGLEnumConverter
    {
        public static FrontFaceDirection GetFrontFace(GalFrontFace FrontFace)
        {
            switch (FrontFace)
            {
                case GalFrontFace.CW:  return FrontFaceDirection.Cw;
                case GalFrontFace.CCW: return FrontFaceDirection.Ccw;
            }

            throw new ArgumentException(nameof(FrontFace));
        }

        public static CullFaceMode GetCullFace(GalCullFace CullFace)
        {
            switch (CullFace)
            {
                case GalCullFace.Front:        return CullFaceMode.Front;
                case GalCullFace.Back:         return CullFaceMode.Back;
                case GalCullFace.FrontAndBack: return CullFaceMode.FrontAndBack;
            }

            throw new ArgumentException(nameof(CullFace));
        }

        public static StencilOp GetStencilOp(GalStencilOp Op)
        {
            switch (Op)
            {
                case GalStencilOp.Keep:     return StencilOp.Keep;
                case GalStencilOp.Zero:     return StencilOp.Zero;
                case GalStencilOp.Replace:  return StencilOp.Replace;
                case GalStencilOp.Incr:     return StencilOp.Incr;
                case GalStencilOp.Decr:     return StencilOp.Decr;
                case GalStencilOp.Invert:   return StencilOp.Invert;
                case GalStencilOp.IncrWrap: return StencilOp.IncrWrap;
                case GalStencilOp.DecrWrap: return StencilOp.DecrWrap;
            }

            throw new ArgumentException(nameof(Op));
        }

        public static DepthFunction GetDepthFunc(GalComparisonOp Func)
        {
            //Looks like the GPU can take it's own values (described in GalComparisonOp) and OpenGL values alike
            if ((int)Func >= (int)DepthFunction.Never &&
                (int)Func <= (int)DepthFunction.Always)
            {
                return (DepthFunction)Func;
            }

            switch (Func)
            {
                case GalComparisonOp.Never:    return DepthFunction.Never;
                case GalComparisonOp.Less:     return DepthFunction.Less;
                case GalComparisonOp.Equal:    return DepthFunction.Equal;
                case GalComparisonOp.Lequal:   return DepthFunction.Lequal;
                case GalComparisonOp.Greater:  return DepthFunction.Greater;
                case GalComparisonOp.NotEqual: return DepthFunction.Notequal;
                case GalComparisonOp.Gequal:   return DepthFunction.Gequal;
                case GalComparisonOp.Always:   return DepthFunction.Always;
            }

            throw new ArgumentException(nameof(Func));
        }

        public static StencilFunction GetStencilFunc(GalComparisonOp Func)
        {
            //OGL comparison values match, it's just an enum cast
            return (StencilFunction)GetDepthFunc(Func);
        }

        public static DrawElementsType GetDrawElementsType(GalIndexFormat Format)
        {
            switch (Format)
            {
                case GalIndexFormat.Byte:  return DrawElementsType.UnsignedByte;
                case GalIndexFormat.Int16: return DrawElementsType.UnsignedShort;
                case GalIndexFormat.Int32: return DrawElementsType.UnsignedInt;
            }

            throw new ArgumentException(nameof(Format));
        }

        public static PrimitiveType GetPrimitiveType(GalPrimitiveType Type)
        {
            switch (Type)
            {
                case GalPrimitiveType.Points:                 return PrimitiveType.Points;
                case GalPrimitiveType.Lines:                  return PrimitiveType.Lines;
                case GalPrimitiveType.LineLoop:               return PrimitiveType.LineLoop;
                case GalPrimitiveType.LineStrip:              return PrimitiveType.LineStrip;
                case GalPrimitiveType.Triangles:              return PrimitiveType.Triangles;
                case GalPrimitiveType.TriangleStrip:          return PrimitiveType.TriangleStrip;
                case GalPrimitiveType.TriangleFan:            return PrimitiveType.TriangleFan;
                case GalPrimitiveType.Quads:                  return PrimitiveType.Quads;
                case GalPrimitiveType.QuadStrip:              return PrimitiveType.QuadStrip;
                case GalPrimitiveType.Polygon:                return PrimitiveType.Polygon;
                case GalPrimitiveType.LinesAdjacency:         return PrimitiveType.LinesAdjacency;
                case GalPrimitiveType.LineStripAdjacency:     return PrimitiveType.LineStripAdjacency;
                case GalPrimitiveType.TrianglesAdjacency:     return PrimitiveType.TrianglesAdjacency;
                case GalPrimitiveType.TriangleStripAdjacency: return PrimitiveType.TriangleStripAdjacency;
                case GalPrimitiveType.Patches:                return PrimitiveType.Patches;
            }

            throw new ArgumentException(nameof(Type));
        }

        public static ShaderType GetShaderType(GalShaderType Type)
        {
            switch (Type)
            {
                case GalShaderType.Vertex:         return ShaderType.VertexShader;
                case GalShaderType.TessControl:    return ShaderType.TessControlShader;
                case GalShaderType.TessEvaluation: return ShaderType.TessEvaluationShader;
                case GalShaderType.Geometry:       return ShaderType.GeometryShader;
                case GalShaderType.Fragment:       return ShaderType.FragmentShader;
            }

            throw new ArgumentException(nameof(Type));
        }

        public static PixelInternalFormat GetFrameBufferInternalFormat(GalFrameBufferFormat Format)
        {
            switch (Format)
            {
                //Sometimes it's not set, use a safe format
                case 0: return PixelInternalFormat.Rgba8;

                case GalFrameBufferFormat.RGBA32Float:    return PixelInternalFormat.Rgba32f;
                case GalFrameBufferFormat.RGBA32Sint:     return PixelInternalFormat.Rgba32i;
                case GalFrameBufferFormat.RGBA32Uint:     return PixelInternalFormat.Rgba32ui;
                case GalFrameBufferFormat.RGBA16Unorm:    return PixelInternalFormat.Rgba16;
                case GalFrameBufferFormat.RGBA16Snorm:    return PixelInternalFormat.Rgba16Snorm;
                case GalFrameBufferFormat.RGBA16Sint:     return PixelInternalFormat.Rgba16i;
                case GalFrameBufferFormat.RGBA16Uint:     return PixelInternalFormat.Rgba16ui;
                case GalFrameBufferFormat.RGBA16Float:    return PixelInternalFormat.Rgba16f;
                case GalFrameBufferFormat.RG32Float:      return PixelInternalFormat.Rg32f;
                case GalFrameBufferFormat.RG32Sint:       return PixelInternalFormat.Rg32i;
                case GalFrameBufferFormat.RG32Uint:       return PixelInternalFormat.Rg32ui;
                case GalFrameBufferFormat.RGB10A2Unorm:   return PixelInternalFormat.Rgb10A2;
                case GalFrameBufferFormat.RGB10A2Uint:    return PixelInternalFormat.Rgb10A2ui;
                case GalFrameBufferFormat.RGBA8Unorm:     return PixelInternalFormat.Rgba8;
                case GalFrameBufferFormat.RGBA8Srgb:      return PixelInternalFormat.Srgb8;
                case GalFrameBufferFormat.RG16Snorm:      return PixelInternalFormat.Rg16Snorm;
                case GalFrameBufferFormat.R11G11B10Float: return PixelInternalFormat.R11fG11fB10f;
                case GalFrameBufferFormat.R32Float:       return PixelInternalFormat.R32f;
                case GalFrameBufferFormat.R16Float:       return PixelInternalFormat.R16f;
                case GalFrameBufferFormat.R8Unorm:        return PixelInternalFormat.R8;
                case GalFrameBufferFormat.R8Snorm:        return PixelInternalFormat.R8Snorm;
                case GalFrameBufferFormat.R8Sint:         return PixelInternalFormat.R8i;
                case GalFrameBufferFormat.R8Uint:         return PixelInternalFormat.R8ui;
            }

            throw new NotImplementedException(Format.ToString());
        }

        public static (PixelFormat Format, PixelType Type) GetFrameBufferFormat(GalFrameBufferFormat Format)
        {
            switch (Format)
            {
                case 0: return (PixelFormat.Rgba, PixelType.UnsignedByte);

                case GalFrameBufferFormat.RGBA32Float:    return (PixelFormat.Rgba, PixelType.Float);
                case GalFrameBufferFormat.RGBA32Sint:     return (PixelFormat.Rgba, PixelType.Int);
                case GalFrameBufferFormat.RGBA32Uint:     return (PixelFormat.Rgba, PixelType.UnsignedInt);
                case GalFrameBufferFormat.RGBA16Unorm:    return (PixelFormat.Rgba, PixelType.UnsignedShort);
                case GalFrameBufferFormat.RGBA16Snorm:    return (PixelFormat.Rgba, PixelType.Short);
                case GalFrameBufferFormat.RGBA16Sint:     return (PixelFormat.Rgba, PixelType.Short);
                case GalFrameBufferFormat.RGBA16Uint:     return (PixelFormat.Rgba, PixelType.UnsignedShort);
                case GalFrameBufferFormat.RGBA16Float:    return (PixelFormat.Rgba, PixelType.HalfFloat);
                case GalFrameBufferFormat.RG32Float:      return (PixelFormat.Rg,   PixelType.Float);
                case GalFrameBufferFormat.RG32Sint:       return (PixelFormat.Rg,   PixelType.Int);
                case GalFrameBufferFormat.RG32Uint:       return (PixelFormat.Rg,   PixelType.UnsignedInt);
                case GalFrameBufferFormat.RGB10A2Unorm:   return (PixelFormat.Rgba, PixelType.UnsignedInt2101010Reversed);
                case GalFrameBufferFormat.RGB10A2Uint:    return (PixelFormat.Rgba, PixelType.UnsignedInt2101010Reversed);
                case GalFrameBufferFormat.RGBA8Unorm:     return (PixelFormat.Rgba, PixelType.UnsignedByte);
                case GalFrameBufferFormat.RGBA8Srgb:      return (PixelFormat.Rgba, PixelType.UnsignedByte);
                case GalFrameBufferFormat.RG16Snorm:      return (PixelFormat.Rg,   PixelType.Short);
                case GalFrameBufferFormat.R11G11B10Float: return (PixelFormat.Rgb,  PixelType.UnsignedInt10F11F11FRev);
                case GalFrameBufferFormat.R32Float:       return (PixelFormat.Red,  PixelType.Float);
                case GalFrameBufferFormat.R16Float:       return (PixelFormat.Red,  PixelType.HalfFloat);
                case GalFrameBufferFormat.R8Unorm:        return (PixelFormat.Red,  PixelType.UnsignedByte);
                case GalFrameBufferFormat.R8Snorm:        return (PixelFormat.Red,  PixelType.Byte);
                case GalFrameBufferFormat.R8Sint:         return (PixelFormat.Red,  PixelType.Byte);
                case GalFrameBufferFormat.R8Uint:         return (PixelFormat.Red,  PixelType.UnsignedByte);
            }

            throw new NotImplementedException(Format.ToString());
        }

        public static (PixelFormat, PixelType) GetTextureFormat(GalTextureFormat Format)
        {
            switch (Format)
            {
                case GalTextureFormat.R32G32B32A32: return (PixelFormat.Rgba,           PixelType.Float);
                case GalTextureFormat.R16G16B16A16: return (PixelFormat.Rgba,           PixelType.HalfFloat);
                case GalTextureFormat.A8B8G8R8:     return (PixelFormat.Rgba,           PixelType.UnsignedByte);
                case GalTextureFormat.A2B10G10R10:  return (PixelFormat.Rgba,           PixelType.UnsignedInt2101010Reversed);
                case GalTextureFormat.R32:          return (PixelFormat.Red,            PixelType.Float);
                case GalTextureFormat.A1B5G5R5:     return (PixelFormat.Rgba,           PixelType.UnsignedShort5551);
                case GalTextureFormat.B5G6R5:       return (PixelFormat.Rgb,            PixelType.UnsignedShort565);
                case GalTextureFormat.G8R8:         return (PixelFormat.Rg,             PixelType.UnsignedByte);
                case GalTextureFormat.R16:          return (PixelFormat.Red,            PixelType.HalfFloat);
                case GalTextureFormat.R8:           return (PixelFormat.Red,            PixelType.UnsignedByte);
                case GalTextureFormat.ZF32:         return (PixelFormat.DepthComponent, PixelType.Float);
                case GalTextureFormat.BF10GF11RF11: return (PixelFormat.Rgb,            PixelType.UnsignedInt10F11F11FRev);
                case GalTextureFormat.Z24S8:        return (PixelFormat.DepthStencil,   PixelType.UnsignedInt248);
            }

            throw new NotImplementedException(Format.ToString());
        }

        public static InternalFormat GetCompressedTextureFormat(GalTextureFormat Format)
        {
            switch (Format)
            {
                case GalTextureFormat.BC6H_UF16: return InternalFormat.CompressedRgbBptcUnsignedFloat;
                case GalTextureFormat.BC6H_SF16: return InternalFormat.CompressedRgbBptcSignedFloat;
                case GalTextureFormat.BC7U:      return InternalFormat.CompressedRgbaBptcUnorm;
                case GalTextureFormat.BC1:       return InternalFormat.CompressedRgbaS3tcDxt1Ext;
                case GalTextureFormat.BC2:       return InternalFormat.CompressedRgbaS3tcDxt3Ext;
                case GalTextureFormat.BC3:       return InternalFormat.CompressedRgbaS3tcDxt5Ext;
                case GalTextureFormat.BC4:       return InternalFormat.CompressedRedRgtc1;
                case GalTextureFormat.BC5:       return InternalFormat.CompressedRgRgtc2;
            }

            throw new NotImplementedException(Format.ToString());
        }

        public static All GetTextureSwizzle(GalTextureSource Source)
        {
            switch (Source)
            {
                case GalTextureSource.Zero:     return All.Zero;
                case GalTextureSource.Red:      return All.Red;
                case GalTextureSource.Green:    return All.Green;
                case GalTextureSource.Blue:     return All.Blue;
                case GalTextureSource.Alpha:    return All.Alpha;
                case GalTextureSource.OneInt:   return All.One;
                case GalTextureSource.OneFloat: return All.One;
            }

            throw new ArgumentException(nameof(Source));
        }

        public static TextureWrapMode GetTextureWrapMode(GalTextureWrap Wrap)
        {
            switch (Wrap)
            {
                case GalTextureWrap.Repeat:              return TextureWrapMode.Repeat;
                case GalTextureWrap.MirroredRepeat:      return TextureWrapMode.MirroredRepeat;
                case GalTextureWrap.ClampToEdge:         return TextureWrapMode.ClampToEdge;
                case GalTextureWrap.ClampToBorder:       return TextureWrapMode.ClampToBorder;
                case GalTextureWrap.Clamp:               return TextureWrapMode.Clamp;

                //TODO: Those needs extensions (and are currently wrong).
                case GalTextureWrap.MirrorClampToEdge:   return TextureWrapMode.ClampToEdge;
                case GalTextureWrap.MirrorClampToBorder: return TextureWrapMode.ClampToBorder;
                case GalTextureWrap.MirrorClamp:         return TextureWrapMode.Clamp;
            }

            throw new ArgumentException(nameof(Wrap));
        }

        public static TextureMinFilter GetTextureMinFilter(
            GalTextureFilter    MinFilter,
            GalTextureMipFilter MipFilter)
        {
            //TODO: Mip (needs mipmap support first).
            switch (MinFilter)
            {
                case GalTextureFilter.Nearest: return TextureMinFilter.Nearest;
                case GalTextureFilter.Linear:  return TextureMinFilter.Linear;
            }

            throw new ArgumentException(nameof(MinFilter));
        }

        public static TextureMagFilter GetTextureMagFilter(GalTextureFilter Filter)
        {
            switch (Filter)
            {
                case GalTextureFilter.Nearest: return TextureMagFilter.Nearest;
                case GalTextureFilter.Linear:  return TextureMagFilter.Linear;
            }

            throw new ArgumentException(nameof(Filter));
        }

        public static BlendEquationMode GetBlendEquation(GalBlendEquation BlendEquation)
        {
            switch (BlendEquation)
            {
                case GalBlendEquation.FuncAdd:             return BlendEquationMode.FuncAdd;
                case GalBlendEquation.FuncSubtract:        return BlendEquationMode.FuncSubtract;
                case GalBlendEquation.FuncReverseSubtract: return BlendEquationMode.FuncReverseSubtract;
                case GalBlendEquation.Min:                 return BlendEquationMode.Min;
                case GalBlendEquation.Max:                 return BlendEquationMode.Max;
            }

            throw new ArgumentException(nameof(BlendEquation));
        }

        public static BlendingFactor GetBlendFactor(GalBlendFactor BlendFactor)
        {
            switch (BlendFactor)
            {
                case GalBlendFactor.Zero:                  return BlendingFactor.Zero;
                case GalBlendFactor.One:                   return BlendingFactor.One;
                case GalBlendFactor.SrcColor:              return BlendingFactor.SrcColor;
                case GalBlendFactor.OneMinusSrcColor:      return BlendingFactor.OneMinusSrcColor;
                case GalBlendFactor.DstColor:              return BlendingFactor.DstColor;
                case GalBlendFactor.OneMinusDstColor:      return BlendingFactor.OneMinusDstColor;
                case GalBlendFactor.SrcAlpha:              return BlendingFactor.SrcAlpha;
                case GalBlendFactor.OneMinusSrcAlpha:      return BlendingFactor.OneMinusSrcAlpha;
                case GalBlendFactor.DstAlpha:              return BlendingFactor.DstAlpha;
                case GalBlendFactor.OneMinusDstAlpha:      return BlendingFactor.OneMinusDstAlpha;
                case GalBlendFactor.OneMinusConstantColor: return BlendingFactor.OneMinusConstantColor;
                case GalBlendFactor.ConstantAlpha:         return BlendingFactor.ConstantAlpha;
                case GalBlendFactor.OneMinusConstantAlpha: return BlendingFactor.OneMinusConstantAlpha;
                case GalBlendFactor.SrcAlphaSaturate:      return BlendingFactor.SrcAlphaSaturate;
                case GalBlendFactor.Src1Color:             return BlendingFactor.Src1Color;
                case GalBlendFactor.OneMinusSrc1Color:     return (BlendingFactor)BlendingFactorSrc.OneMinusSrc1Color;
                case GalBlendFactor.Src1Alpha:             return BlendingFactor.Src1Alpha;
                case GalBlendFactor.OneMinusSrc1Alpha:     return (BlendingFactor)BlendingFactorSrc.OneMinusSrc1Alpha;

                case GalBlendFactor.ConstantColor:
                case GalBlendFactor.ConstantColorG80:
                    return BlendingFactor.ConstantColor;
            }

            throw new ArgumentException(nameof(BlendFactor));
        }
    }
}
