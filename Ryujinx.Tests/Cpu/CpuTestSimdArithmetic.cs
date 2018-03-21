using ChocolArm64.State;
using NUnit.Framework;

namespace Ryujinx.Tests.Cpu
{
    public class CpuTestSimdArithmetic : CpuTest
    {
        [TestCase(0xE228420u,   0x0000000000000000ul, 0x0000000000000000ul, 0x0000000000000000ul, 0x0000000000000000ul, 0x0000000000000000ul, 0x0000000000000000ul)]
        [TestCase(0xE228420u,   0x00000000FFFFFFFFul, 0x00000000FFFFFFFFul, 0x0000000000000001ul, 0x0000000000000001ul, 0x00000000FFFFFF00ul, 0x0000000000000000ul)]
        [TestCase(0xE228420u,   0xFFFFFFFFFFFFFFFFul, 0xFFFFFFFFFFFFFFFFul, 0xFFFFFFFFFFFFFFFFul, 0xFFFFFFFFFFFFFFFFul, 0xFEFEFEFEFEFEFEFEul, 0x0000000000000000ul)]
        [TestCase(0xE228420u,   0x0102030405060708ul, 0xAAAAAAAAAAAAAAAAul, 0x0807060504030201ul, 0x2222222222222222ul, 0x0909090909090909ul, 0x0000000000000000ul)]
        [TestCase(0x4E228420u,  0x0000000000000000ul, 0x0000000000000000ul, 0x0000000000000000ul, 0x0000000000000000ul, 0x0000000000000000ul, 0x0000000000000000ul)]
        [TestCase(0x4E228420u,  0x00000000FFFFFFFFul, 0x00000000FFFFFFFFul, 0x0000000000000001ul, 0x0000000000000001ul, 0x00000000FFFFFF00ul, 0x00000000FFFFFF00ul)]
        [TestCase(0x4E228420u,  0xFFFFFFFFFFFFFFFFul, 0xFFFFFFFFFFFFFFFFul, 0xFFFFFFFFFFFFFFFFul, 0xFFFFFFFFFFFFFFFFul, 0xFEFEFEFEFEFEFEFEul, 0xFEFEFEFEFEFEFEFEul)]
        [TestCase(0x4E228420u,  0x0102030405060708ul, 0xAAAAAAAAAAAAAAAAul, 0x0807060504030201ul, 0x2222222222222222ul, 0x0909090909090909ul, 0xCCCCCCCCCCCCCCCCul)]
        [TestCase(0xE628420u,   0x0000000000000000ul, 0x0000000000000000ul, 0x0000000000000000ul, 0x0000000000000000ul, 0x0000000000000000ul, 0x0000000000000000ul)]
        [TestCase(0xE628420u,   0x00000000FFFFFFFFul, 0x00000000FFFFFFFFul, 0x0000000000000001ul, 0x0000000000000001ul, 0x00000000FFFF0000ul, 0x0000000000000000ul)]
        [TestCase(0xE628420u,   0xFFFFFFFFFFFFFFFFul, 0xFFFFFFFFFFFFFFFFul, 0xFFFFFFFFFFFFFFFFul, 0xFFFFFFFFFFFFFFFFul, 0xFFFEFFFEFFFEFFFEul, 0x0000000000000000ul)]
        [TestCase(0xE628420u,   0x0102030405060708ul, 0xAAAAAAAAAAAAAAAAul, 0x0807060504030201ul, 0x2222222222222222ul, 0x0909090909090909ul, 0x0000000000000000ul)]
        [TestCase(0x4E628420u,  0x0000000000000000ul, 0x0000000000000000ul, 0x0000000000000000ul, 0x0000000000000000ul, 0x0000000000000000ul, 0x0000000000000000ul)]
        [TestCase(0x4E628420u,  0x00000000FFFFFFFFul, 0x00000000FFFFFFFFul, 0x0000000000000001ul, 0x0000000000000001ul, 0x00000000FFFF0000ul, 0x00000000FFFF0000ul)]
        [TestCase(0x4E628420u,  0xFFFFFFFFFFFFFFFFul, 0xFFFFFFFFFFFFFFFFul, 0xFFFFFFFFFFFFFFFFul, 0xFFFFFFFFFFFFFFFFul, 0xFFFEFFFEFFFEFFFEul, 0xFFFEFFFEFFFEFFFEul)]
        [TestCase(0x4E628420u,  0x0102030405060708ul, 0xAAAAAAAAAAAAAAAAul, 0x0807060504030201ul, 0x2222222222222222ul, 0x0909090909090909ul, 0xCCCCCCCCCCCCCCCCul)]
        [TestCase(0xEA28420u,   0x0000000000000000ul, 0x0000000000000000ul, 0x0000000000000000ul, 0x0000000000000000ul, 0x0000000000000000ul, 0x0000000000000000ul)]
        [TestCase(0xEA28420u,   0x00000000FFFFFFFFul, 0x00000000FFFFFFFFul, 0x0000000000000001ul, 0x0000000000000001ul, 0x0000000000000000ul, 0x0000000000000000ul)]
        [TestCase(0xEA28420u,   0xFFFFFFFFFFFFFFFFul, 0xFFFFFFFFFFFFFFFFul, 0xFFFFFFFFFFFFFFFFul, 0xFFFFFFFFFFFFFFFFul, 0xFFFFFFFEFFFFFFFEul, 0x0000000000000000ul)]
        [TestCase(0xEA28420u,   0x0102030405060708ul, 0xAAAAAAAAAAAAAAAAul, 0x0807060504030201ul, 0x2222222222222222ul, 0x0909090909090909ul, 0x0000000000000000ul)]
        [TestCase(0x4EA28420u,  0x0000000000000000ul, 0x0000000000000000ul, 0x0000000000000000ul, 0x0000000000000000ul, 0x0000000000000000ul, 0x0000000000000000ul)]
        [TestCase(0x4EA28420u,  0x00000000FFFFFFFFul, 0x00000000FFFFFFFFul, 0x0000000000000001ul, 0x0000000000000001ul, 0x0000000000000000ul, 0x0000000000000000ul)]
        [TestCase(0x4EA28420u,  0xFFFFFFFFFFFFFFFFul, 0xFFFFFFFFFFFFFFFFul, 0xFFFFFFFFFFFFFFFFul, 0xFFFFFFFFFFFFFFFFul, 0xFFFFFFFEFFFFFFFEul, 0xFFFFFFFEFFFFFFFEul)]
        [TestCase(0x4EA28420u,  0x0102030405060708ul, 0xAAAAAAAAAAAAAAAAul, 0x0807060504030201ul, 0x2222222222222222ul, 0x0909090909090909ul, 0xCCCCCCCCCCCCCCCCul)]
        [TestCase(0x4EE28420u,  0x0000000000000000ul, 0x0000000000000000ul, 0x0000000000000000ul, 0x0000000000000000ul, 0x0000000000000000ul, 0x0000000000000000ul)]
        [TestCase(0x4EE28420u,  0x00000000FFFFFFFFul, 0x00000000FFFFFFFFul, 0x0000000000000001ul, 0x0000000000000001ul, 0x0000000100000000ul, 0x0000000100000000ul)]
        [TestCase(0x4EE28420u,  0xFFFFFFFFFFFFFFFFul, 0xFFFFFFFFFFFFFFFFul, 0xFFFFFFFFFFFFFFFFul, 0xFFFFFFFFFFFFFFFFul, 0xFFFFFFFFFFFFFFFEul, 0xFFFFFFFFFFFFFFFEul)]
        [TestCase(0x4EE28420u,  0x0102030405060708ul, 0xAAAAAAAAAAAAAAAAul, 0x0807060504030201ul, 0x2222222222222222ul, 0x0909090909090909ul, 0xCCCCCCCCCCCCCCCCul)]
        public void Add_V(uint Opcode, ulong A0, ulong A1, ulong B0, ulong B1, ulong Result0, ulong Result1)
        {
            AVec V1 = new AVec { X0 = A0, X1 = A1 };
            AVec V2 = new AVec { X0 = B0, X1 = B1 };
            AThreadState ThreadState = SingleOpcode(Opcode, V1: V1, V2: V2);
            Assert.Multiple(() =>
            {
                Assert.AreEqual(Result0, ThreadState.V0.X0);
                Assert.AreEqual(Result1, ThreadState.V0.X1);
            });
        }
    
