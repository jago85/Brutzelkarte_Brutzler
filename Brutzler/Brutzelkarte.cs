using StxEtx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FTD2XX_NET;
using static FTD2XX_NET.FTDI;

namespace BrutzelProg
{
    public class Brutzelkarte
    {
        enum Commands
        {
            SetAddr = 0x01,
            FlashErase = 0x02,
            FlashWrite = 0x03,
            FlashRead = 0x04,
            SramWrite = 0x05,
            SramRead = 0x06,
            EfbWrite = 0x07,
            EfbRead = 0x08,
            ReadVersion = 0x09,
            SetRtc = 0x0A,
            FlashSelect = 0x0B,
            GetFlashAddrWidth = 0x0C,
            GetSramAddrWidth = 0x0D
        };

        FTDI _Ftdi;
        string _PortName = "";
        int _PendingAck = 0;
        StxEtxParser _AckParser = new StxEtxParser();
        EventWaitHandle _WaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset);

        FirmwareIdentity _FirmwareIdentity;

        public Brutzelkarte(string portName)
        {
            _PortName = portName;
            _Ftdi = new FTDI();
        }

        public void Open()
        {
            Console.WriteLine("Open Port");
            FT_STATUS status;

            status = _Ftdi.OpenBySerialNumber(_PortName);
            if (status == FT_STATUS.FT_OK)
                status = _Ftdi.SetLatency(4);
            if (status == FT_STATUS.FT_OK)
                status = _Ftdi.SetBaudRate(3000000);
            if (status == FT_STATUS.FT_OK)
                status = _Ftdi.InTransferSize(64);
            if (status == FT_STATUS.FT_OK)
                status = _Ftdi.SetEventNotification(FT_EVENTS.FT_EVENT_RXCHAR, _WaitHandle);
            if (status != FT_STATUS.FT_OK)
                throw new Exception("Error opening device " + _PortName);
        }

        public void Close()
        {
            Console.WriteLine("Close Port");
            // don't throw any exception
            try
            {
                _Ftdi.Close();
            }
            catch (Exception)
            { }
        }

        private byte[] ReceiveResponse()
        {
            byte[] result = null;
            StxEtxParser parser = new StxEtxParser();

            parser.PacketComplete += new StxEtxParser.PacketCompleteHandler((par, data) => {
                result = data;
            });

            while (result == null)
            {
                if (_WaitHandle.WaitOne(1000) == false)
                    throw new Exception("Error reading data (ReceiveResponse)");

                uint rxBytes = 0;
                _Ftdi.GetRxBytesAvailable(ref rxBytes);
                if (rxBytes > 0)
                {
                    byte[] readBuffer = new byte[rxBytes];
                    uint readBytes = 0;
                    FT_STATUS status = _Ftdi.Read(readBuffer, (uint)readBuffer.Length, ref readBytes);
                    if (status != FT_STATUS.FT_OK)
                        throw new Exception("Error reading data (ReceiveResponse)");
                    parser.Parse(readBuffer, 0, (int)readBytes);
                }
            }

            return result;
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
            stream.WriteByte((byte)Commands.SetAddr);
            WriteIntBe(stream, addr);
            stream.EndPacket();

            byte[] bytes = stream.GetBytes();
            uint bytesWritten = 0;
            FT_STATUS status = _Ftdi.Write(bytes, bytes.Length, ref bytesWritten);
            if (status != FT_STATUS.FT_OK)
                throw new Exception("Error writing data (SendAddr)");
            if (bytesWritten != bytes.Length)
                throw new Exception("Wrong number of bytes written (SendAddr)");
        }

        public void SetFlashSelect(FlashType flashType)
        {
            byte flashSel;
            if (flashType == FlashType.Rom)
                flashSel = 0;
            else
                flashSel = 1;
            Console.WriteLine(String.Format("Set FlashSel: {0}", flashSel));

            StxEtxMemoryStream stream = new StxEtxMemoryStream();
            stream.StartPacket();
            stream.WriteByte((byte)Commands.FlashSelect);
            stream.WriteByte(flashSel);
            stream.EndPacket();

            byte[] bytes = stream.GetBytes();
            uint bytesWritten = 0;
            FT_STATUS status = _Ftdi.Write(bytes, bytes.Length, ref bytesWritten);
            if (status != FT_STATUS.FT_OK)
                throw new Exception("Error writing data (SetFlashSelect)");
            if (bytesWritten != bytes.Length)
                throw new Exception("Wrong number of bytes written (SetFlashSelect)");
        }

