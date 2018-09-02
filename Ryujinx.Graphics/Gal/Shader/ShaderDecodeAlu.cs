using System;

using static Ryujinx.Graphics.Gal.Shader.ShaderDecodeHelper;

namespace Ryujinx.Graphics.Gal.Shader
{
    static partial class ShaderDecode
    {
        public static void Bfe_C(ShaderIrBlock Block, long OpCode, long Position)
        {
            EmitBfe(Block, OpCode, ShaderOper.CR);
        }

        public static void Bfe_I(ShaderIrBlock Block, long OpCode, long Position)
        {
            EmitBfe(Block, OpCode, ShaderOper.Imm);
        }

        public static void Bfe_R(ShaderIrBlock Block, long OpCode, long Position)
        {
            EmitBfe(Block, OpCode, ShaderOper.RR);
        }

        public static void Fadd_C(ShaderIrBlock Block, long OpCode, long Position)
        {
            EmitFadd(Block, OpCode, ShaderOper.CR);
        }

        public static void Fadd_I(ShaderIrBlock Block, long OpCode, long Position)
        {
            EmitFadd(Block, OpCode, ShaderOper.Immf);
        }

        public static void Fadd_I32(ShaderIrBlock Block, long OpCode, long Position)
        {
            ShaderIrNode OperA = GetOperGpr8     (OpCode);
            ShaderIrNode OperB = GetOperImmf32_20(OpCode);

            bool NegB = ((OpCode >> 53) & 1) != 0;
            bool AbsA = ((OpCode >> 54) & 1) != 0;
            bool NegA = ((OpCode >> 56) & 1) != 0;
            bool AbsB = ((OpCode >> 57) & 1) != 0;

            OperA = GetAluFabsFneg(OperA, AbsA, NegA);
            OperB = GetAluFabsFneg(OperB, AbsB, NegB);

            ShaderIrOp Op = new ShaderIrOp(ShaderIrInst.Fadd, OperA, OperB);

            Block.AddNode(GetPredNode(new ShaderIrAsg(GetOperGpr0(OpCode), Op), OpCode));
        }

        public static void Fadd_R(ShaderIrBlock Block, long OpCode, long Position)
        {
            EmitFadd(Block, OpCode, ShaderOper.RR);
        }

        public static void Ffma_CR(ShaderIrBlock Block, long OpCode, long Position)
        {
            EmitFfma(Block, OpCode, ShaderOper.CR);
        }

        public static void Ffma_I(ShaderIrBlock Block, long OpCode, long Position)
        {
            EmitFfma(Block, OpCode, ShaderOper.Immf);
        }

        public static void Ffma_RC(ShaderIrBlock Block, long OpCode, long Position)
        {
            EmitFfma(Block, OpCode, ShaderOper.RC);
        }

        public static void Ffma_RR(ShaderIrBlock Block, long OpCode, long Position)
        {
            EmitFfma(Block, OpCode, ShaderOper.RR);
        }

        public static void Fmnmx_C(ShaderIrBlock Block, long OpCode, long Position)
        {
            EmitFmnmx(Block, OpCode, ShaderOper.CR);
        }

        public static void Fmnmx_I(ShaderIrBlock Block, long OpCode, long Position)
        {
            EmitFmnmx(Block, OpCode, ShaderOper.Immf);
        }

        public static void Fmnmx_R(ShaderIrBlock Block, long OpCode, long Position)
        {
            EmitFmnmx(Block, OpCode, ShaderOper.RR);
        }

        public static void Fmul_I32(ShaderIrBlock Block, long OpCode, long Position)
        {
            ShaderIrNode OperA = GetOperGpr8     (OpCode);
            ShaderIrNode OperB = GetOperImmf32_20(OpCode);

            ShaderIrOp Op = new ShaderIrOp(ShaderIrInst.Fmul, OperA, OperB);

            Block.AddNode(GetPredNode(new ShaderIrAsg(GetOperGpr0(OpCode), Op), OpCode));
        }

        public static void Fmul_C(ShaderIrBlock Block, long OpCode, long Position)
        {
            EmitFmul(Block, OpCode, ShaderOper.CR);
        }

        public static void Fmul_I(ShaderIrBlock Block, long OpCode, long Position)
        {
            EmitFmul(Block, OpCode, ShaderOper.Immf);
        }

        public static void Fmul_R(ShaderIrBlock Block, long OpCode, long Position)
        {
            EmitFmul(Block, OpCode, ShaderOper.RR);
        }

        public static void Fset_C(ShaderIrBlock Block, long OpCode, long Position)
        {
            EmitFset(Block, OpCode, ShaderOper.CR);
        }

        public static void Fset_I(ShaderIrBlock Block, long OpCode, long Position)
        {
            EmitFset(Block, OpCode, ShaderOper.Immf);
        }

        public static void Fset_R(ShaderIrBlock Block, long OpCode, long Position)
        {
            EmitFset(Block, OpCode, ShaderOper.RR);
        }

        public static void Fsetp_C(ShaderIrBlock Block, long OpCode, long Position)
        {
            EmitFsetp(Block, OpCode, ShaderOper.CR);
        }

        public static void Fsetp_I(ShaderIrBlock Block, long OpCode, long Position)
        {
            EmitFsetp(Block, OpCode, ShaderOper.Immf);
        }

        public static void Fsetp_R(ShaderIrBlock Block, long OpCode, long Position)
        {
            EmitFsetp(Block, OpCode, ShaderOper.RR);
        }

        public static void Iadd_C(ShaderIrBlock Block, long OpCode, long Position)
        {
            EmitIadd(Block, OpCode, ShaderOper.CR);
        }

        public static void Iadd_I(ShaderIrBlock Block, long OpCode, long Position)
        {
            EmitIadd(Block, OpCode, ShaderOper.Imm);
        }

