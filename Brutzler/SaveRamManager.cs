using System;
using System.Collections.Generic;

namespace BrutzelProg
{
    public class SaveRamManager
    {
        SortedList<int, SaveItem> _SaveList = new SortedList<int, SaveItem>();

        int _RamSize;
        int _FragmentSize;
        private int _BytesFree;

        public int BytesFree
        {
            get => _BytesFree;
        }

        public SaveRamManager(int ramSize, int fragmentSize)
        {
            if (ramSize == 0)
                throw new ArgumentException("ramSize");
            if (fragmentSize == 0)
                throw new ArgumentException("fragmentSize");

            _BytesFree = ramSize;
            _RamSize = ramSize;
            _FragmentSize = fragmentSize;
        }
        
        // Calculate actual memory size based on _FragmentSize
        int GetMemorySize(int size)
        {
            return (int)Math.Ceiling(size / (double)_FragmentSize) * _FragmentSize;
        }

        // Reserve Memory with size
        // Return start offset
        // Throws Exception if no memory available
        public int Alloc(int size)
        {
            int offset = 0;
            int memorySize = GetMemorySize(size);

            if (_BytesFree < memorySize)
                throw new Exception("No memory");

            // Search for space between the items
            foreach (var i in _SaveList)
            {
                // If current item is behind current offset, there is a gap
                // Check the size of the gap
                if (i.Value.Offset - offset >= memorySize)
                {
                    // gap found
                    break;
                }

                // move offset behind current item
                offset = i.Value.Offset + i.Value.SizeInMemory;
            }

            // gap found or end of list reached, check remaining memory
            if (offset + size > _RamSize)
            {
                throw new SaveRamFragmentedException();
            }

            _SaveList.Add(offset, new SaveItem() { Offset = offset, Size = size, SizeInMemory = memorySize });
            _BytesFree -= memorySize;
            return offset;
        }

        // Reserve Memory with size
        // Return start offset
        // Throws Exception if no memory available
        // Throws Exception offset is already reserved
        public void AllocAt(int offset, int size)
        {
            int memorySize = GetMemorySize(size);

            if (_BytesFree < memorySize)
                throw new Exception("No memory");

            if (offset % _FragmentSize != 0)
                throw new Exception("Offset not possible");

            if (offset + size > _RamSize)
                throw new Exception("Not enough mem");

            foreach (var i in _SaveList)
            {
                if (i.Value.Offset <= offset)
                {
                    if ((i.Value.Offset + i.Value.SizeInMemory) > offset)
                        throw new Exception("Already reserved");
                }
                else
                {
                    break;
                }
            }

            _SaveList.Add(offset, new SaveItem() { Offset = offset, Size = size, SizeInMemory = memorySize });
            _BytesFree -= memorySize;
        }

        // Removes a memory item from list
        // Throws Exception if no item exists
        public void Return(int offset)
        {
            var i = _SaveList[offset];
            _BytesFree += i.SizeInMemory;
            _SaveList.Remove(offset);
        }

        // Goes through all blocks and moves them close togehter
        // Callback: SaveItem (Offset and Size) and new offset
        public void DefragmentMem(Action<SaveItem, int> defragAction)
        {
            if (_SaveList.Count >= 1)
            {
                // The list will be changed, because the offsets -> keys will change
                SortedList<int, SaveItem> newList = new SortedList<int, SaveItem>();

                int newOffset = 0;
                foreach (var i in _SaveList)
                {
                    if (i.Value.Offset > newOffset)
                    {
                        defragAction?.Invoke(i.Value, newOffset);
                        i.Value.Offset = newOffset;
                    }
                    // Add the (maybe) changed item to the new list
                    newList.Add(newOffset, i.Value);
                    newOffset = i.Value.Offset + i.Value.SizeInMemory;
                }

                // Replace the list
                _SaveList = newList;
            }
        }
    }

    public class SaveItem
    {
        public int Offset { get; set; }
        public int Size { get; set; }
        public int SizeInMemory { get; set; }

        public override string ToString()
        {
            return String.Format("{0} Bytes @{1:X}", Size, Offset);
        }
    }

    public class SaveRamFragmentedException : Exception
    {
        public SaveRamFragmentedException()
            : base("Memory fragmented")
        { }
    }
}
