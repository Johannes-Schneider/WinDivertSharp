using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using WinDivertSharp.Extensions;

namespace WinDivertSharp.Wrapper
{
    public class IPHelper
    {
        #region IPHelper_PInvokes

        public enum AddressVersion : uint
        {
            IPv4 = 2,
            IPv6 = 23,
        }

        private const int ERROR_INSUFFICIENT_BUFFER = 0x7a;
        private const int NO_ERROR = 0x0;

        [DllImport("iphlpapi.dll", ExactSpelling = true, SetLastError = true)]
        private static extern uint GetExtendedTcpTable(IntPtr pTcpTable, ref UInt32 dwTcpTableLength, [MarshalAs(UnmanagedType.Bool)] bool sort, UInt32 ipVersion, TcpTableType tcpTableType, UInt32 reserved);

        private enum TcpTableType
        {
            BasicListener,
            BasicConnections,
            BasicAll,
            OwnerPidListener,
            OwnerPidConnections,
            OwnerPidAll,
            OwnerModuleListener,
            OwnerModuleConnections,
            OwnerModuleAll
        }
        #endregion IPHelper_PInvokes

        public static int MapLocalPortToProcessId(ushort port, AddressVersion addressVersion)
        {
            try
            {
                return TryMapLocalPortToProcessId(port, addressVersion);
            }
            catch (Exception)
            {
                return 0;
            }
        }

        public static int NumberOfActiveConnections(int processId, AddressVersion addressVersion)
        {
            try
            {
                return TryFindNumberOfActiveConnectionsOfProcess(processId, addressVersion);
            }
            catch (Exception)
            {
                return 0;
            }
        }

        private static int TryMapLocalPortToProcessId(ushort port, AddressVersion addressVersion)
        {
            var ptrTcpTable = IntPtr.Zero;
            var tcpTableLength = 0u;

            var offsetToFirstPort = 12;
            var offsetToPIDInRow = 12;
            var tableRowSize = 24; // 24 == Marshal.SizeOf(typeof(TcpRow));

            // IPv6 tables are a different size, so adjust the offsets accordingly
            if (addressVersion == AddressVersion.IPv6)
            {
                offsetToFirstPort = 24;
                offsetToPIDInRow = 32;
                tableRowSize = 56;
            }

            // Determine the size of the memory block to allocate
            if (ERROR_INSUFFICIENT_BUFFER != GetExtendedTcpTable(ptrTcpTable, ref tcpTableLength, false, (uint) addressVersion, TcpTableType.OwnerPidConnections, 0))
            {
                return 0;
            }

            try
            {
                ptrTcpTable = Marshal.AllocHGlobal((int)tcpTableLength);

                // Would it be faster to set the SORTED argument to true, and then iterate the table in reverse order?
                if (NO_ERROR == GetExtendedTcpTable(ptrTcpTable, ref tcpTableLength, false, (uint)addressVersion, TcpTableType.OwnerPidConnections, 0))
                {
                    // Convert port we're looking for into Network byte order
                    var portReversed = port.ReverseBytes();

                    // ISSUE: This function APPEARS to work fine, but might blow up on Itanium or exotic architectures like that. As noted in the docs:
                    // The MIB_TCPTABLE_OWNER_PID structure may contain padding for alignment between the dwNumEntries member and the first MIB_TCPROW_OWNER_PID
                    // array entry in the table  member. Padding for alignment may also be present between the MIB_TCPROW_OWNER_PID array entries in the table member. 
                    // Any access to a MIB_TCPROW_OWNER_PID array entry should assume padding may exist. 
                    //
                    // I have absolutely no idea how to detect such padding, or if .NET handles it automatically if I use PtrToStructure rather than the direct pointer 
                    // manipulation calls this function is now using.
                    //
                    var tableLen = Marshal.ReadInt32(ptrTcpTable);          // Get table row count
                    if (tableLen == 0)
                    {
                        Debug.Assert(false, "How is it possible that the API succeeded and there are really no network connections? Maybe pure IPv6 environment?");
                        return 0;
                    }
                    var ptrRow = (IntPtr)(ptrTcpTable.ToInt64() + offsetToFirstPort);       // Advance pointer to first Port in the table

                    // Iterate each row of the table, looking to see if localPortInNetworkOrder matches. If it does, return the owningPid
                    for (var i = 0; i < tableLen; ++i)
                    {
                        // Check for matching local port
                        if (portReversed == Marshal.ReadInt32(ptrRow))
                        {
                            return Marshal.ReadInt32(ptrRow, offsetToPIDInRow);
                            // Note: the finally clause below will clean up memory
                        }

                        // Move to the next row
                        ptrRow = (IntPtr)(ptrRow.ToInt64() + tableRowSize);
                    }
                }
                else
                {
                    return 0;
                }
            }
            finally
            {
                // Clean up unmanaged memory block. Call succeeds even if tcpTable == 0.
                Marshal.FreeHGlobal(ptrTcpTable);
            }
            return 0;
        }

