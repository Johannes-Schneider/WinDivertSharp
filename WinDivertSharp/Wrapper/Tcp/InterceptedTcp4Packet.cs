using System;
using System.Net;
using WinDivertSharp.Extensions;

namespace WinDivertSharp.Wrapper.Tcp
{
    /// <summary>
    /// Represents an intercepted ipv4/tcp packet.
    /// </summary>
    public class InterceptedTcp4Packet : IWinDivertTcpPacketWrapper
    {
        public IPHelper.AddressVersion IPAddressVersion => IPHelper.AddressVersion.IPv4;

        public IPv4Header IPHeader
        {
            get
            {
                unsafe
                {
                    return *_winDivertParseResult.IPv4Header;
                }
            }
        }

        public TcpHeader TcpHeader
        {
            get
            {
                unsafe
                {
                    return *_winDivertParseResult.TcpHeader;
                }
            }
        }
        
        /// <summary>
        /// The <see cref="IPAddress"/> of the packets sender.
        /// </summary>
        public IPAddress SourceAddress
        {
            get
            {
                unsafe
                {
                    return _winDivertParseResult.IPv4Header->SrcAddr;
                }
            }
            set
            {
                unsafe
                {
                    if (SourceAddress.Equals(value))
                    {
                        return;
                    }

                    _winDivertParseResult.IPv4Header->SrcAddr = value ?? throw new ArgumentNullException();
                    IsModified = true;
                }
            }
        }

        /// <summary>
        /// The <see cref="IPAddress"/> of the packets receiver.
        /// </summary>
        public IPAddress DestinationAddress
        {
            get
            {
                unsafe
                {
                    return _winDivertParseResult.IPv4Header->DstAddr;
                }
            }
            set
            {
                unsafe
                {
                    if (DestinationAddress.Equals(value))
                    {
                        return;
                    }

                    _winDivertParseResult.IPv4Header->DstAddr = value ?? throw new ArgumentNullException();
                    IsModified = true;
                }
            }
        }

        /// <summary>
        /// The port of the packets sender.
        /// </summary>
        public ushort SourcePort
        {
            get
            {
                unsafe
                {
                    return _winDivertParseResult.TcpHeader->SrcPort.ReverseBytes();
                }
            }
            set
            {
                unsafe
                {
                    if (SourcePort.Equals(value))
                    {
                        return;
                    }

                    _winDivertParseResult.TcpHeader->SrcPort = value.ReverseBytes();
                    IsModified = true;
                }
            }
        }

        /// <summary>
        /// The port of the packets receiver.
        /// </summary>
        public ushort DestinationPort
        {
            get
            {
                unsafe
                {
                    return _winDivertParseResult.TcpHeader->DstPort.ReverseBytes();
                }
            }
            set
            {
                unsafe
                {
                    if (DestinationPort.Equals(value))
                    {
                        return;
                    }

                    _winDivertParseResult.TcpHeader->DstPort = value.ReverseBytes();
                    IsModified = true;
                }
            }
        }

        /// <summary>
        /// The intercepted <seealso cref="Direction"/>.
        /// </summary>
        public WinDivertDirection Direction
        {
            get => _winDivertAddress.Direction;
            set
            {
                if (Direction.Equals(value))
                {
                    return;
                }

                _winDivertAddress.Direction = value;
                IsModified = true;
            }
        }

        /// <summary>
        /// Indicates, whether the packet has already been modified.
        /// </summary>
        public bool IsModified { get; private set; } = false;

        /// <summary>
        /// Indicates, whether the packet should be dropped.
        /// </summary>
        public bool IsAboutToBeDropped { get; private set; } = false;

        /// <summary>
        /// Receives the next packet in the queue of the given <paramref name="wrapper"/>.
        /// </summary>
        /// <param name="wrapper">The <seealso cref="WinDivertTcp4Wrapper"/> to receive from.</param>
        /// <returns>A new <seealso cref="InterceptedTcp4Packet"/>.</returns>
        internal static unsafe InterceptedTcp4Packet Receive(WinDivertTcp4Wrapper wrapper)
        {
            if (wrapper.Handle.ToInt64() < 1)
            {
                return null;
            }

            var address = new WinDivertAddress();
            var buffer = new WinDivertBuffer();
            var packetLength = 0u;

            if (!WinDivert.WinDivertRecv(wrapper.Handle, buffer, ref address, ref packetLength))
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

            return new InterceptedTcp4Packet(wrapper.Handle)
            {
                _winDivertAddress = address,
                _winDivertBuffer = buffer,
                _winDivertParseResult = parseResult,
                _packetLength = packetLength
            };
        }

        private readonly IntPtr _winDivertHandle;
        private WinDivertAddress _winDivertAddress;
        private WinDivertBuffer _winDivertBuffer;
        private WinDivertParseResult _winDivertParseResult;
        private uint _packetLength;

        private InterceptedTcp4Packet(IntPtr winDivertHandle)
        {
            if (winDivertHandle.ToInt64() < 1)
            {
                throw new ArgumentNullException(nameof(winDivertHandle));
            }

            _winDivertHandle = winDivertHandle;
        }

        /// <summary>
        /// Sets the <seealso cref="IsAboutToBeDropped"/> flag.
        /// </summary>
        public void Drop()
        {
            IsAboutToBeDropped = true;
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

            switch (Direction)
            {
                case WinDivertDirection.Inbound:
                    {
                        return SourceAddress.Equals(tcpConnection.ServerIPAddress) &&
                               SourcePort == tcpConnection.ServerPort &&
                               DestinationAddress.Equals(tcpConnection.ClientIPAddress) &&
                               DestinationPort == tcpConnection.ClientPort;
                    }
                case WinDivertDirection.Outbound:
                    {
                        return SourceAddress.Equals(tcpConnection.ClientIPAddress) &&
                               SourcePort == tcpConnection.ClientPort &&
                               DestinationAddress.Equals(tcpConnection.ServerIPAddress) &&
                               DestinationPort == tcpConnection.ServerPort;
                    }
                default:
                    {
                        throw new Exception($"Unknown {typeof(WinDivertDirection).Name}: {Direction}");
                    }
            }
        }

        /// <summary>
        /// Sends the packet.
        /// </summary>
        /// <returns>True, if the packet has been sent.</returns>
        public bool Send()
        {
            if (IsAboutToBeDropped)
            {
                return true;
            }

            if (IsModified)
            {
                WinDivert.WinDivertHelperCalcChecksums(_winDivertBuffer, _packetLength, ref _winDivertAddress,
                                                       WinDivertChecksumHelperParam.NoIcmpChecksum | WinDivertChecksumHelperParam.NoIcmpV6Checksum |
                                                       WinDivertChecksumHelperParam.NoUdpChecksum);
            }

            if (!WinDivert.WinDivertSend(_winDivertHandle, _winDivertBuffer, _packetLength, ref _winDivertAddress))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Disposes this packet.
        /// </summary>
        public void Dispose()
        {
            _winDivertBuffer?.Dispose();
        }
    }
}