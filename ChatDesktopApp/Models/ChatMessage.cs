using System;
using Newtonsoft.Json;

namespace ChatDesktopApp.Models
{
    /// <summary>
    /// Model đại diện cho gói tin JSON giao tiếp giữa Client và Java Server.
    /// Khớp 100% với model ChatMessage.java phía Server.
    /// </summary>
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
                if (string.IsNullOrWhiteSpace(json)) return null;
                return JsonConvert.DeserializeObject<ChatMessage>(json);
            }
            catch
            {
                return null;
            }
        }
    }
}
