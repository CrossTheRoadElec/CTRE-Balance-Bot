using System;
using Microsoft.SPOT;

namespace CTRE.Phoenix
{
    public class TPixy2
    {
        public const int PIXY_BUFFERSIZE = 0x104;
        public const int PIXY_CHECKSUM_SYNC = 0xc1af;
        public const int PIXY_NO_CHECKSUM_SYNC = 0xc1ae;
        public const int PIXY_SEND_HEADER_SIZE = 4;
        public const int PIXY_MAX_PROGNAME = 33;

        public const int PIXY_TYPE_REQUEST_CHANGE_PROG = 0x02;
        public const int PIXY_TYPE_REQUEST_RESOLUTION = 0x0c;
        public const int PIXY_TYPE_RESPONSE_RESOLUTION = 0x0d;
        public const int PIXY_TYPE_REQUEST_VERSION = 0x0e;
        public const int PIXY_TYPE_RESPONSE_VERSION = 0x0f;
        public const int PIXY_TYPE_RESPONSE_RESULT = 0x01;
        public const int PIXY_TYPE_RESPONSE_ERROR = 0x03;
        public const int PIXY_TYPE_REQUEST_BRIGHTNESS = 0x10;
        public const int PIXY_TYPE_REQUEST_SERVO = 0x12;
        public const int PIXY_TYPE_REQUEST_LED = 0x14;
        public const int PIXY_TYPE_REQUEST_LAMP = 0x16;

        public const int PIXY_RESULT_OK = 0;
        public const int PIXY_RESULT_ERROR = -1;
        public const int PIXY_RESULT_BUSY = -2;
        public const int PIXY_RESULT_CHECKSUM_ERROR = -3;
        public const int PIXY_RESULT_TIMEOUT = -4;
        public const int PIXY_RESULT_BUTTON_OVERRIDE = -5;


        public struct Version
        {
            public override string ToString()
            {
                return "hardware ver: " + hardware + " firmware ver: " + firmwareMajor + "." + firmwareMinor + "." + firmwareBuild + " " + firmwareType;
            }

            public uint hardware;
            public byte firmwareMajor;
            public byte firmwareMinor;
            public uint firmwareBuild;
            public string firmwareType;
        };


        public Pixy2CCC ccc;
        public uint frameWidth;
        public uint frameHeight;
        public Version version;
        //public Pixy2Line line;
        public LinkType m_link;

        public byte[] m_buf;
        public byte[] m_bufPayload;
        public byte m_type;
        public byte m_length;
        public bool m_cs;

        public TPixy2()
        {
            ccc = new Pixy2CCC(this);
            //line = new Pixy2Line(this);

            // allocate buffer space for send/receive
            m_buf = new byte[PIXY_BUFFERSIZE];
            m_bufPayload = new byte[PIXY_BUFFERSIZE - PIXY_SEND_HEADER_SIZE];
            // shifted buffer is used for sending, so we have space to write header information
            Array.Copy(m_buf, PIXY_SEND_HEADER_SIZE, m_bufPayload, 0, m_bufPayload.Length);

            frameWidth = frameHeight = 0;
            version = new Version();
        }

        ~TPixy2()
        {
            m_link.Close();
        }

        private long millis()
        {
            return DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
        }

        public int Init(CTRE.Gadgeteer.IPortUart portDef, int arg)
        {
            long t0;
            byte res;

            res = m_link.Open(portDef, arg);
            if (res < 0)
                return res;

            // wait for pixy to be ready -- that is, Pixy takes a second or 2 boot up
            // getVersion is an effective "ping".  We timeout after 5s.
            for (t0 = millis(); millis() - t0 < 5000;)
            {
                if (GetVersion() >= 0) // successful version get -> pixy is ready
                {
                    GetResolution(); // get resolution so we have it
                    return PIXY_RESULT_OK;
                }
            }
            // timeout
            return PIXY_RESULT_TIMEOUT;
        }


