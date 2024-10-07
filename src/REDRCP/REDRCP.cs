using System.Buffers.Binary;
using System.Diagnostics;
using Kliskatek.Driver.Rain.REDRCP.Transports;
using Newtonsoft.Json.Converters;
using Serilog;

namespace Kliskatek.Driver.Rain.REDRCP
{
    /// <summary>
    /// Provides higher level access to common RED RCP functionality. Works with serial port connection.
    /// </summary>
    public partial class REDRCP
    {
        private AutoRead2NotificationCallback _autoRead2NotificationCallback;
        private int _autoRead2Ongoing = 0;
        private ReadTypeCUiiNotificationCallback _readTypeCUiiNotificationCallback;
        private int _readTypeCUiiOngoing = 0;
        private ReadTypeCUiiTidNotificationCallback _readTypeCUiiTidNotificationCallback;
        private int _readTypeCUiiTidOngoing = 0;
        private AutoReadRssiNotificationCallback _autoReadRssiNotificationCallback;
        private int _autoReadRssiOngoing = 0;
        private GetDtcResultNotificationCallback _getDtcResultNotificationCallback;
        private int _getDtcResultOngoing = 0;
        private GetDtcResultNotificationCallback _setOptimmumFrequencyHoppingTableNotificationCallback;
        private int _setOptimumFrequencyHoppingTableOngoing = 0;

        private AutoRead2ExNotificationCallback _autoRead2ExNotificationCallback;
        private int _autoRead2ExOngoing = 0;

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
            var dt = BitConverter.GetBytes(parameters.DwellTime).Reverse();
            var it = BitConverter.GetBytes(parameters.IdleTime).Reverse();
            var cst = BitConverter.GetBytes(parameters.CarrierSenseTime).Reverse();
            var rfl = BitConverter.GetBytes((short)(parameters.TargetRfPowerLevel * 10.0)).Reverse();
            var fh = parameters.Fh ? (byte)1 : (byte)0;
            var lbt = parameters.Lbt ? (byte)1 : (byte)0;
            var cwt = parameters.Cw ? (byte)1 : (byte)0;

            var payloadArguments = new List<byte>(dt);
            payloadArguments.AddRange(it);
            payloadArguments.AddRange(cst);
            payloadArguments.AddRange(rfl);
            payloadArguments.Add(fh);
            payloadArguments.Add(lbt);
            payloadArguments.Add(cwt);

            if (ProcessRcpCommand(MessageCode.SetFhLbtParameters, out var responseArguments, payloadArguments) !=
                RcpReturnType.Success)
                return false;
            return ParseSingleByteResponsePayload(responseArguments);
        }

        #endregion

        #region 4.12 Get Tx Power Level

        public bool GetTxPowerLevel(out TxPowerLevels powerLevels)
        {
            powerLevels = new TxPowerLevels();
            if (ProcessRcpCommand(MessageCode.GetTxPower, out var responseArguments) != RcpReturnType.Success)
                return false;
            if (responseArguments.Count != 6)
                return false;

            var responseArgumentsArray = responseArguments.ToArray();
            powerLevels.CurrentTxPower =
                (double)(BinaryPrimitives.ReadInt16BigEndian(GetArraySlice(responseArgumentsArray, 0, 2))) / 10.0;
            powerLevels.MinTxPower =
                (double)(BinaryPrimitives.ReadInt16BigEndian(GetArraySlice(responseArgumentsArray, 2, 2))) / 10.0;
            powerLevels.MaxTxPower =
                (double)(BinaryPrimitives.ReadInt16BigEndian(GetArraySlice(responseArgumentsArray, 4, 2))) / 10.0;

            return true;
        }

        #endregion

        #region 4.13 Set Tx Power Level

        public bool SetTxPowerLevel(double powerLevel)
        {
            var argumentPayload = BitConverter.GetBytes((short)(powerLevel * 10.0)).Reverse().ToList();
            if (ProcessRcpCommand(MessageCode.SetTxPower, out var responseArguments, argumentPayload) !=
                RcpReturnType.Success)
                return false;
            return ParseSingleByteResponsePayload(responseArguments);
        }

        #endregion

        #region 4.14 RF CR signal control

        public bool RfCwSignalControl(bool cwSignalControl)
        {
            List<byte> argumentPayload = new List<byte>
            {
                cwSignalControl ? (byte)0xFF : (byte)0x00
            };
            if (ProcessRcpCommand(MessageCode.RfCwSignalControl, out var responseArguments, argumentPayload) !=
                RcpReturnType.Success)
                return false;
            return ParseSingleByteResponsePayload(responseArguments);
        }

