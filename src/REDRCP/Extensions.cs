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
    }
}
