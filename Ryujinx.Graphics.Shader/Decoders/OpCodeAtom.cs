using Ryujinx.Graphics.Shader.Instructions;

namespace Ryujinx.Graphics.Shader.Decoders
{
    class OpCodeAtom : OpCode, IOpCodeRd, IOpCodeRa, IOpCodeReg
    {
        public Register Rd { get; }
        public Register Ra { get; }
        public Register Rb { get; }

        public ReductionType Type { get; }

        public int Offset { get; }

        public bool Extended { get; }

        public AtomicOp AtomicOp { get; }

        public OpCodeAtom(InstEmitter emitter, ulong address, long opCode) : base(emitter, address, opCode)
        {
            Rd = new Register(opCode.Extract(0,  8), RegisterType.Gpr);
            Ra = new Register(opCode.Extract(8,  8), RegisterType.Gpr);
            Rb = new Register(opCode.Extract(20, 8), RegisterType.Gpr);

            Type = (ReductionType)opCode.Extract(28, 2);

            if (Type == ReductionType.FP32FtzRn)
            {
                Type = ReductionType.S64;
            }

            Offset = opCode.Extract(30, 22);

            Extended = opCode.Extract(48);

            AtomicOp = (AtomicOp)opCode.Extract(52, 4);
        }
    }
}