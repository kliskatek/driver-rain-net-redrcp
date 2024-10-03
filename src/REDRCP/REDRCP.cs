using Serilog;
using Kliskatek.Driver.Rain.REDRCP.CommunicationBuses;

namespace Kliskatek.Driver.Rain.REDRCP
{
    /// <summary>
    /// Provides higher level access to common RED RCP functionality. Works with serial port connection.
    /// </summary>
    public partial class REDRCP
    {
        //private SerialPort _serialPort = new();
        private int _autoRead2Ongoing = 0;

        private AutoRead2NotificationCallback _autoRead2NotificationCallback;

        private IBus _communicationBus;

        public bool Connect(string connectionString)
        {
            try
            {
                var busType = GetConnectionStringBusType(connectionString);
                switch (busType)
                {
                    case SupportedBuses.SerialPort:
                        _communicationBus = (IBus)new SerialPortBus();
                        break;
                    default:
                        throw new ArgumentException($"Bus type {busType} is not supported");
                }

                ClearReceivedCommandAnswerBuffer();

                if (!_communicationBus.Connect(connectionString, OnCommunicationBusByteReceived))
                {
                    Log.Warning($"Could not connect to communication bus of type {busType}");
                    return false;
                }
                ResetRcpDecodeFsm();

                return true;
            }
            catch (Exception e)
            {
                Log.Warning(e, "Exception thrown : ");
                return false;
            }
        }

        private SupportedBuses GetConnectionStringBusType(string connectionString)
        {
            // Check if connection string can be parsed into SerialPortConnectionParameters
            if (connectionString.TryParseJson<SerialPortConnectionParameters>())
                return SupportedBuses.SerialPort;
            throw new ArgumentException("Connection string can not be parsed into a known format");
        }

        public bool Disconnect()
        {
            try
            {
                return _communicationBus.Disconnect();
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
            _communicationBus.TxByteList(command);
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