        public static void Iadd_I32(ShaderIrBlock Block, long OpCode, long Position)
        {
            ShaderIrNode OperA = GetOperGpr8    (OpCode);
            ShaderIrNode OperB = GetOperImm32_20(OpCode);

            bool NegA = ((OpCode >> 56) & 1) != 0;

            OperA = GetAluIneg(OperA, NegA);

            ShaderIrOp Op = new ShaderIrOp(ShaderIrInst.Add, OperA, OperB);

            Block.AddNode(GetPredNode(new ShaderIrAsg(GetOperGpr0(OpCode), Op), OpCode));
        }

        public static void Iadd_R(ShaderIrBlock Block, long OpCode, long Position)
        {
            EmitIadd(Block, OpCode, ShaderOper.RR);
        }

        public static void Iadd3_C(ShaderIrBlock Block, long OpCode, long Position)
        {
            EmitIadd3(Block, OpCode, ShaderOper.CR);
        }

        public static void Iadd3_I(ShaderIrBlock Block, long OpCode, long Position)
        {
            EmitIadd3(Block, OpCode, ShaderOper.Imm);
        }

        public static void Iadd3_R(ShaderIrBlock Block, long OpCode, long Position)
        {
            EmitIadd3(Block, OpCode, ShaderOper.RR);
        }

        public static void Imnmx_C(ShaderIrBlock Block, long OpCode, long Position)
        {
            EmitImnmx(Block, OpCode, ShaderOper.CR);
        }

        public static void Imnmx_I(ShaderIrBlock Block, long OpCode, long Position)
        {
            EmitImnmx(Block, OpCode, ShaderOper.Imm);
        }

        public static void Imnmx_R(ShaderIrBlock Block, long OpCode, long Position)
        {
            EmitImnmx(Block, OpCode, ShaderOper.RR);
        }

        public static void Ipa(ShaderIrBlock Block, long OpCode, long Position)
        {
            ShaderIrNode OperA = GetOperAbuf28(OpCode);
            ShaderIrNode OperB = GetOperGpr20 (OpCode);

            ShaderIpaMode Mode = (ShaderIpaMode)((OpCode >> 54) & 3);

            ShaderIrMetaIpa Meta = new ShaderIrMetaIpa(Mode);

            ShaderIrOp Op = new ShaderIrOp(ShaderIrInst.Ipa, OperA, OperB, null, Meta);

            Block.AddNode(GetPredNode(new ShaderIrAsg(GetOperGpr0(OpCode), Op), OpCode));
        }

        public static void Iscadd_C(ShaderIrBlock Block, long OpCode, long Position)
        {
            EmitIscadd(Block, OpCode, ShaderOper.CR);
        }

        public static void Iscadd_I(ShaderIrBlock Block, long OpCode, long Position)
        {
            EmitIscadd(Block, OpCode, ShaderOper.Imm);
        }

        public static void Iscadd_R(ShaderIrBlock Block, long OpCode, long Position)
        {
            EmitIscadd(Block, OpCode, ShaderOper.RR);
        }

        public static void Iset_C(ShaderIrBlock Block, long OpCode, long Position)
        {
            EmitIset(Block, OpCode, ShaderOper.CR);
        }

        public static void Iset_I(ShaderIrBlock Block, long OpCode, long Position)
        {
            EmitIset(Block, OpCode, ShaderOper.Imm);
        }

        public static void Iset_R(ShaderIrBlock Block, long OpCode, long Position)
        {
            EmitIset(Block, OpCode, ShaderOper.RR);
        }

        public static void Isetp_C(ShaderIrBlock Block, long OpCode, long Position)
        {
            EmitIsetp(Block, OpCode, ShaderOper.CR);
        }

        public static void Isetp_I(ShaderIrBlock Block, long OpCode, long Position)
        {
            EmitIsetp(Block, OpCode, ShaderOper.Imm);
        }

        public static void Isetp_R(ShaderIrBlock Block, long OpCode, long Position)
        {
            EmitIsetp(Block, OpCode, ShaderOper.RR);
        }

        public static void Lop_I32(ShaderIrBlock Block, long OpCode, long Position)
        {
            int SubOp = (int)(OpCode >> 53) & 3;

            bool InvA = ((OpCode >> 55) & 1) != 0;
            bool InvB = ((OpCode >> 56) & 1) != 0;

            ShaderIrInst Inst = 0;

            switch (SubOp)
            {
                case 0: Inst = ShaderIrInst.And; break;
                case 1: Inst = ShaderIrInst.Or;  break;
                case 2: Inst = ShaderIrInst.Xor; break;
            }

            ShaderIrNode OperB = GetAluNot(GetOperImm32_20(OpCode), InvB);

            //SubOp == 3 is pass, used by the not instruction
            //which just moves the inverted register value.
            if (SubOp < 3)
            {
                ShaderIrNode OperA = GetAluNot(GetOperGpr8(OpCode), InvA);

                ShaderIrOp Op = new ShaderIrOp(Inst, OperA, OperB);

                Block.AddNode(GetPredNode(new ShaderIrAsg(GetOperGpr0(OpCode), Op), OpCode));
            }
            else
            {
                Block.AddNode(GetPredNode(new ShaderIrAsg(GetOperGpr0(OpCode), OperB), OpCode));
            }
        }

        public static void Lop_C(ShaderIrBlock Block, long OpCode, long Position)
        {
            EmitLop(Block, OpCode, ShaderOper.CR);
        }

        public static void Lop_I(ShaderIrBlock Block, long OpCode, long Position)
        {
            EmitLop(Block, OpCode, ShaderOper.Imm);
        }

