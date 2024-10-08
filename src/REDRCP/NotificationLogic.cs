using System.Buffers.Binary;

namespace Kliskatek.Driver.Rain.REDRCP
{
    public partial class REDRCP
    {
        /// <summary>
        /// Can generate A, B or C
        /// </summary>
        public event EventHandler<NotificationEventArgs> OnNotificationReceived;

        private void OnNewTransportNotificationReceived()
        {
            if (!Enum.IsDefined(typeof(MessageCode), (int)_rcpCode))
                return;
            switch ((MessageCode)_rcpCode)
            {
                case MessageCode.ReadTypeCUii:
                    HandleReadTypeCUiiNotification();
                    break;
                case MessageCode.ReadTypeCUiiTid:
                    HandleReadTypeCUiiTidNotification();
                    break;
                case MessageCode.ReadTypeCUiiRssi:
                    HandleReadTypeCUiiRssiNotification();
                    break;
                case MessageCode.StartAutoReadRssi:
                    HandleStartAutoReadRssiNotification();
                    break;
                case MessageCode.ReadTypeCUiiEx2:
                    HandleReadTypeCUiiEx2Notification();
                    break;
                case MessageCode.StartAutoRead2Ex:
                    HandleStartAutoRead2Ex();
                    break;
                case MessageCode.GetDtcResult:
                    HandleGetDtcResultNotification();
                    break;
                default:
                    break;
            }
        }

        private void HandleReadTypeCUiiNotification()
        {
            if (_rcpPayloadBuffer.Count < 2)
                return;
            ReadTypeCUiiNotificationParameters parameters = new ReadTypeCUiiNotificationParameters();
            var payloadByteArray = _rcpPayloadBuffer.ToArray();
            parameters.Pc = BitConverter.ToString(payloadByteArray.GetArraySlice(0, sizeof(UInt16))).RemoveHyphen();
            if (_rcpPayloadBuffer.Count > 2)
                parameters.Epc = BitConverter.ToString(payloadByteArray.GetArraySlice(2)).RemoveHyphen();
            OnNotificationReceived?.Invoke(this,
                new NotificationEventArgs
                {
                    NotificationType = SupportedNotifications.ReadTypeCUii,
                    NotificationParameters = (object)parameters
                });
        }

        private void HandleReadTypeCUiiTidNotification()
        {
            var parameters = new ReadTypeCUiiTidNotificationParameters();
            switch (_rcpPayloadBuffer.Count)
            {
                case 1:
                    parameters.ReadComplete = true;
                    break;
                default:
                    var payload = _rcpPayloadBuffer.ToArray();
                    // Check if payload can store PC
                    if (payload.Length < 2)
                        return;
                    ushort pc = BinaryPrimitives.ReadUInt16BigEndian(GetArraySlice(payload, 0, sizeof(UInt16)));
                    var epcByteLength = GetEpcByteLengthFromPc(pc);
                    // Check if payload can store PC + EPC
                    if (payload.Length < 2 + epcByteLength)
                        return;
                    parameters.Pc = BitConverter.ToString(GetArraySlice(payload, 0, sizeof(UInt16))).RemoveHyphen();
                    parameters.Epc = BitConverter.ToString(GetArraySlice(payload, 2, epcByteLength)).RemoveHyphen();
                    if (payload.Length > 2 + epcByteLength)
                        parameters.Tid = BitConverter.ToString(GetArraySlice(payload, 2 + epcByteLength))
                            .RemoveHyphen();
                    break;
            }
            OnNotificationReceived?.Invoke(this, 
                new NotificationEventArgs
                {
                    NotificationType = SupportedNotifications.ReadTypeCUiiTid,
                    NotificationParameters = (object)parameters
            });
        }

        private void HandleReadTypeCUiiRssiNotification()
        {
            var payload = _rcpPayloadBuffer.ToArray();
            // Check if payload can store PC
            if (payload.Length < 2) 
                return;
            var pc = BinaryPrimitives.ReadUInt16BigEndian(GetArraySlice(payload, 0, sizeof(UInt16)));
            var epcByteLength = GetEpcByteLengthFromPc(pc);
            // Check if payload can store PC + EPC + RSSI + GAIN 
            if (payload.Length != 2 + epcByteLength + 4) 
                return;
            var parameters = new ReadTypeCUiiRssiNotificationParameters();
            parameters.Pc = BitConverter.ToString(GetArraySlice(payload, 0, sizeof(UInt16))).RemoveHyphen();
            parameters.Epc = BitConverter.ToString(GetArraySlice(payload, 2, epcByteLength)).RemoveHyphen();
            // Pointer to RSSI - GAIN parameters in payload
            var pointer = 2 + epcByteLength;
            parameters.RssiI = payload[pointer++];
            parameters.RssiQ = payload[pointer++];
            parameters.GainI = payload[pointer++];
            parameters.GainQ = payload[pointer++];
            OnNotificationReceived?.Invoke(this,
                new NotificationEventArgs
                {
                    NotificationType = SupportedNotifications.ReadTypeCUiiRssi,
                    NotificationParameters = (object)parameters
                });
        }

