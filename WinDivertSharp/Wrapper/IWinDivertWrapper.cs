namespace WinDivertSharp.Wrapper
{
    public interface IWinDivertWrapper<out PacketType> : IWinDivertWrapperBase where PacketType : IWinDivertPacketWrapper
    {
        PacketType Receive();
    }
}