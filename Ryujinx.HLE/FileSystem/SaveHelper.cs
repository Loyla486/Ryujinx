﻿using Ryujinx.HLE.HOS;
using System.IO;

using static Ryujinx.HLE.FileSystem.VirtualFileSystem;

namespace Ryujinx.HLE.FileSystem
{
    static class SaveHelper
    {
        public static string GetSavePath(SaveInfo SaveMetaData, ServiceCtx Context)
        {
            string BaseSavePath   = NandPath;
            long   CurrentTitleId = SaveMetaData.TitleId;

            switch (SaveMetaData.SaveSpaceId)
            {
                case SaveSpaceId.NandUser:
                    BaseSavePath = UserNandPath;
                    break;
                case SaveSpaceId.NandSystem:
                    BaseSavePath = SystemNandPath;
                    break;
                case SaveSpaceId.SdCard:
                    BaseSavePath = Path.Combine(SdCardPath, "Nintendo");
                    break;
            }

            BaseSavePath = Path.Combine(BaseSavePath, "save");

            if (SaveMetaData.TitleId == 0 && SaveMetaData.SaveDataType == SaveDataType.SaveData)
            {
                if (Context.Process.MetaData != null)
                {
                    CurrentTitleId = Context.Process.MetaData.ACI0.TitleId;
                }
            }

            string SavePath = Path.Combine(BaseSavePath,
                SaveMetaData.SaveId.ToString("x16"),
                SaveMetaData.UserId.ToString(),
                SaveMetaData.SaveDataType == SaveDataType.SaveData ? CurrentTitleId.ToString("x16") : string.Empty);

            return SavePath;
        }
    }
}
