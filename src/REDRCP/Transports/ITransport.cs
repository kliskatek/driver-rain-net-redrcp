namespace Kliskatek.Driver.Rain.REDRCP.Transports
{
    public interface ITransport
    {
        bool Connect(string connectionString, RxByteCallback rxByteCallback);

        bool Disconnect();

        bool IsConnected { get; }

        void TxByteList(List<byte> txByteList);
    }
}
