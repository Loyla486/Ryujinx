using ARMeilleure.State;
using Ryujinx.Common;
using Ryujinx.Common.Logging;
using Ryujinx.HLE.HOS.Kernel;
using Ryujinx.HLE.HOS.Kernel.Threading;
using Ryujinx.Memory;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using IExecutionContext = Ryujinx.Cpu.IExecutionContext;

namespace Ryujinx.HLE.Debugger
{
    public class Debugger : IDisposable
    {
        internal Switch Device { get; private set; }

        public ushort GdbStubPort { get; private set; }

        private TcpListener ListenerSocket;
        private Socket ClientSocket = null;
        private NetworkStream ReadStream = null;
        private NetworkStream WriteStream = null;
        private BlockingCollection<IMessage> Messages = new BlockingCollection<IMessage>(1);
        private Thread DebuggerThread;
        private Thread MessageHandlerThread;
        private bool _shuttingDown = false;

        private ulong? cThread;
        private ulong? gThread;

        public Debugger(Switch device, ushort port)
        {
            Device = device;
            GdbStubPort = port;

            ARMeilleure.Optimizations.EnableDebugging = true;

            DebuggerThread = new Thread(DebuggerThreadMain);
            DebuggerThread.Start();
            MessageHandlerThread = new Thread(MessageHandlerMain);
            MessageHandlerThread.Start();
        }

        private IDebuggableProcess DebugProcess => Device.System.DebugGetApplicationProcess();
        private KThread[] GetThreads() => DebugProcess.GetThreadUids().Select(x => DebugProcess.GetThread(x)).ToArray();
        private bool IsProcessAarch32 => DebugProcess.GetThread(gThread.Value).Context.IsAarch32;
        private KernelContext KernelContext => Device.System.KernelContext;

        const int GdbRegisterCount64 = 68;
        const int GdbRegisterCount32 = 66;
        /* FPCR = FPSR & ~FpcrMask
        All of FPCR's bits are reserved in FPCR and vice versa,
        see ARM's documentation. */
        private const uint FpcrMask = 0xfc1fffff;

        private string GdbReadRegister64(IExecutionContext state, int gdbRegId)
        {
            switch (gdbRegId)
            {
                case >= 0 and <= 31:
                    return ToHex(BitConverter.GetBytes(state.GetX(gdbRegId)));
                case 32:
                    return ToHex(BitConverter.GetBytes(state.DebugPc));
                case 33:
                    return ToHex(BitConverter.GetBytes(state.Pstate));
                case >= 34 and <= 65:
                    return ToHex(state.GetV(gdbRegId - 34).ToArray());
                case 66:
                    return ToHex(BitConverter.GetBytes((uint)state.Fpsr));
                case 67:
                    return ToHex(BitConverter.GetBytes((uint)state.Fpcr));
                default:
                    return null;
            }
        }

        private bool GdbWriteRegister64(IExecutionContext state, int gdbRegId, StringStream ss)
        {
            switch (gdbRegId)
            {
                case >= 0 and <= 31:
                    {
                        ulong value = ss.ReadLengthAsHex(16);
                        state.SetX(gdbRegId, value);
                        return true;
                    }
                case 32:
                    {
                        ulong value = ss.ReadLengthAsHex(16);
                        state.DebugPc = value;
                        return true;
                    }
                case 33:
                    {
                        ulong value = ss.ReadLengthAsHex(8);
                        state.Pstate = (uint)value;
                        return true;
                    }
                case >= 34 and <= 65:
                    {
                        ulong value0 = ss.ReadLengthAsHex(16);
                        ulong value1 = ss.ReadLengthAsHex(16);
                        state.SetV(gdbRegId - 34, new V128(value0, value1));
                        return true;
                    }
                case 66:
                    {
                        ulong value = ss.ReadLengthAsHex(8);
                        state.Fpsr = (uint)value;
                        return true;
                    }
                case 67:
                    {
                        ulong value = ss.ReadLengthAsHex(8);
                        state.Fpcr = (uint)value;
                        return true;
                    }
                default:
                    return false;
            }
        }

