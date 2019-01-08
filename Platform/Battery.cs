//Define what type of battery is being used
#define LEAD_ACID
//#define NI_MH

using System;
using Microsoft.SPOT;

namespace BalanceBot.Platform
{
    public class Battery
    {
#if LEAD_ACID
        private const float BadBatteryVoltage = 11.02f;
        private const float GoodBatteryVoltage = 11.53f;
#elif NI_MH
        private const float BadBatteryVoltage = 10.03f;
        private const float GoodBatteryVoltage = 10.52f;
#endif


        int _dnCnt = 0;
        int _upCnt = 0;
        bool batIsLow = false;

        public bool IsLow()
        {
            float vbat;
#if CTRE_EMULATE
            vbat = 12;
#else
            vbat = Hardware.leftTalon.GetBusVoltage();
#endif

            if (vbat > GoodBatteryVoltage)
            {
                _dnCnt = 0;
                if (_upCnt < 100)
                    ++_upCnt;
            }
            else if (vbat < BadBatteryVoltage)
            {
                _upCnt = 0;
                if (_dnCnt < 100)
                    ++_dnCnt;
            }

            if (_dnCnt > 50)
                batIsLow = true;
            else if (_upCnt > 50)
                batIsLow = false;
            else
            {
                //don't change filter ouput
            }
            return batIsLow;
        }

        public Battery() { }
    }
}
