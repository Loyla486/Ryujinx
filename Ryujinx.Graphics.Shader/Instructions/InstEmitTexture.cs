using Ryujinx.Graphics.Shader.Decoders;
using Ryujinx.Graphics.Shader.IntermediateRepresentation;
using Ryujinx.Graphics.Shader.Translation;
using System;
using System.Collections.Generic;

using static Ryujinx.Graphics.Shader.IntermediateRepresentation.OperandHelper;

namespace Ryujinx.Graphics.Shader.Instructions
{
    static partial class InstEmit
    {
        public static void Sust(EmitterContext context)
        {
            OpCodeImage op = (OpCodeImage)context.CurrOp;

            SamplerType type = ConvertSamplerType(op.Dimensions);

            if (type == SamplerType.None)
            {
                context.Config.PrintLog("Invalid image store sampler type.");

                return;
            }

            int raIndex = op.Ra.Index;
            int rbIndex = op.Rb.Index;

            Operand Ra()
            {
                if (raIndex > RegisterConsts.RegisterZeroIndex)
                {
                    return Const(0);
                }

                return context.Copy(Register(raIndex++, RegisterType.Gpr));
            }

            Operand Rb()
            {
                if (rbIndex > RegisterConsts.RegisterZeroIndex)
                {
                    return Const(0);
                }

                return context.Copy(Register(rbIndex++, RegisterType.Gpr));
            }

            bool isArray = op.Dimensions == ImageDimensions.Image1DArray ||
                           op.Dimensions == ImageDimensions.Image2DArray;

            Operand arrayIndex = isArray ? Ra() : null;

            List<Operand> sourcesList = new List<Operand>();

            if (op.IsBindless)
            {
                sourcesList.Add(context.Copy(Register(op.Rc)));
            }

            int coordsCount = type.GetDimensions();

            for (int index = 0; index < coordsCount; index++)
            {
                sourcesList.Add(Ra());
            }

            if (isArray)
            {
                sourcesList.Add(arrayIndex);

                type |= SamplerType.Array;
            }

            if (op.UseComponents)
            {
                int componentMask = (int)op.Components;

                for (int compMask = componentMask, compIndex = 0; compMask != 0; compMask >>= 1, compIndex++)
                {
                    if ((compMask & 1) != 0)
                    {
                        sourcesList.Add(Rb());
                    }
                }
            }
            else
            {
                context.Config.PrintLog("Unsized image store not supported.");
            }

            Operand[] sources = sourcesList.ToArray();

            int handle = !op.IsBindless ? op.Immediate : 0;

            TextureFlags flags = op.IsBindless ? TextureFlags.Bindless : TextureFlags.None;

            TextureOperation operation = new TextureOperation(
                Instruction.ImageStore,
                type,
                flags,
                handle,
                0,
                null,
                sources);

            context.Add(operation);
        }

        public static void Tex(EmitterContext context)
        {
            EmitTextureSample(context, TextureFlags.None);
        }

        public static void TexB(EmitterContext context)
        {
            EmitTextureSample(context, TextureFlags.Bindless);
        }

        public static void Tld(EmitterContext context)
        {
            EmitTextureSample(context, TextureFlags.IntCoords);
        }

        public static void TldB(EmitterContext context)
        {
            EmitTextureSample(context, TextureFlags.IntCoords | TextureFlags.Bindless);
        }

