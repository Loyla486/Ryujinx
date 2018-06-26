#define Simd

using ChocolArm64.State;

using NUnit.Framework;

using System.Runtime.Intrinsics;

namespace Ryujinx.Tests.Cpu
{
    using Tester;
    using Tester.Types;

    [Category("Simd")/*, Ignore("Tested: first half of 2018.")*/]
    public sealed class CpuTestSimd : CpuTest
    {
#if Simd
        [SetUp]
        public void SetupTester()
        {
            AArch64.TakeReset(false);
        }

#region "ValueSource"
        private static ulong[] _1D_()
        {
            return new ulong[] { 0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                 0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul };
        }

        private static ulong[] _1H1S1D_()
        {
            return new ulong[] { 0x0000000000000000ul, 0x0000000000007FFFul,
                                 0x0000000000008000ul, 0x000000000000FFFFul,
                                 0x000000007FFFFFFFul, 0x0000000080000000ul,
                                 0x00000000FFFFFFFFul, 0x7FFFFFFFFFFFFFFFul,
                                 0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul };
        }

        private static ulong[] _4H2S1D_()
        {
            return new ulong[] { 0x0000000000000000ul, 0x7FFF7FFF7FFF7FFFul,
                                 0x8000800080008000ul, 0x7FFFFFFF7FFFFFFFul,
                                 0x8000000080000000ul, 0x7FFFFFFFFFFFFFFFul,
                                 0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul };
        }

        private static ulong[] _8B_()
        {
            return new ulong[] { 0x0000000000000000ul, 0x7F7F7F7F7F7F7F7Ful,
                                 0x8080808080808080ul, 0xFFFFFFFFFFFFFFFFul };
        }

        private static ulong[] _8B4H_()
        {
            return new ulong[] { 0x0000000000000000ul, 0x7F7F7F7F7F7F7F7Ful,
                                 0x8080808080808080ul, 0x7FFF7FFF7FFF7FFFul,
                                 0x8000800080008000ul, 0xFFFFFFFFFFFFFFFFul };
        }

        private static ulong[] _8B4H2S_()
        {
            return new ulong[] { 0x0000000000000000ul, 0x7F7F7F7F7F7F7F7Ful,
                                 0x8080808080808080ul, 0x7FFF7FFF7FFF7FFFul,
                                 0x8000800080008000ul, 0x7FFFFFFF7FFFFFFFul,
                                 0x8000000080000000ul, 0xFFFFFFFFFFFFFFFFul };
        }

        private static ulong[] _8B4H2S1D_()
        {
            return new ulong[] { 0x0000000000000000ul, 0x7F7F7F7F7F7F7F7Ful,
                                 0x8080808080808080ul, 0x7FFF7FFF7FFF7FFFul,
                                 0x8000800080008000ul, 0x7FFFFFFF7FFFFFFFul,
                                 0x8000000080000000ul, 0x7FFFFFFFFFFFFFFFul,
                                 0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul };
        }
#endregion

        [Test, Description("ABS <V><d>, <V><n>")]
        public void Abs_S_D([ValueSource("_1D_")] [Random(1)] ulong A)
        {
            uint Opcode = 0x5EE0B820; // ABS D0, D1
            Bits Op = new Bits(Opcode);

            Vector128<float> V0 = MakeVectorE1(TestContext.CurrentContext.Random.NextULong());
            Vector128<float> V1 = MakeVectorE0(A);
            AThreadState ThreadState = SingleOpcode(Opcode, V0: V0, V1: V1);

            AArch64.V(1, new Bits(A));
            SimdFp.Abs_S(Op[23, 22], Op[9, 5], Op[4, 0]);

            Assert.Multiple(() =>
            {
                Assert.That(GetVectorE0(ThreadState.V0), Is.EqualTo(AArch64.V(64, 0).ToUInt64()));
                Assert.That(GetVectorE1(ThreadState.V0), Is.Zero);
            });
        }

        [Test, Description("ABS <Vd>.<T>, <Vn>.<T>")]
        public void Abs_V_8B_4H_2S([ValueSource("_8B4H2S_")] [Random(1)] ulong A,
                                   [Values(0b00u, 0b01u, 0b10u)] uint size) // <8B, 4H, 2S>
        {
            uint Opcode = 0x0E20B820; // ABS V0.8B, V1.8B
            Opcode |= ((size & 3) << 22);
            Bits Op = new Bits(Opcode);

            Vector128<float> V0 = MakeVectorE1(TestContext.CurrentContext.Random.NextULong());
            Vector128<float> V1 = MakeVectorE0(A);
            AThreadState ThreadState = SingleOpcode(Opcode, V0: V0, V1: V1);

            AArch64.V(1, new Bits(A));
            SimdFp.Abs_V(Op[30], Op[23, 22], Op[9, 5], Op[4, 0]);

            Assert.Multiple(() =>
            {
                Assert.That(GetVectorE0(ThreadState.V0), Is.EqualTo(AArch64.V(64, 0).ToUInt64()));
                Assert.That(GetVectorE1(ThreadState.V0), Is.Zero);
            });
        }

        [Test, Pairwise, Description("ABS <Vd>.<T>, <Vn>.<T>")]
        public void Abs_V_16B_8H_4S_2D([ValueSource("_8B4H2S1D_")] [Random(1)] ulong A0,
                                       [ValueSource("_8B4H2S1D_")] [Random(1)] ulong A1,
                                       [Values(0b00u, 0b01u, 0b10u, 0b11u)] uint size) // <16B, 8H, 4S, 2D>
        {
            uint Opcode = 0x4E20B820; // ABS V0.16B, V1.16B
            Opcode |= ((size & 3) << 22);
            Bits Op = new Bits(Opcode);

            Vector128<float> V1 = MakeVectorE0E1(A0, A1);
            AThreadState ThreadState = SingleOpcode(Opcode, V1: V1);

            AArch64.Vpart(1, 0, new Bits(A0));
            AArch64.Vpart(1, 1, new Bits(A1));
            SimdFp.Abs_V(Op[30], Op[23, 22], Op[9, 5], Op[4, 0]);

            Assert.Multiple(() =>
            {
                Assert.That(GetVectorE0(ThreadState.V0), Is.EqualTo(AArch64.Vpart(64, 0, 0).ToUInt64()));
                Assert.That(GetVectorE1(ThreadState.V0), Is.EqualTo(AArch64.Vpart(64, 0, 1).ToUInt64()));
            });
        }

