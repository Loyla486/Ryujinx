using Ryujinx.Graphics.Shader.IntermediateRepresentation;
using System.Collections.Generic;
using System.Diagnostics;

using static Ryujinx.Graphics.Shader.IntermediateRepresentation.OperandHelper;
using static Ryujinx.Graphics.Shader.Translation.GlobalMemory;

namespace Ryujinx.Graphics.Shader.Translation
{
    static class Lowering
    {
        public static void RunPass(BasicBlock[] blocks, ShaderConfig config)
        {
            for (int blkIndex = 0; blkIndex < blocks.Length; blkIndex++)
            {
                BasicBlock block = blocks[blkIndex];

                for (LinkedListNode<INode> node = block.Operations.First; node != null; node = node.Next)
                {
                    if (!(node.Value is Operation operation))
                    {
                        continue;
                    }

                    if (UsesGlobalMemory(operation.Inst))
                    {
                        node = RewriteGlobalAccess(node, config);
                    }

                    if (operation.Inst == Instruction.TextureSample)
                    {
                        node = RewriteTextureSample(node, config);
                    }
                }
            }
        }

        private static LinkedListNode<INode> RewriteGlobalAccess(LinkedListNode<INode> node, ShaderConfig config)
        {
            Operation operation = (Operation)node.Value;

            Operation storageOp;

            Operand PrependOperation(Instruction inst, params Operand[] sources)
            {
                Operand local = Local();

                node.List.AddBefore(node, new Operation(inst, local, sources));

                return local;
            }

            Operand addrLow  = operation.GetSource(0);
            Operand addrHigh = operation.GetSource(1);

            Operand sbBaseAddrLow = Const(0);
            Operand sbSlot        = Const(0);

            for (int slot = 0; slot < StorageMaxCount; slot++)
            {
                int cbOffset = GetStorageCbOffset(config.Stage, slot);

                Operand baseAddrLow  = Cbuf(0, cbOffset);
                Operand baseAddrHigh = Cbuf(0, cbOffset + 1);
                Operand size         = Cbuf(0, cbOffset + 2);

                Operand offset = PrependOperation(Instruction.Subtract,       addrLow, baseAddrLow);
                Operand borrow = PrependOperation(Instruction.CompareLessU32, addrLow, baseAddrLow);

                Operand inRangeLow = PrependOperation(Instruction.CompareLessU32, offset, size);

                Operand addrHighBorrowed = PrependOperation(Instruction.Add, addrHigh, borrow);

                Operand inRangeHigh = PrependOperation(Instruction.CompareEqual, addrHighBorrowed, baseAddrHigh);

                Operand inRange = PrependOperation(Instruction.BitwiseAnd, inRangeLow, inRangeHigh);

                sbBaseAddrLow = PrependOperation(Instruction.ConditionalSelect, inRange, baseAddrLow, sbBaseAddrLow);
                sbSlot        = PrependOperation(Instruction.ConditionalSelect, inRange, Const(slot), sbSlot);
            }

            Operand alignMask = Const(-config.QueryInfo(QueryInfoName.StorageBufferOffsetAlignment));

            Operand baseAddrTrunc = PrependOperation(Instruction.BitwiseAnd,    sbBaseAddrLow, Const(-64));
            Operand byteOffset    = PrependOperation(Instruction.Subtract,      addrLow, baseAddrTrunc);
            Operand wordOffset    = PrependOperation(Instruction.ShiftRightU32, byteOffset, Const(2));

            Operand[] sources = new Operand[operation.SourcesCount];

            sources[0] = sbSlot;
            sources[1] = wordOffset;

            for (int index = 2; index < operation.SourcesCount; index++)
            {
                sources[index] = operation.GetSource(index);
            }

            if (operation.Inst.IsAtomic())
            {
                Instruction inst = (operation.Inst & ~Instruction.MrMask) | Instruction.MrStorage;

                storageOp = new Operation(inst, operation.Dest, sources);
            }
            else if (operation.Inst == Instruction.LoadGlobal)
            {
                storageOp = new Operation(Instruction.LoadStorage, operation.Dest, sources);
            }
            else
            {
                storageOp = new Operation(Instruction.StoreStorage, null, sources);
            }

            for (int index = 0; index < operation.SourcesCount; index++)
            {
                operation.SetSource(index, null);
            }

            LinkedListNode<INode> oldNode = node;

            node = node.List.AddBefore(node, storageOp);

            node.List.Remove(oldNode);

            return node;
        }

