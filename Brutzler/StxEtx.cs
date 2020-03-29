using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StxEtx
{
    public class StxEtxMemoryStream
    {
        const byte STX = 0x02;
        const byte ETX = 0x03;
        const byte DLE = 0x10;
        static byte[] Start = new byte[] { DLE, STX };
        static byte[] Stop = new byte[] { DLE, ETX };

        protected MemoryStream _Mem = new MemoryStream();

        public void StartPacket()
        {
            _Mem.Write(Start, 0, Start.Length);
        }

        public void EndPacket()
        {
            _Mem.Write(Stop, 0, Stop.Length);
        }

        public void Write(byte[] buffer, int offset, int count)
        {
            for (int i = 0; i < count; i++)
            {
                WriteByte(buffer[offset + i]);
            }
        }

        public void WriteByte(byte value)
        {
            if (value == DLE)
                _Mem.WriteByte(DLE);
            _Mem.WriteByte(value);
        }

        public byte[] GetBytes()
        {
            return _Mem.ToArray();
        }

        public void Reset()
        {
            _Mem.Seek(0, SeekOrigin.Begin);
            _Mem.SetLength(0);
        }

        public long Length
        {
            get { return _Mem.Length; }
        }
    }

    public class StxEtxPacket
    {
        StxEtxMemoryStream _Stream = new StxEtxMemoryStream();

        public StxEtxPacket(byte[] data)
        {
            _Stream.StartPacket();
            _Stream.Write(data, 0, data.Length);
            _Stream.EndPacket();
        }

        public byte[] GetBytes()
        {
            return _Stream.GetBytes();
        }

        public int Length
        {
            get { return (int)_Stream.Length; }
        }
    }

    public class StxEtxParser
    {
        const byte STX = 0x02;
        const byte ETX = 0x03;
        const byte DLE = 0x10;

        enum ParserState
        {
            WaitStart = 0,
            Receiving
        };

        ParserState _State = ParserState.WaitStart;
        bool _IsDle = false;
        MemoryStream _Memory = new MemoryStream();

        public delegate void PacketCompleteHandler(StxEtxParser parser, byte[] data);

        public event PacketCompleteHandler PacketComplete; 

        public StxEtxParser()
        { }

        public void Parse(byte[] responseBuffer, int offset, int length)
        {
            for (int i = 0; i < length; i++)
            {
                Parse(responseBuffer[offset + i]);
            }
        }

        public void Parse(byte b)
        {
            bool isWrite = false;

            switch (_State)
            {
                case ParserState.WaitStart:
                    if (_IsDle)
                    {
                        if (b == STX)
                            _State = ParserState.Receiving;
                        _IsDle = false;
                    }
                    else if (b == DLE)
                    {
                        _IsDle = true;
                    }
                    break;

                case ParserState.Receiving:
                    if (_IsDle)
                    {
                        switch (b)
                        {
                            case STX:
                                ClearData();
                                break;
                            case ETX:
                                OnPacketComplete();
                                ClearData();
                                _State = ParserState.WaitStart;
                                break;
                            default:
                                isWrite = true;
                                break;
                        }
                        _IsDle = false;
                    }
                    else if (b == DLE)
                    {
                        _IsDle = true;
                    }
                    else
                    {
                        isWrite = true;
                    }
                    break;
            }
            if (isWrite)
            {
                WriteData(b);
            }
        }

        public void Reset()
        {
            _State = ParserState.WaitStart;
            _Memory.SetLength(0);
        }

        private void ClearData()
        {
            _Memory.SetLength(0);
        }

        private void WriteData(byte b)
        {
            _Memory.WriteByte(b);
        }

        private void OnPacketComplete()
        {
            if (PacketComplete != null)
            {
                PacketComplete(this, _Memory.ToArray());
            }
        }
    }
}
