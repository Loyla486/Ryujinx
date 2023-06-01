using Ryujinx.Graphics.Shader.IntermediateRepresentation;
using System;
using System.Collections.Generic;
using System.Linq;

using static Ryujinx.Graphics.Shader.IntermediateRepresentation.OperandHelper;

namespace Ryujinx.Graphics.Shader.Translation.Optimizations
{
    static class GlobalToStorage
    {
        private const int DriverReservedCb = 0;

        private class GtsContext
        {
            private struct Entry
            {
                public readonly int FunctionId;
                public readonly Instruction Inst;
                public readonly StorageKind StorageKind;
                public readonly bool IsMultiTarget;
                public readonly IReadOnlyList<uint> TargetCbs;

                public Entry(
                    int functionId,
                    Instruction inst,
                    StorageKind storageKind,
                    bool isMultiTarget,
                    IReadOnlyList<uint> targetCbs)
                {
                    FunctionId = functionId;
                    Inst = inst;
                    StorageKind = storageKind;
                    IsMultiTarget = isMultiTarget;
                    TargetCbs = targetCbs;
                }
            }

            private readonly List<Entry> _entries;
            private readonly Dictionary<Operand, Dictionary<uint, SearchResult>> _sharedEntries;
            private readonly HelperFunctionManager _hfm;

            public GtsContext(HelperFunctionManager hfm)
            {
                _entries = new List<Entry>();
                _sharedEntries = new Dictionary<Operand, Dictionary<uint, SearchResult>>();
                _hfm = hfm;
            }

            public int AddFunction(Operation baseOp, bool isMultiTarget, IReadOnlyList<uint> targetCbs, Function function)
            {
                int functionId = _hfm.AddFunction(function);

                _entries.Add(new Entry(functionId, baseOp.Inst, baseOp.StorageKind, isMultiTarget, targetCbs));

                return functionId;
            }

            public bool TryGetFunctionId(Operation baseOp, bool isMultiTarget, IReadOnlyList<uint> targetCbs, out int functionId)
            {
                foreach (Entry entry in _entries)
                {
                    if (entry.Inst != baseOp.Inst ||
                        entry.StorageKind != baseOp.StorageKind ||
                        entry.IsMultiTarget != isMultiTarget ||
                        entry.TargetCbs.Count != targetCbs.Count)
                    {
                        continue;
                    }

                    bool allEqual = true;

                    for (int index = 0; index < targetCbs.Count; index++)
                    {
                        if (targetCbs[index] != entry.TargetCbs[index])
                        {
                            allEqual = false;
                            break;
                        }
                    }

                    if (allEqual)
                    {
                        functionId = entry.FunctionId;
                        return true;
                    }
                }

                functionId = -1;
                return false;
            }

            public void AddSharedMemoryTargetCb(Operand baseOffset, uint targetCb, SearchResult result)
            {
                if (!_sharedEntries.TryGetValue(baseOffset, out Dictionary<uint, SearchResult> targetCbs))
                {
                    // No entry with this base offset, create a new one.

                    targetCbs = new Dictionary<uint, SearchResult>() { { targetCb, result } };

                    _sharedEntries.Add(baseOffset, targetCbs);
                }
                else if (targetCbs.TryGetValue(targetCb, out SearchResult existingResult))
                {
                    // If our entry already exists, but does not many the new result,
                    // just invalidate it since we have multiple different offsets stored
                    // on the same shared memory address, and this case is not supported right now.

                    if (existingResult.Found &&
                        (existingResult.Offset != result.Offset ||
                        existingResult.ConstOffset != result.ConstOffset))
                    {
                        targetCbs[targetCb] = SearchResult.NotFound;
                    }
                }
                else
                {
                    // An entry for this base offset already exists, but not for the specified
                    // constant buffer region where the storage buffer base address and size
                    // comes from.

                    targetCbs.Add(targetCb, result);
                }
            }