        [TestCase(0x3FE66666u, false, 0x40000000u)]
        [TestCase(0x3F99999Au, false, 0x3F800000u)]
        [TestCase(0x404CCCCDu, false, 0x40400000u)]
        [TestCase(0x40733333u, false, 0x40800000u)]
        [TestCase(0x3fc00000u, false, 0x40000000u)]
        [TestCase(0x40200000u, false, 0x40400000u)]
        [TestCase(0x00000000u, false, 0x00000000u)]
        [TestCase(0x00000000u, false, 0x00000000u)]
        [TestCase(0x00000000u, false, 0x00000000u)]
        [TestCase(0x00000000u, false, 0x00000000u)]
        [TestCase(0x80000000u, false, 0x80000000u)]
        [TestCase(0x80000000u, false, 0x80000000u)]
        [TestCase(0x80000000u, false, 0x80000000u)]
        [TestCase(0x80000000u, false, 0x80000000u)]
        [TestCase(0x7F800000u, false, 0x7F800000u)]
        [TestCase(0x7F800000u, false, 0x7F800000u)]
        [TestCase(0x7F800000u, false, 0x7F800000u)]
        [TestCase(0x7F800000u, false, 0x7F800000u)]
        [TestCase(0xFF800000u, false, 0xFF800000u)]
        [TestCase(0xFF800000u, false, 0xFF800000u)]
        [TestCase(0xFF800000u, false, 0xFF800000u)]
        [TestCase(0xFF800000u, false, 0xFF800000u)]
        [TestCase(0xFF800001u, false, 0xFFC00001u)]
        [TestCase(0xFF800001u, false, 0xFFC00001u)]
        [TestCase(0xFF800001u, false, 0xFFC00001u)]
        [TestCase(0xFF800001u, false, 0xFFC00001u)]
        [TestCase(0xFF800001u, true,  0x7FC00000u)]
        [TestCase(0xFF800001u, true,  0x7FC00000u)]
        [TestCase(0xFF800001u, true,  0x7FC00000u)]
        [TestCase(0xFF800001u, true,  0x7FC00000u)]
        [TestCase(0x7FC00002u, false, 0x7FC00002u)]
        [TestCase(0x7FC00002u, false, 0x7FC00002u)]
        [TestCase(0x7FC00002u, false, 0x7FC00002u)]
        [TestCase(0x7FC00002u, false, 0x7FC00002u)]
        [TestCase(0x7FC00002u, true,  0x7FC00000u)]
        [TestCase(0x7FC00002u, true,  0x7FC00000u)]
        [TestCase(0x7FC00002u, true,  0x7FC00000u)]
        [TestCase(0x7FC00002u, true,  0x7FC00000u)]
        public void Frinta_S(uint A, bool DefaultNaN, uint Result)
        {
            int FpcrTemp = 0x0;
            if(DefaultNaN)
            {
                FpcrTemp = 0x2000000;
            }
            AVec V1 = new AVec { X0 = A };
            AThreadState ThreadState = SingleOpcode(0x1E264020, V1: V1, Fpcr: FpcrTemp);
            Assert.AreEqual(Result, ThreadState.V0.X0);
        }

        [TestCase(0x6E618820u, 0x3FF3333333333333ul, 0x3FF3333333333333ul, false, 0x3FF0000000000000ul, 0x3FF0000000000000ul)]
        [TestCase(0x6E618820u, 0x3FFCCCCCCCCCCCCDul, 0x3FFCCCCCCCCCCCCDul, false, 0x4000000000000000ul, 0x4000000000000000ul)]
        [TestCase(0x6E618820u, 0x3FF8000000000000ul, 0x3FF8000000000000ul, false, 0x4000000000000000ul, 0x4000000000000000ul)]
        [TestCase(0x6E219820u, 0x3f99999a3fe66666ul, 0x3f99999a3fe66666ul, false, 0x3f80000040000000ul, 0x3f80000040000000ul)]
        [TestCase(0x6E219820u, 0x3fc000003fc00000ul, 0x3fc000003fc00000ul, false, 0x4000000040000000ul, 0x4000000040000000ul)]    
        [TestCase(0x2E219820u, 0x3f99999a3fe66666ul, 0x3f99999a3fe66666ul, false, 0x3f80000040000000ul, 0x0000000000000000ul)]    
        [TestCase(0x2E219820u, 0x3fc000003fc00000ul, 0x3fc000003fc00000ul, false, 0x4000000040000000ul, 0x0000000000000000ul)]
        [TestCase(0x2E218820u, 0x0000000080000000ul, 0x0000000000000000ul, false, 0x0000000080000000ul, 0x0000000000000000ul)]
        [TestCase(0x2E218820u, 0x7F800000FF800000ul, 0x0000000000000000ul, false, 0x7F800000FF800000ul, 0x0000000000000000ul)]
        [TestCase(0x2E218820u, 0xFF8000017FC00002ul, 0x0000000000000000ul, false, 0xFFC000017FC00002ul, 0x0000000000000000ul)]
        [TestCase(0x2E218820u, 0xFF8000017FC00002ul, 0x0000000000000000ul, true,  0x7FC000007FC00000ul, 0x0000000000000000ul)]
        public void Frinta_V(uint Opcode, ulong A, ulong B, bool DefaultNaN, ulong Result0, ulong Result1)
        {
            int FpcrTemp = 0x0;
            if(DefaultNaN)
            {
                FpcrTemp = 0x2000000;
            }
            AVec V1 = new AVec { X0 = A, X1 = B };
            AThreadState ThreadState = SingleOpcode(Opcode, V1: V1, Fpcr: FpcrTemp);
            Assert.Multiple(() =>
            {
                Assert.AreEqual(Result0, ThreadState.V0.X0);
                Assert.AreEqual(Result1, ThreadState.V0.X1);
            });
        }

