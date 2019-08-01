namespace WinDivertSharp.Wrapper
{
    public interface IWinDivertWrapperBase
    {
        string Filter { get; }

        short Priority { get; }

        bool IsOpen { get; }

        bool Open();

        bool Close();
    }
}