        public static void Lop_R(ShaderIrBlock Block, long OpCode, long Position)
        {
            EmitLop(Block, OpCode, ShaderOper.RR);
        }

        public static void Mufu(ShaderIrBlock Block, long OpCode, long Position)
        {
            int SubOp = (int)(OpCode >> 20) & 0xf;

            bool AbsA = ((OpCode >> 46) & 1) != 0;
            bool NegA = ((OpCode >> 48) & 1) != 0;

            ShaderIrInst Inst = 0;

            switch (SubOp)
            {
                case 0: Inst = ShaderIrInst.Fcos;  break;
                case 1: Inst = ShaderIrInst.Fsin;  break;
                case 2: Inst = ShaderIrInst.Fex2;  break;
                case 3: Inst = ShaderIrInst.Flg2;  break;
                case 4: Inst = ShaderIrInst.Frcp;  break;
                case 5: Inst = ShaderIrInst.Frsq;  break;
                case 8: Inst = ShaderIrInst.Fsqrt; break;

                default: throw new NotImplementedException(SubOp.ToString());
            }

            ShaderIrNode OperA = GetOperGpr8(OpCode);

            ShaderIrOp Op = new ShaderIrOp(Inst, GetAluFabsFneg(OperA, AbsA, NegA));

            Block.AddNode(GetPredNode(new ShaderIrAsg(GetOperGpr0(OpCode), Op), OpCode));
        }

        public static void Psetp(ShaderIrBlock Block, long OpCode, long Position)
        {
            bool NegA = ((OpCode >> 15) & 1) != 0;
            bool NegB = ((OpCode >> 32) & 1) != 0;
            bool NegP = ((OpCode >> 42) & 1) != 0;

            ShaderIrInst LopInst = GetBLop24(OpCode);

            ShaderIrNode OperA = GetOperPred12(OpCode);
            ShaderIrNode OperB = GetOperPred29(OpCode);

            if (NegA)
            {
                OperA = new ShaderIrOp(ShaderIrInst.Bnot, OperA);
            }

            if (NegB)
            {
                OperB = new ShaderIrOp(ShaderIrInst.Bnot, OperB);
            }

            ShaderIrOp Op = new ShaderIrOp(LopInst, OperA, OperB);

            ShaderIrOperPred P0Node = GetOperPred3 (OpCode);
            ShaderIrOperPred P1Node = GetOperPred0 (OpCode);
            ShaderIrOperPred P2Node = GetOperPred39(OpCode);

            Block.AddNode(GetPredNode(new ShaderIrAsg(P0Node, Op), OpCode));

            LopInst = GetBLop45(OpCode);

            if (LopInst == ShaderIrInst.Band && P1Node.IsConst && P2Node.IsConst)
            {
                return;
            }

            ShaderIrNode P2NNode = P2Node;

            if (NegP)
            {
                P2NNode = new ShaderIrOp(ShaderIrInst.Bnot, P2NNode);
            }

            Op = new ShaderIrOp(ShaderIrInst.Bnot, P0Node);

            Op = new ShaderIrOp(LopInst, Op, P2NNode);

            Block.AddNode(GetPredNode(new ShaderIrAsg(P1Node, Op), OpCode));

            Op = new ShaderIrOp(LopInst, P0Node, P2NNode);

            Block.AddNode(GetPredNode(new ShaderIrAsg(P0Node, Op), OpCode));
        }

        public static void Rro_C(ShaderIrBlock Block, long OpCode, long Position)
        {
            EmitRro(Block, OpCode, ShaderOper.CR);
        }

        public static void Rro_I(ShaderIrBlock Block, long OpCode, long Position)
        {
            EmitRro(Block, OpCode, ShaderOper.Immf);
        }

        public static void Rro_R(ShaderIrBlock Block, long OpCode, long Position)
        {
            EmitRro(Block, OpCode, ShaderOper.RR);
        }

        public static void Shl_C(ShaderIrBlock Block, long OpCode, long Position)
        {
            EmitAluBinary(Block, OpCode, ShaderOper.CR, ShaderIrInst.Lsl);
        }

        public static void Shl_I(ShaderIrBlock Block, long OpCode, long Position)
        {
            EmitAluBinary(Block, OpCode, ShaderOper.Imm, ShaderIrInst.Lsl);
        }

        public static void Shl_R(ShaderIrBlock Block, long OpCode, long Position)
        {
            EmitAluBinary(Block, OpCode, ShaderOper.RR, ShaderIrInst.Lsl);
        }

        public static void Shr_C(ShaderIrBlock Block, long OpCode, long Position)
        {
            EmitAluBinary(Block, OpCode, ShaderOper.CR, GetShrInst(OpCode));
        }

        public static void Shr_I(ShaderIrBlock Block, long OpCode, long Position)
        {
            EmitAluBinary(Block, OpCode, ShaderOper.Imm, GetShrInst(OpCode));
        }

        public static void Shr_R(ShaderIrBlock Block, long OpCode, long Position)
        {
            EmitAluBinary(Block, OpCode, ShaderOper.RR, GetShrInst(OpCode));
        }

        private static ShaderIrInst GetShrInst(long OpCode)
        {
            bool Signed = ((OpCode >> 48) & 1) != 0;

            return Signed ? ShaderIrInst.Asr : ShaderIrInst.Lsr;
        }