        #endregion

        #region 4.15 Read Type C UII

        public bool ReadTypeCUiiResponse(out TypeCUii tag)
        {
            tag = new TypeCUii();
            if (ProcessRcpCommand(MessageCode.ReadTypeCUii, out var responseArgument) != RcpReturnType.Success)
                return false;
            if (responseArgument.Count < 2)
                return false;
            var responseArgumentArray = responseArgument.ToArray();
            tag.Pc = GetArraySlice(responseArgumentArray, 0, 2);
            if (responseArgument.Count > 2)
                tag.Epc = GetArraySlice(responseArgumentArray, 2);
            return true;
        }

        public bool ReadTypeCUiiNotification(ReadTypeCUiiNotificationCallback callback)
        {
            _readTypeCUiiNotificationCallback = callback;
            // This command does not return a notification message type 
            ClearReceivedCommandAnswerBuffer();
            var command = AssembleRcpCommand(MessageCode.ReadTypeCUii);
            _communicationTransport.TxByteList(command);
            Interlocked.Exchange(ref _readTypeCUiiOngoing, 1);
            return true;
        }

        #endregion

        #region 4.16 Read Type C UII TID

        public bool ReadTypeCUiiTid(byte MaxNumberTagsToRead, byte MaxElapsedTimeTagging, short RepeatCycle, ReadTypeCUiiTidNotificationCallback callback)
        {
            var argumentPayload = new List<byte> { MaxNumberTagsToRead, MaxElapsedTimeTagging };
            var rc = BitConverter.GetBytes(RepeatCycle).Reverse();
            argumentPayload.AddRange(rc);
            if (ProcessRcpCommand(MessageCode.ReadTypeCUiiTid, out var responsePayload, argumentPayload) !=
                RcpReturnType.Success)
                return false;

            if (!ParseSingleByteResponsePayload(responsePayload))
                return false;

            Interlocked.Exchange(ref _readTypeCUiiTidOngoing, 1);
            return true;
        }

        #endregion

        #region 4.17 Read Type C Tag Data

        public bool ReadTypeCTagData(string epc, ParamMemoryBank memoryBank, ushort startAddress, ushort wordCount,
            out string readData, UInt32 accessPassword = 0)
        {
            readData = "";

            var ap = BitConverter.GetBytes(accessPassword).Reverse();
            var epcByteArray = Convert.FromHexString(epc);
            var ul = BitConverter.GetBytes((ushort)epcByteArray.Length).Reverse();
            var sa = BitConverter.GetBytes((ushort)startAddress).Reverse();
            var dl = BitConverter.GetBytes((ushort)wordCount).Reverse();
            
            var argumentPayload = new List<byte>();
            argumentPayload.AddRange(ap);
            argumentPayload.AddRange(ul);
            argumentPayload.AddRange(epcByteArray);
            argumentPayload.Add((byte)memoryBank);
            argumentPayload.AddRange(sa);
            argumentPayload.AddRange(dl);

            if (ProcessRcpCommand(MessageCode.ReadTypeCTagData, out var responsePayload, argumentPayload) !=
                RcpReturnType.Success)
                return false;

            if (responsePayload.Count > 0)
                readData = BitConverter.ToString(responsePayload.ToArray()).Replace("-", "");
            return true;
        }

        #endregion

        #region 4.18 Get Frequency Hopping Table

        public bool GetFrequencyHoppingTable(out List<byte> channelNumbers)
        {
            channelNumbers = new List<byte>();
            if (ProcessRcpCommand(MessageCode.GetFrequencyHoppingTable, out var responsePayload) !=
                RcpReturnType.Success)
                return false;

            if (responsePayload.Count == 0)
                return false;
            var responsePayloadArray = responsePayload.ToArray();

            if (responsePayloadArray.Length != responsePayloadArray[0] + 1)
                return false;

            for (int i=0; i < responsePayloadArray[0]; i++)
                channelNumbers.Add(responsePayloadArray[i+1]);

            return true;
        }

        #endregion

        #region 4.19 Set Frequency Hopping Table

        public bool SetFrequencyHoppingTable(List<byte> channelNumbers)
        {
            var argumentPayload = new List<byte> { (byte)channelNumbers.Count };
            argumentPayload.AddRange(channelNumbers);
            if (ProcessRcpCommand(MessageCode.SetFrequencyHoppingTable, out var responsePayload, argumentPayload) !=
                RcpReturnType.Success)
                return false;
            return ParseSingleByteResponsePayload(responsePayload);
        }

        #endregion

        #region 4.20 Get Modulation Mode

