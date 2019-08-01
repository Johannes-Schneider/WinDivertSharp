namespace WinDivertSharp.Wrapper.Tcp
{
    public interface IWinDivertTcpPacketWrapper : IWinDivertPacketWrapper
    {
        ushort SourcePort { get; set; }

        ushort DestinationPort { get; set; }
    }
}