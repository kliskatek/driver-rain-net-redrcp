namespace Kliskatek.Driver.Rain.REDRCP.Demo
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello, World!");

            var reader = new REDRCP();

            if (!reader.Connect("COM4"))
                return;

            if (reader.GetReaderFirmwareVersion(out var firmwareVersion))
                Console.WriteLine($"Firmware version = {firmwareVersion}");

            for (int i = 0; i < 1; i++)
            {
                reader.StartAutoRead2(AutoRead2DelegateMethod);

                Thread.Sleep(2000);

                reader.StopAutoRead2();

                Thread.Sleep(2000);
            }

            if (!reader.Disconnect())
                Console.WriteLine("Serial port not disconnected");
        }

        public static void AutoRead2DelegateMethod(string pc, string epc)
        {
            Console.WriteLine($"EPC = {epc}, PC = {pc}");
        }
    }
}