            public bool TryGetSharedMemoryTargetCb(Operand baseOffset, out SearchResult result)
            {
                if (_sharedEntries.TryGetValue(baseOffset, out Dictionary<uint, SearchResult> targetCbs) && targetCbs.Count == 1)
                {
                    SearchResult candidateResult = targetCbs.Values.First();

                    if (candidateResult.Found)
                    {
                        result = candidateResult;

                        return true;
                    }
                }

                result = default;

                return false;
            }
        }

        private struct SearchResult
        {
            public static SearchResult NotFound => new SearchResult(-1, 0);
            public bool Found => SbCbSlot != -1;
            public int SbCbSlot { get; }
            public int SbCbOffset { get; }
            public Operand Offset { get; }
            public int ConstOffset { get; }

            public SearchResult(int sbCbSlot, int sbCbOffset)
            {
                SbCbSlot = sbCbSlot;
                SbCbOffset = sbCbOffset;
            }

            public SearchResult(int sbCbSlot, int sbCbOffset, Operand offset, int constOffset = 0)
            {
                SbCbSlot = sbCbSlot;
                SbCbOffset = sbCbOffset;
                Offset = offset;
                ConstOffset = constOffset;
            }
        }

        public static void RunPass(HelperFunctionManager hfm, BasicBlock[] blocks, ShaderConfig config)
        {
            GtsContext gtsContext = new GtsContext(hfm);

            foreach (BasicBlock block in blocks)
            {
                for (LinkedListNode<INode> node = block.Operations.First; node != null; node = node.Next)
                {
                    if (!(node.Value is Operation operation))
                    {
                        continue;
                    }

                    if (IsGlobalMemory(operation.StorageKind))
                    {
                        node = ReplaceGlobalMemoryWithStorage(gtsContext, config, block, node);
                    }
                    else if (operation.Inst == Instruction.StoreShared)
                    {
                        // The NVIDIA compiler can sometimes use shared memory as temporary
                        // storage to place the base address and size on, so we need
                        // to be able to find such information stored in shared memory too.

                        if (TryGetSharedMemoryOffset(operation, out Operand baseOffset))
                        {
                            Operand value = operation.GetSource(operation.SourcesCount - 1);

                            var result = FindUniqueBaseAddressCb(gtsContext, block, value);
                            if (result.Found)
                            {
                                uint targetCb = PackCbSlotAndOffset(result.SbCbSlot, result.SbCbOffset);
                                gtsContext.AddSharedMemoryTargetCb(baseOffset, targetCb, result);
                            }
                        }
                    }
                }
            }
        }

        private static bool IsGlobalMemory(StorageKind storageKind)
        {
            return storageKind == StorageKind.GlobalMemory ||
                   storageKind == StorageKind.GlobalMemoryS8 ||
                   storageKind == StorageKind.GlobalMemoryS16 ||
                   storageKind == StorageKind.GlobalMemoryU8 ||
                   storageKind == StorageKind.GlobalMemoryU16;
        }

        private static bool IsSmallInt(StorageKind storageKind)
        {
            return storageKind == StorageKind.GlobalMemoryS8 ||
                   storageKind == StorageKind.GlobalMemoryS16 ||
                   storageKind == StorageKind.GlobalMemoryU8 ||
                   storageKind == StorageKind.GlobalMemoryU16;
        }