        public static void Vmad(ShaderIrBlock Block, long OpCode, long Position)
        {
            ShaderIrNode OperA = GetOperGpr8(OpCode);

            ShaderIrNode OperB;

            if (((OpCode >> 50) & 1) != 0)
            {
                OperB = GetOperGpr20(OpCode);
            }
            else
            {
                OperB = GetOperImm19_20(OpCode);
            }

            ShaderIrOperGpr OperC = GetOperGpr39(OpCode);

            ShaderIrNode Tmp = new ShaderIrOp(ShaderIrInst.Mul, OperA, OperB);

            ShaderIrNode Final = new ShaderIrOp(ShaderIrInst.Add, Tmp, OperC);

            int Shr = (int)((OpCode >> 51) & 3);

            if (Shr != 0)
            {
                int Shift = (Shr == 2) ? 15 : 7;

                Final = new ShaderIrOp(ShaderIrInst.Lsr, Final, new ShaderIrOperImm(Shift));
            }

            Block.AddNode(new ShaderIrCmnt("Stubbed. Instruction is reduced to a * b + c"));

            Block.AddNode(GetPredNode(new ShaderIrAsg(GetOperGpr0(OpCode), Final), OpCode));
        }

        public static void Xmad_CR(ShaderIrBlock Block, long OpCode, long Position)
        {
            EmitXmad(Block, OpCode, ShaderOper.CR);
        }

        public static void Xmad_I(ShaderIrBlock Block, long OpCode, long Position)
        {
            EmitXmad(Block, OpCode, ShaderOper.Imm);
        }

        public static void Xmad_RC(ShaderIrBlock Block, long OpCode, long Position)
        {
            EmitXmad(Block, OpCode, ShaderOper.RC);
        }

        public static void Xmad_RR(ShaderIrBlock Block, long OpCode, long Position)
        {
            EmitXmad(Block, OpCode, ShaderOper.RR);
        }

        private static void EmitAluBinary(
            ShaderIrBlock Block,
            long          OpCode,
            ShaderOper    Oper,
            ShaderIrInst  Inst)
        {
            ShaderIrNode OperA = GetOperGpr8(OpCode), OperB;

            switch (Oper)
            {
                case ShaderOper.CR:  OperB = GetOperCbuf34  (OpCode); break;
                case ShaderOper.Imm: OperB = GetOperImm19_20(OpCode); break;
                case ShaderOper.RR:  OperB = GetOperGpr20   (OpCode); break;

                default: throw new ArgumentException(nameof(Oper));
            }

            ShaderIrNode Op = new ShaderIrOp(Inst, OperA, OperB);

            Block.AddNode(GetPredNode(new ShaderIrAsg(GetOperGpr0(OpCode), Op), OpCode));
        }

        private static void EmitBfe(ShaderIrBlock Block, long OpCode, ShaderOper Oper)
        {
            //TODO: Handle the case where position + length
            //is greater than the word size, in this case the sign bit
            //needs to be replicated to fill the remaining space.
            bool NegB = ((OpCode >> 48) & 1) != 0;
            bool NegA = ((OpCode >> 49) & 1) != 0;

            ShaderIrNode OperA = GetOperGpr8(OpCode), OperB;

            switch (Oper)
            {
                case ShaderOper.CR:  OperB = GetOperCbuf34  (OpCode); break;
                case ShaderOper.Imm: OperB = GetOperImm19_20(OpCode); break;
                case ShaderOper.RR:  OperB = GetOperGpr20   (OpCode); break;

                default: throw new ArgumentException(nameof(Oper));
            }

            ShaderIrNode Op;

            bool Signed = ((OpCode >> 48) & 1) != 0; //?

            if (OperB is ShaderIrOperImm PosLen)
            {
                int Position = (PosLen.Value >> 0) & 0xff;
                int Length   = (PosLen.Value >> 8) & 0xff;

                int LSh = 32 - (Position + Length);

                ShaderIrInst RightShift = Signed
                    ? ShaderIrInst.Asr
                    : ShaderIrInst.Lsr;

                Op = new ShaderIrOp(ShaderIrInst.Lsl, OperA, new ShaderIrOperImm(LSh));
                Op = new ShaderIrOp(RightShift,       Op,    new ShaderIrOperImm(LSh + Position));
            }
            else
            {
                ShaderIrOperImm Shift = new ShaderIrOperImm(8);
                ShaderIrOperImm Mask  = new ShaderIrOperImm(0xff);

                ShaderIrNode OpPos, OpLen;

                OpPos = new ShaderIrOp(ShaderIrInst.And, OperB, Mask);
                OpLen = new ShaderIrOp(ShaderIrInst.Lsr, OperB, Shift);
                OpLen = new ShaderIrOp(ShaderIrInst.And, OpLen, Mask);

                Op = new ShaderIrOp(ShaderIrInst.Lsr, OperA, OpPos);

                Op = ExtendTo32(Op, Signed, OpLen);
            }

            Block.AddNode(GetPredNode(new ShaderIrAsg(GetOperGpr0(OpCode), Op), OpCode));
        }

        private static void EmitFadd(ShaderIrBlock Block, long OpCode, ShaderOper Oper)
        {
            bool NegB = ((OpCode >> 45) & 1) != 0;
            bool AbsA = ((OpCode >> 46) & 1) != 0;
            bool NegA = ((OpCode >> 48) & 1) != 0;
            bool AbsB = ((OpCode >> 49) & 1) != 0;

            ShaderIrNode OperA = GetOperGpr8(OpCode), OperB;

            OperA = GetAluFabsFneg(OperA, AbsA, NegA);

            switch (Oper)
            {
                case ShaderOper.CR:   OperB = GetOperCbuf34   (OpCode); break;
                case ShaderOper.Immf: OperB = GetOperImmf19_20(OpCode); break;
                case ShaderOper.RR:   OperB = GetOperGpr20    (OpCode); break;

                default: throw new ArgumentException(nameof(Oper));
            }

            OperB = GetAluFabsFneg(OperB, AbsB, NegB);

            ShaderIrNode Op = new ShaderIrOp(ShaderIrInst.Fadd, OperA, OperB);

            Block.AddNode(GetPredNode(new ShaderIrAsg(GetOperGpr0(OpCode), Op), OpCode));
        }