        public void EraseSector(int sectorAddr)
        {
            Console.WriteLine(String.Format("Erase sector: 0x{0:X8}", sectorAddr));

            StxEtxMemoryStream stream = new StxEtxMemoryStream();

            int addr = sectorAddr / 4;

            stream.StartPacket();
            stream.WriteByte((byte)Commands.SetAddr);
            WriteIntBe(stream, addr);
            stream.EndPacket();

            stream.StartPacket();
            stream.WriteByte((byte)Commands.FlashErase);
            stream.EndPacket();

            byte[] bytes = stream.GetBytes();
            uint bytesWritten = 0;
            FT_STATUS status = _Ftdi.Write(bytes, bytes.Length, ref bytesWritten);
            if (status != FT_STATUS.FT_OK)
                throw new Exception("Error writing data (EraseSector)");
            if (bytesWritten != bytes.Length)
                throw new Exception("Wrong number of bytes written (EraseSector)");
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
            stream.WriteByte((byte)Commands.SetAddr);
            WriteIntBe(stream, addr);
            stream.EndPacket();

            stream.StartPacket();
            stream.WriteByte((byte)Commands.FlashWrite);
            stream.Write(data, 0, data.Length);
            stream.EndPacket();

            byte[] bytes = stream.GetBytes();
            uint bytesWritten = 0;
            FT_STATUS status = _Ftdi.Write(bytes, bytes.Length, ref bytesWritten);
            if (status != FT_STATUS.FT_OK)
                throw new Exception("Error writing data (WritePage)");
            if (bytesWritten != bytes.Length)
                throw new Exception("Wrong number of bytes written (WritePage)");
            _PendingAck++;
        }

        public void WriteSram(int pageAddr, byte[] data)
        {
            if ((pageAddr % 1) != 0)
                throw new ArgumentException("pageAddr must be word aligned");

            StxEtxMemoryStream stream = new StxEtxMemoryStream();

            int addr = pageAddr / 2;

            stream.StartPacket();
            stream.WriteByte((byte)Commands.SetAddr);
            WriteIntBe(stream, addr);
            stream.EndPacket();

            stream.StartPacket();
            stream.WriteByte((byte)Commands.SramWrite);
            stream.Write(data, 0, data.Length);
            stream.EndPacket();

            byte[] bytes = stream.GetBytes();
            uint bytesWritten = 0;
            FT_STATUS status = _Ftdi.Write(bytes, bytes.Length, ref bytesWritten);
            if (status != FT_STATUS.FT_OK)
                throw new Exception("Error writing data (WriteSram)");
            if (bytesWritten != bytes.Length)
                throw new Exception("Wrong number of bytes written (WriteSram)");
            _PendingAck++;
        }

        public UInt32 GetFlashSize()
        {
            byte[] cmdData = new byte[] {
                (byte)Commands.GetFlashAddrWidth
            };

            StxEtxPacket packet = new StxEtxPacket(cmdData);
            byte[] bytes = packet.GetBytes();
            uint bytesWritten = 0;
            FT_STATUS status = _Ftdi.Write(bytes, bytes.Length, ref bytesWritten);
            if (status != FT_STATUS.FT_OK)
                throw new Exception("Error writing data (GetFlashSize)");
            if (bytesWritten != bytes.Length)
                throw new Exception("Wrong number of bytes written (GetFlashSize)");

            byte[] resp = ReceiveResponse();
            return (UInt32)(Math.Pow(2, resp[0]));
        }

        public UInt32 GetSramSize()
        {
            byte[] cmdData = new byte[] {
                (byte)Commands.GetSramAddrWidth
            };

            StxEtxPacket packet = new StxEtxPacket(cmdData);
            byte[] bytes = packet.GetBytes();
            uint bytesWritten = 0;
            FT_STATUS status = _Ftdi.Write(bytes, bytes.Length, ref bytesWritten);
            if (status != FT_STATUS.FT_OK)
                throw new Exception("Error writing data (GetFlashSize)");
            if (bytesWritten != bytes.Length)
                throw new Exception("Wrong number of bytes written (GetFlashSize)");

            byte[] resp = ReceiveResponse();
            return (UInt32)(Math.Pow(2, resp[0]));
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
            stream.WriteByte((byte)Commands.SetRtc);
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
            uint bytesWritten = 0;
            FT_STATUS status = _Ftdi.Write(bytes, bytes.Length, ref bytesWritten);
            if (status != FT_STATUS.FT_OK)
                throw new Exception("Error writing data (SetRtc)");
            if (bytesWritten != bytes.Length)
                throw new Exception("Wrong number of bytes written (SetRtc)");
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
                if (_WaitHandle.WaitOne(1000) == false)
                    throw new Exception("Error reading data (WaitAck)");

                uint rxBytes = 0;
                _Ftdi.GetRxBytesAvailable(ref rxBytes);
                if (rxBytes > 0)
                {
                    byte[] readBuffer = new byte[rxBytes];
                    uint readBytes = 0;
                    FT_STATUS status = _Ftdi.Read(readBuffer, (uint)readBuffer.Length, ref readBytes);
                    if (status != FT_STATUS.FT_OK)
                        throw new Exception("Error reading data (WaitAck)");
                    _AckParser.Parse(readBuffer, 0, (int)readBytes);
                }
            }

