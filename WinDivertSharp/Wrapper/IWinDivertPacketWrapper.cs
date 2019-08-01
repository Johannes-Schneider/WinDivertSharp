using System;
using System.Net;

namespace WinDivertSharp.Wrapper
{
    public interface IWinDivertPacketWrapper : IDisposable
    {
        IPHelper.AddressVersion IPAddressVersion { get; }

        IPAddress SourceAddress { get; set; }

        IPAddress DestinationAddress { get; set; }

        WinDivertDirection Direction { get; set; }

        bool IsModified { get; }

        bool IsAboutToBeDropped { get; }

        void Drop();

        bool Send();
    }
}