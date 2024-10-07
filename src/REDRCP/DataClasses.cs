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

    public class FhLbtParameters
    {
        public ushort DwellTime;
        public ushort IdleTime;
        public ushort CarrierSenseTime;
        public double TargetRfPowerLevel;
        public bool Fh;
        public bool Lbt;
        public bool Cw;
    }

    public class TxPowerLevels
    {
        public double CurrentTxPower;
        public double MinTxPower;
        public double MaxTxPower;
    }

    public class TypeCUii
    {
        public byte[] Pc;
        public byte[] Epc;
    }

    public class ModulationMode
    {
        public ushort BackscatterLinkFrequency;
        public ParamModulation RxMod;
        public ParamDr Dr;
    }

    public class AntiCollisionModeParameters
    {
        public AntiCollisionMode Mode;
        public byte QStart;
        public byte QMax;
        public byte QMin;
    }
}
