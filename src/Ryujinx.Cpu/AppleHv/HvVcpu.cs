using System.Diagnostics;

namespace Ryujinx.Cpu.AppleHv
{
    unsafe class HvVcpu
    {
        private const ulong InterruptIntervalNs = 16 * 1000; // 16 ms

        private static ulong _interruptTimeDeltaTicks = 0;

        public readonly ulong Handle;
        public readonly HvVcpuExit* ExitInfo;
        public readonly IHvExecutionContext ShadowContext;
        public readonly IHvExecutionContext NativeContext;
        public readonly bool IsEphemeral;

        public HvVcpu(
            ulong handle,
            HvVcpuExit* exitInfo,
            IHvExecutionContext shadowContext,
            IHvExecutionContext nativeContext,
            bool isEphemeral)
        {
            Handle = handle;
            ExitInfo = exitInfo;
            ShadowContext = shadowContext;
            NativeContext = nativeContext;
            IsEphemeral = isEphemeral;
        }

        public void EnableAndUpdateVTimer()
        {
            // We need to ensure interrupts will be serviced,
            // and for that we set up the VTime to trigger an interrupt at fixed intervals.

            ulong deltaTicks = _interruptTimeDeltaTicks;

            if (deltaTicks == 0)
            {
                // Calculate our time delta in ticks based on the current clock frequency.

                int result = TimeApi.mach_timebase_info(out var timeBaseInfo);

                Debug.Assert(result == 0);

                deltaTicks = ((InterruptIntervalNs * timeBaseInfo.numer) + (timeBaseInfo.denom - 1)) / timeBaseInfo.denom;
                _interruptTimeDeltaTicks = deltaTicks;
            }

            HvApi.hv_vcpu_set_sys_reg(Handle, HvSysReg.CNTV_CTL_EL0, 1).ThrowOnError();
            HvApi.hv_vcpu_set_sys_reg(Handle, HvSysReg.CNTV_CVAL_EL0, TimeApi.mach_absolute_time() + deltaTicks).ThrowOnError();
        }
    }
}
