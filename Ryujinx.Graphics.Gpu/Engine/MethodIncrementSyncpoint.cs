﻿using Ryujinx.Graphics.Gpu.State;

namespace Ryujinx.Graphics.Gpu.Engine
{
    partial class Methods
    {
        /// <summary>
        /// Performs an incrementation on a syncpoint.
        /// </summary>
        /// <param name="state">Current GPU state</param>
        /// <param name="argument">Method call argument</param>
        public void IncrementSyncpoint(GpuState state, int argument)
        {
            uint syncpointId = (uint)(argument) & 0xFFFF;

            _context.Synchronization.IncrementSyncpoint(syncpointId);
        }
    }
}
