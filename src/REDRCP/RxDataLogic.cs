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
                    pcUshort = BinaryPrimitives.ReadUInt16BigEndian(GetArraySlice(payload, 0, 2));
                    pc = BitConverter.ToString(payload.GetArraySlice(0, 2)).Replace("-", "");
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
    }
}
