using CTRE;

namespace BalanceBot.Platform
{
    public static class Hardware
    {
        //Create our two motors
        public static CTRE.Phoenix.MotorControl.CAN.TalonSRX rightTalon = new CTRE.Phoenix.MotorControl.CAN.TalonSRX(1);
        public static CTRE.Phoenix.MotorControl.CAN.TalonSRX leftTalon = new CTRE.Phoenix.MotorControl.CAN.TalonSRX(2);
        public static CTRE.Phoenix.Mechanical.SensoredGearbox rightGearbox = new CTRE.Phoenix.Mechanical.SensoredGearbox(4096, rightTalon, CTRE.Phoenix.MotorControl.FeedbackDevice.CTRE_MagEncoder_Relative);
        public static CTRE.Phoenix.Mechanical.SensoredGearbox leftGearbox = new CTRE.Phoenix.Mechanical.SensoredGearbox(4096, leftTalon, CTRE.Phoenix.MotorControl.FeedbackDevice.CTRE_MagEncoder_Relative);

        //Create our drivetrain
        public static CTRE.Phoenix.Drive.BalanceSensoredTank drivetrain = new CTRE.Phoenix.Drive.BalanceSensoredTank(leftGearbox, rightGearbox, true, false, 4.25f / 2f);

        //Create Pigeon
        public static CTRE.Phoenix.Sensors.PigeonIMU pidgey = new CTRE.Phoenix.Sensors.PigeonIMU(0);
        //Create Gamepad
        public static CTRE.Phoenix.Controller.GameController Gamepad = new CTRE.Phoenix.Controller.GameController(CTRE.Phoenix.UsbHostDevice.GetInstance(0), 0);

        //Create display module for debugging
        public static CTRE.Gadgeteer.Module.BalanceDisplayModule Display = new CTRE.Gadgeteer.Module.BalanceDisplayModule(CTRE.HERO.IO.Port8, CTRE.Gadgeteer.Module.BalanceDisplayModule.OrientationType.Landscape);
        public static Microsoft.SPOT.Font smallFont = Properties.Resources.GetFont(Properties.Resources.FontResources.small);
        public static Microsoft.SPOT.Font bigFont = Properties.Resources.GetFont(Properties.Resources.FontResources.NinaB);

        //Create Pixy
        public static CTRE.Phoenix.Pixy2UART pixy = new CTRE.Phoenix.Pixy2UART(CTRE.HERO.IO.Port6, 115200);

        //Everything else
        public static Battery battery = new Platform.Battery();
    }
}
namespace CTRE.Phoenix
{
    public class BalanceServoParameters
    {
        public float P;
        public float I;
        public float D;
    }
}