        private string GdbReadRegister32(IExecutionContext state, int gdbRegId)
        {
            switch (gdbRegId)
            {
                case >= 0 and <= 14:
                    return ToHex(BitConverter.GetBytes((uint)state.GetX(gdbRegId)));
                case 15:
                    return ToHex(BitConverter.GetBytes((uint)state.DebugPc));
                case 16:
                    return ToHex(BitConverter.GetBytes((uint)state.Pstate));
                case >= 17 and <= 32:
                    return ToHex(state.GetV(gdbRegId - 17).ToArray());
                case >= 33 and <= 64:
                    int reg = (gdbRegId - 33);
                    int n = reg / 2;
                    int shift = reg % 2;
                    ulong value = state.GetV(n).Extract<ulong>(shift);
                    return ToHex(BitConverter.GetBytes(value));
                case 65:
                    uint fpscr = (uint)state.Fpsr | (uint)state.Fpcr;
                    return ToHex(BitConverter.GetBytes(fpscr));
                default:
                    return null;
            }
        }

        private bool GdbWriteRegister32(IExecutionContext state, int gdbRegId, StringStream ss)
        {
            switch (gdbRegId)
            {
                case >= 0 and <= 14:
                    {
                        ulong value = ss.ReadLengthAsHex(8);
                        state.SetX(gdbRegId, value);
                        return true;
                    }
                case 15:
                    {
                        ulong value = ss.ReadLengthAsHex(8);
                        state.DebugPc = value;
                        return true;
                    }
                case 16:
                    {
                        ulong value = ss.ReadLengthAsHex(8);
                        state.Pstate = (uint)value;
                        return true;
                    }
                case >= 17 and <= 32:
                    {
                        ulong value0 = ss.ReadLengthAsHex(16);
                        ulong value1 = ss.ReadLengthAsHex(16);
                        state.SetV(gdbRegId - 17, new V128(value0, value1));
                        return true;
                    }
                case >= 33 and <= 64:
                    {
                        ulong value = ss.ReadLengthAsHex(16);
                        int regId = (gdbRegId - 33);
                        int regNum = regId / 2;
                        int shift = regId % 2;
                        V128 reg = state.GetV(regNum);
                        reg.Insert(shift, value);
                        return true;
                    }
                case 65:
                    {
                        ulong value = ss.ReadLengthAsHex(8);
                        state.Fpsr = (uint)value & FpcrMask;
                        state.Fpcr = (uint)value & ~FpcrMask;
                        return true;
                    }
                default:
                    return false;
            }
        }

        private void MessageHandlerMain()
        {
            while (!_shuttingDown)
            {
                IMessage msg = Messages.Take();
                switch (msg)
                {
                    case BreakInMessage:
                        Logger.Notice.Print(LogClass.GdbStub, "Break-in requested");
                        CommandQuery();
                        break;

                    case SendNackMessage:
                        WriteStream.WriteByte((byte)'-');
                        break;

                    case CommandMessage { Command: var cmd }:
                        Logger.Debug?.Print(LogClass.GdbStub, $"Received Command: {cmd}");
                        WriteStream.WriteByte((byte)'+');
                        ProcessCommand(cmd);
                        break;

                    case ThreadBreakMessage { Context: var ctx }:
                        DebugProcess.DebugStop();
                        Reply($"T05thread:{ctx.ThreadUid:x};");
                        break;

                    case KillMessage:
                        return;
                }
            }
        }

