using Kliskatek.Driver.Rain.REDRCP.Transports;
using Newtonsoft.Json;

namespace Kliskatek.Driver.Rain.REDRCP.Demo
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("REDRCP ");

            var reader = new REDRCP();

            string connectionString = JsonConvert.SerializeObject(new SerialPortConnectionParameters
            {
                PortName = "COM4",
                BaudRate = 115200
            });

            if (!reader.Connect(connectionString))
                return;

            reader.NewNotificationReceived += NewNotificationReceived;
            reader.NewErrorReceived += NewErrorReceived;

            //var antiCollisionResult = reader.SetAntiCollisionMode(new AntiCollisionModeParameters
            //{
            //    Mode = AntiCollisionMode.Manual,
            //    QStart = 4,
            //    QMax = 4,
            //    QMin = 4
            //});
            
            reader.GetRegion(out var region);
            Console.WriteLine($"Reader region : {region}");

            // Example Read procedure
            string epc = "1234567890ABCDEF";
            ushort startAddress = 5;
            ushort wordCount = 2;
            switch (reader.ReadTypeCTagData(epc, ParamMemoryBank.User, startAddress, wordCount, out var readData))
            {
                case RcpResultType.Success:
                    Console.WriteLine($"Tag data : {readData}");
                    break;
                case RcpResultType.ReaderError:
                    // Option 1: Get last error code recorded by reader
                    var lastErrorCode = reader.GetLastError();
                    // Option 2: Get last error code recorded by reader associated with RCP command
                    var rcpCode = MessageCode.ReadTypeCTagData;
                    if (reader.TryGetCommandErrorCode(rcpCode, out var errorCode))
                        Console.WriteLine($"RCP command {rcpCode} returned error [{(byte)errorCode}] {errorCode}");
                    // User error handling
                    break;
                case RcpResultType.NoResponse:
                    // Timeout condition during RCP protocol command processing
                    break;
                case RcpResultType.OtherError:
                    // Other errors conditions that are not treated as RCP protocol failures
                    break;
            }

            // Example write procedure
            string dataToWrite = "FEDCBA0987654321";
            switch (reader.WriteTypeCTagData(epc, ParamMemoryBank.User, startAddress, dataToWrite))
            {
                case RcpResultType.Success:
                    Console.WriteLine("Tag written successfully");
                    break;
            }

            // Example inventory procedure
            reader.StartAutoRead2();
            Thread.Sleep(2000);
            reader.StopAutoRead2();

            if (!reader.Disconnect())
                Console.WriteLine("Reader is not disconnected");
        }

        public static void NewErrorReceived(object sender, ErrorNotificationEventArgs e)
        {
            Console.WriteLine($"Command {e.CommandCode} [{(byte)e.CommandCode}] returned an error: [{(byte)e.ErrorCode}] {e.ErrorCode}");
        }

        public static void NewNotificationReceived(object sender, NotificationEventArgs e)
        {
            switch (e.NotificationType)
            {
                case SupportedNotifications.ReadTypeCUii:
                    OnReadTypeCUiiNotification((ReadTypeCUiiNotificationParameters)e.NotificationParameters);
                    break;
                case SupportedNotifications.ReadTypeCUiiTid:
                    OnReadTypeCUiiTidNotification((ReadTypeCUiiTidNotificationParameters)e.NotificationParameters);
                    break;
                case SupportedNotifications.ReadTypeCUiiRssi:
                    OnReadTypeCUiiRssiNotification((ReadTypeCUiiRssiNotificationParameters)e.NotificationParameters);
                    break;
                case SupportedNotifications.StartAutoReadRssi:
                    OnStartAutoReadRssiNotification((StartAutoReadRssiNotificationParameters)e.NotificationParameters);
                    break;
                case SupportedNotifications.ReadTypeCUiiEx2:
                    OnReadTypeCUiiEx2Notification((ReadTypeCUiiEx2NotificationParameters)e.NotificationParameters);
                    break;
                case SupportedNotifications.StartAutoRead2Ex:
                    OnStartAutoRead2ExNotification((StartAutoRead2ExNotificationParameters)e.NotificationParameters);
                    break;
                case SupportedNotifications.GetDtcResult:
                    OnGetDtcResult((GetDtcResultNotificationParameters)e.NotificationParameters);
                    break;
                default:
                    Console.WriteLine($"Notification {e.NotificationType} not supported yet");
                    break;
            }
        }

        public static void OnReadTypeCUiiNotification(ReadTypeCUiiNotificationParameters parameters)
        {
            Console.WriteLine("ReadTypeCUii notification received");
            Console.WriteLine($" * [{parameters.Pc}] EPC = {parameters.Epc}\n");
        }

        public static void OnReadTypeCUiiTidNotification(ReadTypeCUiiTidNotificationParameters parameters)
        {
            Console.WriteLine("ReadTypeCUiiTid notification received");
            Console.WriteLine($" * [{parameters.Pc}] EPC = {parameters.Epc}, TID = {parameters.Tid}\n");
        }

        public static void OnReadTypeCUiiRssiNotification(ReadTypeCUiiRssiNotificationParameters parameters)
        {
            Console.WriteLine("ReadTypeCUiiRssi notification received");
            Console.WriteLine($" * [{parameters.Pc}] EPC = {parameters.Epc}");
            Console.WriteLine($"    - RSSI_I {parameters.RssiI}");
            Console.WriteLine($"    - RSSI_I {parameters.RssiQ}");
            Console.WriteLine($"    - GAIN_I {parameters.GainI}");
            Console.WriteLine($"    - GAIN_Q {parameters.GainQ}\n");
        }

        public static void OnStartAutoReadRssiNotification(StartAutoReadRssiNotificationParameters parameters)
        {
            Console.WriteLine("StartAutoReadRssi notification received");
            if (parameters.ReadComplete)
            {
                Console.WriteLine(" * Read completed\n");
            }
            else
            {
                Console.WriteLine(" * Read NOT completed\n");
            }
        }

        public static void OnReadTypeCUiiEx2Notification(ReadTypeCUiiEx2NotificationParameters parameters)
        {
            Console.WriteLine("ReadTypeCUiiEx2 notification received");
            Console.WriteLine($" * [{parameters.Pc}] EPC = {parameters.Epc}");
            Console.WriteLine($"    - RSSI {parameters.TagRssi}");
            Console.WriteLine($"    - AntennaPort {parameters.AntennaPort}");
            Console.WriteLine($"    - Mode {parameters.Mode}\n");
        }

        public static void OnStartAutoRead2ExNotification(StartAutoRead2ExNotificationParameters parameters)
        {
            Console.WriteLine("StartAutoRead2Ex notification received");
            if (parameters.ReadComplete)
            {
                Console.WriteLine(" * Read completed\n");
            }
            else
            {
                Console.WriteLine(" * Read NOT completed\n");
            }
        }

        public static void OnGetDtcResult(GetDtcResultNotificationParameters parameters)
        {
            Console.WriteLine("ReadTypeCUiiEx2 notification received");
            Console.WriteLine($"    - Inductor number {parameters.InductorNumber}");
            Console.WriteLine($"    - Digital tunable capacitor 1 {parameters.DigitalTunableCapacitor1}");
            Console.WriteLine($"    - Digital tunable capacitor 2 {parameters.DigitalTunableCapacitor2}");
            Console.WriteLine($"    - RSSI leakage {parameters.LeakageRssi}");
            Console.WriteLine($"    - Leakage cancellation algorithm state number {parameters.LeakageCancellationAlgorithmStateNumber}");
            Console.WriteLine($"    - Current channel {parameters.CurrentChannel}");
            Console.WriteLine($"    - Operation time of leakage cancellation {parameters.LeakageCancellationOperationTime}");
        }
    }
}
