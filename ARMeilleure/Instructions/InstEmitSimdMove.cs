using ARMeilleure.Decoders;
using ARMeilleure.IntermediateRepresentation;
using ARMeilleure.Translation;

using static ARMeilleure.Instructions.InstEmitHelper;
using static ARMeilleure.Instructions.InstEmitSimdHelper;
using static ARMeilleure.IntermediateRepresentation.OperandHelper;

namespace ARMeilleure.Instructions
{
    static partial class InstEmit
    {
#region "Masks"
        private static readonly long[] _masksE0_TrnUzpXtn = new long[]
        {
            14L << 56 | 12L << 48 | 10L << 40 | 08L << 32 | 06L << 24 | 04L << 16 | 02L << 8 | 00L << 0,
            13L << 56 | 12L << 48 | 09L << 40 | 08L << 32 | 05L << 24 | 04L << 16 | 01L << 8 | 00L << 0,
            11L << 56 | 10L << 48 | 09L << 40 | 08L << 32 | 03L << 24 | 02L << 16 | 01L << 8 | 00L << 0
        };

        private static readonly long[] _masksE1_TrnUzp = new long[]
        {
            15L << 56 | 13L << 48 | 11L << 40 | 09L << 32 | 07L << 24 | 05L << 16 | 03L << 8 | 01L << 0,
            15L << 56 | 14L << 48 | 11L << 40 | 10L << 32 | 07L << 24 | 06L << 16 | 03L << 8 | 02L << 0,
            15L << 56 | 14L << 48 | 13L << 40 | 12L << 32 | 07L << 24 | 06L << 16 | 05L << 8 | 04L << 0
        };

        private static readonly long[] _masksE0_Uzp = new long[]
        {
            13L << 56 | 09L << 48 | 05L << 40 | 01L << 32 | 12L << 24 | 08L << 16 | 04L << 8 | 00L << 0,
            11L << 56 | 10L << 48 | 03L << 40 | 02L << 32 | 09L << 24 | 08L << 16 | 01L << 8 | 00L << 0
        };

        private static readonly long[] _masksE1_Uzp = new long[]
        {
            15L << 56 | 11L << 48 | 07L << 40 | 03L << 32 | 14L << 24 | 10L << 16 | 06L << 8 | 02L << 0,
            15L << 56 | 14L << 48 | 07L << 40 | 06L << 32 | 13L << 24 | 12L << 16 | 05L << 8 | 04L << 0
        };
#endregion

        public static void Dup_Gp(EmitterContext context)
        {
            OpCodeSimdIns op = (OpCodeSimdIns)context.CurrOp;

            Operand n = GetIntOrZR(context, op.Rn);

            if (Optimizations.UseSse2)
            {
                switch (op.Size)
                {
                    case 0: n = context.ZeroExtend8 (n.Type, n); n = context.Multiply(n, Const(0x01010101)); break;
                    case 1: n = context.ZeroExtend16(n.Type, n); n = context.Multiply(n, Const(0x00010001)); break;
                    case 2: n = context.ZeroExtend32(n.Type, n); break;
                }

                Operand res = context.VectorInsert(context.VectorZero(), n, 0);

                if (op.Size < 3)
                {
                    if (op.RegisterSize == RegisterSize.Simd64)
                    {
                        res = context.AddIntrinsic(Instruction.X86Shufps, res, res, Const(0xf0));
                    }
                    else
                    {
                        res = context.AddIntrinsic(Instruction.X86Shufps, res, res, Const(0));
                    }
                }
                else
                {
                    res = context.AddIntrinsic(Instruction.X86Movlhps, res, res);
                }

                context.Copy(GetVec(op.Rd), res);
            }
            else
            {
                Operand res = context.VectorZero();

                int elems = op.GetBytesCount() >> op.Size;

                for (int index = 0; index < elems; index++)
                {
                    res = EmitVectorInsert(context, res, n, index, op.Size);
                }

                context.Copy(GetVec(op.Rd), res);
            }
        }

        public static void Dup_S(EmitterContext context)
        {
            OpCodeSimdIns op = (OpCodeSimdIns)context.CurrOp;

            Operand ne = EmitVectorExtractZx(context, op.Rn, op.DstIndex, op.Size);

            context.Copy(GetVec(op.Rd), EmitVectorInsert(context, context.VectorZero(), ne, 0, op.Size));
        }

