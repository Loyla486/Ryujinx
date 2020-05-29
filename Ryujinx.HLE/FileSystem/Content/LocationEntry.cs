﻿using LibHac.FsSystem.NcaUtils;

namespace Ryujinx.HLE.FileSystem.Content
{
    public struct LocationEntry
    {
        public string         ContentPath { get; private set; }
        public int            Flag        { get; private set; }
        public long           TitleId     { get; private set; }
        public NcaContentType ContentType { get; private set; }

        public LocationEntry(string contentPath, int flag, long titleId, NcaContentType contentType)
        {
            ContentPath = contentPath;
            Flag        = flag;
            TitleId     = titleId;
            ContentType = contentType;
        }

        public void SetFlag(int flag)
        {
            Flag = flag;
        }
    }
}
