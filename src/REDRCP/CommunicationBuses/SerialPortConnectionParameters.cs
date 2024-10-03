using System.IO.Ports;

namespace Kliskatek.Driver.Rain.REDRCP.CommunicationBuses
{
    public class SerialPortConnectionParameters
    {
        public string PortName = string.Empty;
        public int BaudRate = 115200;
        public Parity Parity = Parity.None;
        public int DataBits = 8;
        public StopBits StopBits = StopBits.One;
        public Handshake Handshake = Handshake.None;
        public int ReadTimeout = 500;
        public int WriteTimeout = 500;
    }
}
