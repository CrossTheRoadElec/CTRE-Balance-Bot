using System;
using Microsoft.SPOT;

namespace CTRE.Phoenix
{
    public class Link2UART : LinkType
    {
        public const int PIXY_UART_BAUDRATE = 19200;


        private System.IO.Ports.SerialPort m_uartPort;

        private const int bufSize = 128;
        private byte[] m_localBuf;
        private byte start;
        private byte end;

        private void GetDataHandler(
                                object sender,
                                System.IO.Ports.SerialDataReceivedEventArgs e)
        {
            int tmp = ((System.IO.Ports.SerialPort)sender).BytesToRead;
            if (end + tmp < bufSize)
                ((System.IO.Ports.SerialPort)sender).Read(m_localBuf, end, tmp);
            else
            {
                ((System.IO.Ports.SerialPort)sender).Read(m_localBuf, end, bufSize - 1 - end);
                ((System.IO.Ports.SerialPort)sender).Read(m_localBuf, 0, tmp - (bufSize - 1 - end));
            }
            end += (byte)tmp;
            if (end >= bufSize) end -= bufSize;
        }

        public override void ClearBuffer()
        {
            start = end = 0;
            bufferOverflow = false;
        }
        public override byte bufferSize { get { return (byte)(end - start); } }

        public override byte Open(CTRE.Gadgeteer.IPortUart portDef, int arg)
        {

            m_uartPort = new System.IO.Ports.SerialPort(portDef.UART, arg, System.IO.Ports.Parity.None, 8, System.IO.Ports.StopBits.One);
            m_uartPort.DataReceived += GetDataHandler;
            m_uartPort.Open();

            m_localBuf = new byte[bufSize];
            start = end = 0;
            return 0;
        }

        public override void Close()
        {
        }

        public override int Recv(byte[] buf, byte len)
        {
            UInt16 tmp = 0;
            return Recv(buf, len, ref tmp);
        }

        public override int Recv(byte[] buf, byte len, ref UInt16 cs)
        {
            cs = 0;
            if (bufferSize >= len)
            {
                if (start + len < bufSize)
                    Array.Copy(m_localBuf, start, buf, 0, len);
                else
                {
                    Array.Copy(m_localBuf, start, buf, 0, bufSize - 1 - start);
                    Array.Copy(m_localBuf, 0, buf, bufSize - 1 - start, len - (bufSize - 1 - start));
                }
                for (int i = 0; i < len; i++)
                {
                    cs += buf[i];
                }
                start += len;
                if (start >= bufSize) start -= bufSize;
                return len;
            }
            else
                return -1;
        }

        public override int Send(byte[] buf, byte len)
        {
            m_uartPort.Write(buf, 0, len);
            return len;
        }
    }

    public class Pixy2UART : TPixy2
    {
        public Pixy2UART(CTRE.Gadgeteer.IPortUart portDef, int baudrate = Link2UART.PIXY_UART_BAUDRATE) : base()
        {
            m_link = new Link2UART();
            m_link.Open(portDef, baudrate);
        }
    }
}