        public bool GetModulationMode(out ModulationMode modulationMode)
        {
            modulationMode = new ModulationMode();
            if (ProcessRcpCommand(MessageCode.GetModulationMode, out var responsePayload) != RcpReturnType.Success)
                return false;
            if (responsePayload.Count != 4)
                return false;

            var responsePayloadArray = responsePayload.ToArray();
            modulationMode.BackscatterLinkFrequency =
                BinaryPrimitives.ReadUInt16BigEndian(GetArraySlice(responsePayloadArray, 0, sizeof(UInt16)));
            modulationMode.RxMod = (ParamModulation)responsePayloadArray[2];
            modulationMode.Dr = (ParamDr)responsePayloadArray[3];

            return true;
        }

        #endregion

        #region 4.21 Set Modulation Mode

        public bool SetModulationMode(ModulationMode modulationMode)
        {
            var blf = BitConverter.GetBytes(modulationMode.BackscatterLinkFrequency).Reverse();
            var payloadArguments = new List<byte> { 0xFF };
            payloadArguments.AddRange(blf);
            payloadArguments.Add((byte)modulationMode.RxMod);
            payloadArguments.Add((byte)modulationMode.Dr);

            if (ProcessRcpCommand(MessageCode.SetModulationMode, out var responsePayload, payloadArguments) !=
                RcpReturnType.Success)
                return false;
            return ParseSingleByteResponsePayload(responsePayload);
        }

        #endregion

        #region 4.22 Get Anti-Collision Mode

        public bool GetAntiCollisionMode(out AntiCollisionModeParameters antiCollisionMode)
        {
            antiCollisionMode = new AntiCollisionModeParameters();
            if (ProcessRcpCommand(MessageCode.GetAntiCollisionMode, out var responsePayload) != RcpReturnType.Success)
                return false;
            if (responsePayload.Count != 4)
                return false;
            antiCollisionMode.Mode = (AntiCollisionMode)responsePayload[0];
            antiCollisionMode.QStart = responsePayload[1];
            antiCollisionMode.QMax = responsePayload[2];
            antiCollisionMode.QMin = responsePayload[3];
            return true;
        }

        #endregion

        #region 4.23 Set Anti-Collision Mode

        public bool SetAntiCollisionMode(AntiCollisionModeParameters antiCollisionMode)
        {
            var argumentPayload = new List<byte>
            {
                (byte)antiCollisionMode.Mode,
                antiCollisionMode.QStart,
                antiCollisionMode.QMax,
                antiCollisionMode.QMin,
            };
            if (ProcessRcpCommand(MessageCode.SetAntiCollisionMode, out var responsePayload, argumentPayload) !=
                RcpReturnType.Success)
                return false;
            return ParseSingleByteResponsePayload(responsePayload);
        }

        #endregion

        #region 4.24 Start Auto Read2

