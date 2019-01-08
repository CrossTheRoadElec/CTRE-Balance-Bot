using System;
using Microsoft.SPOT;

namespace CTRE.Phoenix
{
    public class Pixy2CCC
    {
        private const int PIXY_BUFFERSIZE = 0x104;
        private const int PIXY_CHECKSUM_SYNC = 0xc1af;
        private const int PIXY_NO_CHECKSUM_SYNC = 0xc1ae;
        private const int PIXY_SEND_HEADER_SIZE = 4;
        private const int PIXY_MAX_PROGNAME = 33;

        private const int PIXY_TYPE_REQUEST_CHANGE_PROG = 0x02;
        private const int PIXY_TYPE_REQUEST_RESOLUTION = 0x0c;
        private const int PIXY_TYPE_RESPONSE_RESOLUTION = 0x0d;
        private const int PIXY_TYPE_REQUEST_VERSION = 0x0e;
        private const int PIXY_TYPE_RESPONSE_VERSION = 0x0f;
        private const int PIXY_TYPE_RESPONSE_RESULT = 0x01;
        private const int PIXY_TYPE_RESPONSE_ERROR = 0x03;
        private const int PIXY_TYPE_REQUEST_BRIGHTNESS = 0x10;
        private const int PIXY_TYPE_REQUEST_SERVO = 0x12;
        private const int PIXY_TYPE_REQUEST_LED = 0x14;
        private const int PIXY_TYPE_REQUEST_LAMP = 0x16;

        private const int PIXY_RESULT_OK = 0;
        private const int PIXY_RESULT_ERROR = -1;
        private const int PIXY_RESULT_BUSY = -2;
        private const int PIXY_RESULT_CHECKSUM_ERROR = -3;
        private const int PIXY_RESULT_TIMEOUT = -4;
        private const int PIXY_RESULT_BUTTON_OVERRIDE = -5;


        private const int CCC_MAX_SIGNATURE = 7;

        private const int CCC_RESPONSE_BLOCKS = 0x21;
        private const int CCC_REQUEST_BLOCKS = 0x20;

        // Defines for sigmap:
        // You can bitwise "or" these together to make a custom sigmap.
        // For example if you're only interested in receiving blocks
        // with signatures 1 and fiVe, you could use a sigmap of 
        // PIXY_SIG1 | PIXY_SIG5
        public const int CCC_SIG1 = 1;
        public const int CCC_SIG2 = 2;
        public const int CCC_SIG3 = 4;
        public const int CCC_SIG4 = 8;
        public const int CCC_SIG5 = 16;
        public const int CCC_SIG6 = 32;
        public const int CCC_SIG7 = 64;
        public const int CCC_COLOR_CODES = 128;

        private const int CCC_SIG_ALL = 0xff; // all bits or'ed together

        public class Block
        {
            // print block structure!
            public override string ToString()
            {
                int i;
                byte d;
                bool flag;
                if (m_signature > CCC_MAX_SIGNATURE) // color code! (CC)
                {
                    string sig = "";
                    // convert signature number to an octal string
                    for (i = 12, flag = false; i >= 0; i -= 3)
                    {
                        d = (byte)((m_signature >> i) & 0x07);
                        if (d > 0 && !flag)
                            flag = true;
                        if (flag)
                            sig += d.ToString();
                    }
                    sig += '\0';
                    return "CC-block signat: " + sig + " sig: " + m_signature + " x: " + m_x + " y: " + m_y + " width: " + m_width + " height: " + m_height + " angle: " + m_angle + " index: " + m_index + " age: " + m_age;
                }
                else // regular block.  Note, angle is always zero, so no need to print
                    return "sig: " + m_signature + " x: " + m_x + " y: " + m_y + " width: " + m_width + " height: " + m_height + " angle: " + m_angle + " index: " + m_index + " age: " + m_age;
            }

            public UInt16 m_signature;
            public UInt16 m_x;
            public UInt16 m_y;
            public UInt16 m_width;
            public UInt16 m_height;
            public Int16 m_angle;
            public byte m_index;
            public byte m_age;
        };

        private TPixy2 m_pixy;
        private byte m_goodIndex;
        private Block m_goodBlock;
        private CTRE.Phoenix.Stopwatch m_timeSinceLastGoodBlock;

        public uint numBlocks;
        public Block[] blocks;

        public Pixy2CCC(TPixy2 pixy)
        {
            m_pixy = pixy;
            blocks = new Block[64];
            m_timeSinceLastGoodBlock = new CTRE.Phoenix.Stopwatch();
        }

