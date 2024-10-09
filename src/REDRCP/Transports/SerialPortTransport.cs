using System.IO.Ports;
using Newtonsoft.Json;
using Serilog;

namespace Kliskatek.Driver.Rain.REDRCP.Transports
{
    public class SerialPortTransport : ITransport
    {
        private SerialPort _serialPort = new();
        private RxByteCallback? _rxByteCallback;
        private bool _isConnected = false;

        public bool Connect(string connectionString, RxByteCallback rxByteCallback)
        {
            try
            {
                _serialPort = new SerialPort();
                SerialPortConnectionParameters connectionParameters;
                try
                {
                    connectionParameters =
                        JsonConvert.DeserializeObject<SerialPortConnectionParameters>(connectionString);
                }
                catch (Exception e)
                {
                    connectionParameters = new SerialPortConnectionParameters { PortName = connectionString };
                }
                _serialPort.PortName = connectionParameters.PortName;
                _serialPort.BaudRate = connectionParameters.BaudRate;
                _serialPort.Parity = connectionParameters.Parity;
                _serialPort.DataBits = connectionParameters.DataBits;
                _serialPort.StopBits = connectionParameters.StopBits;
                _serialPort.Handshake = connectionParameters.Handshake;
                _serialPort.ReadTimeout = connectionParameters.ReadTimeout;
                _serialPort.WriteTimeout = connectionParameters.WriteTimeout;

                _serialPort.Open();
                _isConnected = _serialPort.IsOpen;
                if (_isConnected)
                {
                    _rxByteCallback = rxByteCallback;
                    _serialPort.DataReceived += OnSerialPortDataReceived;
                }
                    
                return _isConnected;
            }
            catch (Exception e)
            {
                Log.Warning(e, "Exception thrown : ");
                return false;
            }
        }

        public bool Disconnect()
        {
            try
            {
                _serialPort.Close();
                if (!_serialPort.IsOpen)
                {
                    _serialPort.DataReceived -= OnSerialPortDataReceived;
                    _isConnected = false;
                }
                return !_isConnected;
            }
            catch (Exception e)
            {
                Log.Warning(e, "Exception thrown : ");
                return false;
            }
        }

        public bool IsConnected
        {
            get { return _isConnected; }
        }

        public void TxByteList(List<byte> txByteList)
        {
            _serialPort.Write(txByteList.ToArray(), 0, txByteList.Count);
        }

        private readonly object _lockRxData = new();
        private void OnSerialPortDataReceived(object s, SerialDataReceivedEventArgs e)
        {
            lock (_lockRxData)
            {
                byte[] data = new byte[_serialPort.BytesToRead];
                _serialPort.Read(data, 0, data.Length);
                for (int i = 0; i < data.Length; i++)
                    _rxByteCallback(data[i]);
            }
        }
    }
}
