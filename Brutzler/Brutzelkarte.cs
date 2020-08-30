using StxEtx;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BrutzelProg
{
    public class Brutzelkarte
    {
        SerialPort _ComPort;
        int _PendingAck = 0;
        StxEtxParser _AckParser = new StxEtxParser();

        public Brutzelkarte(string portName)
        {
            _ComPort = new SerialPort(portName, 3000000, Parity.None, 8, StopBits.One);
            _ComPort.Handshake = Handshake.RequestToSend;
            _ComPort.RtsEnable = true;
        }

        public void Open()
        {
            Console.WriteLine("Open Port");
            _ComPort.Open();
        }

        public void Close()
        {
            Console.WriteLine("Close Port");
            // don't throw any exception
            try
            {
                _ComPort.Close();
            }
            catch (Exception)
            { }
        }

        private void WriteIntBe(StxEtxMemoryStream stream, int value)
        {
            stream.WriteByte((byte)(value >> 24));
            stream.WriteByte((byte)(value >> 16));
            stream.WriteByte((byte)(value >> 8));
            stream.WriteByte((byte)(value >> 0));
        }

        public void SendAddr(int addr)
        {
            Console.WriteLine(String.Format("Set Addr: 0x{0:X8}", addr));

            StxEtxMemoryStream stream = new StxEtxMemoryStream();
            stream.StartPacket();
            stream.WriteByte(0x01);
            WriteIntBe(stream, addr);
            stream.EndPacket();
            
            byte[] bytes = stream.GetBytes();
            _ComPort.Write(bytes, 0, bytes.Length);
        }

        public void EraseSector(int sectorAddr)
        {
            Console.WriteLine(String.Format("Erase sector: 0x{0:X8}", sectorAddr));

            StxEtxMemoryStream stream = new StxEtxMemoryStream();

            int addr = sectorAddr / 4;

            stream.StartPacket();
            stream.WriteByte(0x01);
            WriteIntBe(stream, addr);
            stream.EndPacket();

            stream.StartPacket();
            stream.WriteByte(0x02);
            stream.EndPacket();

            byte[] bytes = stream.GetBytes();
            _ComPort.Write(bytes, 0, bytes.Length);
            _PendingAck++;
        }

        public void WritePage(int pageAddr, byte[] data)
        {
            Console.WriteLine(String.Format("Write page: 0x{0:X8}", pageAddr));

            if ((pageAddr % 2) != 0)
                throw new ArgumentException("pageAddr must be dword aligned");

            StxEtxMemoryStream stream = new StxEtxMemoryStream();

            int addr = pageAddr / 4;

            stream.StartPacket();
            stream.WriteByte(0x01);
            WriteIntBe(stream, addr);
            stream.EndPacket();

            stream.StartPacket();
            stream.WriteByte(0x03);
            stream.Write(data, 0, data.Length);
            stream.EndPacket();

            byte[] bytes = stream.GetBytes();
            _ComPort.Write(bytes, 0, bytes.Length);
            _PendingAck++;
        }

        public void WriteSram(int pageAddr, byte[] data)
        {
            if ((pageAddr % 1) != 0)
                throw new ArgumentException("pageAddr must be word aligned");

            StxEtxMemoryStream stream = new StxEtxMemoryStream();

            int addr = pageAddr / 2;

            stream.StartPacket();
            stream.WriteByte(0x01);
            WriteIntBe(stream, addr);
            stream.EndPacket();

            stream.StartPacket();
            stream.WriteByte(0x05);
            stream.Write(data, 0, data.Length);
            stream.EndPacket();

            byte[] bytes = stream.GetBytes();
            _ComPort.Write(bytes, 0, bytes.Length);
            _PendingAck++;
        }

        byte ConvertToBcd(int value)
        {
            if (value > 0x99)
                throw new ArgumentOutOfRangeException("value");

            byte res = (byte)(value / 10 * 16 + value % 10);
            return res;
        }

        public void SetRtc(DateTime rtcTime)
        {
            StxEtxMemoryStream stream = new StxEtxMemoryStream();
            stream.StartPacket();
            stream.WriteByte(0x0A);
            stream.WriteByte(ConvertToBcd(rtcTime.Second));
            stream.WriteByte(ConvertToBcd(rtcTime.Minute));
            stream.WriteByte(ConvertToBcd(rtcTime.Hour));
            byte dayOfWeek = (byte)rtcTime.DayOfWeek;
            if (rtcTime.DayOfWeek == DayOfWeek.Sunday)
            {
                dayOfWeek = 7;
            }
            stream.WriteByte(dayOfWeek);
            stream.WriteByte(ConvertToBcd(rtcTime.Day));
            stream.WriteByte(ConvertToBcd(rtcTime.Month));
            stream.WriteByte(ConvertToBcd((rtcTime.Year - 2000)));
            stream.EndPacket();
            byte[] bytes = stream.GetBytes();
            _ComPort.Write(bytes, 0, bytes.Length);
        }

        public bool WaitAck()
        {
            bool receivedData = false;
            bool result = false;

            StxEtxParser.PacketCompleteHandler handler = new StxEtxParser.PacketCompleteHandler((par, data) => {
                result = (data[0] == 0x01);
                receivedData = true;
                _PendingAck--;
            });

            _AckParser.PacketComplete += handler;

            byte[] buffer = new byte[64];
            while (receivedData == false)
            {
                int len = _ComPort.Read(buffer, 0, buffer.Length);
                _AckParser.Parse(buffer, 0, len);
            }

            _AckParser.PacketComplete -= handler;

            return result;
        }

        public byte[] ReadFlashPage()
        {
            byte[] cmdData = new byte[] {
                0x04
            };

            StxEtxPacket packet = new StxEtxPacket(cmdData);
            byte[] bytes = packet.GetBytes();
            _ComPort.Write(bytes, 0, bytes.Length);

            byte[] result = null;
            StxEtxParser parser = new StxEtxParser();

            parser.PacketComplete += new StxEtxParser.PacketCompleteHandler((par, data) => {
                result = data;
            });

            byte[] readBuffer = new byte[512];
            while (result == null)
            {
                int readBytes = _ComPort.Read(readBuffer, 0, readBuffer.Length);
                parser.Parse(readBuffer, 0, readBytes);
            }

            return result;
        }

        public byte[] ReadSramPage()
        {
            byte[] cmdData = new byte[] {
                0x06
            };

            StxEtxPacket packet = new StxEtxPacket(cmdData);
            byte[] bytes = packet.GetBytes();
            _ComPort.Write(bytes, 0, bytes.Length);

            byte[] result = null;
            StxEtxParser parser = new StxEtxParser();

            parser.PacketComplete += new StxEtxParser.PacketCompleteHandler((par, data) => {
                result = data;
            });

            byte[] readBuffer = new byte[512];
            while (result == null)
            {
                int readBytes = _ComPort.Read(readBuffer, 0, readBuffer.Length);
                parser.Parse(readBuffer, 0, readBytes);
            }

            return result;
        }

        public UInt32 ReadVersion()
        {
            byte[] cmdData = new byte[] {
                0x09
            };

            StxEtxPacket packet = new StxEtxPacket(cmdData);
            byte[] bytes = packet.GetBytes();
            _ComPort.Write(bytes, 0, bytes.Length);

            byte[] result = null;
            StxEtxParser parser = new StxEtxParser();

            parser.PacketComplete += new StxEtxParser.PacketCompleteHandler((par, data) => {
                result = data;
            });

            byte[] readBuffer = new byte[512];
            while (result == null)
            {
                int readBytes = _ComPort.Read(readBuffer, 0, readBuffer.Length);
                parser.Parse(readBuffer, 0, readBytes);
            }

            return (UInt32)((result[0] << 24) | (result[1] << 16) | (result[2] << 8) | (result[3] << 0));
        }
        
        public void WriteEfb(int addr, byte[] data, int offset, int count)
        {
            if (addr > 0xff)
                throw new ArgumentException("addr");
            if ((count > 0xff) || (count < 1))
                throw new ArgumentException("count");
            if (data.Length < offset + count)
                throw new ArgumentException("data to small");

            StxEtxMemoryStream stream = new StxEtxMemoryStream();

            stream.StartPacket();
            stream.WriteByte(0x01);
            WriteIntBe(stream, addr);
            stream.EndPacket();

            stream.StartPacket();
            stream.WriteByte(0x07);
            stream.WriteByte((byte)(count - 1));
            stream.Write(data, offset, count);
            stream.EndPacket();

            byte[] bytes = stream.GetBytes();
            _ComPort.Write(bytes, 0, bytes.Length);
        }

        public void ReadEfb(int addr, byte[] data, int offset, int count)
        {
            if (addr > 0xff)
                throw new ArgumentException("addr");
            if ((count > 0xff) || (count < 1))
                throw new ArgumentException("count");
            if (data.Length < count + offset)
                throw new ArgumentException("data to small");

            StxEtxMemoryStream stream = new StxEtxMemoryStream();

            stream.StartPacket();
            stream.WriteByte(0x01);
            WriteIntBe(stream, addr);
            stream.EndPacket();

            stream.StartPacket();
            stream.WriteByte(0x08);
            stream.WriteByte((byte)(count - 1));
            stream.EndPacket();

            byte[] bytes = stream.GetBytes();
            _ComPort.Write(bytes, 0, bytes.Length);


            byte[] result = null;
            StxEtxParser parser = new StxEtxParser();

            parser.PacketComplete += new StxEtxParser.PacketCompleteHandler((par, d) => {
                result = d;
            });

            byte[] readBuffer = new byte[512];
            while (result == null)
            {
                int readBytes = _ComPort.Read(readBuffer, 0, readBuffer.Length);
                parser.Parse(readBuffer, 0, readBytes);
            }

            result.CopyTo(data, offset);
        }

        public void WriteEfb32Bit(int addr, UInt32 val)
        {
            byte[] data = new byte[] {
                (byte)(val >> 24),
                (byte)(val >> 16),
                (byte)(val >> 8),
                (byte)(val >> 0),
            };

            WriteEfb(addr, data, 0, 4);
        }

        public void WriteEfb8Bit(int addr, byte val)
        {
            byte[] data = new byte[] {
                val
            };

            WriteEfb(addr, data, 0, 1);
        }

        public UInt32 ReadEfb32Bit(int addr)
        {
            byte[] data = new byte[4];

            ReadEfb(addr, data, 0, 4);

            return (UInt32)((data[0] << 24) | (data[1] << 16) | (data[2] << 8) | (data[3] << 0));
        }

        public int PendingAck
        {
            get => _PendingAck;
        }
    }

    public class BrutzelkarteDummy
    {
        
        int _PendingAck = 0;
        int _TimeToWait = 0;

        public BrutzelkarteDummy(string portName)
        {
            
        }

        public void Open()
        {
           
        }

        public void Close()
        {
            
        }

        public void SendAddr(int addr)
        {
            
        }

        public void EraseSector(int sectorAddr)
        {
            _PendingAck++;
            _TimeToWait += 100;
        }

        public void WritePage(int pageAddr, byte[] data)
        {
            if ((pageAddr % 2) != 0)
                throw new ArgumentException("pageAddr must be dword aligned");
            
            _PendingAck++;
            _TimeToWait += 1;
        }

        public void WriteSram(int pageAddr, byte[] data)
        {
            if ((pageAddr % 1) != 0)
                throw new ArgumentException("pageAddr must be word aligned");
            
            _PendingAck++;
            _TimeToWait += 1;
        }

        public void SetRtc(DateTime rtcTime)
        {
            Thread.Sleep(5);
        }

        public bool WaitAck()
        {
            Thread.Sleep(_TimeToWait);
            _PendingAck = 0;
            _TimeToWait = 0;
            return true;
        }

        public byte[] ReadFlashPage()
        {
            Thread.Sleep(5);
            return new byte[256];
        }

        public byte[] ReadSramPage()
        {
            Thread.Sleep(5);
            return new byte[256];
        }

        public UInt32 ReadVersion()
        {
            Thread.Sleep(5);
            return 0x12345678;
        }

        public void WriteEfb(int addr, byte[] data, int offset, int count)
        {
            
        }

        public void ReadEfb(int addr, byte[] data, int offset, int count)
        {
            Thread.Sleep(5);
        }

        public void WriteEfb32Bit(int addr, UInt32 val)
        {
            
        }

        public void WriteEfb8Bit(int addr, byte val)
        {
            
        }

        public UInt32 ReadEfb32Bit(int addr)
        {
            Thread.Sleep(5);
            return 0;
        }

        public int PendingAck
        {
            get => _PendingAck;
        }
    }
}
