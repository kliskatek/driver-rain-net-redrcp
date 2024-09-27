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
        private int _autoRead2Ongoing = 0;
        private Thread _processRxBufferThread;

        private EpcCallback _epcCallback;

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

                ClearReceivedCommandAnswerBuffer();

                if (!_serialPort.IsOpen)
                {
                    Log.Warning($"Could not open serial port {serialPort}");
                    return false;
                }
                ResetRcpDecodeFsm();
                _serialPort.DataReceived += OnSerialPortDataReceived;

                _processRxBufferThread = new Thread(ProcessRxByteBuffer);
                Interlocked.Exchange(ref _runProcessRxBufferThread, 1);
                _processRxBufferThread.Start();

                return true;
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
                Interlocked.Exchange(ref _runProcessRxBufferThread, 0);
                _processRxBufferThread.Join();
                _serialPort.Close();
                return true;
            }
            catch (Exception e)
            {
                Log.Warning(e, "Exception thrown : ");
                return false;
            }
        }

        public bool GetReaderFirmwareVersion(out string firmwareVersion)
        {
            firmwareVersion = "";
            var fwVersion = ProcessCommand(MessageCode.GetReaderInformation, [(byte)ReaderInfoType.FwVersion]);
            if (fwVersion is null)
                return false;
            var fwVersionText = System.Text.Encoding.ASCII.GetString(fwVersion.ToArray());
            firmwareVersion = fwVersionText.Replace("\0", string.Empty);
            return true;
        }



        private List<byte>? ProcessCommand(MessageCode messageCode, List<byte>? commandPayload = null)
        {
            var command = AssembleRcpCommand(messageCode, commandPayload);
            command.WriteToSerialInterface(_serialPort);
            if (!_receivedCommandAnswerBuffer.TryTake(out var returnValue, 500))
                return null;
            if (returnValue.First() != (byte)messageCode)
            {
                ClearReceivedCommandAnswerBuffer();
                return null;
            }
            returnValue.RemoveAt(0);
            return returnValue;
        }
    }
}
