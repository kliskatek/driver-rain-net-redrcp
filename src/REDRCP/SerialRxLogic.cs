using System.Collections.Concurrent;
using System.IO.Ports;

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

        private void OnSerialPortDataReceived(object s, SerialDataReceivedEventArgs e)
        {
            lock (_lockRxData)
            {
                byte[] data = new byte[_serialPort.BytesToRead];
                _serialPort.Read(data, 0, data.Length);
                for (int i = 0; i < data.Length; i++)
                {
                    if (!TryDecodeRxByte(data[i]))
                        continue;
                    if (!Enum.IsDefined(typeof(MessageType), (int)_rcpMessageType))
                        continue;
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
