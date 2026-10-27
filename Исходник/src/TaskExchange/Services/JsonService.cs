using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;

namespace TaskExchange.Services
{
    internal static class JsonService
    {
        public static string Serialize<T>(T value)
        {
            var serializer = new DataContractJsonSerializer(typeof(T));
            using (var stream = new MemoryStream())
            {
                serializer.WriteObject(stream, value);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        public static T Deserialize<T>(string json) where T : new()
        {
            if (string.IsNullOrWhiteSpace(json)) return new T();
            var serializer = new DataContractJsonSerializer(typeof(T));
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                return (T)serializer.ReadObject(stream);
        }
    }
}
