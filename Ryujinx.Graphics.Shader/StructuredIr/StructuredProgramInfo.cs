using System.Collections.Generic;

namespace Ryujinx.Graphics.Shader.StructuredIr
{
    class StructuredProgramInfo
    {
        public AstBlock MainBlock { get; }

        public HashSet<AstOperand> Locals { get; }

        public HashSet<int> CBuffers { get; }
        public HashSet<int> SBuffers { get; }

        public HashSet<int> IAttributes { get; }
        public HashSet<int> OAttributes { get; }

        public bool UsesInstanceId { get; set; }

        public HelperFunctionsMask HelperFunctionsMask { get; set; }

        public HashSet<AstTextureOperation> Samplers { get; }
        public HashSet<AstTextureOperation> Images   { get; }

        public StructuredProgramInfo(AstBlock mainBlock)
        {
            MainBlock = mainBlock;

            Locals = new HashSet<AstOperand>();

            CBuffers = new HashSet<int>();
            SBuffers = new HashSet<int>();

            IAttributes = new HashSet<int>();
            OAttributes = new HashSet<int>();

            Samplers = new HashSet<AstTextureOperation>();
            Images   = new HashSet<AstTextureOperation>();
        }
    }
}