        private void ProcessCommand(string cmd)
        {
            StringStream ss = new StringStream(cmd);

            switch (ss.ReadChar())
            {
                case '!':
                    if (!ss.IsEmpty())
                    {
                        goto unknownCommand;
                    }

                    // Enable extended mode
                    ReplyOK();
                    break;
                case '?':
                    if (!ss.IsEmpty())
                    {
                        goto unknownCommand;
                    }

                    CommandQuery();
                    break;
                case 'c':
                    CommandContinue(ss.IsEmpty() ? null : ss.ReadRemainingAsHex());
                    break;
                case 'D':
                    if (!ss.IsEmpty())
                    {
                        goto unknownCommand;
                    }

                    CommandDetach();
                    break;
                case 'g':
                    if (!ss.IsEmpty())
                    {
                        goto unknownCommand;
                    }

                    CommandReadRegisters();
                    break;
                case 'G':
                    CommandWriteRegisters(ss);
                    break;
                case 'H':
                    {
                        char op = ss.ReadChar();
                        ulong? threadId = ss.ReadRemainingAsThreadUid();
                        CommandSetThread(op, threadId);
                        break;
                    }
                case 'k':
                    Logger.Notice.Print(LogClass.GdbStub, "Kill request received");
                    Reply("");
                    Device.IsActive = false;
                    Device.ExitStatus.WaitOne();
                    break;
                case 'm':
                    {
                        ulong addr = ss.ReadUntilAsHex(',');
                        ulong len = ss.ReadRemainingAsHex();
                        CommandReadMemory(addr, len);
                        break;
                    }
                case 'M':
                    {
                        ulong addr = ss.ReadUntilAsHex(',');
                        ulong len = ss.ReadUntilAsHex(':');
                        CommandWriteMemory(addr, len, ss);
                        break;
                    }
                case 'p':
                    {
                        ulong gdbRegId = ss.ReadRemainingAsHex();
                        CommandReadRegister((int)gdbRegId);
                        break;
                    }
                case 'P':
                    {
                        ulong gdbRegId = ss.ReadUntilAsHex('=');
                        CommandWriteRegister((int)gdbRegId, ss);
                        break;
                    }
                case 'q':
                    if (ss.ConsumeRemaining("GDBServerVersion"))
                    {
                        Reply($"name:Ryujinx;version:{ReleaseInformation.Version};");
                        break;
                    }

                    if (ss.ConsumeRemaining("HostInfo"))
                    {
                        if (IsProcessAarch32)
                        {
                            Reply(
                                $"triple:{ToHex("arm-unknown-linux-android")};endian:little;ptrsize:4;hostname:{ToHex("Ryujinx")};");
                        }
                        else
                        {
                            Reply(
                                $"triple:{ToHex("aarch64-unknown-linux-android")};endian:little;ptrsize:8;hostname:{ToHex("Ryujinx")};");
                        }
                        break;
                    }

                    if (ss.ConsumeRemaining("ProcessInfo"))
                    {
                        if (IsProcessAarch32)
                        {
                            Reply(
                                $"pid:1;cputype:12;cpusubtype:0;triple:{ToHex("arm-unknown-linux-android")};ostype:unknown;vendor:none;endian:little;ptrsize:4;");
                        }
                        else
                        {
                            Reply(
                                $"pid:1;cputype:100000c;cpusubtype:0;triple:{ToHex("aarch64-unknown-linux-android")};ostype:unknown;vendor:none;endian:little;ptrsize:8;");
                        }
                        break;
                    }

                    if (ss.ConsumePrefix("Supported:") || ss.ConsumeRemaining("Supported"))
                    {
                        Reply("PacketSize=10000;qXfer:features:read+");
                        break;
                    }

                    if (ss.ConsumeRemaining("fThreadInfo"))
                    {
                        Reply($"m{string.Join(",", DebugProcess.GetThreadUids().Select(x => $"{x:x}"))}");
                        break;
                    }

                    if (ss.ConsumeRemaining("sThreadInfo"))
                    {
                        Reply("l");
                        break;
                    }

                    if (ss.ConsumePrefix("ThreadExtraInfo,"))
                    {
                        ulong? threadId = ss.ReadRemainingAsThreadUid();
                        if (threadId == null)
                        {
                            ReplyError();
                            break;
                        }

                        if (DebugProcess.GetDebugState() == DebugState.Stopped)
                        {
                            Reply(ToHex("Stopped"));
                        }
                        else
                        {
                            Reply(ToHex("Not stopped"));
                        }
                        break;
                    }

                    if (ss.ConsumePrefix("Xfer:features:read:"))
                    {
                        string feature = ss.ReadUntil(':');
                        ulong addr = ss.ReadUntilAsHex(',');
                        ulong len = ss.ReadRemainingAsHex();

                        if (feature == "target.xml")
                        {
                            feature = IsProcessAarch32 ? "target32.xml" : "target64.xml";
                        }

                        string data;
                        if (RegisterInformation.Features.TryGetValue(feature, out data))
                        {
                            if (addr >= (ulong)data.Length)
                            {
                                Reply("l");
                                break;
                            }

                            if (len >= (ulong)data.Length - addr)
                            {
                                Reply("l" + ToBinaryFormat(data.Substring((int)addr)));
                                break;
                            }
                            else
                            {
                                Reply("m" + ToBinaryFormat(data.Substring((int)addr, (int)len)));
                                break;
                            }
                        }
                        else
                        {
                            Reply("E00"); // Invalid annex
                            break;
                        }
                    }

                    goto unknownCommand;
                case 'Q':
                    goto unknownCommand;
                case 's':
                    CommandStep(ss.IsEmpty() ? null : ss.ReadRemainingAsHex());
                    break;
                case 'T':
                    {
                        ulong? threadId = ss.ReadRemainingAsThreadUid();
                        CommandIsAlive(threadId);
                        break;
                    }
                default:
                unknownCommand:
                    Logger.Notice.Print(LogClass.GdbStub, $"Unknown command: {cmd}");
                    Reply("");
                    break;
            }
        }

