using CTRE;
using System;
using System.Threading;
using Microsoft;
using BalanceBot.Platform;

/**
 * It is recommended that you run the Phoenix 5.6.0.0
 * installer to run this project, it will provide the
 * necessary firmware files and API for this project
 * http://www.ctr-electronics.com/installer-archive
 *
 * Firmware for all the devices on this are:
 *    TalonSRX   - 11.8 - 11.11
 *    PigeonIMU  - 0.41
 *	  Hero		 - 1.2.0.0
 */

namespace BalanceBot
{
    public class Program
    {
        /* Constants */
        const float maxOutput = 1;
        const float DegToRad = 0.01745329252f;
        const float RadToDeg = 57.2957795131f;

        /* I of PIDs */
        static float Iaccum = 0;
        static float accummax = 500;
        static float Iaccum_velocity = 0;
        static float accummax_velocity = 300;

        /* toggle to disable and enable robot */
        static bool lastButton1 = false;
        static bool OperateState = false;
        static bool manualMode = false;

        /* Pigeon Taring */
        static bool lastButton2 = false;
        static float pitchoffset = 0;

        /* PID control */
        static bool lastButton3 = false;
        static bool lastButton4 = false;
        static bool lastButton5 = false;
        static bool lastButton6 = false;
        static bool lastButton9 = false;
        static bool lastButton10 = false;
        static float PIDcycle = 0;
        static float inc_dec = 0.001f;


        static CTRE.Phoenix.BalanceServoParameters BalancePID = new CTRE.Phoenix.BalanceServoParameters
        {
            /* acute gains */
            P = 0.046f,
            I = 0.009f,
            D = 1.700f,
        };
        static CTRE.Phoenix.BalanceServoParameters ArbitraryGains = new CTRE.Phoenix.BalanceServoParameters
        {
            /* normal gains */
            P = 1.700f,
            I = 1.000f,
            D = 1.000f,
        };
        static CTRE.Phoenix.BalanceServoParameters VelocityPID = new CTRE.Phoenix.BalanceServoParameters
        {
            /* velocity gains */
            P = 0.011f,
            I = 0.050f,
            D = 0.010f,
        };