        private void HandleStartAutoReadRssiNotification()
        {
            if (_rcpPayloadBuffer.Count != 1)
                return;
            if (_rcpPayloadBuffer[0] != 0x1F)
                return;
            var parameters = new StartAutoReadRssiNotificationParameters();
            parameters.ReadComplete = true;
            OnNotificationReceived?.Invoke(this,
                new NotificationEventArgs
                {
                    NotificationType = SupportedNotifications.StartAutoReadRssi,
                    NotificationParameters = (object)parameters
                });
        }

        private void HandleReadTypeCUiiEx2Notification()
        {
            var payload = _rcpPayloadBuffer.ToArray();
            // Check if payload can store Mode + Tag RSSI + Antenna Port + PC
            if (payload.Length < 5)
                return;
            ushort pc = BinaryPrimitives.ReadUInt16BigEndian(GetArraySlice(payload, 3, sizeof(UInt16)));
            var epcByteLength = GetEpcByteLengthFromPc(pc);
            // Check if payload can store Mode + Tag RSSI + Antenna Port + PC + EPC
            if (payload.Length < 5 + epcByteLength)
                return;
            var parameters = new ReadTypeCUiiEx2NotificationParameters();
            parameters.Pc = BitConverter.ToString(GetArraySlice(payload, 3, sizeof(UInt16))).RemoveHyphen();
            parameters.Epc = BitConverter.ToString(GetArraySlice(payload, 5, epcByteLength)).RemoveHyphen();
            parameters.Mode = (ParamAutoRead2ExMode)payload[0];
            parameters.TagRssi = payload[1];
            parameters.AntennaPort = payload[2];
            OnNotificationReceived?.Invoke(this,
                new NotificationEventArgs
                {
                    NotificationType = SupportedNotifications.ReadTypeCUiiEx2,
                    NotificationParameters = (object)parameters
                });
        }
        
        private void HandleStartAutoRead2Ex()
        {
            if (_rcpPayloadBuffer.Count != 1)
                return;
            if (_rcpPayloadBuffer[0] != 0x1F)
                return;
            var parameters = new StartAutoRead2ExNotificationParameters
            {
                ReadComplete = true
            };
            OnNotificationReceived?.Invoke(this,
                new NotificationEventArgs
                {
                    NotificationType = SupportedNotifications.StartAutoRead2Ex,
                    NotificationParameters = (object)parameters
                });
        }

        private void HandleGetDtcResultNotification()
        {
            if (_rcpPayloadBuffer.Count != 7)
                return;
            var payload = _rcpPayloadBuffer.ToArray();
            int arrayPointer = 0;
            var parameters = new GetDtcResultNotificationParameters();
            parameters.InductorNumber = payload[arrayPointer++];
            parameters.DigitalTunableCapacitor1 = payload[arrayPointer++];
            parameters.DigitalTunableCapacitor2 = payload[arrayPointer++];
            parameters.LeakageRssi = payload[arrayPointer++];
            parameters.LeakageCancellationAlgorithmStateNumber = payload[arrayPointer++];
            parameters.CurrentChannel = payload[arrayPointer++];
            parameters.LeakageCancellationOperationTime = payload[arrayPointer++];
            OnNotificationReceived?.Invoke(this,
                new NotificationEventArgs
                {
                    NotificationType = SupportedNotifications.GetDtcResult,
                    NotificationParameters = (object)parameters
                });

        }

        public Type SupportedNotificationToDataClassType(SupportedNotifications notification)
        {
            switch (notification)
            {
                case SupportedNotifications.ReadTypeCUii:
                    return typeof(ReadTypeCUiiNotificationParameters);
                case SupportedNotifications.ReadTypeCUiiTid:
                    return typeof(ReadTypeCUiiTidNotificationParameters);
                case SupportedNotifications.ReadTypeCUiiRssi:
                    return typeof(ReadTypeCUiiRssiNotificationParameters);
                case SupportedNotifications.StartAutoReadRssi:
                    return typeof(StartAutoReadRssiNotificationParameters);
                case SupportedNotifications.ReadTypeCUiiEx2:
                    return typeof(ReadTypeCUiiEx2NotificationParameters);
                case SupportedNotifications.StartAutoRead2Ex:
                    return typeof(ReadTypeCUiiEx2NotificationParameters);
                case SupportedNotifications.GetDtcResult:
                    return typeof(GetDtcResultNotificationParameters);
                default:
                    throw new ArgumentException($"Notification {notification} not supported or implemented");
            }
        }
    }
}
