using ChocolArm64.Decoder;
using ChocolArm64.State;
using ChocolArm64.Translation;
using System;
using System.Reflection.Emit;

using static ChocolArm64.Instruction.AInstEmitSimdHelper;

namespace ChocolArm64.Instruction
{
    static partial class AInstEmit
    {
        public static void Dup_Gp(AILEmitterCtx Context)
        {
            AOpCodeSimdIns Op = (AOpCodeSimdIns)Context.CurrOp;

            int Bytes = Context.CurrOp.GetBitsCount() >> 3;

            for (int Index = 0; Index < (Bytes >> Op.Size); Index++)
            {
                Context.EmitLdintzr(Op.Rn);

                EmitVectorInsert(Context, Op.Rd, Index, Op.Size);
            }

            if (Op.RegisterSize == ARegisterSize.SIMD64)
            {
                EmitVectorZeroUpper(Context, Op.Rd);
            }
        }

        public static void Dup_S(AILEmitterCtx Context)
        {
            AOpCodeSimdIns Op = (AOpCodeSimdIns)Context.CurrOp;

            EmitVectorExtractZx(Context, Op.Rn, Op.DstIndex, Op.Size);

            EmitScalarSet(Context, Op.Rd, Op.Size);
        }

        public static void Dup_V(AILEmitterCtx Context)
        {
            AOpCodeSimdIns Op = (AOpCodeSimdIns)Context.CurrOp;

            int Bytes = Context.CurrOp.GetBitsCount() >> 3;

            for (int Index = 0; Index < (Bytes >> Op.Size); Index++)
            {
                EmitVectorExtractZx(Context, Op.Rn, Op.DstIndex, Op.Size);

                EmitVectorInsert(Context, Op.Rd, Index, Op.Size);
            }

            if (Op.RegisterSize == ARegisterSize.SIMD64)
            {
                EmitVectorZeroUpper(Context, Op.Rd);
            }
        }

        public static void Fcsel_S(AILEmitterCtx Context)
        {
            AOpCodeSimdFcond Op = (AOpCodeSimdFcond)Context.CurrOp;

            AILLabel LblTrue = new AILLabel();
            AILLabel LblEnd  = new AILLabel();

            Context.EmitCondBranch(LblTrue, Op.Cond);

            EmitVectorExtractF(Context, Op.Rm, 0, Op.Size);

            Context.Emit(OpCodes.Br_S, LblEnd);

            Context.MarkLabel(LblTrue);

            EmitVectorExtractF(Context, Op.Rn, 0, Op.Size);

            Context.MarkLabel(LblEnd);

            EmitScalarSetF(Context, Op.Rd, Op.Size);
        }

        public static void Fmov_Ftoi(AILEmitterCtx Context)
        {
            AOpCodeSimdCvt Op = (AOpCodeSimdCvt)Context.CurrOp;

            EmitVectorExtractZx(Context, Op.Rn, 0, 3);

            EmitIntZeroHigherIfNeeded(Context);

            Context.EmitStintzr(Op.Rd);
        }

        public static void Fmov_Ftoi1(AILEmitterCtx Context)
        {
            AOpCodeSimdCvt Op = (AOpCodeSimdCvt)Context.CurrOp;

            EmitVectorExtractZx(Context, Op.Rn, 1, 3);

            EmitIntZeroHigherIfNeeded(Context);

            Context.EmitStintzr(Op.Rd);
        }

        public static void Fmov_Itof(AILEmitterCtx Context)
        {
            AOpCodeSimdCvt Op = (AOpCodeSimdCvt)Context.CurrOp;

            Context.EmitLdintzr(Op.Rn);

            EmitIntZeroHigherIfNeeded(Context);

            EmitScalarSet(Context, Op.Rd, 3);
        }

        public static void Fmov_Itof1(AILEmitterCtx Context)
        {
            AOpCodeSimdCvt Op = (AOpCodeSimdCvt)Context.CurrOp;

            Context.EmitLdintzr(Op.Rn);

            EmitIntZeroHigherIfNeeded(Context);

            EmitVectorInsert(Context, Op.Rd, 1, 3);
        }

        public static void Fmov_S(AILEmitterCtx Context)
        {
            AOpCodeSimd Op = (AOpCodeSimd)Context.CurrOp;

            EmitVectorExtractF(Context, Op.Rn, 0, Op.Size);

            EmitScalarSetF(Context, Op.Rd, Op.Size);
        }

        public static void Fmov_Si(AILEmitterCtx Context)
        {
            AOpCodeSimdFmov Op = (AOpCodeSimdFmov)Context.CurrOp;

            Context.EmitLdc_I8(Op.Imm);

            EmitScalarSet(Context, Op.Rd, Op.Size + 2);
        }

        public static void Fmov_V(AILEmitterCtx Context)
        {
            AOpCodeSimdImm Op = (AOpCodeSimdImm)Context.CurrOp;

            int Elems = Op.RegisterSize == ARegisterSize.SIMD128 ? 4 : 2;

            for (int Index = 0; Index < (Elems >> Op.Size); Index++)
            {
                Context.EmitLdc_I8(Op.Imm);

                EmitVectorInsert(Context, Op.Rd, Index, Op.Size + 2);
            }

            if (Op.RegisterSize == ARegisterSize.SIMD64)
            {
                EmitVectorZeroUpper(Context, Op.Rd);
            }
        }

        public static void Ins_Gp(AILEmitterCtx Context)
        {
            AOpCodeSimdIns Op = (AOpCodeSimdIns)Context.CurrOp;

            Context.EmitLdintzr(Op.Rn);

            EmitVectorInsert(Context, Op.Rd, Op.DstIndex, Op.Size);
        }

