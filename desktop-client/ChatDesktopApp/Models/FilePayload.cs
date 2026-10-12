using System;
using Newtonsoft.Json;

namespace ChatDesktopApp.Models
{
    /// <summary>
    /// Payload đính kèm trong Content khi Action = FILE_TRANSFER
    /// </summary>
    public class FilePayload
    {
        [JsonProperty("fileName")]
        public string FileName { get; set; }

        [JsonProperty("fileSize")]
        public long FileSize { get; set; }

        [JsonProperty("isImage")]
        public bool IsImage { get; set; }

        [JsonProperty("base64Data")]
        public string Base64Data { get; set; }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this, Formatting.None);
        }

        public static FilePayload FromJson(string json)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(json)) return null;
                return JsonConvert.DeserializeObject<FilePayload>(json);
            }
            catch
            {
                return null;
            }
        }
    }
}
