using Ryujinx.Common.Logging;
using Ryujinx.HLE.HOS.Ipc;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

using static Ryujinx.HLE.HOS.ErrorCode;

namespace Ryujinx.HLE.HOS.Services.Sfdnsres
{
    internal class IResolver : IpcService
    {
        private Dictionary<int, ServiceProcessRequest> _mCommands;

        public override IReadOnlyDictionary<int, ServiceProcessRequest> Commands => _mCommands;

        public IResolver()
        {
            _mCommands = new Dictionary<int, ServiceProcessRequest>()
            {
                { 0,  SetDnsAddressesPrivate },
                { 1,  GetDnsAddressesPrivate },
                { 2,  GetHostByName          },
                { 3,  GetHostByAddress       },
                { 4,  GetHostStringError     },
                { 5,  GetGaiStringError      },
                { 8,  RequestCancelHandle    },
                { 9,  CancelSocketCall       },
                { 11, ClearDnsAddresses      },
            };
        }

        private long SerializeHostEnt(ServiceCtx context, IPHostEntry hostEntry, List<IPAddress> addresses = null)
        {
            long originalBufferPosition = context.Request.ReceiveBuff[0].Position;
            long bufferPosition         = originalBufferPosition;
            long bufferSize             = context.Request.ReceiveBuff[0].Size;

            string hostName = hostEntry.HostName + '\0';

            // h_name
            context.Memory.WriteBytes(bufferPosition, Encoding.ASCII.GetBytes(hostName));
            bufferPosition += hostName.Length;

            // h_aliases list size
            context.Memory.WriteInt32(bufferPosition, IPAddress.HostToNetworkOrder(hostEntry.Aliases.Length));
            bufferPosition += 4;

            // Actual aliases
            foreach (string alias in hostEntry.Aliases)
            {
                context.Memory.WriteBytes(bufferPosition, Encoding.ASCII.GetBytes(alias + '\0'));
                bufferPosition += alias.Length + 1;
            }

            // h_addrtype but it's a short (also only support IPv4)
            context.Memory.WriteInt16(bufferPosition, IPAddress.HostToNetworkOrder((short)2));
            bufferPosition += 2;

            // h_length but it's a short
            context.Memory.WriteInt16(bufferPosition, IPAddress.HostToNetworkOrder((short)4));
            bufferPosition += 2;

            // Ip address count, we can only support ipv4 (blame Nintendo)
            context.Memory.WriteInt32(bufferPosition, addresses != null ? IPAddress.HostToNetworkOrder(addresses.Count) : 0);
            bufferPosition += 4;

            if (addresses != null)
                foreach (IPAddress ip in addresses)
                {
                    context.Memory.WriteInt32(bufferPosition, IPAddress.HostToNetworkOrder(BitConverter.ToInt32(ip.GetAddressBytes(), 0)));
                    bufferPosition += 4;
                }

            return bufferPosition - originalBufferPosition;
        }

        private string GetGaiStringErrorFromErrorCode(GaiError errorCode)
        {
            if (errorCode > GaiError.Max) errorCode = GaiError.Max;

            switch (errorCode)
            {
                case GaiError.AddressFamily:
                    return "Address family for hostname not supported";
                case GaiError.Again:
                    return "Temporary failure in name resolution";
                case GaiError.BadFlags:
                    return "Invalid value for ai_flags";
                case GaiError.Fail:
                    return "Non-recoverable failure in name resolution";
                case GaiError.Family:
                    return "ai_family not supported";
                case GaiError.Memory:
                    return "Memory allocation failure";
                case GaiError.NoData:
                    return "No address associated with hostname";
                case GaiError.NoName:
                    return "hostname nor servname provided, or not known";
                case GaiError.Service:
                    return "servname not supported for ai_socktype";
                case GaiError.SocketType:
                    return "ai_socktype not supported";
                case GaiError.System:
                    return "System error returned in errno";
                case GaiError.BadHints:
                    return "Invalid value for hints";
                case GaiError.Protocol:
                    return "Resolved protocol is unknown";
                case GaiError.Overflow:
                    return "Argument buffer overflow";
                case GaiError.Max:
                    return "Unknown error";
                default:
                    return "Success";
            }
        }

