namespace Kliskatek.Driver.Rain.REDRCP
{
    public class ReaderInformationDetails
    {
        public Region Region;
        public int Channel;
        public int MergeTime;
        public int IdleTime;
        public int CwSenseTime;
        public double LbtRfLevel;
        public double CurrentTxPower;
        public double MinTxPower;
        public double MaxTxPower;
        public int Blf;
        public ParamModulation Modulation;
        public ParamDr Dr;
    }

    public class TypeCaiQueryParameters
    {
        public ParamDr Dr;
        public ParamModulation Modulation;
        public bool TRext;
        public ParamSel Sel;
        public ParamSession Session;
        public ParamTarget Target;
        public uint Q;
        public ParamToggle Toggle;
    }

    public class RfChannel
    {
        public byte ChannelNumber;
        public byte ChannelNumberOffset;
    }
}