        [TestCase(0x3FE66666u, 'N', false, 0x40000000u)]
        [TestCase(0x3F99999Au, 'N', false, 0x3F800000u)]
        [TestCase(0x404CCCCDu, 'P', false, 0x40800000u)]
        [TestCase(0x40733333u, 'P', false, 0x40800000u)]
        [TestCase(0x404CCCCDu, 'M', false, 0x40400000u)]
        [TestCase(0x40733333u, 'M', false, 0x40400000u)]
        [TestCase(0x3F99999Au, 'Z', false, 0x3F800000u)]
        [TestCase(0x3FE66666u, 'Z', false, 0x3F800000u)]
        [TestCase(0x00000000u, 'N', false, 0x00000000u)]
        [TestCase(0x00000000u, 'P', false, 0x00000000u)]
        [TestCase(0x00000000u, 'M', false, 0x00000000u)]
        [TestCase(0x00000000u, 'Z', false, 0x00000000u)]
        [TestCase(0x80000000u, 'N', false, 0x80000000u)]
        [TestCase(0x80000000u, 'P', false, 0x80000000u)]
        [TestCase(0x80000000u, 'M', false, 0x80000000u)]
        [TestCase(0x80000000u, 'Z', false, 0x80000000u)]
        [TestCase(0x7F800000u, 'N', false, 0x7F800000u)]
        [TestCase(0x7F800000u, 'P', false, 0x7F800000u)]
        [TestCase(0x7F800000u, 'M', false, 0x7F800000u)]
        [TestCase(0x7F800000u, 'Z', false, 0x7F800000u)]
        [TestCase(0xFF800000u, 'N', false, 0xFF800000u)]
        [TestCase(0xFF800000u, 'P', false, 0xFF800000u)]
        [TestCase(0xFF800000u, 'M', false, 0xFF800000u)]
        [TestCase(0xFF800000u, 'Z', false, 0xFF800000u)]
        [TestCase(0xFF800001u, 'N', false, 0xFFC00001u)]
        [TestCase(0xFF800001u, 'P', false, 0xFFC00001u)]
        [TestCase(0xFF800001u, 'M', false, 0xFFC00001u)]
        [TestCase(0xFF800001u, 'Z', false, 0xFFC00001u)]
        [TestCase(0xFF800001u, 'N', true,  0x7FC00000u)]
        [TestCase(0xFF800001u, 'P', true,  0x7FC00000u)]
        [TestCase(0xFF800001u, 'M', true,  0x7FC00000u)]
        [TestCase(0xFF800001u, 'Z', true,  0x7FC00000u)]
        [TestCase(0x7FC00002u, 'N', false, 0x7FC00002u)]
        [TestCase(0x7FC00002u, 'P', false, 0x7FC00002u)]
        [TestCase(0x7FC00002u, 'M', false, 0x7FC00002u)]
        [TestCase(0x7FC00002u, 'Z', false, 0x7FC00002u)]
        [TestCase(0x7FC00002u, 'N', true,  0x7FC00000u)]
        [TestCase(0x7FC00002u, 'P', true,  0x7FC00000u)]
        [TestCase(0x7FC00002u, 'M', true,  0x7FC00000u)]
        [TestCase(0x7FC00002u, 'Z', true,  0x7FC00000u)]
        public void Frinti_S(uint A, char RoundType, bool DefaultNaN, uint Result)
        {
            int FpcrTemp = 0x0;
            switch(RoundType)
            {
                case 'N':
                FpcrTemp = 0x0;
                break;

                case 'P':
                FpcrTemp = 0x400000;
                break;

                case 'M':
                FpcrTemp = 0x800000;
                break;

                case 'Z':
                FpcrTemp = 0xC00000;
                break;
            }
            if(DefaultNaN)
            {
                FpcrTemp |= 1 << 25;
            }
            AVec V1 = new AVec { X0 = A };
            AThreadState ThreadState = SingleOpcode(0x1E27C020, V1: V1, Fpcr: FpcrTemp);
            Assert.AreEqual(Result, ThreadState.V0.X0);
        }