        public static void Main()
        {
            /* Initialize Display */
            CTRE.Gadgeteer.Module.BalanceDisplayModule.LabelSprite titleDisplay, pitchDisplay, outputDisplay, PIDBalanceDisplay, PIDVelocityDisplay,
                PIDDriveDisplay, PIDScalerDisplay, batteryDisplay, encoderDisplayLeft, encoderDisplayRight;

            /* State and battery display */
            titleDisplay = Hardware.Display.AddLabelSprite(Hardware.bigFont, CTRE.Gadgeteer.Module.BalanceDisplayModule.Color.Red, 1, 1, 80, 15);
            batteryDisplay = Hardware.Display.AddLabelSprite(Hardware.bigFont, CTRE.Gadgeteer.Module.BalanceDisplayModule.Color.Green, 80, 1, 80, 15);
            /* Pitch and output display on top right */
            pitchDisplay = Hardware.Display.AddLabelSprite(Hardware.bigFont, CTRE.Gadgeteer.Module.BalanceDisplayModule.Color.Cyan, 1, 21, 80, 15);
            outputDisplay = Hardware.Display.AddLabelSprite(Hardware.bigFont, CTRE.Gadgeteer.Module.BalanceDisplayModule.Color.Cyan, 80, 21, 80, 15);
            /* Gain Display at the bottom */
            PIDScalerDisplay = Hardware.Display.AddLabelSprite(Hardware.bigFont, CTRE.Gadgeteer.Module.BalanceDisplayModule.Color.Yellow, 1, 41, 80, 15);
            PIDBalanceDisplay = Hardware.Display.AddLabelSprite(Hardware.bigFont, CTRE.Gadgeteer.Module.BalanceDisplayModule.Color.White, 1, 61, 90, 15);
            PIDDriveDisplay = Hardware.Display.AddLabelSprite(Hardware.bigFont, CTRE.Gadgeteer.Module.BalanceDisplayModule.Color.White, 1, 81, 90, 15);
            PIDVelocityDisplay = Hardware.Display.AddLabelSprite(Hardware.bigFont, CTRE.Gadgeteer.Module.BalanceDisplayModule.Color.White, 1, 101, 90, 15);

            encoderDisplayRight = Hardware.Display.AddLabelSprite(Hardware.bigFont, CTRE.Gadgeteer.Module.BalanceDisplayModule.Color.Cyan, 90, 50, 10, 15);
            encoderDisplayLeft = Hardware.Display.AddLabelSprite(Hardware.bigFont, CTRE.Gadgeteer.Module.BalanceDisplayModule.Color.Cyan, 90, 90, 10, 15);

            /* Initialize drivetrain */
            Hardware.leftTalon.ConfigPeakCurrentLimit(15);
            Hardware.rightTalon.ConfigPeakCurrentLimit(15);
            Hardware.leftTalon.ConfigContinuousCurrentLimit(15);
            Hardware.rightTalon.ConfigContinuousCurrentLimit(15);
            Hardware.leftTalon.ConfigPeakCurrentDuration(0);
            Hardware.rightTalon.ConfigPeakCurrentDuration(0);
            Hardware.leftTalon.EnableCurrentLimit(true);
            Hardware.rightTalon.EnableCurrentLimit(true);
            Hardware.leftGearbox.SetSensorPhase(false);
            Hardware.rightTalon.ConfigVelocityMeasurementPeriod(CTRE.Phoenix.MotorControl.VelocityMeasPeriod.Period_10Ms);
            Hardware.rightTalon.ConfigVelocityMeasurementWindow(32);
            Hardware.leftTalon.ConfigVelocityMeasurementPeriod(CTRE.Phoenix.MotorControl.VelocityMeasPeriod.Period_10Ms);
            Hardware.leftTalon.ConfigVelocityMeasurementWindow(32);

            Hardware.leftTalon.ConfigNeutralDeadband(0.001f);
            Hardware.rightTalon.ConfigNeutralDeadband(0.001f);

            /* Speed up Pigeon frames */
            Hardware.leftTalon.SetStatusFramePeriod(CTRE.Phoenix.MotorControl.StatusFrame.Status_2_Feedback0_, 10, 10);
            Hardware.rightTalon.SetStatusFramePeriod(CTRE.Phoenix.MotorControl.StatusFrame.Status_2_Feedback0_, 10, 10);
            Hardware.leftTalon.SetStatusFramePeriod(CTRE.Phoenix.MotorControl.StatusFrameEnhanced.Status_3_Quadrature, 2, 10);
            Hardware.rightTalon.SetStatusFramePeriod(CTRE.Phoenix.MotorControl.StatusFrameEnhanced.Status_3_Quadrature, 2, 10);

            Hardware.pidgey.SetStatusFramePeriod(CTRE.Phoenix.Sensors.PigeonIMU_StatusFrame.BiasedStatus_2_Gyro, 5, 10);
            Hardware.pidgey.SetStatusFramePeriod(CTRE.Phoenix.Sensors.PigeonIMU_StatusFrame.CondStatus_9_SixDeg_YPR, 5, 10);

            CTRE.Phoenix.MotorControl.SensorCollection _leftSensors = Hardware.leftTalon.GetSensorCollection();
            CTRE.Phoenix.MotorControl.SensorCollection _rightSensors = Hardware.rightTalon.GetSensorCollection();

            float[] XYZ_Dps = new float[3];

            float tempP = 0;
            float tempI = 0;
            float tempD = 0;
            byte CurrentPID = 0;

            float lastVelocity = 0;
            CTRE.MP.MotionProfiler velocityRamp = new CTRE.MP.MotionProfiler(1000000, 6000, 0, 50); //Essentially infinite jerk, with a control on acceleration

            CTRE.Phoenix.Stopwatch loopTimer = new CTRE.Phoenix.Stopwatch();
            loopTimer.Start();
            uint lastOuterLoopTime = loopTimer.DurationMs;
            uint lastInnerLoopTime = loopTimer.DurationMs;
            float angleSetpoint = 0;
            uint loopCounter = 0;

            bool trackingMode = false;

            bool tippedOver = false;
            uint tipStart = 0;
            uint tipEnd = 0;

            bool pointTurnMode = false;
            while (true)
            {
                if (loopTimer.DurationMs < lastOuterLoopTime + 50)
                    continue;
                if (loopTimer.DurationMs - lastOuterLoopTime > 1000)
                    //Just reset the timer, it's way too far away now
                    lastOuterLoopTime = loopTimer.DurationMs;
                
                lastOuterLoopTime += 50;


                if (Hardware.Gamepad.GetConnectionStatus() == CTRE.Phoenix.UsbDeviceConnection.Connected)
                    CTRE.Phoenix.Watchdog.Feed();

                float stick = Hardware.Gamepad.GetAxis(1);
                float turn = Hardware.Gamepad.GetAxis(2);
                CTRE.Phoenix.Util.Deadband(ref stick);
                CTRE.Phoenix.Util.Deadband(ref turn);
                /* Grab values from gamepad */
                turn *= 0.5f;

                int xOffset = 0;
                int targetHeight = 0;
                bool disableForward = true;
                CTRE.Phoenix.Pixy2CCC.Block b = Hardware.pixy.ccc.OffsetBlock(ref disableForward, ref xOffset, ref targetHeight, 50, 2);
                if (b != null && trackingMode && !manualMode)
                {
                    if(!disableForward)
                        stick = (targetHeight - 25) * 0.015f;
                    turn = xOffset * 0.002f;
                }

                bool button10 = Hardware.Gamepad.GetButton(10);
                if(button10 && !lastButton10)
                {
                    trackingMode = !trackingMode;
                    Hardware.Display.Clear();
                    if(trackingMode)
                    {
                        Hardware.Display.AddRectSprite(CTRE.Gadgeteer.Module.BalanceDisplayModule.Color.Blue, 0, 0, 80, 130);
                        Hardware.Display.AddRectSprite(CTRE.Gadgeteer.Module.BalanceDisplayModule.Color.Blue, 80, 0, 80, 130);
                    }
                    else
                    {
                        titleDisplay = Hardware.Display.AddLabelSprite(Hardware.bigFont, CTRE.Gadgeteer.Module.BalanceDisplayModule.Color.Red, 1, 1, 80, 15);
                        batteryDisplay = Hardware.Display.AddLabelSprite(Hardware.bigFont, CTRE.Gadgeteer.Module.BalanceDisplayModule.Color.Green, 80, 1, 80, 15);
                        /* Pitch and output display on top right */
                        pitchDisplay = Hardware.Display.AddLabelSprite(Hardware.bigFont, CTRE.Gadgeteer.Module.BalanceDisplayModule.Color.Cyan, 1, 21, 80, 15);
                        outputDisplay = Hardware.Display.AddLabelSprite(Hardware.bigFont, CTRE.Gadgeteer.Module.BalanceDisplayModule.Color.Cyan, 80, 21, 80, 15);
                        /* Gain Display at the bottom */
                        PIDScalerDisplay = Hardware.Display.AddLabelSprite(Hardware.bigFont, CTRE.Gadgeteer.Module.BalanceDisplayModule.Color.Yellow, 1, 41, 80, 15);
                        PIDBalanceDisplay = Hardware.Display.AddLabelSprite(Hardware.bigFont, CTRE.Gadgeteer.Module.BalanceDisplayModule.Color.White, 1, 61, 90, 15);
                        PIDDriveDisplay = Hardware.Display.AddLabelSprite(Hardware.bigFont, CTRE.Gadgeteer.Module.BalanceDisplayModule.Color.White, 1, 81, 90, 15);
                        PIDVelocityDisplay = Hardware.Display.AddLabelSprite(Hardware.bigFont, CTRE.Gadgeteer.Module.BalanceDisplayModule.Color.White, 1, 101, 90, 15);

                        encoderDisplayRight = Hardware.Display.AddLabelSprite(Hardware.bigFont, CTRE.Gadgeteer.Module.BalanceDisplayModule.Color.Cyan, 90, 50, 10, 15);
                        encoderDisplayLeft = Hardware.Display.AddLabelSprite(Hardware.bigFont, CTRE.Gadgeteer.Module.BalanceDisplayModule.Color.Cyan, 90, 90, 10, 15);
                    }
                }
                lastButton10 = button10;

                /* Pitch offsetter */
                bool button2 = Hardware.Gamepad.GetButton(2);
                if (button2 && !lastButton2)
                {
                    pitchoffset = 0;
                    pitchoffset = GetPitch();
                }
                lastButton2 = button2;

                /* State control */
                bool button1 = Hardware.Gamepad.GetButton(1);
                if (button1 && !lastButton1)
                {
                    OperateState = !OperateState;
                    Iaccum = 0;
                    Iaccum_velocity = 0;
                }
                lastButton1 = button1;

                bool button4 = Hardware.Gamepad.GetButton(4);
                if (button4 && !lastButton4)
                {
                    CurrentPID++;
                    if (CurrentPID > 2)
                        CurrentPID = 0;
                }
                lastButton4 = button4;

                if (!trackingMode)
                {
                    if (CurrentPID == 0)
                    {
                        PIDcontrol_test(BalancePID);
                        PIDBalanceDisplay.SetColor(CTRE.Gadgeteer.Module.BalanceDisplayModule.Color.White);
                        PIDDriveDisplay.SetColor(CTRE.Gadgeteer.Module.BalanceDisplayModule.Color.Orange);
                        PIDVelocityDisplay.SetColor(CTRE.Gadgeteer.Module.BalanceDisplayModule.Color.Orange);
                    }
                    else if (CurrentPID == 1)
                    {
                        PIDcontrol_test(ArbitraryGains);
                        PIDBalanceDisplay.SetColor(CTRE.Gadgeteer.Module.BalanceDisplayModule.Color.Orange);
                        PIDDriveDisplay.SetColor(CTRE.Gadgeteer.Module.BalanceDisplayModule.Color.White);
                        PIDVelocityDisplay.SetColor(CTRE.Gadgeteer.Module.BalanceDisplayModule.Color.Orange);
                    }
                    else if (CurrentPID == 2)
                    {
                        PIDcontrol_test(VelocityPID);
                        PIDBalanceDisplay.SetColor(CTRE.Gadgeteer.Module.BalanceDisplayModule.Color.Orange);
                        PIDDriveDisplay.SetColor(CTRE.Gadgeteer.Module.BalanceDisplayModule.Color.Orange);
                        PIDVelocityDisplay.SetColor(CTRE.Gadgeteer.Module.BalanceDisplayModule.Color.White);
                    }
                    if (PIDcycle == 0)
                    {
                        PIDBalanceDisplay.SetText("B  P: " + BalancePID.P);
                        PIDDriveDisplay.SetText("D  P: " + ArbitraryGains.P);
                        PIDVelocityDisplay.SetText("V  P: " + VelocityPID.P);
                    }
                    else if (PIDcycle == 1)
                    {
                        PIDBalanceDisplay.SetText("B  I: " + BalancePID.I);
                        PIDDriveDisplay.SetText("D  I: " + ArbitraryGains.I);
                        PIDVelocityDisplay.SetText("V  I: " + VelocityPID.I);
                    }
                    else if (PIDcycle == 2)
                    {
                        PIDBalanceDisplay.SetText("B  D: " + BalancePID.D);
                        PIDDriveDisplay.SetText("D  D: " + ArbitraryGains.D);
                        PIDVelocityDisplay.SetText("V  D: " + VelocityPID.D);
                    }
                    PIDScalerDisplay.SetText("" + inc_dec);
                    batteryDisplay.SetText("Bat: " + Hardware.leftTalon.GetBusVoltage());
                }

                /* If Pigeon is connected and operation state lko09 */
                if (Hardware.pidgey.GetState() == CTRE.Phoenix.Sensors.PigeonState.Ready && OperateState == true)
                {
                    manualMode = false;
                }
                else {
                    manualMode = true;
                }


                /* Velocity setpoint pulled from gamepad throttle joystick */
                float velocitySetpoint = velocityRamp.Set(-stick * 1000.00f);


                /* Get pitch angular rate */
                Hardware.pidgey.GetRawGyro(XYZ_Dps);
                float pitchRate = -XYZ_Dps[1];
                int leftVel = 0;
                _leftSensors.GetQuadratureVelocity(out leftVel);
                int rightVel = 0;
                _rightSensors.GetQuadratureVelocity(out rightVel);
                float tempVelocity = -(-leftVel + rightVel) * 10 * 180 / 1920;  //  u/100ms converted into DPS
                                                                                /* Compensate for pitch angular rate when finding velocity */
                float robotVelocity = ((tempVelocity) + pitchRate);   //Keep in dps for easy math


                if ((turn > 0.4 || turn < -0.4) && ((velocitySetpoint < 100 && velocitySetpoint > -100) || pointTurnMode))
                {
                    //I'm doing a point turn, lock until I let go of turn
                    velocitySetpoint *= 0.1f;
                    pointTurnMode = true;
                }
                else
                    pointTurnMode = false;

                turn *= (float)Math.Abs(linearlyInterpolate(robotVelocity, 0, 2000, 1, 0f));
                turn *= (float)Math.Max(linearlyInterpolate((float)Math.Abs(GetPitch()), 0, 15, 1, 0f), 0);

                float velocityError = velocitySetpoint - robotVelocity;


                if (!trackingMode)
                {
                    if (leftVel > 0)
                        encoderDisplayLeft.SetText("+");
                    else if (leftVel < 0)
                        encoderDisplayLeft.SetText("|");
                    else
                        encoderDisplayLeft.SetText("o");

                    if (rightVel < 0)
                        encoderDisplayRight.SetText("+");
                    else if (rightVel > 0)
                        encoderDisplayRight.SetText("|");
                    else
                        encoderDisplayRight.SetText("o");
                }
                //=============================================================================================================================================//
                //=============================================================================================================================================//

                Iaccum_velocity += (velocityError);
                Iaccum_velocity = CTRE.Phoenix.Util.Cap(Iaccum_velocity, accummax_velocity);

                float velocityDerivative = robotVelocity - lastVelocity;
                lastVelocity = robotVelocity;

                float pValue_vel = velocityError * VelocityPID.P;
                pValue_vel = CTRE.Phoenix.Util.Cap(pValue_vel, 20);
                float iValue_vel = Iaccum_velocity * VelocityPID.I / 10;
                iValue_vel = CTRE.Phoenix.Util.Cap(iValue_vel, 20); //Cap I value to 20 degrees
                float dValue_vel = velocityDerivative * VelocityPID.D;
                angleSetpoint = -(pValue_vel + iValue_vel + dValue_vel);
                angleSetpoint = CTRE.Phoenix.Util.Cap(angleSetpoint, 15);


                byte[] frame2 = new byte[8];
                frame2[0] = (byte)((int)(robotVelocity * 10) >> 8);
                frame2[1] = (byte)((int)(robotVelocity * 10) & 0xFF);
                frame2[2] = (byte)((int)(velocitySetpoint / 10));
                frame2[3] = (byte)((int)(angleSetpoint * 100) >> 8);
                frame2[4] = (byte)((int)(angleSetpoint * 100) & 0xFF);
                frame2[5] = (byte)((int)(pValue_vel * 10));
                frame2[6] = (byte)((int)(iValue_vel * 10));
                frame2[7] = (byte)((int)(dValue_vel * 10));
                ulong data2 = (ulong)BitConverter.ToUInt64(frame2, 0);
                CTRE.Native.CAN.Send(8, data2, 8, 0);


                /* Balance PID, call 5 times per outer call */
                //=============================================================================================================================================//
                //=============================================================================================================================================//
                float pitchError = 0;
                lastInnerLoopTime = loopTimer.DurationMs;
                loopCounter = 0;
                while (loopCounter < 5)
                {

                    if (loopTimer.DurationMs < lastInnerLoopTime )
                        continue;
                    lastInnerLoopTime += 5;
                    loopCounter++;


                    Hardware.pidgey.GetRawGyro(XYZ_Dps);
                    float currentAngularRate = XYZ_Dps[1] * 0.001f;       //Scaled down for easier gain control

                    float currentPitch = GetPitch();

                    float targetPitch = angleSetpoint;
                    pitchError = targetPitch - currentPitch;

                    Iaccum += pitchError;
                    Iaccum = CTRE.Phoenix.Util.Cap(Iaccum, accummax);
                    
                    tempP = BalancePID.P * getGainTerm(robotVelocity); //Scale P term with velocity
                    tempI = BalancePID.I / 10;
                    tempD = BalancePID.D;

                    if(pointTurnMode)
                    {
                        //We are point turning so zero I and Scalar P
                        tempP = BalancePID.P;
                        tempI = 0;
                    }
                    
                    float pValue = (pitchError) * tempP;
                    float iValue = (Iaccum) * tempI;
                    float dValue = (currentAngularRate) * tempD;
                    iValue = CTRE.Phoenix.Util.Cap(iValue, 0.3f); //Cap I to 30% of max motor output
                    float Output = pValue - dValue + iValue;
                    

                    float outputVelocity = robotVelocity / 2000;
                    float eventHorizonForward;
                    float eventHorizonReverse;
                    if(outputVelocity > 0)
                    {
                        eventHorizonForward = linearlyInterpolate(outputVelocity, 0, 1, 60, 30);
                        eventHorizonReverse = linearlyInterpolate(outputVelocity, 0, 1, -60, -120);
                    }
                    else
                    {
                        eventHorizonForward = linearlyInterpolate(outputVelocity, 0, -1, 60, 120);
                        eventHorizonReverse = linearlyInterpolate(outputVelocity, 0, -1, -60, -30);
                    }


                    if (-currentPitch > eventHorizonForward || -currentPitch < eventHorizonReverse)
                    {
                        tipEnd = loopTimer.DurationMs;
                    }

                    if(loopTimer.DurationMs - tipStart > 200)
                    {
                        tippedOver = true;
                    }
                    if (loopTimer.DurationMs - tipEnd > 1000)
                    {
                        tippedOver = false;
                    }
                    if (tippedOver)
                    {
                        Output = 0;
                        Iaccum_velocity = 0;
                        Iaccum = 0;
                        manualMode = true;
                    }
                    /* Process output */
                    //=============================================================================================================================================//
                    //=============================================================================================================================================//
                    Output = CTRE.Phoenix.Util.Cap(Output, maxOutput);  //cap value from [-1, 1]

                    if (Hardware.battery.IsLow())
                    {
                        /* Scale all drivetrain inputs to 25% if battery is low */
                        batteryDisplay.SetColor(CTRE.Gadgeteer.Module.BalanceDisplayModule.Color.Red);
                        Output *= 0.25f;
                        stick *= 0.25f;
                        turn *= 0.25f;
                    }

                    
                    if (manualMode == false)
                    {
                        /* In balance mode, use PI -> PID -> Output */
                        DrivetrainSet(Output, turn, false);
                        if (!trackingMode) titleDisplay.SetText("Enabled");
                        if (!trackingMode) outputDisplay.SetText("Out: " + Output);
                    }
                    else
                    {
                        /* In maual mode/disabled, use joystick -> Output */
                        DrivetrainSet(stick, turn, true);
                        if (!trackingMode) titleDisplay.SetText("Disabled");
                        if (!trackingMode) outputDisplay.SetText("Out: " + stick);
                    }

                    /* Balance CAN Frame */
                    byte[] frame = new byte[8];
                    frame[0] = (byte)((int)(pValue * 1000) >> 8);
                    frame[1] = (byte)((int)(pValue * 1000) & 0xFF);
                    frame[2] = (byte)((int)(-dValue * 100000) >> 8);
                    frame[3] = (byte)((int)(-dValue * 100000) & 0xFF);
                    frame[4] = (byte)((int)(iValue * 1000) >> 8);
                    frame[5] = (byte)((int)(iValue * 1000) & 0xFF);
                    frame[6] = (byte)((int)(Output * 1000) >> 8);
                    frame[7] = (byte)((int)(Output * 1000) & 0xFF);
                    ulong data = (ulong)BitConverter.ToUInt64(frame, 0);
                    CTRE.Native.CAN.Send(9, data, 8, 0);
                }
                if(!trackingMode) pitchDisplay.SetText("P: " + GetPitch());


                //Thread.Sleep(5);
            }
        }

