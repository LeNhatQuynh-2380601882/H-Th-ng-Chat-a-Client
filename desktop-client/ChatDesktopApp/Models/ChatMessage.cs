using System;
using Newtonsoft.Json;

namespace ChatDesktopApp.Models
{
    public class ChatMessage
    {
        [JsonProperty("action")]
        public string Action { get; set; }

        [JsonProperty("token")]
        public string Token { get; set; }

        [JsonProperty("sender")]
        public string Sender { get; set; }

        [JsonProperty("receiver")]
        public string Receiver { get; set; }

        [JsonProperty("content")]
        public string Content { get; set; }

        [JsonProperty("timestamp")]
        public long Timestamp { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this);
        }

        public static ChatMessage FromJson(string json)
        {
            try
            {
                return JsonConvert.DeserializeObject<ChatMessage>(json);
            }
            catch
            {
                return null;
            }
        }
    }
}
