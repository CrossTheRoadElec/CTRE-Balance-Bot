using System;
using Microsoft.SPOT;
using CTRE.Phoenix.Motion;
using CTRE.Phoenix.MotorControl;

namespace CTRE.Phoenix.Drive
{
    public interface IBalanceSensoredDrivetrain : IDrivetrain
    {
        void BalanceSet(Styles.AdvancedStyle style, float forward, float turn);
        ErrorCode ConfigClosedloopRamp(float secondsFromNeutralToFull, int timeoutMs = 0);
        ErrorCode SetPosition(float sensorPos, int timeoutMs = 0);
        float GetPosition();
        float GetVelocity();
        float GetSensorDerivedAngle();
        float GetSensorDerivedAngularVelocity();
    }
}