        void CommandQuery()
        {
            // GDB is performing initial contact. Stop everything.
            DebugProcess.DebugStop();
            gThread = cThread = DebugProcess.GetThreadUids().First();
            Reply($"T05thread:{cThread:x};");
        }

        void CommandContinue(ulong? newPc)
        {
            if (newPc.HasValue)
            {
                if (cThread == null)
                {
                    ReplyError();
                    return;
                }

                DebugProcess.GetThread(cThread.Value).Context.DebugPc = newPc.Value;
            }

            DebugProcess.DebugContinue();
        }

        void CommandDetach()
        {
            // TODO: Remove all breakpoints
            CommandContinue(null);
        }

        void CommandReadRegisters()
        {
            if (gThread == null)
            {
                ReplyError();
                return;
            }

            var ctx = DebugProcess.GetThread(gThread.Value).Context;
            string registers = "";
            if (IsProcessAarch32)
            {
                for (int i = 0; i < GdbRegisterCount32; i++)
                {
                    registers += GdbReadRegister32(ctx, i);
                }
            }
            else
            {
                for (int i = 0; i < GdbRegisterCount64; i++)
                {
                    registers += GdbReadRegister64(ctx, i);
                }
            }

            Reply(registers);
        }

        void CommandWriteRegisters(StringStream ss)
        {
            if (gThread == null)
            {
                ReplyError();
                return;
            }

            var ctx = DebugProcess.GetThread(gThread.Value).Context;
            if (IsProcessAarch32)
            {
                for (int i = 0; i < GdbRegisterCount32; i++)
                {
                    if (!GdbWriteRegister32(ctx, i, ss))
                    {
                        ReplyError();
                        return;
                    }
                }
            }
            else
            {
                for (int i = 0; i < GdbRegisterCount64; i++)
                {
                    if (!GdbWriteRegister64(ctx, i, ss))
                    {
                        ReplyError();
                        return;
                    }
                }
            }

            if (ss.IsEmpty())
            {
                ReplyOK();
            }
            else
            {
                ReplyError();
            }
        }