        private static LinkedListNode<INode> ReplaceGlobalMemoryWithStorage(
            GtsContext gtsContext,
            ShaderConfig config,
            BasicBlock block,
            LinkedListNode<INode> node)
        {
            Operation operation = node.Value as Operation;
            Operand globalAddress = operation.GetSource(0);
            SearchResult result = FindUniqueBaseAddressCb(gtsContext, block, globalAddress);

            if (result.Found)
            {
                // We found the storage buffer that is being accessed.
                // There are two possible paths here, if the operation is simple enough,
                // we just generate the storage access code inline.
                // Otherwise, we generate a function call (and the function if necessary).

                Operand offset = result.Offset;

                bool storageAligned = !(config.GpuAccessor.QueryHasUnalignedStorageBuffer() ||
                    config.GpuAccessor.QueryHostStorageBufferOffsetAlignment() > Constants.StorageAlignment);

                if (!storageAligned)
                {
                    Operand baseAddress = Cbuf(result.SbCbSlot, result.SbCbOffset);

                    Operand baseAddressMasked = Local();
                    Operand hostOffset = Local();

                    int alignment = config.GpuAccessor.QueryHostStorageBufferOffsetAlignment();

                    Operation maskOp = new Operation(Instruction.BitwiseAnd, baseAddressMasked, new[] { baseAddress, Const(-alignment) });
                    Operation subOp = new Operation(Instruction.Subtract, hostOffset, new[] { globalAddress, baseAddressMasked });

                    node.List.AddBefore(node, maskOp);
                    node.List.AddBefore(node, subOp);

                    offset = hostOffset;
                }
                else if (result.ConstOffset != 0)
                {
                    Operand newOffset = Local();

                    Operation addOp = new Operation(Instruction.Add, newOffset, new[] { offset, Const(result.ConstOffset) });

                    node.List.AddBefore(node, addOp);

                    offset = newOffset;
                }

                if (CanUseInlineStorageOp(operation, config.Options.TargetLanguage))
                {
                    return GenerateInlineStorageOp(config, node, operation, offset, result);
                }
                else
                {
                    int functionId = GenerateSingleTargetStorageOp(gtsContext, config, operation, result);

                    return GenerateCallStorageOp(node, operation, offset, functionId);
                }
            }
            else
            {
                // Failed to find the storage buffer directly.
                // Try to walk through Phi chains and find all possible constant buffers where
                // the base address might be stored.
                // Generate a helper function that will check all possible storage buffers and use the right one.

                int functionId = GenerateMultiTargetStorageOp(gtsContext, config, block, operation, node);

                return GenerateCallStorageOp(node, operation, null, functionId);
            }
        }

        private static bool CanUseInlineStorageOp(Operation operation, TargetLanguage targetLanguage)
        {
            if (operation.StorageKind != StorageKind.GlobalMemory)
            {
                return false;
            }

            return (operation.Inst != Instruction.AtomicMaxS32 &&
                    operation.Inst != Instruction.AtomicMinS32) || targetLanguage == TargetLanguage.Spirv;
        }

        private static LinkedListNode<INode> GenerateInlineStorageOp(
            ShaderConfig config,
            LinkedListNode<INode> node,
            Operation operation,
            Operand offset,
            SearchResult result)
        {
            bool isStore = operation.Inst == Instruction.Store || operation.Inst.IsAtomic();
            int binding = config.ResourceManager.GetStorageBufferBinding(result.SbCbSlot, result.SbCbOffset, isStore);

            Operand wordOffset = Local();

            Operand[] sources;

            if (operation.Inst == Instruction.AtomicCompareAndSwap)
            {
                sources = new Operand[]
                {
                    Const(binding),
                    Const(0),
                    wordOffset,
                    operation.GetSource(operation.SourcesCount - 2),
                    operation.GetSource(operation.SourcesCount - 1)
                };
            }
            else if (isStore)
            {
                sources = new Operand[] { Const(binding), Const(0), wordOffset, operation.GetSource(operation.SourcesCount - 1) };
            }
            else
            {
                sources = new Operand[] { Const(binding), Const(0), wordOffset };
            }

            Operation shiftOp = new Operation(Instruction.ShiftRightU32, wordOffset, new[] { offset, Const(2) });
            Operation storageOp = new Operation(operation.Inst, StorageKind.StorageBuffer, operation.Dest, sources);

            node.List.AddBefore(node, shiftOp);
            LinkedListNode<INode> newNode = node.List.AddBefore(node, storageOp);
            node.List.Remove(node);

            for (int srcIndex = 0; srcIndex < operation.SourcesCount; srcIndex++)
            {
                operation.SetSource(srcIndex, null);
            }

            operation.Dest = null;

            return newNode;
        }

