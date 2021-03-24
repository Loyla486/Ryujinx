using Ryujinx.Common.Logging;
using Ryujinx.Cpu;
using Ryujinx.HLE.HOS.Services.Sockets.Sfdnsres.Proxy;
using Ryujinx.HLE.HOS.Services.Sockets.Sfdnsres.Types;
using Ryujinx.Memory;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;

namespace Ryujinx.HLE.HOS.Services.Sockets.Sfdnsres
{
    [Service("sfdnsres")]
    class IResolver : IpcService
    {
        public IResolver(ServiceCtx context) { }

        [Command(0)]
        // SetDnsAddressesPrivateRequest(u32, buffer<unknown, 5, 0>)
        public ResultCode SetDnsAddressesPrivateRequest(ServiceCtx context)
        {
            uint cancelHandleRequest = context.RequestData.ReadUInt32();
            long bufferPosition      = context.Request.SendBuff[0].Position;
            long bufferSize          = context.Request.SendBuff[0].Size;

            // TODO: This is stubbed in 2.0.0+, reverse 1.0.0 version for the sake of completeness.
            Logger.Stub?.PrintStub(LogClass.ServiceSfdnsres, new { cancelHandleRequest });

            return ResultCode.NotAllocated;
        }

        [Command(1)]
        // GetDnsAddressPrivateRequest(u32) -> buffer<unknown, 6, 0>
        public ResultCode GetDnsAddressPrivateRequest(ServiceCtx context)
        {
            uint cancelHandleRequest = context.RequestData.ReadUInt32();
            long bufferPosition      = context.Request.ReceiveBuff[0].Position;
            long bufferSize          = context.Request.ReceiveBuff[0].Size;

            // TODO: This is stubbed in 2.0.0+, reverse 1.0.0 version for the sake of completeness.
            Logger.Stub?.PrintStub(LogClass.ServiceSfdnsres, new { cancelHandleRequest });

            return ResultCode.NotAllocated;
        }

        [Command(2)]
        // GetHostByNameRequest(u8, u32, u64, pid, buffer<unknown, 5, 0>) -> (u32, u32, u32, buffer<unknown, 6, 0>)
        public ResultCode GetHostByNameRequest(ServiceCtx context)
        {
            long inputBufferPosition = context.Request.SendBuff[0].Position;
            long inputBufferSize     = context.Request.SendBuff[0].Size;

            long outputBufferPosition = context.Request.ReceiveBuff[0].Position;
            long outputBufferSize     = context.Request.ReceiveBuff[0].Size;

            return GetHostByNameRequestImpl(context, inputBufferPosition, inputBufferSize, outputBufferPosition, outputBufferSize, 0, 0);
        }

        [Command(3)]
        // GetHostByAddrRequest(u32, u32, u32, u64, pid, buffer<unknown, 5, 0>) -> (u32, u32, u32, buffer<unknown, 6, 0>)
        public ResultCode GetHostByAddrRequest(ServiceCtx context)
        {
            long inputBufferPosition = context.Request.SendBuff[0].Position;
            long inputBufferSize     = context.Request.SendBuff[0].Size;

            long outputBufferPosition = context.Request.ReceiveBuff[0].Position;
            long outputBufferSize     = context.Request.ReceiveBuff[0].Size;

            return GetHostByAddrRequestImpl(context, inputBufferPosition, inputBufferSize, outputBufferPosition, outputBufferSize, 0, 0);
        }

        [Command(4)]
        // GetHostStringErrorRequest(u32) -> buffer<unknown, 6, 0>
        public ResultCode GetHostStringErrorRequest(ServiceCtx context)
        {
            ResultCode resultCode = ResultCode.NotAllocated;
            NetDbError errorCode  = (NetDbError)context.RequestData.ReadInt32();

            string errorString = errorCode switch
            {
                NetDbError.Success      => "Resolver Error 0 (no error)",
                NetDbError.HostNotFound => "Unknown host",
                NetDbError.TryAgain     => "Host name lookup failure",
                NetDbError.NoRecovery   => "Unknown server error",
                NetDbError.NoData       => "No address associated with name",
                _                       => (errorCode <= NetDbError.Internal) ? "Resolver internal error" : "Unknown resolver error"
            };

            long bufferPosition = context.Request.ReceiveBuff[0].Position;
            long bufferSize     = context.Request.ReceiveBuff[0].Size;

            if (errorString.Length + 1 <= bufferSize)
            {
                context.Memory.Write((ulong)bufferPosition, Encoding.ASCII.GetBytes(errorString + '\0'));

                resultCode = ResultCode.Success;
            }

            return resultCode;
        }

