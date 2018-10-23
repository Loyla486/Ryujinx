namespace Ryujinx.Graphics.Gal.Shader
{
    class ShaderIrCond : ShaderIrNode
    {
        public ShaderIrNode Pred  { get; set; }
        public ShaderIrNode Child { get; set; }

        public bool Not { get; private set; }

        public ShaderIrCond(ShaderIrNode pred, ShaderIrNode child, bool not)
        {
            this.Pred  = pred;
            this.Child = child;
            this.Not   = not;
        }
    }
}