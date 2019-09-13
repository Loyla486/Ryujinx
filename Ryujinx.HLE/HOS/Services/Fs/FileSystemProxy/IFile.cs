using LibHac;
using LibHac.Fs;
using System;

namespace Ryujinx.HLE.HOS.Services.Fs.FileSystemProxy
{
    class IFile : IpcService, IDisposable
    {
        private LibHac.Fs.IFile _baseFile;

        public IFile(LibHac.Fs.IFile baseFile)
        {
            _baseFile = baseFile;
        }

        [Command(0)]
        // Read(u32 readOption, u64 offset, u64 size) -> (u64 out_size, buffer<u8, 0x46, 0> out_buf)
        public ResultCode Read(ServiceCtx context)
        {
            long position = context.Request.ReceiveBuff[0].Position;

            ReadOption readOption = (ReadOption)context.RequestData.ReadInt32();
            context.RequestData.BaseStream.Position += 4;

            long offset = context.RequestData.ReadInt64();
            long size   = context.RequestData.ReadInt64();

            byte[] data = new byte[size];
            int readSize;

            try
            {
                readSize = _baseFile.Read(data, offset, readOption);
            }
            catch (HorizonResultException ex)
            {
                return (ResultCode)ex.ResultValue.Value;
            }

            context.Memory.WriteBytes(position, data);

            context.ResponseData.Write((long)readSize);

            return ResultCode.Success;
        }

        [Command(1)]
        // Write(u32 writeOption, u64 offset, u64 size, buffer<u8, 0x45, 0>)
        public ResultCode Write(ServiceCtx context)
        {
            long position = context.Request.SendBuff[0].Position;

            WriteOption writeOption = (WriteOption)context.RequestData.ReadInt32();
            context.RequestData.BaseStream.Position += 4;

            long offset = context.RequestData.ReadInt64();
            long size   = context.RequestData.ReadInt64();

            byte[] data = context.Memory.ReadBytes(position, size);

            try
            {
                _baseFile.Write(data, offset, writeOption);
            }
            catch (HorizonResultException ex)
            {
                return (ResultCode)ex.ResultValue.Value;
            }

            return ResultCode.Success;
        }

        [Command(2)]
        // Flush()
        public ResultCode Flush(ServiceCtx context)
        {
            try
            {
                _baseFile.Flush();
            }
            catch (HorizonResultException ex)
            {
                return (ResultCode)ex.ResultValue.Value;
            }

            return ResultCode.Success;
        }

        [Command(3)]
        // SetSize(u64 size)
        public ResultCode SetSize(ServiceCtx context)
        {
            try
            {
                long size = context.RequestData.ReadInt64();

                _baseFile.SetSize(size);
            }
            catch (HorizonResultException ex)
            {
                return (ResultCode)ex.ResultValue.Value;
            }

            return ResultCode.Success;
        }

        [Command(4)]
        // GetSize() -> u64 fileSize
        public ResultCode GetSize(ServiceCtx context)
        {
            try
            {
                context.ResponseData.Write(_baseFile.GetSize());
            }
            catch (HorizonResultException ex)
            {
                return (ResultCode)ex.ResultValue.Value;
            }

            return ResultCode.Success;
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _baseFile?.Dispose();
            }
        }
    }
}