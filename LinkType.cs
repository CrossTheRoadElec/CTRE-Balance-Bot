using System;
using Microsoft.SPOT;

namespace CTRE.Phoenix
{
    public abstract class LinkType
    {
        abstract public byte Open(CTRE.Gadgeteer.IPortUart portDef, int arg);
        abstract public int Recv(byte[] buf, byte len, ref UInt16 cs);
        abstract public int Recv(byte[] buf, byte len);
        abstract public int Send(byte[] buf, byte len);
        abstract public void Close();
        abstract public void ClearBuffer();

        public bool bufferOverflow = false;
        abstract public byte bufferSize { get; }
    }
}
