using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kliskatek.REDRCP
{
    public partial class REDRCP
    {
        private int _rcpDecodeState;

        private byte _messageType;
        private byte _code;
        private byte _payloadLengthH, _payloadLengthL;
        private int _payloadLength, _payloadCounter;
        private byte _crc16H, _crc16L;
        private List<byte> _byteBuffer, _payloadBuffer;

        private void UpdateRcpState(RcpState newState)
        {
            UpdateRcpState((int)newState);
        }

        private void UpdateRcpState(int newStateValue)
        {
            Interlocked.Exchange(ref _rcpDecodeState, newStateValue);
        }

        private void ResetRcpDecodeFsm()
        {
            UpdateRcpState(RcpState.Preamble);
        }

        private bool TryDecodeRxByte(byte newByte)
        {
            switch ((RcpState)_rcpDecodeState)
            {
                case RcpState.Preamble:
                    if (newByte == Constants.Preamble)
                        UpdateRcpState(RcpState.MessageType);
                    break;
                case RcpState.MessageType:
                    _messageType = newByte;
                    _byteBuffer = new List<byte>();
                    _payloadBuffer = new List<byte>();

                default:
                    UpdateRcpState(RcpState.Preamble);
                    break;
            }

            return false;
        }
    }
}