        private static int TryFindNumberOfActiveConnectionsOfProcess(int processId, AddressVersion addressVersion)
        {
            var ptrTcpTable = IntPtr.Zero;
            var tcpTableLength = 0u;

            var offsetToFirstPort = 12;
            var offsetToPIDInRow = 12;
            var tableRowSize = 24; // 24 == Marshal.SizeOf(typeof(TcpRow));

            // IPv6 tables are a different size, so adjust the offsets accordingly
            if (addressVersion == AddressVersion.IPv6)
            {
                offsetToFirstPort = 24;
                offsetToPIDInRow = 32;
                tableRowSize = 56;
            }

            // Determine the size of the memory block to allocate
            if (ERROR_INSUFFICIENT_BUFFER != GetExtendedTcpTable(ptrTcpTable, ref tcpTableLength, false, (uint) addressVersion, TcpTableType.OwnerPidConnections, 0))
            {
                return 0;
            }

            try
            {
                ptrTcpTable = Marshal.AllocHGlobal((int)tcpTableLength);

                // Would it be faster to set the SORTED argument to true, and then iterate the table in reverse order?
                if (NO_ERROR == GetExtendedTcpTable(ptrTcpTable, ref tcpTableLength, false, (uint)addressVersion, TcpTableType.OwnerPidConnections, 0))
                {

                    var connectionsCount = 0;

                    // ISSUE: This function APPEARS to work fine, but might blow up on Itanium or exotic architectures like that. As noted in the docs:
                    // The MIB_TCPTABLE_OWNER_PID structure may contain padding for alignment between the dwNumEntries member and the first MIB_TCPROW_OWNER_PID
                    // array entry in the table  member. Padding for alignment may also be present between the MIB_TCPROW_OWNER_PID array entries in the table member. 
                    // Any access to a MIB_TCPROW_OWNER_PID array entry should assume padding may exist. 
                    //
                    // I have absolutely no idea how to detect such padding, or if .NET handles it automatically if I use PtrToStructure rather than the direct pointer 
                    // manipulation calls this function is now using.
                    //
                    var tableLen = Marshal.ReadInt32(ptrTcpTable);          // Get table row count
                    if (tableLen == 0)
                    {
                        Debug.Assert(false, "How is it possible that the API succeeded and there are really no network connections? Maybe pure IPv6 environment?");
                        return 0;
                    }
                    var ptrRow = (IntPtr)(ptrTcpTable.ToInt64() + offsetToFirstPort);       // Advance pointer to first Port in the table

                    // Iterate each row of the table, looking to see if localPortInNetworkOrder matches. If it does, return the owningPid
                    for (var i = 0; i < tableLen; ++i)
                    {
                        if (Marshal.ReadInt32(ptrRow, offsetToPIDInRow) == processId)
                        {
                            connectionsCount++;
                        }

                        // Move to the next row
                        ptrRow = (IntPtr)(ptrRow.ToInt64() + tableRowSize);
                    }

                    return connectionsCount;
                }
                else
                {
                    return 0;
                }
            }
            finally
            {
                // Clean up unmanaged memory block. Call succeeds even if tcpTable == 0.
                Marshal.FreeHGlobal(ptrTcpTable);
            }
        }
    }
}