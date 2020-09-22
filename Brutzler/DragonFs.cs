using Crc32;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DragonFS
{
    class Utils
    {
        public static void WriteArrayBigEndian(byte[] destination, int offset, UInt32 value)
        {
            destination[offset + 0] = (byte)(value >> 24);
            destination[offset + 1] = (byte)(value >> 16);
            destination[offset + 2] = (byte)(value >> 8);
            destination[offset + 3] = (byte)(value >> 0);
        }

        public static UInt32 ReadArrayBigEndianUint32(byte[] source, int offset)
        {
            UInt32 res = 0;
            res += (UInt32)source[offset + 0] << 24;
            res += (UInt32)source[offset + 1] << 16;
            res += (UInt32)source[offset + 2] << 8;
            res += (UInt32)source[offset + 3] << 0;
            return res;
        }
    }

    public class DragonFs
    {
        const UInt32 ROOT_FLAGS     = 0xFFFFFFFF;
        const UInt32 ROOT_NEXTENTRY = 0xDEADBEEF;
        const string ROOT_PATH   = "DragonFS 1.0";

        List<DfsSector> _SectorList = new List<DfsSector>();
        UInt32 _NextOffset = 0;
        DfsDirectoryEntry _RootDirectory;
        DfsDirectoryEntry _CurrentDirectory;

        public DragonFs()
        {
            DfsDirectoryEntry fsRoot = new DfsDirectoryEntry(NewSector())
            {
                Flags = ROOT_FLAGS,
                NextEntry = ROOT_NEXTENTRY,
                Path = ROOT_PATH
            };

            // build a dummy root with a valid file pointer
            // the real first sectors file pointer is always 0
            // but this one can be modified
            _RootDirectory = new DfsDirectoryEntry(new DfsSector(0));
            _RootDirectory.Flags = DfsDirectoryEntry.FLAG_DIR;
            _CurrentDirectory = _RootDirectory;
        }

        private DfsSector NewSector()
        {
            DfsSector sector = new DfsSector(_NextOffset);
            _SectorList.Add(sector);
            _NextOffset += sector.Size;
            return sector;
        }

        public DfsDirectoryEntry NewDirectoryEntry()
        {
            return new DfsDirectoryEntry(NewSector());
        }

        public DfsFileEntry NewFileEntry()
        {
            return new DfsFileEntry(NewSector());
        }

        public DfsSector FindSector(uint offset)
        {
            DfsSector sector;
            int index = (int)(offset / DfsSector.SECTOR_SIZE);
            sector = _SectorList[index];
            if (sector.Offset != offset)
            {
                throw new Exception("Error finding sector");
            }
            return sector;
        }

        private DfsDirectoryEntry FindDirectory(string path, uint flags)
        {
            DfsDirectoryEntry currentDir;
            if (path[0] == '/')
            {
                currentDir = _RootDirectory;
            }
            else
            {
                currentDir = _CurrentDirectory;
            }

            string[] elements = path.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < elements.Length; i++)
            {
                string search = elements[i];

                // is it the last element?
                if (i == elements.Length - 1)
                {
                    currentDir = FindDirectory(currentDir, search, flags);
                }
                else
                {
                    // not the last -> always a directory
                    currentDir = FindDirectory(currentDir, search, DfsDirectoryEntry.FLAG_DIR);
                }
                if (currentDir == null)
                    break;
            }
            return currentDir;
        }

        private DfsDirectoryEntry FindDirectory(DfsDirectoryEntry baseDir, string name, uint flags)
        {
            if (baseDir.FilePointer == 0)
                return null;

            DfsDirectoryEntry currentDir = new DfsDirectoryEntry(FindSector(baseDir.FilePointer));
            do
            {
                // check flags and path
                if (((currentDir.Flags & DfsDirectoryEntry.FLAG_MASK) == flags) && (currentDir.Path.Equals(name)))
                    return currentDir;

                // no next enty? -> done
                if (currentDir.NextEntry == 0)
                    break;
                currentDir = new DfsDirectoryEntry(FindSector(currentDir.NextEntry));
            } while (true);

            // not found
            return null;
        }

        public bool DirectoryExists(string path)
        {
            return (FindDirectory(path, DfsDirectoryEntry.FLAG_DIR) != null);
        }

        public bool FileExists(string path)
        {
            return (FindDirectory(path, DfsDirectoryEntry.FLAG_DIR) != null);
        }

        public void CreateDirectory(string path)
        {
            DfsDirectoryEntry currentDir;
            if (path[0] == '/')
            {
                currentDir = _RootDirectory;
            }
            else
            {
                currentDir = _CurrentDirectory;
            }

            string[] dirs = path.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string searchDir in dirs)
            {
                DfsDirectoryEntry foundDir = FindDirectory(currentDir, searchDir, DfsDirectoryEntry.FLAG_DIR);
                if (foundDir == null)
                {
                    // Create the new Directory and put it at the end
                    foundDir = NewDirectoryEntry();
                    foundDir.Flags = DfsDirectoryEntry.FLAG_DIR;
                    foundDir.Path = searchDir;
                    if (currentDir.FilePointer == 0)
                    {
                        currentDir.FilePointer = foundDir.Offset;
                    }
                    else
                    {
                        currentDir = new DfsDirectoryEntry(FindSector(currentDir.FilePointer));
                        AppendDirectoryEntry(currentDir, foundDir.Offset);
                    }
                }
                currentDir = foundDir;
            }
        }

        private DfsDirectoryEntry CreateFileEntry(string path)
        {
            DfsDirectoryEntry currentDir;
            if (path[0] == '/')
            {
                currentDir = _RootDirectory;
            }
            else
            {
                currentDir = _CurrentDirectory;
            }

            string[] dirs = path.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < dirs.Length; i++)
            {
                string searchDir = dirs[i];
                DfsDirectoryEntry foundDir;
                if (i == dirs.Length - 1)
                {
                    foundDir = FindDirectory(currentDir, searchDir, DfsDirectoryEntry.FLAG_FILE);
                }
                else
                {
                    foundDir = FindDirectory(currentDir, searchDir, DfsDirectoryEntry.FLAG_DIR);
                }
                if (foundDir == null)
                {
                    // Create the new Directory and put it at the end
                    foundDir = NewDirectoryEntry();
                    if (i == dirs.Length - 1)
                    {
                        foundDir.Flags = DfsDirectoryEntry.FLAG_FILE;
                    }
                    else
                    {
                        foundDir.Flags = DfsDirectoryEntry.FLAG_DIR;
                    }
                    foundDir.Path = searchDir;
                    if (currentDir.FilePointer == 0)
                    {
                        currentDir.FilePointer = foundDir.Offset;
                    }
                    else
                    {
                        currentDir = new DfsDirectoryEntry(FindSector(currentDir.FilePointer));
                        AppendDirectoryEntry(currentDir, foundDir.Offset);
                    }
                }
                currentDir = foundDir;
            }
            return currentDir;
        }

        public DfsFileStream OpenFile(string path, FileAccess access)
        {
            DfsDirectoryEntry directoryEntry = FindDirectory(path, DfsDirectoryEntry.FLAG_FILE);

            if (directoryEntry == null)
            {
                if (access == FileAccess.Read)
                {
                    throw new FileNotFoundException();
                }
                else
                {
                    directoryEntry = CreateFileEntry(path);
                }
            }
            return new DfsFileStream(this, directoryEntry, access);
        }

        void AppendDirectoryEntry(DfsDirectoryEntry directoryEntry, uint offset)
        {
            while (directoryEntry.NextEntry != 0)
            {
                directoryEntry = new DfsDirectoryEntry(FindSector(directoryEntry.NextEntry));
            }
            directoryEntry.NextEntry = offset;
        }

        // This is an extension to the normal DFS
        // The Root sector contains the number of sectors (@ SECTOR_SIZE - 8)
        // and the CRC32 of all payload sectors (@ SECTOR_SIZE - 4)
        private void UpdateMetadata()
        {
            // minus root sector
            int sectorCount = _SectorList.Count - 1;
            byte[] buffer = _SectorList[0].Buffer;
            Utils.WriteArrayBigEndian(buffer, (int)DfsSector.SECTOR_SIZE - 8, (uint)sectorCount);

            byte[] buf = new byte[(_SectorList.Count - 1) * DfsSector.SECTOR_SIZE];
            for (int i = 1; i < _SectorList.Count; i++)
            {
                _SectorList[i].Buffer.CopyTo(buf, (i - 1) * DfsSector.SECTOR_SIZE);
            }
            uint crc = Crc32Algorithm.Compute(buf);
            Utils.WriteArrayBigEndian(buffer, (int)DfsSector.SECTOR_SIZE - 4, crc);
        }

        public void WriteToStream(Stream s)
        {
            UpdateMetadata();
            foreach (var sector in _SectorList)
            {
                s.Write(sector.Buffer, 0, (int)sector.Size);
            }
        }

        public byte[] GetImage()
        {
            MemoryStream ms = new MemoryStream();
            WriteToStream(ms);
            return ms.ToArray();
        }

        public static DragonFs CreateFromStream(Stream source)
        {
            DragonFs newFs = new DragonFs();
            DfsSector sector = new DfsSector(0);
            source.Read(sector.Buffer, 0, (int)DfsSector.SECTOR_SIZE);
            DfsDirectoryEntry root = new DfsDirectoryEntry(sector);
            if ((root.Flags != ROOT_FLAGS) || (root.NextEntry != ROOT_NEXTENTRY))
            {
                throw new ArgumentException("source", "The stream does not contain a valid DFS.");
            }
            while (source.Position != source.Length)
            {
                sector = newFs.NewSector();
                source.Read(sector.Buffer, 0, (int)DfsSector.SECTOR_SIZE);
            }

            // The virtual root directory entry needs to point to the first sector
            // TODO: Can this be done better?
            newFs._RootDirectory.FilePointer = DfsSector.SECTOR_SIZE;

            return newFs;
        }

        public void TestSectorsReferences()
        {
            List<DfsSector> sectors = new List<DfsSector>(_SectorList);
            sectors.Remove(FindSector(0));
            DfsDirectoryEntry currentDir = _RootDirectory;
            WalkDirectory(_RootDirectory, sectors);

        }

        private void WalkDirectory(DfsDirectoryEntry dir, List<DfsSector> sectors)
        {
            do
            {
                var sector = FindSector(dir.Offset);
                sectors.Remove(sector);
                if (dir.FilePointer != 0)
                {
                    sector = FindSector(dir.FilePointer);
                    var entry = new DfsDirectoryEntry(sector);

                    if ((entry.Flags & DfsDirectoryEntry.FLAG_DIR) != 0)
                    {
                        WalkDirectory(entry, sectors);
                    }
                    else
                    //if ((entry.Flags & DfsDirectoryEntry.FLAG_FILE) != 0)
                    {
                        sectors.Remove(sector);
                        sector = FindSector(entry.FilePointer);
                        WalkFile(new DfsFileEntry(sector), sectors);
                    }
                }

                if (dir.NextEntry == 0)
                    break;
                dir = new DfsDirectoryEntry(FindSector(dir.NextEntry));
            } while (true);
        }

        private void WalkFile(DfsFileEntry file, List<DfsSector> sectors)
        {
            do
            {
                var sector = FindSector(file.Offset);
                sectors.Remove(sector);
                file = new DfsFileEntry(sector);

                if (file.NextSector == 0)
                    break;
                file = new DfsFileEntry(FindSector(file.NextSector));
            }
            while (true);
        }
    }

    public class DfsSector
    {
        public const UInt32 SECTOR_SIZE = 256;

        readonly byte[] _Buffer;
        readonly UInt32 _Offset = 0;

        public DfsSector(UInt32 offset)
        {
            _Buffer = new byte[SECTOR_SIZE];
            _Offset = offset;
        }

        public byte[] Buffer
        {
            get { return _Buffer; }
        }

        public UInt32 Size
        {
            get { return SECTOR_SIZE; }
        }

        public UInt32 Offset
        {
            get { return _Offset; }
        }
    }

    public class DfsDirectoryEntry
    {
        public const int MAX_FILENAME_LENGTH = 243;
        public const UInt32 FLAG_FILE = 0x00000000;
        public const UInt32 FLAG_DIR = 0x01000000;
        public const UInt32 FLAG_EOF = 0x02000000;
        public const UInt32 FLAG_MASK = 0xFF000000;

        DfsSector _Sector;

        public DfsDirectoryEntry(DfsSector sector)
        {
            _Sector = sector;
        }

        public UInt32 Flags
        {
            set
            {
                Utils.WriteArrayBigEndian(_Sector.Buffer, 0, value);
            }
            get
            {
                return Utils.ReadArrayBigEndianUint32(_Sector.Buffer, 0);
            }
        }

        public UInt32 NextEntry
        {
            set
            {
                Utils.WriteArrayBigEndian(_Sector.Buffer, 4, value);
            }
            get
            {
                return Utils.ReadArrayBigEndianUint32(_Sector.Buffer, 4);
            }
        }

        public string Path
        {
            set
            {
                if (value.Length > MAX_FILENAME_LENGTH)
                    throw new ArgumentException("Path", "String to long");
                var buf = _Sector.Buffer;
                for (int i = 0; i < value.Length; i++)
                {
                    buf[8 + i] = (byte)value[i];
                }
                buf[8 + value.Length] = 0;
            }
            get
            {
                var buf = _Sector.Buffer;
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < MAX_FILENAME_LENGTH; i++)
                {
                    if (buf[8 + i] == 0)
                        break;
                    sb.Append((char)buf[8 + i]);
                }
                return sb.ToString();
            }
        }

        public UInt32 FilePointer
        {
            set
            {
                Utils.WriteArrayBigEndian(_Sector.Buffer, 252, value);
            }
            get
            {
                return Utils.ReadArrayBigEndianUint32(_Sector.Buffer, 252);
            }
        }

        public UInt32 Offset
        {
            get { return _Sector.Offset; }
        }
    }

    public class DfsFileEntry
    {
        public const UInt32 SECTOR_PAYLOAD = DfsSector.SECTOR_SIZE - 4;

        DfsSector _Sector;

        public DfsFileEntry(DfsSector sector)
        {
            _Sector = sector;
        }

        public UInt32 NextSector
        {
            set
            {
                Utils.WriteArrayBigEndian(_Sector.Buffer, 0, value);
            }
            get
            {
                return Utils.ReadArrayBigEndianUint32(_Sector.Buffer, 0);
            }
        }

        public UInt32 Offset => _Sector.Offset;

        public byte[] Data => _Sector.Buffer;
    }

    public class DfsFileStream : Stream
    {
        DragonFs _Fs;
        DfsDirectoryEntry _Root;
        bool _CanRead = false;
        bool _CanWrite = false;
        uint _Position = 0;
        DfsFileEntry _CurrentEntry = null;
        uint _CurrentEntryPosition = 0;

        public DfsFileStream(DragonFs fs, DfsDirectoryEntry root, FileAccess access)
        {
            _Fs = fs;
            _Root = root;
            if (root.FilePointer != 0)
            {
                _CurrentEntry = new DfsFileEntry(_Fs.FindSector(root.FilePointer));
            }
            switch (access)
            {
                case FileAccess.Read:
                    _CanRead = true;
                    break;
                case FileAccess.ReadWrite:
                    _CanRead = true;
                    _CanWrite = true;
                    break;
                case FileAccess.Write:
                    _CanWrite = true;
                    break;
            }
        }

        public override bool CanRead => _CanRead;

        public override bool CanSeek => true;

        public override bool CanWrite => _CanWrite;

        public override long Length => _Root.Flags;

        public override long Position
        {
            get => _Position;
            set => throw new NotImplementedException();
        }

        private void EnsureCanRead()
        {
            if (!CanRead)
                throw new NotSupportedException("The stream is not readable.");
        }

        private void EnsureCanWrite()
        {
            if (!CanWrite)
                throw new NotSupportedException("The stream is not writable.");
        }

        public override void Flush()
        {
            
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            EnsureCanRead();

            if (_Root.FilePointer == 0)
            {
                // no content
                return 0;
            }

            // don't read beyond the file end
            int readableBytes = (int)(Length - Position);
            if (count > readableBytes)
            {
                count = readableBytes;
            }

            int readBytes = 0;

            while (count > 0)
            {
                // stop on end of file
                if (_Position >= Length)
                    break;

                readableBytes = (int)Math.Min(count, DfsFileEntry.SECTOR_PAYLOAD - _CurrentEntryPosition);
                Array.Copy(_CurrentEntry.Data, 4 + _CurrentEntryPosition, buffer, offset, readableBytes);
                count -= readableBytes;
                _CurrentEntryPosition += (uint)readableBytes;
                _Position += (uint)readableBytes;
                offset += readableBytes;
                readBytes += readableBytes;

                if (_CurrentEntryPosition >= DfsFileEntry.SECTOR_PAYLOAD)
                {
                    if (_CurrentEntry.NextSector != 0)
                    {
                        _CurrentEntry = new DfsFileEntry(_Fs.FindSector(_CurrentEntry.NextSector));
                    }
                    _CurrentEntryPosition = 0;
                }
            }

            return readBytes;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            if (_Root.FilePointer == 0)
            {
                // no content
                return 0;
            }
            if (origin == SeekOrigin.Begin)
            {
                if (offset > Length)
                {
                    offset = Length;
                }
                _Position = (uint)offset;
                _CurrentEntry = new DfsFileEntry(_Fs.FindSector(_Root.FilePointer));
                while (offset > 0)
                {
                    if (offset >= DfsFileEntry.SECTOR_PAYLOAD)
                    {
                        _CurrentEntry = new DfsFileEntry(_Fs.FindSector(_CurrentEntry.NextSector));
                        offset -= DfsFileEntry.SECTOR_PAYLOAD;
                        _CurrentEntryPosition = 0;
                    }
                    else
                    {
                        _CurrentEntryPosition = (uint)offset;
                        offset = 0;
                    }
                }
            }
            else
            {
                throw new NotImplementedException();
            }
            return Position;
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            EnsureCanWrite();

            if (_Root.FilePointer == 0)
            {
                _CurrentEntry = _Fs.NewFileEntry();
                _Root.FilePointer = _CurrentEntry.Offset;
            }
            while (count > 0)
            {
                if (_CurrentEntryPosition == DfsFileEntry.SECTOR_PAYLOAD)
                {
                    if (_CurrentEntry.NextSector == 0)
                    {
                        DfsFileEntry newEntry = _Fs.NewFileEntry();
                        _CurrentEntry.NextSector = newEntry.Offset;
                        _CurrentEntry = newEntry;
                    }
                    else
                    {
                        _CurrentEntry = new DfsFileEntry(_Fs.FindSector(_CurrentEntry.NextSector));
                    }
                    _CurrentEntryPosition = 0;
                }
                int writableBytes = (int)Math.Min(count, DfsFileEntry.SECTOR_PAYLOAD - _CurrentEntryPosition);
                Array.Copy(buffer, offset, _CurrentEntry.Data, 4 + _CurrentEntryPosition, writableBytes);
                _Position += (uint)writableBytes;
                offset += writableBytes;
                count -= writableBytes;
                if (_Position > _Root.Flags)
                {
                    if ((_Position & DfsDirectoryEntry.FLAG_MASK) != 0)
                        throw new IOException("maximum file size reached");
                    _Root.Flags = _Position;
                }
                _CurrentEntryPosition += (uint)writableBytes;
            }
        }
    }
}
