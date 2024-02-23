#define SimdCvt32

using ARMeilleure.State;
using NUnit.Framework;
using System.Collections.Generic;

namespace Ryujinx.Tests.Cpu
{
    [Category("SimdCvt32")]
    public sealed class CpuTestSimdCvt32 : CpuTest32
    {
#if SimdCvt32

#region "ValueSource (Opcodes)"
#endregion

#region "ValueSource (Types)"
        private static uint[] _1S_()
        {
            return new uint[] { 0x00000000u, 0x7FFFFFFFu,
                                0x80000000u, 0xFFFFFFFFu };
        }

        private static IEnumerable<uint> _1S_F_()
        {
            yield return 0xFF7FFFFFu; // -Max Normal    (float.MinValue)
            yield return 0x80800000u; // -Min Normal
            yield return 0x807FFFFFu; // -Max Subnormal
            yield return 0x80000001u; // -Min Subnormal (-float.Epsilon)
            yield return 0x7F7FFFFFu; // +Max Normal    (float.MaxValue)
            yield return 0x00800000u; // +Min Normal
            yield return 0x007FFFFFu; // +Max Subnormal
            yield return 0x00000001u; // +Min Subnormal (float.Epsilon)

            if (!NoZeros)
            {
                yield return 0x80000000u; // -Zero
                yield return 0x00000000u; // +Zero
            }

            if (!NoInfs)
            {
                yield return 0xFF800000u; // -Infinity
                yield return 0x7F800000u; // +Infinity
            }

            if (!NoNaNs)
            {
                yield return 0xFFC00000u; // -QNaN (all zeros payload) (float.NaN)
                yield return 0xFFBFFFFFu; // -SNaN (all ones  payload)
                yield return 0x7FC00000u; // +QNaN (all zeros payload) (-float.NaN) (DefaultNaN)
                yield return 0x7FBFFFFFu; // +SNaN (all ones  payload)
            }

            for (int cnt = 1; cnt <= RndCnt; cnt++)
            {
                yield return GenNormalS();
                yield return GenSubnormalS();
            }
        }

        private static IEnumerable<ulong> _1D_F_()
        {
            yield return 0xFFEFFFFFFFFFFFFFul; // -Max Normal    (double.MinValue)
            yield return 0x8010000000000000ul; // -Min Normal
            yield return 0x800FFFFFFFFFFFFFul; // -Max Subnormal
            yield return 0x8000000000000001ul; // -Min Subnormal (-double.Epsilon)
            yield return 0x7FEFFFFFFFFFFFFFul; // +Max Normal    (double.MaxValue)
            yield return 0x0010000000000000ul; // +Min Normal
            yield return 0x000FFFFFFFFFFFFFul; // +Max Subnormal
            yield return 0x0000000000000001ul; // +Min Subnormal (double.Epsilon)

            if (!NoZeros)
            {
                yield return 0x8000000000000000ul; // -Zero
                yield return 0x0000000000000000ul; // +Zero
            }

            if (!NoInfs)
            {
                yield return 0xFFF0000000000000ul; // -Infinity
                yield return 0x7FF0000000000000ul; // +Infinity
            }

            if (!NoNaNs)
            {
                yield return 0xFFF8000000000000ul; // -QNaN (all zeros payload) (double.NaN)
                yield return 0xFFF7FFFFFFFFFFFFul; // -SNaN (all ones  payload)
                yield return 0x7FF8000000000000ul; // +QNaN (all zeros payload) (-double.NaN) (DefaultNaN)
                yield return 0x7FF7FFFFFFFFFFFFul; // +SNaN (all ones  payload)
            }

            for (int cnt = 1; cnt <= RndCnt; cnt++)
            {
                yield return GenNormalD();
                yield return GenSubnormalD();
            }
        }
#endregion

        private const int RndCnt = 2;

        private static readonly bool NoZeros = false;
        private static readonly bool NoInfs  = false;
        private static readonly bool NoNaNs  = false;

