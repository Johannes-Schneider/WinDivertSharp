using System;
using WinDivertSharp.Wrapper.Tcp;

namespace WinDivertSharp.Wrapper
{
    public class WinDivertTcp4Wrapper : IWinDivertWrapper<InterceptedTcp4Packet>
    {
        public string Filter { get; private set; }

        public short Priority { get; private set; }

        public bool IsOpen { get; private set; } = false;

        internal IntPtr Handle { get; private set; } = IntPtr.Zero;

        public WinDivertTcp4Wrapper(string filter, short priority = 0)
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

            Handle = WinDivert.WinDivertOpen(Filter, WinDivertLayer.Network, Priority, WinDivertOpenFlags.None);
            if (Handle.ToInt64() < 1)
            {
                return false;
            }

            WinDivert.WinDivertSetParam(Handle, WinDivertParam.QueueLen, 16384);
            WinDivert.WinDivertSetParam(Handle, WinDivertParam.QueueTime, 8000);
            WinDivert.WinDivertSetParam(Handle, WinDivertParam.QueueSize, 33554432);
            IsOpen = true;
            return true;
        }

        public InterceptedTcp4Packet Receive()
        {
            if (!IsOpen)
            {
                throw new InvalidOperationException($"{nameof(IsOpen)} = false");
            }

            return InterceptedTcp4Packet.Receive(this);
        }

        public bool Send(InterceptedTcp4Packet packet)
        {
            if (packet == null)
            {
                throw new ArgumentNullException(nameof(packet));
            }

            if (!IsOpen)
            {
                return false;
            }

            return packet.Send(Handle);
        }

        public bool Close()
        {
            if (!IsOpen)
            {
                return true;
            }

            var result = WinDivert.WinDivertClose(Handle);
            Handle = IntPtr.Zero;
            IsOpen = false;
            return result;
        }
    }
}