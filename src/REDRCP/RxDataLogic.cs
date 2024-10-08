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
                        OnNewTransportNotificationReceived();
                        break;
                    default:
                        break;
                }
            }
        }
    }
}
