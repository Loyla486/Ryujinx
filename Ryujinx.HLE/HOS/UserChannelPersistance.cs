﻿using Ryujinx.HLE.HOS.Services.Am.AppletOE.ApplicationProxyService.ApplicationProxy.Types;
using System;
using System.Collections.Generic;

namespace Ryujinx.HLE.HOS
{
    public class UserChannelPersistance
    {
        private Stack<byte[]> _userChannelStorages;
        public int PreviousIndex { get; private set; }
        public int Index { get; private set; }
        public ProgramSpecifyKind Kind { get; private set; }

        public UserChannelPersistance()
        {
            _userChannelStorages = new Stack<byte[]>();
            Kind = ProgramSpecifyKind.ExecuteProgram;
            PreviousIndex = -1;
            Index = 0;
        }

        public void Clear()
        {
            _userChannelStorages.Clear();
        }

        public void Push(byte[] data)
        {
            _userChannelStorages.Push(data);
        }

        public byte[] Pop()
        {
            return _userChannelStorages.Pop();
        }

        public bool IsEmpty => _userChannelStorages.Count == 0;

        public void ExecuteProgram(ProgramSpecifyKind kind, ulong value)
        {
            Kind = kind;
            PreviousIndex = Index;

            switch (kind)
            {
                case ProgramSpecifyKind.ExecuteProgram:
                    Index = (int)value;
                    break;
                case ProgramSpecifyKind.RestartProgram:
                    break;
                default:
                    throw new NotImplementedException($"{kind} not implemented");
            }
        }
    }
}