        [Test, Pairwise, Description("ADDP <V><d>, <Vn>.<T>")]
        public void Addp_S_2DD([ValueSource("_1D_")] [Random(1)] ulong A0,
                               [ValueSource("_1D_")] [Random(1)] ulong A1)
        {
            uint Opcode = 0x5EF1B820; // ADDP D0, V1.2D
            Bits Op = new Bits(Opcode);

            Vector128<float> V0 = MakeVectorE1(TestContext.CurrentContext.Random.NextULong());
            Vector128<float> V1 = MakeVectorE0E1(A0, A1);
            AThreadState ThreadState = SingleOpcode(Opcode, V0: V0, V1: V1);

            AArch64.Vpart(1, 0, new Bits(A0));
            AArch64.Vpart(1, 1, new Bits(A1));
            SimdFp.Addp_S(Op[23, 22], Op[9, 5], Op[4, 0]);

            Assert.Multiple(() =>
            {
                Assert.That(GetVectorE0(ThreadState.V0), Is.EqualTo(AArch64.V(64, 0).ToUInt64()));
                Assert.That(GetVectorE1(ThreadState.V0), Is.Zero);
            });
        }

        [Test, Description("ADDV <V><d>, <Vn>.<T>")]
        public void Addv_V_8BB_4HH([ValueSource("_8B4H_")] [Random(1)] ulong A,
                                   [Values(0b00u, 0b01u)] uint size) // <8B, 4H>
        {
            uint Opcode = 0x0E31B820; // ADDV B0, V1.8B
            Opcode |= ((size & 3) << 22);
            Bits Op = new Bits(Opcode);

            Vector128<float> V0 = MakeVectorE0E1(TestContext.CurrentContext.Random.NextULong(),
                                                 TestContext.CurrentContext.Random.NextULong());
            Vector128<float> V1 = MakeVectorE0(A);
            AThreadState ThreadState = SingleOpcode(Opcode, V0: V0, V1: V1);

            AArch64.Vpart(0, 0, new Bits(TestContext.CurrentContext.Random.NextULong()));
            AArch64.V(1, new Bits(A));
            SimdFp.Addv_V(Op[30], Op[23, 22], Op[9, 5], Op[4, 0]);

            Assert.Multiple(() =>
            {
                Assert.That(GetVectorE0(ThreadState.V0), Is.EqualTo(AArch64.V(64, 0).ToUInt64()));
                Assert.That(GetVectorE1(ThreadState.V0), Is.Zero);
            });
        }

        [Test, Pairwise, Description("ADDV <V><d>, <Vn>.<T>")]
        public void Addv_V_16BB_8HH_4SS([ValueSource("_8B4H2S_")] [Random(1)] ulong A0,
                                        [ValueSource("_8B4H2S_")] [Random(1)] ulong A1,
                                        [Values(0b00u, 0b01u, 0b10u)] uint size) // <16B, 8H, 4S>
        {
            uint Opcode = 0x4E31B820; // ADDV B0, V1.16B
            Opcode |= ((size & 3) << 22);
            Bits Op = new Bits(Opcode);

            Vector128<float> V0 = MakeVectorE0E1(TestContext.CurrentContext.Random.NextULong(),
                                                 TestContext.CurrentContext.Random.NextULong());
            Vector128<float> V1 = MakeVectorE0E1(A0, A1);
            AThreadState ThreadState = SingleOpcode(Opcode, V0: V0, V1: V1);

            AArch64.Vpart(0, 0, new Bits(TestContext.CurrentContext.Random.NextULong()));
            AArch64.Vpart(1, 0, new Bits(A0));
            AArch64.Vpart(1, 1, new Bits(A1));
            SimdFp.Addv_V(Op[30], Op[23, 22], Op[9, 5], Op[4, 0]);

            Assert.Multiple(() =>
            {
                Assert.That(GetVectorE0(ThreadState.V0), Is.EqualTo(AArch64.V(64, 0).ToUInt64()));
                Assert.That(GetVectorE1(ThreadState.V0), Is.Zero);
            });
        }

        [Test, Description("CLS <Vd>.<T>, <Vn>.<T>")]
        public void Cls_V_8B_4H_2S([ValueSource("_8B4H2S_")] [Random(1)] ulong A,
                                   [Values(0b00u, 0b01u, 0b10u)] uint size) // <8B, 4H, 2S>
        {
            uint Opcode = 0x0E204820; // CLS V0.8B, V1.8B
            Opcode |= ((size & 3) << 22);
            Bits Op = new Bits(Opcode);

            Vector128<float> V0 = MakeVectorE1(TestContext.CurrentContext.Random.NextULong());
            Vector128<float> V1 = MakeVectorE0(A);
            AThreadState ThreadState = SingleOpcode(Opcode, V0: V0, V1: V1);

            AArch64.V(1, new Bits(A));
            SimdFp.Cls_V(Op[30], Op[23, 22], Op[9, 5], Op[4, 0]);

            Assert.Multiple(() =>
            {
                Assert.That(GetVectorE0(ThreadState.V0), Is.EqualTo(AArch64.V(64, 0).ToUInt64()));
                Assert.That(GetVectorE1(ThreadState.V0), Is.Zero);
            });
        }

        [Test, Pairwise, Description("CLS <Vd>.<T>, <Vn>.<T>")]
        public void Cls_V_16B_8H_4S([ValueSource("_8B4H2S_")] [Random(1)] ulong A0,
                                    [ValueSource("_8B4H2S_")] [Random(1)] ulong A1,
                                    [Values(0b00u, 0b01u, 0b10u)] uint size) // <16B, 8H, 4S>
        {
            uint Opcode = 0x4E204820; // CLS V0.16B, V1.16B
            Opcode |= ((size & 3) << 22);
            Bits Op = new Bits(Opcode);

            Vector128<float> V1 = MakeVectorE0E1(A0, A1);
            AThreadState ThreadState = SingleOpcode(Opcode, V1: V1);

            AArch64.Vpart(1, 0, new Bits(A0));
            AArch64.Vpart(1, 1, new Bits(A1));
            SimdFp.Cls_V(Op[30], Op[23, 22], Op[9, 5], Op[4, 0]);

            Assert.Multiple(() =>
            {
                Assert.That(GetVectorE0(ThreadState.V0), Is.EqualTo(AArch64.Vpart(64, 0, 0).ToUInt64()));
                Assert.That(GetVectorE1(ThreadState.V0), Is.EqualTo(AArch64.Vpart(64, 0, 1).ToUInt64()));
            });
        }

        [Test, Description("CLZ <Vd>.<T>, <Vn>.<T>")]
        public void Clz_V_8B_4H_2S([ValueSource("_8B4H2S_")] [Random(1)] ulong A,
                                   [Values(0b00u, 0b01u, 0b10u)] uint size) // <8B, 4H, 2S>
        {
            uint Opcode = 0x2E204820; // CLZ V0.8B, V1.8B
            Opcode |= ((size & 3) << 22);
            Bits Op = new Bits(Opcode);

            Vector128<float> V0 = MakeVectorE1(TestContext.CurrentContext.Random.NextULong());
            Vector128<float> V1 = MakeVectorE0(A);
            AThreadState ThreadState = SingleOpcode(Opcode, V0: V0, V1: V1);

            AArch64.V(1, new Bits(A));
            SimdFp.Clz_V(Op[30], Op[23, 22], Op[9, 5], Op[4, 0]);

            Assert.Multiple(() =>
            {
                Assert.That(GetVectorE0(ThreadState.V0), Is.EqualTo(AArch64.V(64, 0).ToUInt64()));
                Assert.That(GetVectorE1(ThreadState.V0), Is.Zero);
            });
        }

