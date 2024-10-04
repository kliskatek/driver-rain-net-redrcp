using System.Text.Json.Serialization;
using Kliskatek.Driver.Rain.REDRCP.CommunicationBuses;
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
                Console.WriteLine("KK");



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