        public static void Dup_V(EmitterContext context)
        {
            OpCodeSimdIns op = (OpCodeSimdIns)context.CurrOp;

            if (Optimizations.UseSse2)
            {
                Operand res = GetVec(op.Rn);

                if (op.Size == 0)
                {
                    if (op.DstIndex != 0)
                    {
                        res = context.AddIntrinsic(Instruction.X86Psrldq, res, Const(op.DstIndex));
                    }

                    res = context.AddIntrinsic(Instruction.X86Punpcklbw, res, res);
                    res = context.AddIntrinsic(Instruction.X86Punpcklwd, res, res);
                    res = context.AddIntrinsic(Instruction.X86Shufps, res, res, Const(0));
                }
                else if (op.Size == 1)
                {
                    if (op.DstIndex != 0)
                    {
                        res = context.AddIntrinsic(Instruction.X86Psrldq, res, Const(op.DstIndex * 2));
                    }

                    res = context.AddIntrinsic(Instruction.X86Punpcklwd, res, res);
                    res = context.AddIntrinsic(Instruction.X86Shufps, res, res, Const(0));
                }
                else if (op.Size == 2)
                {
                    int mask = op.DstIndex * 0b01010101;

                    res = context.AddIntrinsic(Instruction.X86Shufps, res, res, Const(mask));
                }
                else if (op.DstIndex == 0 && op.RegisterSize != RegisterSize.Simd64)
                {
                    res = context.AddIntrinsic(Instruction.X86Movlhps, res, res);
                }
                else if (op.DstIndex == 1)
                {
                    res = context.AddIntrinsic(Instruction.X86Movhlps, res, res);
                }

                if (op.RegisterSize == RegisterSize.Simd64)
                {
                    res = context.VectorZeroUpper64(res);
                }

                context.Copy(GetVec(op.Rd), res);
            }
            else
            {
                Operand ne = EmitVectorExtractZx(context, op.Rn, op.DstIndex, op.Size);

                Operand res = context.VectorZero();

                int elems = op.GetBytesCount() >> op.Size;

                for (int index = 0; index < elems; index++)
                {
                    res = EmitVectorInsert(context, res, ne, index, op.Size);
                }

                context.Copy(GetVec(op.Rd), res);
            }
        }

        public static void Ext_V(EmitterContext context)
        {
            OpCodeSimdExt op = (OpCodeSimdExt)context.CurrOp;

            if (Optimizations.UseSse2)
            {
                Operand nShifted = GetVec(op.Rn);

                if (op.RegisterSize == RegisterSize.Simd64)
                {
                    nShifted = context.AddIntrinsic(Instruction.X86Movlhps, nShifted, context.VectorZero());
                }

                nShifted = context.AddIntrinsic(Instruction.X86Psrldq, nShifted, Const(op.Imm4));

                Operand mShifted = GetVec(op.Rm);

                mShifted = context.AddIntrinsic(Instruction.X86Pslldq, mShifted, Const(op.GetBytesCount() - op.Imm4));

                if (op.RegisterSize == RegisterSize.Simd64)
                {
                    mShifted = context.AddIntrinsic(Instruction.X86Movlhps, mShifted, context.VectorZero());
                }

                Operand res = context.AddIntrinsic(Instruction.X86Por, nShifted, mShifted);

                context.Copy(GetVec(op.Rd), res);
            }
            else
            {
                Operand res = context.VectorZero();

                int bytes = op.GetBytesCount();

                int position = op.Imm4 & (bytes - 1);

                for (int index = 0; index < bytes; index++)
                {
                    int reg = op.Imm4 + index < bytes ? op.Rn : op.Rm;

                    Operand e = EmitVectorExtractZx(context, reg, position, 0);

                    position = (position + 1) & (bytes - 1);

                    res = EmitVectorInsert(context, res, e, index, 0);
                }

                context.Copy(GetVec(op.Rd), res);
            }
        }

        public static void Fcsel_S(EmitterContext context)
        {
            OpCodeSimdFcond op = (OpCodeSimdFcond)context.CurrOp;

            Operand lblTrue = Label();
            Operand lblEnd  = Label();

            Operand isTrue = InstEmitFlowHelper.GetCondTrue(context, op.Cond);

            context.BranchIfTrue(lblTrue, isTrue);

            OperandType type = op.Size == 0 ? OperandType.FP32 : OperandType.FP64;

            Operand me = context.VectorExtract(type, GetVec(op.Rm), 0);

            context.Copy(GetVec(op.Rd), context.VectorInsert(context.VectorZero(), me, 0));

            context.Branch(lblEnd);

            context.MarkLabel(lblTrue);

            Operand ne = context.VectorExtract(type, GetVec(op.Rn), 0);

            context.Copy(GetVec(op.Rd), context.VectorInsert(context.VectorZero(), ne, 0));

            context.MarkLabel(lblEnd);
        }