        public static void Texs(EmitterContext context)
        {
            OpCodeTextureScalar op = (OpCodeTextureScalar)context.CurrOp;

            if (op.Rd0.IsRZ && op.Rd1.IsRZ)
            {
                return;
            }

            List<Operand> sourcesList = new List<Operand>();

            int raIndex = op.Ra.Index;
            int rbIndex = op.Rb.Index;

            Operand Ra()
            {
                if (raIndex > RegisterConsts.RegisterZeroIndex)
                {
                    return Const(0);
                }

                return context.Copy(Register(raIndex++, RegisterType.Gpr));
            }

            Operand Rb()
            {
                if (rbIndex > RegisterConsts.RegisterZeroIndex)
                {
                    return Const(0);
                }

                return context.Copy(Register(rbIndex++, RegisterType.Gpr));
            }

            void AddTextureOffset(int coordsCount, int stride, int size)
            {
                Operand packedOffs = Rb();

                for (int index = 0; index < coordsCount; index++)
                {
                    sourcesList.Add(context.BitfieldExtractS32(packedOffs, Const(index * stride), Const(size)));
                }
            }

            SamplerType  type;
            TextureFlags flags;

            if (op is OpCodeTexs texsOp)
            {
                type = ConvertSamplerType(texsOp.Target);

                if (type == SamplerType.None)
                {
                    context.Config.PrintLog("Invalid texture sampler type.");

                    return;
                }

                flags = ConvertTextureFlags(texsOp.Target);

                if ((type & SamplerType.Array) != 0)
                {
                    Operand arrayIndex = Ra();

                    sourcesList.Add(Ra());
                    sourcesList.Add(Rb());

                    sourcesList.Add(arrayIndex);

                    if ((type & SamplerType.Shadow) != 0)
                    {
                        sourcesList.Add(Rb());
                    }

                    if ((flags & TextureFlags.LodLevel) != 0)
                    {
                        sourcesList.Add(ConstF(0));
                    }
                }
                else
                {
                    switch (texsOp.Target)
                    {
                        case TextureTarget.Texture1DLodZero:
                            sourcesList.Add(Ra());
                            break;

                        case TextureTarget.Texture2D:
                            sourcesList.Add(Ra());
                            sourcesList.Add(Rb());
                            break;

                        case TextureTarget.Texture2DLodZero:
                            sourcesList.Add(Ra());
                            sourcesList.Add(Rb());
                            sourcesList.Add(ConstF(0));
                            break;

                        case TextureTarget.Texture2DLodLevel:
                        case TextureTarget.Texture2DDepthCompare:
                        case TextureTarget.Texture3D:
                        case TextureTarget.TextureCube:
                            sourcesList.Add(Ra());
                            sourcesList.Add(Ra());
                            sourcesList.Add(Rb());
                            break;

                        case TextureTarget.Texture2DLodZeroDepthCompare:
                        case TextureTarget.Texture3DLodZero:
                            sourcesList.Add(Ra());
                            sourcesList.Add(Ra());
                            sourcesList.Add(Rb());
                            sourcesList.Add(ConstF(0));
                            break;

                        case TextureTarget.Texture2DLodLevelDepthCompare:
                        case TextureTarget.TextureCubeLodLevel:
                            sourcesList.Add(Ra());
                            sourcesList.Add(Ra());
                            sourcesList.Add(Rb());
                            sourcesList.Add(Rb());
                            break;
                    }
                }
            }
            else if (op is OpCodeTlds tldsOp)
            {
                type = ConvertSamplerType (tldsOp.Target);

                if (type == SamplerType.None)
                {
                    context.Config.PrintLog("Invalid texel fetch sampler type.");

                    return;
                }

                flags = ConvertTextureFlags(tldsOp.Target) | TextureFlags.IntCoords;

                switch (tldsOp.Target)
                {
                    case TexelLoadTarget.Texture1DLodZero:
                        sourcesList.Add(Ra());
                        sourcesList.Add(Const(0));
                        break;

                    case TexelLoadTarget.Texture1DLodLevel:
                        sourcesList.Add(Ra());
                        sourcesList.Add(Rb());
                        break;

                    case TexelLoadTarget.Texture2DLodZero:
                        sourcesList.Add(Ra());
                        sourcesList.Add(Rb());
                        sourcesList.Add(Const(0));
                        break;

                    case TexelLoadTarget.Texture2DLodZeroOffset:
                        sourcesList.Add(Ra());
                        sourcesList.Add(Ra());
                        sourcesList.Add(Const(0));
                        break;

                    case TexelLoadTarget.Texture2DLodZeroMultisample:
                    case TexelLoadTarget.Texture2DLodLevel:
                    case TexelLoadTarget.Texture2DLodLevelOffset:
                        sourcesList.Add(Ra());
                        sourcesList.Add(Ra());
                        sourcesList.Add(Rb());
                        break;

                    case TexelLoadTarget.Texture3DLodZero:
                        sourcesList.Add(Ra());
                        sourcesList.Add(Ra());
                        sourcesList.Add(Rb());
                        sourcesList.Add(Const(0));
                        break;

                    case TexelLoadTarget.Texture2DArrayLodZero:
                        sourcesList.Add(Rb());
                        sourcesList.Add(Rb());
                        sourcesList.Add(Ra());
                        sourcesList.Add(Const(0));
                        break;
                }

                if ((flags & TextureFlags.Offset) != 0)
                {
                    AddTextureOffset(type.GetDimensions(), 4, 4);
                }
            }
            else if (op is OpCodeTld4s tld4sOp)
            {
                if (!(tld4sOp.HasDepthCompare || tld4sOp.HasOffset))
                {
                    sourcesList.Add(Ra());
                    sourcesList.Add(Rb());
                }
                else
                {
                    sourcesList.Add(Ra());
                    sourcesList.Add(Ra());
                }

                type  = SamplerType.Texture2D;
                flags = TextureFlags.Gather;

                if (tld4sOp.HasDepthCompare)
                {
                    sourcesList.Add(Rb());

                    type |= SamplerType.Shadow;
                }

                if (tld4sOp.HasOffset)
                {
                    AddTextureOffset(type.GetDimensions(), 8, 6);

                    flags |= TextureFlags.Offset;
                }

                sourcesList.Add(Const(tld4sOp.GatherCompIndex));
            }
            else
            {
                throw new InvalidOperationException($"Invalid opcode type \"{op.GetType().Name}\".");
            }

            Operand[] sources = sourcesList.ToArray();

            Operand[] rd0 = new Operand[2] { ConstF(0), ConstF(0) };
            Operand[] rd1 = new Operand[2] { ConstF(0), ConstF(0) };

            int destIncrement = 0;

            Operand GetDest()
            {
                int high = destIncrement >> 1;
                int low  = destIncrement &  1;

                destIncrement++;

                if (op.IsFp16)
                {
                    return high != 0
                        ? (rd1[low] = Local())
                        : (rd0[low] = Local());
                }
                else
                {
                    int rdIndex = high != 0 ? op.Rd1.Index : op.Rd0.Index;

                    if (rdIndex < RegisterConsts.RegisterZeroIndex)
                    {
                        rdIndex += low;
                    }

                    return Register(rdIndex, RegisterType.Gpr);
                }
            }

            int handle = op.Immediate;

            for (int compMask = op.ComponentMask, compIndex = 0; compMask != 0; compMask >>= 1, compIndex++)
            {
                if ((compMask & 1) != 0)
                {
                    Operand dest = GetDest();

                    TextureOperation operation = new TextureOperation(
                        Instruction.TextureSample,
                        type,
                        flags,
                        handle,
                        compIndex,
                        dest,
                        sources);

                    context.Add(operation);
                }
            }

            if (op.IsFp16)
            {
                context.Copy(Register(op.Rd0), context.PackHalf2x16(rd0[0], rd0[1]));
                context.Copy(Register(op.Rd1), context.PackHalf2x16(rd1[0], rd1[1]));
            }
        }