        private static void EmitFmul(ShaderIrBlock Block, long OpCode, ShaderOper Oper)
        {
            bool NegB = ((OpCode >> 48) & 1) != 0;

            ShaderIrNode OperA = GetOperGpr8(OpCode), OperB;

            switch (Oper)
            {
                case ShaderOper.CR:   OperB = GetOperCbuf34   (OpCode); break;
                case ShaderOper.Immf: OperB = GetOperImmf19_20(OpCode); break;
                case ShaderOper.RR:   OperB = GetOperGpr20    (OpCode); break;

                default: throw new ArgumentException(nameof(Oper));
            }

            OperB = GetAluFneg(OperB, NegB);

            ShaderIrNode Op = new ShaderIrOp(ShaderIrInst.Fmul, OperA, OperB);

            Block.AddNode(GetPredNode(new ShaderIrAsg(GetOperGpr0(OpCode), Op), OpCode));
        }

        private static void EmitFfma(ShaderIrBlock Block, long OpCode, ShaderOper Oper)
        {
            bool NegB = ((OpCode >> 48) & 1) != 0;
            bool NegC = ((OpCode >> 49) & 1) != 0;

            ShaderIrNode OperA = GetOperGpr8(OpCode), OperB, OperC;

            switch (Oper)
            {
                case ShaderOper.CR:   OperB = GetOperCbuf34   (OpCode); break;
                case ShaderOper.Immf: OperB = GetOperImmf19_20(OpCode); break;
                case ShaderOper.RC:   OperB = GetOperGpr39    (OpCode); break;
                case ShaderOper.RR:   OperB = GetOperGpr20    (OpCode); break;

                default: throw new ArgumentException(nameof(Oper));
            }

            OperB = GetAluFneg(OperB, NegB);

            if (Oper == ShaderOper.RC)
            {
                OperC = GetAluFneg(GetOperCbuf34(OpCode), NegC);
            }
            else
            {
                OperC = GetAluFneg(GetOperGpr39(OpCode), NegC);
            }

            ShaderIrOp Op = new ShaderIrOp(ShaderIrInst.Ffma, OperA, OperB, OperC);

            Block.AddNode(GetPredNode(new ShaderIrAsg(GetOperGpr0(OpCode), Op), OpCode));
        }

        private static void EmitIadd(ShaderIrBlock Block, long OpCode, ShaderOper Oper)
        {
            ShaderIrNode OperA = GetOperGpr8(OpCode);
            ShaderIrNode OperB;

            switch (Oper)
            {
                case ShaderOper.CR:  OperB = GetOperCbuf34  (OpCode); break;
                case ShaderOper.Imm: OperB = GetOperImm19_20(OpCode); break;
                case ShaderOper.RR:  OperB = GetOperGpr20   (OpCode); break;

                default: throw new ArgumentException(nameof(Oper));
            }

            bool NegA = ((OpCode >> 49) & 1) != 0;
            bool NegB = ((OpCode >> 48) & 1) != 0;

            OperA = GetAluIneg(OperA, NegA);
            OperB = GetAluIneg(OperB, NegB);

            ShaderIrOp Op = new ShaderIrOp(ShaderIrInst.Add, OperA, OperB);

            Block.AddNode(GetPredNode(new ShaderIrAsg(GetOperGpr0(OpCode), Op), OpCode));
        }

        private static void EmitIadd3(ShaderIrBlock Block, long OpCode, ShaderOper Oper)
        {
            int Mode = (int)((OpCode >> 37) & 3);

            bool Neg1 = ((OpCode >> 51) & 1) != 0;
            bool Neg2 = ((OpCode >> 50) & 1) != 0;
            bool Neg3 = ((OpCode >> 49) & 1) != 0;

            int Height1 = (int)((OpCode >> 35) & 3);
            int Height2 = (int)((OpCode >> 33) & 3);
            int Height3 = (int)((OpCode >> 31) & 3);

            ShaderIrNode OperB;

            switch (Oper)
            {
                case ShaderOper.CR:  OperB = GetOperCbuf34  (OpCode); break;
                case ShaderOper.Imm: OperB = GetOperImm19_20(OpCode); break;
                case ShaderOper.RR:  OperB = GetOperGpr20   (OpCode); break;

                default: throw new ArgumentException(nameof(Oper));
            }

            ShaderIrNode ApplyHeight(ShaderIrNode Src, int Height)
            {
                if (Oper != ShaderOper.RR)
                {
                    return Src;
                }

                switch (Height)
                {
                    case 0: return Src;
                    case 1: return new ShaderIrOp(ShaderIrInst.And, Src, new ShaderIrOperImm(0xffff));
                    case 2: return new ShaderIrOp(ShaderIrInst.Lsr, Src, new ShaderIrOperImm(16));

                    default: throw new InvalidOperationException();
                }
            }

            ShaderIrNode Src1 = GetAluIneg(ApplyHeight(GetOperGpr8(OpCode),  Height1), Neg1);
            ShaderIrNode Src2 = GetAluIneg(ApplyHeight(OperB,                Height2), Neg2);
            ShaderIrNode Src3 = GetAluIneg(ApplyHeight(GetOperGpr39(OpCode), Height3), Neg3);

            ShaderIrOp Sum = new ShaderIrOp(ShaderIrInst.Add, Src1, Src2);

            if (Oper == ShaderOper.RR)
            {
                switch (Mode)
                {
                    case 1: Sum = new ShaderIrOp(ShaderIrInst.Lsr, Sum, new ShaderIrOperImm(16)); break;
                    case 2: Sum = new ShaderIrOp(ShaderIrInst.Lsl, Sum, new ShaderIrOperImm(16)); break;
                }
            }

            //Note: Here there should be a "+ 1" when carry flag is set
            //but since carry is mostly ignored by other instructions, it's excluded for now

            Block.AddNode(GetPredNode(new ShaderIrAsg(GetOperGpr0(OpCode), new ShaderIrOp(ShaderIrInst.Add, Sum, Src3)), OpCode));
        }