        public static void Fmov_Ftoi(EmitterContext context)
        {
            OpCodeSimd op = (OpCodeSimd)context.CurrOp;

            Operand ne = EmitVectorExtractZx(context, op.Rn, 0, op.Size + 2);

            SetIntOrZR(context, op.Rd, ne);
        }

        public static void Fmov_Ftoi1(EmitterContext context)
        {
            OpCodeSimd op = (OpCodeSimd)context.CurrOp;

            Operand ne = EmitVectorExtractZx(context, op.Rn, 1, 3);

            SetIntOrZR(context, op.Rd, ne);
        }

        public static void Fmov_Itof(EmitterContext context)
        {
            OpCodeSimd op = (OpCodeSimd)context.CurrOp;

            Operand n = GetIntOrZR(context, op.Rn);

            context.Copy(GetVec(op.Rd), EmitVectorInsert(context, context.VectorZero(), n, 0, op.Size + 2));
        }

        public static void Fmov_Itof1(EmitterContext context)
        {
            OpCodeSimd op = (OpCodeSimd)context.CurrOp;

            Operand n = GetIntOrZR(context, op.Rn);

            context.Copy(GetVec(op.Rd), EmitVectorInsert(context, GetVec(op.Rd), n, 1, 3));
        }

        public static void Fmov_S(EmitterContext context)
        {
            OpCodeSimd op = (OpCodeSimd)context.CurrOp;

            OperandType type = op.Size == 0 ? OperandType.FP32 : OperandType.FP64;

            Operand ne = context.VectorExtract(type, GetVec(op.Rn), 0);

            context.Copy(GetVec(op.Rd), context.VectorInsert(context.VectorZero(), ne, 0));
        }

        public static void Fmov_Si(EmitterContext context)
        {
            OpCodeSimdFmov op = (OpCodeSimdFmov)context.CurrOp;

            if (op.Size == 0)
            {
                context.Copy(GetVec(op.Rd), X86GetScalar(context, (int)op.Immediate));
            }
            else
            {
                context.Copy(GetVec(op.Rd), X86GetScalar(context, op.Immediate));
            }
        }

        public static void Fmov_Vi(EmitterContext context)
        {
            OpCodeSimdImm op = (OpCodeSimdImm)context.CurrOp;

            Operand e = Const(op.Immediate);

            Operand res = context.VectorZero();

            int elems = op.RegisterSize == RegisterSize.Simd128 ? 4 : 2;

            for (int index = 0; index < (elems >> op.Size); index++)
            {
                res = EmitVectorInsert(context, res, e, index, op.Size + 2);
            }

            context.Copy(GetVec(op.Rd), res);
        }

        public static void Ins_Gp(EmitterContext context)
        {
            OpCodeSimdIns op = (OpCodeSimdIns)context.CurrOp;

            Operand d = GetVec(op.Rd);
            Operand n = GetIntOrZR(context, op.Rn);

            context.Copy(d, EmitVectorInsert(context, d, n, op.DstIndex, op.Size));
        }

        public static void Ins_V(EmitterContext context)
        {
            OpCodeSimdIns op = (OpCodeSimdIns)context.CurrOp;

            Operand d  = GetVec(op.Rd);
            Operand ne = EmitVectorExtractZx(context, op.Rn, op.SrcIndex, op.Size);

            context.Copy(d, EmitVectorInsert(context, d, ne, op.DstIndex, op.Size));
        }

        public static void Movi_V(EmitterContext context)
        {
            if (Optimizations.UseSse2)
            {
                EmitMoviMvni(context, not: false);
            }
            else
            {
                EmitVectorImmUnaryOp(context, (op1) => op1);
            }
        }

        public static void Mvni_V(EmitterContext context)
        {
            if (Optimizations.UseSse2)
            {
                EmitMoviMvni(context, not: true);
            }
            else
            {
                EmitVectorImmUnaryOp(context, (op1) => context.BitwiseNot(op1));
            }
        }