        /* PID for straigtness */
        static float KpGain = 0.02f;
        static float KdGain = 0.0004f;
        static float KMaxCorrectionRatio = 0.30f;
        static bool straightState = false;
        static float angleToHold = 0;

        /** Set the drivetrain and convert inputs to set voltage */
        public static void DrivetrainSet(float fset, float tset, bool manualMode)
        {
            if (tset == 0)
            {
                /* No turn value requested */
                if (straightState == false)
                {
                    /* Update yaw to hold for drive straight */
                    angleToHold = GetYaw();
                    straightState = true;
                }
                else if (straightState == true)
                {
                    /* Use the same angle until yaw changes from user */
                }
            }
            else
            {
                /* turn value not 0, drive normally */
                straightState = false;
            }
            straightState &= !manualMode;

            /* Get yaw angular rate */
            float[] XYZ_Dps = new float[3];
            Hardware.pidgey.GetRawGyro(XYZ_Dps);
            float yawRate = XYZ_Dps[2];

            /* Straight drive PD */
            //=============================================================================================================================================//
            //=============================================================================================================================================//
            float X = (angleToHold - GetYaw()) * KpGain - (yawRate) * KdGain;
            float MaxThrottle = MaxCorrection(fset, KMaxCorrectionRatio);       // Scale correction output based on throttle
            X = CTRE.Phoenix.Util.Cap(X, MaxThrottle);
            X *= -1;    // For our robot, invert the correction

            //=============================================================================================================================================//
            //=============================================================================================================================================//

            /* Selects which variable to use for our turn value */
            if (straightState == true)
                Hardware.drivetrain.BalanceSet(CTRE.Phoenix.Drive.Styles.AdvancedStyle.PercentOutput, fset, X);
            else if (straightState == false)
                Hardware.drivetrain.BalanceSet(CTRE.Phoenix.Drive.Styles.AdvancedStyle.PercentOutput, fset, tset);
        }

