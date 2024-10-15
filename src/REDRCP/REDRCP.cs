using System.Buffers.Binary;
using Kliskatek.Driver.Rain.REDRCP.Transports;
using Serilog;

namespace Kliskatek.Driver.Rain.REDRCP
{
    /// <summary>
    /// Class <c>REDRCP</c> provides high level methods that give access to  RED RCP protocol commands,
    /// responses and notifications.
    /// <see href="https://www.phychips.com/upload/board/Reader_Control_Protocol_User_Manual.pdf"/>
    /// </summary>
    public partial class REDRCP
    {
        private ITransport _communicationTransport;

        public bool Connect(string connectionString)
        {
            try
            {
                var busType = GetBusTypeFromConnectionString(connectionString);
                switch (busType)
                {
                    case SupportedBuses.SerialPort:
                        _communicationTransport = new SerialPortTransport();
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
            if (IsSerialBus(connectionString))
                return SupportedBuses.SerialPort;
            throw new ArgumentException("Connection string can not be parsed into a known format");
        }

        private bool IsSerialBus(string connectionString)
        {
            // Windows
            if (connectionString.ToUpper().StartsWith("COM"))
            {
                if (int.TryParse(connectionString.Substring(3), out var _))
                    return true;
            }
            // Linux & macos
            if (connectionString.StartsWith("/dev/tty"))
                return true;

            return false;
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

        public RcpResultType GetReaderInformation(byte commandArgument, out List<byte> responseArguments)
        {
            responseArguments = new List<byte>();
            return ProcessRcpCommand(MessageCode.GetReaderInformation, out responseArguments, [commandArgument]);
        }

        public RcpResultType GetReaderInformationReaderModel(out string model)
        {
            model = string.Empty;
            var modelBinary = new List<byte>();
            var rcpResult = GetReaderInformation((byte)ReaderInfoType.Model, out modelBinary);
            if (rcpResult == RcpResultType.Success)
                model = System.Text.Encoding.ASCII.GetString(modelBinary.ToArray());
            return rcpResult;
        }
        
        public RcpResultType GetReaderInformationFirmwareVersion(out string firmwareVersion)
        {
            firmwareVersion = string.Empty;
            var rcpResult = GetReaderInformation((byte)ReaderInfoType.FwVersion, out var firmwareBinary);
            if (rcpResult == RcpResultType.Success)
                firmwareVersion = System.Text.Encoding.ASCII.GetString(firmwareBinary.ToArray())
                    .Replace("\0", string.Empty);
            return rcpResult;
        }

        public RcpResultType GetReaderInformationManufacturer(out string manufacturer)
        {
            manufacturer = string.Empty;
            var rcpResult = GetReaderInformation((byte)ReaderInfoType.Manufacturer, out var manufacturerBinary);
            if (rcpResult == RcpResultType.Success)
                manufacturer = System.Text.Encoding.ASCII.GetString(manufacturerBinary.ToArray());
            return rcpResult;
        }

        public RcpResultType GetReaderInformationDetails(out ReaderInformationDetails details)
        {
            details = new ReaderInformationDetails();
            var returnValue = GetReaderInformation((byte)ReaderInfoType.Detail, out var detailBinary);
            if (returnValue != RcpResultType.Success)
                return returnValue;

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

            return RcpResultType.Success;
        }
        #endregion

        #region 4.3 Get Region

        public RcpResultType GetRegion(out Region region)
        {
            region = Region.Europe;
            var rcpResult = ProcessRcpCommand(MessageCode.GetRegion, out var responseArguments);
            if (rcpResult != RcpResultType.Success)
                return rcpResult;
            region = (Region)responseArguments[0];
            return RcpResultType.Success;
        }

        #endregion

        #region 4.4 Set Region

        public RcpResultType SetRegion(Region region)
        {
            var rcpResult = ProcessRcpCommand(MessageCode.SetRegion, out var responseArguments, [(byte)region]);
            return (rcpResult == RcpResultType.Success) ? ParseSingleByteResponsePayload(responseArguments) : rcpResult;
        }
        #endregion

        #region 4.5 Set System Reset

        public RcpResultType SetSystemReset()
        {
            var rcpResult = ProcessRcpCommand(MessageCode.SetSystemReset, out var responseArguments);
            return (rcpResult == RcpResultType.Success) ? ParseSingleByteResponsePayload(responseArguments) : rcpResult;
        }

        #endregion

        #region 4.6 Get Type C A/I Query Parameters

        public RcpResultType GetTypeCaiQueryParameters(out TypeCaiQueryParameters parameters)
        {
            parameters = new TypeCaiQueryParameters();
            var rcpResult = ProcessRcpCommand(MessageCode.GetTypeQueryRelatedParameters, out var responseArguments);
            if (rcpResult != RcpResultType.Success)
                return rcpResult;

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

            return RcpResultType.Success;
        }

        #endregion

        #region 4.7 Set Type C A/I Query Parameters

        public RcpResultType SetTypeCaiQueryParameters(TypeCaiQueryParameters parameters)
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

            var rcpResult = ProcessRcpCommand(MessageCode.SetTypeQueryRelatedParameters, out var responseArguments,
                arguments);
            return (rcpResult == RcpResultType.Success) ? ParseSingleByteResponsePayload(responseArguments) : rcpResult;
        }

        #endregion

        #region 4.8 Get RF Channel

        public RcpResultType GetRfChannel(out RfChannel channel)
        {
            channel = new RfChannel();
            var rcpResult = ProcessRcpCommand(MessageCode.GetRfChannel, out var responseArguments);
            if (rcpResult != RcpResultType.Success)
                return rcpResult;
            channel.ChannelNumber = responseArguments[0];
            channel.ChannelNumberOffset = responseArguments[1];
            return RcpResultType.Success;
        }

        #endregion

        #region 4.9 Set RF Channel

        public RcpResultType SetRfChannel(RfChannel channel)
        {
            var arguments = new List<Byte>();
            arguments.Add(channel.ChannelNumber);
            arguments.Add(channel.ChannelNumberOffset);
            var rcpResult = ProcessRcpCommand(MessageCode.SetRfChannel, out var responseArguments, arguments);
            return (rcpResult == RcpResultType.Success) ? ParseSingleByteResponsePayload(responseArguments) : rcpResult;
        }

        #endregion

        #region 4.10 Get FH and LBT Parameters

        public RcpResultType GetFhLbtParameters(out FhLbtParameters parameters)
        {
            parameters = new FhLbtParameters();
            var rcpResult = ProcessRcpCommand(MessageCode.GetFhLbtParameters, out var responseArguments);
            if (rcpResult != RcpResultType.Success)
                return rcpResult;
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
            return RcpResultType.Success;
        }

        #endregion

        #region 4.11 Set FH and LBT Parameters
        
        public RcpResultType SetFhLbtParameters(FhLbtParameters parameters)
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

            var rcpResult = ProcessRcpCommand(MessageCode.SetFhLbtParameters, out var responseArguments,
                payloadArguments);
            return (rcpResult == RcpResultType.Success) ? ParseSingleByteResponsePayload(responseArguments) : rcpResult;
        }

        #endregion

        #region 4.12 Get Tx Power Level

        public RcpResultType GetTxPowerLevel(out TxPowerLevels powerLevels)
        {
            powerLevels = new TxPowerLevels();
            var rcpResult = ProcessRcpCommand(MessageCode.GetTxPower, out var responseArguments);
            if (rcpResult != RcpResultType.Success)
                return rcpResult;

            var responseArgumentsArray = responseArguments.ToArray();
            powerLevels.CurrentTxPower =
                (double)(BinaryPrimitives.ReadInt16BigEndian(GetArraySlice(responseArgumentsArray, 0, 2))) / 10.0;
            powerLevels.MinTxPower =
                (double)(BinaryPrimitives.ReadInt16BigEndian(GetArraySlice(responseArgumentsArray, 2, 2))) / 10.0;
            powerLevels.MaxTxPower =
                (double)(BinaryPrimitives.ReadInt16BigEndian(GetArraySlice(responseArgumentsArray, 4, 2))) / 10.0;
            return RcpResultType.Success;
        }

        #endregion

        #region 4.13 Set Tx Power Level

        public RcpResultType SetTxPowerLevel(double powerLevel)
        {
            var argumentPayload = BitConverter.GetBytes((short)(powerLevel * 10.0)).Reverse().ToList();
            var rcpResult = ProcessRcpCommand(MessageCode.SetTxPower, out var responseArguments, argumentPayload);
            return (rcpResult == RcpResultType.Success) ? ParseSingleByteResponsePayload(responseArguments) : rcpResult;
        }

        #endregion

        #region 4.14 RF CR signal control

        public RcpResultType RfCwSignalControl(bool cwSignalControl)
        {
            List<byte> argumentPayload = [(byte)(cwSignalControl ? 0xFF : 0x00)];
            var rcpResult =
                ProcessRcpCommand(MessageCode.RfCwSignalControl, out var responseArguments, argumentPayload);
            return (rcpResult == RcpResultType.Success) ? ParseSingleByteResponsePayload(responseArguments) : rcpResult;
        }

        #endregion

        #region 4.15 Read Type C UII

        public RcpResultType ReadTypeCUiiResponse(out TypeCUii tag)
        {
            tag = new TypeCUii();
            var rcpResult = ProcessRcpCommand(MessageCode.ReadTypeCUii, out var responseArgument);
            if (rcpResult != RcpResultType.Success)
                return rcpResult;

            var responseArgumentArray = responseArgument.ToArray();
            tag.Pc = GetArraySlice(responseArgumentArray, 0, 2);
            if (responseArgument.Count > 2)
                tag.Epc = GetArraySlice(responseArgumentArray, 2);
            return RcpResultType.Success;
        }

        // TODO: make sure that the return value of this method is in accodance with the documentation
        public RcpResultType ReadTypeCUiiNotification()
        {
            ClearReceivedCommandAnswerBuffer();
            var command = AssembleRcpCommand(MessageCode.ReadTypeCUii);
            _communicationTransport.TxByteList(command);
            return RcpResultType.Success;
        }

        #endregion

        #region 4.16 Read Type C UII TID

        public RcpResultType ReadTypeCUiiTid(byte maxNumberTagsToRead, byte maxElapsedTimeTagging, short repeatCycle)
        {
            List<byte> argumentPayload = [maxNumberTagsToRead, maxElapsedTimeTagging];
            var rc = BitConverter.GetBytes(repeatCycle).Reverse();
            argumentPayload.AddRange(rc);
            var rcpResult = ProcessRcpCommand(MessageCode.ReadTypeCUiiTid, out var responsePayload, argumentPayload);
            return (rcpResult == RcpResultType.Success) ? ParseSingleByteResponsePayload(responsePayload) : rcpResult;
        }

        #endregion

        #region 4.17 Read Type C Tag Data

        public RcpResultType ReadTypeCTagData(string epc, ParamMemoryBank memoryBank, ushort startAddress, ushort wordCount,
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

            var rcpResult = ProcessRcpCommand(MessageCode.ReadTypeCTagData, out var responsePayload, argumentPayload);
            if (rcpResult != RcpResultType.Success)
                return rcpResult;

            if (responsePayload.Count > 0)
                readData = BitConverter.ToString(responsePayload.ToArray()).Replace("-", "");
            return RcpResultType.Success;
        }

        #endregion

        #region 4.18 Get Frequency Hopping Table

        public RcpResultType GetFrequencyHoppingTable(out List<byte> channelNumbers)
        {
            channelNumbers = new List<byte>();
            var rcpResult = ProcessRcpCommand(MessageCode.GetFrequencyHoppingTable, out var responsePayload);
            if (rcpResult != RcpResultType.Success)
                return rcpResult;

            var responsePayloadArray = responsePayload.ToArray();
            
            for (int i=0; i < responsePayloadArray[0]; i++)
                channelNumbers.Add(responsePayloadArray[i+1]);

            return RcpResultType.Success;
        }

        #endregion

        #region 4.19 Set Frequency Hopping Table

        public RcpResultType SetFrequencyHoppingTable(List<byte> channelNumbers)
        {
            List<byte> argumentPayload = [(byte)channelNumbers.Count];
            argumentPayload.AddRange(channelNumbers);
            var rcpResult = ProcessRcpCommand(MessageCode.SetFrequencyHoppingTable, out var responsePayload,
                argumentPayload);
            return (rcpResult == RcpResultType.Success) ? ParseSingleByteResponsePayload(responsePayload) : rcpResult;
        }

        #endregion

        #region 4.20 Get Modulation Mode

        public RcpResultType GetModulationMode(out ModulationMode modulationMode)
        {
            modulationMode = new ModulationMode();
            var rcpResult = ProcessRcpCommand(MessageCode.GetModulationMode, out var responsePayload);
            if (rcpResult != RcpResultType.Success)
                return rcpResult;

            var responsePayloadArray = responsePayload.ToArray();
            modulationMode.BackscatterLinkFrequency =
                BinaryPrimitives.ReadUInt16BigEndian(GetArraySlice(responsePayloadArray, 0, sizeof(UInt16)));
            modulationMode.RxMod = (ParamModulation)responsePayloadArray[2];
            modulationMode.Dr = (ParamDr)responsePayloadArray[3];

            return RcpResultType.Success;
        }

        #endregion

        #region 4.21 Set Modulation Mode

        public RcpResultType SetModulationMode(ModulationMode modulationMode)
        {
            var blf = BitConverter.GetBytes(modulationMode.BackscatterLinkFrequency).Reverse();
            List<byte> payloadArguments = [0xFF];
            payloadArguments.AddRange(blf);
            payloadArguments.Add((byte)modulationMode.RxMod);
            payloadArguments.Add((byte)modulationMode.Dr);

            var rcpResult = ProcessRcpCommand(MessageCode.SetModulationMode, out var responsePayload, payloadArguments);
            return (rcpResult == RcpResultType.Success) ? ParseSingleByteResponsePayload(responsePayload) : rcpResult;
        }

        #endregion

        #region 4.22 Get Anti-Collision Mode

        public RcpResultType GetAntiCollisionMode(out AntiCollisionModeParameters antiCollisionMode)
        {
            antiCollisionMode = new AntiCollisionModeParameters();
            var rcpResult = ProcessRcpCommand(MessageCode.GetAntiCollisionMode, out var responsePayload);
            if (rcpResult != RcpResultType.Success)
                return rcpResult;
            antiCollisionMode.Mode = (AntiCollisionMode)responsePayload[0];
            antiCollisionMode.QStart = responsePayload[1];
            antiCollisionMode.QMax = responsePayload[2];
            antiCollisionMode.QMin = responsePayload[3];
            return RcpResultType.Success;
        }

        #endregion

        #region 4.23 Set Anti-Collision Mode

        public RcpResultType SetAntiCollisionMode(AntiCollisionModeParameters antiCollisionMode)
        {
            List<byte> argumentPayload = [
                (byte)antiCollisionMode.Mode,
                antiCollisionMode.QStart,
                antiCollisionMode.QMax,
                antiCollisionMode.QMin
            ];
            var rcpResult =
                ProcessRcpCommand(MessageCode.SetAntiCollisionMode, out var responsePayload, argumentPayload);
            return (rcpResult == RcpResultType.Success) ? ParseSingleByteResponsePayload(responsePayload) : rcpResult;
        }

        #endregion

        #region 4.24 Start Auto Read2

        public RcpResultType StartAutoRead2()
        {
            var rcpResult = ProcessRcpCommand(MessageCode.StartAutoRead2, out var responseArguments);
            return (rcpResult == RcpResultType.Success) ? ParseSingleByteResponsePayload(responseArguments) : rcpResult;
        }

        #endregion

        #region 4.25 Start Auto Read RSSI

        public RcpResultType StartAutoReadRssi(TagType tagType, byte maximumNumberOfTags, byte maximumElapsedTime,
            ushort repeatCycle)
        {
            var rc = BitConverter.GetBytes(repeatCycle).Reverse();
            List<byte> argumentPayload =
            [
                (byte)tagType,
                maximumNumberOfTags,
                maximumElapsedTime
            ];
            argumentPayload.AddRange(rc);
            var rcpResult = ProcessRcpCommand(MessageCode.StartAutoReadRssi, out var responsePayload, argumentPayload);
            return (rcpResult == RcpResultType.Success) ? ParseSingleByteResponsePayload(responsePayload) : rcpResult;
        }

        #endregion

        #region 4.26 Stop Auto Read2

        public RcpResultType StopAutoRead2()
        {
            var rcpResult = ProcessRcpCommand(MessageCode.StopAutoRead2, out var responseArguments);
            return (rcpResult == RcpResultType.Success) ? ParseSingleByteResponsePayload(responseArguments) : rcpResult;
        }

        #endregion

        #region 4.27 Start Auto Read2 Ex

        // TODO: Documentation does not show notification field when TagRssi = True. Testing pending with real hardware
        public RcpResultType StartAutoRead2Ex(ParamAutoRead2ExMode mode, bool tagRssi, byte antPort, byte maximumNumberOfTags,
            byte maximumElapsedTime, ushort repeatCycle)
        {
            var rc = BitConverter.GetBytes(repeatCycle).Reverse();
            List<byte> argumentPayload =
            [
                (byte)mode,
                (byte)(tagRssi ? 1 : 0),
                antPort,
                maximumNumberOfTags,
                maximumElapsedTime
            ];
            argumentPayload.AddRange(rc);
            var rcpResult = ProcessRcpCommand(MessageCode.StartAutoRead2Ex, out var responsePayload, argumentPayload);
            return (rcpResult == RcpResultType.Success) ? ParseSingleByteResponsePayload(responsePayload) : rcpResult;
        }

        #endregion

        #region 4.28 Get Frequency Information

        // TODO: documentation says 8 byte response arguments. RED4S is responding with a 9 byte sequence
        public RcpResultType GetFrequencyInformation(out FrequencyInformation frequencyInformation)
        {
            frequencyInformation = new FrequencyInformation();
            var rcpResult = ProcessRcpCommand(MessageCode.GetFrequencyInformation, out var responsePayload);
            if (rcpResult != RcpResultType.Success)
                return rcpResult;

            var payload = responsePayload.ToArray();
            frequencyInformation.Spacing = BinaryPrimitives.ReadUInt16BigEndian(payload.GetArraySlice(0, sizeof(UInt16)));
            frequencyInformation.StartFreq = BinaryPrimitives.ReadUInt32BigEndian(payload.GetArraySlice(2, sizeof(UInt32)));
            frequencyInformation.Channel = payload[6];
            frequencyInformation.RfPreset = (ParamRfPreset)payload[7];
            return RcpResultType.Success;
        }

        #endregion

        #region 4.29 Set Frequency Information

        public RcpResultType SetFrequencyInformation(FrequencyInformation frequencyInformation)
        {
            var spacing = BitConverter.GetBytes(frequencyInformation.Spacing).Reverse();
            var startFreq = BitConverter.GetBytes(frequencyInformation.StartFreq).Reverse();
            var channel = frequencyInformation.Channel;
            var rfPreset = (byte)frequencyInformation.RfPreset;

            List<byte> payloadArgument = [];
            payloadArgument.AddRange(spacing);
            payloadArgument.AddRange(startFreq);
            payloadArgument.Add(channel);
            payloadArgument.Add(rfPreset);

            var rcpResult = ProcessRcpCommand(MessageCode.SetFrequencyInformation, out var responseAnswer,
                payloadArgument);
            return (rcpResult == RcpResultType.Success) ? ParseSingleByteResponsePayload(responseAnswer) : rcpResult;
        }

        #endregion

        #region 4.30 Write Type C Data

        public RcpResultType WriteTypeCTagData(string epc, ParamMemoryBank memoryBank, ushort startAddress, string dataToWrite,
            UInt32 accessPassword = 0)
        {
            var ap = BitConverter.GetBytes(accessPassword).Reverse();
            var epcByteArray = Convert.FromHexString(epc);
            var ul = BitConverter.GetBytes((ushort)epcByteArray.Length).Reverse();
            var mb = (byte)memoryBank;
            var sa = BitConverter.GetBytes(startAddress).Reverse();
            var dt = Convert.FromHexString(dataToWrite);
            var dl = BitConverter.GetBytes((ushort)dt.Length).Reverse();

            List<byte> argumentPayload = [];
            argumentPayload.AddRange(ap);
            argumentPayload.AddRange(ul);
            argumentPayload.AddRange(epcByteArray);
            argumentPayload.Add(mb);
            argumentPayload.AddRange(sa);
            argumentPayload.AddRange(dl);
            argumentPayload.AddRange(dt);

            var rcpResult =
                ProcessRcpCommand(MessageCode.WriteTypeCTagData, out var responseArguments, argumentPayload);
            var payload = responseArguments.ToArray();

            for (int i=0; i < payload.Length; i++)
                if (epcByteArray[i] != payload[i])
                    return RcpResultType.OtherError;
            return RcpResultType.Success;
        }

        #endregion

        #region 4.31 BlockWrite Type C Tag Data

        public RcpResultType BlockWriteTypeCTagData(string epc, ParamMemoryBank memoryBank, ushort startAddress, string dataToWrite,
            UInt32 accessPassword = 0)
        {
            var ap = BitConverter.GetBytes(accessPassword).Reverse();
            var epcByteArray = Convert.FromHexString(epc);
            var ul = BitConverter.GetBytes((ushort)epcByteArray.Length).Reverse();
            var mb = (byte)memoryBank;
            var sa = BitConverter.GetBytes(startAddress).Reverse();
            var dt = Convert.FromHexString(dataToWrite);
            var dl = BitConverter.GetBytes((ushort)dt.Length).Reverse();

            List<byte> argumentPayload = [];
            argumentPayload.AddRange(ap);
            argumentPayload.AddRange(ul);
            argumentPayload.AddRange(epcByteArray);
            argumentPayload.Add(mb);
            argumentPayload.AddRange(sa);
            argumentPayload.AddRange(dl);
            argumentPayload.AddRange(dt);

            var rcpResult = ProcessRcpCommand(MessageCode.BlockWriteTypeCTagData, out var responseArguments,
                argumentPayload);
            return (rcpResult == RcpResultType.Success) ? ParseSingleByteResponsePayload(responseArguments) : rcpResult;
        }

        #endregion

        #region 4.32 BlockErase Type C Tag Data

        public RcpResultType BlockEraseTypeCTagData(string epc, ParamMemoryBank memoryBank, ushort startAddress, ushort dataLength, 
            UInt32 accessPassword = 0)
        {
            var ap = BitConverter.GetBytes(accessPassword).Reverse();
            var epcByteArray = Convert.FromHexString(epc);
            var ul = BitConverter.GetBytes((ushort)epcByteArray.Length).Reverse();
            var mb = (byte)memoryBank;
            var sa = BitConverter.GetBytes(startAddress).Reverse();
            var dl = BitConverter.GetBytes(dataLength).Reverse();

            List<byte> argumentPayload = [];
            argumentPayload.AddRange(ap);
            argumentPayload.AddRange(ul);
            argumentPayload.AddRange(epcByteArray);
            argumentPayload.Add(mb);
            argumentPayload.AddRange(sa);
            argumentPayload.AddRange(dl);

            var rcpResult = ProcessRcpCommand(MessageCode.BlockEraseTypeCTagData, out var responseArguments,
                argumentPayload);
            return (rcpResult == RcpResultType.Success) ? ParseSingleByteResponsePayload(responseArguments) : rcpResult;
        }

        #endregion

        #region 4.33 BlockPermalock Type C Tag

        public RcpResultType BlockPermalockTypeCTag(string epc, ParamReadLock readLock, ParamMemoryBank memoryBank,
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

            List<byte> argumentPayload = [];
            argumentPayload.AddRange(ap);
            argumentPayload.AddRange(ul);
            argumentPayload.AddRange(epcByteArray);
            argumentPayload.Add(rfu);
            argumentPayload.Add(rl);
            argumentPayload.Add(mb);
            argumentPayload.AddRange(bp);
            argumentPayload.Add(br);
            argumentPayload.AddRange(maskByteArray);

            var rcpResult = ProcessRcpCommand(MessageCode.BlockPermalockTypeCTag, out var responseArgument,
                argumentPayload);
            return (rcpResult == RcpResultType.Success) ? ParseSingleByteResponsePayload(responseArgument) : rcpResult;
        }

        #endregion

        #region 4.34 Kill Type C Tag

        public RcpResultType KillTypeCTag(UInt32 killPassword, string epc)
        {
            var kp = BitConverter.GetBytes(killPassword).Reverse();
            var epcByteArray = Convert.FromHexString(epc);
            var ul = BitConverter.GetBytes((ushort)epcByteArray.Length).Reverse();

            List<byte> argumentPayload = [];
            argumentPayload.AddRange(kp);
            argumentPayload.AddRange(ul);
            argumentPayload.AddRange(epcByteArray);

            var rcpResult = ProcessRcpCommand(MessageCode.KillRecomTypeCTag, out var responsePayload, argumentPayload);
            return (rcpResult == RcpResultType.Success) ? ParseSingleByteResponsePayload(responsePayload) : rcpResult;
        }

        #endregion

        #region 4.35 Lock Type C Tag

        public RcpResultType LocTypeCTag(string epc, UInt32 lockMaskAction, UInt32 accessPassword)
        {
            var ap = BitConverter.GetBytes(accessPassword).Reverse();
            var epcByteArray = Convert.FromHexString(epc);
            var ul = BitConverter.GetBytes((ushort)epcByteArray.Length).Reverse();
            var ld = BitConverter.GetBytes(lockMaskAction).Reverse().ToArray();

            List<byte> argumentPayload = [];
            argumentPayload.AddRange(ap);
            argumentPayload.AddRange(ul);
            argumentPayload.AddRange(epcByteArray);
            argumentPayload.AddRange(ld.GetArraySlice(1));

            var rcpResult = ProcessRcpCommand(MessageCode.LockTypeCTag, out var responsePayload, argumentPayload);
            return (rcpResult == RcpResultType.Success) ? ParseSingleByteResponsePayload(responsePayload) : rcpResult;
        }

        #endregion

        #region 4.36 Get Selection Enable

        public RcpResultType GetSelectionEnable(out byte enableStatus)
        {
            enableStatus = 0;
            var rcpResult = ProcessRcpCommand(MessageCode.GetSelectionEnable, out var responsePayload);
            if (rcpResult != RcpResultType.Success)
                return rcpResult;
            enableStatus = responsePayload[0];
            return RcpResultType.Success;
        }

        public RcpResultType GetSelectionEnable(out EnableStatus enableStatus)
        {
            enableStatus = new EnableStatus();
            var rcpResult = ProcessRcpCommand(MessageCode.GetSelectionEnable, out var responsePayload);
            if (rcpResult != RcpResultType.Success)
                return rcpResult;
            enableStatus = responsePayload[0].ToEnableStatus();
            return RcpResultType.Success;
        }

        #endregion

        #region 4.37 Set Selection Enable

        public RcpResultType SetSelectionEnable(byte enableStatus)
        {
            var rcpResult = ProcessRcpCommand(MessageCode.SetSelectionEnable, out var responsePayload, [enableStatus]);
            return (rcpResult == RcpResultType.Success) ? ParseSingleByteResponsePayload(responsePayload) : rcpResult;
        }

        public RcpResultType SetSelectionEnable(EnableStatus enableStatus)
        {
            var rcpResult = ProcessRcpCommand(MessageCode.SetSelectionEnable, out var responsePayload,
                [enableStatus.ToByte()]);
            return (rcpResult == RcpResultType.Success) ? ParseSingleByteResponsePayload(responsePayload) : rcpResult;
        }

        #endregion

        #region 4.38 Get Multi-Antenna Sequence

        public RcpResultType GetMultiAntennaSequence(out List<byte> antennaSequence)
        {
            antennaSequence = [];
            var rcpResult = ProcessRcpCommand(MessageCode.GetMultiAntennaSequence, out var responsePayload);
            if (rcpResult != RcpResultType.Success)
                return rcpResult;

            antennaSequence.AddRange(responsePayload.ToArray().GetArraySlice(1));
            return RcpResultType.Success;
        }

        #endregion

        #region 4.39 Set Multi-Antenna Sequence

        public RcpResultType SetMultiAntennaSequence(List<byte> antennaSequence)
        {
            List<byte> payloadArguments = [(byte)antennaSequence.Count];
            payloadArguments.AddRange(antennaSequence);
            var rcpResult = ProcessRcpCommand(MessageCode.SetMultiAntennaSequence, out var responsePayload,
                payloadArguments);
            return (rcpResult == RcpResultType.Success) ? ParseSingleByteResponsePayload(responsePayload) : rcpResult;
        }

        #endregion

        #region 4.40 Antenna Check

        public RcpResultType AntennaCheck(byte refLevel, out AntennaCheckError antennaError)
        {
            antennaError = new AntennaCheckError();
            ClearReceivedCommandAnswerBuffer();
            var command = AssembleRcpCommand(MessageCode.AntennaCheck, [refLevel]);
            _communicationTransport.TxByteList(command);
            if (!_receivedCommandAnswerBuffer.TryTake(out var commandAnswer, Constants.RcpCommandMaxResponseTimeMs))
            {
                Log.Warning($"Command {MessageCode.AntennaCheck} timeout out");
                return RcpResultType.NoResponse;
            }

            if (commandAnswer.Count == 0)
                return RcpResultType.OtherError;

            // Extract command response code and command error flag. Leave commandAnswer as the response payload
            var commandResponseCode = commandAnswer.First();
            commandAnswer.RemoveAt(0);
            var commandErrorFlag = (ErrorFlag)commandAnswer.First();
            commandAnswer.RemoveAt(0);

            switch (commandResponseCode)
            {
                case (byte)MessageCode.AntennaCheck:
                    return ParseSingleByteResponsePayload(commandAnswer);
                case (byte)MessageCode.CommandFailure:
                    if (commandAnswer.Count != 3)
                        return RcpResultType.OtherError;
                    antennaError.IsFailure = true;
                    antennaError.ErrorCode = commandAnswer[0];
                    antennaError.SubErrorCode = commandAnswer[2];
                    return RcpResultType.ReaderError;
                default:
                    return RcpResultType.OtherError;
            }
        }

        #endregion

        #region 4.41 Get Selection

        public RcpResultType GetSelection(byte index, out Selection selection)
        {
            selection = new Selection();
            var rcpResult = ProcessRcpCommand(MessageCode.GetSelection, out var responsePayload, [index]);
            if (rcpResult != RcpResultType.Success)
                return rcpResult;
            
            var payload = responsePayload.ToArray();
            selection.Length = payload[6];
            var lengthBytes = selection.Length >> 3;
            // Round up if 3 least significant bits are not zero
            lengthBytes += (((ushort)(selection.Length) & 0x07) > 0) ? 1 : 0;

            selection.Index = payload[0];
            selection.Target = (ParamTarget)payload[1];
            selection.Action = (ParamSelectAction)payload[2];
            selection.MemoryBank = (ParamMemoryBank)payload[3];
            selection.Pointer = BinaryPrimitives.ReadUInt16BigEndian(payload.GetArraySlice(4, sizeof(UInt16)));
            if (selection.Length > 0)
                selection.Mask.AddRange(payload.GetArraySlice(7));
            return RcpResultType.Success;
        }

        #endregion

        #region 4.42 Set Selection

        public RcpResultType SetSelection(Selection selection)
        {
            List<byte> argumentPayload =
            [
                selection.Index,
                (byte)selection.Target,
                (byte)selection.Action,
                (byte)selection.MemoryBank
            ];
            argumentPayload.AddRange(BitConverter.GetBytes(selection.Pointer).Reverse());
            argumentPayload.Add(selection.Length);
            argumentPayload.AddRange(selection.Mask);

            var rcpResult = ProcessRcpCommand(MessageCode.SetSelection, out var responseArgument, argumentPayload);
            return (rcpResult == RcpResultType.Success) ? ParseSingleByteResponsePayload(responseArgument) : rcpResult;
        }

        #endregion

        #region 4.43 Get RSSI

        public RcpResultType GetRssi(out double rssi)
        {
            rssi = 0.0;
            var rcpResult = ProcessRcpCommand(MessageCode.GetRssi, out var responsePayload);
            if (rcpResult != RcpResultType.Success)
                return rcpResult;
            var ushortRssi = BinaryPrimitives.ReadUInt16BigEndian(responsePayload.ToArray());
            rssi = ((double)ushortRssi) / 10.0;
            return RcpResultType.Success;
        }

        #endregion

        #region 4.44 Scan RSSI

        public RcpResultType ScanRssi(out ScanRssiParameters rssiParameters)
        {
            rssiParameters = new ScanRssiParameters();
            var rcpResult = ProcessRcpCommand(MessageCode.ScanRssi, out var responseArguments);
            if (rcpResult != RcpResultType.Success)
                return rcpResult;

            var payload = responseArguments.ToArray();
            rssiParameters.StartChannelNumber = payload[0];
            rssiParameters.StopChannelNumber = payload[1];
            rssiParameters.BestChannelNumber = payload[2];
            rssiParameters.RssiLevels.AddRange(payload.GetArraySlice(3));
            return RcpResultType.Success;
        }

        #endregion

        #region 4.45 Get DTC Result

        public RcpResultType GetDtcResult(out DtcResultResponseParameters parameters)
        {
            parameters = new DtcResultResponseParameters();
            var rcpResult = ProcessRcpCommand(MessageCode.GetDtcResult, out var responseArguments);
            if (rcpResult != RcpResultType.Success)
                return rcpResult;
            
            parameters.InductorNumber = responseArguments[0];
            parameters.DigitalTunableCapacitor1 = responseArguments[1];
            parameters.DigitalTunableCapacitor2 = responseArguments[2];
            parameters.LeakageRssi = responseArguments[3];
            parameters.LeakageCancellationAlgorithmStateNumber = responseArguments[4];

            return RcpResultType.Success;
        }

        #endregion

        #region 4.46 Update Registry

        public RcpResultType UpdateRegistry()
        {
            var rcpResult = ProcessRcpCommand(MessageCode.UpdateRegistry, out var responseArguments);
            return (rcpResult == RcpResultType.Success) ? ParseSingleByteResponsePayload(responseArguments) : rcpResult;
        }

        #endregion

        #region 4.47 Get Registry Item

        public RcpResultType GetRegistryItem(Registry registry, out RegistryItem item)
        {
            item = new RegistryItem();
            var argumentPayload = new List<byte>();
            argumentPayload.AddRange(BitConverter.GetBytes((ushort)registry).Reverse());
            var rcpResult = ProcessRcpCommand(MessageCode.GetRegistryItem, out var responseArguments, argumentPayload);
            if (rcpResult != RcpResultType.Success)
                return rcpResult;
            item.Active = (RegistryItemStatus)responseArguments[0];
            if (responseArguments.Count > 1)
                item.Data.AddRange(responseArguments.ToArray().GetArraySlice(1));
            return RcpResultType.Success;
        }

        #endregion

        #region 4.48 Set Optimum Frequency Hopping Table

        // TODO: verify on real hardware if this is the correct flow of the command
        public RcpResultType SetOptimumFrequencyHoppingTable(int maxTimeOutMs = 2*Constants.RcpCommandMaxResponseTimeMs)
        {
            var rcpResult = ProcessRcpCommand(MessageCode.SetOptimumFrequencyHoppingTable, out var responseArgument);
            if (rcpResult != RcpResultType.Success)
                return rcpResult;
            if (ParseSingleByteResponsePayload(responseArgument) != RcpResultType.Success)
                return RcpResultType.OtherError;

            if (!_receivedCommandAnswerBuffer.TryTake(out var stopResponseArgument,
                    maxTimeOutMs))
                return RcpResultType.NoResponse;

            var stopResponseCode = stopResponseArgument[0];
            var commandErrorFlag = stopResponseArgument[1];
            var payload = stopResponseArgument[2];

            if ((stopResponseCode == (byte)MessageCode.SetOptimumFrequencyHoppingTable) &&
                (commandErrorFlag == (byte)ErrorFlag.NoError) && (payload == (byte)0x01))
                return RcpResultType.Success;
            return RcpResultType.OtherError;
        }

        #endregion

        #region 4.49 GetFrequency Hopping Mode

        public RcpResultType GetFrequencyHoppingMode(out ParamFrequencyHoppingMode mode)
        {
            mode = ParamFrequencyHoppingMode.NormalMode;
            var rcpResult = ProcessRcpCommand(MessageCode.GetFrequencyHoppingMode, out var responseArgument);
            if (rcpResult != RcpResultType.Success)
                return rcpResult;
            mode = (ParamFrequencyHoppingMode)responseArgument[0];
            return RcpResultType.Success;
        }

        #endregion

        #region 4.50 Set Frequency Hopping Mode

        public RcpResultType SetFrequencyHoppingMode(ParamFrequencyHoppingMode mode)
        {
            List<byte> argumentPayload = [(byte)mode];
            var rcpResult = ProcessRcpCommand(MessageCode.SetFrequencyHoppingMode, out var responseArgument,
                argumentPayload);
            return (rcpResult == RcpResultType.Success) ? ParseSingleByteResponsePayload(responseArgument) : rcpResult;
        }

        #endregion

        #region 4.51 Get Tx Leakage RSSI Level for Smart Hopping Mode

        public RcpResultType GetTxLeakageRssiLevelForSmartHoppingMode(out byte reference)
        {
            reference = 0;
            var rcpResult = ProcessRcpCommand(MessageCode.GetTxLeakageRssiLevelSmartHoppingMode,
                out var responseArgument);
            if (rcpResult != RcpResultType.Success)
                return rcpResult;
            reference = responseArgument[0];
            return RcpResultType.Success;
        }

        #endregion

        #region 4.52 Set Tx Leakage RSSI Level for Smart Hopping Mode

        public RcpResultType SetTxLeakageRssiLevelForSmartHoppingMode(byte reference)
        {
            var rcpResult = ProcessRcpCommand(MessageCode.SetTxLeakageRsiiLevelSmartHoppingMode,
                out var responseArgument, [reference]);
            return (rcpResult == RcpResultType.Success) ? ParseSingleByteResponsePayload(responseArgument) : rcpResult;
        }

        #endregion

        #endregion

        private RcpResultType ProcessRcpCommand(MessageCode messageCode, out List<byte> responsePayload, List<byte> commandPayload = null)
        {
            ClearReceivedCommandAnswerBuffer();
            responsePayload = new List<byte>();
            var command = AssembleRcpCommand(messageCode, commandPayload);
            _communicationTransport.TxByteList(command);
            if (!_receivedCommandAnswerBuffer.TryTake(out var commandAnswer, Constants.RcpCommandMaxResponseTimeMs))
            {
                Log.Warning($"Command {messageCode} timed out");
                return RcpResultType.NoResponse;
            }

            if (commandAnswer.Count == 0)
                return RcpResultType.OtherError;

            // Extract command response code and command error flag. Leave commandAnswer as the response payload
            var commandResponseCode = commandAnswer.First();
            commandAnswer.RemoveAt(0);
            var commandErrorFlag = (ErrorFlag)commandAnswer.First();
            commandAnswer.RemoveAt(0);
                
            if (commandResponseCode == (byte)messageCode)
            {
                // Command response
                responsePayload = commandAnswer;
                switch (commandErrorFlag)
                {
                    case ErrorFlag.NoError:
                        return RcpResultType.Success;
                    case ErrorFlag.Error:
                        return RcpResultType.ReaderError;
                    default:
                        throw new ArgumentException($"Invalid command error flag {commandErrorFlag}");
                }
            }

            Log.Warning($"RCP message code error. Expected {(byte)messageCode}, received {commandResponseCode}");
            return RcpResultType.OtherError;
        }

        private T[] GetArraySlice<T>(T[] inputArray, int startIndex, int elementCount)
        {
            return (new ArraySegment<T>(inputArray)).Slice(startIndex, elementCount).ToArray();
        }

        private T[] GetArraySlice<T>(T[] inputArray, int startIndex)
        {
            return (new ArraySegment<T>(inputArray)).Slice(startIndex, inputArray.Length - startIndex).ToArray();
        }

        private RcpResultType ParseSingleByteResponsePayload(List<byte> responsePayload)
        {
            if (responsePayload.Count != 1)
                return RcpResultType.OtherError;
            return (responsePayload[Constants.ResponseArgOffset] == Constants.Success)
                ? RcpResultType.Success
                : RcpResultType.OtherError;
        }

        private int GetEpcByteLengthFromPc(ushort pc)
        {
            var wordCount = (pc >> 11);
            return wordCount * 2;
        }
    }
}