        public int ParseBlocks(byte sigmap = CCC_SIG_ALL, byte maxBlocks = 0xFF)
        {
            numBlocks = 0;

            // fill in request data
            m_pixy.m_bufPayload[0] = sigmap;
            m_pixy.m_bufPayload[1] = maxBlocks;
            m_pixy.m_length = 2;
            m_pixy.m_type = CCC_REQUEST_BLOCKS;

            // send request
            m_pixy.SendPacket();
            if (m_pixy.RecvPacket() == 0)
            {
                if (m_pixy.m_type == CCC_RESPONSE_BLOCKS)
                {
                    for (int i = 0; i < blocks.Length; i++) blocks[i] = null;

                    for (int i = 0; i < m_pixy.m_length - 13; i += 14)
                    {
                        blocks[numBlocks] = new Block();
                        blocks[numBlocks].m_signature = m_pixy.m_buf[0 + i];
                        blocks[numBlocks].m_signature |= (UInt16)(m_pixy.m_buf[1 + i] << 8);
                        blocks[numBlocks].m_x = m_pixy.m_buf[2 + i];
                        blocks[numBlocks].m_x |= (UInt16)(m_pixy.m_buf[3 + i] << 8);
                        blocks[numBlocks].m_y = m_pixy.m_buf[4 + i];
                        blocks[numBlocks].m_y |= (UInt16)(m_pixy.m_buf[5 + i] << 8);
                        blocks[numBlocks].m_width = m_pixy.m_buf[6 + i];
                        blocks[numBlocks].m_width |= (UInt16)(m_pixy.m_buf[7 + i] << 8);
                        blocks[numBlocks].m_height = m_pixy.m_buf[8 + i];
                        blocks[numBlocks].m_height |= (UInt16)(m_pixy.m_buf[9 + i] << 8);
                        blocks[numBlocks].m_angle = m_pixy.m_buf[10 + i];
                        blocks[numBlocks].m_angle |= (Int16)(m_pixy.m_buf[11 + i] << 8);
                        blocks[numBlocks].m_index = m_pixy.m_buf[12 + i];
                        blocks[numBlocks].m_age = m_pixy.m_buf[13 + i];

                        if (blocks[numBlocks].m_x > 315 || blocks[numBlocks].m_y > 207 ||
                            blocks[numBlocks].m_width > 315 || blocks[numBlocks].m_height > 207 ||
                            blocks[numBlocks].m_angle > 180 || blocks[numBlocks].m_angle < -180)
                            blocks[numBlocks] = null;
                        else
                             numBlocks++;
                    }
                    CTRE.Native.CAN.Send(0x330000, numBlocks, 0x1, 0); //Send number of blocks received by Pixy to CAN
                    return (int)numBlocks;
                }
                else if (m_pixy.m_type == PIXY_TYPE_RESPONSE_ERROR && m_pixy.m_buf[0] == unchecked((byte)PIXY_RESULT_BUSY))
                {
                    CTRE.Native.CAN.Send(0x330001, 0, 0, 0); //Send frame onto CAN bus showing we timed out
                    return PIXY_RESULT_BUSY; // new data not available yet
                }
                else // some other error, return as-is
                    return m_pixy.m_buf[0];
            }
            else
                return PIXY_RESULT_ERROR;  // some kind of bitstream error
        }
        public Block GetBlock()
        {
            ParseBlocks(CCC_SIG_ALL, 255);

            //Check every block to find the one I'm looking for
            foreach(Block b in blocks)
            {
                //Found it, return it
                if (b != null)
                {
                    if (b.m_index == m_goodIndex)
                    {
                        m_timeSinceLastGoodBlock.Start();
                        m_goodBlock = b;
                        m_goodIndex = b.m_index;
                        return b;
                    }
                }
                else
                    break; //Hit a null value so just break the loop
            }
            if (m_timeSinceLastGoodBlock.DurationMs > 100)
            {
                if (blocks[0] == null) return null;
                //Couldn't find it, use the largest block instead
                m_goodIndex = blocks[0].m_index;
                m_goodBlock = blocks[0];
                return blocks[0];
            }
            else
                return m_goodBlock;
        }

        public Block OffsetBlock(ref bool atBounds, ref int xOffset, ref int yOffset, int xBoundary = 5, int yBoundary = 5)
        {
            const int rightBoundary = 315;
            const int downBoundary = 207;


            Block b = GetBlock();
            if (b == null) return null;
            int leftPos = b.m_x - (b.m_width / 2);
            int rightPos = b.m_x + (b.m_width / 2);
            int topPos = b.m_y - (b.m_height / 2);
            int downPos = b.m_y + (b.m_height / 2);

            atBounds = (leftPos < xBoundary) || (rightPos > rightBoundary - xBoundary) ||
                (topPos < yBoundary || downPos > downBoundary - yBoundary);

            xOffset = b.m_x - (rightBoundary / 2);
            yOffset = b.m_height;

            return b;
        }
    }
}
