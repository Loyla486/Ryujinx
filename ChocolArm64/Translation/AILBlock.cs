using System.Collections.Generic;

namespace ChocolArm64.Translation
{
    class AILBlock : IAILEmit
    {
        public long IntInputs  { get; private set; }
        public long IntOutputs { get; private set; }

        public long VecInputs  { get; private set; }
        public long VecOutputs { get; private set; }

        public bool HasStateStore { get; private set; }

        public List<IAILEmit> ILEmitters { get; private set; }

        public AILBlock Next   { get; set; }
        public AILBlock Branch { get; set; }

        public AILBlock()
        {
            ILEmitters = new List<IAILEmit>();
        }

        public void Add(IAILEmit ILEmitter)
        {
            if (ILEmitter is AILOpCodeLoad Ld && AILEmitter.IsRegIndex(Ld.Index))
            {
                switch (Ld.IoType)
                {
                    case AIoType.Flag:   IntInputs |= ((1L << Ld.Index) << 32) & ~IntOutputs; break;
                    case AIoType.Int:    IntInputs |=  (1L << Ld.Index)        & ~IntOutputs; break;
                    case AIoType.Vector: VecInputs |=  (1L << Ld.Index)        & ~VecOutputs; break;
                }
            }
            else if (ILEmitter is AILOpCodeStore St)
            {
                if (AILEmitter.IsRegIndex(St.Index))
                {
                    switch (St.IoType)
                    {
                        case AIoType.Flag:   IntOutputs |= (1L << St.Index) << 32; break;
                        case AIoType.Int:    IntOutputs |=  1L << St.Index;        break;
                        case AIoType.Vector: VecOutputs |=  1L << St.Index;        break;
                    }
                }

                if (St.IoType == AIoType.Fields)
                {
                    HasStateStore = true;
                }
            }

            ILEmitters.Add(ILEmitter);
        }

        public void Emit(AILEmitter Context)
        {
            foreach (IAILEmit ILEmitter in ILEmitters)
            {
                ILEmitter.Emit(Context);
            }
        }
    }
}