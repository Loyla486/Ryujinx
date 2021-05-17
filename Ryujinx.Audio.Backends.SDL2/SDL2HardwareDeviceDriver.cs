﻿using Ryujinx.Audio.Backends.Common;
using Ryujinx.Audio.Common;
using Ryujinx.Audio.Integration;
using Ryujinx.Memory;
using Ryujinx.SDL2.Common;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

using static Ryujinx.Audio.Integration.IHardwareDeviceDriver;
using static SDL2.SDL;

namespace Ryujinx.Audio.Backends.SDL2
{
    public class SDL2HardwareDeviceDriver : IHardwareDeviceDriver
    {
        private object _lock = new object();

        private ManualResetEvent _updateRequiredEvent;
        private List<SDL2HardwareDeviceSession> _sessions;

        public SDL2HardwareDeviceDriver()
        {
            _updateRequiredEvent = new ManualResetEvent(false);
            _sessions = new List<SDL2HardwareDeviceSession>();

            SDL2Driver.Instance.Initialize();
        }

        public static bool IsSupported => IsSupportedInternal();

        private static bool IsSupportedInternal()
        {
            uint device = OpenStream(SampleFormat.PcmInt16, Constants.TargetSampleRate, Constants.ChannelCountMax, Constants.TargetSampleCount, null);

            if (device != 0)
            {
                SDL_CloseAudioDevice(device);
            }

            return device != 0;
        }

        public ManualResetEvent GetUpdateRequiredEvent()
        {
            return _updateRequiredEvent;
        }

        public IHardwareDeviceSession OpenDeviceSession(Direction direction, IVirtualMemoryManager memoryManager, SampleFormat sampleFormat, uint sampleRate, uint channelCount)
        {
            if (channelCount == 0)
            {
                channelCount = 2;
            }

            if (sampleRate == 0)
            {
                sampleRate = Constants.TargetSampleRate;
            }

            if (direction != Direction.Output)
            {
                throw new NotImplementedException("Input direction is currently not implemented on SDL2 backend!");
            }

            lock (_lock)
            {
                SDL2HardwareDeviceSession session = new SDL2HardwareDeviceSession(this, memoryManager, sampleFormat, sampleRate, channelCount);

                _sessions.Add(session);

                return session;
            }
        }

        internal void Unregister(SDL2HardwareDeviceSession session)
        {
            lock (_lock)
            {
                _sessions.Remove(session);
            }
        }

        private static SDL_AudioSpec GetSDL2Spec(SampleFormat requestedSampleFormat, uint requestedSampleRate, uint requestedChannelCount, uint sampleCount)
        {
            return new SDL_AudioSpec
            {
                channels = (byte)requestedChannelCount,
                format = GetSDL2Format(requestedSampleFormat),
                freq = (int)requestedSampleRate,
                samples = (ushort)sampleCount
            };
        }

        internal static ushort GetSDL2Format(SampleFormat format)
        {
            return format switch
            {
                SampleFormat.PcmInt8 => AUDIO_S8,
                SampleFormat.PcmInt16 => AUDIO_S16,
                SampleFormat.PcmInt32 => AUDIO_S32,
                SampleFormat.PcmFloat => AUDIO_F32,
                _ => throw new ArgumentException($"Unsupported sample format {format}"),
            };
        }

        // TODO: Fix this in SDL2-CS.
        [DllImport("SDL2", EntryPoint = "SDL_OpenAudioDevice", CallingConvention = CallingConvention.Cdecl)]
        private static extern uint SDL_OpenAudioDevice_Workaround(
            IntPtr name,
            int iscapture,
            ref SDL_AudioSpec desired,
            out SDL_AudioSpec obtained,
            uint allowed_changes
        );

        internal static uint OpenStream(SampleFormat requestedSampleFormat, uint requestedSampleRate, uint requestedChannelCount, uint sampleCount, SDL_AudioCallback callback)
        {
            SDL_AudioSpec desired = GetSDL2Spec(requestedSampleFormat, requestedSampleRate, requestedChannelCount, sampleCount);

            desired.callback = callback;

            uint device = SDL_OpenAudioDevice_Workaround(IntPtr.Zero, 0, ref desired, out SDL_AudioSpec got, 0);

            if (device == 0)
            {
                return 0;
            }

            bool isValid = got.format == desired.format && got.freq == desired.freq && got.channels == desired.channels;

            if (!isValid)
            {
                SDL_CloseAudioDevice(device);

                return 0;
            }

            return device;
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                while (_sessions.Count > 0)
                {
                    SDL2HardwareDeviceSession session = _sessions[_sessions.Count - 1];

                    session.Dispose();
                }

                SDL2Driver.Instance.Dispose();
            }
        }

        public bool SupportsSampleRate(uint sampleRate)
        {
            return true;
        }

        public bool SupportsSampleFormat(SampleFormat sampleFormat)
        {
            return sampleFormat != SampleFormat.PcmInt24;
        }

        public bool SupportsChannelCount(uint channelCount)
        {
            return true;
        }

        public bool SupportsDirection(Direction direction)
        {
            // TODO: add direction input when supported.
            return direction == Direction.Output;
        }
    }
}