        private static void EmitIscadd(ShaderIrBlock Block, long OpCode, ShaderOper Oper)
        {
            bool NegB = ((OpCode >> 48) & 1) != 0;
            bool NegA = ((OpCode >> 49) & 1) != 0;

            ShaderIrNode OperA = GetOperGpr8(OpCode), OperB;

            ShaderIrOperImm Scale = GetOperImm5_39(OpCode);

            switch (Oper)
            {
                case ShaderOper.CR:  OperB = GetOperCbuf34  (OpCode); break;
                case ShaderOper.Imm: OperB = GetOperImm19_20(OpCode); break;
                case ShaderOper.RR:  OperB = GetOperGpr20   (OpCode); break;

                default: throw new ArgumentException(nameof(Oper));
            }

            OperA = GetAluIneg(OperA, NegA);
            OperB = GetAluIneg(OperB, NegB);

            ShaderIrOp ScaleOp = new ShaderIrOp(ShaderIrInst.Lsl, OperA, Scale);
            ShaderIrOp AddOp   = new ShaderIrOp(ShaderIrInst.Add, OperB, ScaleOp);

            Block.AddNode(GetPredNode(new ShaderIrAsg(GetOperGpr0(OpCode), AddOp), OpCode));
        }

        private static void EmitFmnmx(ShaderIrBlock Block, long OpCode, ShaderOper Oper)
        {
            EmitMnmx(Block, OpCode, true, Oper);
        }

        private static void EmitImnmx(ShaderIrBlock Block, long OpCode, ShaderOper Oper)
        {
            EmitMnmx(Block, OpCode, false, Oper);
        }

        private static void EmitMnmx(ShaderIrBlock Block, long OpCode, bool IsFloat, ShaderOper Oper)
        {
            bool NegB = ((OpCode >> 45) & 1) != 0;
            bool AbsA = ((OpCode >> 46) & 1) != 0;
            bool NegA = ((OpCode >> 48) & 1) != 0;
            bool AbsB = ((OpCode >> 49) & 1) != 0;

            ShaderIrNode OperA = GetOperGpr8(OpCode), OperB;

            if (IsFloat)
            {
                OperA = GetAluFabsFneg(OperA, AbsA, NegA);
            }
            else
            {
                OperA = GetAluIabsIneg(OperA, AbsA, NegA);
            }

            switch (Oper)
            {
                case ShaderOper.CR:   OperB = GetOperCbuf34   (OpCode); break;
                case ShaderOper.Imm:  OperB = GetOperImm19_20 (OpCode); break;
                case ShaderOper.Immf: OperB = GetOperImmf19_20(OpCode); break;
                case ShaderOper.RR:   OperB = GetOperGpr20    (OpCode); break;

                default: throw new ArgumentException(nameof(Oper));
            }

            if (IsFloat)
            {
                OperB = GetAluFabsFneg(OperB, AbsB, NegB);
            }
            else
            {
                OperB = GetAluIabsIneg(OperB, AbsB, NegB);
            }

            ShaderIrOperPred Pred = GetOperPred39(OpCode);

            ShaderIrOp Op;

            ShaderIrInst MaxInst = IsFloat ? ShaderIrInst.Fmax : ShaderIrInst.Max;
            ShaderIrInst MinInst = IsFloat ? ShaderIrInst.Fmin : ShaderIrInst.Min;

            if (Pred.IsConst)
            {
                bool IsMax = ((OpCode >> 42) & 1) != 0;

                Op = new ShaderIrOp(IsMax
                    ? MaxInst
                    : MinInst, OperA, OperB);

                Block.AddNode(GetPredNode(new ShaderIrAsg(GetOperGpr0(OpCode), Op), OpCode));
            }
            else
            {
                ShaderIrNode PredN = GetOperPred39N(OpCode);

                ShaderIrOp OpMax = new ShaderIrOp(MaxInst, OperA, OperB);
                ShaderIrOp OpMin = new ShaderIrOp(MinInst, OperA, OperB);

                ShaderIrAsg AsgMax = new ShaderIrAsg(GetOperGpr0(OpCode), OpMax);
                ShaderIrAsg AsgMin = new ShaderIrAsg(GetOperGpr0(OpCode), OpMin);

                Block.AddNode(GetPredNode(new ShaderIrCond(PredN, AsgMax, Not: true),  OpCode));
                Block.AddNode(GetPredNode(new ShaderIrCond(PredN, AsgMin, Not: false), OpCode));
            }
        }

        private static void EmitRro(ShaderIrBlock Block, long OpCode, ShaderOper Oper)
        {
            //Note: this is a range reduction instruction and is supposed to
            //be used with Mufu, here it just moves the value and ignores the operation.
            bool NegA = ((OpCode >> 45) & 1) != 0;
            bool AbsA = ((OpCode >> 49) & 1) != 0;

            ShaderIrNode OperA;

            switch (Oper)
            {
                case ShaderOper.CR:   OperA = GetOperCbuf34   (OpCode); break;
                case ShaderOper.Immf: OperA = GetOperImmf19_20(OpCode); break;
                case ShaderOper.RR:   OperA = GetOperGpr20    (OpCode); break;

                default: throw new ArgumentException(nameof(Oper));
            }

            OperA = GetAluFabsFneg(OperA, AbsA, NegA);

            Block.AddNode(new ShaderIrCmnt("Stubbed."));

            Block.AddNode(GetPredNode(new ShaderIrAsg(GetOperGpr0(OpCode), OperA), OpCode));
        }