        public static void Smov_S(EmitterContext context)
        {
            OpCodeSimdIns op = (OpCodeSimdIns)context.CurrOp;

            Operand ne = EmitVectorExtractSx(context, op.Rn, op.DstIndex, op.Size);

            if (op.RegisterSize == RegisterSize.Simd64)
            {
                ne = context.ZeroExtend32(OperandType.I64, ne);
            }

            SetIntOrZR(context, op.Rd, ne);
        }

        public static void Tbl_V(EmitterContext context)
        {
            OpCodeSimdTbl op = (OpCodeSimdTbl)context.CurrOp;

            if (Optimizations.UseSsse3)
            {
                Operand n = GetVec(op.Rn);
                Operand m = GetVec(op.Rm);

                Operand mask = X86GetAllElements(context, 0x0F0F0F0F0F0F0F0FL);

                Operand mMask = context.AddIntrinsic(Instruction.X86Pcmpgtb, m, mask);

                mMask = context.AddIntrinsic(Instruction.X86Por, mMask, m);

                Operand res = context.AddIntrinsic(Instruction.X86Pshufb, n, mMask);

                for (int index = 1; index < op.Size; index++)
                {
                    Operand ni = GetVec((op.Rn + index) & 0x1f);

                    Operand indexMask = X86GetAllElements(context, 0x1010101010101010L * index);

                    Operand mMinusMask = context.AddIntrinsic(Instruction.X86Psubb, m, indexMask);

                    Operand mMask2 = context.AddIntrinsic(Instruction.X86Pcmpgtb, mMinusMask, mask);

                    mMask2 = context.AddIntrinsic(Instruction.X86Por, mMask2, mMinusMask);

                    Operand res2 = context.AddIntrinsic(Instruction.X86Pshufb, ni, mMask2);

                    res = context.AddIntrinsic(Instruction.X86Por, res, res2);
                }

                if (op.RegisterSize == RegisterSize.Simd64)
                {
                    res = context.VectorZeroUpper64(res);
                }

                context.Copy(GetVec(op.Rd), res);
            }
            else
            {
                Operand[] args = new Operand[1 + op.Size];

                args[0] = GetVec(op.Rm);

                for (int index = 0; index < op.Size; index++)
                {
                    args[1 + index] = GetVec((op.Rn + index) & 0x1f);
                }

                string name = null;

                switch (op.Size)
                {
                    case 1: name = op.RegisterSize == RegisterSize.Simd64
                        ? nameof(SoftFallback.Tbl1_V64)
                        : nameof(SoftFallback.Tbl1_V128); break;

                    case 2: name = op.RegisterSize == RegisterSize.Simd64
                        ? nameof(SoftFallback.Tbl2_V64)
                        : nameof(SoftFallback.Tbl2_V128); break;

                    case 3: name = op.RegisterSize == RegisterSize.Simd64
                        ? nameof(SoftFallback.Tbl3_V64)
                        : nameof(SoftFallback.Tbl3_V128); break;

                    case 4: name = op.RegisterSize == RegisterSize.Simd64
                        ? nameof(SoftFallback.Tbl4_V64)
                        : nameof(SoftFallback.Tbl4_V128); break;
                }

                context.Copy(GetVec(op.Rd), context.Call(typeof(SoftFallback).GetMethod(name), args));
            }
        }

        public static void Trn1_V(EmitterContext context)
        {
            EmitVectorTranspose(context, part: 0);
        }

        public static void Trn2_V(EmitterContext context)
        {
            EmitVectorTranspose(context, part: 1);
        }

        public static void Umov_S(EmitterContext context)
        {
            OpCodeSimdIns op = (OpCodeSimdIns)context.CurrOp;

            Operand ne = EmitVectorExtractZx(context, op.Rn, op.DstIndex, op.Size);

            SetIntOrZR(context, op.Rd, ne);
        }

        public static void Uzp1_V(EmitterContext context)
        {
            EmitVectorUnzip(context, part: 0);
        }

        public static void Uzp2_V(EmitterContext context)
        {
            EmitVectorUnzip(context, part: 1);
        }