        private static LinkedListNode<INode> GenerateCallStorageOp(LinkedListNode<INode> node, Operation operation, Operand offset, int functionId)
        {
            // Generate call to a helper function that will perform the storage buffer operation.

            Operand[] sources = new Operand[operation.SourcesCount - 1 + (offset == null ? 2 : 1)];

            sources[0] = Const(functionId);

            if (offset != null)
            {
                // If the offset was supplised, we use that and skip the global address.

                sources[1] = offset;

                for (int srcIndex = 2; srcIndex < operation.SourcesCount; srcIndex++)
                {
                    sources[srcIndex] = operation.GetSource(srcIndex);
                }
            }
            else
            {
                // Use the 64-bit global address which is split in 2 32-bit arguments.

                for (int srcIndex = 0; srcIndex < operation.SourcesCount; srcIndex++)
                {
                    sources[srcIndex + 1] = operation.GetSource(srcIndex);
                }
            }

            bool returnsValue = operation.Dest != null;
            Operand returnValue = returnsValue ? Local() : null;

            Operation callOp = new Operation(Instruction.Call, returnValue, sources);

            LinkedListNode<INode> newNode = node.List.AddBefore(node, callOp);

            if (returnsValue)
            {
                operation.TurnIntoCopy(returnValue);

                return node;
            }
            else
            {
                node.List.Remove(node);

                for (int srcIndex = 0; srcIndex < operation.SourcesCount; srcIndex++)
                {
                    operation.SetSource(srcIndex, null);
                }

                operation.Dest = null;

                return newNode;
            }
        }

        private static int GenerateSingleTargetStorageOp(GtsContext gtsContext, ShaderConfig config, Operation operation, SearchResult result)
        {
            List<uint> targetCbs = new List<uint>() { PackCbSlotAndOffset(result.SbCbSlot, result.SbCbOffset) };

            if (gtsContext.TryGetFunctionId(operation, isMultiTarget: false, targetCbs, out int functionId))
            {
                return functionId;
            }

            int inArgumentsCount = 1;

            if (operation.Inst == Instruction.AtomicCompareAndSwap)
            {
                inArgumentsCount = 3;
            }
            else if (operation.Inst == Instruction.Store || operation.Inst.IsAtomic())
            {
                inArgumentsCount = 2;
            }

            EmitterContext context = new EmitterContext();

            Operand offset = Argument(0);
            Operand compare = null;
            Operand value = null;

            if (inArgumentsCount == 3)
            {
                compare = Argument(1);
                value = Argument(2);
            }
            else if (inArgumentsCount == 2)
            {
                value = Argument(1);
            }

            Operand resultValue  = GenerateStorageOp(
                config,
                context,
                operation.Inst,
                operation.StorageKind,
                offset,
                compare,
                value,
                result);

            bool returnsValue = resultValue != null;

            if (returnsValue)
            {
                context.Return(resultValue);
            }
            else
            {
                context.Return();
            }

            string functionName = GetFunctionName(operation, isMultiTarget: false, targetCbs);

            Function function = new Function(
                ControlFlowGraph.Create(context.GetOperations()).Blocks,
                functionName,
                returnsValue,
                inArgumentsCount,
                0);

            functionId = gtsContext.AddFunction(operation, isMultiTarget: false, targetCbs, function);

            return functionId;
        }