        private static LinkedListNode<INode> RewriteTextureSample(LinkedListNode<INode> node, ShaderConfig config)
        {
            TextureOperation texOp = (TextureOperation)node.Value;

            bool hasOffset  = (texOp.Flags & TextureFlags.Offset)  != 0;
            bool hasOffsets = (texOp.Flags & TextureFlags.Offsets) != 0;

            bool hasInvalidOffset = (hasOffset || hasOffsets) && !config.QueryInfoBool(QueryInfoName.SupportsNonConstantTextureOffset);

            bool isRect = config.QueryInfoBool(QueryInfoName.IsTextureRectangle, texOp.Handle);

            if (!(hasInvalidOffset || isRect))
            {
                return node;
            }

            bool isBindless     = (texOp.Flags & TextureFlags.Bindless)    != 0;
            bool isGather       = (texOp.Flags & TextureFlags.Gather)      != 0;
            bool hasDerivatives = (texOp.Flags & TextureFlags.Derivatives) != 0;
            bool intCoords      = (texOp.Flags & TextureFlags.IntCoords)   != 0;
            bool hasLodBias     = (texOp.Flags & TextureFlags.LodBias)     != 0;
            bool hasLodLevel    = (texOp.Flags & TextureFlags.LodLevel)    != 0;

            bool isArray       = (texOp.Type & SamplerType.Array)       != 0;
            bool isIndexed     = (texOp.Type & SamplerType.Indexed)     != 0;
            bool isMultisample = (texOp.Type & SamplerType.Multisample) != 0;
            bool isShadow      = (texOp.Type & SamplerType.Shadow)      != 0;

            int coordsCount = texOp.Type.GetDimensions();

            int offsetsCount;

            if (hasOffsets)
            {
                offsetsCount = coordsCount * 4;
            }
            else if (hasOffset)
            {
                offsetsCount = coordsCount;
            }
            else
            {
                offsetsCount = 0;
            }

            Operand[] offsets = new Operand[offsetsCount];
            Operand[] sources = new Operand[texOp.SourcesCount - offsetsCount];

            int copyCount = 0;

            if (isBindless || isIndexed)
            {
                copyCount++;
            }

            Operand[] lodSources = new Operand[copyCount + coordsCount];

            for (int index = 0; index < lodSources.Length; index++)
            {
                lodSources[index] = texOp.GetSource(index);
            }

            copyCount += coordsCount;

            if (isArray)
            {
                copyCount++;
            }

            if (isShadow)
            {
                copyCount++;
            }

            if (hasDerivatives)
            {
                copyCount += coordsCount * 2;
            }

            if (isMultisample)
            {
                copyCount++;
            }
            else if (hasLodLevel)
            {
                copyCount++;
            }

            int srcIndex = 0;
            int dstIndex = 0;

            for (int index = 0; index < copyCount; index++)
            {
                sources[dstIndex++] = texOp.GetSource(srcIndex++);
            }

            bool areAllOffsetsConstant = true;

            for (int index = 0; index < offsetsCount; index++)
            {
                Operand offset = texOp.GetSource(srcIndex++);

                areAllOffsetsConstant &= offset.Type == OperandType.Constant;

                offsets[index] = offset;
            }

            hasInvalidOffset &= !areAllOffsetsConstant;

            if (!(hasInvalidOffset || isRect))
            {
                return node;
            }

            if (hasLodBias)
            {
               sources[dstIndex++] = texOp.GetSource(srcIndex++);
            }

            if (isGather && !isShadow)
            {
               sources[dstIndex++] = texOp.GetSource(srcIndex++);
            }

            int coordsIndex = isBindless || isIndexed ? 1 : 0;

            int componentIndex = texOp.Index;

            Operand Int(Operand value)
            {
                Operand res = Local();

                node.List.AddBefore(node, new Operation(Instruction.ConvertFPToS32, res, value));

                return res;
            }

            Operand Float(Operand value)
            {
                Operand res = Local();

                node.List.AddBefore(node, new Operation(Instruction.ConvertS32ToFP, res, value));

                return res;
            }

            // Emulate texture rectangle by normalizing the coordinates on the shader.
            // When sampler*Rect is used, the coords are expected to the in the [0, W or H] range,
            // and otherwise, it is expected to be in the [0, 1] range.
            // We normalize by dividing the coords by the texture size.
            if (isRect && !intCoords)
            {
                for (int index = 0; index < coordsCount; index++)
                {
                    Operand coordSize = Local();

                    Operand[] texSizeSources;

                    if (isBindless || isIndexed)
                    {
                        texSizeSources = new Operand[] { sources[0], Const(0) };
                    }
                    else
                    {
                        texSizeSources = new Operand[] { Const(0) };
                    }

                    node.List.AddBefore(node, new TextureOperation(
                        Instruction.TextureSize,
                        texOp.Type,
                        texOp.Flags,
                        texOp.Handle,
                        index,
                        coordSize,
                        texSizeSources));

                    Operand source = sources[coordsIndex + index];

                    Operand coordNormalized = Local();

                    node.List.AddBefore(node, new Operation(Instruction.FP | Instruction.Divide, coordNormalized, source, Float(coordSize)));

                    sources[coordsIndex + index] = coordNormalized;
                }
            }

            // Technically, non-constant texture offsets are not allowed (according to the spec),
            // however some GPUs does support that.
            // For GPUs where it is not supported, we can replace the instruction with the following:
            // For texture*Offset, we replace it by texture*, and add the offset to the P coords.
            // The offset can be calculated as offset / textureSize(lod), where lod = textureQueryLod(coords).
            // For texelFetchOffset, we replace it by texelFetch and add the offset to the P coords directly.
            // For textureGatherOffset, we take advantage of the fact that the operation is already broken down
            // to read the 4 pixels separately, and just replace it with 4 textureGather with a different offset
            // for each pixel.
            if (hasInvalidOffset)
            {
                if (intCoords)
                {
                    for (int index = 0; index < coordsCount; index++)
                    {
                        Operand source = sources[coordsIndex + index];

                        Operand coordPlusOffset = Local();

                        node.List.AddBefore(node, new Operation(Instruction.Add, coordPlusOffset, source, offsets[index]));

                        sources[coordsIndex + index] = coordPlusOffset;
                    }
                }
                else
                {
                    Operand lod = Local();

                    node.List.AddBefore(node, new TextureOperation(
                        Instruction.Lod,
                        texOp.Type,
                        texOp.Flags,
                        texOp.Handle,
                        1,
                        lod,
                        lodSources));

                    for (int index = 0; index < coordsCount; index++)
                    {
                        Operand coordSize = Local();

                        Operand[] texSizeSources;

                        if (isBindless || isIndexed)
                        {
                            texSizeSources = new Operand[] { sources[0], Int(lod) };
                        }
                        else
                        {
                            texSizeSources = new Operand[] { Int(lod) };
                        }

                        node.List.AddBefore(node, new TextureOperation(
                            Instruction.TextureSize,
                            texOp.Type,
                            texOp.Flags,
                            texOp.Handle,
                            index,
                            coordSize,
                            texSizeSources));

                        Operand offset = Local();

                        Operand intOffset = offsets[index + (hasOffsets ? texOp.Index * coordsCount : 0)];

                        node.List.AddBefore(node, new Operation(Instruction.FP | Instruction.Divide, offset, Float(intOffset), Float(coordSize)));

                        Operand source = sources[coordsIndex + index];

                        Operand coordPlusOffset = Local();

                        node.List.AddBefore(node, new Operation(Instruction.FP | Instruction.Add, coordPlusOffset, source, offset));

                        sources[coordsIndex + index] = coordPlusOffset;
                    }
                }

                if (isGather && !isShadow)
                {
                    Operand gatherComponent = sources[dstIndex - 1];

                    Debug.Assert(gatherComponent.Type == OperandType.Constant);

                    componentIndex = gatherComponent.Value;
                }
            }

            TextureOperation newTexOp = new TextureOperation(
                Instruction.TextureSample,
                texOp.Type,
                texOp.Flags & ~(TextureFlags.Offset | TextureFlags.Offsets),
                texOp.Handle,
                componentIndex,
                texOp.Dest,
                sources);

            for (int index = 0; index < texOp.SourcesCount; index++)
            {
                texOp.SetSource(index, null);
            }

            LinkedListNode<INode> oldNode = node;

            node = node.List.AddBefore(node, newTexOp);

            node.List.Remove(oldNode);

            return node;
        }
    }
}