        [TestCase(0x6EE19820u, 0x3FF3333333333333ul, 0x3FF3333333333333ul, 'N', false, 0x3FF0000000000000ul, 0x3FF0000000000000ul)]
        [TestCase(0x6EE19820u, 0x3FFCCCCCCCCCCCCDul, 0x3FFCCCCCCCCCCCCDul, 'N', false, 0x4000000000000000ul, 0x4000000000000000ul)]
        [TestCase(0x6EE19820u, 0x3FF3333333333333ul, 0x3FF3333333333333ul, 'P', false, 0x4000000000000000ul, 0x4000000000000000ul)]
        [TestCase(0x6EE19820u, 0x3FFCCCCCCCCCCCCDul, 0x3FFCCCCCCCCCCCCDul, 'P', false, 0x4000000000000000ul, 0x4000000000000000ul)]
        [TestCase(0x6EE19820u, 0x3FF3333333333333ul, 0x3FF3333333333333ul, 'M', false, 0x3FF0000000000000ul, 0x3FF0000000000000ul)]
        [TestCase(0x6EE19820u, 0x3FFCCCCCCCCCCCCDul, 0x3FFCCCCCCCCCCCCDul, 'M', false, 0x3FF0000000000000ul, 0x3FF0000000000000ul)]
        [TestCase(0x6EE19820u, 0x3FF3333333333333ul, 0x3FF3333333333333ul, 'Z', false, 0x3FF0000000000000ul, 0x3FF0000000000000ul)]
        [TestCase(0x6EE19820u, 0x3FFCCCCCCCCCCCCDul, 0x3FFCCCCCCCCCCCCDul, 'Z', false, 0x3FF0000000000000ul, 0x3FF0000000000000ul)]
        [TestCase(0x6EA19820u, 0x3f99999a3fe66666ul, 0x3f99999a3fe66666ul, 'N', false, 0x3f80000040000000ul, 0x3f80000040000000ul)]
        [TestCase(0x6EA19820u, 0x3f99999a3fe66666ul, 0x3f99999a3fe66666ul, 'P', false, 0x4000000040000000ul, 0x4000000040000000ul)]    
        [TestCase(0x6EA19820u, 0x3f99999a3fe66666ul, 0x3f99999a3fe66666ul, 'M', false, 0x3f8000003f800000ul, 0x3f8000003f800000ul)]
        [TestCase(0x6EA19820u, 0x3f99999a3fe66666ul, 0x3f99999a3fe66666ul, 'Z', false, 0x3f8000003f800000ul, 0x3f8000003f800000ul)]
        [TestCase(0x2EA19820u, 0x3f99999a3fe66666ul, 0x3f99999a3fe66666ul, 'N', false, 0x3f80000040000000ul, 0x0000000000000000ul)]
        [TestCase(0x2EA19820u, 0x3f99999a3fe66666ul, 0x3f99999a3fe66666ul, 'P', false, 0x4000000040000000ul, 0x0000000000000000ul)]    
        [TestCase(0x2EA19820u, 0x3f99999a3fe66666ul, 0x3f99999a3fe66666ul, 'M', false, 0x3f8000003f800000ul, 0x0000000000000000ul)]
        [TestCase(0x2EA19820u, 0x3f99999a3fe66666ul, 0x3f99999a3fe66666ul, 'Z', false, 0x3f8000003f800000ul, 0x0000000000000000ul)]
        [TestCase(0x2EA19820u, 0x0000000080000000ul, 0x0000000000000000ul, 'N', false, 0x0000000080000000ul, 0x0000000000000000ul)]
        [TestCase(0x2EA19820u, 0x0000000080000000ul, 0x0000000000000000ul, 'P', false, 0x0000000080000000ul, 0x0000000000000000ul)]
        [TestCase(0x2EA19820u, 0x0000000080000000ul, 0x0000000000000000ul, 'M', false, 0x0000000080000000ul, 0x0000000000000000ul)]
        [TestCase(0x2EA19820u, 0x0000000080000000ul, 0x0000000000000000ul, 'Z', false, 0x0000000080000000ul, 0x0000000000000000ul)]
        [TestCase(0x2EA19820u, 0x7F800000FF800000ul, 0x0000000000000000ul, 'N', false, 0x7F800000FF800000ul, 0x0000000000000000ul)]
        [TestCase(0x2EA19820u, 0x7F800000FF800000ul, 0x0000000000000000ul, 'P', false, 0x7F800000FF800000ul, 0x0000000000000000ul)]
        [TestCase(0x2EA19820u, 0x7F800000FF800000ul, 0x0000000000000000ul, 'M', false, 0x7F800000FF800000ul, 0x0000000000000000ul)]
        [TestCase(0x2EA19820u, 0x7F800000FF800000ul, 0x0000000000000000ul, 'Z', false, 0x7F800000FF800000ul, 0x0000000000000000ul)]
        [TestCase(0x2EA19820u, 0xFF8000017FC00002ul, 0x0000000000000000ul, 'N', false, 0xFFC000017FC00002ul, 0x0000000000000000ul)]
        [TestCase(0x2EA19820u, 0xFF8000017FC00002ul, 0x0000000000000000ul, 'P', false, 0xFFC000017FC00002ul, 0x0000000000000000ul)]
        [TestCase(0x2EA19820u, 0xFF8000017FC00002ul, 0x0000000000000000ul, 'M', false, 0xFFC000017FC00002ul, 0x0000000000000000ul)]
        [TestCase(0x2EA19820u, 0xFF8000017FC00002ul, 0x0000000000000000ul, 'Z', false, 0xFFC000017FC00002ul, 0x0000000000000000ul)]
        [TestCase(0x2EA19820u, 0xFF8000017FC00002ul, 0x0000000000000000ul, 'N', true,  0x7FC000007FC00000ul, 0x0000000000000000ul)]
        [TestCase(0x2EA19820u, 0xFF8000017FC00002ul, 0x0000000000000000ul, 'P', true,  0x7FC000007FC00000ul, 0x0000000000000000ul)]
        [TestCase(0x2EA19820u, 0xFF8000017FC00002ul, 0x0000000000000000ul, 'M', true,  0x7FC000007FC00000ul, 0x0000000000000000ul)]
        [TestCase(0x2EA19820u, 0xFF8000017FC00002ul, 0x0000000000000000ul, 'Z', true,  0x7FC000007FC00000ul, 0x0000000000000000ul)]
        public void Frinti_V(uint Opcode, ulong A, ulong B, char RoundType, bool DefaultNaN, ulong Result0, ulong Result1)
        {
            int FpcrTemp = 0x0;
            switch(RoundType)
            {
                case 'N':
                FpcrTemp = 0x0;
                break;

                case 'P':
                FpcrTemp = 0x400000;
                break;

                case 'M':
                FpcrTemp = 0x800000;
                break;

                case 'Z':
                FpcrTemp = 0xC00000;
                break;
            }
            if(DefaultNaN)
            {
                FpcrTemp |= 1 << 25;
            }
            AVec V1 = new AVec { X0 = A, X1 = B };
            AThreadState ThreadState = SingleOpcode(Opcode, V1: V1, Fpcr: FpcrTemp);
            Assert.Multiple(() =>
            {
                Assert.AreEqual(Result0, ThreadState.V0.X0);
                Assert.AreEqual(Result1, ThreadState.V0.X1);
            });
        }