        void CommandSetThread(char op, ulong? threadId)
        {
            if (threadId == 0 || threadId == null)
            {
                threadId = GetThreads().First().ThreadUid;
            }

            if (DebugProcess.GetThread(threadId.Value) == null)
            {
                ReplyError();
                return;
            }

            switch (op)
            {
                case 'c':
                    cThread = threadId;
                    ReplyOK();
                    return;
                case 'g':
                    gThread = threadId;
                    ReplyOK();
                    return;
                default:
                    ReplyError();
                    return;
            }
        }

        void CommandReadMemory(ulong addr, ulong len)
        {
            try
            {
                var data = new byte[len];
                DebugProcess.CpuMemory.Read(addr, data);
                Reply(ToHex(data));
            }
            catch (InvalidMemoryRegionException)
            {
                ReplyError();
            }
        }

        void CommandWriteMemory(ulong addr, ulong len, StringStream ss)
        {
            try
            {
                var data = new byte[len];
                for (ulong i = 0; i < len; i++)
                {
                    data[i] = (byte)ss.ReadLengthAsHex(2);
                }

                DebugProcess.CpuMemory.Write(addr, data);
                DebugProcess.InvalidateCacheRegion(addr, len);
                ReplyOK();
            }
            catch (InvalidMemoryRegionException)
            {
                ReplyError();
            }
        }

        void CommandReadRegister(int gdbRegId)
        {
            if (gThread == null)
            {
                ReplyError();
                return;
            }

            var ctx = DebugProcess.GetThread(gThread.Value).Context;
            string result;
            if (IsProcessAarch32)
            {
                result = GdbReadRegister32(ctx, gdbRegId);
                if (result != null)
                {
                    Reply(result);
                }
                else
                {
                    ReplyError();
                }
            }
            else
            {
                result = GdbReadRegister64(ctx, gdbRegId);
                if (result != null)
                {
                    Reply(result);
                }
                else
                {
                    ReplyError();
                }
            }
        }

        void CommandWriteRegister(int gdbRegId, StringStream ss)
        {
            if (gThread == null)
            {
                ReplyError();
                return;
            }

            var ctx = DebugProcess.GetThread(gThread.Value).Context;
            if (IsProcessAarch32)
            {
                if (GdbWriteRegister32(ctx, gdbRegId, ss) && ss.IsEmpty())
                {
                    ReplyOK();
                }
                else
                {
                    ReplyError();
                }
            }
            else
            {
                if (GdbWriteRegister64(ctx, gdbRegId, ss) && ss.IsEmpty())
                {
                    ReplyOK();
                }
                else
                {
                    ReplyError();
                }
            }
        }

        private void CommandStep(ulong? newPc)
        {
            if (cThread == null)
            {
                ReplyError();
                return;
            }

            var thread = DebugProcess.GetThread(cThread.Value);

            if (newPc.HasValue)
            {
                thread.Context.DebugPc = newPc.Value;
            }

            if (!DebugProcess.DebugStep(thread))
            {
                ReplyError();
            }
            else
            {
                Reply($"T05thread:{thread.ThreadUid:x};");
            }
        }

        private void CommandIsAlive(ulong? threadId)
        {
            if (GetThreads().Any(x => x.ThreadUid == threadId))
            {
                ReplyOK();
            }
            else
            {
                Reply("E00");
            }
        }

        private void Reply(string cmd)
        {
            Logger.Debug?.Print(LogClass.GdbStub, $"Reply: {cmd}");
            WriteStream.Write(Encoding.ASCII.GetBytes($"${cmd}#{CalculateChecksum(cmd):x2}"));
        }

        private void ReplyOK()
        {
            Reply("OK");
        }

        private void ReplyError()
        {
            Reply("E01");
        }

