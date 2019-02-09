using System;
using System.Collections.Generic;
using System.Text;

namespace WinDivertSharp.Extensions
{
    public static class IntegralTypeExtensions
    {
        public static byte GetBit(this byte @byte, int index)
        {
            return (byte)(@byte & (1 << index - 1));
        }

        public static byte SetBit(this byte @byte, int index)
        {
            return (byte)(@byte & (1 << index - 1));
        }

        public static UInt16 ReverseBytes(this UInt16 value)
        {
            return (UInt16)((value & 0xFFU) << 8 | (value & 0xFF00U) >> 8);
        }
    }
}