        private static int GenerateMultiTargetStorageOp(GtsContext gtsContext, ShaderConfig config, BasicBlock block, Operation operation, LinkedListNode<INode> node)
        {
            Queue<PhiNode> phis = new Queue<PhiNode>();
            HashSet<PhiNode> visited = new HashSet<PhiNode>();
            List<uint> targetCbs = new List<uint>();

            Operand globalAddress = operation.GetSource(0);

            if (globalAddress.AsgOp is Operation addOp && addOp.Inst == Instruction.Add)
            {
                Operand src1 = addOp.GetSource(0);
                Operand src2 = addOp.GetSource(1);

                if (src1.Type == OperandType.Constant && src2.Type == OperandType.LocalVariable)
                {
                    globalAddress = src2;
                }
                else if (src1.Type == OperandType.LocalVariable && src2.Type == OperandType.Constant)
                {
                    globalAddress = src1;
                }
            }

            if (globalAddress.AsgOp is PhiNode phi && visited.Add(phi))
            {
                phis.Enqueue(phi);
            }

            while (phis.TryDequeue(out phi))
            {
                for (int srcIndex = 0; srcIndex < phi.SourcesCount; srcIndex++)
                {
                    BasicBlock phiBlock = phi.GetBlock(srcIndex);
                    Operand phiSource = phi.GetSource(srcIndex);

                    SearchResult result = FindUniqueBaseAddressCb(gtsContext, phiBlock, phiSource);

                    if (result.Found)
                    {
                        uint targetCb = PackCbSlotAndOffset(result.SbCbSlot, result.SbCbOffset);

                        if (!targetCbs.Contains(targetCb))
                        {
                            targetCbs.Add(targetCb);
                        }
                    }
                    else if (phiSource.AsgOp is PhiNode phi2 && visited.Add(phi2))
                    {
                        phis.Enqueue(phi2);
                    }
                }
            }

            targetCbs.Sort();

            if (targetCbs.Count == 0)
            {
                node.List.AddBefore(node, new CommentNode("global elimination failed"));
                config.GpuAccessor.Log($"Failed to find storage buffer for global memory operation \"{operation.Inst}\".");
            }

            if (gtsContext.TryGetFunctionId(operation, isMultiTarget: true, targetCbs, out int functionId))
            {
                return functionId;
            }

            int inArgumentsCount = 2;

            if (operation.Inst == Instruction.AtomicCompareAndSwap)
            {
                inArgumentsCount = 4;
            }
            else if (operation.Inst == Instruction.Store || operation.Inst.IsAtomic())
            {
                inArgumentsCount = 3;
            }

            EmitterContext context = new EmitterContext();

            Operand globalAddressLow = Argument(0);
            Operand globalAddressHigh = Argument(1);

            foreach (uint targetCb in targetCbs)
            {
                (int sbCbSlot, int sbCbOffset) = UnpackCbSlotAndOffset(targetCb);

                Operand baseAddrLow = Cbuf(sbCbSlot, sbCbOffset);
                Operand baseAddrHigh = Cbuf(sbCbSlot, sbCbOffset + 1);
                Operand size = Cbuf(sbCbSlot, sbCbOffset + 2);

                Operand offset = context.ISubtract(globalAddressLow, baseAddrLow);
                Operand borrow = context.ICompareLessUnsigned(globalAddressLow, baseAddrLow);

                Operand inRangeLow = context.ICompareLessUnsigned(offset, size);

                Operand addrHighBorrowed = context.IAdd(globalAddressHigh, borrow);

                Operand inRangeHigh = context.ICompareEqual(addrHighBorrowed, baseAddrHigh);

                Operand inRange = context.BitwiseAnd(inRangeLow, inRangeHigh);

                Operand lblSkip = Label();
                context.BranchIfFalse(lblSkip, inRange);

                Operand compare = null;
                Operand value = null;

                if (inArgumentsCount == 4)
                {
                    compare = Argument(2);
                    value = Argument(3);
                }
                else if (inArgumentsCount == 3)
                {
                    value = Argument(2);
                }

                SearchResult result = new SearchResult(sbCbSlot, sbCbOffset);

                int alignment = config.GpuAccessor.QueryHostStorageBufferOffsetAlignment();

                Operand baseAddressMasked = context.BitwiseAnd(baseAddrLow, Const(-alignment));
                Operand hostOffset = context.ISubtract(globalAddressLow, baseAddressMasked);

                Operand resultValue  = GenerateStorageOp(
                    config,
                    context,
                    operation.Inst,
                    operation.StorageKind,
                    hostOffset,
                    compare,
                    value,
                    result);

                if (resultValue != null)
                {
                    context.Return(resultValue);
                }
                else
                {
                    context.Return();
                }

                context.MarkLabel(lblSkip);
            }

            bool returnsValue = operation.Dest != null;

            if (returnsValue)
            {
                context.Return(Const(0));
            }
            else
            {
                context.Return();
            }

            string functionName = GetFunctionName(operation, isMultiTarget: true, targetCbs);

            Function function = new Function(
                ControlFlowGraph.Create(context.GetOperations()).Blocks,
                functionName,
                returnsValue,
                inArgumentsCount,
                0);

            functionId = gtsContext.AddFunction(operation, isMultiTarget: true, targetCbs, function);

            return functionId;
        }

