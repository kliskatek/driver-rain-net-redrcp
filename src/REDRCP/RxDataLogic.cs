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
                case MessageCode.ReadTpeCuiii:
                    if (_autoRead2Ongoing > 0)
                    {
                        // Remove PC from returned byte array
                        if (_rcpPayloadBuffer.Count > 2)
                        {
                            var payloadByteArray = _rcpPayloadBuffer.ToArray();

                            var pc = BitConverter.ToString(payloadByteArray.GetArraySlice(0, 2)).Replace("-", "");
                            var epc = BitConverter.ToString(payloadByteArray.GetArraySlice(2)).Replace("-", "");

                            _autoRead2NotificationCallback(pc, epc);
                        }
                    }
                    break;
                default:
                    break;
            }
        }
    }
}
