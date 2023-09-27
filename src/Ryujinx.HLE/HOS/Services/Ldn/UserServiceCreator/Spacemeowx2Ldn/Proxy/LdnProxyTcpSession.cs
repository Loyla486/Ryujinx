﻿using Ryujinx.Common.Logging;
using Ryujinx.HLE.HOS.Services.Ldn.Types;
using System.Net;
using System.Net.Sockets;

namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.Spacemeowx2Ldn.Proxy
{
    internal class LdnProxyTcpSession : NetCoreServer.TcpSession
    {
        private LdnProxyTcpServer _parent;
        private LanProtocol _protocol;

        internal int nodeId;

        internal NodeInfo nodeInfo;

        private byte[] _buffer;
        private int _bufferEnd;

        public LdnProxyTcpSession(LdnProxyTcpServer server, LanProtocol protocol) : base(server)
        {
            _parent = server;
            _protocol = protocol;
            _protocol.Connect += OnConnect;
            _buffer = new byte[LanProtocol.BufferSize];
            OptionReceiveBufferSize = LanProtocol.BufferSize;
            OptionSendBufferSize = LanProtocol.BufferSize;
            Logger.Info?.PrintMsg(LogClass.ServiceLdn, $"LdnProxyTCPSession created!");
        }

        public void OverrideInfo()
        {
            Logger.Info?.PrintMsg(LogClass.ServiceLdn, $"LdnProxyTCPSession overriding info!");
            nodeInfo.NodeId = (byte)nodeId;
            nodeInfo.IsConnected = (byte)(IsConnected ? 1 : 0);
        }

        protected override void OnConnected()
        {
            Logger.Info?.PrintMsg(LogClass.ServiceLdn, $"LdnProxyTCPSession connected!");
            _protocol.InvokeAccept(this);
        }

        protected override void OnDisconnected()
        {
            Logger.Info?.PrintMsg(LogClass.ServiceLdn, $"LdnProxyTCPSession connected!");
            _protocol.InvokeDisconnectStation(this);
        }

        protected override void OnReceived(byte[] buffer, long offset, long size)
        {
            // Done via LANDiscovery::loopPoll()
            _protocol.Read(ref _buffer, ref _bufferEnd, buffer, (int)offset, (int)size, this.Socket.RemoteEndPoint);
        }

        protected override void OnError(SocketError error)
        {
            Logger.Error?.PrintMsg(LogClass.ServiceLdn, $"LdnProxyTCPSession caught an error with code {error}");
            Dispose();
        }

        protected override void Dispose(bool disposingManagedResources)
        {
            _protocol.Connect -= OnConnect;
            base.Dispose(disposingManagedResources);
        }

        private void OnConnect(NodeInfo info, EndPoint endPoint)
        {
            try
            {
                if (endPoint.Equals(this.Socket.RemoteEndPoint))
                {
                    nodeInfo = info;
                }
            }
            catch (System.ObjectDisposedException)
            {
                Logger.Error?.PrintMsg(LogClass.ServiceLdn, $"LdnProxyTCPSession was disposed. [IP: {nodeInfo.Ipv4Address}]");
                _protocol.InvokeDisconnectStation(this);
            }
        }
    }
}
