using System;
using System.Collections.Generic;
using System.IO;
using ChatDesktopApp.Models;
using Newtonsoft.Json;

namespace ChatDesktopApp.Services
{
    /// <summary>
    /// Quản lý lưu trữ và nạp lịch sử chat cục bộ dưới dạng JSON (AppData/ChatDesktopApp/history_{username}.json)
    /// </summary>
    public class LocalStorageService
    {
        private readonly string _storageFilePath;

        public LocalStorageService(string username)
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string folder = Path.Combine(appData, "ChatDesktopApp");
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            string safeUser = string.IsNullOrWhiteSpace(username) ? "guest" : username.Trim().ToLowerInvariant();
            _storageFilePath = Path.Combine(folder, $"history_{safeUser}.json");
        }

        public Dictionary<string, List<ChatMessage>> LoadHistory()
        {
            try
            {
                if (File.Exists(_storageFilePath))
                {
                    string json = File.ReadAllText(_storageFilePath);
                    var data = JsonConvert.DeserializeObject<Dictionary<string, List<ChatMessage>>>(json);
                    return data ?? new Dictionary<string, List<ChatMessage>>(StringComparer.OrdinalIgnoreCase);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LocalStorageService] Lỗi đọc lịch sử: {ex.Message}");
            }

            return new Dictionary<string, List<ChatMessage>>(StringComparer.OrdinalIgnoreCase);
        }

        public void SaveHistory(Dictionary<string, List<ChatMessage>> history)
        {
            try
            {
                string json = JsonConvert.SerializeObject(history, Formatting.Indented);
                File.WriteAllText(_storageFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LocalStorageService] Lỗi lưu lịch sử: {ex.Message}");
            }
        }
    }
}