        [Command(5)]
        // GetGaiStringErrorRequest(u32) -> buffer<byte, 6, 0>
        public ResultCode GetGaiStringErrorRequest(ServiceCtx context)
        {
            ResultCode resultCode = ResultCode.NotAllocated;
            GaiError   errorCode  = (GaiError)context.RequestData.ReadInt32();

            if (errorCode > GaiError.Max)
            {
                errorCode = GaiError.Max;
            }

            string errorString = errorCode switch
            {
                GaiError.AddressFamily => "Address family for hostname not supported",
                GaiError.Again         => "Temporary failure in name resolution",
                GaiError.BadFlags      => "Invalid value for ai_flags",
                GaiError.Fail          => "Non-recoverable failure in name resolution",
                GaiError.Family        => "ai_family not supported",
                GaiError.Memory        => "Memory allocation failure",
                GaiError.NoData        => "No address associated with hostname",
                GaiError.NoName        => "hostname nor servname provided, or not known",
                GaiError.Service       => "servname not supported for ai_socktype",
                GaiError.SocketType    => "ai_socktype not supported",
                GaiError.System        => "System error returned in errno",
                GaiError.BadHints      => "Invalid value for hints",
                GaiError.Protocol      => "Resolved protocol is unknown",
                GaiError.Overflow      => "Argument buffer overflow",
                GaiError.Max           => "Unknown error",
                _                      => "Success"
            };

            long bufferPosition = context.Request.ReceiveBuff[0].Position;
            long bufferSize     = context.Request.ReceiveBuff[0].Size;

            if (errorString.Length + 1 <= bufferSize)
            {
                context.Memory.Write((ulong)bufferPosition, Encoding.ASCII.GetBytes(errorString + '\0'));

                resultCode = ResultCode.Success;
            }

            return resultCode;
        }

        [Command(6)]
        // GetAddrInfoRequest(bool enable_nsd_resolve, u32, u64 pid_placeholder, pid, buffer<i8, 5, 0> host, buffer<i8, 5, 0> service, buffer<packed_addrinfo, 5, 0> hints) -> (i32 ret, u32 bsd_errno, u32 packed_addrinfo_size, buffer<packed_addrinfo, 6, 0> response)
        public ResultCode GetAddrInfoRequest(ServiceCtx context)
        {
            long responseBufferPosition = context.Request.ReceiveBuff[0].Position;
            long responseBufferSize     = context.Request.ReceiveBuff[0].Size;

            return GetAddrInfoRequestImpl(context, responseBufferPosition, responseBufferSize, 0, 0);
        }

        [Command(8)]
        // GetCancelHandleRequest(u64, pid) -> u32
        public ResultCode GetCancelHandleRequest(ServiceCtx context)
        {
            ulong pidPlaceHolder      = context.RequestData.ReadUInt64();
            uint  cancelHandleRequest = 0;

            context.ResponseData.Write(cancelHandleRequest);

            Logger.Stub?.PrintStub(LogClass.ServiceSfdnsres, new { cancelHandleRequest });

            return ResultCode.Success;
        }

        [Command(9)]
        // CancelRequest(u32, u64, pid)
        public ResultCode CancelRequest(ServiceCtx context)
        {
            uint  cancelHandleRequest = context.RequestData.ReadUInt32();
            ulong pidPlaceHolder      = context.RequestData.ReadUInt64();

            Logger.Stub?.PrintStub(LogClass.ServiceSfdnsres, new { cancelHandleRequest });

            return ResultCode.Success;
        }

        [Command(10)] // 5.0.0+
        // GetHostByNameRequestWithOptions(u8, u32, u64, pid, buffer<unknown, 21, 0>, buffer<unknown, 21, 0>) -> (u32, u32, u32, buffer<unknown, 22, 0>)
        public ResultCode GetHostByNameRequestWithOptions(ServiceCtx context)
        {
            (long inputBufferPosition,   long inputBufferSize)   = context.Request.GetBufferType0x21();
            (long outputBufferPosition,  long outputBufferSize)  = context.Request.GetBufferType0x22();
            (long optionsBufferPosition, long optionsBufferSize) = context.Request.GetBufferType0x21();

            return GetHostByNameRequestImpl(context, inputBufferPosition, inputBufferSize, outputBufferPosition, outputBufferSize, optionsBufferPosition, optionsBufferSize);
        }