        public static void Tld4(EmitterContext context)
        {
            IOpCodeTld4 op = (IOpCodeTld4)context.CurrOp;

            if (op.Rd.IsRZ)
            {
                return;
            }

            int raIndex = op.Ra.Index;
            int rbIndex = op.Rb.Index;

            Operand Ra()
            {
                if (raIndex > RegisterConsts.RegisterZeroIndex)
                {
                    return Const(0);
                }

                return context.Copy(Register(raIndex++, RegisterType.Gpr));
            }

            Operand Rb()
            {
                if (rbIndex > RegisterConsts.RegisterZeroIndex)
                {
                    return Const(0);
                }

                return context.Copy(Register(rbIndex++, RegisterType.Gpr));
            }

            Operand arrayIndex = op.IsArray ? Ra() : null;

            List<Operand> sourcesList = new List<Operand>();

            SamplerType type = ConvertSamplerType(op.Dimensions);

            TextureFlags flags = TextureFlags.Gather;

            if (op.Bindless)
            {
                sourcesList.Add(Rb());

                flags |= TextureFlags.Bindless;
            }

            int coordsCount = type.GetDimensions();

            for (int index = 0; index < coordsCount; index++)
            {
                sourcesList.Add(Ra());
            }

            if (op.IsArray)
            {
                sourcesList.Add(arrayIndex);

                type |= SamplerType.Array;
            }

            Operand[] packedOffs = new Operand[2];

            packedOffs[0] = op.Offset != TextureGatherOffset.None    ? Rb() : null;
            packedOffs[1] = op.Offset == TextureGatherOffset.Offsets ? Rb() : null;

            if (op.HasDepthCompare)
            {
                sourcesList.Add(Rb());

                type |= SamplerType.Shadow;
            }

            if (op.Offset != TextureGatherOffset.None)
            {
                int offsetTexelsCount = op.Offset == TextureGatherOffset.Offsets ? 4 : 1;

                for (int index = 0; index < coordsCount * offsetTexelsCount; index++)
                {
                    Operand packed = packedOffs[(index >> 2) & 1];

                    sourcesList.Add(context.BitfieldExtractS32(packed, Const((index & 3) * 8), Const(6)));
                }

                flags |= op.Offset == TextureGatherOffset.Offsets
                    ? TextureFlags.Offsets
                    : TextureFlags.Offset;
            }

            sourcesList.Add(Const(op.GatherCompIndex));

            Operand[] sources = sourcesList.ToArray();

            int rdIndex = op.Rd.Index;

            Operand GetDest()
            {
                if (rdIndex > RegisterConsts.RegisterZeroIndex)
                {
                    return Const(0);
                }

                return Register(rdIndex++, RegisterType.Gpr);
            }

            int handle = op.Immediate;

            for (int compMask = op.ComponentMask, compIndex = 0; compMask != 0; compMask >>= 1, compIndex++)
            {
                if ((compMask & 1) != 0)
                {
                    Operand dest = GetDest();

                    TextureOperation operation = new TextureOperation(
                        Instruction.TextureSample,
                        type,
                        flags,
                        handle,
                        compIndex,
                        dest,
                        sources);

                    context.Add(operation);
                }
            }
        }

