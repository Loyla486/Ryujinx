﻿using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace Ryujinx.Graphics.Gpu.Engine.GPFifo
{
    public class GPFifoDevice
    {
        private enum CommandBufferType
        {
            Prefetch,
            NoPrefetch
        }

        private struct CommandBuffer
        {
            /// <summary>
            /// The type of the command buffer.
            /// </summary>
            public CommandBufferType Type;

            /// <summary>
            /// Fetched data.
            /// </summary>
            public int[] Words;

            /// <summary>
            /// The GPFIFO entry address. (used in NoPrefetch mode)
            /// </summary>
            public ulong EntryAddress;

            /// <summary>
            /// The count of entries inside this GPFIFO entry.
            /// </summary>
            public uint EntryCount;

            /// <summary>
            /// Fetch the command buffer.
            /// </summary>
            public void Fetch(GpuContext context)
            {
                if (Words == null)
                {
                    Words = MemoryMarshal.Cast<byte, int>(context.MemoryAccessor.GetSpan(EntryAddress, (int)EntryCount * 4)).ToArray();
                }
            }
        }

        private readonly ConcurrentQueue<CommandBuffer> _commandBufferQueue;

        private CommandBuffer _currentCommandBuffer;

        private readonly bool _ibEnable;
        private readonly GpuContext _context;
        private readonly AutoResetEvent _event;

        internal GPFifoProcessor Processor { get; }

        /// <summary>
        /// Creates a new instance of the GPU DMA pusher.
        /// </summary>
        /// <param name="context">GPU context that the pusher belongs to</param>
        internal GPFifoDevice(GpuContext context)
        {
            _commandBufferQueue = new ConcurrentQueue<CommandBuffer>();
            _ibEnable = true;
            _context = context;
            _event = new AutoResetEvent(false);

            Processor = new GPFifoProcessor(context);
        }

        /// <summary>
        /// Signal the pusher that there are new entries to process.
        /// </summary>
        public void SignalNewEntries()
        {
            _event.Set();
        }

        /// <summary>
        /// Push a GPFIFO entry in the form of a prefetched command buffer.
        /// It is intended to be used by nvservices to handle special cases.
        /// </summary>
        /// <param name="commandBuffer">The command buffer containing the prefetched commands</param>
        public void PushHostCommandBuffer(int[] commandBuffer)
        {
            _commandBufferQueue.Enqueue(new CommandBuffer
            {
                Type = CommandBufferType.Prefetch,
                Words = commandBuffer,
                EntryAddress = ulong.MaxValue,
                EntryCount = (uint)commandBuffer.Length
            });
        }

        /// <summary>
        /// Create a CommandBuffer from a GPFIFO entry.
        /// </summary>
        /// <param name="entry">The GPFIFO entry</param>
        /// <returns>A new CommandBuffer based on the GPFIFO entry</returns>
        private CommandBuffer CreateCommandBuffer(GPEntry entry)
        {
            CommandBufferType type = CommandBufferType.Prefetch;

            if (entry.Entry1Sync == Entry1Sync.Wait)
            {
                type = CommandBufferType.NoPrefetch;
            }

            ulong startAddress = ((ulong)entry.Entry0Get << 2) | ((ulong)entry.Entry1GetHi << 32);

            return new CommandBuffer
            {
                Type = type,
                Words = null,
                EntryAddress = startAddress,
                EntryCount = (uint)entry.Entry1Length
            };
        }

        /// <summary>
        /// Pushes GPFIFO entries.
        /// </summary>
        /// <param name="entries">GPFIFO entries</param>
        public void PushEntries(ReadOnlySpan<ulong> entries)
        {
            bool beforeBarrier = true;

            for (int index = 0; index < entries.Length; index++)
            {
                ulong entry = entries[index];

                CommandBuffer commandBuffer = CreateCommandBuffer(Unsafe.As<ulong, GPEntry>(ref entry));

                if (beforeBarrier && commandBuffer.Type == CommandBufferType.Prefetch)
                {
                    commandBuffer.Fetch(_context);
                }

                if (commandBuffer.Type == CommandBufferType.NoPrefetch)
                {
                    beforeBarrier = false;
                }

                _commandBufferQueue.Enqueue(commandBuffer);
            }
        }

        /// <summary>
        /// Waits until commands are pushed to the FIFO.
        /// </summary>
        /// <returns>True if commands were received, false if wait timed out</returns>
        public bool WaitForCommands()
        {
            return _event.WaitOne(8);
        }

        /// <summary>
        /// Processes commands pushed to the FIFO.
        /// </summary>
        public void DispatchCalls()
        {
            while (_ibEnable && _commandBufferQueue.TryDequeue(out CommandBuffer entry))
            {
                _currentCommandBuffer = entry;
                _currentCommandBuffer.Fetch(_context);

                Processor.Process(_currentCommandBuffer.Words);
            }
        }
    }
}