        [Command(11)] // 5.0.0+
        // GetHostByAddrRequestWithOptions(u32, u32, u32, u64, pid, buffer<unknown, 21, 0>, buffer<unknown, 21, 0>) -> (u32, u32, u32, buffer<unknown, 22, 0>)
        public ResultCode GetHostByAddrRequestWithOptions(ServiceCtx context)
        {
            (long inputBufferPosition,   long inputBufferSize)   = context.Request.GetBufferType0x21();
            (long outputBufferPosition,  long outputBufferSize)  = context.Request.GetBufferType0x22();
            (long optionsBufferPosition, long optionsBufferSize) = context.Request.GetBufferType0x21();

            return GetHostByAddrRequestImpl(context, inputBufferPosition, inputBufferSize, outputBufferPosition, outputBufferSize, optionsBufferPosition, optionsBufferSize);
        }

        [Command(12)] // 5.0.0+
        // GetAddrInfoRequestWithOptions(bool enable_nsd_resolve, u32, u64 pid_placeholder, pid, buffer<i8, 5, 0> host, buffer<i8, 5, 0> service, buffer<packed_addrinfo, 5, 0> hints, buffer<unknown, 21, 0>) -> (i32 ret, u32 bsd_errno, u32 unknown, u32 packed_addrinfo_size, buffer<packed_addrinfo, 22, 0> response)
        public ResultCode GetAddrInfoRequestWithOptions(ServiceCtx context)
        {
            (long responseBufferPosition, long responseBufferSize) = context.Request.GetBufferType0x22();
            (long optionsBufferPosition,  long optionsBufferSize)  = context.Request.GetBufferType0x21();

            return GetAddrInfoRequestImpl(context, responseBufferPosition, responseBufferSize, optionsBufferPosition, optionsBufferSize);
        }

        private ResultCode GetHostByNameRequestImpl(ServiceCtx context, long inputBufferPosition, long inputBufferSize, long outputBufferPosition, long outputBufferSize, long optionsBufferPosition, long optionsBufferSize)
        {
            byte[] rawName = new byte[inputBufferSize];

            context.Memory.Read((ulong)inputBufferPosition, rawName);

            string name = Encoding.ASCII.GetString(rawName).TrimEnd('\0');

            // TODO: Use params.
            bool  enableNsdResolve = (context.RequestData.ReadInt32() & 1) != 0;
            int   timeOut          = context.RequestData.ReadInt32();
            ulong pidPlaceholder   = context.RequestData.ReadUInt64();

            if (optionsBufferSize > 0)
            {
                // TODO: Parse and use options.
            }

            IPHostEntry hostEntry = null;

            NetDbError netDbErrorCode = NetDbError.Success;
            GaiError   errno          = GaiError.Overflow;
            long       serializedSize = 0;

            if (name.Length <= byte.MaxValue)
            {
                string targetHost = name;

                if (DnsBlacklist.IsHostBlocked(name))
                {
                    Logger.Info?.Print(LogClass.ServiceSfdnsres, $"DNS Blocked: {name}");

                    netDbErrorCode = NetDbError.HostNotFound;
                    errno          = GaiError.NoData;
                }
                else
                {
                    Logger.Info?.Print(LogClass.ServiceSfdnsres, $"Trying to resolve: {name}");

                    try
                    {
                        hostEntry = Dns.GetHostEntry(targetHost);
                    }
                    catch (SocketException exception)
                    {
                        netDbErrorCode = ConvertSocketErrorCodeToNetDbError(exception.ErrorCode);
                        errno          = ConvertSocketErrorCodeToGaiError(exception.ErrorCode, errno);
                    }
                }
            }
            else
            {
                netDbErrorCode = NetDbError.HostNotFound;
            }

            if (hostEntry != null)
            {
                IEnumerable<IPAddress> addresses = GetIpv4Addresses(hostEntry);

                if (!addresses.Any())
                {
                    errno          = GaiError.NoData;
                    netDbErrorCode = NetDbError.NoAddress;
                }
                else
                {
                    errno          = GaiError.Success;
                    serializedSize = SerializeHostEntries(context, outputBufferPosition, outputBufferSize, hostEntry, addresses);
                }
            }

            context.ResponseData.Write((int)netDbErrorCode);
            context.ResponseData.Write((int)errno);
            context.ResponseData.Write(serializedSize);

            return ResultCode.Success;
        }