        [Test, Pairwise, Description("CLZ <Vd>.<T>, <Vn>.<T>")]
        public void Clz_V_16B_8H_4S([ValueSource("_8B4H2S_")] [Random(1)] ulong A0,
                                    [ValueSource("_8B4H2S_")] [Random(1)] ulong A1,
                                    [Values(0b00u, 0b01u, 0b10u)] uint size) // <16B, 8H, 4S>
        {
            uint Opcode = 0x6E204820; // CLZ V0.16B, V1.16B
            Opcode |= ((size & 3) << 22);
            Bits Op = new Bits(Opcode);

            Vector128<float> V1 = MakeVectorE0E1(A0, A1);
            AThreadState ThreadState = SingleOpcode(Opcode, V1: V1);

            AArch64.Vpart(1, 0, new Bits(A0));
            AArch64.Vpart(1, 1, new Bits(A1));
            SimdFp.Clz_V(Op[30], Op[23, 22], Op[9, 5], Op[4, 0]);

            Assert.Multiple(() =>
            {
                Assert.That(GetVectorE0(ThreadState.V0), Is.EqualTo(AArch64.Vpart(64, 0, 0).ToUInt64()));
                Assert.That(GetVectorE1(ThreadState.V0), Is.EqualTo(AArch64.Vpart(64, 0, 1).ToUInt64()));
            });
        }

        [Test, Description("CMEQ <V><d>, <V><n>, #0")]
        public void Cmeq_S_D([ValueSource("_1D_")] [Random(1)] ulong A)
        {
            uint Opcode = 0x5EE09820; // CMEQ D0, D1, #0
            Bits Op = new Bits(Opcode);

            Vector128<float> V0 = MakeVectorE1(TestContext.CurrentContext.Random.NextULong());
            Vector128<float> V1 = MakeVectorE0(A);
            AThreadState ThreadState = SingleOpcode(Opcode, V0: V0, V1: V1);

            AArch64.V(1, new Bits(A));
            SimdFp.Cmeq_Zero_S(Op[23, 22], Op[9, 5], Op[4, 0]);

            Assert.Multiple(() =>
            {
                Assert.That(GetVectorE0(ThreadState.V0), Is.EqualTo(AArch64.V(64, 0).ToUInt64()));
                Assert.That(GetVectorE1(ThreadState.V0), Is.Zero);
            });
        }

        [Test, Description("CMEQ <Vd>.<T>, <Vn>.<T>, #0")]
        public void Cmeq_V_8B_4H_2S([ValueSource("_8B4H2S_")] [Random(1)] ulong A,
                                    [Values(0b00u, 0b01u, 0b10u)] uint size) // <8B, 4H, 2S>
        {
            uint Opcode = 0x0E209820; // CMEQ V0.8B, V1.8B, #0
            Opcode |= ((size & 3) << 22);
            Bits Op = new Bits(Opcode);

            Vector128<float> V0 = MakeVectorE1(TestContext.CurrentContext.Random.NextULong());
            Vector128<float> V1 = MakeVectorE0(A);
            AThreadState ThreadState = SingleOpcode(Opcode, V0: V0, V1: V1);

            AArch64.V(1, new Bits(A));
            SimdFp.Cmeq_Zero_V(Op[30], Op[23, 22], Op[9, 5], Op[4, 0]);

            Assert.Multiple(() =>
            {
                Assert.That(GetVectorE0(ThreadState.V0), Is.EqualTo(AArch64.V(64, 0).ToUInt64()));
                Assert.That(GetVectorE1(ThreadState.V0), Is.Zero);
            });
        }

        [Test, Pairwise, Description("CMEQ <Vd>.<T>, <Vn>.<T>, #0")]
        public void Cmeq_V_16B_8H_4S_2D([ValueSource("_8B4H2S1D_")] [Random(1)] ulong A0,
                                        [ValueSource("_8B4H2S1D_")] [Random(1)] ulong A1,
                                        [Values(0b00u, 0b01u, 0b10u, 0b11u)] uint size) // <16B, 8H, 4S, 2D>
        {
            uint Opcode = 0x4E209820; // CMEQ V0.16B, V1.16B, #0
            Opcode |= ((size & 3) << 22);
            Bits Op = new Bits(Opcode);

            Vector128<float> V1 = MakeVectorE0E1(A0, A1);
            AThreadState ThreadState = SingleOpcode(Opcode, V1: V1);

            AArch64.Vpart(1, 0, new Bits(A0));
            AArch64.Vpart(1, 1, new Bits(A1));
            SimdFp.Cmeq_Zero_V(Op[30], Op[23, 22], Op[9, 5], Op[4, 0]);

            Assert.Multiple(() =>
            {
                Assert.That(GetVectorE0(ThreadState.V0), Is.EqualTo(AArch64.Vpart(64, 0, 0).ToUInt64()));
                Assert.That(GetVectorE1(ThreadState.V0), Is.EqualTo(AArch64.Vpart(64, 0, 1).ToUInt64()));
            });
        }

        [Test, Description("CMGE <V><d>, <V><n>, #0")]
        public void Cmge_S_D([ValueSource("_1D_")] [Random(1)] ulong A)
        {
            uint Opcode = 0x7EE08820; // CMGE D0, D1, #0
            Bits Op = new Bits(Opcode);

            Vector128<float> V0 = MakeVectorE1(TestContext.CurrentContext.Random.NextULong());
            Vector128<float> V1 = MakeVectorE0(A);
            AThreadState ThreadState = SingleOpcode(Opcode, V0: V0, V1: V1);

            AArch64.V(1, new Bits(A));
            SimdFp.Cmge_Zero_S(Op[23, 22], Op[9, 5], Op[4, 0]);

            Assert.Multiple(() =>
            {
                Assert.That(GetVectorE0(ThreadState.V0), Is.EqualTo(AArch64.V(64, 0).ToUInt64()));
                Assert.That(GetVectorE1(ThreadState.V0), Is.Zero);
            });
        }

        [Test, Description("CMGE <Vd>.<T>, <Vn>.<T>, #0")]
        public void Cmge_V_8B_4H_2S([ValueSource("_8B4H2S_")] [Random(1)] ulong A,
                                    [Values(0b00u, 0b01u, 0b10u)] uint size) // <8B, 4H, 2S>
        {
            uint Opcode = 0x2E208820; // CMGE V0.8B, V1.8B, #0
            Opcode |= ((size & 3) << 22);
            Bits Op = new Bits(Opcode);

            Vector128<float> V0 = MakeVectorE1(TestContext.CurrentContext.Random.NextULong());
            Vector128<float> V1 = MakeVectorE0(A);
            AThreadState ThreadState = SingleOpcode(Opcode, V0: V0, V1: V1);

            AArch64.V(1, new Bits(A));
            SimdFp.Cmge_Zero_V(Op[30], Op[23, 22], Op[9, 5], Op[4, 0]);

            Assert.Multiple(() =>
            {
                Assert.That(GetVectorE0(ThreadState.V0), Is.EqualTo(AArch64.V(64, 0).ToUInt64()));
                Assert.That(GetVectorE1(ThreadState.V0), Is.Zero);
            });
        }