        public int GetSync()
        {
            byte i, j, cprev;
            byte[] c = new byte[1];
            int res;
            uint start;

            // parse bytes until we find sync
            for (i = j = 0, cprev = 0; true; i++)
            {
                res = m_link.Recv(c, 1);
                if (res >= PIXY_RESULT_OK)
                {
                    // since we're using little endian, previous byte is least significant byte
                    start = cprev;
                    // current byte is most significant byte
                    start |= ((uint)c[0] << 8);
                    cprev = c[0];
                    if (start == PIXY_CHECKSUM_SYNC)
                    {
                        m_cs = true;
                        return PIXY_RESULT_OK;
                    }
                    if (start == PIXY_NO_CHECKSUM_SYNC)
                    {
                        m_cs = false;
                        return PIXY_RESULT_OK;
                    }
                }
                // If we've read some bytes and no sync, then wait and try again.
                // And do that several more times before we give up.  
                // Pixy guarantees to respond within 100us.
                if (i >= 4)
                {
                    if (j >= 4)
                    {
#if PIXY_DEBUG
                        Debug.Print("error: no response");
#endif
                        return PIXY_RESULT_ERROR;
                    }
                    j++;
                    i = 0;
                }
            }
        }


        public int RecvPacket()
        {
            UInt16 csCalc, csSerial;
            int res;
            csCalc = csSerial = 0;
            res = GetSync();
            if (res < 0)
                return res;

            if (m_cs)
            {
                res = m_link.Recv(m_buf, 4);
                if (res < 0)
                    return res;

                m_type = m_buf[0];
                m_length = m_buf[1];

                csSerial = m_buf[2];
                csSerial |= (UInt16)(m_buf[3] << 8);

                res = m_link.Recv(m_buf, m_length, ref csCalc);
                if (res < 0)
                    return res;

                if (csSerial != csCalc)
                {
#if PIXY_DEBUG
                    Serial.println("error: checksum");
#endif
                    return PIXY_RESULT_CHECKSUM_ERROR;
                }
            }
            else
            {
                res = m_link.Recv(m_buf, 2);
                if (res < 0)
                    return res;

                m_type = m_buf[0];
                m_length = m_buf[1];

                res = m_link.Recv(m_buf, m_length);
                if (res < 0)
                    return res;
            }
            return PIXY_RESULT_OK;
        }


        public int SendPacket()
        {
            // write header info at beginnig of buffer
            m_buf[0] = PIXY_NO_CHECKSUM_SYNC & 0xff;
            m_buf[1] = PIXY_NO_CHECKSUM_SYNC >> 8;
            m_buf[2] = m_type;
            m_buf[3] = m_length;
            Array.Copy(m_bufPayload, 0, m_buf, 4, m_length);
            // send whole thing -- header and data in one call
            return m_link.Send(m_buf, (byte)(m_length + PIXY_SEND_HEADER_SIZE));
        }


        public int ChangeProg(char[] prog)
        {
            int res;

            // poll for program to change
            while (true)
            {
                Array.Copy(m_bufPayload, prog, PIXY_MAX_PROGNAME);
                m_length = PIXY_MAX_PROGNAME;
                m_type = PIXY_TYPE_REQUEST_CHANGE_PROG;
                SendPacket();
                if (RecvPacket() == 0)
                {
                    res = m_buf[0];
                    res |= m_buf[1] << 8;
                    res |= m_buf[2] << 16;
                    res |= m_buf[3] << 24;
                    if (res > 0)
                    {
                        GetResolution();  // get resolution so we have it
                        return PIXY_RESULT_OK; // success     
                    }
                }
                else
                    return PIXY_RESULT_ERROR;  // some kind of bitstream error
                System.Threading.Thread.Sleep(1);
            }
        }


        public int GetVersion()
        {
            m_length = 0;
            m_type = PIXY_TYPE_REQUEST_VERSION;
            SendPacket();
            if (RecvPacket() == 0)
            {
                if (m_type == PIXY_TYPE_RESPONSE_VERSION)
                {
                    version.hardware = m_buf[0];
                    version.hardware = (uint)m_buf[1] << 8;
                    version.firmwareMajor = m_buf[2];
                    version.firmwareMinor = m_buf[3];
                    version.firmwareBuild = m_buf[4];
                    string firmwareTypeString = "";
                    for (int i = 0; i < 10; i++)
                        firmwareTypeString += (char)m_buf[i + 5];
                    version.firmwareType = firmwareTypeString;
                    return m_length;
                }
                else if (m_type == PIXY_TYPE_RESPONSE_ERROR)
                    return PIXY_RESULT_BUSY;
            }

            return PIXY_RESULT_ERROR;  // some kind of bitstream error
        }


