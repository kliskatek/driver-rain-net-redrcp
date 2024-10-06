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


            //for (int i = 0; i < 1; i++)
            //{
            //    reader.StartAutoRead2(AutoRead2DelegateMethod);

            //    Thread.Sleep(2000);

            //    reader.StopAutoRead2();

            //    Thread.Sleep(2000);
            //}

            if (!reader.Disconnect())
                Console.WriteLine("Serial port not disconnected");
        }

        public static void AutoRead2DelegateMethod(string pc, string epc)
        {
            Console.WriteLine($"EPC = {epc}, PC = {pc}");
        }
    }
}
