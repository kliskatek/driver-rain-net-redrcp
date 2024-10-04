﻿using Serilog;
using Kliskatek.Driver.Rain.REDRCP.CommunicationBuses;

namespace Kliskatek.Driver.Rain.REDRCP
{
    /// <summary>
    /// Provides higher level access to common RED RCP functionality. Works with serial port connection.
    /// </summary>
    public partial class REDRCP
    {
        private int _autoRead2Ongoing = 0;
        private AutoRead2NotificationCallback _autoRead2NotificationCallback;
        private IBus _communicationBus;

        public bool Connect(string connectionString)
        {
            try
            {
                var busType = GetBusTypeFromConnectionString(connectionString);
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

        private SupportedBuses GetBusTypeFromConnectionString(string connectionString)
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

        #region RCP commands
        
        #region 4.2 Get Reader Information

        public bool GetReaderInformation(byte commandArgument, out List<byte> responseArguments)
        {
            responseArguments = new List<byte>();
            responseArguments = ProcessRcpCommand(MessageCode.GetReaderInformation, [commandArgument]);
            if (responseArguments is null)
                return false;
            return (responseArguments.Count > 0);
        }

        public bool GetReaderInformationReaderModel(out string model)
        {
            model = string.Empty;
            var modelBinary = new List<byte>();
            if (!GetReaderInformation((byte)ReaderInfoType.Model, out modelBinary))
                return false;
            if (modelBinary.Count == 0)
                return false;
            model = System.Text.Encoding.ASCII.GetString(modelBinary.ToArray());
            return true;
        }
        
        public bool GetReaderInformationFirmwareVersion(out string firmwareVersion)
        {
            firmwareVersion = string.Empty;
            //var fwVersion = ProcessRcpCommand(MessageCode.GetReaderInformation, [(byte)ReaderInfoType.FwVersion]);
            //if (fwVersion is null)
            //    return false;
            //var fwVersionText = System.Text.Encoding.ASCII.GetString(fwVersion.ToArray());
            //firmwareVersion = fwVersionText.Replace("\0", string.Empty);
            //return true;
            if (!GetReaderInformation((byte)ReaderInfoType.FwVersion, out var firmwareBinary))
                return false;
            if (firmwareBinary.Count == 0)
                return false;
            firmwareVersion = System.Text.Encoding.ASCII.GetString(firmwareBinary.ToArray())
                .Replace("\0", string.Empty);
            return true;
        }

        public bool GetReaderInformationManufacturer(out string manufacturer)
        {
            manufacturer = string.Empty;
            if (!GetReaderInformation((byte)ReaderInfoType.Manufacturer, out var manufacturerBinary))
                return false;
            if (manufacturerBinary.Count == 0)
                return false;
            manufacturer = System.Text.Encoding.ASCII.GetString(manufacturerBinary.ToArray());
            return true;
        }

        public bool GetReaderInformationDetails(out List<byte> returnValue)
        {
            returnValue = new List<byte>();
            if (!GetReaderInformation((byte)ReaderInfoType.Detail, out var detailBinary))
                return false;
            if (detailBinary.Count < Constants.ReaderInformationDetailBinaryLength)
                return false;
            return true;
        }


        #endregion




        public bool StartAutoRead2(AutoRead2NotificationCallback callback)
        {
            try
            {
                _autoRead2NotificationCallback = callback;
                Interlocked.Exchange(ref _autoRead2Ongoing, 1);
                var result = ProcessRcpCommand(MessageCode.StartAutoRead2);
                if ((result is not null) && (result.Count == 1) && (result[Constants.ResponseArgOffset] == 0x00))
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
                var result = ProcessRcpCommand(MessageCode.StopAutoRead2);
                if ((result is not null) && (result.Count == 1) && (result[Constants.ResponseArgOffset] == 0x00))
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
        #endregion

        private List<byte>? ProcessRcpCommand(MessageCode messageCode, List<byte>? commandPayload = null)
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
