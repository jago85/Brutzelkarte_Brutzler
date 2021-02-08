using Crc32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

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
        const string ROOT_PATH   = "DragonFS 2.0";

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

        public uint NewBlob(int size)
        {
            if (size <= 0)
                throw new NotSupportedException("Cannot create blob with no data");

            uint firstOffset = 0;
            while (size > 0)
            {
                DfsSector sector = NewSector();
                if (firstOffset == 0)
                    firstOffset = sector.Offset;
                size -= (int)DfsSector.SECTOR_SIZE;
            }
            return firstOffset;
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
            bool checkValid = true;
            if ((root.Flags != ROOT_FLAGS) || (root.NextEntry != ROOT_NEXTENTRY))
            {
                checkValid = false;
            }
            else
            {
                // Check DFS version string
                if (!root.Path.Equals(DragonFs.ROOT_PATH))
                {
                    checkValid = false;
                }
            }

            if (checkValid == false)
            {
                throw new ArgumentException("source", "The stream does not contain a valid DFS 2.0 image");
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
            WalkDirectory(_RootDirectory, sectors);
            if (sectors.Count > 0)
                throw new Exception("Unreferenced sectors found");
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
                        WalkFile(entry, sectors);
                    }
                }

                if (dir.NextEntry == 0)
                    break;
                dir = new DfsDirectoryEntry(FindSector(dir.NextEntry));
            } while (true);
        }

        private void WalkFile(DfsDirectoryEntry fileEntry, List<DfsSector> sectors)
        {
            int fileSize = (int)(fileEntry.Flags & ~DfsDirectoryEntry.FLAG_MASK);
            uint offset = 0;
            do
            {
                var sector = FindSector(fileEntry.FilePointer + offset);
                sectors.Remove(sector);
                offset += DfsSector.SECTOR_SIZE;
            } while (offset < fileSize);
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
        public const UInt32 FLAG_DIR = 0x10000000;
        public const UInt32 FLAG_EOF = 0x20000000;
        public const UInt32 FLAG_MASK = 0xF0000000;

        public const Int32 NEXT_OFFSET = 0;
        public const Int32 FLAGS_OFFSET = 4;
        public const Int32 PATH_OFFSET = 8;
        public const Int32 FILEPOINTER_OFFSET = 252;

        DfsSector _Sector;

        public DfsDirectoryEntry(DfsSector sector)
        {
            _Sector = sector;
        }

        public UInt32 Flags
        {
            set
            {
                Utils.WriteArrayBigEndian(_Sector.Buffer, FLAGS_OFFSET, value);
            }
            get
            {
                return Utils.ReadArrayBigEndianUint32(_Sector.Buffer, FLAGS_OFFSET);
            }
        }

        public UInt32 NextEntry
        {
            set
            {
                Utils.WriteArrayBigEndian(_Sector.Buffer, NEXT_OFFSET, value);
            }
            get
            {
                return Utils.ReadArrayBigEndianUint32(_Sector.Buffer, NEXT_OFFSET);
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
                    buf[PATH_OFFSET + i] = (byte)value[i];
                }
                buf[PATH_OFFSET + value.Length] = 0;
            }
            get
            {
                var buf = _Sector.Buffer;
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < MAX_FILENAME_LENGTH; i++)
                {
                    if (buf[PATH_OFFSET + i] == 0)
                        break;
                    sb.Append((char)buf[PATH_OFFSET + i]);
                }
                return sb.ToString();
            }
        }

        public UInt32 FilePointer
        {
            set
            {
                Utils.WriteArrayBigEndian(_Sector.Buffer, FILEPOINTER_OFFSET, value);
            }
            get
            {
                return Utils.ReadArrayBigEndianUint32(_Sector.Buffer, FILEPOINTER_OFFSET);
            }
        }

        public UInt32 Offset
        {
            get { return _Sector.Offset; }
        }
    }

    public class DfsFileStream : Stream
    {
        DragonFs _Fs;
        DfsDirectoryEntry _Root;
        bool _CanRead = false;
        bool _CanWrite = false;
        MemoryStream _MemStream;

        public DfsFileStream(DragonFs fs, DfsDirectoryEntry root, FileAccess access)
        {
            _Fs = fs;
            _Root = root;
            if (root.FilePointer != 0)
            {
                if ((access == FileAccess.Write) || (access == FileAccess.ReadWrite))
                    throw new NotSupportedException("Writing an existing file is not supported");

                // Create MemoryStream from existing data
                _MemStream = new MemoryStream((int)(_Root.Flags & ~DfsDirectoryEntry.FLAG_MASK));

                int remainingBytes = (int)Length;
                uint offset = root.FilePointer;
                while (remainingBytes > 0)
                {
                    // Fetch a sector and write to the stream
                    DfsSector sector = _Fs.FindSector(offset);
                    int sectorBytes = (int)DfsSector.SECTOR_SIZE;
                    if (sectorBytes >= remainingBytes)
                        sectorBytes = remainingBytes;
                    _MemStream.Write(sector.Buffer, 0, sectorBytes);

                    remainingBytes -= sectorBytes;
                    offset += DfsSector.SECTOR_SIZE;
                }
                _MemStream.Seek(0, SeekOrigin.Begin);
            }
            else
            {
                if (access == FileAccess.Read)
                    throw new NotSupportedException("Cannot read file without content");

                // Create MemoryStream for all file operations
                _MemStream = new MemoryStream();
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

        public override long Length => _Root.Flags & ~DfsDirectoryEntry.FLAG_MASK;

        public override long Position
        {
            get => _MemStream.Position;
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

        public override void Close()
        {
            if ((_Root.FilePointer == 0) && (_CanWrite) && (_MemStream.Length > 0))
            {
                // Create a data blob in the FS
                uint blobOffset = _Fs.NewBlob((int)_MemStream.Length);

                // Write the data to the blob sectors
                _MemStream.Seek(0, SeekOrigin.Begin);
                while (_MemStream.Position < _MemStream.Length)
                {
                    DfsSector sector = _Fs.FindSector((uint)(blobOffset + _MemStream.Position));
                    _MemStream.Read(sector.Buffer, 0, (int)DfsSector.SECTOR_SIZE);
                }

                _Root.FilePointer = blobOffset;
                _Root.Flags = (uint)(_MemStream.Length & ~DfsDirectoryEntry.FLAG_MASK);
            }
            _MemStream.Close();
            base.Close();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            EnsureCanRead();
            
            return _MemStream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return _MemStream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            EnsureCanWrite();

            _MemStream.Write(buffer, offset, count);
        }
    }
}
