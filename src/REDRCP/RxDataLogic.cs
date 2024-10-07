using System.Buffers.Binary;
using System.Collections.Concurrent;

namespace Kliskatek.Driver.Rain.REDRCP
{
    public partial class REDRCP
    {
        private readonly BlockingCollection<List<byte>> _receivedCommandAnswerBuffer = new();
        private readonly object _lockRxData = new();

        private void ClearReceivedCommandAnswerBuffer()
        {
            while (_receivedCommandAnswerBuffer.TryTake(out _)) { }
        }

        private void OnCommunicationBusByteReceived(byte rxByte)
        {
            lock (_lockRxData)
            {
                if (!TryDecodeRxByte(rxByte))
                    return;
                if (!Enum.IsDefined(typeof(MessageType), (int)_rcpMessageType))
                    return;
                switch ((MessageType)_rcpMessageType)
                {
                    case MessageType.Response:
                        _rcpPayloadBuffer.Insert(0, _rcpCode);
                        _receivedCommandAnswerBuffer.Add(_rcpPayloadBuffer);
                        break;
                    case MessageType.Notification:
                        ProcessRxNotificationMessage();
                        break;
                    default:
                        break;
                }
            }
        }

        private void ProcessRxNotificationMessage()
        {
            if (!Enum.IsDefined(typeof(MessageCode), (int)_rcpCode))
                return;
            switch ((MessageCode)_rcpCode)
            {
                case MessageCode.ReadTypeCUii:
                    if (_autoRead2Ongoing > 0)
                        HandleAutoRead2Ongoing();
                    if (_readTypeCUiiOngoing > 0)
                        HandleReadTypeCUiiOngoing();
                    break;
                case MessageCode.ReadTypeCUiiTid:
                    if (_readTypeCUiiTidOngoing > 0)
                        HandleReadTypeCUiiTidOngoing();
                    break;
                case MessageCode.ReadTypeCUiiRssi:
                    if (_autoReadRssiOngoing > 0)
                        HandleAutoReadRssiOngoing();
                    break;
                case MessageCode.StartAutoReadRssi:
                    if (_autoReadRssiOngoing > 0)
                        HandleAutoReadRssiOngoing();
                    break;
                case MessageCode.ReadTypeCUiiEx2:
                    if (_autoRead2ExOngoing > 0)
                        HandleAutoRead2ExOngoing();
                    break;
                case MessageCode.StartAutoRead2Ex:
                    if (_autoRead2ExOngoing > 0)
                        HandleAutoRead2ExOngoing();
                    break;
                case MessageCode.GetDtcResult:
                    if (_getDtcResultOngoing > 0)
                        HandleGetDtcResult();
                    if (_setOptimumFrequencyHoppingTableOngoing > 0)
                        HandleSetOptimumFrequencyHoppingTable();
                    break;
                default:
                    break;
            }
        }

        private void HandleAutoRead2Ongoing()
        {
            if (_rcpPayloadBuffer.Count >1)
            {
                var payloadByteArray = _rcpPayloadBuffer.ToArray();

                var pc = BitConverter.ToString(payloadByteArray.GetArraySlice(0, 2)).Replace("-", "");
                var epc = string.Empty;
                if (_rcpPayloadBuffer.Count > 2)
                    epc = BitConverter.ToString(payloadByteArray.GetArraySlice(2)).Replace("-", "");

                _autoRead2NotificationCallback(pc, epc);
            }
        }

        private void HandleReadTypeCUiiOngoing()
        {
            // TODO: consider removing this line if MsgCode 0x22 (read type C UII returns more than one tag)
            Interlocked.Exchange(ref _readTypeCUiiOngoing, 0);
            if (_rcpPayloadBuffer.Count > 1)
            {
                var payloadByteArray = _rcpPayloadBuffer.ToArray();
                var pc = BitConverter.ToString(payloadByteArray.GetArraySlice(0, 2)).Replace("-", "");
                var epc = string.Empty;
                if (_rcpPayloadBuffer.Count > 2)
                    epc = BitConverter.ToString(payloadByteArray.GetArraySlice(2)).Replace("-", "");

                _readTypeCUiiNotificationCallback(pc, epc);
            }
        }

        private void HandleReadTypeCUiiTidOngoing()
        {
            switch (_rcpPayloadBuffer.Count)
            {
                // Operation complete when Arg == 0x1F
                case 1:
                    if (_rcpPayloadBuffer[0] == 0x1F)
                        Interlocked.Exchange(ref _readTypeCUiiTidOngoing, 0);
                    break;
                default:
                    var payload = _rcpPayloadBuffer.ToArray();
                    ushort pcUshort = 0;
                    string pc = "";
                    string epc = "";
                    string tid = "";
                    // Check if payload can store PC
                    if (payload.Length < 2)
                        return;
                    pcUshort = BinaryPrimitives.ReadUInt16BigEndian(GetArraySlice(payload, 0, sizeof(UInt16)));
                    pc = BitConverter.ToString(payload.GetArraySlice(0, sizeof(UInt16))).Replace("-", "");
                    // Check if payload can store PC + EPC
                    var epcByteLength = GetEpcByteLengthFromPc(pcUshort);
                    if (payload.Length < 2 + epcByteLength)
                        return;
                    epc = BitConverter.ToString(payload.GetArraySlice(2, epcByteLength)).Replace("-", "");
                    if (payload.Length > 2 + epcByteLength)
                        tid = BitConverter.ToString(payload.GetArraySlice(2 + epcByteLength)).Replace("-", "");
                    _readTypeCUiiTidNotificationCallback(pc, epc, tid);
                    break;
            }
        }