        public static void Txd(EmitterContext context)
        {
            OpCodeTxd op = (OpCodeTxd)context.CurrOp;

            if (op.Rd.IsRZ)
            {
                return;
            }

            int raIndex = op.Ra.Index;
            int rbIndex = op.Rb.Index;

            Operand Ra()
            {
                if (raIndex > RegisterConsts.RegisterZeroIndex)
                {
                    return Const(0);
                }

                return context.Copy(Register(raIndex++, RegisterType.Gpr));
            }

            Operand Rb()
            {
                if (rbIndex > RegisterConsts.RegisterZeroIndex)
                {
                    return Const(0);
                }

                return context.Copy(Register(rbIndex++, RegisterType.Gpr));
            }

            TextureFlags flags = TextureFlags.Derivatives;

            List<Operand> sourcesList = new List<Operand>();

            if (op.IsBindless)
            {
                sourcesList.Add(Ra());
            }

            SamplerType type = ConvertSamplerType(op.Dimensions);

            int coordsCount = type.GetDimensions();

            for (int index = 0; index < coordsCount; index++)
            {
                sourcesList.Add(Ra());
            }

            Operand packedParams = Ra();

            if (op.IsArray)
            {
                sourcesList.Add(context.BitwiseAnd(packedParams, Const(0xffff)));

                type |= SamplerType.Array;
            }

            // Derivatives (X and Y).
            for (int dIndex = 0; dIndex < 2 * coordsCount; dIndex++)
            {
                sourcesList.Add(Rb());
            }

            if (op.HasOffset)
            {
                for (int index = 0; index < coordsCount; index++)
                {
                    sourcesList.Add(context.BitfieldExtractS32(packedParams, Const(16 + index * 4), Const(4)));
                }

                flags |= TextureFlags.Offset;
            }

            Operand[] sources = sourcesList.ToArray();

            int rdIndex = op.Rd.Index;

            Operand GetDest()
            {
                if (rdIndex > RegisterConsts.RegisterZeroIndex)
                {
                    return Const(0);
                }

                return Register(rdIndex++, RegisterType.Gpr);
            }

            int handle = !op.IsBindless ? op.Immediate : 0;

            for (int compMask = op.ComponentMask, compIndex = 0; compMask != 0; compMask >>= 1, compIndex++)
            {
                if ((compMask & 1) != 0)
                {
                    Operand dest = GetDest();

                    TextureOperation operation = new TextureOperation(
                        Instruction.TextureSample,
                        type,
                        flags,
                        handle,
                        compIndex,
                        dest,
                        sources);

                    context.Add(operation);
                }
            }
        }