        private static void EmitFset(ShaderIrBlock Block, long OpCode, ShaderOper Oper)
        {
            EmitSet(Block, OpCode, true, Oper);
        }

        private static void EmitIset(ShaderIrBlock Block, long OpCode, ShaderOper Oper)
        {
            EmitSet(Block, OpCode, false, Oper);
        }

        private static void EmitSet(ShaderIrBlock Block, long OpCode, bool IsFloat, ShaderOper Oper)
        {
            bool NegA = ((OpCode >> 43) & 1) != 0;
            bool AbsB = ((OpCode >> 44) & 1) != 0;
            bool NegB = ((OpCode >> 53) & 1) != 0;
            bool AbsA = ((OpCode >> 54) & 1) != 0;

            bool BoolFloat = ((OpCode >> (IsFloat ? 52 : 44)) & 1) != 0;

            ShaderIrNode OperA = GetOperGpr8(OpCode), OperB;

            switch (Oper)
            {
                case ShaderOper.CR:   OperB = GetOperCbuf34   (OpCode); break;
                case ShaderOper.Imm:  OperB = GetOperImm19_20 (OpCode); break;
                case ShaderOper.Immf: OperB = GetOperImmf19_20(OpCode); break;
                case ShaderOper.RR:   OperB = GetOperGpr20    (OpCode); break;

                default: throw new ArgumentException(nameof(Oper));
            }

            ShaderIrInst CmpInst;

            if (IsFloat)
            {
                OperA = GetAluFabsFneg(OperA, AbsA, NegA);
                OperB = GetAluFabsFneg(OperB, AbsB, NegB);

                CmpInst = GetCmpF(OpCode);
            }
            else
            {
                CmpInst = GetCmp(OpCode);
            }

            ShaderIrOp Op = new ShaderIrOp(CmpInst, OperA, OperB);

            ShaderIrInst LopInst = GetBLop45(OpCode);

            ShaderIrOperPred PNode = GetOperPred39(OpCode);

            ShaderIrNode Imm0, Imm1;

            if (BoolFloat)
            {
                Imm0 = new ShaderIrOperImmf(0);
                Imm1 = new ShaderIrOperImmf(1);
            }
            else
            {
                Imm0 = new ShaderIrOperImm(0);
                Imm1 = new ShaderIrOperImm(-1);
            }

            ShaderIrNode Asg0 = new ShaderIrAsg(GetOperGpr0(OpCode), Imm0);
            ShaderIrNode Asg1 = new ShaderIrAsg(GetOperGpr0(OpCode), Imm1);

            if (LopInst != ShaderIrInst.Band || !PNode.IsConst)
            {
                ShaderIrOp Op2 = new ShaderIrOp(LopInst, Op, PNode);

                Asg0 = new ShaderIrCond(Op2, Asg0, Not: true);
                Asg1 = new ShaderIrCond(Op2, Asg1, Not: false);
            }
            else
            {
                Asg0 = new ShaderIrCond(Op, Asg0, Not: true);
                Asg1 = new ShaderIrCond(Op, Asg1, Not: false);
            }

            Block.AddNode(GetPredNode(Asg0, OpCode));
            Block.AddNode(GetPredNode(Asg1, OpCode));
        }

        private static void EmitFsetp(ShaderIrBlock Block, long OpCode, ShaderOper Oper)
        {
            EmitSetp(Block, OpCode, true, Oper);
        }

        private static void EmitIsetp(ShaderIrBlock Block, long OpCode, ShaderOper Oper)
        {
            EmitSetp(Block, OpCode, false, Oper);
        }

        private static void EmitSetp(ShaderIrBlock Block, long OpCode, bool IsFloat, ShaderOper Oper)
        {
            bool AbsA = ((OpCode >>  7) & 1) != 0;
            bool NegP = ((OpCode >> 42) & 1) != 0;
            bool NegA = ((OpCode >> 43) & 1) != 0;
            bool AbsB = ((OpCode >> 44) & 1) != 0;

            ShaderIrNode OperA = GetOperGpr8(OpCode), OperB;

            switch (Oper)
            {
                case ShaderOper.CR:   OperB = GetOperCbuf34   (OpCode); break;
                case ShaderOper.Imm:  OperB = GetOperImm19_20 (OpCode); break;
                case ShaderOper.Immf: OperB = GetOperImmf19_20(OpCode); break;
                case ShaderOper.RR:   OperB = GetOperGpr20    (OpCode); break;

                default: throw new ArgumentException(nameof(Oper));
            }

            ShaderIrInst CmpInst;

            if (IsFloat)
            {
                OperA = GetAluFabsFneg(OperA, AbsA, NegA);
                OperB = GetAluFabs    (OperB, AbsB);

                CmpInst = GetCmpF(OpCode);
            }
            else
            {
                CmpInst = GetCmp(OpCode);
            }

            ShaderIrOp Op = new ShaderIrOp(CmpInst, OperA, OperB);

            ShaderIrOperPred P0Node = GetOperPred3 (OpCode);
            ShaderIrOperPred P1Node = GetOperPred0 (OpCode);
            ShaderIrOperPred P2Node = GetOperPred39(OpCode);

            Block.AddNode(GetPredNode(new ShaderIrAsg(P0Node, Op), OpCode));

            ShaderIrInst LopInst = GetBLop45(OpCode);

            if (LopInst == ShaderIrInst.Band && P1Node.IsConst && P2Node.IsConst)
            {
                return;
            }

            ShaderIrNode P2NNode = P2Node;

            if (NegP)
            {
                P2NNode = new ShaderIrOp(ShaderIrInst.Bnot, P2NNode);
            }

            Op = new ShaderIrOp(ShaderIrInst.Bnot, P0Node);

            Op = new ShaderIrOp(LopInst, Op, P2NNode);

            Block.AddNode(GetPredNode(new ShaderIrAsg(P1Node, Op), OpCode));

            Op = new ShaderIrOp(LopInst, P0Node, P2NNode);

            Block.AddNode(GetPredNode(new ShaderIrAsg(P0Node, Op), OpCode));
        }