        [TestCase(0x3FE66666u, false, 0x3F800000u)]
        [TestCase(0x3F99999Au, false, 0x3F800000u)]
        [TestCase(0x404CCCCDu, false, 0x40400000u)]
        [TestCase(0x40733333u, false, 0x40400000u)]
        [TestCase(0x3fc00000u, false, 0x3F800000u)]
        [TestCase(0x40200000u, false, 0x40000000u)]
        [TestCase(0x00000000u, false, 0x00000000u)]
        [TestCase(0x00000000u, false, 0x00000000u)]
        [TestCase(0x00000000u, false, 0x00000000u)]
        [TestCase(0x00000000u, false, 0x00000000u)]
        [TestCase(0x80000000u, false, 0x80000000u)]
        [TestCase(0x80000000u, false, 0x80000000u)]
        [TestCase(0x80000000u, false, 0x80000000u)]
        [TestCase(0x80000000u, false, 0x80000000u)]
        [TestCase(0x7F800000u, false, 0x7F800000u)]
        [TestCase(0x7F800000u, false, 0x7F800000u)]
        [TestCase(0x7F800000u, false, 0x7F800000u)]
        [TestCase(0x7F800000u, false, 0x7F800000u)]
        [TestCase(0xFF800000u, false, 0xFF800000u)]
        [TestCase(0xFF800000u, false, 0xFF800000u)]
        [TestCase(0xFF800000u, false, 0xFF800000u)]
        [TestCase(0xFF800000u, false, 0xFF800000u)]
        [TestCase(0xFF800001u, false, 0xFFC00001u)]
        [TestCase(0xFF800001u, false, 0xFFC00001u)]
        [TestCase(0xFF800001u, false, 0xFFC00001u)]
        [TestCase(0xFF800001u, false, 0xFFC00001u)]
        [TestCase(0xFF800001u, true,  0x7FC00000u)]
        [TestCase(0xFF800001u, true,  0x7FC00000u)]
        [TestCase(0xFF800001u, true,  0x7FC00000u)]
        [TestCase(0xFF800001u, true,  0x7FC00000u)]
        [TestCase(0x7FC00002u, false, 0x7FC00002u)]
        [TestCase(0x7FC00002u, false, 0x7FC00002u)]
        [TestCase(0x7FC00002u, false, 0x7FC00002u)]
        [TestCase(0x7FC00002u, false, 0x7FC00002u)]
        [TestCase(0x7FC00002u, true,  0x7FC00000u)]
        [TestCase(0x7FC00002u, true,  0x7FC00000u)]
        [TestCase(0x7FC00002u, true,  0x7FC00000u)]
        [TestCase(0x7FC00002u, true,  0x7FC00000u)]
        public void Frintm_S(uint A, bool DefaultNaN, uint Result)
        {
            int FpcrTemp = 0x0;
            if(DefaultNaN)
            {
                FpcrTemp = 0x2000000;
            }
            AVec V1 = new AVec { X0 = A };
            AThreadState ThreadState = SingleOpcode(0x1E254020, V1: V1, Fpcr: FpcrTemp);
            Assert.AreEqual(Result, ThreadState.V0.X0);
        }

        [TestCase(0x4E619820u, 0x3FF3333333333333ul, 0x3FF3333333333333ul, false, 0x3FF0000000000000ul, 0x3FF0000000000000ul)]
        [TestCase(0x4E619820u, 0x3FFCCCCCCCCCCCCDul, 0x3FFCCCCCCCCCCCCDul, false, 0x3FF0000000000000ul, 0x3FF0000000000000ul)]
        [TestCase(0x4E219820u, 0x3f99999a3fe66666ul, 0x3f99999a3fe66666ul, false, 0x3f8000003f800000ul, 0x3f8000003f800000ul)]    
        [TestCase(0xE219820u,  0x3f99999a3fe66666ul, 0x3f99999a3fe66666ul, false, 0x3f8000003f800000ul, 0x0000000000000000ul)]    
        [TestCase(0xE219820u,  0x0000000080000000ul, 0x0000000000000000ul, false, 0x0000000080000000ul, 0x0000000000000000ul)]
        [TestCase(0xE219820u,  0x7F800000FF800000ul, 0x0000000000000000ul, false, 0x7F800000FF800000ul, 0x0000000000000000ul)]
        [TestCase(0xE219820u,  0xFF8000017FC00002ul, 0x0000000000000000ul, false, 0xFFC000017FC00002ul, 0x0000000000000000ul)]
        [TestCase(0xE219820u,  0xFF8000017FC00002ul, 0x0000000000000000ul, true,  0x7FC000007FC00000ul, 0x0000000000000000ul)]
        public void Frintm_V(uint Opcode, ulong A, ulong B, bool DefaultNaN, ulong Result0, ulong Result1)
        {
            int FpcrTemp = 0x0;
            if(DefaultNaN)
            {
                FpcrTemp = 0x2000000;
            }
            AVec V1 = new AVec { X0 = A, X1 = B };
            AThreadState ThreadState = SingleOpcode(Opcode, V1: V1, Fpcr: FpcrTemp);
            Assert.Multiple(() =>
            {
                Assert.AreEqual(Result0, ThreadState.V0.X0);
                Assert.AreEqual(Result1, ThreadState.V0.X1);
            });
        }

        [TestCase(0x3FE66666u, false, 0x40000000u)]
        [TestCase(0x3F99999Au, false, 0x3F800000u)]
        [TestCase(0x404CCCCDu, false, 0x40400000u)]
        [TestCase(0x40733333u, false, 0x40800000u)]
        [TestCase(0x3fc00000u, false, 0x40000000u)]
        [TestCase(0x40200000u, false, 0x40400000u)]
        [TestCase(0x00000000u, false, 0x00000000u)]
        [TestCase(0x00000000u, false, 0x00000000u)]
        [TestCase(0x00000000u, false, 0x00000000u)]
        [TestCase(0x00000000u, false, 0x00000000u)]
        [TestCase(0x80000000u, false, 0x80000000u)]
        [TestCase(0x80000000u, false, 0x80000000u)]
        [TestCase(0x80000000u, false, 0x80000000u)]
        [TestCase(0x80000000u, false, 0x80000000u)]
        [TestCase(0x7F800000u, false, 0x7F800000u)]
        [TestCase(0x7F800000u, false, 0x7F800000u)]
        [TestCase(0x7F800000u, false, 0x7F800000u)]
        [TestCase(0x7F800000u, false, 0x7F800000u)]
        [TestCase(0xFF800000u, false, 0xFF800000u)]
        [TestCase(0xFF800000u, false, 0xFF800000u)]
        [TestCase(0xFF800000u, false, 0xFF800000u)]
        [TestCase(0xFF800000u, false, 0xFF800000u)]
        [TestCase(0xFF800001u, false, 0xFFC00001u)]
        [TestCase(0xFF800001u, false, 0xFFC00001u)]
        [TestCase(0xFF800001u, false, 0xFFC00001u)]
        [TestCase(0xFF800001u, false, 0xFFC00001u)]
        [TestCase(0xFF800001u, true,  0x7FC00000u)]
        [TestCase(0xFF800001u, true,  0x7FC00000u)]
        [TestCase(0xFF800001u, true,  0x7FC00000u)]
        [TestCase(0xFF800001u, true,  0x7FC00000u)]
        [TestCase(0x7FC00002u, false, 0x7FC00002u)]
        [TestCase(0x7FC00002u, false, 0x7FC00002u)]
        [TestCase(0x7FC00002u, false, 0x7FC00002u)]
        [TestCase(0x7FC00002u, false, 0x7FC00002u)]
        [TestCase(0x7FC00002u, true,  0x7FC00000u)]
        [TestCase(0x7FC00002u, true,  0x7FC00000u)]
        [TestCase(0x7FC00002u, true,  0x7FC00000u)]
        [TestCase(0x7FC00002u, true,  0x7FC00000u)]
        public void Frintn_S(uint A, bool DefaultNaN, uint Result)
        {
            int FpcrTemp = 0x0;
            if(DefaultNaN)
            {
                FpcrTemp = 0x2000000;
            }
            AVec V1 = new AVec { X0 = A };
            AThreadState ThreadState = SingleOpcode(0x1E264020, V1: V1, Fpcr: FpcrTemp);
            Assert.AreEqual(Result, ThreadState.V0.X0);
        }

