namespace Kliskatek.REDRCP
{
    public enum MessageType
    {
        Command = 0x00,
        Response = 0x01,
        Notification = 0x02,
        Reserved = 0x03
    }

    public enum MessageCode
    {
        SetReaderPowerControl = 0x01,
        GetReaderInformation = 0x03,
        GetRegion = 0x06,
        SetRegion = 0x07,
        SetSystemReset = 0x08,
        GetSelectionEnables = 0x8E,
        SetSelectionEnables = 0x8F,
        GetSelectionFilter = 0xAE,
        SetSelectionFilter = 0xAF,
        GetTypeQueryRelatedParameters = 0x0D,
        SetTypeQueryRelatedParameters = 0x0E,
        GetCurrentRfChannel = 0x11,
        SetCurrentRfChannel = 0x12,
        GetFhLbtParameters = 0x13,
        SetFhLbtParameters = 0x14,
        GetTxPower = 0x15,
        SetTxPower = 0x16,
        RfCwSignalControl = 0x17,
        ReadTpeCuiii = 0x22,
        ReadTpeCuiiiRssi = 0x23,
        ReadTpeCuiiiTid = 0x25,
        ReadTpeCTagData = 0x29,
        ReadTpeCTagLongData = 0x2A,
        RcpCmdGetTxPwrRaw = 0x2C,
        RcpCmdSetTxPwrRaw = 0x2D,
        GetSession = 0x2E,
        SetSession = 0x2F,
        GetFrequencyHoppingTable = 0x30,
        SetFrequencyHoppingTable = 0x31,
        GetModulation = 0x32,
        SetModulation = 0x33,
        GetAntiCollisionMode = 0x34,
        SetAntiCollisionMode = 0x35,
        StartAutoRead2 = 0x36,
        StopAutoRead2 = 0x37,
        StartAutoReadRssi = 0x38,
        StopAutoReadRssi = 0x39,
        WriteTypeCTagData = 0x46,
        BlockWriteTypeCTagData = 0x47,
        BlockEraseTypeCTagData = 0x48,
        BlockPermalockTypeCTag = 0x83,
        KillRecomTypeCTag = 0x65,
        LockTypeCTag = 0x82,
        AntennaCheck = 0xAC,
        GetTemperature = 0xB7,
        GetRssi = 0xC5,
        ScanRssi = 0xC6,
        GetDtcResult = 0xCA,
        UpdateRegistry = 0xD2,
        GetRegistryItem = 0xD4,
        CommandFailure = 0xFF,
        SetOptimumFrequencyHoppingTable = 0xE4,
        GetFrequencyHoppingMode = 0xE5,
        SetFrequencyHoppingMode = 0xE6,
        GetTxLeakageRssiLevelSmartHoppingMode = 0xE7,
        SetTxLeakageRsiiLevelSmartHoppingMode = 0xE8,
        SmartReadFastLeakageCal = 0xEC,
        RequestFastLeakageCal = 0xED,
        Error = 0xFF
    }

    public enum ErrorCode
    {
        OtherError = 0x00,
        NotSupported = 0x01,
        InsufficientPrivileges = 0x02,
        MemoryOverrun = 0x03,
        MemoryLocked = 0x04,
        CryptoSuiteError = 0x05,
        CommandNotEncapsulated = 0x06,
        ResponseBufferOverflow = 0x07,
        SecurityTimeout = 0x08,
        InsufficientPower = 0x0B,
        NonSpecificError = 0x0F,
        // Vendor specific error
        SensorSchedulingConfiguration = 0x11,
        TagBusy = 0x12,
        MeasurementTypeNotSupported = 0x13,
        // Protocol error
        NoTagDetected = 0x80,
        HandleAcquisitionFailure = 0x81,
        AccessPasswordFailure = 0x82,
        KillPasswordFailure = 0x83,
        // Modem error
        CrcError = 0x90,
        RxTimeout = 0x91,
        // Registry
        RegistryUpdateFailure = 0xA0,
        RegistryEraseFailure = 0xA1,
        RegistryWriteFailure = 0xA2,
        RegistryNotExist = 0xA3,
        // Peripheral
        UartFailure = 0xB0,
        SpiFailure = 0xB1,
        I2CFailure = 0xB2,
        GpioFailure = 0xB3,
        // Custom error
        NotSupportedCommand = 0xE0,
        UndefinedCommand = 0xE1,
        InvalidParameter = 0xE2,
        TooHighParameter = 0xE3,
        TooLowParameter = 0xE4,
        FailureAutomaticReadOperation = 0xE5,
        NotAutomaticReadMode = 0xE6,
        FailureToGetLastResponse = 0xE7,
        FailureToControlTest = 0xE8,
        FailureToResetReader = 0xE9,
        RfidBlockControlFailure = 0xEA,
        AutomaticReadInOperation = 0xEB,
        UndefinedOtherError = 0xF0,
        FailureToVerifyWriteOperation = 0xF1,
        AbnormalAntenna = 0xFC,
        NoneError = 0xFF
    }