        private static void EmitLop(ShaderIrBlock Block, long OpCode, ShaderOper Oper)
        {
            int SubOp = (int)(OpCode >> 41) & 3;

            bool InvA = ((OpCode >> 39) & 1) != 0;
            bool InvB = ((OpCode >> 40) & 1) != 0;

            ShaderIrInst Inst = 0;

            switch (SubOp)
            {
                case 0: Inst = ShaderIrInst.And; break;
                case 1: Inst = ShaderIrInst.Or;  break;
                case 2: Inst = ShaderIrInst.Xor; break;
            }

            ShaderIrNode OperA = GetAluNot(GetOperGpr8(OpCode), InvA);
            ShaderIrNode OperB;

            switch (Oper)
            {
                case ShaderOper.CR:  OperB = GetOperCbuf34  (OpCode); break;
                case ShaderOper.Imm: OperB = GetOperImm19_20(OpCode); break;
                case ShaderOper.RR:  OperB = GetOperGpr20   (OpCode); break;

                default: throw new ArgumentException(nameof(Oper));
            }

            OperB = GetAluNot(OperB, InvB);

            ShaderIrNode Op;

            if (SubOp < 3)
            {
                Op = new ShaderIrOp(Inst, OperA, OperB);
            }
            else
            {
                Op = OperB;
            }

            ShaderIrNode Compare = new ShaderIrOp(ShaderIrInst.Cne, Op, new ShaderIrOperImm(0));

            Block.AddNode(GetPredNode(new ShaderIrAsg(GetOperPred48(OpCode), Compare), OpCode));

            Block.AddNode(GetPredNode(new ShaderIrAsg(GetOperGpr0(OpCode), Op), OpCode));
        }

        private static void EmitXmad(ShaderIrBlock Block, long OpCode, ShaderOper Oper)
        {
            //TODO: Confirm SignAB/C, it is just a guess.
            //TODO: Implement Mode 3 (CSFU), what it does?
            bool SignAB = ((OpCode >> 48) & 1) != 0;
            bool SignC  = ((OpCode >> 49) & 1) != 0;
            bool HighB  = ((OpCode >> 52) & 1) != 0;
            bool HighA  = ((OpCode >> 53) & 1) != 0;

            int Mode = (int)(OpCode >> 50) & 7;

            ShaderIrNode OperA = GetOperGpr8(OpCode), OperB, OperC;

            ShaderIrOperImm Imm16  = new ShaderIrOperImm(16);
            ShaderIrOperImm ImmMsk = new ShaderIrOperImm(0xffff);

            ShaderIrInst ShiftAB = SignAB ? ShaderIrInst.Asr : ShaderIrInst.Lsr;
            ShaderIrInst ShiftC  = SignC  ? ShaderIrInst.Asr : ShaderIrInst.Lsr;

            if (HighA)
            {
                OperA = new ShaderIrOp(ShiftAB, OperA, Imm16);
            }

            switch (Oper)
            {
                case ShaderOper.CR:  OperB = GetOperCbuf34  (OpCode); break;
                case ShaderOper.Imm: OperB = GetOperImm19_20(OpCode); break;
                case ShaderOper.RC:  OperB = GetOperGpr39   (OpCode); break;
                case ShaderOper.RR:  OperB = GetOperGpr20   (OpCode); break;

                default: throw new ArgumentException(nameof(Oper));
            }

            bool ProductShiftLeft = false, Merge = false;

            if (Oper == ShaderOper.RC)
            {
                OperC = GetOperCbuf34(OpCode);
            }
            else
            {
                OperC = GetOperGpr39(OpCode);

                ProductShiftLeft = ((OpCode >> 36) & 1) != 0;
                Merge            = ((OpCode >> 37) & 1) != 0;
            }

            switch (Mode)
            {
                //CLO.
                case 1: OperC = ExtendTo32(OperC, SignC, 16); break;

                //CHI.
                case 2: OperC = new ShaderIrOp(ShiftC, OperC, Imm16); break;
            }

            ShaderIrNode OperBH = OperB;

            if (HighB)
            {
                OperBH = new ShaderIrOp(ShiftAB, OperBH, Imm16);
            }

            ShaderIrOp MulOp = new ShaderIrOp(ShaderIrInst.Mul, OperA, OperBH);

            if (ProductShiftLeft)
            {
                MulOp = new ShaderIrOp(ShaderIrInst.Lsl, MulOp, Imm16);
            }

            ShaderIrOp AddOp = new ShaderIrOp(ShaderIrInst.Add, MulOp, OperC);

            if (Merge)
            {
                AddOp = new ShaderIrOp(ShaderIrInst.And, AddOp, ImmMsk);
                OperB = new ShaderIrOp(ShaderIrInst.Lsl, OperB, Imm16);
                AddOp = new ShaderIrOp(ShaderIrInst.Or,  AddOp, OperB);
            }

            if (Mode == 4)
            {
                OperB = new ShaderIrOp(ShaderIrInst.Lsl, OperB, Imm16);
                AddOp = new ShaderIrOp(ShaderIrInst.Or,  AddOp, OperB);
            }

            Block.AddNode(GetPredNode(new ShaderIrAsg(GetOperGpr0(OpCode), AddOp), OpCode));
        }
    }
}