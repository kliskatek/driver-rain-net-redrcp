using System.Text.Json.Serialization;
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

            var connectionString = JsonConvert.SerializeObject(new SerialPortConnectionParameters
            {
                PortName = "COM4"
            });

            if (!reader.Connect(connectionString))
                return;
            reader.OnNotificationReceived += OnNotificationReceived;

            if (reader.GetReaderInformationFirmwareVersion(out var firmwareVersion))
                Console.WriteLine($"Firmware version = {firmwareVersion}");

            if (reader.GetReaderInformationReaderModel(out var modelName))
                Console.WriteLine($"Model name = {modelName}");

            if (reader.GetReaderInformationManufacturer(out var manufacturer))
                Console.WriteLine($"Manufacturer = {manufacturer}");

            if (reader.GetReaderInformationDetails(out var details))
                Console.WriteLine("Reader details obtained");

            if (reader.GetRegion(out var region))
                Console.WriteLine($"Reader region : {region}");

            if (reader.GetTypeCaiQueryParameters(out var queryParameters))
                Console.WriteLine("Query parameters obtained");

            if (reader.GetRfChannel(out var rfChannel))
                Console.WriteLine($"CN = {rfChannel.ChannelNumber}, CNO = {rfChannel.ChannelNumberOffset}");

            if (reader.GetFhLbtParameters(out var fhLbtParameters))
                Console.WriteLine("FH and LBT parameters obtained");

            //if (reader.ReadTypeCTagData("E2003411B802011526370494", ParamMemoryBank.Reserved, 0, 4, out var readData, 1))
            //    Console.WriteLine($"Read data : {readData}");

            if (reader.GetFrequencyHoppingTable(out var frequencyHoppingTable))
            {
                Console.WriteLine("Frequency hopping table:");
                foreach (var channel in frequencyHoppingTable)
                    Console.WriteLine($"  * Channel {channel}");
            }

            if (reader.GetTxPowerLevel(out var txPowerLevel))
                Console.WriteLine(
                    $"Reader power levels: current level {txPowerLevel.CurrentTxPower}, min level {txPowerLevel.MinTxPower}, max level {txPowerLevel.MaxTxPower}");

            if (reader.GetModulationMode(out var modulationMode))
                Console.WriteLine(
                    $"Modulation mode: BLF {modulationMode.BackscatterLinkFrequency}, RxMod {modulationMode.RxMod}, DR {modulationMode.Dr}");

            if (reader.GetAntiCollisionMode(out var anticollisionMode))
                Console.WriteLine(
                    $"Anti-collision mode : mode {anticollisionMode.Mode}, Q Start {anticollisionMode.QStart}, Q Max {anticollisionMode.QMax}, Q Min {anticollisionMode.QMin}");


            for (int i = 0; i < 1; i++)
            {
                reader.StartAutoRead2();

                Thread.Sleep(2000);

                reader.StopAutoRead2();

                Thread.Sleep(2000);
            }

            //byte maxElapsedTimeS = 5;
            //if (reader.StartAutoRead2Ex(ParamAutoRead2ExMode.EpcOnly, false, 1, 0, 0, 100, AutoRead2ExDelegateMethod))
            //{
            //    Console.WriteLine("StartAutoRead2Ex started");
            //    Thread.Sleep((int)maxElapsedTimeS * 1100);
            //}

            //if (reader.GetFrequencyInformation(out var frequencyInformation))
            //    Console.WriteLine(
            //        $"Frequency Information: Spacing {frequencyInformation.Spacing}, Start Frequency {frequencyInformation.StartFreq}, Channel {frequencyInformation.Channel}, RF Preset {frequencyInformation.RfPreset}");


            if (!reader.Disconnect())
                Console.WriteLine("Serial port not disconnected");
        }

        public static void OnNotificationReceived(object sender, NotificationEventArgs e)
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
