namespace Kliskatek.Driver.Rain.REDRCP
{
    public class NotificationEventArgs : EventArgs
    {
        public SupportedNotifications NotificationType { get; set; }
        public object NotificationParameters { get; set; }
    }

    public class ReadTypeCUiiNotificationParameters
    {
        public string Pc = string.Empty;
        public string Epc = string.Empty;
    }

    public class ReadTypeCUiiTidNotificationParameters
    {
        public string Pc = string.Empty;
        public string Epc = string.Empty;
        public string Tid = string.Empty;
        public bool ReadComplete = false;
    }

    public class ReadTypeCUiiRssiNotificationParameters
    {
        public string Pc = string.Empty;
        public string Epc = string.Empty;
        public byte RssiI;
        public byte RssiQ;
        public byte GainI;
        public byte GainQ;
    }

    public class StartAutoReadRssiNotificationParameters
    {
        public bool ReadComplete = false;
    }

    public class ReadTypeCUiiEx2NotificationParameters
    {
        public ParamAutoRead2ExMode Mode;
        public byte TagRssi;
        public byte AntennaPort;
        public string Pc = string.Empty;
        public string Epc = string.Empty;
    }

    public class StartAutoRead2ExNotificationParameters
    {
        public bool ReadComplete = false;
    }

    public class GetDtcResultNotificationParameters : DtcResultResponseParameters
    {
        public byte CurrentChannel;
        public byte LeakageCancellationOperationTime;
    }


}