        public static void Txq(EmitterContext context)
        {
            EmitTextureQuery(context, bindless: false);
        }

        public static void TxqB(EmitterContext context)
        {
            EmitTextureQuery(context, bindless: true);
        }

        private static void EmitTextureQuery(EmitterContext context, bool bindless)
        {
            OpCodeTex op = (OpCodeTex)context.CurrOp;

            if (op.Rd.IsRZ)
            {
                return;
            }

            TextureProperty property = (TextureProperty)op.RawOpCode.Extract(22, 6);

            // TODO: Validate and use property.
            Instruction inst = Instruction.TextureSize;

            SamplerType type = SamplerType.Texture2D;

            TextureFlags flags = bindless ? TextureFlags.Bindless : TextureFlags.None;

            int raIndex = op.Ra.Index;

            Operand Ra()
            {
                if (raIndex > RegisterConsts.RegisterZeroIndex)
                {
                    return Const(0);
                }

                return context.Copy(Register(raIndex++, RegisterType.Gpr));
            }

            List<Operand> sourcesList = new List<Operand>();

            if (bindless)
            {
                sourcesList.Add(Ra());
            }

            sourcesList.Add(Ra());

            Operand[] sources = sourcesList.ToArray();

            int rdIndex = op.Rd.Index;

            Operand GetDest()
            {
                if (rdIndex > RegisterConsts.RegisterZeroIndex)
                {
                    return Const(0);
                }

                return Register(rdIndex++, RegisterType.Gpr);
            }

            int handle = !bindless ? op.Immediate : 0;

            for (int compMask = op.ComponentMask, compIndex = 0; compMask != 0; compMask >>= 1, compIndex++)
            {
                if ((compMask & 1) != 0)
                {
                    Operand dest = GetDest();

                    TextureOperation operation = new TextureOperation(
                        inst,
                        type,
                        flags,
                        handle,
                        compIndex,
                        dest,
                        sources);

                    context.Add(operation);
                }
            }
        }

