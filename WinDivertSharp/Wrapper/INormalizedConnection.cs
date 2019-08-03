using System.Net;

namespace WinDivertSharp.Wrapper
{
    public interface INormalizedConnection
    {
        IPHelper.AddressVersion IPAddressVersion { get; }

        IPAddress ClientIPAddress { get; }

        IPAddress ServerIPAddress { get; }
    }
}