        public bool StartAutoRead2(AutoRead2NotificationCallback callback)
        {
            try
            {
                _autoRead2NotificationCallback = callback;
                if (ProcessRcpCommand(MessageCode.StartAutoRead2, out var responseArguments) != RcpReturnType.Success)
                    return false;
                if (!ParseSingleByteResponsePayload(responseArguments))
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

        #endregion

        #region 4.25 Start Auto Read RSSI

        public bool StartAutoReadRssi(TagType tagType, byte maximumNumberOfTags, byte maximumElapsedTime,
            ushort repeatCycle, AutoReadRssiNotificationCallback callback)
        {
            var rc = BitConverter.GetBytes(repeatCycle).Reverse();
            var argumentPayload = new List<byte>
            {
                (byte)tagType,
                maximumNumberOfTags,
                maximumElapsedTime
            };
            argumentPayload.AddRange(rc);

            if (ProcessRcpCommand(MessageCode.StartAutoReadRssi, out var responsePayload, argumentPayload) !=
                RcpReturnType.Success)
                return false;

            if (!ParseSingleByteResponsePayload(responsePayload))
                return false;

            _autoReadRssiNotificationCallback = callback;
            Interlocked.Exchange(ref _autoReadRssiOngoing, 1);
            return true;
        }

        #endregion

        #region 4.26 Stop Auto Read2

        public bool StopAutoRead2()
        {
            try
            {
                if (ProcessRcpCommand(MessageCode.StopAutoRead2, out var responseArguments) != RcpReturnType.Success)
                    return false;
                if (!ParseSingleByteResponsePayload(responseArguments))
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

        #region 4.27 Start Auto Read2 Ex

        // TODO: Documentation does not show notification filed when TagRssi = True. Testing pending with real hardware
        public bool StartAutoRead2Ex(ParamAutoRead2ExMode mode, bool tagRssi, byte antPort, byte maximumNumberOfTags,
            byte maximumElapsedTime, ushort repeatCycle, AutoRead2ExNotificationCallback callback)
        {
            var rc = BitConverter.GetBytes(repeatCycle).Reverse();
            var argumentPayload = new List<byte>
            {
                (byte)mode,
                (byte)(tagRssi ? 1 : 0),
                antPort,
                maximumNumberOfTags,
                maximumElapsedTime
            };
            argumentPayload.AddRange(rc);
            if (ProcessRcpCommand(MessageCode.StartAutoRead2Ex, out var responsePayload, argumentPayload) !=
                RcpReturnType.Success)
                return false;
            if (!ParseSingleByteResponsePayload(responsePayload))
                return false;
            _autoRead2ExNotificationCallback = callback;
            Interlocked.Exchange(ref _autoRead2ExOngoing, 1);
            return true;
        }

        #endregion

        #region 4.28 Get Frequency Information

        // TODO: documentation says 8 byte response arguments. Receiving 9 byte response arguments with RED4SW
        public bool GetFrequencyInformation(out FrequencyInformation frequencyInformation)
        {
            frequencyInformation = new FrequencyInformation();
            if (ProcessRcpCommand(MessageCode.GetFrequencyInformation, out var responsePayload) !=
                RcpReturnType.Success)
                return false;
            if (responsePayload.Count != 8)
                return false;
            var payload = responsePayload.ToArray();

            frequencyInformation.Spacing = BinaryPrimitives.ReadUInt16BigEndian(payload.GetArraySlice(0, sizeof(UInt16)));
            frequencyInformation.StartFreq = BinaryPrimitives.ReadUInt32BigEndian(payload.GetArraySlice(2, sizeof(UInt32)));
            frequencyInformation.Channel = payload[6];
            frequencyInformation.RfPreset = (ParamRfPreset)payload[7];
            return true;
        }

        #endregion

        #region 4.29 Set Frequency Information

        public bool SetFrequencyInformation(FrequencyInformation frequencyInformation)
        {
            var spacing = BitConverter.GetBytes(frequencyInformation.Spacing).Reverse();
            var startFreq = BitConverter.GetBytes(frequencyInformation.StartFreq).Reverse();
            var channel = frequencyInformation.Channel;
            var rfPreset = (byte)frequencyInformation.RfPreset;

            var payloadArgument = new List<byte>();
            payloadArgument.AddRange(spacing);
            payloadArgument.AddRange(startFreq);
            payloadArgument.Add(channel);
            payloadArgument.Add(rfPreset);

            if (ProcessRcpCommand(MessageCode.SetFrequencyInformation, out var responseAnswer, payloadArgument) !=
                RcpReturnType.Success)
                return false;
            return ParseSingleByteResponsePayload(responseAnswer);
        }

        #endregion

        #region 4.30 Write Type C Data

        public bool WriteTypeCTagData(string epc, ParamMemoryBank memoryBank, ushort startAddress, string dataToWrite,
            UInt32 accessPassword = 0)
        {
            var ap = BitConverter.GetBytes(accessPassword).Reverse();
            var epcByteArray = Convert.FromHexString(epc);
            var ul = BitConverter.GetBytes((ushort)epcByteArray.Length).Reverse();
            var mb = (byte)memoryBank;
            var sa = BitConverter.GetBytes(startAddress).Reverse();
            var dt = Convert.FromHexString(dataToWrite);
            var dl = BitConverter.GetBytes((ushort)dt.Length).Reverse();

            var argumentPayload = new List<byte>();
            argumentPayload.AddRange(ap);
            argumentPayload.AddRange(ul);
            argumentPayload.AddRange(epcByteArray);
            argumentPayload.Add(mb);
            argumentPayload.AddRange(sa);
            argumentPayload.AddRange(dl);
            argumentPayload.AddRange(dt);

            if (ProcessRcpCommand(MessageCode.WriteTypeCTagData, out var responseArguments, argumentPayload) !=
                RcpReturnType.Success)
                return false;
            if (responseArguments.Count != epcByteArray.Length) 
                return false;
            var payload = responseArguments.ToArray();

            for (int i=0; i < payload.Length; i++)
                if (epcByteArray[i] != payload[i])
                    return false;
            return true;
        }

        #endregion

        #region 4.31 BlockWrite Type C Tag Data

        public bool BlockWriteTypeCTagData(string epc, ParamMemoryBank memoryBank, ushort startAddress, string dataToWrite,
            UInt32 accessPassword = 0)
        {
            var ap = BitConverter.GetBytes(accessPassword).Reverse();
            var epcByteArray = Convert.FromHexString(epc);
            var ul = BitConverter.GetBytes((ushort)epcByteArray.Length).Reverse();
            var mb = (byte)memoryBank;
            var sa = BitConverter.GetBytes(startAddress).Reverse();
            var dt = Convert.FromHexString(dataToWrite);
            var dl = BitConverter.GetBytes((ushort)dt.Length).Reverse();

            var argumentPayload = new List<byte>();
            argumentPayload.AddRange(ap);
            argumentPayload.AddRange(ul);
            argumentPayload.AddRange(epcByteArray);
            argumentPayload.Add(mb);
            argumentPayload.AddRange(sa);
            argumentPayload.AddRange(dl);
            argumentPayload.AddRange(dt);

            if (ProcessRcpCommand(MessageCode.BlockWriteTypeCTagData, out var responseArguments, argumentPayload) !=
                RcpReturnType.Success)
                return false;
            return ParseSingleByteResponsePayload(responseArguments);
        }

        #endregion

        #region 4.32 BlockErase Type C Tag Data

        public bool BlockEraseTypeCTagData(string epc, ParamMemoryBank memoryBank, ushort startAddress, ushort dataLength, 
            UInt32 accessPassword = 0)
        {
            var ap = BitConverter.GetBytes(accessPassword).Reverse();
            var epcByteArray = Convert.FromHexString(epc);
            var ul = BitConverter.GetBytes((ushort)epcByteArray.Length).Reverse();
            var mb = (byte)memoryBank;
            var sa = BitConverter.GetBytes(startAddress).Reverse();
            var dl = BitConverter.GetBytes(dataLength).Reverse();

            var argumentPayload = new List<byte>();
            argumentPayload.AddRange(ap);
            argumentPayload.AddRange(ul);
            argumentPayload.AddRange(epcByteArray);
            argumentPayload.Add(mb);
            argumentPayload.AddRange(sa);
            argumentPayload.AddRange(dl);

            if (ProcessRcpCommand(MessageCode.BlockEraseTypeCTagData, out var responseArguments, argumentPayload) !=
                RcpReturnType.Success)
                return false;
            return ParseSingleByteResponsePayload(responseArguments);
        }

        #endregion

        #region 4.33 BlockPermalock Type C Tag

        public bool BlockPermalockTypeCTag(string epc, ParamReadLock readLock, ParamMemoryBank memoryBank,
            ushort blockPointer, byte blockRange, string mask, UInt32 accessPassword = 0)
        {
            var ap = BitConverter.GetBytes(accessPassword).Reverse();
            var epcByteArray = Convert.FromHexString(epc);
            var ul = BitConverter.GetBytes((ushort)epcByteArray.Length).Reverse();
            byte rfu = 0x00;
            var rl = (byte)readLock;
            var mb = (byte)memoryBank;
            var bp = BitConverter.GetBytes(blockPointer).Reverse();
            var br = blockRange;
            var maskByteArray = Convert.FromHexString(mask);

            var argumentPayload = new List<byte>();
            argumentPayload.AddRange(ap);
            argumentPayload.AddRange(ul);
            argumentPayload.AddRange(epcByteArray);
            argumentPayload.Add(rfu);
            argumentPayload.Add(rl);
            argumentPayload.Add(mb);
            argumentPayload.AddRange(bp);
            argumentPayload.Add(br);
            argumentPayload.AddRange(maskByteArray);

            if (ProcessRcpCommand(MessageCode.BlockPermalockTypeCTag, out var responseArgument, argumentPayload) !=
                RcpReturnType.Success)
                return false;
            return ParseSingleByteResponsePayload(responseArgument);
        }

        #endregion

        #region 4.34 Kill Type C Tag

        public bool KillTypeCTag(UInt32 killPassword, string epc)
        {
            var kp = BitConverter.GetBytes(killPassword).Reverse();
            var epcByteArray = Convert.FromHexString(epc);
            var ul = BitConverter.GetBytes((ushort)epcByteArray.Length).Reverse();

            var argumentPayload = new List<byte>();
            argumentPayload.AddRange(kp);
            argumentPayload.AddRange(ul);
            argumentPayload.AddRange(epcByteArray);

            if (ProcessRcpCommand(MessageCode.KillRecomTypeCTag, out var responsePayload, argumentPayload) !=
                RcpReturnType.Success)
                return false;
            return ParseSingleByteResponsePayload(responsePayload);
        }

        #endregion

        #region 4.35 Lock Type C Tag

        public bool LocTypeCTag(string epc, UInt32 lockMaskAction, UInt32 accessPassword)
        {
            var ap = BitConverter.GetBytes(accessPassword).Reverse();
            var epcByteArray = Convert.FromHexString(epc);
            var ul = BitConverter.GetBytes((ushort)epcByteArray.Length).Reverse();
            var ld = BitConverter.GetBytes(lockMaskAction).Reverse().ToArray();

            var argumentPayload = new List<byte>();
            argumentPayload.AddRange(ap);
            argumentPayload.AddRange(ul);
            argumentPayload.AddRange(epcByteArray);
            argumentPayload.AddRange(ld.GetArraySlice(1));

            if (ProcessRcpCommand(MessageCode.LockTypeCTag, out var responsePayload, argumentPayload) !=
                RcpReturnType.Success)
                return false;
            return ParseSingleByteResponsePayload(responsePayload);
        }

        #endregion

        #region 4.36 Get Selection Enable

        public bool GetSelectionEnable(out byte enableStatus)
        {
            enableStatus = 0;
            if (ProcessRcpCommand(MessageCode.GetSelectionEnable, out var responsePayload) != RcpReturnType.Success)
                return false;
            if (responsePayload.Count != 1)
                return false;
            enableStatus = responsePayload[0];
            return true;
        }

        public bool GetSelectionEnable(out EnableStatus enableStatus)
        {
            enableStatus = new EnableStatus();
            if (ProcessRcpCommand(MessageCode.GetSelectionEnable, out var responsePayload) != RcpReturnType.Success)
                return false;
            if (responsePayload.Count != 1)
                return false;
            enableStatus = responsePayload[0].ToEnableStatus();
            return true;
        }

        #endregion

        #region 4.37 Set Selection Enable

        public bool SetSelectionEnable(byte enableStatus)
        {
            if (ProcessRcpCommand(MessageCode.SetSelectionEnable, out var responsePayload,
                    new List<byte> { enableStatus }) != RcpReturnType.Success)
                return false;
            return ParseSingleByteResponsePayload(responsePayload);
        }

        public bool SetSelectionEnable(EnableStatus enableStatus)
        {
            if (ProcessRcpCommand(MessageCode.SetSelectionEnable, out var responsePayload,
                    new List<byte> { enableStatus.ToByte() }) != RcpReturnType.Success)
                return false;
            return ParseSingleByteResponsePayload(responsePayload);
        }

        #endregion

        #region 4.38 Get Multi-Antenna Sequence

        public bool GetMultiAntennaSequence(out List<byte> antennaSequence)
        {
            antennaSequence = [];
            if (ProcessRcpCommand(MessageCode.GetMultiAntennaSequence, out var responsePayload) !=
                RcpReturnType.Success)
                return false;
            if (responsePayload.Count != responsePayload[0] + 1)
                return false;
            antennaSequence.AddRange(responsePayload.ToArray().GetArraySlice(1));
            return true;
        }

        #endregion

        #region 4.39 Set Multi-Antenna Sequence

        public bool SetMultiAntennaSequence(List<byte> antennaSequence)
        {
            var payloadArguments = new List<byte> { (byte)antennaSequence.Count };
            payloadArguments.AddRange(antennaSequence);
            if (ProcessRcpCommand(MessageCode.SetMultiAntennaSequence, out var responsePayload, payloadArguments) !=
                RcpReturnType.Success)
                return false;
            return ParseSingleByteResponsePayload(responsePayload);
        }

        #endregion

        #region 4.40 Antenna Check

        public bool AntennaCheck(byte refLevel, out AntennaCheckError antennaError)
        {
            antennaError = new AntennaCheckError();
            ClearReceivedCommandAnswerBuffer();
            var command = AssembleRcpCommand(MessageCode.AntennaCheck, [refLevel]);
            _communicationTransport.TxByteList(command);
            if (!_receivedCommandAnswerBuffer.TryTake(out var commandAnswer, Constants.RcpCommandMaxResponseTimeMs))
            {
                Log.Warning($"Command {MessageCode.AntennaCheck} timeout out");
                return false;
            }

            if (commandAnswer.Count == 0)
                return false;

            var commandResponseCode = commandAnswer.First();
            commandAnswer.RemoveAt(0);

            switch (commandResponseCode)
            {
                case (byte)MessageCode.AntennaCheck:
                    return ParseSingleByteResponsePayload(commandAnswer);
                case (byte)MessageCode.Error:
                    if (commandAnswer.Count != 3)
                        return false;
                    antennaError.IsFailure = true;
                    antennaError.ErrorCode = commandAnswer[0];
                    antennaError.SubErrorCode = commandAnswer[2];
                    return false;
                default:
                    return false;
            }
        }

        #endregion

        #region 4.41 Get Selection

        public bool GetSelection(byte index, out Selection selection)
        {
            selection = new Selection();
            if (ProcessRcpCommand(MessageCode.GetSelection, out var responsePayload, [index]) != RcpReturnType.Success)
                return false;
            if (responsePayload.Count < 7)
                return false;
            
            var payload = responsePayload.ToArray();
            selection.Length = payload[6];
            var lengthBytes = selection.Length >> 3;
            // Round up if 3 least significant bits are not zero
            lengthBytes += (((ushort)(selection.Length) & 0x07) > 0) ? 1 : 0;
            if (responsePayload.Count != 7 + lengthBytes)
                return false;

            selection.Index = payload[0];
            selection.Target = (ParamTarget)payload[1];
            selection.Action = (ParamSelectAction)payload[2];
            selection.MemoryBank = (ParamMemoryBank)payload[3];
            selection.Pointer = BinaryPrimitives.ReadUInt16BigEndian(payload.GetArraySlice(4, sizeof(UInt16)));
            if (selection.Length > 0)
                selection.Mask = new List<byte>((payload.GetArraySlice(7)));
            return true;
        }

        #endregion

        #region 4.42 Set Selection

        public bool SetSelection(Selection selection)
        {
            var argumentPayload = new List<byte>
            {
                selection.Index,
                (byte)selection.Target,
                (byte)selection.Action,
                (byte)selection.MemoryBank
            };
            argumentPayload.AddRange(BitConverter.GetBytes(selection.Pointer).Reverse());
            argumentPayload.Add(selection.Length);
            argumentPayload.AddRange(selection.Mask);

            if (ProcessRcpCommand(MessageCode.SetSelection, out var responseArgument, argumentPayload) !=
                RcpReturnType.Success)
                return false;
            return ParseSingleByteResponsePayload(responseArgument);
        }

        #endregion

        #region 4.43 Get RSSI

        public bool GetRssi(out double rssi)
        {
            rssi = 0.0;
            if (ProcessRcpCommand(MessageCode.GetRssi, out var responsePayload) != RcpReturnType.Success)
                return false;
            if (responsePayload.Count != 2)
                return false;
            var ushortRssi = BinaryPrimitives.ReadUInt16BigEndian(responsePayload.ToArray());
            rssi = ((double)ushortRssi) / 10.0;
            return true;
        }

        #endregion

        #region 4.44 Scan RSSI

        public bool ScanRssi(out ScanRssiParameters rssiParameters)
        {
            rssiParameters = new ScanRssiParameters();
            if (ProcessRcpCommand(MessageCode.ScanRssi, out var responseArguments) != RcpReturnType.Success)
                return false;
            if (responseArguments.Count < 4)
                return false;
            var payload = responseArguments.ToArray();
            rssiParameters.StartChannelNumber = payload[0];
            rssiParameters.StopChannelNumber = payload[1];
            rssiParameters.BestChannelNumber = payload[2];
            rssiParameters.RssiLevels.AddRange(payload.GetArraySlice(3));
            return true;
        }

        #endregion

        #region 4.45 Get DTC Result

        public bool GetDtcResult(out DtcResultResponseParameters parameters, GetDtcResultNotificationCallback callback)
        {
            parameters = new DtcResultResponseParameters();
            ClearReceivedCommandAnswerBuffer();
            var command = AssembleRcpCommand(MessageCode.GetDtcResult);
            _communicationTransport.TxByteList(command);
            if (!_receivedCommandAnswerBuffer.TryTake(out var commandAnswer, Constants.RcpCommandMaxResponseTimeMs))
            {
                Log.Warning($"Command {MessageCode.GetDtcResult} timed out");
                return false;
            }

            if (commandAnswer.Count == 0)
                return false;
            var commandResponseCode = commandAnswer.First();
            commandAnswer.RemoveAt(0);

            if (commandResponseCode != (byte)MessageCode.GetDtcResult)
                return false;
            if (commandAnswer.Count != 5)
                return false;

            parameters.InductorNumber = commandAnswer[0];
            parameters.DigitalTunableCapacitor1 = commandAnswer[1];
            parameters.DigitalTunableCapacitor2 = commandAnswer[2];
            parameters.LeakageRssi = commandAnswer[3];
            parameters.LeakageCancellationAlgorithmStateNumber = commandAnswer[4];

            _getDtcResultNotificationCallback = callback;
            Interlocked.Exchange(ref _getDtcResultOngoing, 1);

            return true;
        }

        #endregion

        #region 4.46 Update Registry

        public bool UpdateRegistry()
        {
            if (ProcessRcpCommand(MessageCode.UpdateRegistry, out var responseArguments) != RcpReturnType.Success)
                return false;
            return ParseSingleByteResponsePayload(responseArguments);
        }

        #endregion

        #region 4.47 Get Registry Item

        public bool GetRegistryItem(Registry registry, out RegistryItem item)
        {
            item = new RegistryItem();
            var argumentPayload = new List<byte>();
            argumentPayload.AddRange(BitConverter.GetBytes((ushort)registry).Reverse());
            if (ProcessRcpCommand(MessageCode.GetRegistryItem, out var responseArguments, argumentPayload) !=
                RcpReturnType.Success)
                return false;
            if (responseArguments.Count == 0)
                return false;
            item.Active = (RegistryItemStatus)responseArguments[0];
            if (responseArguments.Count > 1)
                item.Data.AddRange(responseArguments.ToArray().GetArraySlice(1));
            return true;
        }

        #endregion

        #region 4.48 Set Optimum Frequency Hopping Table

        public bool SetOptimumFrequencyHoppingTable(GetDtcResultNotificationCallback callback)
        {
            if (ProcessRcpCommand(MessageCode.SetOptimumFrequencyHoppingTable, out var responseArgument) !=
                RcpReturnType.Success)
                return false;
            if (!ParseSingleByteResponsePayload(responseArgument))
                return false;

            Interlocked.Exchange(ref _setOptimumFrequencyHoppingTableOngoing, 1);
            _setOptimmumFrequencyHoppingTableNotificationCallback = callback;

            if (!_receivedCommandAnswerBuffer.TryTake(out var stopResponseArgument,
                    Constants.RcpCommandMaxResponseTimeMs * 2))
            {
                Interlocked.Exchange(ref _setOptimumFrequencyHoppingTableOngoing, 0);
                return false;
            }

            Interlocked.Exchange(ref _setOptimumFrequencyHoppingTableOngoing, 0);
            if (stopResponseArgument.Count != 2)
                return false;
            var stopResponseCode = stopResponseArgument.First();
            var payload = stopResponseArgument.Last();
            if (stopResponseCode == (byte)MessageCode.SetOptimumFrequencyHoppingTable)
                return payload == (byte)0x01;
            return false;
        }

        #endregion

        #region 4.49 GetFrequency Hopping Mode

        public bool GetFrequencyHoppingMode(out ParamFrequencyHoppingMode mode)
        {
            mode = ParamFrequencyHoppingMode.NormalMode;
            if (ProcessRcpCommand(MessageCode.GetFrequencyHoppingMode, out var responseArgument) !=
                RcpReturnType.Success)
                return false;
            if (responseArgument.Count != 1)
                return false;
            mode = (ParamFrequencyHoppingMode)responseArgument[0];
            return true;
        }

        #endregion

        #region 4.50 Set Frequency Hopping Mode

        public bool SetFrequencyHoppingMode(ParamFrequencyHoppingMode mode)
        {
            var argumentPayload = new List<byte> { (byte)mode };
            if (ProcessRcpCommand(MessageCode.SetFrequencyHoppingMode, out var responseArgument, argumentPayload) !=
                RcpReturnType.Success)
                return false;
            return ParseSingleByteResponsePayload(responseArgument);
        }

        #endregion

        #region 4.51 Get Tx Leakage RSSI Level for Smart Hopping Mode

        public bool GetTxLeakageRssiLevelForSmartHoppingMode(out byte reference)
        {
            reference = 0;
            if (ProcessRcpCommand(MessageCode.GetTxLeakageRssiLevelSmartHoppingMode, out var responseArgument) !=
                RcpReturnType.Success)
                return false;
            if (responseArgument.Count != 1)
                return false;
            reference = responseArgument[0];
            return true;
        }

        #endregion

        #region 4.52 Set Tx Leakage RSSI Level for Smart Hopping Mode

        public bool SetTxLeakageRssiLevelForSmartHoppingMode(byte reference)
        {
            if (ProcessRcpCommand(MessageCode.SetTxLeakageRsiiLevelSmartHoppingMode, out var responseArgument,
                    [reference]) != RcpReturnType.Success)
                return false;
            return ParseSingleByteResponsePayload(responseArgument);
        }

        #endregion

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

            if (commandAnswer.Count == 0)
                return RcpReturnType.OtherError;

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

        private int GetEpcByteLengthFromPc(ushort pc)
        {
            var wordCount = (pc >> 11);
            return wordCount * 2;
        }
    }
}