        private ResultCode GetHostByAddrRequestImpl(ServiceCtx context, long inputBufferPosition, long inputBufferSize, long outputBufferPosition, long outputBufferSize, long optionsBufferPosition, long optionsBufferSize)
        {
            byte[] rawIp = new byte[inputBufferSize];

            context.Memory.Read((ulong)inputBufferPosition, rawIp);

            // TODO: Use params.
            uint  socketLength   = context.RequestData.ReadUInt32();
            uint  type           = context.RequestData.ReadUInt32();
            int   timeOut        = context.RequestData.ReadInt32();
            ulong pidPlaceholder = context.RequestData.ReadUInt64();

            if (optionsBufferSize > 0)
            {
                // TODO: Parse and use options.
            }

            IPHostEntry hostEntry = null;

            NetDbError netDbErrorCode = NetDbError.Success;
            GaiError   errno          = GaiError.AddressFamily;
            long       serializedSize = 0;

            if (rawIp.Length == 4)
            {
                try
                {
                    IPAddress address = new IPAddress(rawIp);

                    hostEntry = Dns.GetHostEntry(address);
                }
                catch (SocketException exception)
                {
                    netDbErrorCode = ConvertSocketErrorCodeToNetDbError(exception.ErrorCode);
                    errno          = ConvertSocketErrorCodeToGaiError(exception.ErrorCode, errno);
                }
            }
            else
            {
                netDbErrorCode = NetDbError.NoAddress;
            }

            if (hostEntry != null)
            {
                errno          = GaiError.Success;
                serializedSize = SerializeHostEntries(context, outputBufferPosition, outputBufferSize, hostEntry, GetIpv4Addresses(hostEntry));
            }

            context.ResponseData.Write((int)netDbErrorCode);
            context.ResponseData.Write((int)errno);
            context.ResponseData.Write(serializedSize);

            return ResultCode.Success;
        }

        private long SerializeHostEntries(ServiceCtx context, long outputBufferPosition, long outputBufferSize, IPHostEntry hostEntry, IEnumerable<IPAddress> addresses = null)
        {
            long originalBufferPosition = outputBufferPosition;
            long bufferPosition         = originalBufferPosition;

            string hostName = hostEntry.HostName + '\0';

            // h_name
            context.Memory.Write((ulong)bufferPosition, Encoding.ASCII.GetBytes(hostName));
            bufferPosition += hostName.Length;

            // h_aliases list size
            context.Memory.Write((ulong)bufferPosition, BinaryPrimitives.ReverseEndianness(hostEntry.Aliases.Length));
            bufferPosition += 4;

            // Actual aliases
            foreach (string alias in hostEntry.Aliases)
            {
                context.Memory.Write((ulong)bufferPosition, Encoding.ASCII.GetBytes(alias + '\0'));
                bufferPosition += alias.Length + 1;
            }

            // h_addrtype but it's a short (also only support IPv4)
            context.Memory.Write((ulong)bufferPosition, BinaryPrimitives.ReverseEndianness((short)AddressFamily.InterNetwork));
            bufferPosition += 2;

            // h_length but it's a short
            context.Memory.Write((ulong)bufferPosition, BinaryPrimitives.ReverseEndianness((short)4));
            bufferPosition += 2;

            // Ip address count, we can only support ipv4 (blame Nintendo)
            context.Memory.Write((ulong)bufferPosition, addresses != null ? BinaryPrimitives.ReverseEndianness(addresses.Count()) : 0);
            bufferPosition += 4;

            if (addresses != null)
            {
                foreach (IPAddress ip in addresses)
                {
                    context.Memory.Write((ulong)bufferPosition, BinaryPrimitives.ReverseEndianness(BitConverter.ToInt32(ip.GetAddressBytes(), 0)));
                    bufferPosition += 4;
                }
            }

            return bufferPosition - originalBufferPosition;
        }

