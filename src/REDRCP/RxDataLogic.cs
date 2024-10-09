using System.Collections.Concurrent;
using Serilog;

namespace Kliskatek.Driver.Rain.REDRCP
{
    public partial class REDRCP
    {
        private readonly BlockingCollection<List<byte>> _receivedCommandAnswerBuffer = new();
        private readonly object _lockRxData = new();

        public event EventHandler<ErrorNotificationEventArgs> NewErrorReceived;

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
                        // Check if response code is "Command Failure" 
                        if (_rcpCode == (byte)MessageCode.CommandFailure)
                        {
                            // If response code is "Command Failure", handle error message
                            if (_rcpPayloadBuffer.Count != 3)
                                return;
                            try
                            {
                                var commandCode = (MessageCode)_rcpPayloadBuffer[1];
                                var errorCode = (ErrorCode)_rcpPayloadBuffer[2];
                                AddUpdateMessageCodeError(commandCode, errorCode);
                                _receivedCommandAnswerBuffer.Add([
                                    (byte)commandCode,
                                    (byte)ErrorFlag.Error,
                                    (byte)errorCode
                                ]);
                                NewErrorReceived?.Invoke(
                                    this,
                                    new ErrorNotificationEventArgs
                                    {
                                        ErrorCode = errorCode,
                                        CommandCode = commandCode
                                    });
                            }
                            catch (Exception e)
                            {
                                Log.Warning(e, "Exception thrown while decoding command failure");
                            }
                        }
                        else
                        {
                            // If response code is not "Command Failure", 
                            _rcpPayloadBuffer.Insert(0, (byte)ErrorFlag.NoError);
                            _rcpPayloadBuffer.Insert(0, _rcpCode);
                            _receivedCommandAnswerBuffer.Add(_rcpPayloadBuffer);
                        }

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