        private static void EmitTextureSample(EmitterContext context, TextureFlags flags)
        {
            OpCodeTexture op = (OpCodeTexture)context.CurrOp;

            bool isBindless = (flags & TextureFlags.Bindless) != 0;

            if (op.Rd.IsRZ)
            {
                return;
            }

            int raIndex = op.Ra.Index;
            int rbIndex = op.Rb.Index;

            Operand Ra()
            {
                if (raIndex > RegisterConsts.RegisterZeroIndex)
                {
                    return Const(0);
                }

                return context.Copy(Register(raIndex++, RegisterType.Gpr));
            }

            Operand Rb()
            {
                if (rbIndex > RegisterConsts.RegisterZeroIndex)
                {
                    return Const(0);
                }

                return context.Copy(Register(rbIndex++, RegisterType.Gpr));
            }

            Operand arrayIndex = op.IsArray ? Ra() : null;

            List<Operand> sourcesList = new List<Operand>();

            if (isBindless)
            {
                sourcesList.Add(Rb());
            }

            SamplerType type = ConvertSamplerType(op.Dimensions);

            int coordsCount = type.GetDimensions();

            for (int index = 0; index < coordsCount; index++)
            {
                sourcesList.Add(Ra());
            }

            if (op.IsArray)
            {
                sourcesList.Add(arrayIndex);

                type |= SamplerType.Array;
            }

            bool hasLod = op.LodMode > TextureLodMode.LodZero;

            Operand lodValue = hasLod ? Rb() : ConstF(0);

            Operand packedOffs = op.HasOffset ? Rb() : null;

            if (op.HasDepthCompare)
            {
                sourcesList.Add(Rb());

                type |= SamplerType.Shadow;
            }

            if ((op.LodMode == TextureLodMode.LodZero  ||
                 op.LodMode == TextureLodMode.LodLevel ||
                 op.LodMode == TextureLodMode.LodLevelA) && !op.IsMultisample)
            {
                sourcesList.Add(lodValue);

                flags |= TextureFlags.LodLevel;
            }

            if (op.HasOffset)
            {
                for (int index = 0; index < coordsCount; index++)
                {
                    sourcesList.Add(context.BitfieldExtractS32(packedOffs, Const(index * 4), Const(4)));
                }

                flags |= TextureFlags.Offset;
            }

            if (op.LodMode == TextureLodMode.LodBias ||
                op.LodMode == TextureLodMode.LodBiasA)
            {
                sourcesList.Add(lodValue);

                flags |= TextureFlags.LodBias;
            }

            if (op.IsMultisample)
            {
                sourcesList.Add(Rb());

                type |= SamplerType.Multisample;
            }

            Operand[] sources = sourcesList.ToArray();

            int rdIndex = op.Rd.Index;

            Operand GetDest()
            {
                if (rdIndex > RegisterConsts.RegisterZeroIndex)
                {
                    return Const(0);
                }

                return Register(rdIndex++, RegisterType.Gpr);
            }

            int handle = !isBindless ? op.Immediate : 0;

            for (int compMask = op.ComponentMask, compIndex = 0; compMask != 0; compMask >>= 1, compIndex++)
            {
                if ((compMask & 1) != 0)
                {
                    Operand dest = GetDest();

                    TextureOperation operation = new TextureOperation(
                        Instruction.TextureSample,
                        type,
                        flags,
                        handle,
                        compIndex,
                        dest,
                        sources);

                    context.Add(operation);
                }
            }
        }

        private static SamplerType ConvertSamplerType(ImageDimensions target)
        {
            switch (target)
            {
                case ImageDimensions.Image1D:
                    return SamplerType.Texture1D;

                case ImageDimensions.ImageBuffer:
                    return SamplerType.TextureBuffer;

                case ImageDimensions.Image1DArray:
                    return SamplerType.Texture1D | SamplerType.Array;

                case ImageDimensions.Image2D:
                    return SamplerType.Texture2D;

                case ImageDimensions.Image2DArray:
                    return SamplerType.Texture2D | SamplerType.Array;

                case ImageDimensions.Image3D:
                    return SamplerType.Texture3D;
            }

            return SamplerType.None;
        }

        private static SamplerType ConvertSamplerType(TextureDimensions dimensions)
        {
            switch (dimensions)
            {
                case TextureDimensions.Texture1D:   return SamplerType.Texture1D;
                case TextureDimensions.Texture2D:   return SamplerType.Texture2D;
                case TextureDimensions.Texture3D:   return SamplerType.Texture3D;
                case TextureDimensions.TextureCube: return SamplerType.TextureCube;
            }

            throw new ArgumentException($"Invalid texture dimensions \"{dimensions}\".");
        }

