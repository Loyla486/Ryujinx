using Ryujinx.Common.Logging;
using Ryujinx.HLE.HOS.Tamper.Atmosphere.Operations;

namespace Ryujinx.HLE.HOS.Tamper.Atmosphere
{
    class Register : IOperand
    {
        private ulong _register = 0;
        private string _alias;

        public Register(string alias)
        {
            _alias = alias;
        }

        public T Get<T>() where T : unmanaged
        {
            return (T)(dynamic)_register;
        }

        public void Set<T>(T value) where T : unmanaged
        {
            Logger.Debug?.Print(LogClass.TamperMachine, $"{_alias}: {value}");

            _register = (ulong)(dynamic)value;
        }
    }
}