        private static uint PackCbSlotAndOffset(int cbSlot, int cbOffset)
        {
            return (uint)((ushort)cbSlot | ((ushort)cbOffset << 16));
        }

        private static (int, int) UnpackCbSlotAndOffset(uint packed)
        {
            return ((ushort)packed, (ushort)(packed >> 16));
        }

        private static string GetFunctionName(Operation baseOp, bool isMultiTarget, IReadOnlyList<uint> targetCbs)
        {
            string name = baseOp.Inst.ToString();

            name += baseOp.StorageKind switch
            {
                StorageKind.GlobalMemoryS8 => "S8",
                StorageKind.GlobalMemoryS16 => "S16",
                StorageKind.GlobalMemoryU8 => "U8",
                StorageKind.GlobalMemoryU16 => "U16",
                _ => string.Empty
            };

            if (isMultiTarget)
            {
                name += "Multi";
            }

            foreach (uint targetCb in targetCbs)
            {
                (int sbCbSlot, int sbCbOffset) = UnpackCbSlotAndOffset(targetCb);

                name += $"_c{sbCbSlot}o{sbCbOffset}";
            }

            return name;
        }

        private static Operand GenerateStorageOp(
            ShaderConfig config,
            EmitterContext context,
            Instruction inst,
            StorageKind storageKind,
            Operand offset,
            Operand compare,
            Operand value,
            SearchResult result)
        {
            Operand wordOffset = context.ShiftRightU32(offset, Const(2));

            if (inst.IsAtomic())
            {
                if (IsSmallInt(storageKind))
                {
                    throw new NotImplementedException();
                }

                int binding = config.ResourceManager.GetStorageBufferBinding(result.SbCbSlot, result.SbCbOffset, write: true);

                switch (inst)
                {
                    case Instruction.AtomicAdd:
                        return context.AtomicAdd(StorageKind.StorageBuffer, binding, Const(0), wordOffset, value);
                    case Instruction.AtomicAnd:
                        return context.AtomicAnd(StorageKind.StorageBuffer, binding, Const(0), wordOffset, value);
                    case Instruction.AtomicCompareAndSwap:
                        return context.AtomicCompareAndSwap(StorageKind.StorageBuffer, binding, Const(0), wordOffset, compare, value);
                    case Instruction.AtomicMaxS32:
                        if (config.Options.TargetLanguage == TargetLanguage.Spirv)
                        {
                            return context.AtomicMaxS32(StorageKind.StorageBuffer, binding, Const(0), wordOffset, value);
                        }
                        else
                        {
                            return GenerateAtomicCasLoop(context, wordOffset, binding, (memValue) =>
                            {
                                return context.IMaximumS32(memValue, value);
                            });
                        }
                    case Instruction.AtomicMaxU32:
                        return context.AtomicMaxU32(StorageKind.StorageBuffer, binding, Const(0), wordOffset, value);
                    case Instruction.AtomicMinS32:
                        if (config.Options.TargetLanguage == TargetLanguage.Spirv)
                        {
                            return context.AtomicMinS32(StorageKind.StorageBuffer, binding, Const(0), wordOffset, value);
                        }
                        else
                        {
                            return GenerateAtomicCasLoop(context, wordOffset, binding, (memValue) =>
                            {
                                return context.IMinimumS32(memValue, value);
                            });
                        }
                    case Instruction.AtomicMinU32:
                        return context.AtomicMinU32(StorageKind.StorageBuffer, binding, Const(0), wordOffset, value);
                    case Instruction.AtomicOr:
                        return context.AtomicOr(StorageKind.StorageBuffer, binding, Const(0), wordOffset, value);
                    case Instruction.AtomicSwap:
                        return context.AtomicSwap(StorageKind.StorageBuffer, binding, Const(0), wordOffset, value);
                    case Instruction.AtomicXor:
                        return context.AtomicXor(StorageKind.StorageBuffer, binding, Const(0), wordOffset, value);
                }
            }
            else if (inst == Instruction.Store)
            {
                int binding = config.ResourceManager.GetStorageBufferBinding(result.SbCbSlot, result.SbCbOffset, write: true);

                int bitSize = storageKind switch
                {
                    StorageKind.GlobalMemoryS8 or
                    StorageKind.GlobalMemoryU8 => 8,
                    StorageKind.GlobalMemoryS16 or
                    StorageKind.GlobalMemoryU16 => 16,
                    _ => 32
                };

                if (bitSize < 32)
                {
                    Operand bitOffset = GetBitOffset(context, offset);

                    GenerateAtomicCasLoop(context, wordOffset, binding, (memValue) =>
                    {
                        return context.BitfieldInsert(memValue, value, bitOffset, Const(bitSize));
                    });
                }
                else
                {
                    context.Store(StorageKind.StorageBuffer, binding, Const(0), wordOffset, value);
                }
            }
            else
            {
                int binding = config.ResourceManager.GetStorageBufferBinding(result.SbCbSlot, result.SbCbOffset, write: false);

                value = context.Load(StorageKind.StorageBuffer, binding, Const(0), wordOffset);

                if (IsSmallInt(storageKind))
                {
                    Operand bitOffset = GetBitOffset(context, offset);

                    switch (storageKind)
                    {
                        case StorageKind.GlobalMemoryS8:
                            value = context.ShiftRightS32(value, bitOffset);
                            value = context.BitfieldExtractS32(value, Const(0), Const(8));
                            break;
                        case StorageKind.GlobalMemoryS16:
                            value = context.ShiftRightS32(value, bitOffset);
                            value = context.BitfieldExtractS32(value, Const(0), Const(16));
                            break;
                        case StorageKind.GlobalMemoryU8:
                            value = context.ShiftRightU32(value, bitOffset);
                            value = context.BitwiseAnd(value, Const(byte.MaxValue));
                            break;
                        case StorageKind.GlobalMemoryU16:
                            value = context.ShiftRightU32(value, bitOffset);
                            value = context.BitwiseAnd(value, Const(ushort.MaxValue));
                            break;
                    }
                }

                return value;
            }

            return null;
        }