    public enum Region
    {
        Korea = 0x11,
        UsWide = 0x21,
        UsNarrow = 0x22,
        Europe = 0x31,
        Japan = 0x41,
        China = 0x52,
        Brazil = 0x61
    }

    public enum ReaderInfoType
    {
        Model = 0x00,
        FwVersion = 0x01,
        Manufacturer = 0x02,
        Detail = 0xB0
    }

    public enum MessageConstants
    {
        Preamble = 0xBB,
        EndMark = 0x7E
    }

    public enum ParamModulation
    {
        Fm0 = 0,
        Miller2 = 1,
        Miller4 = 2,
        Miller8 = 3
    }

    public enum ParamDr
    {
        Dr8 = 0,
        Dr64Div3 = 1
    }

    public enum ParamSel
    {
        All0 = 0,
        All1 = 1,
        Nsl = 2,
        Sl = 3
    }

    public enum ParamSession
    {
        S0 = 0,
        S1 = 1,
        S2 = 2,
        S3 = 3
    }

    public enum ParamTarget
    {
        A = 0,
        B = 1
    }

    public enum ParamSelectTarget
    {
        S0 = 0,
        S1 = 1,
        S2 = 2,
        S3 = 3,
        Sl = 4
    }

    public enum ParamSelectAction
    {
        MatchANoMatchB = 0,
        MatchANoMatchNoChange = 1,
        MatchNoChangeNoMatchB = 2,
        MatchToggleNoMatchNoChange = 3,
        MatchBNoMatchA = 4,
        MatchBNoMatchNoChange = 5,
        MatchNoChangeNoMatchA = 6,
        MatchNoChangeNoMatchToggle = 7
    }

    public enum AntiCollisionMode
    {
        MultiTag = 3,
        SingleTag = 32,
        UniqueRecognition = 16,
        Manual = 0
    }

    public enum Registry
    {
        Version = 0,
        Fw = 1,
        Band = 2,
        AntiCollision = 3,
        Modulation = 4,
        Query = 5,
        PartNumber = 6,
        Dev = 7,
        FwVersion = 8,
        LeakMode = 9,
        Session = 10,
        Serial = 11,
        Beep = 12,
        GpAdc = 13,
        Q = 14,
        Antenna = 15,
        FhMode = 16,
        ModulationRaw = 17,
        SupportRegion = 18,
        Gain = 19,
        Report = 20,
        TxKrHighGain = 21,
        TxKrLowGain = 22,
        FhLbtKr = 23,
        FhTableKr = 24,
        PowerTableKrHigh = 25,
        PowerTableKrLow = 26,
        FbParameterKrHigh = 27,
        FbParameterKrLow = 28,
        RssiOffsetKr = 29,
        ChOffsetKr = 29,
        TxUsHigh = 31,
        TxUsLow = 32,
        FhLbtUs = 33,
        GhTableUs = 34,
        PowerTableUsHigh = 35,
        PowerTableUsLow = 36,
        FbParameterUsHigh = 37,
        FbParameterUsLow = 38,
        RssiOffsetUs = 39,
        ChOffsetUs = 40,
        TxJpHigh = 41,
        TxJpLow = 42,
        FhLbtJp = 43,
        FhTableJp = 44,
        PowerTableJpHigh = 45,
        PowerTableJpLow = 46,
        FbParameterJpHigh = 47,
        FbParameterJpLow = 48,
        RssiOffsetJp = 49,
        ChOffsetJp = 50,
        TxEuHigh = 51,
        TxEuLow = 52,
        FhLbtEu = 53,
        FhTableEu = 54,
        PowerTableEuHigh = 55,
        PowerTableEuLow = 56,
        FbParameterEuHigh = 57,
        FbParameterEuLow = 58,
        RssiOffsetEu = 59,
        ChOffsetEu = 60,
        FhTableUs2 = 61,
        FhTableCh = 62,
        TxBrHigh = 63,
        TxBrLow = 64,
        FhLbtBr = 65,
        FhTableBr = 66,
        RssiOffsetBr = 67,
        ChOffsetBr = 68,
        TxChHigh = 69,
        TxChLow = 70,
        AntSequence = 71,
        FrequencyTable = 72
    }

    public enum TagType
    {
        TypeB = 0x01,
        TypeC = 0x02
    }

    public enum ParamMemory
    {
        Reserved = 0,
        Epc = 1,
        Tid = 2,
        User = 3
    }

    public enum ParamFhLbtMode
    {
        FhOnly = 0x0100,
        FhWithLbt = 0x0201,
        LbtOnly = 0x0001,
        LbtWithFh = 0x0102
    }

    public enum AutoMode
    {
        Idle,
        Epc,
        EpcRssi,
        EpcTid
    }

    public enum RcpState
    {
        Preamble = 0,
        MessageType = 1,
        Code = 2,
        PayloadLengthH = 3,
        PayloadLengthL = 4,
        Payload = 5,
        EndMark = 6,
        Crc16H = 7,
        Crc16L = 8
    }
}
