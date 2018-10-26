using ChocolArm64.Memory;
using Ryujinx.Common.Logging;
using Ryujinx.HLE.HOS.Ipc;
using Ryujinx.HLE.HOS.SystemState;
using Ryujinx.HLE.Utilities;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace Ryujinx.HLE.HOS.Services.Acc
{
    internal class IProfile : IpcService
    {
        private Dictionary<int, ServiceProcessRequest> _mCommands;

        public override IReadOnlyDictionary<int, ServiceProcessRequest> Commands => _mCommands;

        private UserProfile _profile;

        private Stream _profilePictureStream;

        public IProfile(UserProfile profile)
        {
            _mCommands = new Dictionary<int, ServiceProcessRequest>()
            {
                { 0,  Get          },
                { 1,  GetBase      },
                { 10, GetImageSize },
                { 11, LoadImage    },
            };

            this._profile = profile;

            _profilePictureStream = Assembly.GetCallingAssembly().GetManifestResourceStream("Ryujinx.HLE.RyujinxProfileImage.jpg");
        }

        public long Get(ServiceCtx context)
        {
            Logger.PrintStub(LogClass.ServiceAcc, "Stubbed.");

            long position = context.Request.ReceiveBuff[0].Position;

            AMemoryHelper.FillWithZeros(context.Memory, position, 0x80);

            context.Memory.WriteInt32(position, 0);
            context.Memory.WriteInt32(position + 4, 1);
            context.Memory.WriteInt64(position + 8, 1);

            return GetBase(context);
        }

        public long GetBase(ServiceCtx context)
        {
            _profile.Uuid.Write(context.ResponseData);

            context.ResponseData.Write(_profile.LastModifiedTimestamp);

            byte[] username = StringUtils.GetFixedLengthBytes(_profile.Name, 0x20, Encoding.UTF8);

            context.ResponseData.Write(username);

            return 0;
        }

        private long LoadImage(ServiceCtx context)
        {
            long bufferPosition = context.Request.ReceiveBuff[0].Position;
            long bufferLen      = context.Request.ReceiveBuff[0].Size;

            byte[] profilePictureData = new byte[bufferLen];

            _profilePictureStream.Read(profilePictureData, 0, profilePictureData.Length);

            context.Memory.WriteBytes(bufferPosition, profilePictureData);

            context.ResponseData.Write(_profilePictureStream.Length);

            return 0;
        }

        private long GetImageSize(ServiceCtx context)
        {
            context.ResponseData.Write(_profilePictureStream.Length);

            return 0;
        }
    }
}