        private static Operand GetBitOffset(EmitterContext context, Operand offset)
        {
            return context.ShiftLeft(context.BitwiseAnd(offset, Const(3)), Const(3));
        }

        private static Operand GenerateAtomicCasLoop(EmitterContext context, Operand wordOffset, int binding, Func<Operand, Operand> opCallback)
        {
            Operand lblLoopHead = Label();

            context.MarkLabel(lblLoopHead);

            Operand oldValue = context.Load(StorageKind.StorageBuffer, binding, Const(0), wordOffset);
            Operand newValue = opCallback(oldValue);

            Operand casResult = context.AtomicCompareAndSwap(
                StorageKind.StorageBuffer,
                binding,
                Const(0),
                wordOffset,
                oldValue,
                newValue);

            Operand casFail = context.ICompareNotEqual(casResult, oldValue);

            context.BranchIfTrue(lblLoopHead, casFail);

            return oldValue;
        }

        private static SearchResult FindUniqueBaseAddressCb(GtsContext gtsContext, BasicBlock block, Operand globalAddress)
        {
            globalAddress = Utils.FindLastOperation(globalAddress, block);

            if (globalAddress.Type == OperandType.ConstantBuffer)
            {
                return GetBaseAddressCbWithOffset(globalAddress, Const(0), 0);
            }

            Operation operation = globalAddress.AsgOp as Operation;

            if (operation == null)
            {
                return SearchResult.NotFound;
            }

            if (operation.Inst != Instruction.Add)
            {
                if (operation.Inst == Instruction.LoadShared &&
                    TryGetSharedMemoryOffset(operation, out Operand sharedBo) &&
                    gtsContext.TryGetSharedMemoryTargetCb(sharedBo, out SearchResult result))
                {
                    return result;
                }

                return SearchResult.NotFound;
            }

            Operand src1 = operation.GetSource(0);
            Operand src2 = operation.GetSource(1);

            int constOffset = 0;

            if ((src1.Type == OperandType.LocalVariable && src2.Type == OperandType.Constant) ||
                (src2.Type == OperandType.LocalVariable && src1.Type == OperandType.Constant))
            {
                Operand baseAddr;
                Operand offset;

                if (src1.Type == OperandType.LocalVariable)
                {
                    baseAddr = Utils.FindLastOperation(src1, block);
                    offset = src2;
                }
                else
                {
                    baseAddr = Utils.FindLastOperation(src2, block);
                    offset = src1;
                }

                var result = GetBaseAddressCbWithOffset(baseAddr, offset, 0);
                if (result.Found)
                {
                    return result;
                }

                constOffset = offset.Value;
                operation = baseAddr.AsgOp as Operation;

                if (operation == null || operation.Inst != Instruction.Add)
                {
                    return SearchResult.NotFound;
                }
            }

            src1 = operation.GetSource(0);
            src2 = operation.GetSource(1);

            // If we have two possible results, we give preference to the ones from
            // the driver reserved constant buffer, as those are the ones that
            // contains the base address.

            if (src1.Type != OperandType.ConstantBuffer ||
                (src2.Type == OperandType.ConstantBuffer && src2.GetCbufSlot() == DriverReservedCb))
            {
                return GetBaseAddressCbWithOffset(src2, src1, constOffset);
            }

            return GetBaseAddressCbWithOffset(src1, src2, constOffset);
        }

