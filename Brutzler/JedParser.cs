using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Brutzler
{
    // This is a quick and dirty JED file parser only for this single purpose.
    // Don't use it for other stuff without testing or your machine could explode!
    public class JedParser
    {
        string _FileName = "";

        byte[] _BinData;
        int _BitSize;

        public JedParser(string filename)
        {
            _FileName = filename;
        }

        // Do the hard work
        public void Parse()
        {
            using (FileStream file = File.Open(_FileName, FileMode.Open))
            {
                // Don't blow the memory
                // File for LCMXO2-7000 is usually under 2 MiB, so 20 MiB should be plenty
                if (file.Length > 20 * 1024 * 1024)
                    throw new Exception("File too large.");

                ParseStream(file);
            }
        }

        private void ParseStream(Stream stream)
        {
            string jedStr = ReadMainContent(stream);
            jedStr = jedStr.Replace("\r\n", " ").Replace("\n", " ");
            var items = ReadItems(jedStr);
            ReadBinData(items);
        }

        // Read everything between STX and ETX
        private string ReadMainContent(Stream stream)
        {
            bool stx = false;
            bool etx = false;

            byte[] buffer = new byte[stream.Length];
            stream.Read(buffer, 0, (int)stream.Length);

            int pos = 0;
            while (pos < buffer.Length)
            {
                if (buffer[pos++] == 0x02)
                {
                    stx = true;
                    break;
                }
            }

            if (!stx)
                throw new Exception("No STX");

            StringBuilder sb = new StringBuilder();

            while (pos < buffer.Length)
            {
                int c = buffer[pos++];
                if (c == 0x03)
                {
                    etx = true;
                    break;
                }
                else
                {
                    sb.Append((char)c);
                }
            }

            if (!etx)
                throw new Exception("No ETX");

            return sb.ToString();
        }

        // Make an array of tuples (Item1 is field identifier, Item2 is content)
        // Skip notes
        Tuple<char, string[]>[] ReadItems(string inStr)
        {
            List<Tuple<char, string[]>> items = new List<Tuple<char, string[]>>();
            int pos = 0;
            while (pos < inStr.Length)
            {
                while ((inStr[pos] == ' ') || (inStr[pos] == '*'))
                {
                    pos++;
                    if (pos >= inStr.Length)
                        break;
                }
                if (pos < inStr.Length)
                {
                    char code = inStr[pos++];
                    StringBuilder sb = new StringBuilder();
                    while (inStr[pos] != '*')
                    {
                        sb.Append(inStr[pos++]);
                    }
                    pos++;

                    // Skip Notes
                    if (code != 'N')
                    {
                        Tuple<char, string[]> tuple = new Tuple<char, string[]>(code, sb.ToString().Split(' '));
                        items.Add(tuple);
                    }
                }
            }
            return items.ToArray();
        }

        // Get a binary image
        // Size is read from field QF
        // All L fields will be written to the respective address
        void ReadBinData(Tuple<char, string[]>[] items)
        {
            UInt16 checksum = 0;
            foreach (var t in items)
            {
                switch (t.Item1)
                {
                    case 'Q':
                        // QF => Number of fuses
                        if (t.Item2[0][0] == 'F')
                        {
                            // Read number of fuses (bits) and create the buffer
                            string number = t.Item2[0].Substring(1);
                            _BitSize = int.Parse(number);
                            _BinData = new byte[_BitSize / 8];
                        }
                        break;

                    case 'L':
                        // read the address and copy the data to the buffer
                        int addr = int.Parse(t.Item2[0]) / 8;
                        using (MemoryStream ms = new MemoryStream(_BinData))
                        {
                            ms.Seek(addr, SeekOrigin.Begin);
                            for (int i = 1; i < t.Item2.Length; i++)
                            {
                                // Consume every line which has 128 ASCII bits
                                string dat = t.Item2[i];
                                int pos = 0;
                                while (pos < dat.Length)
                                {
                                    // convert 8 ASCII bits to a byte
                                    string binStr = dat.Substring(pos, Math.Min(dat.Length - pos,8));
                                    byte b = 0;
                                    byte checksumByte = 0;
                                    for (int bit = 0; bit < 8; bit++)
                                    {
                                        b <<= 1;
                                        if (binStr[bit] == '1')
                                        {
                                            b |= 1;
                                            checksumByte |= (byte)(1 << bit);
                                        }
                                        pos++;
                                        if (pos >= dat.Length)
                                            break;
                                    }

                                    // Write the byte to the buffer
                                    ms.WriteByte(b);
                                    checksum += checksumByte;
                                }
                            }
                        }

                        break;
                }
            }
        }

        public byte[] Image
        {
            get
            {
                return (byte[])_BinData?.Clone();
            }
        }

        public int BitSize
        { 
            get => _BitSize;
        }
    }
}
