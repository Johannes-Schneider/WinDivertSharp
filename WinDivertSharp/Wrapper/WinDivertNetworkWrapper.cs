using System;
using WinDivertSharp.Wrapper.Tcp;

namespace WinDivertSharp.Wrapper
{
    public class WinDivertNetworkWrapper
    {
        public string Filter { get; private set; }

        public short Priority { get; private set; }

        public bool IsOpen { get; private set; } = false;

        private IntPtr _handle = IntPtr.Zero;

        public WinDivertNetworkWrapper(string filter, short priority = 0)
        {
            Filter = filter;
            Priority = priority;
        }

        public bool Open()
        {
            if (IsOpen)
            {
                return true;
            }

            _handle = WinDivert.WinDivertOpen(Filter, WinDivertLayer.Network, Priority, WinDivertOpenFlags.None);
            if (_handle.ToInt64() < 1)
            {
                return false;
            }

            WinDivert.WinDivertSetParam(_handle, WinDivertParam.QueueLen, 16384);
            WinDivert.WinDivertSetParam(_handle, WinDivertParam.QueueTime, 8000);
            WinDivert.WinDivertSetParam(_handle, WinDivertParam.QueueSize, 33554432);
            IsOpen = true;
            return true;
        }

        public InterceptedTcpPacket ReceiveTcpPacket()
        {
            if (!IsOpen)
            {
                throw new InvalidOperationException($"{nameof(IsOpen)} = false");
            }

            return InterceptedTcpPacket.Receive(_handle);
        }

        public bool Close()
        {
            if (!IsOpen)
            {
                return true;
            }

            var result = WinDivert.WinDivertClose(_handle);
            _handle = IntPtr.Zero;
            IsOpen = false;
            return result;
        }
    }
}