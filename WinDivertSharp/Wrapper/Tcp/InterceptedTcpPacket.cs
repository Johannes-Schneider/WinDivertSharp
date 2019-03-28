using System;
using WinDivertSharp.Extensions;

namespace WinDivertSharp.Wrapper.Tcp
{
    /// <summary>
    /// Represents an intercepted ipv4/tcp packet.
    /// </summary>
    public unsafe class InterceptedTcpPacket
    {
        private WinDivertAddress _winDivertAddress;

        /// <summary>
        /// The intercepted <seealso cref="WinDivertAddress"/>.
        /// </summary>
        public WinDivertAddress WinDivertAddress
        {
            get => _winDivertAddress;
            private set => _winDivertAddress = value;
        }

        /// <summary>
        /// The intercepted <seealso cref="WinDivertBuffer"/>.
        /// </summary>
        public WinDivertBuffer WinDivertBuffer { get; private set; }

        /// <summary>
        /// The intercepted <seealso cref="WinDivertParseResult"/>.
        /// </summary>
        public WinDivertParseResult WinDivertParseResult { get; private set; }

        /// <summary>
        /// The intercepted <seealso cref="IPv4Header"/>.
        /// </summary>
        public IPv4Header IPHeader => *WinDivertParseResult.IPv4Header;

        /// <summary>
        /// The intercepted <seealso cref="TcpHeader"/>.
        /// </summary>
        public TcpHeader TcpHeader => *WinDivertParseResult.TcpHeader;

        /// <summary>
        /// The intercepted <seealso cref="Direction"/>.
        /// </summary>
        public WinDivertDirection Direction
        {
            get => WinDivertAddress.Direction;
            set => _winDivertAddress.Direction = value;
        }

        /// <summary>
        /// The intercepted length of the packet.
        /// </summary>
        public uint PacketLength { get; set; }

        /// <summary>
        /// Indicates, whether the packet has already been modified.
        /// </summary>
        public bool HasBeenModified { get; private set; } = false;

        /// <summary>
        /// Indicates, whether the packet should be dropped.
        /// </summary>
        public bool ShouldBeDropped { get; private set; } = false;

        /// <summary>
        /// Receives the next packet in the queue of the given <paramref name="winDivertHandle"/>.
        /// </summary>
        /// <param name="winDivertHandle">The win divert handle to receive from.</param>
        /// <returns>A new <seealso cref="InterceptedTcpPacket"/>.</returns>
        public static unsafe InterceptedTcpPacket Receive(IntPtr winDivertHandle)
        {
            if (winDivertHandle.ToInt64() < 1)
            {
                return null;
            }

            var address = new WinDivertAddress();
            var buffer = new WinDivertBuffer();
            var packetLength = 0u;

            if (!WinDivert.WinDivertRecv(winDivertHandle, buffer, ref address, ref packetLength))
            {
                return null;
            }

            var parseResult = WinDivert.WinDivertHelperParsePacket(buffer, packetLength);
            var ipHeader = parseResult.IPv4Header;
            var tcpHeader = parseResult.TcpHeader;

            if (ipHeader == null || tcpHeader == null)
            {
                return null;
            }

            return new InterceptedTcpPacket()
            {
                WinDivertAddress = address,
                WinDivertBuffer = buffer,
                WinDivertParseResult = parseResult,
                PacketLength = packetLength
            };
        }

        private InterceptedTcpPacket()
        { }

        /// <summary>
        /// Set the <seealso cref="HasBeenModified"/> flag.
        /// </summary>
        public void MarkAsModified()
        {
            HasBeenModified = true;
        }

        /// <summary>
        /// Sets the <seealso cref="ShouldBeDropped"/> flag.
        /// </summary>
        public void Drop()
        {
            ShouldBeDropped = true;
        }

        /// <summary>
        /// Indicates, whether the packet belongs to the given <paramref name="tcpConnection"/>.
        /// </summary>
        /// <param name="tcpConnection">A <seealso cref="NormalizedTcpConnection"/>.</param>
        /// <returns>True, if the packet belongs to the given <paramref name="tcpConnection"/>.</returns>
        public bool BelongsToConnection(NormalizedTcpConnection tcpConnection)
        {
            if (tcpConnection == null)
            {
                return false;
            }

            switch (WinDivertAddress.Direction)
            {
                case WinDivertDirection.Inbound:
                    {
                        return IPHeader.SrcAddr.Equals(tcpConnection.ServerIPAddress) &&
                               TcpHeader.SrcPort.ReverseBytes() == tcpConnection.ServerPort &&
                               IPHeader.DstAddr.Equals(tcpConnection.ClientIPAddress) &&
                               TcpHeader.DstPort.ReverseBytes() == tcpConnection.ClientPort;
                    }
                case WinDivertDirection.Outbound:
                    {
                        return IPHeader.SrcAddr.Equals(tcpConnection.ClientIPAddress) &&
                               TcpHeader.SrcPort.ReverseBytes() == tcpConnection.ClientPort &&
                               IPHeader.DstAddr.Equals(tcpConnection.ServerIPAddress) &&
                               TcpHeader.DstPort.ReverseBytes() == tcpConnection.ServerPort;
                    }
                default:
                    {
                        throw new Exception($"Unknown {typeof(WinDivertDirection).Name}: {WinDivertAddress.Direction}");
                    }
            }
        }

        /// <summary>
        /// Sends the packet using the given <paramref name="winDivertHandle"/>.
        /// </summary>
        /// <param name="winDivertHandle">The win divert handle to insert the packet to.</param>
        /// <returns>True, if the packet has been sent.</returns>
        public bool Send(IntPtr winDivertHandle)
        {
            if (winDivertHandle.ToInt64() < 1)
            {
                WinDivertBuffer?.Dispose();
                return false;
            }

            if (ShouldBeDropped)
            {
                WinDivertBuffer?.Dispose();
                return true;
            }

            if (HasBeenModified)
            {
                WinDivert.WinDivertHelperCalcChecksums(WinDivertBuffer, PacketLength, ref _winDivertAddress,
                    WinDivertChecksumHelperParam.NoIcmpChecksum | WinDivertChecksumHelperParam.NoIcmpV6Checksum |
                    WinDivertChecksumHelperParam.NoUdpChecksum);
            }

            if (!WinDivert.WinDivertSend(winDivertHandle, WinDivertBuffer, PacketLength, ref _winDivertAddress))
            {
                WinDivertBuffer?.Dispose();
                return false;
            }

            WinDivertBuffer?.Dispose();
            return true;
        }
    }
}