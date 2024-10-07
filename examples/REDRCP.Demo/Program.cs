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

            if (!reader.SetSystemReset())
            {
                Console.WriteLine("Could not reset system. Stopping execution");
                return;
            }

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


            //FhLbtParameters tmp = new FhLbtParameters
            //{
            //    DwellTime = 400,
            //    IdleTime = 100,
            //    CarrierSenseTime = 10,
            //    TargetRfPowerLevel = -74,
            //    Fh = true,
            //    Lbt = true,
            //    Cw = false
            //};
            //reader.SetFhLbtParameters(tmp);

            if (reader.GetTxPowerLevel(out var txPowerLevel))
                Console.WriteLine(
                    $"Reader power levels: current level {txPowerLevel.CurrentTxPower}, min level {txPowerLevel.MinTxPower}, max level {txPowerLevel.MaxTxPower}");

            if (reader.GetModulationMode(out var modulationMode))
                Console.WriteLine(
                    $"Modulation mode: BLF {modulationMode.BackscatterLinkFrequency}, RxMod {modulationMode.RxMod}, DR {modulationMode.Dr}");

            if (reader.GetAntiCollisionMode(out var anticollisionMode))
                Console.WriteLine(
                    $"Anti-collision mode : mode {anticollisionMode.Mode}, Q Start {anticollisionMode.QStart}, Q Max {anticollisionMode.QMax}, Q Min {anticollisionMode.QMin}");


            //for (int i = 0; i < 1; i++)
            //{
            //    reader.StartAutoRead2(AutoRead2DelegateMethod);

            //    Thread.Sleep(2000);

            //    reader.StopAutoRead2();

            //    Thread.Sleep(2000);
            //}

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

        public static void AutoRead2DelegateMethod(string pc, string epc)
        {
            Console.WriteLine($"EPC = {epc}, PC = {pc}");
        }

        public static void AutoRead2ExDelegateMethod(ParamAutoRead2ExMode mode, bool tagRssi, byte antPort,
            string pc, string epc, byte rssiI, byte rssiQ, byte gainI, byte gainQ)
        {
            Console.WriteLine($"[{pc}] EPC = {epc}");
            if (tagRssi)
                Console.WriteLine($"RSSI_I = {rssiI}, RSSI_Q = {rssiQ}, GAIN_I = {gainI}, GAIN_Q = {gainQ}");
        }
    }
}