        public int GetResolution()
        {
            m_length = 1;
            m_bufPayload[0] = 0; // for future types of queries
            m_type = PIXY_TYPE_REQUEST_RESOLUTION;
            SendPacket();
            if (RecvPacket() == 0)
            {
                if (m_type == PIXY_TYPE_RESPONSE_RESOLUTION)
                {
                    frameWidth = m_buf[0];
                    frameWidth |= (uint)m_buf[1] << 8;
                    frameHeight = m_buf[2];
                    frameHeight |= (uint)m_buf[3] << 8;
                    return PIXY_RESULT_OK; // success
                }
                else
                    return PIXY_RESULT_ERROR;
            }
            else
                return PIXY_RESULT_ERROR;  // some kind of bitstream error
        }


        public int SetCameraBrightness(byte brightness)
        {
            ulong res;

            m_bufPayload[0] = brightness;
            m_length = 1;
            m_type = PIXY_TYPE_REQUEST_BRIGHTNESS;
            SendPacket();
            if (RecvPacket() == 0) // && m_type==PIXY_TYPE_RESPONSE_RESULT && m_length==4)
            {
                res = m_buf[0];
                res |= (ulong)m_buf[1] << 8;
                res |= (ulong)m_buf[2] << 16;
                res |= (ulong)m_buf[3] << 24;
                return (int)res;
            }
            else
                return PIXY_RESULT_ERROR;  // some kind of bitstream error
        }


        public int SetServos(uint s0, uint s1)
        {
            ulong res;

            m_bufPayload[0] = (byte)(s0 & 0xFF);
            m_bufPayload[1] = (byte)((s0 >> 8) & 0xFF);
            m_bufPayload[2] = (byte)(s1 & 0xFF);
            m_bufPayload[3] = (byte)((s1 >> 8) & 0xFF);
            m_length = 4;
            m_type = PIXY_TYPE_REQUEST_SERVO;
            SendPacket();
            if (RecvPacket() == 0 && m_type == PIXY_TYPE_RESPONSE_RESULT && m_length == 4)
            {
                res = m_buf[0];
                res |= (ulong)m_buf[1] << 8;
                res |= (ulong)m_buf[2] << 16;
                res |= (ulong)m_buf[3] << 24;
                return (int)res;
            }
            else
                return PIXY_RESULT_ERROR;  // some kind of bitstream error	  
        }


        public int SetLED(byte r, byte g, byte b)
        {
            ulong res;

            m_bufPayload[0] = r;
            m_bufPayload[1] = g;
            m_bufPayload[2] = b;
            m_length = 3;
            m_type = PIXY_TYPE_REQUEST_LED;
            SendPacket();
            if (RecvPacket() == 0 && m_type == PIXY_TYPE_RESPONSE_RESULT && m_length == 4)
            {
                res = m_buf[0];
                res |= (ulong)m_buf[1] << 8;
                res |= (ulong)m_buf[2] << 16;
                res |= (ulong)m_buf[3] << 24;
                return (int)res;
            }
            else
                return PIXY_RESULT_ERROR;  // some kind of bitstream error
        }

        public int SetLamp(byte upper, byte lower)
        {
            ulong res;

            m_bufPayload[0] = upper;
            m_bufPayload[1] = lower;
            m_length = 2;
            m_type = PIXY_TYPE_REQUEST_LAMP;
            SendPacket();
            if (RecvPacket() == 0 && m_type == PIXY_TYPE_RESPONSE_RESULT && m_length == 4)
            {
                res = m_buf[0];
                res |= (ulong)m_buf[1] << 8;
                res |= (ulong)m_buf[2] << 16;
                res |= (ulong)m_buf[3] << 24;
                return (int)res;
            }
            else
                return PIXY_RESULT_ERROR;  // some kind of bitstream error	
        }
    }
}
