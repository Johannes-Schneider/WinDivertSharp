using System;
using System.Net;
using WinDivertSharp.Extensions;

namespace WinDivertSharp.Wrapper.Tcp
{
    /// <summary>
    /// Represents a normalized ip/tcp connection.
    /// </summary>
    public class NormalizedTcpConnection : IEquatable<NormalizedTcpConnection>
    {
        /// <summary>
        /// The <seealso cref="IPAddress"/> of the client end.
        /// </summary>
        public IPAddress ClientIPAddress { get; private set; }

        /// <summary>
        /// The port of the client end.
        /// </summary>
        public ushort ClientPort { get; private set; }

        /// <summary>
        /// The <seealso cref="IPAddress"/> of the server end.
        /// </summary>
        public IPAddress ServerIPAddress { get; private set; }

        /// <summary>
        /// The port of the server end.
        /// </summary>
        public ushort ServerPort { get; private set; }

        /// <summary>
        /// The process id of the client process.
        /// </summary>
        public int ClientProcessId =>
            new Lazy<int>(() => IPHelper.MapLocalPortToProcessId(ClientPort, _ipAddressVersion)).Value;

        private IPHelper.AddressVersion _ipAddressVersion;
        private int _cachedHashCode = 0;

        /// <summary>
        /// Creates a new <seealso cref="NormalizedTcpConnection"/> from the given <paramref name="packet"/>.
        /// </summary>
        /// <param name="packet">The <seealso cref="IWinDivertTcpPacketWrapper"/> to create a new <seealso cref="NormalizedTcpConnection"/> from.</param>
        /// <returns>A new <seealso cref="NormalizedTcpConnection"/> based on the given <paramref name="packet"/>.</returns>
        public static NormalizedTcpConnection CreateFromInterceptedPacket(IWinDivertTcpPacketWrapper packet)
        {
            if (packet == null)
            {
                throw new ArgumentNullException(nameof(packet));
            }

            switch (packet.Direction)
            {
                case WinDivertDirection.Inbound:
                {
                    return new NormalizedTcpConnection()
                    {
                        _ipAddressVersion = packet.IPAddressVersion,
                        ClientIPAddress = packet.DestinationAddress,
                        ClientPort = packet.DestinationPort,
                        ServerIPAddress = packet.SourceAddress,
                        ServerPort = packet.SourcePort
                    };
                }
                case WinDivertDirection.Outbound:
                {
                    return new NormalizedTcpConnection()
                    {
                        _ipAddressVersion = packet.IPAddressVersion,
                        ClientIPAddress = packet.SourceAddress,
                        ClientPort = packet.SourcePort,
                        ServerIPAddress = packet.DestinationAddress,
                        ServerPort = packet.DestinationPort
                    };
                }
                default:
                {
                    throw new Exception($"Unknown {typeof(WinDivertDirection).Name}: {packet.Direction}");
                }
            }
        }

        private NormalizedTcpConnection()
        { }

        /// <summary>
        /// Indicates, whether this <seealso cref="NormalizedTcpConnection"/> equals the given <paramref name="other"/>.
        /// </summary>
        /// <param name="other">The <seealso cref="NormalizedTcpConnection"/> to compare this instance with.</param>
        /// <returns>True, if both instances are equal.</returns>
        public bool Equals(NormalizedTcpConnection other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(ClientIPAddress, other.ClientIPAddress) && ClientPort == other.ClientPort && Equals(ServerIPAddress, other.ServerIPAddress) && ServerPort == other.ServerPort;
        }

        /// <summary>
        /// Indicates, whether this <seealso cref="NormalizedTcpConnection"/> equals the given <paramref name="obj"/>.
        /// </summary>
        /// <param name="obj">The object to compare this instance with.</param>
        /// <returns>True, if the object equals this instance.</returns>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((NormalizedTcpConnection)obj);
        }

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        /// <returns>The hash code for this instance.</returns>
        public override int GetHashCode()
        {
            if (_cachedHashCode < 1)
            {
                unchecked
                {
                    _cachedHashCode = (ClientIPAddress != null ? ClientIPAddress.GetHashCode() : 0);
                    _cachedHashCode = (_cachedHashCode * 397) ^ ClientPort.GetHashCode();
                    _cachedHashCode = (_cachedHashCode * 397) ^ (ServerIPAddress != null ? ServerIPAddress.GetHashCode() : 0);
                    _cachedHashCode = (_cachedHashCode * 397) ^ ServerPort.GetHashCode();
                }
            }

            return _cachedHashCode;
        }

        /// <summary>
        /// Returns the textual representation of this <seealso cref="NormalizedTcpConnection"/>.
        /// </summary>
        /// <returns>The textual representation of this <seealso cref="NormalizedTcpConnection"/>.</returns>
        public override string ToString()
        {
            return $"[{ClientProcessId}] {ClientIPAddress}:{ClientPort} -> {ServerIPAddress}:{ServerPort}";
        }
    }
}