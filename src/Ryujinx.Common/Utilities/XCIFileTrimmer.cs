using Ryujinx.Common.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Ryujinx.Common.Utilities
{
    internal static class Performance
    {
        internal static TimeSpan Measure(Action action)
        {
            var sw = new Stopwatch();
            sw.Start();

            try
            {
                action();
            }
            finally
            {
                sw.Stop();
            }

            return sw.Elapsed;
        }
    }

    public class XCIFileTrimmer
    {
        private const long BytesInAMegabyte = 1024 * 1024;
        private const int BufferSize = 8 * (int)BytesInAMegabyte;

        private const long CartSizeMBinFormattedGB = 952;
        private const int CartKeyAreaSize = 0x1000;
        private const byte PaddingByte = 0xFF;
        private const int HeaderFilePos = 0x100;
        private const int CartSizeFilePos = 0x10D;
        private const int DataSizeFilePos = 0x118;
        private const string HeaderMagicValue = "HEAD";

        private static readonly Dictionary<byte, long> _cartSizesGB = new()
        {
            { 0xFA, 1 },
            { 0xF8, 2 },
            { 0xF0, 4 },
            { 0xE0, 8 },
            { 0xE1, 16 },
            { 0xE2, 32 }
        };

        private static long RecordsToByte(long records)
        {
            return 512 + (records * 512);
        }

        public static bool CanTrim(string filename, ILog log = null)
        {
            if (System.IO.Path.GetExtension(filename).ToUpperInvariant() == ".XCI")
            {
                var trimmer = new XCIFileTrimmer(filename, log);
                return trimmer.CanBeTrimmed;
            }

            return false;
        }

        private ILog _log;
        private string _filename;
        private FileStream _fileStream;
        private BinaryReader _binaryReader;
        private long _offsetB, _dataSizeB, _cartSizeB, _fileSizeB;
        private bool _fileOK = true;
        private bool _freeSpaceChecked = false;
        private bool _freeSpaceValid = false;

        public enum OperationOutcome
        {
            InvalidXCIFile,
            NoTrimNecessary,
            NoUntrimPossible,
            FreeSpaceCheckFailed,
            FileIOWriteError,
            ReadOnlyFileCannotFix,
            FileSizeChanged,
            Successful
        }

        public enum LogType
        {
            Info,
            Warn,
            Error,
            Progress
        }

        public interface ILog
        {
            public void Write(LogType logType, string text);
            public void Progress(long current, long total, string text, bool complete);
        }

        public bool FileOK => _fileOK;
        public bool Trimmed => _fileOK && FileSizeB < UntrimmedFileSizeB;
        public bool ContainsKeyArea => _offsetB != 0;
        public bool CanBeTrimmed => _fileOK && FileSizeB > TrimmedFileSizeB;
        public bool CanBeUntrimmed => _fileOK && FileSizeB < UntrimmedFileSizeB;
        public bool FreeSpaceChecked => _fileOK && _freeSpaceChecked;
        public bool FreeSpaceValid => _fileOK && _freeSpaceValid;
        public long DataSizeB => _dataSizeB;
        public long CartSizeB => _cartSizeB;
        public long FileSizeB => _fileSizeB;
        public long DiskSpaceSavedB => CartSizeB - FileSizeB;
        public long DiskSpaceSavingsB => CartSizeB - DataSizeB;
        public long TrimmedFileSizeB => _offsetB + _dataSizeB;
        public long UntrimmedFileSizeB => _offsetB + _cartSizeB;

        public ILog Log
        {
            get => _log;
            set => _log = value;
        }

        public String Filename
        {
            get => this._filename;
            set
            {
                this._filename = value;
                Reset();
            }
        }

        public long Pos
        {
            get => this._fileStream.Position;
            set => this._fileStream.Position = value;
        }

        public XCIFileTrimmer(string path, ILog log = null)
        {
            this.Log = log;
            this.Filename = path;
            ReadHeader();
        }

        public void CheckFreeSpace()
        {
            if (this.FreeSpaceChecked)
                return;

            try
            {
                if (this.CanBeTrimmed)
                {
                    this._freeSpaceValid = false;

                    OpenReaders();

                    try
                    {
                        this.Pos = this.TrimmedFileSizeB;
                        var buffer = new byte[BufferSize];
                        var readSizeB = this.FileSizeB - this.TrimmedFileSizeB;
                        var reads = readSizeB / XCIFileTrimmer.BufferSize;
                        long read = 0;

                        var time = Performance.Measure(() =>
                        {
                            try
                            {
                                while (true)
                                {
                                    var bytes = _fileStream.Read(buffer, 0, XCIFileTrimmer.BufferSize);
                                    if (bytes == 0)
                                        break;

                                    if (buffer.Take(bytes).AsParallel().Any(b => b != XCIFileTrimmer.PaddingByte))
                                    {
                                        Log?.Write(LogType.Warn, "Free space is NOT valid");
                                        return;
                                    }
                                    Log?.Progress(read, reads, "Verifying file can be trimmed", false);
                                    read++;
                                }
                            }
                            finally
                            {
                                Log?.Progress(reads, reads, "Verifying file can be trimmed", true);
                            }
                        });

                        if (time.TotalSeconds > 0)
                        {
                            Log?.Write(LogType.Info, $"Checked at {readSizeB / (double)XCIFileTrimmer.BytesInAMegabyte / time.TotalSeconds:N} Mb/sec");
                        }

                        Log?.Write(LogType.Info, "Free space is valid");
                        this._freeSpaceValid = true;
                    }
                    finally
                    {
                        CloseReaders();
                    }

                }
                else
                {
                    Log?.Write(LogType.Warn, "There is no free space to check.");
                    this._freeSpaceValid = false;
                }
            }
            finally
            {
                this._freeSpaceChecked = true;
            }
        }

        protected void Reset()
        {
            this._freeSpaceChecked = false;
            this._freeSpaceValid = false;
            ReadHeader();
        }

        public OperationOutcome Trim()
        {
            if (!this.FileOK)
            {
                return OperationOutcome.InvalidXCIFile;
            }

            if (!this.CanBeTrimmed)
            {
                return OperationOutcome.NoTrimNecessary;
            }

            if (!this.FreeSpaceChecked)
            {
                CheckFreeSpace();
            }

            if (!this.FreeSpaceValid)
            {
                return OperationOutcome.FreeSpaceCheckFailed;
            }

            Log?.Write(LogType.Info, "Trimming...");

            try
            {
                var info = new FileInfo(this.Filename);
                if ((info.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                {
                    try
                    {
                        Log?.Write(LogType.Info, "Attempting to remove ReadOnly attribute");
                        File.SetAttributes(this.Filename, info.Attributes & ~FileAttributes.ReadOnly);
                    }
                    catch (Exception e)
                    {
                        Log?.Write(LogType.Error, e.ToString());
                        return OperationOutcome.ReadOnlyFileCannotFix;
                    }
                }

                if (info.Length != this.FileSizeB)
                {
                    Log?.Write(LogType.Error, "File size has changed, cannot safely trim.");
                    return OperationOutcome.FileSizeChanged;
                }

                var outfileStream = new FileStream(_filename, FileMode.Open, FileAccess.Write, FileShare.Write);

                try
                {
                    outfileStream.SetLength(this.TrimmedFileSizeB);
                    return OperationOutcome.Successful;
                }
                finally
                {
                    outfileStream.Close();
                    Reset();
                }
            }
            catch (Exception e)
            {
                Log?.Write(LogType.Error, e.ToString());
                return OperationOutcome.FileIOWriteError;
            }
        }

        public OperationOutcome Untrim()
        {
            if (!this.FileOK)
            {
                return OperationOutcome.InvalidXCIFile;
            }

            if (!this.CanBeUntrimmed)
            {
                return OperationOutcome.NoUntrimPossible;
            }

            try
            {
                Log?.Write(LogType.Info, "Untrimming...");

                var info = new FileInfo(this.Filename);
                if ((info.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                {
                    try
                    {
                        Log?.Write(LogType.Info, "Attempting to remove ReadOnly attribute");
                        File.SetAttributes(this.Filename, info.Attributes & ~FileAttributes.ReadOnly);
                    }
                    catch (Exception e)
                    {
                        Log?.Write(LogType.Error, e.ToString());
                        return OperationOutcome.ReadOnlyFileCannotFix;
                    }
                }

                if (info.Length != this.FileSizeB)
                {
                    Log?.Write(LogType.Error, "File size has changed, cannot safely untrim.");
                    return OperationOutcome.FileSizeChanged;
                }

                var outfileStream = new FileStream(this._filename, FileMode.Append, FileAccess.Write, FileShare.Write);
                var buffer = new byte[BufferSize];
                Array.Fill<byte>(buffer, XCIFileTrimmer.PaddingByte);
                var bytesToWriteB = this.UntrimmedFileSizeB - this.FileSizeB;
                var bytesLeftToWriteB = bytesToWriteB;
                var writes = bytesLeftToWriteB / XCIFileTrimmer.BufferSize;
                var write = 0;

                try
                {
                    var time = Performance.Measure(() =>
                    {
                        try
                        {
                            while (bytesLeftToWriteB > 0)
                            {
                                var bytesToWrite = Math.Min(XCIFileTrimmer.BufferSize, bytesLeftToWriteB);
                                outfileStream.Write(buffer, 0, (int)bytesToWrite);
                                bytesLeftToWriteB -= bytesToWrite;
                                Log?.Progress(write, writes, "Writing padding data...", false);
                                write++;
                            }
                        }
                        finally
                        {
                            Log?.Progress(write, writes, "Writing padding data...", true);
                        }
                    });

                    if (time.TotalSeconds > 0)
                    {
                        Log?.Write(LogType.Info, $"Wrote at {bytesToWriteB / (double)XCIFileTrimmer.BytesInAMegabyte / time.TotalSeconds:N} Mb/sec");
                    }

                    return OperationOutcome.Successful;
                }
                finally
                {
                    outfileStream.Close();
                    Reset();
                }
            }
            catch (Exception e)
            {
                Log?.Write(LogType.Error, e.ToString());
                return OperationOutcome.FileIOWriteError;
            }
        }

        protected void OpenReaders()
        {
            if (_binaryReader == null)
            {
                this._fileStream = new FileStream(this._filename, FileMode.Open, FileAccess.Read, FileShare.Read);
                this._binaryReader = new BinaryReader(this._fileStream);
            }
        }

        protected void CloseReaders()
        {
            if (this._binaryReader != null && this._binaryReader.BaseStream != null)
                this._binaryReader.Close();
            this._binaryReader = null;
            this._fileStream = null;
            GC.Collect();
        }

        private void ReadHeader()
        {
            try
            {
                OpenReaders();

                try
                {
                    // Attempt without key area
                    var success = CheckAndReadHeader(false);

                    if (!success)
                    {
                        // Attempt with key area
                        success = CheckAndReadHeader(true);
                    }

                    this._fileOK = success;
                }
                finally
                {
                    CloseReaders();
                }
            }
            catch (Exception ex)
            {
                Log?.Write(LogType.Error, ex.Message);
                this._fileOK = false;
                this._dataSizeB = 0;
                this._cartSizeB = 0;
                this._fileSizeB = 0;
                this._offsetB = 0;
            }
        }

        private bool CheckAndReadHeader(bool assumeKeyArea)
        {
            // Read file size
            this._fileSizeB = _fileStream.Length;
            if (_fileSizeB < 32 * 1024)
            {
                Log?.Write(LogType.Error, "The source file doesn't look like an XCI file as the data size is too small");
                return false;
            }

            // Setup offset
            this._offsetB = (long)(assumeKeyArea ? XCIFileTrimmer.CartKeyAreaSize : 0);

            // Check header
            this.Pos = _offsetB + XCIFileTrimmer.HeaderFilePos;
            var head = System.Text.Encoding.ASCII.GetString(_binaryReader.ReadBytes(4));
            if (head != XCIFileTrimmer.HeaderMagicValue)
            {
                if (!assumeKeyArea)
                {
                    Log?.Write(LogType.Warn, $"Incorrect header found, file mat contain a key area...");
                }
                else
                {
                    Log?.Write(LogType.Error, "The source file doesn't look like an XCI file as the header is corrupted");
                }

                return false;
            }

            // Read Cart Size
            this.Pos = _offsetB + XCIFileTrimmer.CartSizeFilePos;
            var cartSizeId = _binaryReader.ReadByte();
            if (!_cartSizesGB.TryGetValue(cartSizeId, out long cartSizeNGB))
            {
                Log?.Write(LogType.Error, "The source file doesn't look like an XCI file as the Cartridge Size is incorrect");
                return false;
            }
            this._cartSizeB = cartSizeNGB * XCIFileTrimmer.CartSizeMBinFormattedGB * XCIFileTrimmer.BytesInAMegabyte;

            // Read data size
            this.Pos = _offsetB + XCIFileTrimmer.DataSizeFilePos;
            var records = (long)BitConverter.ToUInt32(_binaryReader.ReadBytes(4), 0);
            this._dataSizeB = RecordsToByte(records);

            return true;
        }
    }
}