        private string GetHostStringErrorFromErrorCode(NetDBError errorCode)
        {
            if (errorCode <= NetDBError.Internal) return "Resolver internal error";

            switch (errorCode)
            {
                case NetDBError.Success:
                    return "Resolver Error 0 (no error)";
                case NetDBError.HostNotFound:
                    return "Unknown host";
                case NetDBError.TryAgain:
                    return "Host name lookup failure";
                case NetDBError.NoRecovery:
                    return "Unknown server error";
                case NetDBError.NoData:
                    return "No address associated with name";
                default:
                    return "Unknown resolver error";
            }
        }

        private List<IPAddress> GetIpv4Addresses(IPHostEntry hostEntry)
        {
            List<IPAddress> result = new List<IPAddress>();
            foreach (IPAddress ip in hostEntry.AddressList)
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                    result.Add(ip);
            return result;
        }

        // SetDnsAddressesPrivate(u32, buffer<unknown, 5, 0>)
        public long SetDnsAddressesPrivate(ServiceCtx context)
        {
            uint unknown0       = context.RequestData.ReadUInt32();
            long bufferPosition = context.Request.SendBuff[0].Position;
            long bufferSize     = context.Request.SendBuff[0].Size;

            // TODO: This is stubbed in 2.0.0+, reverse 1.0.0 version for the sake completeness.
            Logger.PrintStub(LogClass.ServiceSfdnsres, $"Stubbed. Unknown0: {unknown0}");

            return MakeError(ErrorModule.Os, 1023);
        }

        // GetDnsAddressPrivate(u32) -> buffer<unknown, 6, 0>
        public long GetDnsAddressesPrivate(ServiceCtx context)
        {
            uint unknown0 = context.RequestData.ReadUInt32();

            // TODO: This is stubbed in 2.0.0+, reverse 1.0.0 version for the sake completeness.
            Logger.PrintStub(LogClass.ServiceSfdnsres, $"Stubbed. Unknown0: {unknown0}");

            return MakeError(ErrorModule.Os, 1023);
        }

        // GetHostByName(u8, u32, u64, pid, buffer<unknown, 5, 0>) -> (u32, u32, u32, buffer<unknown, 6, 0>)
        public long GetHostByName(ServiceCtx context)
        {
            byte[] rawName = context.Memory.ReadBytes(context.Request.SendBuff[0].Position, context.Request.SendBuff[0].Size);
            string name    = Encoding.ASCII.GetString(rawName).TrimEnd('\0');

            // TODO: use params
            bool  enableNsdResolve = context.RequestData.ReadInt32() == 1;
            int   timeOut          = context.RequestData.ReadInt32();
            ulong pidPlaceholder   = context.RequestData.ReadUInt64();

            IPHostEntry hostEntry = null;

            NetDBError netDbErrorCode = NetDBError.Success;
            GaiError   errno          = GaiError.Overflow;
            long       serializedSize = 0;

            if (name.Length <= 255)
                try
                {
                    hostEntry = Dns.GetHostEntry(name);
                }
                catch (SocketException exception)
                {
                    netDbErrorCode = NetDBError.Internal;

                    if (exception.ErrorCode == 11001)
                    {
                        netDbErrorCode = NetDBError.HostNotFound;
                        errno          = GaiError.NoData;
                    }
                    else if (exception.ErrorCode == 11002)
                    {
                        netDbErrorCode = NetDBError.TryAgain;
                    }
                    else if (exception.ErrorCode == 11003)
                    {
                        netDbErrorCode = NetDBError.NoRecovery;
                    }
                    else if (exception.ErrorCode == 11004)
                    {
                        netDbErrorCode = NetDBError.NoData;
                    }
                    else if (exception.ErrorCode == 10060)
                    {
                        errno = GaiError.Again;
                    }
                }
            else
                netDbErrorCode = NetDBError.HostNotFound;

            if (hostEntry != null)
            {
                errno = GaiError.Success;

                List<IPAddress> addresses = GetIpv4Addresses(hostEntry);

                if (addresses.Count == 0)
                {
                    errno          = GaiError.NoData;
                    netDbErrorCode = NetDBError.NoAddress;
                }
                else
                {
                    serializedSize = SerializeHostEnt(context, hostEntry, addresses);
                }
            }

            context.ResponseData.Write((int)netDbErrorCode);
            context.ResponseData.Write((int)errno);
            context.ResponseData.Write(serializedSize);

            return 0;
        }

