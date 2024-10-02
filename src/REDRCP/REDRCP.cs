using Serilog;
using System.IO.Ports;

namespace Kliskatek.Driver.Rain.REDRCP
{
    /// <summary>
    /// Provides higher level access to common RED RCP functionality. Works with serial port connection.
    /// </summary>
    public partial class REDRCP
    {
        private SerialPort _serialPort = new();
        private int _autoRead2Ongoing = 0;

        private AutoRead2NotificationCallback _autoRead2NotificationCallback;

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

                _serialPort.Open();

                ClearReceivedCommandAnswerBuffer();

                if (!_serialPort.IsOpen)
                {
                    Log.Warning($"Could not open serial port {serialPort}");
                    return false;
                }
                ResetRcpDecodeFsm();
                _serialPort.DataReceived += OnSerialPortDataReceived;

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
                _serialPort.Close();
                return !_serialPort.IsOpen;
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

        public bool StartAutoRead2(AutoRead2NotificationCallback callback)
        {
            try
            {
                _autoRead2NotificationCallback = callback;
                Interlocked.Exchange(ref _autoRead2Ongoing, 1);
                var result = ProcessCommand(MessageCode.StartAutoRead2);
                if ((result is not null) && (result.Count == 1) && (result[0] == 0x00))
                    return true;
                Interlocked.Exchange(ref _autoRead2Ongoing, 0);
                return false;

            }
            catch (Exception e)
            {
                Log.Warning(e, "Exception thrown");
                Interlocked.Exchange(ref _autoRead2Ongoing, 0);
                return false;
            }
        }

        public bool StopAutoRead2()
        {
            try
            {
                var result = ProcessCommand(MessageCode.StopAutoRead2);
                if ((result is not null) && (result.Count == 1) && (result[0] == 0x00))
                {
                    Interlocked.Exchange(ref _autoRead2Ongoing, 0);
                    return true;
                }

                return false;
            }
            catch (Exception e)
            {
                Log.Warning(e, "Exception thrown");
                return false;
            }
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