        public static void Ins_V(AILEmitterCtx Context)
        {
            AOpCodeSimdIns Op = (AOpCodeSimdIns)Context.CurrOp;

            EmitVectorExtractZx(Context, Op.Rn, Op.SrcIndex, Op.Size);

            EmitVectorInsert(Context, Op.Rd, Op.DstIndex, Op.Size);
        }

        public static void Movi_V(AILEmitterCtx Context)
        {
            EmitVectorImmUnaryOp(Context, () => { });
        }

        public static void Mvni_V(AILEmitterCtx Context)
        {
            EmitVectorImmUnaryOp(Context, () => Context.Emit(OpCodes.Not));
        }

        public static void Tbl_V(AILEmitterCtx Context)
        {
            AOpCodeSimdTbl Op = (AOpCodeSimdTbl)Context.CurrOp;

            Context.EmitLdvec(Op.Rm);

            for (int Index = 0; Index < Op.Size; Index++)
            {
                Context.EmitLdvec((Op.Rn + Index) & 0x1f);
            }

            switch (Op.Size)
            {
                case 1: ASoftFallback.EmitCall(Context,
                    nameof(ASoftFallback.Tbl1_V64),
                    nameof(ASoftFallback.Tbl1_V128)); break;

                case 2: ASoftFallback.EmitCall(Context,
                    nameof(ASoftFallback.Tbl2_V64),
                    nameof(ASoftFallback.Tbl2_V128)); break;

                case 3: ASoftFallback.EmitCall(Context,
                    nameof(ASoftFallback.Tbl3_V64),
                    nameof(ASoftFallback.Tbl3_V128)); break;

                case 4: ASoftFallback.EmitCall(Context,
                    nameof(ASoftFallback.Tbl4_V64),
                    nameof(ASoftFallback.Tbl4_V128)); break;

                default: throw new InvalidOperationException();
            }

            Context.EmitStvec(Op.Rd);
        }

        public static void Umov_S(AILEmitterCtx Context)
        {
            AOpCodeSimdIns Op = (AOpCodeSimdIns)Context.CurrOp;

            EmitVectorExtractZx(Context, Op.Rn, Op.DstIndex, Op.Size);

            Context.EmitStintzr(Op.Rd);
        }

        public static void Uzp1_V(AILEmitterCtx Context)
        {
            EmitVectorUnzip(Context, Part: 0);
        }

        public static void Uzp2_V(AILEmitterCtx Context)
        {
            EmitVectorUnzip(Context, Part: 1);
        }

        public static void Xtn_V(AILEmitterCtx Context)
        {
            AOpCodeSimd Op = (AOpCodeSimd)Context.CurrOp;

            int Elems = 8 >> Op.Size;

            int Part = Op.RegisterSize == ARegisterSize.SIMD128 ? Elems : 0;

            for (int Index = 0; Index < Elems; Index++)
            {
                EmitVectorExtractZx(Context, Op.Rn, Index, Op.Size + 1);

                EmitVectorInsert(Context, Op.Rd, Part + Index, Op.Size);
            }

            if (Part == 0)
            {
                EmitVectorZeroUpper(Context, Op.Rd);
            }
        }

        public static void Zip1_V(AILEmitterCtx Context)
        {
            EmitVectorZip(Context, Part: 0);
        }

        public static void Zip2_V(AILEmitterCtx Context)
        {
            EmitVectorZip(Context, Part: 1);
        }

        private static void EmitIntZeroHigherIfNeeded(AILEmitterCtx Context)
        {
            if (Context.CurrOp.RegisterSize == ARegisterSize.Int32)
            {
                Context.Emit(OpCodes.Conv_U4);
                Context.Emit(OpCodes.Conv_U8);
            }
        }

        private static void EmitVectorUnzip(AILEmitterCtx Context, int Part)
        {
            AOpCodeSimdReg Op = (AOpCodeSimdReg)Context.CurrOp;

            int Bytes = Context.CurrOp.GetBitsCount() >> 3;

            int Elems = Bytes >> Op.Size;
            int Half  = Elems >> 1;

            for (int Index = 0; Index < Elems; Index++)
            {
                int Elem = Part + ((Index & (Half - 1)) << 1);
                
                EmitVectorExtractZx(Context, Index < Half ? Op.Rn : Op.Rm, Elem, Op.Size);

                EmitVectorInsert(Context, Op.Rd, Index, Op.Size);
            }

            if (Op.RegisterSize == ARegisterSize.SIMD64)
            {
                EmitVectorZeroUpper(Context, Op.Rd);
            }
        }

        private static void EmitVectorZip(AILEmitterCtx Context, int Part)
        {
            AOpCodeSimdReg Op = (AOpCodeSimdReg)Context.CurrOp;

            int Bytes = Context.CurrOp.GetBitsCount() >> 3;

            int Elems = Bytes >> Op.Size;
            int Half  = Elems >> 1;

            for (int Index = 0; Index < Elems; Index++)
            {
                int Elem = Part * Half + (Index >> 1);

                EmitVectorExtractZx(Context, (Index & 1) == 0 ? Op.Rn : Op.Rm, Elem, Op.Size);

                EmitVectorInsert(Context, Op.Rd, Index, Op.Size);
            }

            if (Op.RegisterSize == ARegisterSize.SIMD64)
            {
                EmitVectorZeroUpper(Context, Op.Rd);
            }
        }
    }
}