        private static SamplerType ConvertSamplerType(TextureTarget type)
        {
            switch (type)
            {
                case TextureTarget.Texture1DLodZero:
                    return SamplerType.Texture1D;

                case TextureTarget.Texture2D:
                case TextureTarget.Texture2DLodZero:
                case TextureTarget.Texture2DLodLevel:
                    return SamplerType.Texture2D;

                case TextureTarget.Texture2DDepthCompare:
                case TextureTarget.Texture2DLodLevelDepthCompare:
                case TextureTarget.Texture2DLodZeroDepthCompare:
                    return SamplerType.Texture2D | SamplerType.Shadow;

                case TextureTarget.Texture2DArray:
                case TextureTarget.Texture2DArrayLodZero:
                    return SamplerType.Texture2D | SamplerType.Array;

                case TextureTarget.Texture2DArrayLodZeroDepthCompare:
                    return SamplerType.Texture2D | SamplerType.Array | SamplerType.Shadow;

                case TextureTarget.Texture3D:
                case TextureTarget.Texture3DLodZero:
                    return SamplerType.Texture3D;

                case TextureTarget.TextureCube:
                case TextureTarget.TextureCubeLodLevel:
                    return SamplerType.TextureCube;
            }

            return SamplerType.None;
        }

        private static SamplerType ConvertSamplerType(TexelLoadTarget type)
        {
            switch (type)
            {
                case TexelLoadTarget.Texture1DLodZero:
                case TexelLoadTarget.Texture1DLodLevel:
                    return SamplerType.Texture1D;

                case TexelLoadTarget.Texture2DLodZero:
                case TexelLoadTarget.Texture2DLodZeroOffset:
                case TexelLoadTarget.Texture2DLodLevel:
                case TexelLoadTarget.Texture2DLodLevelOffset:
                    return SamplerType.Texture2D;

                case TexelLoadTarget.Texture2DLodZeroMultisample:
                    return SamplerType.Texture2D | SamplerType.Multisample;

                case TexelLoadTarget.Texture3DLodZero:
                    return SamplerType.Texture3D;

                case TexelLoadTarget.Texture2DArrayLodZero:
                    return SamplerType.Texture2D | SamplerType.Array;
            }

            return SamplerType.None;
        }

        private static TextureFlags ConvertTextureFlags(Decoders.TextureTarget type)
        {
            switch (type)
            {
                case TextureTarget.Texture1DLodZero:
                case TextureTarget.Texture2DLodZero:
                case TextureTarget.Texture2DLodLevel:
                case TextureTarget.Texture2DLodLevelDepthCompare:
                case TextureTarget.Texture2DLodZeroDepthCompare:
                case TextureTarget.Texture2DArrayLodZero:
                case TextureTarget.Texture2DArrayLodZeroDepthCompare:
                case TextureTarget.Texture3DLodZero:
                case TextureTarget.TextureCubeLodLevel:
                    return TextureFlags.LodLevel;

                case TextureTarget.Texture2D:
                case TextureTarget.Texture2DDepthCompare:
                case TextureTarget.Texture2DArray:
                case TextureTarget.Texture3D:
                case TextureTarget.TextureCube:
                    return TextureFlags.None;
            }

            return TextureFlags.None;
        }

        private static TextureFlags ConvertTextureFlags(TexelLoadTarget type)
        {
            switch (type)
            {
                case TexelLoadTarget.Texture1DLodZero:
                case TexelLoadTarget.Texture1DLodLevel:
                case TexelLoadTarget.Texture2DLodZero:
                case TexelLoadTarget.Texture2DLodLevel:
                case TexelLoadTarget.Texture2DLodZeroMultisample:
                case TexelLoadTarget.Texture3DLodZero:
                case TexelLoadTarget.Texture2DArrayLodZero:
                    return TextureFlags.LodLevel;

                case TexelLoadTarget.Texture2DLodZeroOffset:
                case TexelLoadTarget.Texture2DLodLevelOffset:
                    return TextureFlags.LodLevel | TextureFlags.Offset;
            }

            return TextureFlags.None;
        }
    }
}