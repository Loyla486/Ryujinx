﻿using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Ryujinx.Memory.Tracking
{
    public class BitmapMultiRegionHandle : IMultiRegionHandle
    {
        /// <summary>
        /// A list of region handles for each granularity sized chunk of the whole region.
        /// </summary>
        private readonly BitmapRegionHandle[] _handles;
        private readonly ulong Address;
        private readonly ulong Granularity;
        private readonly ulong Size;

        private MultithreadedBitmap _dirtyBitmap;

        private int _sequenceNumber;
        private BitMap _sequenceNumberBitmap;
        private bool _sequenceNumberSet;

        public bool Dirty { get; private set; } = true;

        internal BitmapMultiRegionHandle(MemoryTracking tracking, ulong address, ulong size, IEnumerable<IRegionHandle> handles, ulong granularity)
        {
            _handles = new BitmapRegionHandle[size / granularity];
            Granularity = granularity;

            _dirtyBitmap = new MultithreadedBitmap(_handles.Length, true);
            _sequenceNumberBitmap = new BitMap(_handles.Length);

            int i = 0;

            if (handles != null)
            {
                // Inherit from the handles we were given. Any gaps must be filled with new handles,
                // and old handles larger than our granularity must copy their state onto new granular handles and dispose.
                // It is assumed that the provided handles do not overlap, in order, are on page boundaries,
                // and don't extend past the requested range.

                foreach (RegionHandleBase handle in handles)
                {
                    int startIndex = (int)((handle.Address - address) / granularity);

                    // Fill any gap left before this handle.
                    while (i < startIndex)
                    {
                        BitmapRegionHandle fillHandle = tracking.BeginTrackingBitmap(address + (ulong)i * granularity, granularity, _dirtyBitmap, i);
                        fillHandle.Parent = this;
                        _handles[i++] = fillHandle;
                    }

                    lock (tracking.TrackingLock)
                    {
                        if (handle is BitmapRegionHandle bitHandle && handle.Size == granularity)
                        {
                            handle.Parent = this;

                            bitHandle.ReplaceBitmap(_dirtyBitmap, i);

                            _handles[i++] = bitHandle;
                        }
                        else
                        {
                            int endIndex = (int)((handle.EndAddress - address) / granularity);

                            while (i < endIndex)
                            {
                                BitmapRegionHandle splitHandle = tracking.BeginTrackingBitmap(address + (ulong)i * granularity, granularity, _dirtyBitmap, i);
                                splitHandle.Parent = this;

                                splitHandle.Reprotect(handle.Dirty);

                                RegionSignal signal = handle.PreAction;
                                if (signal != null)
                                {
                                    splitHandle.RegisterAction(signal);
                                }

                                _handles[i++] = splitHandle;
                            }

                            handle.Dispose();
                        }
                    }
                }
            }

            // Fill any remaining space with new handles.
            while (i < _handles.Length)
            {
                BitmapRegionHandle handle = tracking.BeginTrackingBitmap(address + (ulong)i * granularity, granularity, _dirtyBitmap, i);
                handle.Parent = this;
                _handles[i++] = handle;
            }

            Address = address;
            Size = size;
        }

        public void SignalWrite()
        {
            Dirty = true;
        }

        public IEnumerable<BitmapRegionHandle> GetHandles()
        {
            return _handles;
        }

        public void ForceDirty(ulong address, ulong size)
        {
            Dirty = true;

            int startHandle = (int)((address - Address) / Granularity);
            int lastHandle = (int)((address + (size - 1) - Address) / Granularity);

            // TODO: speed up

            for (int i = startHandle; i <= lastHandle; i++)
            {
                _sequenceNumberBitmap.Clear(i);
                _handles[i].ForceDirty();
            }
        }

        public void QueryModified(Action<ulong, ulong> modifiedAction)
        {
            if (!Dirty)
            {
                return;
            }

            Dirty = false;

            QueryModified(Address, Size, modifiedAction);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ParseDirtyBits(long dirtyBits, ref int baseBit, ref int prevHandle, ref ulong rgStart, ref ulong rgSize, Action<ulong, ulong> modifiedAction)
        {
            while (dirtyBits != 0)
            {
                int bit = BitOperations.TrailingZeroCount(dirtyBits);

                dirtyBits &= ~(1L << bit);

                int handleIndex = baseBit + bit;

                BitmapRegionHandle handle = _handles[handleIndex];

                if (handleIndex != prevHandle + 1)
                {
                    // Submit handles scanned until the gap as dirty
                    if (rgSize != 0)
                    {
                        modifiedAction(rgStart, rgSize);
                        rgSize = 0;
                    }
                    rgStart = handle.Address;
                }

                if (handle.Dirty)
                {
                    rgSize += handle.Size;
                    handle.Reprotect();
                }

                prevHandle = handleIndex;
            }

            baseBit += MultithreadedBitmap.IntSize;
        }

        public void QueryModified(ulong address, ulong size, Action<ulong, ulong> modifiedAction)
        {
            int startHandle = (int)((address - Address) / Granularity);
            int lastHandle = (int)((address + (size - 1) - Address) / Granularity);

            ulong rgStart = _handles[startHandle].Address;

            if (startHandle == lastHandle)
            {
                BitmapRegionHandle handle = _handles[startHandle];

                if (handle.Dirty)
                {
                    handle.Reprotect();
                    modifiedAction(rgStart, handle.Size);
                }

                return;
            }

            ulong rgSize = 0;

            long[] masks = _dirtyBitmap.Masks;

            int startIndex = startHandle >> MultithreadedBitmap.IntShift;
            int startBit = startHandle & MultithreadedBitmap.IntMask;
            long startMask = -1L << startBit;

            int endIndex = lastHandle >> MultithreadedBitmap.IntShift;
            int endBit = lastHandle & MultithreadedBitmap.IntMask;
            long endMask = (long)(ulong.MaxValue >> (MultithreadedBitmap.IntMask - endBit));

            long startValue = Volatile.Read(ref masks[startIndex]);

            int baseBit = startIndex << MultithreadedBitmap.IntShift;
            int prevHandle = startHandle - 1;

            if (startIndex == endIndex)
            {
                ParseDirtyBits(startValue & startMask & endMask, ref baseBit, ref prevHandle, ref rgStart, ref rgSize, modifiedAction);
            }
            else
            {
                ParseDirtyBits(startValue & startMask, ref baseBit, ref prevHandle, ref rgStart, ref rgSize, modifiedAction);

                for (int i = startIndex + 1; i < endIndex; i++)
                {
                    ParseDirtyBits(Volatile.Read(ref masks[i]), ref baseBit, ref prevHandle, ref rgStart, ref rgSize, modifiedAction);
                }

                long endValue = Volatile.Read(ref masks[endIndex]);

                ParseDirtyBits(endValue & endMask, ref baseBit, ref prevHandle, ref rgStart, ref rgSize, modifiedAction);
            }

            if (rgSize != 0)
            {
                modifiedAction(rgStart, rgSize);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ParseDirtyBits(long dirtyBits, long mask, int index, long[] seqMasks, ref int baseBit, ref int prevHandle, ref ulong rgStart, ref ulong rgSize, Action<ulong, ulong> modifiedAction)
        {
            dirtyBits &= mask & ~seqMasks[index];

            while (dirtyBits != 0)
            {
                int bit = BitOperations.TrailingZeroCount(dirtyBits);

                dirtyBits &= ~(1L << bit);

                int handleIndex = baseBit + bit;

                BitmapRegionHandle handle = _handles[handleIndex];

                if (handleIndex != prevHandle + 1)
                {
                    // Submit handles scanned until the gap as dirty
                    if (rgSize != 0)
                    {
                        modifiedAction(rgStart, rgSize);
                        rgSize = 0;
                    }
                    rgStart = handle.Address;
                }

                rgSize += handle.Size;
                handle.Reprotect();

                prevHandle = handleIndex;
            }

            seqMasks[index] |= mask;
            _sequenceNumberSet = true;

            baseBit += MultithreadedBitmap.IntSize;
        }

        public void QueryModified(ulong address, ulong size, Action<ulong, ulong> modifiedAction, int sequenceNumber)
        {
            int startHandle = (int)((address - Address) / Granularity);
            int lastHandle = (int)((address + (size - 1) - Address) / Granularity);

            ulong rgStart = Address + (ulong)startHandle * Granularity;

            if (sequenceNumber != _sequenceNumber)
            {
                if (_sequenceNumberSet)
                {
                    _sequenceNumberBitmap.Clear();

                    _sequenceNumberSet = false;
                }

                _sequenceNumber = sequenceNumber;
            }

            if (startHandle == lastHandle)
            {
                var handle = _handles[startHandle];
                if (!_sequenceNumberBitmap.IsSet(startHandle))
                {
                    if (handle.DirtyOrVolatile())
                    {
                        _sequenceNumberBitmap.Set(startHandle);
                        _sequenceNumberSet = true;
                        handle.Reprotect();

                        modifiedAction(rgStart, handle.Size);
                    }
                }

                return;
            }

            ulong rgSize = 0;

            long[] seqMasks = _sequenceNumberBitmap.Masks;
            long[] masks = _dirtyBitmap.Masks;

            int startIndex = startHandle >> MultithreadedBitmap.IntShift;
            int startBit = startHandle & MultithreadedBitmap.IntMask;
            long startMask = -1L << startBit;

            int endIndex = lastHandle >> MultithreadedBitmap.IntShift;
            int endBit = lastHandle & MultithreadedBitmap.IntMask;
            long endMask = (long)(ulong.MaxValue >> (MultithreadedBitmap.IntMask - endBit));

            long startValue = Volatile.Read(ref masks[startIndex]);

            int baseBit = startIndex << MultithreadedBitmap.IntShift;
            int prevHandle = startHandle - 1;

            if (startIndex == endIndex)
            {
                ParseDirtyBits(startValue, startMask & endMask, startIndex, seqMasks, ref baseBit, ref prevHandle, ref rgStart, ref rgSize, modifiedAction);
            }
            else
            {
                ParseDirtyBits(startValue, startMask, startIndex, seqMasks, ref baseBit, ref prevHandle, ref rgStart, ref rgSize, modifiedAction);

                for (int i = startIndex + 1; i < endIndex; i++)
                {
                    ParseDirtyBits(Volatile.Read(ref masks[i]), -1L, i, seqMasks, ref baseBit, ref prevHandle, ref rgStart, ref rgSize, modifiedAction);
                }

                long endValue = Volatile.Read(ref masks[endIndex]);

                ParseDirtyBits(endValue, endMask, endIndex, seqMasks, ref baseBit, ref prevHandle, ref rgStart, ref rgSize, modifiedAction);
            }

            if (rgSize != 0)
            {
                modifiedAction(rgStart, rgSize);
            }
        }

        public void RegisterAction(ulong address, ulong size, RegionSignal action)
        {
            int startHandle = (int)((address - Address) / Granularity);
            int lastHandle = (int)((address + (size - 1) - Address) / Granularity);

            for (int i = startHandle; i <= lastHandle; i++)
            {
                _handles[i].RegisterAction(action);
            }
        }

        public void RegisterPreciseAction(ulong address, ulong size, PreciseRegionSignal action)
        {
            int startHandle = (int)((address - Address) / Granularity);
            int lastHandle = (int)((address + (size - 1) - Address) / Granularity);

            for (int i = startHandle; i <= lastHandle; i++)
            {
                _handles[i].RegisterPreciseAction(action);
            }
        }

        public void Dispose()
        {
            foreach (var handle in _handles)
            {
                handle.Dispose();
            }
        }
    }
}
