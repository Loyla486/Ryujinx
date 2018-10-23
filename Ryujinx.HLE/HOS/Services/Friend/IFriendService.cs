using Ryujinx.Common.Logging;
using Ryujinx.HLE.HOS.Ipc;
using Ryujinx.HLE.HOS.SystemState;
using Ryujinx.HLE.Utilities;
using System.Collections.Generic;

namespace Ryujinx.HLE.HOS.Services.Friend
{
    class FriendService : IpcService
    {
        private Dictionary<int, ServiceProcessRequest> _mCommands;

        public override IReadOnlyDictionary<int, ServiceProcessRequest> Commands => _mCommands;

        public FriendService()
        {
            _mCommands = new Dictionary<int, ServiceProcessRequest>()
            {
                { 10101, GetFriendList                 },
                { 10601, DeclareCloseOnlinePlaySession },
                { 10610, UpdateUserPresence            }
            };
        }

        // nn::friends::GetFriendListGetFriendListIds(nn::account::Uid, int Unknown0, nn::friends::detail::ipc::SizedFriendFilter, ulong Unknown1) -> int CounterIds,  array<nn::account::NetworkServiceAccountId>
        public long GetFriendList(ServiceCtx context)
        {
            UInt128 uuid = new UInt128(
                context.RequestData.ReadInt64(),
                context.RequestData.ReadInt64());

            int unknown0 = context.RequestData.ReadInt32();

            FriendFilter filter = new FriendFilter()
            {
                PresenceStatus           = (PresenceStatusFilter)context.RequestData.ReadInt32(),
                IsFavoriteOnly           = context.RequestData.ReadBoolean(),
                IsSameAppPresenceOnly    = context.RequestData.ReadBoolean(),
                IsSameAppPlayedOnly      = context.RequestData.ReadBoolean(),
                IsArbitraryAppPlayedOnly = context.RequestData.ReadBoolean(),
                PresenceGroupId          = context.RequestData.ReadInt64()
            };

            long unknown1 = context.RequestData.ReadInt64();

            // There are no friends online, so we return 0 because the nn::account::NetworkServiceAccountId array is empty.
            context.ResponseData.Write(0);

            Logger.PrintStub(LogClass.ServiceFriend, $"Stubbed. UserId: {uuid.ToString()} - " +
                                                     $"Unknown0: {unknown0} - " +
                                                     $"PresenceStatus: {filter.PresenceStatus} - " +
                                                     $"IsFavoriteOnly: {filter.IsFavoriteOnly} - " +
                                                     $"IsSameAppPresenceOnly: {filter.IsSameAppPresenceOnly} - " +
                                                     $"IsSameAppPlayedOnly: {filter.IsSameAppPlayedOnly} - " +
                                                     $"IsArbitraryAppPlayedOnly: {filter.IsArbitraryAppPlayedOnly} - " +
                                                     $"PresenceGroupId: {filter.PresenceGroupId} - " +
                                                     $"Unknown1: {unknown1}");

            return 0;
        }

        // DeclareCloseOnlinePlaySession(nn::account::Uid)
        public long DeclareCloseOnlinePlaySession(ServiceCtx context)
        {
            UInt128 uuid = new UInt128(
                context.RequestData.ReadInt64(),
                context.RequestData.ReadInt64());

            if (context.Device.System.State.TryGetUser(uuid, out UserProfile profile))
            {
                profile.OnlinePlayState = OpenCloseState.Closed;
            }

            Logger.PrintStub(LogClass.ServiceFriend, $"Stubbed. Uuid: {uuid.ToString()} - " +
                                                     $"OnlinePlayState: {profile.OnlinePlayState}");

            return 0;
        }

        // UpdateUserPresence(nn::account::Uid, ulong Unknown0) -> buffer<Unknown1, type: 0x19, size: 0xe0>
        public long UpdateUserPresence(ServiceCtx context)
        {
            UInt128 uuid = new UInt128(
                context.RequestData.ReadInt64(),
                context.RequestData.ReadInt64());

            long unknown0 = context.RequestData.ReadInt64();

            long position = context.Request.PtrBuff[0].Position;
            long size     = context.Request.PtrBuff[0].Size;

            //Todo: Write the buffer content.

            Logger.PrintStub(LogClass.ServiceFriend, $"Stubbed. Uuid: {uuid.ToString()} - " +
                                                     $"Unknown0: {unknown0}");

            return 0;
        }
    }
}
