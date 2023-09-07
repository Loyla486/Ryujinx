﻿using System;

namespace Ryujinx.Ava.UI.Models
{
    internal class StatusUpdatedEventArgs : EventArgs
    {
        public string SpeedState { get; }
        public string VolumeStatus { get; }
        public string GpuBackend { get; }
        public string AspectRatio { get; }
        public string DockedMode { get; }
        public string FifoStatus { get; }
        public string GameStatus { get; }
        public string GpuName { get; }

        public StatusUpdatedEventArgs(string speedState, string volumeStatus, string gpuBackend, string dockedMode, string aspectRatio, string gameStatus, string fifoStatus, string gpuName)
        {
            SpeedState = speedState;
            VolumeStatus = volumeStatus;
            GpuBackend = gpuBackend;
            DockedMode = dockedMode;
            AspectRatio = aspectRatio;
            GameStatus = gameStatus;
            FifoStatus = fifoStatus;
            GpuName = gpuName;
        }
    }
}
