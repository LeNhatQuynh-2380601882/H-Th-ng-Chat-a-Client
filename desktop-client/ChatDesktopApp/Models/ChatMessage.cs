using System;
using Newtonsoft.Json;

namespace ChatDesktopApp.Models
{
    /// <summary>
    /// Model đại diện cho gói tin JSON giao tiếp giữa Client và Java Server.
    /// Khớp với model ChatMessage.java phía Server.
    /// Phiên bản cải tiến: thêm msgId (UUID) và clientType để phân biệt nguồn gửi.
    /// </summary>
    public class ChatMessage
    {
        /// <summary>
        /// Loại hành động của gói tin (LOGIN, CHAT_SINGLE, CHAT_GROUP, TYPING, LOGOUT...)
        /// </summary>
        [JsonProperty("action")]
        public string Action { get; set; }

        /// <summary>
        /// Session Token xác thực — bắt buộc với mọi gói tin sau khi đăng nhập.
        /// </summary>
        [JsonProperty("token")]
        public string Token { get; set; }

        /// <summary>
        /// Định danh duy nhất của gói tin (UUID v4). Hỗ trợ deduplication và Read Receipt.
        /// Tự động tạo khi khởi tạo nếu không cung cấp.
        /// </summary>
        [JsonProperty("msgId")]
        public string MsgId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Tên người gửi tin nhắn.
        /// </summary>
        [JsonProperty("sender")]
        public string Sender { get; set; }

        /// <summary>
        /// Tên người nhận. Dùng "ALL" để broadcast cho toàn bộ CHAT_GROUP.
        /// </summary>
        [JsonProperty("receiver")]
        public string Receiver { get; set; }

        /// <summary>
        /// Nội dung chính của gói tin (văn bản chat, trạng thái typing, v.v.)
        /// </summary>
        [JsonProperty("content")]
        public string Content { get; set; }

        /// <summary>
        /// Unix timestamp milliseconds tại thời điểm tạo gói tin.
        /// </summary>
        [JsonProperty("timestamp")]
        public long Timestamp { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        /// <summary>
        /// Loại client gửi gói tin: "DESKTOP" (C# WinForms) | "WEB" (Browser WebSocket).
        /// Phục vụ thống kê và logging phía Server.
        /// </summary>
        [JsonProperty("clientType")]
        public string ClientType { get; set; } = "DESKTOP";

        /// <summary>
        /// Serialize object thành chuỗi JSON (không kèm \n — framing do TcpSocketClient xử lý).
        /// </summary>
        public string ToJson()
        {
            return JsonConvert.SerializeObject(this, Formatting.None);
        }

        /// <summary>
        /// Parse chuỗi JSON thành ChatMessage. Trả về null nếu không hợp lệ.
        /// </summary>
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