        private void DebuggerThreadMain()
        {
            var endpoint = new IPEndPoint(IPAddress.Any, GdbStubPort);
            ListenerSocket = new TcpListener(endpoint);
            ListenerSocket.Start();
            Logger.Notice.Print(LogClass.GdbStub, $"Currently waiting on {endpoint} for GDB client");

            while (!_shuttingDown)
            {
                try
                {
                    ClientSocket = ListenerSocket.AcceptSocket();
                }
                catch (SocketException)
                {
                    return;
                }
                ClientSocket.NoDelay = true;
                ReadStream = new NetworkStream(ClientSocket, System.IO.FileAccess.Read);
                WriteStream = new NetworkStream(ClientSocket, System.IO.FileAccess.Write);
                Logger.Notice.Print(LogClass.GdbStub, "GDB client connected");

                while (true)
                {
                    try
                    {
                        switch (ReadStream.ReadByte())
                        {
                            case -1:
                                goto eof;
                            case '+':
                                continue;
                            case '-':
                                Logger.Notice.Print(LogClass.GdbStub, "NACK received!");
                                continue;
                            case '\x03':
                                Messages.Add(new BreakInMessage());
                                break;
                            case '$':
                                string cmd = "";
                                while (true)
                                {
                                    int x = ReadStream.ReadByte();
                                    if (x == -1)
                                        goto eof;
                                    if (x == '#')
                                        break;
                                    cmd += (char)x;
                                }

                                string checksum = $"{(char)ReadStream.ReadByte()}{(char)ReadStream.ReadByte()}";
                                if (checksum == $"{CalculateChecksum(cmd):x2}")
                                {
                                    Messages.Add(new CommandMessage(cmd));
                                }
                                else
                                {
                                    Messages.Add(new SendNackMessage());
                                }

                                break;
                        }
                    }
                    catch (IOException)
                    {
                        goto eof;
                    }
                }

            eof:
                Logger.Notice.Print(LogClass.GdbStub, "GDB client lost connection");
                ReadStream.Close();
                ReadStream = null;
                WriteStream.Close();
                WriteStream = null;
                ClientSocket.Close();
                ClientSocket = null;
            }
        }

        private byte CalculateChecksum(string cmd)
        {
            byte checksum = 0;
            foreach (char x in cmd)
            {
                unchecked
                {
                    checksum += (byte)x;
                }
            }

            return checksum;
        }

        private string ToHex(byte[] bytes)
        {
            return string.Join("", bytes.Select(x => $"{x:x2}"));
        }

        private string ToHex(string str)
        {
            return ToHex(Encoding.ASCII.GetBytes(str));
        }

        private string ToBinaryFormat(byte[] bytes)
        {
            return string.Join("", bytes.Select(x =>
                x switch
                {
                    (byte)'#' => "}\x03",
                    (byte)'$' => "}\x04",
                    (byte)'*' => "}\x0a",
                    (byte)'}' => "}\x5d",
                    _ => Convert.ToChar(x).ToString(),
                }
            ));
        }

        private string ToBinaryFormat(string str)
        {
            return ToBinaryFormat(Encoding.ASCII.GetBytes(str));
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _shuttingDown = true;

                ListenerSocket.Stop();
                ClientSocket?.Shutdown(SocketShutdown.Both);
                ClientSocket?.Close();
                ReadStream?.Close();
                WriteStream?.Close();
                DebuggerThread.Join();
                Messages.Add(new KillMessage());
                MessageHandlerThread.Join();
                Messages.Dispose();
            }
        }

        public void BreakHandler(IExecutionContext ctx, ulong address, int imm)
        {
            Logger.Notice.Print(LogClass.GdbStub, $"Break hit on thread {ctx.ThreadUid} at pc {address:x016}");

            Messages.Add(new ThreadBreakMessage(ctx, address, imm));
            DebugProcess.DebugInterruptHandler(ctx);
        }

        public void StepHandler(IExecutionContext ctx)
        {
            DebugProcess.DebugInterruptHandler(ctx);
        }
    }
}
