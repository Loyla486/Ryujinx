﻿using Ryujinx.HLE.HOS;
using System.IO;

using static Ryujinx.HLE.FileSystem.VirtualFileSystem;

namespace Ryujinx.HLE.FileSystem
{
    internal static class SaveHelper
    {
        public static string GetSavePath(SaveInfo saveMetaData, ServiceCtx context)
        {
            string baseSavePath   = NandPath;
            long   currentTitleId = saveMetaData.TitleId;

            switch (saveMetaData.SaveSpaceId)
            {
                case SaveSpaceId.NandUser:
                    baseSavePath = UserNandPath;
                    break;
                case SaveSpaceId.NandSystem:
                    baseSavePath = SystemNandPath;
                    break;
                case SaveSpaceId.SdCard:
                    baseSavePath = Path.Combine(SdCardPath, "Nintendo");
                    break;
            }

            baseSavePath = Path.Combine(baseSavePath, "save");

            if (saveMetaData.TitleId == 0 && saveMetaData.SaveDataType == SaveDataType.SaveData)
            {
                if (context.Process.MetaData != null)
                {
                    currentTitleId = context.Process.MetaData.Aci0.TitleId;
                }
            }

            string saveAccount = saveMetaData.UserId.IsZero() ? "savecommon" : saveMetaData.UserId.ToString();

            string savePath = Path.Combine(baseSavePath,
                saveMetaData.SaveId.ToString("x16"),
                saveAccount,
                saveMetaData.SaveDataType == SaveDataType.SaveData ? currentTitleId.ToString("x16") : string.Empty);

            return savePath;
        }
    }
}