        public static void Xtn_V(EmitterContext context)
        {
            OpCodeSimd op = (OpCodeSimd)context.CurrOp;

            if (Optimizations.UseSsse3)
            {
                Operand d = GetVec(op.Rd);

                Operand res = context.AddIntrinsic(Instruction.X86Movlhps, d, context.VectorZero());

                Operand n = GetVec(op.Rn);

                Operand mask = X86GetAllElements(context, _masksE0_TrnUzpXtn[op.Size]);

                Operand res2 = context.AddIntrinsic(Instruction.X86Pshufb, n, mask);

                Instruction movInst = op.RegisterSize == RegisterSize.Simd128
                    ? Instruction.X86Movlhps
                    : Instruction.X86Movhlps;

                res = context.AddIntrinsic(movInst, res, res2);

                context.Copy(GetVec(op.Rd), res);
            }
            else
            {
                int elems = 8 >> op.Size;

                int part = op.RegisterSize == RegisterSize.Simd128 ? elems : 0;

                Operand res = part == 0 ? context.VectorZero() : context.Copy(GetVec(op.Rd));

                for (int index = 0; index < elems; index++)
                {
                    Operand ne = EmitVectorExtractZx(context, op.Rn, index, op.Size + 1);

                    res = EmitVectorInsert(context, res, ne, part + index, op.Size);
                }

                context.Copy(GetVec(op.Rd), res);
            }
        }

        public static void Zip1_V(EmitterContext context)
        {
            EmitVectorZip(context, part: 0);
        }

        public static void Zip2_V(EmitterContext context)
        {
            EmitVectorZip(context, part: 1);
        }

        private static void EmitMoviMvni(EmitterContext context, bool not)
        {
            OpCodeSimdImm op = (OpCodeSimdImm)context.CurrOp;

            long imm = op.Immediate;

            switch (op.Size)
            {
                case 0: imm *= 0x01010101; break;
                case 1: imm *= 0x00010001; break;
            }

            if (not)
            {
                imm = ~imm;
            }

            Operand mask;

            if (op.Size < 3)
            {
                mask = X86GetAllElements(context, (int)imm);
            }
            else
            {
                mask = X86GetAllElements(context, imm);
            }

            if (op.RegisterSize == RegisterSize.Simd64)
            {
                mask = context.VectorZeroUpper64(mask);
            }

            context.Copy(GetVec(op.Rd), mask);
        }

        private static void EmitVectorTranspose(EmitterContext context, int part)
        {
            OpCodeSimdReg op = (OpCodeSimdReg)context.CurrOp;

            if (Optimizations.UseSsse3)
            {
                Operand mask = null;

                if (op.Size < 3)
                {
                    long maskE0 = _masksE0_TrnUzpXtn[op.Size];
                    long maskE1 = _masksE1_TrnUzp   [op.Size];

                    mask = X86GetScalar(context, maskE0);

                    mask = EmitVectorInsert(context, mask, Const(maskE1), 1, 3);
                }

                Operand n = GetVec(op.Rn);

                if (op.Size < 3)
                {
                    n = context.AddIntrinsic(Instruction.X86Pshufb, n, mask);
                }

                Operand m = GetVec(op.Rm);

                if (op.Size < 3)
                {
                    m = context.AddIntrinsic(Instruction.X86Pshufb, m, mask);
                }

                Instruction punpckInst = part == 0
                    ? X86PunpcklInstruction[op.Size]
                    : X86PunpckhInstruction[op.Size];

                Operand res = context.AddIntrinsic(punpckInst, n, m);

                if (op.RegisterSize == RegisterSize.Simd64)
                {
                    res = context.VectorZeroUpper64(res);
                }

                context.Copy(GetVec(op.Rd), res);
            }
            else
            {
                Operand res = context.VectorZero();

                int pairs = op.GetPairsCount() >> op.Size;

                for (int index = 0; index < pairs; index++)
                {
                    int pairIndex = index << 1;

                    Operand ne = EmitVectorExtractZx(context, op.Rn, pairIndex + part, op.Size);
                    Operand me = EmitVectorExtractZx(context, op.Rm, pairIndex + part, op.Size);

                    res = EmitVectorInsert(context, res, ne, pairIndex,     op.Size);
                    res = EmitVectorInsert(context, res, me, pairIndex + 1, op.Size);
                }

                context.Copy(GetVec(op.Rd), res);
            }
        }