        [Test, Pairwise, Description("CMGE <Vd>.<T>, <Vn>.<T>, #0")]
        public void Cmge_V_16B_8H_4S_2D([ValueSource("_8B4H2S1D_")] [Random(1)] ulong A0,
                                        [ValueSource("_8B4H2S1D_")] [Random(1)] ulong A1,
                                        [Values(0b00u, 0b01u, 0b10u, 0b11u)] uint size) // <16B, 8H, 4S, 2D>
        {
            uint Opcode = 0x6E208820; // CMGE V0.16B, V1.16B, #0
            Opcode |= ((size & 3) << 22);
            Bits Op = new Bits(Opcode);

            Vector128<float> V1 = MakeVectorE0E1(A0, A1);
            AThreadState ThreadState = SingleOpcode(Opcode, V1: V1);

            AArch64.Vpart(1, 0, new Bits(A0));
            AArch64.Vpart(1, 1, new Bits(A1));
            SimdFp.Cmge_Zero_V(Op[30], Op[23, 22], Op[9, 5], Op[4, 0]);

            Assert.Multiple(() =>
            {
                Assert.That(GetVectorE0(ThreadState.V0), Is.EqualTo(AArch64.Vpart(64, 0, 0).ToUInt64()));
                Assert.That(GetVectorE1(ThreadState.V0), Is.EqualTo(AArch64.Vpart(64, 0, 1).ToUInt64()));
            });
        }

        [Test, Description("CMGT <V><d>, <V><n>, #0")]
        public void Cmgt_S_D([ValueSource("_1D_")] [Random(1)] ulong A)
        {
            uint Opcode = 0x5EE08820; // CMGT D0, D1, #0
            Bits Op = new Bits(Opcode);

            Vector128<float> V0 = MakeVectorE1(TestContext.CurrentContext.Random.NextULong());
            Vector128<float> V1 = MakeVectorE0(A);
            AThreadState ThreadState = SingleOpcode(Opcode, V0: V0, V1: V1);

            AArch64.V(1, new Bits(A));
            SimdFp.Cmgt_Zero_S(Op[23, 22], Op[9, 5], Op[4, 0]);

            Assert.Multiple(() =>
            {
                Assert.That(GetVectorE0(ThreadState.V0), Is.EqualTo(AArch64.V(64, 0).ToUInt64()));
                Assert.That(GetVectorE1(ThreadState.V0), Is.Zero);
            });
        }

        [Test, Description("CMGT <Vd>.<T>, <Vn>.<T>, #0")]
        public void Cmgt_V_8B_4H_2S([ValueSource("_8B4H2S_")] [Random(1)] ulong A,
                                    [Values(0b00u, 0b01u, 0b10u)] uint size) // <8B, 4H, 2S>
        {
            uint Opcode = 0x0E208820; // CMGT V0.8B, V1.8B, #0
            Opcode |= ((size & 3) << 22);
            Bits Op = new Bits(Opcode);

            Vector128<float> V0 = MakeVectorE1(TestContext.CurrentContext.Random.NextULong());
            Vector128<float> V1 = MakeVectorE0(A);
            AThreadState ThreadState = SingleOpcode(Opcode, V0: V0, V1: V1);

            AArch64.V(1, new Bits(A));
            SimdFp.Cmgt_Zero_V(Op[30], Op[23, 22], Op[9, 5], Op[4, 0]);

            Assert.Multiple(() =>
            {
                Assert.That(GetVectorE0(ThreadState.V0), Is.EqualTo(AArch64.V(64, 0).ToUInt64()));
                Assert.That(GetVectorE1(ThreadState.V0), Is.Zero);
            });
        }

        [Test, Pairwise, Description("CMGT <Vd>.<T>, <Vn>.<T>, #0")]
        public void Cmgt_V_16B_8H_4S_2D([ValueSource("_8B4H2S1D_")] [Random(1)] ulong A0,
                                        [ValueSource("_8B4H2S1D_")] [Random(1)] ulong A1,
                                        [Values(0b00u, 0b01u, 0b10u, 0b11u)] uint size) // <16B, 8H, 4S, 2D>
        {
            uint Opcode = 0x4E208820; // CMGT V0.16B, V1.16B, #0
            Opcode |= ((size & 3) << 22);
            Bits Op = new Bits(Opcode);

            Vector128<float> V1 = MakeVectorE0E1(A0, A1);
            AThreadState ThreadState = SingleOpcode(Opcode, V1: V1);

            AArch64.Vpart(1, 0, new Bits(A0));
            AArch64.Vpart(1, 1, new Bits(A1));
            SimdFp.Cmgt_Zero_V(Op[30], Op[23, 22], Op[9, 5], Op[4, 0]);

            Assert.Multiple(() =>
            {
                Assert.That(GetVectorE0(ThreadState.V0), Is.EqualTo(AArch64.Vpart(64, 0, 0).ToUInt64()));
                Assert.That(GetVectorE1(ThreadState.V0), Is.EqualTo(AArch64.Vpart(64, 0, 1).ToUInt64()));
            });
        }

        [Test, Description("CMLE <V><d>, <V><n>, #0")]
        public void Cmle_S_D([ValueSource("_1D_")] [Random(1)] ulong A)
        {
            uint Opcode = 0x7EE09820; // CMLE D0, D1, #0
            Bits Op = new Bits(Opcode);

            Vector128<float> V0 = MakeVectorE1(TestContext.CurrentContext.Random.NextULong());
            Vector128<float> V1 = MakeVectorE0(A);
            AThreadState ThreadState = SingleOpcode(Opcode, V0: V0, V1: V1);

            AArch64.V(1, new Bits(A));
            SimdFp.Cmle_S(Op[23, 22], Op[9, 5], Op[4, 0]);

            Assert.Multiple(() =>
            {
                Assert.That(GetVectorE0(ThreadState.V0), Is.EqualTo(AArch64.V(64, 0).ToUInt64()));
                Assert.That(GetVectorE1(ThreadState.V0), Is.Zero);
            });
        }

