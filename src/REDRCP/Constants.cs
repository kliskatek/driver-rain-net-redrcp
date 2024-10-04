namespace Kliskatek.Driver.Rain.REDRCP
{
    internal static class Constants
    {
        public const byte Preamble = 0xBB;
        public const byte EndMark = 0x7E;
        public const byte Success = 0x00;
        public const int RcpCommandMaxResponseTimeMs = 5000;
        public const int DetailReaderInfoRegionOffset = 1;
        public const int DetailReaderInfoMinTxPowerOffset = 16;
        public const int DetailReaderInfoMaxTxPowerOffset = 18;
        public const int ResponseArgOffset = 0;
        public const int ReaderInformationDetailBinaryLength = 26;
        #region ReaderInfoDetails

        public const int RidRegionOffset = 1;
        public const int RidChannelOffset = 2;
        public const int RidMergeTimeOffset = 3;
        public const int RidIdleTimeOffset = 5;
        public const int RidCwSenseTimeOffset = 7;
        public const int RidLbtRfLevelOffset = 9;
        public const int RidCurrentTxPowerOffset = 14;
        public const int RidMinTxPowerOffset = 16;
        public const int RidMaxTxPowerOffset = 18;
        public const int RidBlfOffset = 20;
        public const int RidModulationOffset = 22;
        public const int RidDrOffset = 23;

        #endregion
        #region FH and LBT parameters

        public const int FlpDtOffset = 0;
        public const int FlpItOffset = 2;
        public const int FlpCstOffset = 4;
        public const int FlpRflOffset = 6;
        public const int FlpFhOffset = 8;
        public const int FlpLbtOffset = 9;
        public const int FlpCwOffset = 10;

        #endregion

    }
}