        private static void EmitVectorUnzip(EmitterContext context, int part)
        {
            OpCodeSimdReg op = (OpCodeSimdReg)context.CurrOp;

            if (Optimizations.UseSsse3)
            {
                if (op.RegisterSize == RegisterSize.Simd128)
                {
                    Operand mask = null;

                    if (op.Size < 3)
                    {
                        long maskE0 = _masksE0_TrnUzpXtn[op.Size];
                        long maskE1 = _masksE1_TrnUzp   [op.Size];

                        mask = X86GetScalar(context, maskE0);

                        mask = EmitVectorInsert(context, mask, Const(maskE1), 1, 3);
                    }

                    Operand n = GetVec(op.Rn);

                    if (op.Size < 3)
                    {
                        n = context.AddIntrinsic(Instruction.X86Pshufb, n, mask);
                    }

                    Operand m = GetVec(op.Rm);

                    if (op.Size < 3)
                    {
                        m = context.AddIntrinsic(Instruction.X86Pshufb, m, mask);
                    }

                    Instruction punpckInst = part == 0
                        ? Instruction.X86Punpcklqdq
                        : Instruction.X86Punpckhqdq;

                    Operand res = context.AddIntrinsic(punpckInst, n, m);

                    context.Copy(GetVec(op.Rd), res);
                }
                else
                {
                    Operand n = GetVec(op.Rn);
                    Operand m = GetVec(op.Rm);

                    Instruction punpcklInst = X86PunpcklInstruction[op.Size];

                    Operand res = context.AddIntrinsic(punpcklInst, n, m);

                    if (op.Size < 2)
                    {
                        long maskE0 = _masksE0_Uzp[op.Size];
                        long maskE1 = _masksE1_Uzp[op.Size];

                        Operand mask = X86GetScalar(context, maskE0);

                        mask = EmitVectorInsert(context, mask, Const(maskE1), 1, 3);

                        res = context.AddIntrinsic(Instruction.X86Pshufb, res, mask);
                    }

                    Instruction punpckInst = part == 0
                        ? Instruction.X86Punpcklqdq
                        : Instruction.X86Punpckhqdq;

                    res = context.AddIntrinsic(punpckInst, res, context.VectorZero());

                    context.Copy(GetVec(op.Rd), res);
                }
            }
            else
            {
                Operand res = context.VectorZero();

                int pairs = op.GetPairsCount() >> op.Size;

                for (int index = 0; index < pairs; index++)
                {
                    int idx = index << 1;

                    Operand ne = EmitVectorExtractZx(context, op.Rn, idx + part, op.Size);
                    Operand me = EmitVectorExtractZx(context, op.Rm, idx + part, op.Size);

                    res = EmitVectorInsert(context, res, ne,         index, op.Size);
                    res = EmitVectorInsert(context, res, me, pairs + index, op.Size);
                }

                context.Copy(GetVec(op.Rd), res);
            }
        }

        private static void EmitVectorZip(EmitterContext context, int part)
        {
            OpCodeSimdReg op = (OpCodeSimdReg)context.CurrOp;

            if (Optimizations.UseSse2)
            {
                Operand n = GetVec(op.Rn);
                Operand m = GetVec(op.Rm);

                if (op.RegisterSize == RegisterSize.Simd128)
                {
                    Instruction punpckInst = part == 0
                        ? X86PunpcklInstruction[op.Size]
                        : X86PunpckhInstruction[op.Size];

                    Operand res = context.AddIntrinsic(punpckInst, n, m);

                    context.Copy(GetVec(op.Rd), res);
                }
                else
                {
                    Operand res = context.AddIntrinsic(X86PunpcklInstruction[op.Size], n, m);

                    Instruction punpckInst = part == 0
                        ? Instruction.X86Punpcklqdq
                        : Instruction.X86Punpckhqdq;

                    res = context.AddIntrinsic(punpckInst, res, context.VectorZero());

                    context.Copy(GetVec(op.Rd), res);
                }
            }
            else
            {
                Operand res = context.VectorZero();

                int pairs = op.GetPairsCount() >> op.Size;

                int baseIndex = part != 0 ? pairs : 0;

                for (int index = 0; index < pairs; index++)
                {
                    int pairIndex = index << 1;

                    Operand ne = EmitVectorExtractZx(context, op.Rn, baseIndex + index, op.Size);
                    Operand me = EmitVectorExtractZx(context, op.Rm, baseIndex + index, op.Size);

                    res = EmitVectorInsert(context, res, ne, pairIndex,     op.Size);
                    res = EmitVectorInsert(context, res, me, pairIndex + 1, op.Size);
                }

                context.Copy(GetVec(op.Rd), res);
            }
        }
    }
}