        private void HandleAutoReadRssiOngoing()
        {
            switch (_rcpPayloadBuffer.Count)
            {
                case 1:
                    if (_rcpPayloadBuffer[0] == 0x1F)
                        Interlocked.Exchange(ref _autoReadRssiOngoing, 0);
                    break;
                default:
                    var payload = _rcpPayloadBuffer.ToArray();
                    ushort pcUshort = 0;
                    string pc = "";
                    string epc = "";
                    // Check if payload can store PC
                    if (payload.Length < 2)
                        return;
                    pcUshort = BinaryPrimitives.ReadUInt16BigEndian(GetArraySlice(payload, 0, sizeof(UInt16)));
                    pc = BitConverter.ToString(payload.GetArraySlice(0, sizeof(UInt16))).Replace("-", "");
                    // Check if payload can store PC + EPC + RSSI + GAIN
                    var epcByteLength = GetEpcByteLengthFromPc(pcUshort);
                    if (payload.Length != 2 + epcByteLength + 4)
                        return;
                    epc = BitConverter.ToString(payload.GetArraySlice(2, epcByteLength)).Replace("-", "");
                    // Pointer to RSSI / GAIN parameters in payload array
                    var pointer = 2 + epcByteLength;
                    var rssiI = payload[pointer++];
                    var rssiQ = payload[pointer++];
                    var gainI = payload[pointer++];
                    var gainQ = payload[pointer++];
                    _autoReadRssiNotificationCallback(pc, epc, rssiI, rssiQ, gainI, gainQ);
                    break;
            }
        }

        private void HandleAutoRead2ExOngoing()
        {
            switch (_rcpPayloadBuffer.Count)
            {
                case 1:
                    if (_rcpPayloadBuffer[0] == 0x1F)
                        Interlocked.Exchange(ref _autoRead2ExOngoing, 0);
                    break;
                default:
                    var payload = _rcpPayloadBuffer.ToArray();
                    // Check if payload can store Mode + Tag RSSI + AntPort + PC
                    if (payload.Length < 5)
                        return;
                    var mode = (ParamAutoRead2ExMode)payload[0];
                    bool tagRssi = payload[1] > 0;
                    var antPort = payload[2];
                    ushort pcUshort = BinaryPrimitives.ReadUInt16BigEndian(GetArraySlice(payload, 3, sizeof(UInt16)));
                    var epcByteLength = GetEpcByteLengthFromPc(pcUshort);
                    // Check if payload can store Mode + Tag RSSI + AntPort + PC + EPC
                    if (payload.Length < 5 + epcByteLength)
                        return;
                    string pc = BitConverter.ToString(payload.GetArraySlice(3, sizeof(UInt16))).Replace("-", "");
                    string epc = BitConverter.ToString(payload.GetArraySlice(5, epcByteLength)).Replace("-", "");

                    byte rssiI = 0;
                    byte rssiQ = 0;
                    byte gainI = 0;
                    byte gainQ = 0;

                    if (tagRssi)
                    {
                        // Check if payload can store Mode + Tag RSSI + AntPort + PC + EPC + RSSI_I + RSSI_Q + GAIN_I + GAIN_Q
                        if (payload.Length != 5 + epcByteLength + 4)
                            return;
                        // Pointer to RSSI / GAIN parameters in payload array
                        var pointer = 5 + epcByteLength;
                        rssiI = payload[pointer++];
                        rssiQ = payload[pointer++];
                        gainI = payload[pointer++];
                        gainQ = payload[pointer++];
                    }
                    _autoRead2ExNotificationCallback(mode, tagRssi, antPort, pc, epc, rssiI, rssiQ, gainI, gainQ);
                    break;
            }
        }

        private void HandleGetDtcResult()
        {
            switch (_rcpPayloadBuffer.Count)
            {
                case 7:
                    var parameters = new DtcResultNotificationParameters();
                    int arrayPointer = 0;
                    parameters.InductorNumber = _rcpPayloadBuffer[arrayPointer++];
                    parameters.DigitalTunableCapacitor1 = _rcpPayloadBuffer[arrayPointer++];
                    parameters.DigitalTunableCapacitor2 = _rcpPayloadBuffer[arrayPointer++];
                    parameters.LeakageRssi = _rcpPayloadBuffer[arrayPointer++];
                    parameters.LeakageCancellationAlgorithmStateNumber = _rcpPayloadBuffer[arrayPointer++];
                    parameters.CurrentChannel = _rcpPayloadBuffer[arrayPointer++];
                    parameters.LeakageCancellationOperationTime = _rcpPayloadBuffer[arrayPointer++];
                    _getDtcResultNotificationCallback(parameters);
                    break;
                default:
                    break;
            }
        }

        private void HandleSetOptimumFrequencyHoppingTable()
        {
            switch (_rcpPayloadBuffer.Count)
            {
                case 7:
                    var parameters = new DtcResultNotificationParameters();
                    int arrayPointer = 0;
                    parameters.InductorNumber = _rcpPayloadBuffer[arrayPointer++];
                    parameters.DigitalTunableCapacitor1 = _rcpPayloadBuffer[arrayPointer++];
                    parameters.DigitalTunableCapacitor2 = _rcpPayloadBuffer[arrayPointer++];
                    parameters.LeakageRssi = _rcpPayloadBuffer[arrayPointer++];
                    parameters.LeakageCancellationAlgorithmStateNumber = _rcpPayloadBuffer[arrayPointer++];
                    parameters.CurrentChannel = _rcpPayloadBuffer[arrayPointer++];
                    parameters.LeakageCancellationOperationTime = _rcpPayloadBuffer[arrayPointer++];
                    _setOptimmumFrequencyHoppingTableNotificationCallback(parameters);
                    break;
                default:
                    break;
            }
        }
    }
}
