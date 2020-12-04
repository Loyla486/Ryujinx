using Ryujinx.Audio;
using Ryujinx.Cpu;
using Ryujinx.HLE.HOS.Ipc;
using Ryujinx.HLE.HOS.Kernel;
using Ryujinx.HLE.HOS.Kernel.Common;
using Ryujinx.HLE.HOS.Kernel.Threading;
using System;
using System.Runtime.InteropServices;

namespace Ryujinx.HLE.HOS.Services.Audio.AudioOutManager
{
    class IAudioOut : IpcService, IDisposable
    {
        private readonly KernelContext _kernelContext;
        private readonly IAalOutput    _audioOut;
        private readonly KEvent        _releaseEvent;
        private          int           _releaseEventHandle;
        private readonly int           _track;
        private readonly int           _clientHandle;

        public IAudioOut(KernelContext kernelContext, IAalOutput audioOut, KEvent releaseEvent, int track, int clientHandle)
        {
            _kernelContext = kernelContext;
            _audioOut      = audioOut;
            _releaseEvent  = releaseEvent;
            _track         = track;
            _clientHandle  = clientHandle;
        }

        [Command(0)]
        // GetAudioOutState() -> u32 state
        public ResultCode GetAudioOutState(ServiceCtx context)
        {
            context.ResponseData.Write((int)_audioOut.GetState(_track));

            return ResultCode.Success;
        }

        [Command(1)]
        // StartAudioOut()
        public ResultCode StartAudioOut(ServiceCtx context)
        {
            _audioOut.Start(_track);

            return ResultCode.Success;
        }

        [Command(2)]
        // StopAudioOut()
        public ResultCode StopAudioOut(ServiceCtx context)
        {
            _audioOut.Stop(_track);

            return ResultCode.Success;
        }

        [Command(3)]
        // AppendAudioOutBuffer(u64 tag, buffer<nn::audio::AudioOutBuffer, 5>)
        public ResultCode AppendAudioOutBuffer(ServiceCtx context)
        {
            return AppendAudioOutBufferImpl(context, context.Request.SendBuff[0].Position);
        }

        [Command(4)]
        // RegisterBufferEvent() -> handle<copy>
        public ResultCode RegisterBufferEvent(ServiceCtx context)
        {
            if (_releaseEventHandle == 0)
            {
                if (context.Process.HandleTable.GenerateHandle(_releaseEvent.ReadableEvent, out _releaseEventHandle) != KernelResult.Success)
                {
                    throw new InvalidOperationException("Out of handles!");
                }
            }

            context.Response.HandleDesc = IpcHandleDesc.MakeCopy(_releaseEventHandle);

            return ResultCode.Success;
        }

        [Command(5)]
        // GetReleasedAudioOutBuffer() -> (u32 count, buffer<nn::audio::AudioOutBuffer, 6>)
        public ResultCode GetReleasedAudioOutBuffer(ServiceCtx context)
        {
            long position = context.Request.ReceiveBuff[0].Position;
            long size     = context.Request.ReceiveBuff[0].Size;

            return GetReleasedAudioOutBufferImpl(context, position, size);
        }

        [Command(6)]
        // ContainsAudioOutBuffer(u64 tag) -> b8
        public ResultCode ContainsAudioOutBuffer(ServiceCtx context)
        {
            long tag = context.RequestData.ReadInt64();

            context.ResponseData.Write(_audioOut.ContainsBuffer(_track, tag));

            return ResultCode.Success;
        }

        [Command(7)] // 3.0.0+
        // AppendAudioOutBufferAuto(u64 tag, buffer<nn::audio::AudioOutBuffer, 0x21>)
        public ResultCode AppendAudioOutBufferAuto(ServiceCtx context)
        {
            (long position, _) = context.Request.GetBufferType0x21();

            return AppendAudioOutBufferImpl(context, position);
        }

        public ResultCode AppendAudioOutBufferImpl(ServiceCtx context, long position)
        {
            long tag = context.RequestData.ReadInt64();

            AudioOutData data = MemoryHelper.Read<AudioOutData>(context.Memory, position);

            // NOTE: Assume PCM16 all the time, change if new format are found.
            short[] buffer = new short[data.SampleBufferSize / sizeof(short)];

            context.Process.HandleTable.GetKProcess(_clientHandle).CpuMemory.Read((ulong)data.SampleBufferPtr, MemoryMarshal.Cast<short, byte>(buffer));

            _audioOut.AppendBuffer(_track, tag, buffer);

            return ResultCode.Success;
        }

        [Command(8)] // 3.0.0+
        // GetReleasedAudioOutBufferAuto() -> (u32 count, buffer<nn::audio::AudioOutBuffer, 0x22>)
        public ResultCode GetReleasedAudioOutBufferAuto(ServiceCtx context)
        {
            (long position, long size) = context.Request.GetBufferType0x22();

            return GetReleasedAudioOutBufferImpl(context, position, size);
        }

        public ResultCode GetReleasedAudioOutBufferImpl(ServiceCtx context, long position, long size)
        {
            uint count = (uint)((ulong)size >> 3);

            long[] releasedBuffers = _audioOut.GetReleasedBuffers(_track, (int)count);

            for (uint index = 0; index < count; index++)
            {
                long tag = 0;

                if (index < releasedBuffers.Length)
                {
                    tag = releasedBuffers[index];
                }

                context.Memory.Write((ulong)(position + index * 8), tag);
            }

            context.ResponseData.Write(releasedBuffers.Length);

            return ResultCode.Success;
        }

        [Command(9)] // 4.0.0+
        // GetAudioOutBufferCount() -> u32
        public ResultCode GetAudioOutBufferCount(ServiceCtx context)
        {
            uint bufferCount = _audioOut.GetBufferCount(_track);

            context.ResponseData.Write(bufferCount);

            return ResultCode.Success;
        }

        [Command(10)] // 4.0.0+
        // GetAudioOutPlayedSampleCount() -> u64
        public ResultCode GetAudioOutPlayedSampleCount(ServiceCtx context)
        {
            ulong playedSampleCount = _audioOut.GetPlayedSampleCount(_track);

            context.ResponseData.Write(playedSampleCount);

            return ResultCode.Success;
        }

        [Command(11)] // 4.0.0+
        // FlushAudioOutBuffers() -> b8
        public ResultCode FlushAudioOutBuffers(ServiceCtx context)
        {
            bool heldBuffers = _audioOut.FlushBuffers(_track);

            context.ResponseData.Write(heldBuffers);

            return ResultCode.Success;
        }

        [Command(12)] // 6.0.0+
        // SetAudioOutVolume(s32)
        public ResultCode SetAudioOutVolume(ServiceCtx context)
        {
            float volume = context.RequestData.ReadSingle();

            _audioOut.SetVolume(_track, volume);

            return ResultCode.Success;
        }

        [Command(13)] // 6.0.0+
        // GetAudioOutVolume() -> s32
        public ResultCode GetAudioOutVolume(ServiceCtx context)
        {
            float volume = _audioOut.GetVolume(_track);

            context.ResponseData.Write(volume);

            return ResultCode.Success;
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _kernelContext.Syscall.CloseHandle(_clientHandle);
                _audioOut.CloseTrack(_track);
            }
        }
    }
}