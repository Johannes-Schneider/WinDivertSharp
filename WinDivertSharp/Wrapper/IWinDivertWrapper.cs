namespace WinDivertSharp.Wrapper
{
    public interface IWinDivertWrapper<PacketType> : IWinDivertWrapperBase where PacketType : IWinDivertPacketWrapper
    {
        PacketType Receive();

        bool Send(PacketType packet);
    }
}