﻿using FFmpeg.AutoGen;
using Ryujinx.Common.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Ryujinx.Graphics.Nvdec.FFmpeg
{
    unsafe class FFmpegContext : IDisposable
    {
        private readonly AVCodec_decode _decodeFrame;
        private static readonly av_log_set_callback_callback _logFunc;
        private readonly AVCodec* _codec;
        private AVPacket* _packet;
        private AVCodecContext* _context;

        public FFmpegContext(AVCodecID codecId)
        {
            _codec = ffmpeg.avcodec_find_decoder(codecId);
            if (_codec == null)
            {
                Logger.Error?.PrintMsg(LogClass.FFmpeg, $"Codec wasn't found. Make sure you have the {codecId} codec present in your FFmpeg installation.");

                return;
            }

            _context = ffmpeg.avcodec_alloc_context3(_codec);
            if (_context == null)
            {
                Logger.Error?.PrintMsg(LogClass.FFmpeg, "Codec context couldn't be allocated.");

                return;
            }

            if (ffmpeg.avcodec_open2(_context, _codec, null) != 0)
            {
                Logger.Error?.PrintMsg(LogClass.FFmpeg, "Codec couldn't be opened.");

                return;
            }

            _packet = ffmpeg.av_packet_alloc();
            if (_packet == null)
            {
                Logger.Error?.PrintMsg(LogClass.FFmpeg, "Packet couldn't be allocated.");

                return;
            }

            _decodeFrame = Marshal.GetDelegateForFunctionPointer<AVCodec_decode>(_codec->decode.Pointer);
        }

        static FFmpegContext()
        {
            SetRootPath();

            _logFunc = Log;

            // Redirect log output.
            ffmpeg.av_log_set_level(ffmpeg.AV_LOG_MAX_OFFSET);
            ffmpeg.av_log_set_callback(_logFunc);
        }

        private static void SetRootPath()
        {
            if (OperatingSystem.IsLinux())
            {
                // Configure FFmpeg search path
                Process lddProcess = Process.Start(new ProcessStartInfo
                {
                    FileName               = "/bin/sh",
                    Arguments              = "-c \"ldd $(which ffmpeg 2>/dev/null) | grep libavfilter\" 2>/dev/null",
                    UseShellExecute        = false,
                    RedirectStandardOutput = true
                });

                string lddOutput = lddProcess.StandardOutput.ReadToEnd();

                lddProcess.WaitForExit();
                lddProcess.Close();

                if (lddOutput.Contains(" => "))
                {
                    ffmpeg.RootPath = Path.GetDirectoryName(lddOutput.Split(" => ")[1]);
                }
                else
                {
                    Logger.Error?.PrintMsg(LogClass.FFmpeg, "FFmpeg wasn't found. Make sure that you have it installed and up to date.");
                }
            }
        }

        private static void Log(void* p0, int level, string format, byte* vl)
        {
            if (level > ffmpeg.av_log_get_level())
            {
                return;
            }

            int lineSize = 1024;
            byte* lineBuffer = stackalloc byte[lineSize];
            int printPrefix = 1;

            ffmpeg.av_log_format_line(p0, level, format, vl, lineBuffer, lineSize, &printPrefix);

            string line = Marshal.PtrToStringAnsi((IntPtr)lineBuffer).Trim();

            switch (level)
            {
                case ffmpeg.AV_LOG_PANIC:
                case ffmpeg.AV_LOG_FATAL:
                case ffmpeg.AV_LOG_ERROR:
                    Logger.Error?.Print(LogClass.FFmpeg, line);
                    break;
                case ffmpeg.AV_LOG_WARNING:
                    Logger.Warning?.Print(LogClass.FFmpeg, line);
                    break;
                case ffmpeg.AV_LOG_INFO:
                    Logger.Info?.Print(LogClass.FFmpeg, line);
                    break;
                case ffmpeg.AV_LOG_VERBOSE:
                case ffmpeg.AV_LOG_DEBUG:
                case ffmpeg.AV_LOG_TRACE:
                    Logger.Debug?.Print(LogClass.FFmpeg, line);
                    break;
            }
        }

        public int DecodeFrame(Surface output, ReadOnlySpan<byte> bitstream)
        {
            ffmpeg.av_frame_unref(output.Frame);

            int result;
            int gotFrame;

            fixed (byte* ptr = bitstream)
            {
                _packet->data = ptr;
                _packet->size = bitstream.Length;
                result = _decodeFrame(_context, output.Frame, &gotFrame, _packet);
            }

            if (gotFrame == 0)
            {
                ffmpeg.av_frame_unref(output.Frame);

                // If the frame was not delivered, it was probably delayed.
                // Get the next delayed frame by passing a 0 length packet.
                _packet->data = null;
                _packet->size = 0;
                result = _decodeFrame(_context, output.Frame, &gotFrame, _packet);

                // We need to set B frames to 0 as we already consumed all delayed frames.
                // This prevents the decoder from trying to return a delayed frame next time.
                _context->has_b_frames = 0;
            }

            ffmpeg.av_packet_unref(_packet);

            if (gotFrame == 0)
            {
                ffmpeg.av_frame_unref(output.Frame);

                return -1;
            }

            return result < 0 ? result : 0;
        }

        public void Dispose()
        {
            fixed (AVPacket** ppPacket = &_packet)
            {
                ffmpeg.av_packet_free(ppPacket);
            }

            ffmpeg.avcodec_close(_context);

            fixed (AVCodecContext** ppContext = &_context)
            {
                ffmpeg.avcodec_free_context(ppContext);
            }
        }
    }
}
