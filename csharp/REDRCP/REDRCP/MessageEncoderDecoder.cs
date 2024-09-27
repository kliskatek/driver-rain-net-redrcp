namespace Kliskatek.REDRCP
{
    public partial class REDRCP
    {
        private int _rcpDecodeState;

        private byte _rcpMessageType;
        private byte _rcpCode;
        private byte _rcpPayloadLengthH, _rcpPayloadLengthL;
        private int _rcpPayloadLength, _rcpPayloadByteCounter;
        private byte _rcpCrc16H, _rcpCrc16L;
        private List<byte> _rcpByteBuffer, _rcpPayloadBuffer;

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
                    _rcpMessageType = newByte;
                    _rcpByteBuffer = new List<byte>
                    {
                        newByte
                    };
                    UpdateRcpState(RcpState.Code);
                    break;
                case RcpState.Code:
                    _rcpCode = newByte;
                    _rcpByteBuffer.Add(newByte);
                    UpdateRcpState(RcpState.PayloadLengthH);
                    break;
                case RcpState.PayloadLengthH:
                    _rcpPayloadLengthH = newByte;
                    _rcpByteBuffer.Add(newByte);
                    UpdateRcpState(RcpState.PayloadLengthL);
                    break;
                case RcpState.PayloadLengthL:
                    _rcpPayloadLengthL = newByte;
                    _rcpByteBuffer.Add(newByte);
                    _rcpPayloadLength = (((int)_rcpPayloadLengthH) << 8) + (int)_rcpPayloadLengthL;
                    _rcpPayloadByteCounter = 0;
                    _rcpPayloadBuffer = new List<byte>();
                    UpdateRcpState(_rcpPayloadLength > 0 ? RcpState.Payload : RcpState.EndMark);
                    break;
                case RcpState.Payload:
                    _rcpByteBuffer.Add(newByte);
                    _rcpPayloadBuffer.Add(newByte);
                    if (++_rcpPayloadByteCounter == _rcpPayloadLength)
                        UpdateRcpState(RcpState.EndMark);
                    break;
                case RcpState.EndMark:
                    _rcpByteBuffer.Add(newByte);
                    UpdateRcpState(newByte == Constants.EndMark ? RcpState.Crc16H : RcpState.Preamble);
                    break;
                case RcpState.Crc16H:
                    _rcpCrc16H = newByte;
                    UpdateRcpState(RcpState.Crc16L);
                    break;
                case RcpState.Crc16L:
                    _rcpCrc16L = newByte;
                    int crc = (((int)_rcpCrc16H) << 8) + (int)_rcpCrc16L;
                    UpdateRcpState(RcpState.Preamble);
                    return (crc == CalculateCrc16(_rcpByteBuffer));
                default:
                    UpdateRcpState(RcpState.Preamble);
                    break;
            }
            return false;
        }
        private ushort CalculateCrc16(List<byte> byteBuffer)
        {
            ushort crc = 0xFFFF;
            foreach (var newByte in byteBuffer)
            {
                crc ^= (ushort)((ushort)newByte << 8);
                for (int j = 0; j < 8; j++)
                {
                    if (crc >= 0x8000)
                    {
                        crc = (ushort)((ushort)(crc << 1) ^ 0x1021);
                    }
                    else
                    {
                        crc = (ushort)(crc << 1);
                    }
                }
            }
            return (ushort)crc;
        }

        private List<byte> AssembleRcpCommand(MessageCode code, List<byte>? payload = null)
        {
            if (payload is null)
                payload = new List<byte>();
            // Byte Buffer
            var rcpCommand = new List<byte>
            {
                // Assemble command (omit Preamble byte before CRC-16 calculation)
                (byte)MessageType.Command,
                (byte)code
            };
            var payloadLength = payload.Count;
            rcpCommand.Add((byte)(payloadLength >> 8));
            rcpCommand.Add((byte)(payloadLength & 0x0FF));
            foreach (var payloadByte in payload)
                rcpCommand.Add(payloadByte);
            rcpCommand.Add(Constants.EndMark);
            // Calculate CRC-16
            var crc16 = (int)CalculateCrc16(rcpCommand);
            rcpCommand.Add((byte)(crc16 >> 8));
            rcpCommand.Add((byte)(crc16 & 0x0FF));
            // Prepend preamble to complete RCP command
            rcpCommand.Insert(0, Constants.Preamble);
            return rcpCommand;
        }
    }
}