            _AckParser.PacketComplete -= handler;

            return result;
        }

        public byte[] ReadFlashPage()
        {
            byte[] cmdData = new byte[] {
                (byte)Commands.FlashRead
            };

            StxEtxPacket packet = new StxEtxPacket(cmdData);
            byte[] bytes = packet.GetBytes();
            uint bytesWritten = 0;
            FT_STATUS status = _Ftdi.Write(bytes, bytes.Length, ref bytesWritten);
            if (status != FT_STATUS.FT_OK)
                throw new Exception("Error writing data (ReadFlashPage)");
            if (bytesWritten != bytes.Length)
                throw new Exception("Wrong number of bytes written (ReadFlashPage)");

            return ReceiveResponse();
        }

        public byte[] ReadSramPage()
        {
            Console.WriteLine("ReadSramPage");

            byte[] cmdData = new byte[] {
                (byte)Commands.SramRead
            };

            StxEtxPacket packet = new StxEtxPacket(cmdData);
            byte[] bytes = packet.GetBytes();
            uint bytesWritten = 0;
            FT_STATUS status = _Ftdi.Write(bytes, bytes.Length, ref bytesWritten);
            if (status != FT_STATUS.FT_OK)
                throw new Exception("Error writing data (ReadSramPage)");
            if (bytesWritten != bytes.Length)
                throw new Exception("Wrong number of bytes written (ReadSramPage)");

            return ReceiveResponse();
        }

        public FirmwareIdentity ReadVersion()
        {
            byte[] cmdData = new byte[] {
                (byte)Commands.ReadVersion
            };

            StxEtxPacket packet = new StxEtxPacket(cmdData);
            byte[] bytes = packet.GetBytes();
            uint bytesWritten = 0;
            FT_STATUS status = _Ftdi.Write(bytes, bytes.Length, ref bytesWritten);
            if (status != FT_STATUS.FT_OK)
                throw new Exception("Error writing data (ReadVersion)");
            if (bytesWritten != bytes.Length)
                throw new Exception("Wrong number of bytes written (ReadVersion)");

            byte[] result = ReceiveResponse();

            _FirmwareIdentity.Id = result[0];
            _FirmwareIdentity.Major = result[1];
            _FirmwareIdentity.Minor = result[2];
            _FirmwareIdentity.Debug = result[3];

            return _FirmwareIdentity;
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
            stream.WriteByte((byte)Commands.SetAddr);
            WriteIntBe(stream, addr);
            stream.EndPacket();

            stream.StartPacket();
            stream.WriteByte((byte)Commands.EfbWrite);
            stream.WriteByte((byte)(count - 1));
            stream.Write(data, offset, count);
            stream.EndPacket();

            byte[] bytes = stream.GetBytes();
            uint bytesWritten = 0;
            FT_STATUS status = _Ftdi.Write(bytes, bytes.Length, ref bytesWritten);
            if (status != FT_STATUS.FT_OK)
                throw new Exception("Error writing data (WriteEfb)");
            if (bytesWritten != bytes.Length)
                throw new Exception("Wrong number of bytes written (WriteEfb)");
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
            stream.WriteByte((byte)Commands.SetAddr);
            WriteIntBe(stream, addr);
            stream.EndPacket();

            stream.StartPacket();
            stream.WriteByte((byte)Commands.EfbRead);
            stream.WriteByte((byte)(count - 1));
            stream.EndPacket();

            byte[] bytes = stream.GetBytes();
            uint bytesWritten = 0;
            FT_STATUS status = _Ftdi.Write(bytes, bytes.Length, ref bytesWritten);
            if (status != FT_STATUS.FT_OK)
                throw new Exception("Error writing data (ReadEfb)");
            if (bytesWritten != bytes.Length)
                throw new Exception("Wrong number of bytes written (ReadEfb)");

            byte[] result = ReceiveResponse();
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

        public FirmwareIdentity FirmwareIdentity
        {
            get => _FirmwareIdentity;
        }
    }

    public enum FlashType
    { 
        Rom,
        Boot
    }

    public struct FirmwareIdentity
    {
        public byte Id;
        public byte Major;
        public byte Minor;
        public byte Debug;

        public override string ToString()
        {
            return String.Format("ID:{0} v{1}.{2}.{3}", Id, Major, Minor, Debug);
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
