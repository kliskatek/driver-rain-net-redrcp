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

    public class FrequencyInformation
    {
        public ushort Spacing;
        public UInt32 StartFreq;
        public byte Channel;
        public ParamRfPreset RfPreset;
    }

    public class EnableStatus
    {
        public bool Sel1;
        public bool Sel2;
        public bool Sel3;
        public bool Sel4;
        public bool Sel5;
        public bool Sel6;
        public bool Sel7;
        public bool Sel8;
    }

    public class AntennaCheckError
    {
        public bool IsFailure;
        public byte ErrorCode;
        public byte SubErrorCode;
    }

    public class Selection
    {
        public byte Index;
        public ParamTarget Target;
        public ParamSelectAction Action;
        public ParamMemoryBank MemoryBank;
        public ushort Pointer;
        public byte Length;
        public List<byte> Mask = [];
    }

    public class ScanRssiParameters
    {
        public byte StartChannelNumber;
        public byte StopChannelNumber;
        public byte BestChannelNumber;
        public List<byte> RssiLevels = [];
    }

    public class DtcResultResponseParameters
    {
        public byte InductorNumber;
        public byte DigitalTunableCapacitor1;
        public byte DigitalTunableCapacitor2;
        public byte LeakageRssi;
        public byte LeakageCancellationAlgorithmStateNumber;
    }

    public class DtcResultNotificationParameters : DtcResultResponseParameters
    {
        public byte CurrentChannel;
        public byte LeakageCancellationOperationTime;
    }

    public class RegistryItem
    {
        public RegistryItemStatus Active;
        public List<byte> Data = [];
    }
}