        [Test, Description("CMLE <Vd>.<T>, <Vn>.<T>, #0")]
        public void Cmle_V_8B_4H_2S([ValueSource("_8B4H2S_")] [Random(1)] ulong A,
                                    [Values(0b00u, 0b01u, 0b10u)] uint size) // <8B, 4H, 2S>
        {
            uint Opcode = 0x2E209820; // CMLE V0.8B, V1.8B, #0
            Opcode |= ((size & 3) << 22);
            Bits Op = new Bits(Opcode);

            Vector128<float> V0 = MakeVectorE1(TestContext.CurrentContext.Random.NextULong());
            Vector128<float> V1 = MakeVectorE0(A);
            AThreadState ThreadState = SingleOpcode(Opcode, V0: V0, V1: V1);

            AArch64.V(1, new Bits(A));
            SimdFp.Cmle_V(Op[30], Op[23, 22], Op[9, 5], Op[4, 0]);

            Assert.Multiple(() =>
            {
                Assert.That(GetVectorE0(ThreadState.V0), Is.EqualTo(AArch64.V(64, 0).ToUInt64()));
                Assert.That(GetVectorE1(ThreadState.V0), Is.Zero);
            });
        }

        [Test, Pairwise, Description("CMLE <Vd>.<T>, <Vn>.<T>, #0")]
        public void Cmle_V_16B_8H_4S_2D([ValueSource("_8B4H2S1D_")] [Random(1)] ulong A0,
                                        [ValueSource("_8B4H2S1D_")] [Random(1)] ulong A1,
                                        [Values(0b00u, 0b01u, 0b10u, 0b11u)] uint size) // <16B, 8H, 4S, 2D>
        {
            uint Opcode = 0x6E209820; // CMLE V0.16B, V1.16B, #0
            Opcode |= ((size & 3) << 22);
            Bits Op = new Bits(Opcode);

            Vector128<float> V1 = MakeVectorE0E1(A0, A1);
            AThreadState ThreadState = SingleOpcode(Opcode, V1: V1);

            AArch64.Vpart(1, 0, new Bits(A0));
            AArch64.Vpart(1, 1, new Bits(A1));
            SimdFp.Cmle_V(Op[30], Op[23, 22], Op[9, 5], Op[4, 0]);

            Assert.Multiple(() =>
            {
                Assert.That(GetVectorE0(ThreadState.V0), Is.EqualTo(AArch64.Vpart(64, 0, 0).ToUInt64()));
                Assert.That(GetVectorE1(ThreadState.V0), Is.EqualTo(AArch64.Vpart(64, 0, 1).ToUInt64()));
            });
        }

        [Test, Description("CMLT <V><d>, <V><n>, #0")]
        public void Cmlt_S_D([ValueSource("_1D_")] [Random(1)] ulong A)
        {
            uint Opcode = 0x5EE0A820; // CMLT D0, D1, #0
            Bits Op = new Bits(Opcode);

            Vector128<float> V0 = MakeVectorE1(TestContext.CurrentContext.Random.NextULong());
            Vector128<float> V1 = MakeVectorE0(A);
            AThreadState ThreadState = SingleOpcode(Opcode, V0: V0, V1: V1);

            AArch64.V(1, new Bits(A));
            SimdFp.Cmlt_S(Op[23, 22], Op[9, 5], Op[4, 0]);

            Assert.Multiple(() =>
            {
                Assert.That(GetVectorE0(ThreadState.V0), Is.EqualTo(AArch64.V(64, 0).ToUInt64()));
                Assert.That(GetVectorE1(ThreadState.V0), Is.Zero);
            });
        }

        [Test, Description("CMLT <Vd>.<T>, <Vn>.<T>, #0")]
        public void Cmlt_V_8B_4H_2S([ValueSource("_8B4H2S_")] [Random(1)] ulong A,
                                    [Values(0b00u, 0b01u, 0b10u)] uint size) // <8B, 4H, 2S>
        {
            uint Opcode = 0x0E20A820; // CMLT V0.8B, V1.8B, #0
            Opcode |= ((size & 3) << 22);
            Bits Op = new Bits(Opcode);

            Vector128<float> V0 = MakeVectorE1(TestContext.CurrentContext.Random.NextULong());
            Vector128<float> V1 = MakeVectorE0(A);
            AThreadState ThreadState = SingleOpcode(Opcode, V0: V0, V1: V1);

            AArch64.V(1, new Bits(A));
            SimdFp.Cmlt_V(Op[30], Op[23, 22], Op[9, 5], Op[4, 0]);

            Assert.Multiple(() =>
            {
                Assert.That(GetVectorE0(ThreadState.V0), Is.EqualTo(AArch64.V(64, 0).ToUInt64()));
                Assert.That(GetVectorE1(ThreadState.V0), Is.Zero);
            });
        }

        [Test, Pairwise, Description("CMLT <Vd>.<T>, <Vn>.<T>, #0")]
        public void Cmlt_V_16B_8H_4S_2D([ValueSource("_8B4H2S1D_")] [Random(1)] ulong A0,
                                        [ValueSource("_8B4H2S1D_")] [Random(1)] ulong A1,
                                        [Values(0b00u, 0b01u, 0b10u, 0b11u)] uint size) // <16B, 8H, 4S, 2D>
        {
            uint Opcode = 0x4E20A820; // CMLT V0.16B, V1.16B, #0
            Opcode |= ((size & 3) << 22);
            Bits Op = new Bits(Opcode);

            Vector128<float> V1 = MakeVectorE0E1(A0, A1);
            AThreadState ThreadState = SingleOpcode(Opcode, V1: V1);

            AArch64.Vpart(1, 0, new Bits(A0));
            AArch64.Vpart(1, 1, new Bits(A1));
            SimdFp.Cmlt_V(Op[30], Op[23, 22], Op[9, 5], Op[4, 0]);

            Assert.Multiple(() =>
            {
                Assert.That(GetVectorE0(ThreadState.V0), Is.EqualTo(AArch64.Vpart(64, 0, 0).ToUInt64()));
                Assert.That(GetVectorE1(ThreadState.V0), Is.EqualTo(AArch64.Vpart(64, 0, 1).ToUInt64()));
            });
        }

        [Test, Description("CNT <Vd>.<T>, <Vn>.<T>")]
        public void Cnt_V_8B([ValueSource("_8B_")] [Random(1)] ulong A)
        {
            uint Opcode = 0x0E205820; // CNT V0.8B, V1.8B
            Bits Op = new Bits(Opcode);

            Vector128<float> V0 = MakeVectorE1(TestContext.CurrentContext.Random.NextULong());
            Vector128<float> V1 = MakeVectorE0(A);
            AThreadState ThreadState = SingleOpcode(Opcode, V0: V0, V1: V1);

            AArch64.V(1, new Bits(A));
            SimdFp.Cnt_V(Op[30], Op[23, 22], Op[9, 5], Op[4, 0]);

            Assert.Multiple(() =>
            {
                Assert.That(GetVectorE0(ThreadState.V0), Is.EqualTo(AArch64.V(64, 0).ToUInt64()));
                Assert.That(GetVectorE1(ThreadState.V0), Is.Zero);
            });
        }

