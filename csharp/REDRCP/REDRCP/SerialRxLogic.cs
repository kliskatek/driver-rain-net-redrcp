using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kliskatek.REDRCP
{
    public partial class REDRCP
    {
        private ConcurrentQueue<byte> _rxByteBuffer = new ConcurrentQueue<byte>();
        private BlockingCollection<List<byte>> _receivedCommandAnswerBuffer = new BlockingCollection<List<byte>>();
        private readonly object _lockRxData = new();
        private int _runProcessRxBufferThread = 0;

        private void ClearReceivedCommandAnswerBuffer()
        {
            while(_receivedCommandAnswerBuffer.TryTake(out _)) { }
        }

        private void OnSerialPortDataReceived(object s, SerialDataReceivedEventArgs e)
        {
            lock (_lockRxData)
            {
                byte[] data = new byte[_serialPort.BytesToRead];
                _serialPort.Read(data, 0, data.Length);
                for (int i = 0; i < data.Length; i++)
                    _rxByteBuffer.Enqueue(data[i]);
            }
        }

        private void ProcessRxByteBuffer()
        {
            UpdateRcpState(RcpState.Preamble);
            while (_runProcessRxBufferThread > 0)
            {
                if (!_rxByteBuffer.TryDequeue(out var newByte))
                    continue;
                if (TryDecodeRxByte(newByte))
                    continue;
                // Get RCP package
                if (!Enum.IsDefined(typeof(MessageType), _rcpMessageType))
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

        private void ProcessRxNotificationMessage()
        {
            if (!Enum.IsDefined(typeof(MessageCode), _rcpCode))
                return;
            switch ((MessageCode)_rcpCode)
            {
                case MessageCode.StartAutoRead2:
                    if (_autoRead2Ongoing > 0)
                    {
                        // Remove PC from returned byte array
                        if (_rcpPayloadBuffer.Count > 2)
                        {
                            _rcpPayloadBuffer.RemoveRange(0, 2);
                            var epc = BitConverter.ToString(_rcpPayloadBuffer.ToArray()).Replace("-", "");
                            _epcCallback(epc);
                        }
                    }
                    break;
                default:
                    break;
            }
        }
    }
}