        private static SearchResult GetBaseAddressCbWithOffset(Operand baseAddress, Operand offset, int constOffset)
        {
            if (baseAddress.Type == OperandType.ConstantBuffer)
            {
                int sbCbSlot = baseAddress.GetCbufSlot();
                int sbCbOffset = baseAddress.GetCbufOffset();

                if ((sbCbOffset & 3) == 0)
                {
                    return new SearchResult(sbCbSlot, sbCbOffset, offset, constOffset);
                }
            }

            return SearchResult.NotFound;
        }

        private static bool TryGetSharedMemoryOffset(Operation operation, out Operand baseOffset)
        {
            baseOffset = operation.GetSource(0);

            // The byte offset is right shifted by 2 to get the 32-bit word offset,
            // so we want to get the byte offset back, since each one of those word
            // offsets are a new "local variable" which will not match.

            if (baseOffset.AsgOp is Operation shiftRightOp &&
                shiftRightOp.Inst == Instruction.ShiftRightU32 &&
                shiftRightOp.GetSource(1).Type == OperandType.Constant &&
                shiftRightOp.GetSource(1).Value == 2)
            {
                baseOffset = shiftRightOp.GetSource(0);

                return baseOffset.Type == OperandType.LocalVariable;
            }

            return false;
        }
    }
}