        [Test, Pairwise, Description("CNT <Vd>.<T>, <Vn>.<T>")]
        public void Cnt_V_16B([ValueSource("_8B_")] [Random(1)] ulong A0,
                              [ValueSource("_8B_")] [Random(1)] ulong A1)
        {
            uint Opcode = 0x4E205820; // CNT V0.16B, V1.16B
            Bits Op = new Bits(Opcode);

            Vector128<float> V1 = MakeVectorE0E1(A0, A1);
            AThreadState ThreadState = SingleOpcode(Opcode, V1: V1);

            AArch64.Vpart(1, 0, new Bits(A0));
            AArch64.Vpart(1, 1, new Bits(A1));
            SimdFp.Cnt_V(Op[30], Op[23, 22], Op[9, 5], Op[4, 0]);

            Assert.Multiple(() =>
            {
                Assert.That(GetVectorE0(ThreadState.V0), Is.EqualTo(AArch64.Vpart(64, 0, 0).ToUInt64()));
                Assert.That(GetVectorE1(ThreadState.V0), Is.EqualTo(AArch64.Vpart(64, 0, 1).ToUInt64()));
            });
        }

        [Test, Description("NEG <V><d>, <V><n>")]
        public void Neg_S_D([ValueSource("_1D_")] [Random(1)] ulong A)
        {
            uint Opcode = 0x7EE0B820; // NEG D0, D1
            Bits Op = new Bits(Opcode);

            Vector128<float> V0 = MakeVectorE1(TestContext.CurrentContext.Random.NextULong());
            Vector128<float> V1 = MakeVectorE0(A);
            AThreadState ThreadState = SingleOpcode(Opcode, V0: V0, V1: V1);

            AArch64.V(1, new Bits(A));
            SimdFp.Neg_S(Op[23, 22], Op[9, 5], Op[4, 0]);

            Assert.Multiple(() =>
            {
                Assert.That(GetVectorE0(ThreadState.V0), Is.EqualTo(AArch64.V(64, 0).ToUInt64()));
                Assert.That(GetVectorE1(ThreadState.V0), Is.Zero);
            });
        }

        [Test, Description("NEG <Vd>.<T>, <Vn>.<T>")]
        public void Neg_V_8B_4H_2S([ValueSource("_8B4H2S_")] [Random(1)] ulong A,
                                   [Values(0b00u, 0b01u, 0b10u)] uint size) // <8B, 4H, 2S>
        {
            uint Opcode = 0x2E20B820; // NEG V0.8B, V1.8B
            Opcode |= ((size & 3) << 22);
            Bits Op = new Bits(Opcode);

            Vector128<float> V0 = MakeVectorE1(TestContext.CurrentContext.Random.NextULong());
            Vector128<float> V1 = MakeVectorE0(A);
            AThreadState ThreadState = SingleOpcode(Opcode, V0: V0, V1: V1);

            AArch64.V(1, new Bits(A));
            SimdFp.Neg_V(Op[30], Op[23, 22], Op[9, 5], Op[4, 0]);

            Assert.Multiple(() =>
            {
                Assert.That(GetVectorE0(ThreadState.V0), Is.EqualTo(AArch64.V(64, 0).ToUInt64()));
                Assert.That(GetVectorE1(ThreadState.V0), Is.Zero);
            });
        }

        [Test, Pairwise, Description("NEG <Vd>.<T>, <Vn>.<T>")]
        public void Neg_V_16B_8H_4S_2D([ValueSource("_8B4H2S1D_")] [Random(1)] ulong A0,
                                       [ValueSource("_8B4H2S1D_")] [Random(1)] ulong A1,
                                       [Values(0b00u, 0b01u, 0b10u, 0b11u)] uint size) // <16B, 8H, 4S, 2D>
        {
            uint Opcode = 0x6E20B820; // NEG V0.16B, V1.16B
            Opcode |= ((size & 3) << 22);
            Bits Op = new Bits(Opcode);

            Vector128<float> V1 = MakeVectorE0E1(A0, A1);
            AThreadState ThreadState = SingleOpcode(Opcode, V1: V1);

            AArch64.Vpart(1, 0, new Bits(A0));
            AArch64.Vpart(1, 1, new Bits(A1));
            SimdFp.Neg_V(Op[30], Op[23, 22], Op[9, 5], Op[4, 0]);

            Assert.Multiple(() =>
            {
                Assert.That(GetVectorE0(ThreadState.V0), Is.EqualTo(AArch64.Vpart(64, 0, 0).ToUInt64()));
                Assert.That(GetVectorE1(ThreadState.V0), Is.EqualTo(AArch64.Vpart(64, 0, 1).ToUInt64()));
            });
        }

        [Test, Description("NOT <Vd>.<T>, <Vn>.<T>")]
        public void Not_V_8B([ValueSource("_8B_")] [Random(1)] ulong A)
        {
            uint Opcode = 0x2E205820; // NOT V0.8B, V1.8B
            Bits Op = new Bits(Opcode);

            Vector128<float> V0 = MakeVectorE1(TestContext.CurrentContext.Random.NextULong());
            Vector128<float> V1 = MakeVectorE0(A);
            AThreadState ThreadState = SingleOpcode(Opcode, V0: V0, V1: V1);

            AArch64.V(1, new Bits(A));
            SimdFp.Not_V(Op[30], Op[9, 5], Op[4, 0]);

            Assert.Multiple(() =>
            {
                Assert.That(GetVectorE0(ThreadState.V0), Is.EqualTo(AArch64.V(64, 0).ToUInt64()));
                Assert.That(GetVectorE1(ThreadState.V0), Is.Zero);
            });
        }

        [Test, Pairwise, Description("NOT <Vd>.<T>, <Vn>.<T>")]
        public void Not_V_16B([ValueSource("_8B_")] [Random(1)] ulong A0,
                              [ValueSource("_8B_")] [Random(1)] ulong A1)
        {
            uint Opcode = 0x6E205820; // NOT V0.16B, V1.16B
            Bits Op = new Bits(Opcode);

            Vector128<float> V1 = MakeVectorE0E1(A0, A1);
            AThreadState ThreadState = SingleOpcode(Opcode, V1: V1);

            AArch64.Vpart(1, 0, new Bits(A0));
            AArch64.Vpart(1, 1, new Bits(A1));
            SimdFp.Not_V(Op[30], Op[9, 5], Op[4, 0]);

            Assert.Multiple(() =>
            {
                Assert.That(GetVectorE0(ThreadState.V0), Is.EqualTo(AArch64.Vpart(64, 0, 0).ToUInt64()));
                Assert.That(GetVectorE1(ThreadState.V0), Is.EqualTo(AArch64.Vpart(64, 0, 1).ToUInt64()));
            });
        }

