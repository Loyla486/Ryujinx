using ChocolArm64.Instruction;
using ChocolArm64.Memory;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace ChocolArm64.Decoder
{
    static class ADecoder
    {
        public static (ABlock[] Graph, ABlock Root) DecodeSubroutine(ATranslator Translator, long Start)
        {
            Dictionary<long, ABlock> Visited    = new Dictionary<long, ABlock>();
            Dictionary<long, ABlock> VisitedEnd = new Dictionary<long, ABlock>();

            Queue<ABlock> Blocks = new Queue<ABlock>();

            ABlock Enqueue(long Position)
            {
                if (!Visited.TryGetValue(Position, out ABlock Output))
                {
                    Output = new ABlock(Position);

                    Blocks.Enqueue(Output);

                    Visited.Add(Position, Output);
                }

                return Output;
            }

            ABlock Root = Enqueue(Start);

            while (Blocks.Count > 0)
            {
                ABlock Current = Blocks.Dequeue();

                FillBlock(Translator.Thread.Memory, Current);

                //Set child blocks. "Branch" is the block the branch instruction
                //points to (when taken), "Next" is the block at the next address,
                //executed when the branch is not taken. For Unconditional Branches
                //(except BL/BLR that are sub calls) or end of executable, Next is null.
                if (Current.OpCodes.Count > 0)
                {
                    bool HasCachedSub = false;

                    AOpCode LastOp = Current.GetLastOp();

                    if (LastOp is AOpCodeBImm Op)
                    {
                        if (Op.Emitter == AInstEmit.Bl)
                        {
                            HasCachedSub = Translator.HasCachedSub(Op.Imm);
                        }
                        else
                        {
                            Current.Branch = Enqueue(Op.Imm);
                        }
                    }

                    if ((!(LastOp is AOpCodeBImmAl) &&
                         !(LastOp is AOpCodeBReg)) || HasCachedSub)
                    {
                        Current.Next = Enqueue(Current.EndPosition);
                    }
                }

                //If we have on the tree two blocks with the same end position,
                //then we need to split the bigger block and have two small blocks,
                //the end position of the bigger "Current" block should then be == to
                //the position of the "Smaller" block.
                while (VisitedEnd.TryGetValue(Current.EndPosition, out ABlock Smaller))
                {
                    if (Current.Position > Smaller.Position)
                    {
                        ABlock Temp = Smaller;

                        Smaller = Current;
                        Current = Temp;
                    }

                    Current.EndPosition = Smaller.Position;
                    Current.Next        = Smaller;
                    Current.Branch      = null;

                    Current.OpCodes.RemoveRange(
                        Current.OpCodes.Count - Smaller.OpCodes.Count,
                        Smaller.OpCodes.Count);

                    VisitedEnd[Smaller.EndPosition] = Smaller;
                }

                VisitedEnd.Add(Current.EndPosition, Current);
            }

            //Make and sort Graph blocks array by position.
            ABlock[] Graph = new ABlock[Visited.Count];

            while (Visited.Count > 0)
            {
                ulong FirstPos = ulong.MaxValue;

                foreach (ABlock Block in Visited.Values)
                {
                    if (FirstPos > (ulong)Block.Position)
                        FirstPos = (ulong)Block.Position;
                }

                ABlock Current = Visited[(long)FirstPos];

                do
                {
                    Graph[Graph.Length - Visited.Count] = Current;

                    Visited.Remove(Current.Position);

                    Current = Current.Next;
                }
                while (Current != null);
            }

            return (Graph, Root);
        }

        private static void FillBlock(AMemory Memory, ABlock Block)
        {
            long Position = Block.Position;

            AOpCode OpCode;

            do
            {
                OpCode = DecodeOpCode(Memory, Position);

                Block.OpCodes.Add(OpCode);

                Position += 4;
            }
            while (!(IsBranch(OpCode) || IsException(OpCode)));

            Block.EndPosition = Position;
        }

        private static bool IsBranch(AOpCode OpCode)
        {
            return OpCode is AOpCodeBImm ||
                   OpCode is AOpCodeBReg;
        }

        private static bool IsException(AOpCode OpCode)
        {
            return OpCode.Emitter == AInstEmit.Brk ||
                   OpCode.Emitter == AInstEmit.Svc ||
                   OpCode.Emitter == AInstEmit.Und;
        }

        public static AOpCode DecodeOpCode(AMemory Memory, long Position)
        {
            int OpCode = Memory.ReadInt32(Position);

            AInst Inst = AOpCodeTable.GetInst(OpCode);

            AOpCode DecodedOpCode = new AOpCode(AInst.Undefined, Position, OpCode);

            if (Inst.Type != null)
            {
                DecodedOpCode = CreateOpCode(Inst.Type, Inst, Position, OpCode);
            }

            return DecodedOpCode;
        }

        private delegate object OpActivator(AInst Inst, long Position, int OpCode);

        private static Dictionary<Type, OpActivator> Activators = new Dictionary<Type, OpActivator>();

        private static AOpCode CreateOpCode(Type Type, AInst Inst, long Position, int OpCode)
        {
            if (Type == null)
            {
                throw new ArgumentNullException(nameof(Type));
            }

            if (!Activators.TryGetValue(Type, out OpActivator CreateInstance))
            {
                Type[] ArgTypes = new Type[] { typeof(AInst), typeof(long), typeof(int) };

                DynamicMethod Mthd = new DynamicMethod($"{Type.Name}_Create", Type, ArgTypes);

                ILGenerator Generator = Mthd.GetILGenerator();

                Generator.Emit(OpCodes.Ldarg_0);
                Generator.Emit(OpCodes.Ldarg_1);
                Generator.Emit(OpCodes.Ldarg_2);
                Generator.Emit(OpCodes.Newobj, Type.GetConstructor(ArgTypes));
                Generator.Emit(OpCodes.Ret);

                CreateInstance = (OpActivator)Mthd.CreateDelegate(typeof(OpActivator));

                Activators.Add(Type, CreateInstance);
            }

            return (AOpCode)CreateInstance(Inst, Position, OpCode);
        }
    }
}