        [TestCase(0x4E618820u, 0x3FF3333333333333ul, 0x3FF3333333333333ul, false, 0x3FF0000000000000ul, 0x3FF0000000000000ul)]
        [TestCase(0x4E618820u, 0x3FFCCCCCCCCCCCCDul, 0x3FFCCCCCCCCCCCCDul, false, 0x4000000000000000ul, 0x4000000000000000ul)]
        [TestCase(0x4E618820u, 0x3FF8000000000000ul, 0x3FF8000000000000ul, false, 0x4000000000000000ul, 0x4000000000000000ul)]
        [TestCase(0x4E218820u, 0x3f99999a3fe66666ul, 0x3f99999a3fe66666ul, false, 0x3f80000040000000ul, 0x3f80000040000000ul)]
        [TestCase(0x4E218820u, 0x3fc000003fc00000ul, 0x3fc000003fc00000ul, false, 0x4000000040000000ul, 0x4000000040000000ul)]    
        [TestCase(0xE218820u,  0x3f99999a3fe66666ul, 0x3f99999a3fe66666ul, false, 0x3f80000040000000ul, 0x0000000000000000ul)]    
        [TestCase(0xE218820u,  0x3fc000003fc00000ul, 0x3fc000003fc00000ul, false, 0x4000000040000000ul, 0x0000000000000000ul)]
        [TestCase(0xE218820u,  0x0000000080000000ul, 0x0000000000000000ul, false, 0x0000000080000000ul, 0x0000000000000000ul)]
        [TestCase(0xE218820u,  0x7F800000FF800000ul, 0x0000000000000000ul, false, 0x7F800000FF800000ul, 0x0000000000000000ul)]
        [TestCase(0xE218820u,  0xFF8000017FC00002ul, 0x0000000000000000ul, false, 0xFFC000017FC00002ul, 0x0000000000000000ul)]
        [TestCase(0xE218820u,  0xFF8000017FC00002ul, 0x0000000000000000ul, true,  0x7FC000007FC00000ul, 0x0000000000000000ul)]
        public void Frintn_V(uint Opcode, ulong A, ulong B, bool DefaultNaN, ulong Result0, ulong Result1)
        {
            int FpcrTemp = 0x0;
            if(DefaultNaN)
            {
                FpcrTemp = 0x2000000;
            }
            AVec V1 = new AVec { X0 = A, X1 = B };
            AThreadState ThreadState = SingleOpcode(Opcode, V1: V1, Fpcr: FpcrTemp);
            Assert.Multiple(() =>
            {
                Assert.AreEqual(Result0, ThreadState.V0.X0);
                Assert.AreEqual(Result1, ThreadState.V0.X1);
            });
        }

        [TestCase(0x3FE66666u, false, 0x40000000u)]
        [TestCase(0x3F99999Au, false, 0x40000000u)]
        [TestCase(0x404CCCCDu, false, 0x40800000u)]
        [TestCase(0x40733333u, false, 0x40800000u)]
        [TestCase(0x3fc00000u, false, 0x40000000u)]
        [TestCase(0x40200000u, false, 0x40400000u)]
        [TestCase(0x00000000u, false, 0x00000000u)]
        [TestCase(0x00000000u, false, 0x00000000u)]
        [TestCase(0x00000000u, false, 0x00000000u)]
        [TestCase(0x00000000u, false, 0x00000000u)]
        [TestCase(0x80000000u, false, 0x80000000u)]
        [TestCase(0x80000000u, false, 0x80000000u)]
        [TestCase(0x80000000u, false, 0x80000000u)]
        [TestCase(0x80000000u, false, 0x80000000u)]
        [TestCase(0x7F800000u, false, 0x7F800000u)]
        [TestCase(0x7F800000u, false, 0x7F800000u)]
        [TestCase(0x7F800000u, false, 0x7F800000u)]
        [TestCase(0x7F800000u, false, 0x7F800000u)]
        [TestCase(0xFF800000u, false, 0xFF800000u)]
        [TestCase(0xFF800000u, false, 0xFF800000u)]
        [TestCase(0xFF800000u, false, 0xFF800000u)]
        [TestCase(0xFF800000u, false, 0xFF800000u)]
        [TestCase(0xFF800001u, false, 0xFFC00001u)]
        [TestCase(0xFF800001u, false, 0xFFC00001u)]
        [TestCase(0xFF800001u, false, 0xFFC00001u)]
        [TestCase(0xFF800001u, false, 0xFFC00001u)]
        [TestCase(0xFF800001u, true,  0x7FC00000u)]
        [TestCase(0xFF800001u, true,  0x7FC00000u)]
        [TestCase(0xFF800001u, true,  0x7FC00000u)]
        [TestCase(0xFF800001u, true,  0x7FC00000u)]
        [TestCase(0x7FC00002u, false, 0x7FC00002u)]
        [TestCase(0x7FC00002u, false, 0x7FC00002u)]
        [TestCase(0x7FC00002u, false, 0x7FC00002u)]
        [TestCase(0x7FC00002u, false, 0x7FC00002u)]
        [TestCase(0x7FC00002u, true,  0x7FC00000u)]
        [TestCase(0x7FC00002u, true,  0x7FC00000u)]
        [TestCase(0x7FC00002u, true,  0x7FC00000u)]
        [TestCase(0x7FC00002u, true,  0x7FC00000u)]
        public void Frintp_S(uint A, bool DefaultNaN, uint Result)
        {
            int FpcrTemp = 0x0;
            if(DefaultNaN)
            {
                FpcrTemp = 0x2000000;
            }
            AVec V1 = new AVec { X0 = A };
            AThreadState ThreadState = SingleOpcode(0x1E24C020, V1: V1, Fpcr: FpcrTemp);
            Assert.AreEqual(Result, ThreadState.V0.X0);
        }