        [Test, Description("SQXTN <Vb><d>, <Va><n>")]
        public void Sqxtn_S_HB_SH_DS([ValueSource("_1H1S1D_")] [Random(1)] ulong A,
                                     [Values(0b00u, 0b01u, 0b10u)] uint size) // <HB, SH, DS>
        {
            uint Opcode = 0x5E214820; // SQXTN B0, H1
            Opcode |= ((size & 3) << 22);
            Bits Op = new Bits(Opcode);

            Vector128<float> V0 = MakeVectorE0E1(TestContext.CurrentContext.Random.NextULong(),
                                                 TestContext.CurrentContext.Random.NextULong());
            Vector128<float> V1 = MakeVectorE0(A);
            AThreadState ThreadState = SingleOpcode(Opcode, V0: V0, V1: V1);

            AArch64.Vpart(0, 0, new Bits(TestContext.CurrentContext.Random.NextULong()));
            AArch64.V(1, new Bits(A));
            SimdFp.Sqxtn_S(Op[23, 22], Op[9, 5], Op[4, 0]);

            Assert.Multiple(() =>
            {
                Assert.That(GetVectorE0(ThreadState.V0), Is.EqualTo(AArch64.V(64, 0).ToUInt64()));
                Assert.That(GetVectorE1(ThreadState.V0), Is.Zero);
            });
            Assert.That(((ThreadState.Fpsr >> 27) & 1) != 0, Is.EqualTo(Shared.FPSR[27]));
        }

        [Test, Pairwise, Description("SQXTN{2} <Vd>.<Tb>, <Vn>.<Ta>")]
        public void Sqxtn_V_8H8B_4S4H_2D2S([ValueSource("_4H2S1D_")] [Random(1)] ulong A0,
                                           [ValueSource("_4H2S1D_")] [Random(1)] ulong A1,
                                           [Values(0b00u, 0b01u, 0b10u)] uint size) // <8H8B, 4S4H, 2D2S>
        {
            uint Opcode = 0x0E214820; // SQXTN V0.8B, V1.8H
            Opcode |= ((size & 3) << 22);
            Bits Op = new Bits(Opcode);

            Vector128<float> V0 = MakeVectorE1(TestContext.CurrentContext.Random.NextULong());
            Vector128<float> V1 = MakeVectorE0E1(A0, A1);
            AThreadState ThreadState = SingleOpcode(Opcode, V0: V0, V1: V1);

            AArch64.Vpart(1, 0, new Bits(A0));
            AArch64.Vpart(1, 1, new Bits(A1));
            SimdFp.Sqxtn_V(Op[30], Op[23, 22], Op[9, 5], Op[4, 0]);

            Assert.Multiple(() =>
            {
                Assert.That(GetVectorE0(ThreadState.V0), Is.EqualTo(AArch64.V(64, 0).ToUInt64()));
                Assert.That(GetVectorE1(ThreadState.V0), Is.Zero);
            });
            Assert.That(((ThreadState.Fpsr >> 27) & 1) != 0, Is.EqualTo(Shared.FPSR[27]));
        }

        [Test, Pairwise, Description("SQXTN{2} <Vd>.<Tb>, <Vn>.<Ta>")]
        public void Sqxtn_V_8H16B_4S8H_2D4S([ValueSource("_4H2S1D_")] [Random(1)] ulong A0,
                                            [ValueSource("_4H2S1D_")] [Random(1)] ulong A1,
                                            [Values(0b00u, 0b01u, 0b10u)] uint size) // <8H16B, 4S8H, 2D4S>
        {
            uint Opcode = 0x4E214820; // SQXTN2 V0.16B, V1.8H
            Opcode |= ((size & 3) << 22);
            Bits Op = new Bits(Opcode);

            ulong _X0 = TestContext.CurrentContext.Random.NextULong();
            Vector128<float> V0 = MakeVectorE0(_X0);
            Vector128<float> V1 = MakeVectorE0E1(A0, A1);
            AThreadState ThreadState = SingleOpcode(Opcode, V0: V0, V1: V1);

            AArch64.Vpart(1, 0, new Bits(A0));
            AArch64.Vpart(1, 1, new Bits(A1));
            SimdFp.Sqxtn_V(Op[30], Op[23, 22], Op[9, 5], Op[4, 0]);

            Assert.Multiple(() =>
            {
                Assert.That(GetVectorE0(ThreadState.V0), Is.EqualTo(_X0));
                Assert.That(GetVectorE1(ThreadState.V0), Is.EqualTo(AArch64.Vpart(64, 0, 1).ToUInt64()));
            });
            Assert.That(((ThreadState.Fpsr >> 27) & 1) != 0, Is.EqualTo(Shared.FPSR[27]));
        }

        [Test, Description("SQXTUN <Vb><d>, <Va><n>")]
        public void Sqxtun_S_HB_SH_DS([ValueSource("_1H1S1D_")] [Random(1)] ulong A,
                                      [Values(0b00u, 0b01u, 0b10u)] uint size) // <HB, SH, DS>
        {
            uint Opcode = 0x7E212820; // SQXTUN B0, H1
            Opcode |= ((size & 3) << 22);
            Bits Op = new Bits(Opcode);

            Vector128<float> V0 = MakeVectorE0E1(TestContext.CurrentContext.Random.NextULong(),
                                                 TestContext.CurrentContext.Random.NextULong());
            Vector128<float> V1 = MakeVectorE0(A);
            AThreadState ThreadState = SingleOpcode(Opcode, V0: V0, V1: V1);

            AArch64.Vpart(0, 0, new Bits(TestContext.CurrentContext.Random.NextULong()));
            AArch64.V(1, new Bits(A));
            SimdFp.Sqxtun_S(Op[23, 22], Op[9, 5], Op[4, 0]);

            Assert.Multiple(() =>
            {
                Assert.That(GetVectorE0(ThreadState.V0), Is.EqualTo(AArch64.V(64, 0).ToUInt64()));
                Assert.That(GetVectorE1(ThreadState.V0), Is.Zero);
            });
            Assert.That(((ThreadState.Fpsr >> 27) & 1) != 0, Is.EqualTo(Shared.FPSR[27]));
        }

        [Test, Pairwise, Description("SQXTUN{2} <Vd>.<Tb>, <Vn>.<Ta>")]
        public void Sqxtun_V_8H8B_4S4H_2D2S([ValueSource("_4H2S1D_")] [Random(1)] ulong A0,
                                            [ValueSource("_4H2S1D_")] [Random(1)] ulong A1,
                                            [Values(0b00u, 0b01u, 0b10u)] uint size) // <8H8B, 4S4H, 2D2S>
        {
            uint Opcode = 0x2E212820; // SQXTUN V0.8B, V1.8H
            Opcode |= ((size & 3) << 22);
            Bits Op = new Bits(Opcode);

            Vector128<float> V0 = MakeVectorE1(TestContext.CurrentContext.Random.NextULong());
            Vector128<float> V1 = MakeVectorE0E1(A0, A1);
            AThreadState ThreadState = SingleOpcode(Opcode, V0: V0, V1: V1);

            AArch64.Vpart(1, 0, new Bits(A0));
            AArch64.Vpart(1, 1, new Bits(A1));
            SimdFp.Sqxtun_V(Op[30], Op[23, 22], Op[9, 5], Op[4, 0]);

            Assert.Multiple(() =>
            {
                Assert.That(GetVectorE0(ThreadState.V0), Is.EqualTo(AArch64.V(64, 0).ToUInt64()));
                Assert.That(GetVectorE1(ThreadState.V0), Is.Zero);
            });
            Assert.That(((ThreadState.Fpsr >> 27) & 1) != 0, Is.EqualTo(Shared.FPSR[27]));
        }

