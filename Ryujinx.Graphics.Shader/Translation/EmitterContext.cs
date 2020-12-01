using Ryujinx.Common;
using Ryujinx.Graphics.Shader.Decoders;
using Ryujinx.Graphics.Shader.IntermediateRepresentation;
using System.Collections.Generic;

using static Ryujinx.Graphics.Shader.IntermediateRepresentation.OperandHelper;

namespace Ryujinx.Graphics.Shader.Translation
{
    class EmitterContext
    {
        public Block  CurrBlock { get; set; }
        public OpCode CurrOp    { get; set; }

        public ShaderConfig Config { get; }

        private List<Operation> _operations;

        private Dictionary<ulong, Operand> _labels;

        public EmitterContext(ShaderConfig config)
        {
            Config = config;

            _operations = new List<Operation>();

            _labels = new Dictionary<ulong, Operand>();
        }

        public Operand Add(Instruction inst, Operand dest = null, params Operand[] sources)
        {
            Operation operation = new Operation(inst, dest, sources);

            Add(operation);

            return dest;
        }

        public void Add(Operation operation)
        {
            _operations.Add(operation);
        }

        public void FlagAttributeRead(int attribute)
        {
            if (Config.Stage == ShaderStage.Fragment)
            {
                switch (attribute)
                {
                    case AttributeConsts.PositionX:
                    case AttributeConsts.PositionY:
                        Config.SetUsedFeature(FeatureFlags.FragCoordXY);
                        break;
                }
            }
        }

        public void MarkLabel(Operand label)
        {
            Add(Instruction.MarkLabel, label);
        }

        public Operand GetLabel(ulong address)
        {
            if (!_labels.TryGetValue(address, out Operand label))
            {
                label = Label();

                _labels.Add(address, label);
            }

            return label;
        }

        public void PrepareForReturn()
        {
            if (Config.Stage == ShaderStage.Fragment)
            {
                if (Config.OmapDepth)
                {
                    Operand dest = Attribute(AttributeConsts.FragmentOutputDepth);

                    Operand src = Register(Config.GetDepthRegister(), RegisterType.Gpr);

                    this.Copy(dest, src);
                }

                int regIndex = 0;

                for (int rtIndex = 0; rtIndex < 8; rtIndex++)
                {
                    OmapTarget target = Config.OmapTargets[rtIndex];

                    for (int component = 0; component < 4; component++)
                    {
                        if (!target.ComponentEnabled(component))
                        {
                            continue;
                        }

                        int fragmentOutputColorAttr = AttributeConsts.FragmentOutputColorBase + rtIndex * 16;

                        Operand src = Register(regIndex, RegisterType.Gpr);

                        // Perform B <-> R swap if needed, for BGRA formats (not supported on OpenGL).
                        if (component == 0 || component == 2)
                        {
                            Operand isBgra = Attribute(AttributeConsts.FragmentOutputIsBgraBase + rtIndex * 4);

                            Operand lblIsBgra = Label();
                            Operand lblEnd    = Label();

                            this.BranchIfTrue(lblIsBgra, isBgra);

                            this.Copy(Attribute(fragmentOutputColorAttr + component * 4), src);
                            this.Branch(lblEnd);

                            MarkLabel(lblIsBgra);

                            this.Copy(Attribute(fragmentOutputColorAttr + (2 - component) * 4), src);

                            MarkLabel(lblEnd);
                        }
                        else
                        {
                            this.Copy(Attribute(fragmentOutputColorAttr + component * 4), src);
                        }

                        regIndex++;
                    }

                    regIndex = BitUtils.AlignUp(regIndex, 4);
                }
            }
        }

        public Operation[] GetOperations()
        {
            return _operations.ToArray();
        }
    }
}