        [TestCase(0x4EE18820u, 0x3FF3333333333333ul, 0x3FF3333333333333ul, false, 0x4000000000000000ul, 0x4000000000000000ul)]
        [TestCase(0x4EE18820u, 0x3FFCCCCCCCCCCCCDul, 0x3FFCCCCCCCCCCCCDul, false, 0x4000000000000000ul, 0x4000000000000000ul)]
        [TestCase(0x4EA18820u, 0x3f99999a3fe66666ul, 0x3f99999a3fe66666ul, false, 0x4000000040000000ul, 0x4000000040000000ul)]    
        [TestCase(0xEA18820u,  0x3f99999a3fe66666ul, 0x3f99999a3fe66666ul, false, 0x4000000040000000ul, 0x0000000000000000ul)]    
        [TestCase(0xEA18820u,  0x0000000080000000ul, 0x0000000000000000ul, false, 0x0000000080000000ul, 0x0000000000000000ul)]
        [TestCase(0xEA18820u,  0x7F800000FF800000ul, 0x0000000000000000ul, false, 0x7F800000FF800000ul, 0x0000000000000000ul)]
        [TestCase(0xEA18820u,  0xFF8000017FC00002ul, 0x0000000000000000ul, false, 0xFFC000017FC00002ul, 0x0000000000000000ul)]
        [TestCase(0xEA18820u,  0xFF8000017FC00002ul, 0x0000000000000000ul, true,  0x7FC000007FC00000ul, 0x0000000000000000ul)]
        public void Frintp_V(uint Opcode, ulong A, ulong B, bool DefaultNaN, ulong Result0, ulong Result1)
        {
            int FpcrTemp = 0x0;
            if(DefaultNaN)
            {
                FpcrTemp = 0x2000000;
            }
            AVec V1 = new AVec { X0 = A, X1 = B };
            AThreadState ThreadState = SingleOpcode(Opcode, V1: V1, Fpcr: FpcrTemp);
            Assert.Multiple(() =>
            {
                Assert.AreEqual(Result0, ThreadState.V0.X0);
                Assert.AreEqual(Result1, ThreadState.V0.X1);
            });
        }

    	[TestCase(0x3FE66666u, 'N', false, 0x40000000u)]
    	[TestCase(0x3F99999Au, 'N', false, 0x3F800000u)]
    	[TestCase(0x404CCCCDu, 'P', false, 0x40800000u)]
    	[TestCase(0x40733333u, 'P', false, 0x40800000u)]
    	[TestCase(0x404CCCCDu, 'M', false, 0x40400000u)]
    	[TestCase(0x40733333u, 'M', false, 0x40400000u)]
    	[TestCase(0x3F99999Au, 'Z', false, 0x3F800000u)]
    	[TestCase(0x3FE66666u, 'Z', false, 0x3F800000u)]
    	[TestCase(0x00000000u, 'N', false, 0x00000000u)]
    	[TestCase(0x00000000u, 'P', false, 0x00000000u)]
    	[TestCase(0x00000000u, 'M', false, 0x00000000u)]
    	[TestCase(0x00000000u, 'Z', false, 0x00000000u)]
    	[TestCase(0x80000000u, 'N', false, 0x80000000u)]
    	[TestCase(0x80000000u, 'P', false, 0x80000000u)]
    	[TestCase(0x80000000u, 'M', false, 0x80000000u)]
    	[TestCase(0x80000000u, 'Z', false, 0x80000000u)]
    	[TestCase(0x7F800000u, 'N', false, 0x7F800000u)]
    	[TestCase(0x7F800000u, 'P', false, 0x7F800000u)]
    	[TestCase(0x7F800000u, 'M', false, 0x7F800000u)]
    	[TestCase(0x7F800000u, 'Z', false, 0x7F800000u)]
    	[TestCase(0xFF800000u, 'N', false, 0xFF800000u)]
    	[TestCase(0xFF800000u, 'P', false, 0xFF800000u)]
    	[TestCase(0xFF800000u, 'M', false, 0xFF800000u)]
    	[TestCase(0xFF800000u, 'Z', false, 0xFF800000u)]
    	[TestCase(0xFF800001u, 'N', false, 0xFFC00001u)]
    	[TestCase(0xFF800001u, 'P', false, 0xFFC00001u)]
    	[TestCase(0xFF800001u, 'M', false, 0xFFC00001u)]
    	[TestCase(0xFF800001u, 'Z', false, 0xFFC00001u)]
    	[TestCase(0xFF800001u, 'N', true,  0x7FC00000u)]
    	[TestCase(0xFF800001u, 'P', true,  0x7FC00000u)]
    	[TestCase(0xFF800001u, 'M', true,  0x7FC00000u)]
    	[TestCase(0xFF800001u, 'Z', true,  0x7FC00000u)]
    	[TestCase(0x7FC00002u, 'N', false, 0x7FC00002u)]
    	[TestCase(0x7FC00002u, 'P', false, 0x7FC00002u)]
    	[TestCase(0x7FC00002u, 'M', false, 0x7FC00002u)]
    	[TestCase(0x7FC00002u, 'Z', false, 0x7FC00002u)]
    	[TestCase(0x7FC00002u, 'N', true,  0x7FC00000u)]
    	[TestCase(0x7FC00002u, 'P', true,  0x7FC00000u)]
    	[TestCase(0x7FC00002u, 'M', true,  0x7FC00000u)]
    	[TestCase(0x7FC00002u, 'Z', true,  0x7FC00000u)]
    	public void Frintx_S(uint A, char RoundType, bool DefaultNaN, uint Result)
    	{
        	int FpcrTemp = 0x0;
        	switch(RoundType)
        	{
        		case 'N':
        		FpcrTemp = 0x0;
        		break;

        		case 'P':
        		FpcrTemp = 0x400000;
        		break;

        		case 'M':
        		FpcrTemp = 0x800000;
        		break;

        		case 'Z':
        		FpcrTemp = 0xC00000;
        		break;
        	}
        	if(DefaultNaN)
        	{
        		FpcrTemp |= 1 << 25;
        	}
        	AVec V1 = new AVec { X0 = A };
        	AThreadState ThreadState = SingleOpcode(0x1E274020, V1: V1, Fpcr: FpcrTemp);
        	Assert.AreEqual(Result, ThreadState.V0.X0);
        }