        [Test, Pairwise, Description("SQXTUN{2} <Vd>.<Tb>, <Vn>.<Ta>")]
        public void Sqxtun_V_8H16B_4S8H_2D4S([ValueSource("_4H2S1D_")] [Random(1)] ulong A0,
                                             [ValueSource("_4H2S1D_")] [Random(1)] ulong A1,
                                             [Values(0b00u, 0b01u, 0b10u)] uint size) // <8H16B, 4S8H, 2D4S>
        {
            uint Opcode = 0x6E212820; // SQXTUN2 V0.16B, V1.8H
            Opcode |= ((size & 3) << 22);
            Bits Op = new Bits(Opcode);

            ulong _X0 = TestContext.CurrentContext.Random.NextULong();
            Vector128<float> V0 = MakeVectorE0(_X0);
            Vector128<float> V1 = MakeVectorE0E1(A0, A1);
            AThreadState ThreadState = SingleOpcode(Opcode, V0: V0, V1: V1);

            AArch64.Vpart(1, 0, new Bits(A0));
            AArch64.Vpart(1, 1, new Bits(A1));
            SimdFp.Sqxtun_V(Op[30], Op[23, 22], Op[9, 5], Op[4, 0]);

            Assert.Multiple(() =>
            {
                Assert.That(GetVectorE0(ThreadState.V0), Is.EqualTo(_X0));
                Assert.That(GetVectorE1(ThreadState.V0), Is.EqualTo(AArch64.Vpart(64, 0, 1).ToUInt64()));
            });
            Assert.That(((ThreadState.Fpsr >> 27) & 1) != 0, Is.EqualTo(Shared.FPSR[27]));
        }

        [Test, Description("UQXTN <Vb><d>, <Va><n>")]
        public void Uqxtn_S_HB_SH_DS([ValueSource("_1H1S1D_")] [Random(1)] ulong A,
                                     [Values(0b00u, 0b01u, 0b10u)] uint size) // <HB, SH, DS>
        {
            uint Opcode = 0x7E214820; // UQXTN B0, H1
            Opcode |= ((size & 3) << 22);
            Bits Op = new Bits(Opcode);

            Vector128<float> V0 = MakeVectorE0E1(TestContext.CurrentContext.Random.NextULong(),
                                                 TestContext.CurrentContext.Random.NextULong());
            Vector128<float> V1 = MakeVectorE0(A);
            AThreadState ThreadState = SingleOpcode(Opcode, V0: V0, V1: V1);

            AArch64.Vpart(0, 0, new Bits(TestContext.CurrentContext.Random.NextULong()));
            AArch64.V(1, new Bits(A));
            SimdFp.Uqxtn_S(Op[23, 22], Op[9, 5], Op[4, 0]);

            Assert.Multiple(() =>
            {
                Assert.That(GetVectorE0(ThreadState.V0), Is.EqualTo(AArch64.V(64, 0).ToUInt64()));
                Assert.That(GetVectorE1(ThreadState.V0), Is.Zero);
            });
            Assert.That(((ThreadState.Fpsr >> 27) & 1) != 0, Is.EqualTo(Shared.FPSR[27]));
        }

        [Test, Pairwise, Description("UQXTN{2} <Vd>.<Tb>, <Vn>.<Ta>")]
        public void Uqxtn_V_8H8B_4S4H_2D2S([ValueSource("_4H2S1D_")] [Random(1)] ulong A0,
                                           [ValueSource("_4H2S1D_")] [Random(1)] ulong A1,
                                           [Values(0b00u, 0b01u, 0b10u)] uint size) // <8H8B, 4S4H, 2D2S>
        {
            uint Opcode = 0x2E214820; // UQXTN V0.8B, V1.8H
            Opcode |= ((size & 3) << 22);
            Bits Op = new Bits(Opcode);

            Vector128<float> V0 = MakeVectorE1(TestContext.CurrentContext.Random.NextULong());
            Vector128<float> V1 = MakeVectorE0E1(A0, A1);
            AThreadState ThreadState = SingleOpcode(Opcode, V0: V0, V1: V1);

            AArch64.Vpart(1, 0, new Bits(A0));
            AArch64.Vpart(1, 1, new Bits(A1));
            SimdFp.Uqxtn_V(Op[30], Op[23, 22], Op[9, 5], Op[4, 0]);

            Assert.Multiple(() =>
            {
                Assert.That(GetVectorE0(ThreadState.V0), Is.EqualTo(AArch64.V(64, 0).ToUInt64()));
                Assert.That(GetVectorE1(ThreadState.V0), Is.Zero);
            });
            Assert.That(((ThreadState.Fpsr >> 27) & 1) != 0, Is.EqualTo(Shared.FPSR[27]));
        }

        [Test, Pairwise, Description("UQXTN{2} <Vd>.<Tb>, <Vn>.<Ta>")]
        public void Uqxtn_V_8H16B_4S8H_2D4S([ValueSource("_4H2S1D_")] [Random(1)] ulong A0,
                                            [ValueSource("_4H2S1D_")] [Random(1)] ulong A1,
                                            [Values(0b00u, 0b01u, 0b10u)] uint size) // <8H16B, 4S8H, 2D4S>
        {
            uint Opcode = 0x6E214820; // UQXTN2 V0.16B, V1.8H
            Opcode |= ((size & 3) << 22);
            Bits Op = new Bits(Opcode);

            ulong _X0 = TestContext.CurrentContext.Random.NextULong();
            Vector128<float> V0 = MakeVectorE0(_X0);
            Vector128<float> V1 = MakeVectorE0E1(A0, A1);
            AThreadState ThreadState = SingleOpcode(Opcode, V0: V0, V1: V1);

            AArch64.Vpart(1, 0, new Bits(A0));
            AArch64.Vpart(1, 1, new Bits(A1));
            SimdFp.Uqxtn_V(Op[30], Op[23, 22], Op[9, 5], Op[4, 0]);

            Assert.Multiple(() =>
            {
                Assert.That(GetVectorE0(ThreadState.V0), Is.EqualTo(_X0));
                Assert.That(GetVectorE1(ThreadState.V0), Is.EqualTo(AArch64.Vpart(64, 0, 1).ToUInt64()));
            });
            Assert.That(((ThreadState.Fpsr >> 27) & 1) != 0, Is.EqualTo(Shared.FPSR[27]));
        }
#endif
    }
}
