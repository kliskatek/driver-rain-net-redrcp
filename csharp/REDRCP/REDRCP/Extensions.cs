using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kliskatek.REDRCP
{
    public static class Extensions
    {
        public static void WriteToSerialInterface(this List<byte> byteList, SerialPort serialPort)
        {
            serialPort.Write(byteList.ToArray(), 0, byteList.Count);
        }

        public static void WriteToSerialInterface(this byte[] byteArray, SerialPort serialPort)
        {
            serialPort.Write(byteArray, 0, byteArray.Length);
        }

        public static T[] GetArraySlice<T>(this T[] inputArray, int startIndex, int sliceItemCount)
        {
            return (new ArraySegment<T>(inputArray)).Slice(startIndex, sliceItemCount).ToArray();
        }

        public static T[] GetArraySlice<T>(this T[] inputArray, int startIndex)
        {
            return (new ArraySegment<T>(inputArray)).Slice(startIndex, inputArray.Length - startIndex).ToArray();
        }

    }
}
