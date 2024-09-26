using System.IO.Ports;
using Serilog;

namespace Kliskatek.REDRCP
{
    /// <summary>
    /// Provides higher level access to common RED RCP functionality. Works with serial port connection.
    /// </summary>
    public partial class REDRCP
    {
        private SerialPort _serialPort;

        public bool Connect(string serialPort)
        {
            try
            {
                _serialPort = new SerialPort();
                _serialPort.PortName = serialPort;
                _serialPort.BaudRate = 115200;
                _serialPort.Parity = Parity.None;
                _serialPort.DataBits = 8;
                _serialPort.StopBits = StopBits.One;
                _serialPort.Handshake = Handshake.None;
                _serialPort.ReadTimeout = 500;
                _serialPort.WriteTimeout = 500;

                return true;
            }
            catch (Exception e)
            {
                Log.Warning(e, "Exception thrown : ");
                return false;
            }
        }
    }
}
