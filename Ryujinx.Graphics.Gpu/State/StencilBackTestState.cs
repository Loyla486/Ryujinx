using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.GAL.DepthStencil;

namespace Ryujinx.Graphics.Gpu.State
{
    struct StencilBackTestState
    {
        public Boolean32 TwoSided;
        public StencilOp BackSFail;
        public StencilOp BackDpFail;
        public StencilOp BackDpPass;
        public CompareOp BackFunc;
    }
}