        private ResultCode GetAddrInfoRequestImpl(ServiceCtx context, long responseBufferPosition, long responseBufferSize, long optionsBufferPosition, long optionsBufferSize)
        {
            bool enableNsdResolve = (context.RequestData.ReadInt32() & 1) != 0;
            uint cancelHandle     = context.RequestData.ReadUInt32();

            string host    = MemoryHelper.ReadAsciiString(context.Memory, context.Request.SendBuff[0].Position, context.Request.SendBuff[0].Size);
            string service = MemoryHelper.ReadAsciiString(context.Memory, context.Request.SendBuff[1].Position, context.Request.SendBuff[1].Size);

            // NOTE: We ignore hints for now.
            DeserializeAddrInfos(context.Memory, (ulong)context.Request.SendBuff[2].Position, (ulong)context.Request.SendBuff[2].Size);

            if (optionsBufferSize > 0)
            {
                // TODO: Find unknown, Parse and use options.
                uint unknown = context.RequestData.ReadUInt32();
            }

            ulong pidPlaceHolder = context.RequestData.ReadUInt64();

            Logger.Stub?.PrintStub(LogClass.ServiceSfdnsres, new { enableNsdResolve, cancelHandle, pidPlaceHolder, host, service });

            IPHostEntry hostEntry = null;

            NetDbError netDbErrorCode = NetDbError.Success;
            GaiError   errno          = GaiError.AddressFamily;
            ulong      serializedSize = 0;

            if (host.Length <= byte.MaxValue)
            {
                string targetHost = host;

                if (DnsBlacklist.IsHostBlocked(host))
                {
                    Logger.Info?.Print(LogClass.ServiceSfdnsres, $"DNS Blocked: {host}");

                    netDbErrorCode = NetDbError.HostNotFound;
                    errno          = GaiError.NoData;
                }
                else
                {
                    Logger.Info?.Print(LogClass.ServiceSfdnsres, $"Trying to resolve: {host}");

                    try
                    {
                        hostEntry = Dns.GetHostEntry(targetHost);
                    }
                    catch (SocketException exception)
                    {
                        netDbErrorCode = ConvertSocketErrorCodeToNetDbError(exception.ErrorCode);
                        errno          = ConvertSocketErrorCodeToGaiError(exception.ErrorCode, errno);
                    }
                }
            }
            else
            {
                netDbErrorCode = NetDbError.NoAddress;
            }

            if (hostEntry != null)
            {
                int.TryParse(service, out int port);

                errno          = GaiError.Success;
                serializedSize = SerializeAddrInfos(context, responseBufferPosition, responseBufferSize, hostEntry, port);
            }

            context.ResponseData.Write((int)netDbErrorCode);
            context.ResponseData.Write((int)errno);
            context.ResponseData.Write(serializedSize);

            return ResultCode.Success;
        }

        private void DeserializeAddrInfos(IVirtualMemoryManager memory, ulong address, ulong size)
        {
            ulong endAddress = address + size;

            while (address < endAddress)
            {
                AddrInfoSerializedHeader header = memory.Read<AddrInfoSerializedHeader>(address);

                if (header.Magic != SfdnsresContants.AddrInfoMagic)
                {
                    break;
                }

                address += (ulong)Unsafe.SizeOf<AddrInfoSerializedHeader>() + header.AddressLength;

                // ai_canonname
                string canonname = string.Empty;

                while (true)
                {
                    byte chr = memory.Read<byte>(address++);

                    if (chr == 0)
                    {
                        break;
                    }

                    canonname += (char)chr;
                }
            }
        }

        private ulong SerializeAddrInfos(ServiceCtx context, long responseBufferPosition, long responseBufferSize, IPHostEntry hostEntry, int port)
        {
            ulong originalBufferPosition = (ulong)responseBufferPosition;
            ulong bufferPosition         = originalBufferPosition;

            string hostName = hostEntry.HostName + '\0';

            for (int i = 0; i < hostEntry.AddressList.Length; i++)
            {
                IPAddress ip = hostEntry.AddressList[i];

                if (ip.AddressFamily != AddressFamily.InterNetwork)
                {
                    continue;
                }

                AddrInfoSerializedHeader header = new AddrInfoSerializedHeader(ip, 0);

                // NOTE: 0 = Any
                context.Memory.Write(bufferPosition, header);
                bufferPosition += (ulong)Unsafe.SizeOf<AddrInfoSerializedHeader>();

                // addrinfo_in
                context.Memory.Write(bufferPosition, new AddrInfo4(ip, (short)port));
                bufferPosition += header.AddressLength;

                // ai_canonname
                context.Memory.Write(bufferPosition, Encoding.ASCII.GetBytes(hostName));
                bufferPosition += (ulong)hostName.Length;
            }

            // Termination zero value.
            context.Memory.Write(bufferPosition, 0);
            bufferPosition += 4;

            return bufferPosition - originalBufferPosition;
        }

        private IEnumerable<IPAddress> GetIpv4Addresses(IPHostEntry hostEntry)
        {
            return hostEntry.AddressList.Where(x => x.AddressFamily == AddressFamily.InterNetwork);
        }

        private NetDbError ConvertSocketErrorCodeToNetDbError(int errorCode)
        {
            return errorCode switch
            {
                11001 => NetDbError.HostNotFound,
                11002 => NetDbError.TryAgain,
                11003 => NetDbError.NoRecovery,
                11004 => NetDbError.NoData,
                _     => NetDbError.Internal
            };
        }

        private GaiError ConvertSocketErrorCodeToGaiError(int errorCode, GaiError errno)
        {
            return errorCode switch
            {
                11001 => GaiError.NoData,
                10060 => GaiError.Again,
                _     => errno
            };
        }
    }
}