        [TestCase(0x6E619820u, 0x3FF3333333333333ul, 0x3FF3333333333333ul, 'N', false, 0x3FF0000000000000ul, 0x3FF0000000000000ul)]
        [TestCase(0x6E619820u, 0x3FFCCCCCCCCCCCCDul, 0x3FFCCCCCCCCCCCCDul, 'N', false, 0x4000000000000000ul, 0x4000000000000000ul)]
        [TestCase(0x6E619820u, 0x3FF3333333333333ul, 0x3FF3333333333333ul, 'P', false, 0x4000000000000000ul, 0x4000000000000000ul)]
        [TestCase(0x6E619820u, 0x3FFCCCCCCCCCCCCDul, 0x3FFCCCCCCCCCCCCDul, 'P', false, 0x4000000000000000ul, 0x4000000000000000ul)]
        [TestCase(0x6E619820u, 0x3FF3333333333333ul, 0x3FF3333333333333ul, 'M', false, 0x3FF0000000000000ul, 0x3FF0000000000000ul)]
        [TestCase(0x6E619820u, 0x3FFCCCCCCCCCCCCDul, 0x3FFCCCCCCCCCCCCDul, 'M', false, 0x3FF0000000000000ul, 0x3FF0000000000000ul)]
        [TestCase(0x6E619820u, 0x3FF3333333333333ul, 0x3FF3333333333333ul, 'Z', false, 0x3FF0000000000000ul, 0x3FF0000000000000ul)]
        [TestCase(0x6E619820u, 0x3FFCCCCCCCCCCCCDul, 0x3FFCCCCCCCCCCCCDul, 'Z', false, 0x3FF0000000000000ul, 0x3FF0000000000000ul)]
        [TestCase(0x6E219820u, 0x3f99999a3fe66666ul, 0x3f99999a3fe66666ul, 'N', false, 0x3f80000040000000ul, 0x3f80000040000000ul)]
        [TestCase(0x6E219820u, 0x3f99999a3fe66666ul, 0x3f99999a3fe66666ul, 'P', false, 0x4000000040000000ul, 0x4000000040000000ul)]    
        [TestCase(0x6E219820u, 0x3f99999a3fe66666ul, 0x3f99999a3fe66666ul, 'M', false, 0x3f8000003f800000ul, 0x3f8000003f800000ul)]
        [TestCase(0x6E219820u, 0x3f99999a3fe66666ul, 0x3f99999a3fe66666ul, 'Z', false, 0x3f8000003f800000ul, 0x3f8000003f800000ul)]
        [TestCase(0x2E219820u, 0x3f99999a3fe66666ul, 0x3f99999a3fe66666ul, 'N', false, 0x3f80000040000000ul, 0x0000000000000000ul)]
        [TestCase(0x2E219820u, 0x3f99999a3fe66666ul, 0x3f99999a3fe66666ul, 'P', false, 0x4000000040000000ul, 0x0000000000000000ul)]    
        [TestCase(0x2E219820u, 0x3f99999a3fe66666ul, 0x3f99999a3fe66666ul, 'M', false, 0x3f8000003f800000ul, 0x0000000000000000ul)]
        [TestCase(0x2E219820u, 0x3f99999a3fe66666ul, 0x3f99999a3fe66666ul, 'Z', false, 0x3f8000003f800000ul, 0x0000000000000000ul)]
        [TestCase(0x2E219820u, 0x0000000080000000ul, 0x0000000000000000ul, 'N', false, 0x0000000080000000ul, 0x0000000000000000ul)]
        [TestCase(0x2E219820u, 0x0000000080000000ul, 0x0000000000000000ul, 'P', false, 0x0000000080000000ul, 0x0000000000000000ul)]
        [TestCase(0x2E219820u, 0x0000000080000000ul, 0x0000000000000000ul, 'M', false, 0x0000000080000000ul, 0x0000000000000000ul)]
        [TestCase(0x2E219820u, 0x0000000080000000ul, 0x0000000000000000ul, 'Z', false, 0x0000000080000000ul, 0x0000000000000000ul)]
        [TestCase(0x2E219820u, 0x7F800000FF800000ul, 0x0000000000000000ul, 'N', false, 0x7F800000FF800000ul, 0x0000000000000000ul)]
        [TestCase(0x2E219820u, 0x7F800000FF800000ul, 0x0000000000000000ul, 'P', false, 0x7F800000FF800000ul, 0x0000000000000000ul)]
        [TestCase(0x2E219820u, 0x7F800000FF800000ul, 0x0000000000000000ul, 'M', false, 0x7F800000FF800000ul, 0x0000000000000000ul)]
        [TestCase(0x2E219820u, 0x7F800000FF800000ul, 0x0000000000000000ul, 'Z', false, 0x7F800000FF800000ul, 0x0000000000000000ul)]
        [TestCase(0x2E219820u, 0xFF8000017FC00002ul, 0x0000000000000000ul, 'N', false, 0xFFC000017FC00002ul, 0x0000000000000000ul)]
        [TestCase(0x2E219820u, 0xFF8000017FC00002ul, 0x0000000000000000ul, 'P', false, 0xFFC000017FC00002ul, 0x0000000000000000ul)]
        [TestCase(0x2E219820u, 0xFF8000017FC00002ul, 0x0000000000000000ul, 'M', false, 0xFFC000017FC00002ul, 0x0000000000000000ul)]
        [TestCase(0x2E219820u, 0xFF8000017FC00002ul, 0x0000000000000000ul, 'Z', false, 0xFFC000017FC00002ul, 0x0000000000000000ul)]
        [TestCase(0x2E219820u, 0xFF8000017FC00002ul, 0x0000000000000000ul, 'N', true,  0x7FC000007FC00000ul, 0x0000000000000000ul)]
        [TestCase(0x2E219820u, 0xFF8000017FC00002ul, 0x0000000000000000ul, 'P', true,  0x7FC000007FC00000ul, 0x0000000000000000ul)]
        [TestCase(0x2E219820u, 0xFF8000017FC00002ul, 0x0000000000000000ul, 'M', true,  0x7FC000007FC00000ul, 0x0000000000000000ul)]
        [TestCase(0x2E219820u, 0xFF8000017FC00002ul, 0x0000000000000000ul, 'Z', true,  0x7FC000007FC00000ul, 0x0000000000000000ul)]
        public void Frintx_V(uint Opcode, ulong A, ulong B, char RoundType, bool DefaultNaN, ulong Result0, ulong Result1)
        {
            int FpcrTemp = 0x0;
            switch(RoundType)
            {
                case 'N':
                FpcrTemp = 0x0;
                break;

                case 'P':
                FpcrTemp = 0x400000;
                break;

                case 'M':
                FpcrTemp = 0x800000;
                break;

                case 'Z':
                FpcrTemp = 0xC00000;
                break;
            }
            if(DefaultNaN)
            {
                FpcrTemp |= 1 << 25;
            }
            AVec V1 = new AVec { X0 = A, X1 = B };
            AThreadState ThreadState = SingleOpcode(Opcode, V1: V1, Fpcr: FpcrTemp);
            Assert.Multiple(() =>
            {
                Assert.AreEqual(Result0, ThreadState.V0.X0);
                Assert.AreEqual(Result1, ThreadState.V0.X1);
            });
        }
    }
}
