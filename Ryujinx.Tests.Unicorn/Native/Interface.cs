using Ryujinx.Tests.Unicorn.Native.Const;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Ryujinx.Tests.Unicorn.Native
{
    public static class Interface
    {
        public static bool IsUnicornAvailable { get; private set; } = true;

        private static IntPtr ImportResolver(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
        {
            if (libraryName == "unicorn")
            {
                string loadPath = $"{Path.GetDirectoryName(assembly.Location)}/";
                loadPath += OperatingSystem.IsWindows() ? $"{libraryName}.dll" : $"lib{libraryName}.so";

                if (!NativeLibrary.TryLoad(loadPath, out IntPtr libraryPtr))
                {
                    IsUnicornAvailable = false;
                    Console.Error.WriteLine($"ERROR: Could not find unicorn at: {loadPath}");
                }

                return libraryPtr;
            }

            // Otherwise, fallback to default import resolver.
            return IntPtr.Zero;
        }

        static Interface()
        {
            NativeLibrary.SetDllImportResolver(Assembly.GetExecutingAssembly(), ImportResolver);
        }

        public static void Checked(Error error)
        {
            if (error != Error.OK)
            {
                throw new UnicornException(error);
            }
        }

        public static void MarshalArrayOf<T>(IntPtr input, int length, out T[] output)
        {
            int size = Marshal.SizeOf(typeof(T));

            output = new T[length];

            for (int i = 0; i < length; i++)
            {
                IntPtr item = new IntPtr(input.ToInt64() + i * size);

                output[i] = Marshal.PtrToStructure<T>(item);
            }
        }

        [DllImport("unicorn", CallingConvention = CallingConvention.Cdecl)]
        public static extern uint uc_version(out uint major, out uint minor);

        [DllImport("unicorn", CallingConvention = CallingConvention.Cdecl)]
        public static extern Error uc_open(Arch arch, Mode mode, out IntPtr uc);

        [DllImport("unicorn", CallingConvention = CallingConvention.Cdecl)]
        public static extern Error uc_close(IntPtr uc);

        [DllImport("unicorn", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr uc_strerror(Error err);

        [DllImport("unicorn", CallingConvention = CallingConvention.Cdecl)]
        public static extern Error uc_reg_write(IntPtr uc, int regid, byte[] value);

        [DllImport("unicorn", CallingConvention = CallingConvention.Cdecl)]
        public static extern Error uc_reg_read(IntPtr uc, int regid, byte[] value);

        [DllImport("unicorn", CallingConvention = CallingConvention.Cdecl)]
        public static extern Error uc_mem_write(IntPtr uc, ulong address, byte[] bytes, ulong size);

        [DllImport("unicorn", CallingConvention = CallingConvention.Cdecl)]
        public static extern Error uc_mem_read(IntPtr uc, ulong address, byte[] bytes, ulong size);

        [DllImport("unicorn", CallingConvention = CallingConvention.Cdecl)]
        public static extern Error uc_emu_start(IntPtr uc, ulong begin, ulong until, ulong timeout, ulong count);

        [DllImport("unicorn", CallingConvention = CallingConvention.Cdecl)]
        public static extern Error uc_mem_map(IntPtr uc, ulong address, ulong size, uint perms);

        [DllImport("unicorn", CallingConvention = CallingConvention.Cdecl)]
        public static extern Error uc_mem_unmap(IntPtr uc, ulong address, ulong size);

        [DllImport("unicorn", CallingConvention = CallingConvention.Cdecl)]
        public static extern Error uc_mem_protect(IntPtr uc, ulong address, ulong size, uint perms);

        [DllImport("unicorn", CallingConvention = CallingConvention.Cdecl)]
        public static extern Error uc_mem_regions(IntPtr uc, out IntPtr regions, out uint count);
    }
}