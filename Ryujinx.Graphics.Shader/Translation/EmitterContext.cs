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

        private ShaderConfig _config;

        public ShaderConfig Config => _config;

        private List<Operation> _operations;

        private Dictionary<ulong, Operand> _labels;

        public EmitterContext(ShaderConfig config)
        {
            _config = config;

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
            if (_config.Stage == ShaderStage.Fragment)
            {
                if (_config.OmapDepth)
                {
                    Operand dest = Attribute(AttributeConsts.FragmentOutputDepth);

                    Operand src = Register(_config.GetDepthRegister(), RegisterType.Gpr);

                    this.Copy(dest, src);
                }

                int regIndex = 0;

                for (int attachment = 0; attachment < 8; attachment++)
                {
                    OmapTarget target = _config.OmapTargets[attachment];

                    for (int component = 0; component < 4; component++)
                    {
                        if (target.ComponentEnabled(component))
                        {
                            Operand dest = Attribute(AttributeConsts.FragmentOutputColorBase + attachment * 16 + component * 4);

                            Operand src = Register(regIndex, RegisterType.Gpr);

                            this.Copy(dest, src);

                            regIndex++;
                        }
                    }
                }
            }
        }

        public Operation[] GetOperations()
        {
            return _operations.ToArray();
        }
    }
}