        /** Return the pitch with offset */
        public static float GetPitch()
        {
            float[] YPR = new float[3];
            Hardware.pidgey.GetYawPitchRoll(YPR);

            return YPR[2] - pitchoffset;
        }

        /** Return the yaw */
        public static float GetYaw()
        {
            float[] YPR = new float[3];
            Hardware.pidgey.GetYawPitchRoll(YPR);

            return YPR[0];
        }

        public static float linearlyInterpolate(float x, float x0, float x1, float y0, float y1)
        {
            if (x1 - x0 == 0) return y0 + y1 / 2;
            return y0 + (x - x0) * (y1 - y0) / (x1 - x0);
        }

        public static float interpolateMultiple(float x, float[] xD, float[] yD)
        {
            int i = xD.Length - 2; //Minus 1 because indexing at 0, minus 1 more because I take at current + 1
            for(; i > 0; i--)
            {
                if (x > xD[i]) break;
            }
            return linearlyInterpolate(x, xD[i], xD[i + 1], yD[i], yD[i + 1]);
        }

        public static float getGainTerm(float x)
        {
            return (ArbitraryGains.P * x * x * 0.0000001f) + 1;
        }

        /** PID gains setter */
        public static void PIDcontrol_test(CTRE.Phoenix.BalanceServoParameters PIDtoControl)
        {
            bool button3 = Hardware.Gamepad.GetButton(3);
            if (button3 && !lastButton3)
            {
                /* Select gain to display and control */
                PIDcycle++;
                if (PIDcycle > 2)
                    PIDcycle = 0;
            }
            lastButton3 = button3;

            bool button5 = Hardware.Gamepad.GetButton(5);
            bool button6 = Hardware.Gamepad.GetButton(6);
            if (button6 && !lastButton6)
            {
                /* Increment PID value */
                if (PIDcycle == 0)
                    PIDtoControl.P += inc_dec;
                else if (PIDcycle == 1)
                    PIDtoControl.I += inc_dec;
                else if (PIDcycle == 2)
                    PIDtoControl.D += inc_dec;
            }
            else if (button5 && !lastButton5)
            {
                /* Decrement PID value */
                if (PIDcycle == 0)
                    PIDtoControl.P -= inc_dec;
                else if (PIDcycle == 1)
                    PIDtoControl.I -= inc_dec;
                else if (PIDcycle == 2)
                    PIDtoControl.D -= inc_dec;
            }
            lastButton5 = button5;
            lastButton6 = button6;

            /* Force PID gains to be positive */
            if (PIDtoControl.P <= 0)
                PIDtoControl.P = 0;
            if (PIDtoControl.I <= 0)
                PIDtoControl.I = 0;
            if (PIDtoControl.D <= 0)
                PIDtoControl.D = 0;

            bool button9 = Hardware.Gamepad.GetButton(9);
            if (button9 && !lastButton9)
            {
                /* Increase the increment/decrement value by x10 */
                inc_dec *= 10;
                if (inc_dec >= 100)
                    inc_dec = 0.001f;
            }
            lastButton9 = button9;
        }

        /* Scales the scalor based on Y  value, which is the Joystick in our case */
        static float MaxCorrection(float Y, float Scalor)
        {
            Y = (float)System.Math.Abs(Y);
            Y *= Scalor;
            if (Y < 0.10)
                return 0.10f;
            return Y;
        }
    }
}