        // GetHostByAddr(u32, u32, u32, u64, pid, buffer<unknown, 5, 0>) -> (u32, u32, u32, buffer<unknown, 6, 0>)
        public long GetHostByAddress(ServiceCtx context)
        {
            byte[] rawIp = context.Memory.ReadBytes(context.Request.SendBuff[0].Position, context.Request.SendBuff[0].Size);

            // TODO: use params
            uint  socketLength   = context.RequestData.ReadUInt32();
            uint  type           = context.RequestData.ReadUInt32();
            int   timeOut        = context.RequestData.ReadInt32();
            ulong pidPlaceholder = context.RequestData.ReadUInt64();

            IPHostEntry hostEntry = null;

            NetDBError netDbErrorCode = NetDBError.Success;
            GaiError   errno          = GaiError.AddressFamily;
            long       serializedSize = 0;

            if (rawIp.Length == 4)
                try
                {
                    IPAddress address = new IPAddress(rawIp);

                    hostEntry = Dns.GetHostEntry(address);
                }
                catch (SocketException exception)
                {
                    netDbErrorCode = NetDBError.Internal;
                    if (exception.ErrorCode == 11001)
                    {
                        netDbErrorCode = NetDBError.HostNotFound;
                        errno          = GaiError.NoData;
                    }
                    else if (exception.ErrorCode == 11002)
                    {
                        netDbErrorCode = NetDBError.TryAgain;
                    }
                    else if (exception.ErrorCode == 11003)
                    {
                        netDbErrorCode = NetDBError.NoRecovery;
                    }
                    else if (exception.ErrorCode == 11004)
                    {
                        netDbErrorCode = NetDBError.NoData;
                    }
                    else if (exception.ErrorCode == 10060)
                    {
                        errno = GaiError.Again;
                    }
                }
            else
                netDbErrorCode = NetDBError.NoAddress;

            if (hostEntry != null)
            {
                errno = GaiError.Success;
                serializedSize = SerializeHostEnt(context, hostEntry, GetIpv4Addresses(hostEntry));
            }

            context.ResponseData.Write((int)netDbErrorCode);
            context.ResponseData.Write((int)errno);
            context.ResponseData.Write(serializedSize);

            return 0;
        }

        // GetHostStringError(u32) -> buffer<unknown, 6, 0>
        public long GetHostStringError(ServiceCtx context)
        {
            long       resultCode  = MakeError(ErrorModule.Os, 1023);
            NetDBError errorCode   = (NetDBError)context.RequestData.ReadInt32();
            string     errorString = GetHostStringErrorFromErrorCode(errorCode);

            if (errorString.Length + 1 <= context.Request.ReceiveBuff[0].Size)
            {
                resultCode = 0;
                context.Memory.WriteBytes(context.Request.ReceiveBuff[0].Position, Encoding.ASCII.GetBytes(errorString + '\0'));
            }

            return resultCode;
        }

        // GetGaiStringError(u32) -> buffer<unknown, 6, 0>
        public long GetGaiStringError(ServiceCtx context)
        {
            long     resultCode  = MakeError(ErrorModule.Os, 1023);
            GaiError errorCode   = (GaiError)context.RequestData.ReadInt32();
            string   errorString = GetGaiStringErrorFromErrorCode(errorCode);

            if (errorString.Length + 1 <= context.Request.ReceiveBuff[0].Size)
            {
                resultCode = 0;
                context.Memory.WriteBytes(context.Request.ReceiveBuff[0].Position, Encoding.ASCII.GetBytes(errorString + '\0'));
            }

            return resultCode;
        }

        // RequestCancelHandle(u64, pid) -> u32
        public long RequestCancelHandle(ServiceCtx context)
        {
            ulong unknown0 = context.RequestData.ReadUInt64();

            context.ResponseData.Write(0);

            Logger.PrintStub(LogClass.ServiceSfdnsres, $"Stubbed. Unknown0: {unknown0}");

            return 0;
        }

        // CancelSocketCall(u32, u64, pid)
        public long CancelSocketCall(ServiceCtx context)
        {
            uint  unknown0 = context.RequestData.ReadUInt32();
            ulong unknown1 = context.RequestData.ReadUInt64();

            Logger.PrintStub(LogClass.ServiceSfdnsres, $"Stubbed. Unknown0: {unknown0} - " +
                                                       $"Unknown1: {unknown1}");

            return 0;
        }

        // ClearDnsAddresses(u32)
        public long ClearDnsAddresses(ServiceCtx context)
        {
            uint unknown0 = context.RequestData.ReadUInt32();

            Logger.PrintStub(LogClass.ServiceSfdnsres, $"Stubbed. Unknown0: {unknown0}");

            return 0;
        }
    }
}
