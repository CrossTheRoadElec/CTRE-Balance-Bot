using System;
using Microsoft.SPOT;
using CTRE.Phoenix.Mechanical;
using CTRE.Phoenix.MotorControl;

namespace CTRE.Phoenix.Drive
{
    public class BalanceSensoredTank : Tank, IBalanceSensoredDrivetrain
    {
        RemoteSensoredGearbox _left;
        RemoteSensoredGearbox _right;
        RemoteSensoredGearbox[] _gearBoxes;


        /** Encoder heading properties */
        public float DistanceBetweenWheels { get; set; }
        public uint ticksPerRev { get; set; }
        public float wheelRadius { get; set; }
        float ScrubCoefficient = 0.9998f;

        /* Sensored Tank constructor (uses two gearboxes)*/
        public BalanceSensoredTank(RemoteSensoredGearbox left, RemoteSensoredGearbox right, bool leftInverted, bool rightInverted, float wheelRadius) : base(left, right, leftInverted, rightInverted)
        {
            _left = left;
            _right = right;
            _gearBoxes = new RemoteSensoredGearbox[] { _left, _right };

            if (wheelRadius < 0.01)
                Debug.Print("HERO: Wheel radius must be greater than 0.01");
            this.wheelRadius = wheelRadius;
        }
        public BalanceSensoredTank(SensoredGearbox left, SensoredGearbox right, bool leftInverted, bool rightInverted, float wheelRadius)
          : this((RemoteSensoredGearbox)left, (RemoteSensoredGearbox)right, leftInverted, rightInverted, wheelRadius)
        {
        }

        //------ Access motor controller  ----------//

        //------ Set output routines. ----------//
        public void BalanceSet(Styles.AdvancedStyle driveStyle, float forward, float turn)
        {
            /* calc the left and right demand */
            float l, r;
            Util.Split_1(forward, turn, out l, out r);
            if (l > 1)
                //Left side is saturated +, beef up right side by remainder amount
                r += (l - 1);
            else if (r > 1)
                //Right side is saturated +, beef up left side by remainder
                l += (r - 1);
            else if (l < -1)
                //Left side is saturated -, beef up right side by remainder
                r += (l + 1); 
            else if (r < -1)
                //Right side is saturated -, beef up left side by remainder
                l += (r + 1);

            /* apply it */
            _left.Set(ControlMode.PercentOutput, l);
            _right.Set(ControlMode.PercentOutput, r);
        }

        //----- general output shaping ------------------//
        public ErrorCode ConfigClosedloopRamp(float secondsFromNeutralToFull, int timeoutMs = 0)
        {
            /* clear code(s) */
            ErrorCode errorCode = ErrorCode.OK;
            /* call each GB and save error codes */
            foreach (var gb in _gearBoxes)
            {
                /*for this gearbox */
                errorCode = gb.ConfigClosedloopRamp(secondsFromNeutralToFull, timeoutMs);
                /* save the error for this GB */
                //_lastError.Push(errorCode);
            }
            /* return the first/worst one */
            return errorCode;// _lastError.LastError;
        }

        //------- sensor status --------- //
        public ErrorCode SetPosition(float sensorPos, int timeoutMs = 0)
        {
            /* clear code(s) */
            ErrorCode errorCode = ErrorCode.OK;
            /* call each GB and save error codes */
            foreach (var gb in _gearBoxes)
            {
                /*for this gearbox */
                errorCode = gb.SetPosition(sensorPos, timeoutMs);
            }
            /* return the first/worst one */
            return errorCode;
        }
        public float GetPosition()
        {
            float l = _left.GetPosition();
            float r = _right.GetPosition();
            return (l + r) * 0.5f;
        }
        public float GetVelocity()
        {
            float l = _left.GetVelocity();
            float r = _right.GetVelocity();

            return (l + r) * 0.5f;
        }

        public float GetSensorDerivedAngle()
        {
            float l = _left.GetPosition();
            float r = _right.GetPosition();

            if (wheelRadius < 0.01)
            {
                Debug.Print("HERO: Sensored Tank has too small of a wheel radius, cannot get heading");
                return 0;
            }

            if (ticksPerRev == 0)
            {
                Debug.Print("HERO: Sensored Tank has not set ticks per wheel revolution, cannot get heading");
                return 0;
            }

            if (DistanceBetweenWheels < 0.01)
            {
                Debug.Print("HERO: Sensored Tank has too small of a distance between wheels, cannot get heading");
                return 0;
            }

            float unitsPerTick = (float)(2 * System.Math.PI * wheelRadius) / ticksPerRev;
            float theta = ((r - l) / (DistanceBetweenWheels / unitsPerTick) * (float)(180 / System.Math.PI)) * ScrubCoefficient;

            return theta;
        }
        public float GetSensorDerivedAngularVelocity()
        {
            float l = _left.GetVelocity();
            float r = _right.GetVelocity();

            if (wheelRadius < 0.01)
            {
                Debug.Print("HERO: Sensored Tank has too small of a wheel radius, cannot get heading");
                return 0;
            }

            if (ticksPerRev == 0)
            {
                Debug.Print("HERO: Sensored Tank has not set ticks per wheel revolution, cannot get heading");
                return 0;
            }

            if (DistanceBetweenWheels < 0.01)
            {
                Debug.Print("HERO: Sensored Tank has too small of a distance between wheels, cannot get heading");
                return 0;
            }

            float unitsPerTick = (float)(2 * System.Math.PI * wheelRadius) / ticksPerRev;
            float theta = ((r - l) / (DistanceBetweenWheels / unitsPerTick) * (float)(180 / System.Math.PI)) * ScrubCoefficient;

            return theta;
        }
    }
}
