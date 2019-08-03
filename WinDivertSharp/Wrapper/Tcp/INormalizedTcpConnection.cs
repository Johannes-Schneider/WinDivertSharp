namespace WinDivertSharp.Wrapper.Tcp
{
    public interface INormalizedTcpConnection : INormalizedConnection
    {
        ushort ClientPort { get; }

        ushort ServerPort { get; }

        int ClientProcessId { get; }
    }
}