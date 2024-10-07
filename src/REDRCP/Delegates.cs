namespace Kliskatek.Driver.Rain.REDRCP
{
    public delegate void AutoRead2NotificationCallback(string pc, string epc);

    public delegate void ReadTypeCUiiNotificationCallback(string pc, string epc);

    public delegate void ReadTypeCUiiTidNotificationCallback(string pc, string epc, string tid);

    public delegate void AutoReadRssiNotificationCallback(string pc, string epc, byte rssiI, byte rsiiQ, byte gainI,
        byte gainQ);

    public delegate void AutoRead2ExNotificationCallback(ParamAutoRead2ExMode mode, bool tagRssi, byte antPort,
        string pc, string epc, byte rssiI, byte rssiQ, byte gainI, byte gainQ);
}
