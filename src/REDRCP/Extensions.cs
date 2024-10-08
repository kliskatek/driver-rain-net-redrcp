using Newtonsoft.Json;

namespace Kliskatek.Driver.Rain.REDRCP
{
    public static class Extensions
    {
        public static T[] GetArraySlice<T>(this T[] inputArray, int startIndex, int sliceItemCount)
        {
            return (new ArraySegment<T>(inputArray)).Slice(startIndex, sliceItemCount).ToArray();
        }

        public static T[] GetArraySlice<T>(this T[] inputArray, int startIndex)
        {
            return (new ArraySegment<T>(inputArray)).Slice(startIndex, inputArray.Length - startIndex).ToArray();
        }

        public static bool TryParseJson<T>(this string testSerializedData)
        {
            try
            {
                var result = JsonConvert.DeserializeObject<T>(testSerializedData);
                return true;
            }
            catch (Exception e)
            {
                return false;
            }
        }

        public static byte ToByte(this EnableStatus enableStatus)
        {
            byte returnValue = 0;
            returnValue += (byte)(enableStatus.Sel1 ? 0x01 : 0);
            returnValue += (byte)(enableStatus.Sel2 ? 0x02 : 0);
            returnValue += (byte)(enableStatus.Sel3 ? 0x04 : 0);
            returnValue += (byte)(enableStatus.Sel4 ? 0x08 : 0);
            returnValue += (byte)(enableStatus.Sel5 ? 0x10 : 0);
            returnValue += (byte)(enableStatus.Sel6 ? 0x20 : 0);
            returnValue += (byte)(enableStatus.Sel7 ? 0x40 : 0);
            returnValue += (byte)(enableStatus.Sel8 ? 0x80 : 0);
            return returnValue;
        }

        public static EnableStatus ToEnableStatus(this byte enableStatus)
        {
            return new EnableStatus
            {
                Sel1 = (enableStatus & 0x01) > 0,
                Sel2 = (enableStatus & 0x02) > 0,
                Sel3 = (enableStatus & 0x04) > 0,
                Sel4 = (enableStatus & 0x08) > 0,
                Sel5 = (enableStatus & 0x10) > 0,
                Sel6 = (enableStatus & 0x20) > 0,
                Sel7 = (enableStatus & 0x40) > 0,
                Sel8 = (enableStatus & 0x80) > 0,
            };
        }

        public static string RemoveHyphen(this string text)
        {
            return text.Replace("-", "");
        }
    }
}
