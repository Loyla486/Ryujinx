using ARMeilleure.Common;
using ARMeilleure.Memory;
using ARMeilleure.Signal;
using Ryujinx.Cpu.LightningJit.Cache;
using Ryujinx.Cpu.LightningJit.State;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace Ryujinx.Cpu.LightningJit
{
    class Translator : IDisposable
    {
        private static readonly AddressTable<ulong>.Level[] _levels64Bit =
            new AddressTable<ulong>.Level[]
            {
                new(31, 17),
                new(23,  8),
                new(15,  8),
                new( 7,  8),
                new( 2,  5),
            };

        private static readonly AddressTable<ulong>.Level[] _levels32Bit =
            new AddressTable<ulong>.Level[]
            {
                new(23, 9),
                new(15, 8),
                new( 7, 8),
                new( 1, 6),
            };

        private readonly ConcurrentQueue<KeyValuePair<ulong, TranslatedFunction>> _oldFuncs;
        private bool _disposed;

        internal TranslatorCache<TranslatedFunction> Functions { get; }
        internal AddressTable<ulong> FunctionTable { get; }
        internal TranslatorStubs Stubs { get; }
        internal IMemoryManager Memory { get; }

        public Translator(IJitMemoryAllocator allocator, IMemoryManager memory, bool for64Bits)
        {
            Memory = memory;

            _oldFuncs = new ConcurrentQueue<KeyValuePair<ulong, TranslatedFunction>>();

            JitCache.Initialize(allocator);
            NativeSignalHandler.Initialize(allocator);

            Functions = new TranslatorCache<TranslatedFunction>();
            FunctionTable = new AddressTable<ulong>(for64Bits ? _levels64Bit : _levels32Bit);
            Stubs = new TranslatorStubs(FunctionTable);

            FunctionTable.Fill = (ulong)Stubs.SlowDispatchStub;

            if (memory.Type == MemoryManagerType.HostMapped || memory.Type == MemoryManagerType.HostMappedUnsafe)
            {
                NativeSignalHandler.InitializeSignalHandler(allocator.GetPageSize());
            }
        }

        public void Execute(State.ExecutionContext context, ulong address)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            NativeInterface.RegisterThread(context, Memory, this);

            Stubs.DispatchLoop(context.NativeContextPtr, address);

            NativeInterface.UnregisterThread();
        }

        internal TranslatedFunction GetOrTranslate(ulong address, ExecutionMode mode)
        {
            if (!Functions.TryGetValue(address, out TranslatedFunction func))
            {
                func = Translate(address, mode);

                TranslatedFunction oldFunc = Functions.GetOrAdd(address, func.GuestSize, func);

                if (oldFunc != func)
                {
                    JitCache.Unmap(func.FuncPointer);
                    func = oldFunc;
                }


                RegisterFunction(address, func);
            }

            return func;
        }

        internal void RegisterFunction(ulong guestAddress, TranslatedFunction func)
        {
            if (FunctionTable.IsValid(guestAddress))
            {
                Volatile.Write(ref FunctionTable.GetValue(guestAddress), (ulong)func.FuncPointer);
            }
        }

        internal TranslatedFunction Translate(ulong address, ExecutionMode mode)
        {
            CompiledFunction func = AarchCompiler.Compile(CpuPresets.CortexA57, Memory, address, FunctionTable, Stubs.DispatchStub, mode, RuntimeInformation.ProcessArchitecture);

            IntPtr funcPointer = JitCache.Map(func.Code);

            return new TranslatedFunction(funcPointer, (ulong)func.GuestCodeLength);
        }

        public void InvalidateJitCacheRegion(ulong address, ulong size)
        {
            ulong[] overlapAddresses = Array.Empty<ulong>();

            int overlapsCount = Functions.GetOverlaps(address, size, ref overlapAddresses);

            for (int index = 0; index < overlapsCount; index++)
            {
                ulong overlapAddress = overlapAddresses[index];

                if (Functions.TryGetValue(overlapAddress, out TranslatedFunction overlap))
                {
                    Functions.Remove(overlapAddress);
                    Volatile.Write(ref FunctionTable.GetValue(overlapAddress), FunctionTable.Fill);
                    EnqueueForDeletion(overlapAddress, overlap);
                }
            }

            // TODO: Remove overlapping functions from the JitCache aswell.
            // This should be done safely, with a mechanism to ensure the function is not being executed.
        }

        private void EnqueueForDeletion(ulong guestAddress, TranslatedFunction func)
        {
            _oldFuncs.Enqueue(new(guestAddress, func));
        }

        private void ClearJitCache()
        {
            List<TranslatedFunction> functions = Functions.AsList();

            foreach (var func in functions)
            {
                JitCache.Unmap(func.FuncPointer);
            }

            Functions.Clear();

            while (_oldFuncs.TryDequeue(out var kv))
            {
                JitCache.Unmap(kv.Value.FuncPointer);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    ClearJitCache();

                    Stubs.Dispose();
                    FunctionTable.Dispose();
                }

                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
