﻿/*
* Copyright © 2018-Present Jesse Nicholson
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using WinDivertSharp;
using WinDivertSharp.Extensions;
using WinDivertSharp.WinAPI;

namespace WinDivertSharpSandbox
{
    internal class Program
    {
        private static volatile bool s_running = true;

        private static void Main(string[] args)
        {
            Console.CancelKeyPress += ((sender, eArgs) =>
            {
                s_running = false;
            });

            string filter = "tcp";
            string badFilter = "stoopid and not super serious and tcp";

            uint errorPos = 0;

            if (!WinDivert.WinDivertHelperCheckFilter(filter, WinDivertLayer.Network, out string errorMsg, ref errorPos))
            {
                Console.WriteLine($"Error in filter string at position {errorPos}");
                Console.WriteLine(errorMsg);
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey();
                return;
            }

            errorPos = 0;

            if (!WinDivert.WinDivertHelperCheckFilter(badFilter, WinDivertLayer.Network, out string errorMsg2, ref errorPos))
            {
                Console.WriteLine($"Error in filter string at position {errorPos}");
                Console.WriteLine(errorMsg2);
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey();
            }

            var handle = WinDivert.WinDivertOpen(filter, WinDivertLayer.Network, 0, WinDivertOpenFlags.None);

            if (handle == IntPtr.Zero || handle == new IntPtr(-1))
            {
                Console.WriteLine("Invalid handle. Failed to open.");
                Console.ReadKey();
                return;
            }

            // Set everything to maximum values.
            WinDivert.WinDivertSetParam(handle, WinDivertParam.QueueLen, 16384);
            WinDivert.WinDivertSetParam(handle, WinDivertParam.QueueTime, 8000);
            WinDivert.WinDivertSetParam(handle, WinDivertParam.QueueSize, 33554432);

            var threads = new List<Thread>();

            for (int i = 0; i < Environment.ProcessorCount; ++i)
            {
                threads.Add(new Thread(() =>
                {
                    RunDiversion(handle);
                }));

                threads.Last().Start();
            }

            foreach (var dt in threads)
            {
                dt.Join();
            }

            WinDivert.WinDivertClose(handle);
        }

        private static unsafe void RunDiversion(IntPtr handle)
        {
            var packet = new WinDivertBuffer();

            var addr = new WinDivertAddress();

            uint readLen = 0;

            IPv4Header* ipv4Header = null;
            IPv6Header* ipv6Header = null;
            IcmpV4Header* icmpV4Header = null;
            IcmpV6Header* icmpV6Header = null;
            TcpHeader* tcpHeader = null;
            UdpHeader* udpHeader = null;

            Span<byte> packetData = null;

            NativeOverlapped recvOverlapped;

            IntPtr recvEvent = IntPtr.Zero;
            uint recvAsyncIoLen = 0;

            do
            {
                if (s_running)
                {
                    packetData = null;

                    readLen = 0;

                    recvAsyncIoLen = 0;
                    recvOverlapped = new NativeOverlapped();

                    recvEvent = Kernel32.CreateEvent(IntPtr.Zero, false, false, IntPtr.Zero);

                    if (recvEvent == IntPtr.Zero)
                    {
                        Console.WriteLine("Failed to initialize receive IO event.");
                        continue;
                    }

                    addr.Reset();

                    recvOverlapped.EventHandle = recvEvent;

                    Console.WriteLine("Read");

                    if (!WinDivert.WinDivertRecvEx(handle, packet, 0, ref addr, ref readLen, ref recvOverlapped))
                    {
                        var error = Marshal.GetLastWin32Error();

                        // 997 == ERROR_IO_PENDING
                        if (error != 997)
                        {
                            Console.WriteLine(string.Format("Unknown IO error ID {0} while awaiting overlapped result.", error));
                            Kernel32.CloseHandle(recvEvent);
                            continue;
                        }

                        while (Kernel32.WaitForSingleObject(recvEvent, 1000) == (uint)WaitForSingleObjectResult.WaitTimeout)
                            ;

                        if (!Kernel32.GetOverlappedResult(handle, ref recvOverlapped, ref recvAsyncIoLen, false))
                        {
                            Console.WriteLine("Failed to get overlapped result.");
                            Kernel32.CloseHandle(recvEvent);
                            continue;
                        }

                        readLen = recvAsyncIoLen;
                    }

                    Kernel32.CloseHandle(recvEvent);

                    Console.WriteLine("Read packet {0}", readLen);

                    var parseResult = WinDivert.WinDivertHelperParsePacket(packet, readLen);
                    ipv4Header = parseResult.IPv4Header;
                    ipv6Header = parseResult.IPv6Header;
                    icmpV4Header = parseResult.IcmpV4Header;
                    icmpV6Header = parseResult.IcmpV6Header;
                    tcpHeader = parseResult.TcpHeader;
                    udpHeader = parseResult.UdpHeader;

                    if (addr.Direction == WinDivertDirection.Inbound)
                    {
                        Console.WriteLine("inbound!");
                    }

                    if (ipv4Header != null && tcpHeader != null)
                    {
                        Console.WriteLine($"V4 TCP packet {addr.Direction} from {ipv4Header->SrcAddr}:{tcpHeader->SrcPort.ReverseBytes()} to {ipv4Header->DstAddr}:{tcpHeader->DstPort.ReverseBytes()}");
                    }
                    else if (ipv6Header != null && tcpHeader != null)
                    {
                        Console.WriteLine($"V4 TCP packet {addr.Direction} from {ipv6Header->SrcAddr}:{tcpHeader->SrcPort.ReverseBytes()} to {ipv6Header->DstAddr}:{tcpHeader->DstPort.ReverseBytes()}");
                    }

                    if (packetData != null)
                    {
                        Console.WriteLine("Packet has {0} byte payload.", packetData.Length);
                    }

                    Console.WriteLine($"{nameof(addr.Direction)} - {addr.Direction}");
                    Console.WriteLine($"{nameof(addr.Impostor)} - {addr.Impostor}");
                    Console.WriteLine($"{nameof(addr.Loopback)} - {addr.Loopback}");
                    Console.WriteLine($"{nameof(addr.IfIdx)} - {addr.IfIdx}");
                    Console.WriteLine($"{nameof(addr.SubIfIdx)} - {addr.SubIfIdx}");
                    Console.WriteLine($"{nameof(addr.Timestamp)} - {addr.Timestamp}");
                    Console.WriteLine($"{nameof(addr.PseudoIPChecksum)} - {addr.PseudoIPChecksum}");
                    Console.WriteLine($"{nameof(addr.PseudoTCPChecksum)} - {addr.PseudoTCPChecksum}");
                    Console.WriteLine($"{nameof(addr.PseudoUDPChecksum)} - {addr.PseudoUDPChecksum}");

                    // Console.WriteLine(WinDivert.WinDivertHelperCalcChecksums(packet, ref addr, WinDivertChecksumHelperParam.All));

                    if (!WinDivert.WinDivertSendEx(handle, packet, readLen, 0, ref addr))
                    {
                        Console.WriteLine("Write Err: {0}", Marshal.GetLastWin32Error());
                    }
                }
            }
            while (s_running);
        }
    }
}