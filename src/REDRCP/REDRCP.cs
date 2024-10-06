using System.Buffers.Binary;
using System.Diagnostics;
using Kliskatek.Driver.Rain.REDRCP.Transports;
using Serilog;

namespace Kliskatek.Driver.Rain.REDRCP
{
    /// <summary>
    /// Provides higher level access to common RED RCP functionality. Works with serial port connection.
    /// </summary>
    public partial class REDRCP
    {
        private int _autoRead2Ongoing = 0;
        private AutoRead2NotificationCallback _autoRead2NotificationCallback;
        private ITransport _communicationTransport;

        public bool Connect(string connectionString)
        {
            try
            {
                var busType = GetBusTypeFromConnectionString(connectionString);
                switch (busType)
                {
                    case SupportedBuses.SerialPort:
                        _communicationTransport = (ITransport)new SerialPortTransport();
                        break;
                    default:
                        throw new ArgumentException($"Bus type {busType} is not supported");
                }

                ClearReceivedCommandAnswerBuffer();

                if (!_communicationTransport.Connect(connectionString, OnCommunicationBusByteReceived))
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
                return _communicationTransport.Disconnect();
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
            if (ProcessRcpCommand(MessageCode.GetReaderInformation, out responseArguments, 
                    [commandArgument]) != RcpReturnType.Success)
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

        public bool GetReaderInformationDetails(out ReaderInformationDetails details)
        {
            details = new ReaderInformationDetails();
            if (!GetReaderInformation((byte)ReaderInfoType.Detail, out var detailBinary))
                return false;
            if (detailBinary.Count < Constants.ReaderInformationDetailBinaryLength)
                return false;
            var detailBinaryArray = detailBinary.ToArray();
            // Parse details
            details.Region = (Region)detailBinaryArray[Constants.RidRegionOffset];
            details.Channel = detailBinaryArray[Constants.RidChannelOffset];
            details.MergeTime =
                BinaryPrimitives.ReadUInt16BigEndian(GetArraySlice(detailBinaryArray, Constants.RidMergeTimeOffset,
                    sizeof(UInt16)));
            details.IdleTime =
                BinaryPrimitives.ReadUInt16BigEndian(GetArraySlice(detailBinaryArray, Constants.RidIdleTimeOffset,
                    sizeof(UInt16)));
            details.CwSenseTime =
                BinaryPrimitives.ReadUInt16BigEndian(GetArraySlice(detailBinaryArray, Constants.RidCwSenseTimeOffset,
                    sizeof(UInt16)));
            details.LbtRfLevel =
                (double)(BinaryPrimitives.ReadInt16BigEndian(GetArraySlice(detailBinaryArray,
                    Constants.RidLbtRfLevelOffset, sizeof(Int16)))) / 10.0;
            details.CurrentTxPower =
                (double)(BinaryPrimitives.ReadInt16BigEndian(GetArraySlice(detailBinaryArray,
                    Constants.RidCurrentTxPowerOffset, sizeof(Int16)))) / 10.0;
            details.MinTxPower =
                (double)(BinaryPrimitives.ReadInt16BigEndian(GetArraySlice(detailBinaryArray,
                    Constants.RidMinTxPowerOffset, sizeof(Int16)))) / 10.0;
            details.MaxTxPower =
                (double)(BinaryPrimitives.ReadInt16BigEndian(GetArraySlice(detailBinaryArray,
                    Constants.RidMaxTxPowerOffset, sizeof(Int16)))) / 10.0;
            details.Blf =
                BinaryPrimitives.ReadUInt16BigEndian(GetArraySlice(detailBinaryArray, Constants.RidBlfOffset,
                    sizeof(UInt16)));
            details.Modulation = (ParamModulation)detailBinaryArray[Constants.RidModulationOffset];
            details.Dr = (ParamDr)detailBinaryArray[Constants.RidDrOffset];

            return true;
        }
        #endregion

        #region 4.3 Get Region

        public bool GetRegion(out Region region)
        {
            region = Region.Europe;
            if (ProcessRcpCommand(MessageCode.GetRegion, out var responseArguments) != RcpReturnType.Success)
                return false;
            if (responseArguments.Count != 1)
                return false;
            region = (Region)responseArguments[Constants.ResponseArgOffset];
            return true;
        }

        #endregion

        #region 4.4 Set Region

        public bool SetRegion(Region region)
        {
            if (ProcessRcpCommand(MessageCode.SetRegion, out var responseArguments, [(byte)region]) !=
                RcpReturnType.Success)
                return false;
            return ParseSingleByteResponsePayload(responseArguments);
        }
        #endregion

        #region 4.5 Set System Reset

        public bool SetSystemReset()
        {
            if (ProcessRcpCommand(MessageCode.SetSystemReset, out var responseArguments) != RcpReturnType.Success)
                return false;
            return ParseSingleByteResponsePayload(responseArguments);
        }

        #endregion

        #region 4.6 Get Type C A/I Query Parameters

        public bool GetTypeCaiQueryParameters(out TypeCaiQueryParameters parameters)
        {
            parameters = new TypeCaiQueryParameters();
            if (ProcessRcpCommand(MessageCode.GetTypeQueryRelatedParameters, out var responseArguments) !=
                RcpReturnType.Success)
                return false;
            if (responseArguments.Count != 2)
                return false;

            // Decode parameters in MSB
            var resultByte = responseArguments.First();

            parameters.Session = (ParamSession)(resultByte & 0x03);
            resultByte = (byte)(resultByte >> 2);
            parameters.Sel = (ParamSel)(resultByte & 0x03);
            resultByte = (byte)(resultByte >> 2);
            parameters.TRext = (resultByte & 0x01) > 0;
            resultByte = (byte)(resultByte >> 1);
            parameters.Modulation = (ParamModulation)(resultByte & 0x03);
            resultByte = (byte)(resultByte >> 2);
            parameters.Dr = (ParamDr)resultByte;

            // Decode parameters in LSB
            resultByte = responseArguments.Last();
            parameters.Toggle = (ParamToggle)(resultByte & 0x07);
            resultByte = (byte)(resultByte >> 3);
            parameters.Q = (uint)(resultByte & 0x0F);
            resultByte = (byte)(resultByte >> 4);
            parameters.Target = (ParamTarget)(resultByte & 0x01);

            return true;
        }

        #endregion

        #region 4.7 Set Type C A/I Query Parameters

        public bool SetTypeCaiQueryParameters(TypeCaiQueryParameters parameters)
        {
            var arguments = new List<byte>();

            // Encode first byte
            byte msb = 0;
            msb += (byte)parameters.Dr;
            msb = (byte)(msb << 2);
            msb += (byte)parameters.Modulation;
            msb = (byte)(msb << 1);
            msb += (byte)(parameters.TRext ? 1 : 0);
            msb = (byte)(msb << 2);
            msb += (byte)parameters.Sel;
            msb = (byte)(msb << 2);
            msb += (byte)parameters.Session;
            arguments.Add(msb);
            // Encode second byte
            byte lsb = 0;
            lsb += (byte)(parameters.Target);
            lsb = (byte)(lsb << 4);
            lsb += (byte)((((byte)parameters.Q) & 0x0F));
            lsb = (byte)(lsb << 3);
            lsb += (byte)parameters.Toggle;
            arguments.Add(lsb);

            if (ProcessRcpCommand(MessageCode.SetTypeQueryRelatedParameters, out var responseArguments, arguments) !=
                RcpReturnType.Success)
                return false;
            return ParseSingleByteResponsePayload(responseArguments);
        }

        #endregion

        #region 4.8 Get RF Channel

        public bool GetRfChannel(out RfChannel channel)
        {
            channel = new RfChannel();
            if (ProcessRcpCommand(MessageCode.GetRfChannel, out var responseArguments) != RcpReturnType.Success)
                return false;
            if (responseArguments.Count != 2)
                return false;
            channel.ChannelNumber = responseArguments[0];
            channel.ChannelNumberOffset = responseArguments[1];
            return true;
        }

        #endregion

        #region 4.9 Set RF Channel

        public bool SetRfChannel(RfChannel channel)
        {
            var arguments = new List<Byte>();
            arguments.Add(channel.ChannelNumber);
            arguments.Add(channel.ChannelNumberOffset);
            if (ProcessRcpCommand(MessageCode.SetRfChannel, out var responseArguments, arguments) !=
                RcpReturnType.Success)
                return false;
            return ParseSingleByteResponsePayload(responseArguments);
        }

        #endregion

        #region 4.10 Get FH and LBT Parameters

        public bool GetFhLbtParameters(out FhLbtParameters parameters)
        {
            parameters = new FhLbtParameters();
            if (ProcessRcpCommand(MessageCode.GetFhLbtParameters, out var responseArguments) != RcpReturnType.Success)
                return false;
            if (responseArguments.Count != 11)
                return false;
            var responseArgumentsArray = responseArguments.ToArray();
            parameters.DwellTime =
                BinaryPrimitives.ReadUInt16BigEndian(GetArraySlice(responseArgumentsArray, Constants.FlpDtOffset,
                    sizeof(UInt16)));
            parameters.IdleTime =
                BinaryPrimitives.ReadUInt16BigEndian(GetArraySlice(responseArgumentsArray, Constants.FlpItOffset,
                    sizeof(UInt16)));
            parameters.CarrierSenseTime =
                BinaryPrimitives.ReadUInt16BigEndian(GetArraySlice(responseArgumentsArray, Constants.FlpCstOffset,
                    sizeof(UInt16)));
            parameters.TargetRfPowerLevel =
                (double)(BinaryPrimitives.ReadInt16BigEndian(GetArraySlice(responseArgumentsArray,
                    Constants.FlpRflOffset, sizeof(Int16)))) / 10.0;
            parameters.Fh = (responseArgumentsArray[Constants.FlpFhOffset] > 0);
            parameters.Lbt = (responseArgumentsArray[Constants.FlpLbtOffset] > 0);
            parameters.Cw = (responseArgumentsArray[Constants.FlpCwOffset] > 0);
            return true;
        }

        #endregion

        #region 4.11 Set FH and LBT Parameters


        public bool SetFhLbtParameters(FhLbtParameters parameters)
        {
            throw new NotImplementedException();
        }

        #endregion



        public bool StartAutoRead2(AutoRead2NotificationCallback callback)
        {
            try
            {
                _autoRead2NotificationCallback = callback;
                if (ProcessRcpCommand(MessageCode.StartAutoRead2, out var result) != RcpReturnType.Success)
                    return false;
                if (!((result.Count == 1) && (result[Constants.ResponseArgOffset] == Constants.Success)))
                    return false;
                Interlocked.Exchange(ref _autoRead2Ongoing, 1);
                return true;
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
                if (ProcessRcpCommand(MessageCode.StopAutoRead2, out var result) != RcpReturnType.Success)
                    return false;
                if (!((result.Count == 1) && (result[Constants.ResponseArgOffset] == Constants.Success)))
                    return false;
                Interlocked.Exchange(ref _autoRead2Ongoing, 0);
                return true;
            }
            catch (Exception e)
            {
                Log.Warning(e, "Exception thrown");
                return false;
            }
        }
        #endregion

        private RcpReturnType ProcessRcpCommand(MessageCode messageCode, out List<byte> responsePayload, List<byte> commandPayload = null)
        {
            ClearReceivedCommandAnswerBuffer();
            responsePayload = new List<byte>();
            var command = AssembleRcpCommand(messageCode, commandPayload);
            _communicationTransport.TxByteList(command);
            if (!_receivedCommandAnswerBuffer.TryTake(out var commandAnswer, Constants.RcpCommandMaxResponseTimeMs))
            {
                Log.Warning($"Command {messageCode} timed out");
                return RcpReturnType.NoResponse;
            }

            // Extract command response code and leave commandAnswer as the response payload
            var commandResponseCode = commandAnswer.First();
            commandAnswer.RemoveAt(0);
                
            if (commandResponseCode == (byte)messageCode)
            {
                // Command response
                responsePayload = commandAnswer;
                return RcpReturnType.Success;
            }
            if (commandResponseCode == ((byte)MessageCode.CommandFailure))
            {
                // Command failure
                Log.Warning($"Command {messageCode} returned an error");
                if (commandAnswer.Count == 0)
                    Log.Warning(" * No error details available (payload length == 0)");
                else
                {
                    var errorByte = commandAnswer.First();
                    Log.Warning($" * Error code : {errorByte} ({(ErrorCode)errorByte})");
                }
                responsePayload = commandAnswer;
                return RcpReturnType.ReaderError;
            }
            Log.Warning($"RCP message code error. Expected {(byte)messageCode}, received {commandResponseCode}");
            return RcpReturnType.OtherError;
        }

        private T[] GetArraySlice<T>(T[] inputArray, int startIndex, int elementCount)
        {
            return (new ArraySegment<T>(inputArray)).Slice(startIndex, elementCount).ToArray();
        }

        private T[] GetArraySlice<T>(T[] inputArray, int startIndex)
        {
            return (new ArraySegment<T>(inputArray)).Slice(startIndex, inputArray.Length - startIndex).ToArray();
        }

        private bool ParseSingleByteResponsePayload(List<byte> responsePayload)
        {
            if (responsePayload.Count != 1)
                return false;
            return (responsePayload[Constants.ResponseArgOffset] == Constants.Success);
        }
    }
}
