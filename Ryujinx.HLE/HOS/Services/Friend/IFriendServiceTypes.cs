using Ryujinx.HLE.Utilities;
using System.Runtime.InteropServices;

namespace Ryujinx.HLE.HOS.Services.Friend
{
    enum PresenceStatusFilter : uint
    {
        None,
        Online,
        OnlinePlay,
        OnlineOrOnlinePlay
    }

    enum PresenceStatus : uint
    {
        Offline,
        Online,
        OnlinePlay,
    }

    [StructLayout(LayoutKind.Sequential)]
    struct FriendFilter
    {
        public PresenceStatusFilter PresenceStatus;

        [MarshalAs(UnmanagedType.I1)]
        public bool IsFavoriteOnly;

        [MarshalAs(UnmanagedType.I1)]
        public bool IsSameAppPresenceOnly;

        [MarshalAs(UnmanagedType.I1)]
        public bool IsSameAppPlayedOnly;

        [MarshalAs(UnmanagedType.I1)]
        public bool IsArbitraryAppPlayedOnly;

        public long PresenceGroupId;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 0x8)]
    struct UserPresence
    {
        public UInt128        UserId;
        public long           LastTimeOnlineTimestamp;
        public PresenceStatus Status;

        [MarshalAs(UnmanagedType.I1)]
        public bool SamePresenceGroupApplication;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x3)]
        char[] unknown;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0xC0)]
        public char[] AppKeyValueStorage;


        public override string ToString()
        {
            return $"UserPresence {{ UserId: {UserId}, LastTimeOnlineTimestamp: {LastTimeOnlineTimestamp}, Status: {Status}, AppKeyValueStorage: {AppKeyValueStorage} }}";
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 0x8, Size = 0x200)]
    struct Friend
    {
        public UInt128 UserId;
        public long    NetworkUserId;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x21)]
        public char[] Nickname;

        public UserPresence presence;

        [MarshalAs(UnmanagedType.I1)]
        public bool IsFavourite;

        [MarshalAs(UnmanagedType.I1)]
        public bool IsNew;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x6)]
        char[] unknown;

        [MarshalAs(UnmanagedType.I1)]
        public bool IsValid;
    }
}