        [Explicit]
        [Test, Pairwise, Description("VCVT.<dt>.F32 <Sd>, <Sm>")]
        public void Vcvt_F32_I32([Values(0u, 1u, 2u, 3u)] uint rd,
                                 [Values(0u, 1u, 2u, 3u)] uint rm,
                                 [ValueSource(nameof(_1S_F_))] uint s0,
                                 [ValueSource(nameof(_1S_F_))] uint s1,
                                 [ValueSource(nameof(_1S_F_))] uint s2,
                                 [ValueSource(nameof(_1S_F_))] uint s3,
                                 [Values] bool unsigned) // <U32, S32>
        {
            uint opcode = 0xeebc0ac0u; // VCVT.U32.F32 S0, S0

            if (!unsigned)
            {
                opcode |= 1 << 16; // opc2<0>
            }

            opcode |= ((rd & 0x1e) << 11) | ((rd & 0x1) << 22);
            opcode |= ((rm & 0x1e) >> 1) | ((rm & 0x1) << 5);

            V128 v0 = MakeVectorE0E1E2E3(s0, s1, s2, s3);

            SingleOpcode(opcode, v0: v0);

            CompareAgainstUnicorn();
        }

        [Explicit]
        [Test, Pairwise, Description("VCVT.<dt>.F64 <Sd>, <Dm>")]
        public void Vcvt_F64_I32([Values(0u, 1u, 2u, 3u)] uint rd,
                                 [Values(0u, 1u)] uint rm,
                                 [ValueSource(nameof(_1D_F_))] ulong d0,
                                 [ValueSource(nameof(_1D_F_))] ulong d1,
                                 [Values] bool unsigned) // <U32, S32>
        {
            uint opcode = 0xeebc0bc0u; // VCVT.U32.F64 S0, D0

            if (!unsigned)
            {
                opcode |= 1 << 16; // opc2<0>
            }

            opcode |= ((rd & 0x1e) << 11) | ((rd & 0x1) << 22);
            opcode |= ((rm & 0xf) << 0) | ((rm & 0x10) << 1);

            V128 v0 = MakeVectorE0E1(d0, d1);

            SingleOpcode(opcode, v0: v0);

            CompareAgainstUnicorn();
        }

        [Explicit]
        [Test, Pairwise, Description("VCVT.F32.<dt> <Sd>, <Sm>")]
        public void Vcvt_I32_F32([Values(0u, 1u, 2u, 3u)] uint rd,
                                 [Values(0u, 1u, 2u, 3u)] uint rm,
                                 [ValueSource(nameof(_1S_))] [Random(RndCnt)] uint s0,
                                 [ValueSource(nameof(_1S_))] [Random(RndCnt)] uint s1,
                                 [ValueSource(nameof(_1S_))] [Random(RndCnt)] uint s2,
                                 [ValueSource(nameof(_1S_))] [Random(RndCnt)] uint s3,
                                 [Values] bool unsigned, // <U32, S32>
                                 [Values(RMode.Rn)] RMode rMode)
        {
            uint opcode = 0xeeb80a40u; // VCVT.F32.U32 S0, S0

            if (!unsigned)
            {
                opcode |= 1 << 7; // op
            }

            opcode |= ((rm & 0x1e) >> 1) | ((rm & 0x1) << 5);
            opcode |= ((rd & 0x1e) << 11) | ((rd & 0x1) << 22);

            V128 v0 = MakeVectorE0E1E2E3(s0, s1, s2, s3);

            int fpscr = (int)rMode << (int)Fpcr.RMode;

            SingleOpcode(opcode, v0: v0, fpscr: fpscr);

            CompareAgainstUnicorn();
        }

        [Explicit]
        [Test, Pairwise, Description("VCVT.F64.<dt> <Dd>, <Sm>")]
        public void Vcvt_I32_F64([Values(0u, 1u)] uint rd,
                                 [Values(0u, 1u, 2u, 3u)] uint rm,
                                 [ValueSource(nameof(_1S_))] [Random(RndCnt)] uint s0,
                                 [ValueSource(nameof(_1S_))] [Random(RndCnt)] uint s1,
                                 [ValueSource(nameof(_1S_))] [Random(RndCnt)] uint s2,
                                 [ValueSource(nameof(_1S_))] [Random(RndCnt)] uint s3,
                                 [Values] bool unsigned, // <U32, S32>
                                 [Values(RMode.Rn)] RMode rMode)
        {
            uint opcode = 0xeeb80b40u; // VCVT.F64.U32 D0, S0

            if (!unsigned)
            {
                opcode |= 1 << 7; // op
            }

            opcode |= ((rm & 0x1e) >> 1) | ((rm & 0x1) << 5);
            opcode |= ((rd & 0xf) << 12) | ((rd & 0x10) << 18);

            V128 v0 = MakeVectorE0E1E2E3(s0, s1, s2, s3);

            int fpscr = (int)rMode << (int)Fpcr.RMode;

            SingleOpcode(opcode, v0: v0, fpscr: fpscr);

            CompareAgainstUnicorn();
        }
#endif
    }
}
