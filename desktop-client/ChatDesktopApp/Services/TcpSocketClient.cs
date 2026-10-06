using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using ChatDesktopApp.Models;

namespace ChatDesktopApp.Services
{
    public class TcpSocketClient
    {
        private TcpClient _client;
        private StreamReader _reader;
        private StreamWriter _writer;
        private bool _isConnected;

        public event Action<ChatMessage> OnMessageReceived;
        public event Action<string> OnErrorOccurred;
        public event Action OnDisconnected;

        public string CurrentUsername { get; private set; }
        public string SessionToken { get; set; }
        public bool IsConnected => _isConnected;

        public async Task<bool> ConnectAsync(string ip, int port)
        {
            try
            {
                _client = new TcpClient();
                await _client.ConnectAsync(ip, port);

                var stream = _client.GetStream();
                _reader = new StreamReader(stream, Encoding.UTF8);
                _writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

                _isConnected = true;
                _ = Task.Run(ListenForMessagesAsync);
                return true;
            }
            catch (Exception ex)
            {
                OnErrorOccurred?.Invoke($"Lỗi kết nối Server: {ex.Message}");
                return false;
            }
        }

        public async Task SendLoginAsync(string username)
        {
            CurrentUsername = username;
            var msg = new ChatMessage
            {
                Action = "LOGIN",
                Sender = username,
                Content = "login_request"
            };
            await SendMessageAsync(msg);
        }

        public async Task SendChatMessageAsync(string receiver, string content)
        {
            var action = string.Equals(receiver, "ALL", StringComparison.OrdinalIgnoreCase) ? "CHAT_GROUP" : "CHAT_SINGLE";
            var msg = new ChatMessage
            {
                Action = action,
                Token = SessionToken,
                Sender = CurrentUsername,
                Receiver = receiver,
                Content = content
            };
            await SendMessageAsync(msg);
        }

        public async Task SendTypingStatusAsync(string receiver)
        {
            var msg = new ChatMessage
            {
                Action = "TYPING",
                Token = SessionToken,
                Sender = CurrentUsername,
                Receiver = receiver,
                Content = "is_typing"
            };
            await SendMessageAsync(msg);
        }

        public async Task SendMessageAsync(ChatMessage message)
        {
            if (!_isConnected || _writer == null) return;
            try
            {
                string jsonStr = message.ToJson();
                // Framing protocol: Appending '\n' at end of JSON string
                await _writer.WriteLineAsync(jsonStr);
            }
            catch (Exception ex)
            {
                OnErrorOccurred?.Invoke($"Lỗi khi gửi gói tin: {ex.Message}");
            }
        }

        private async Task ListenForMessagesAsync()
        {
            try
            {
                string line;
                // Reading line by line using '\n' delimiter
                while (_isConnected && (line = await _reader.ReadLineAsync()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var msg = ChatMessage.FromJson(line);
                    if (msg != null)
                    {
                        if (msg.Action == "LOGIN_SUCCESS" && !string.IsNullOrEmpty(msg.Token))
                        {
                            SessionToken = msg.Token;
                        }
                        OnMessageReceived?.Invoke(msg);
                    }
                }
            }
            catch (Exception ex)
            {
                if (_isConnected)
                {
                    OnErrorOccurred?.Invoke($"Mất kết nối với Server: {ex.Message}");
                }
            }
            finally
            {
                Disconnect();
            }
        }

        public void Disconnect()
        {
            if (!_isConnected) return;
            _isConnected = false;
            try
            {
                _reader?.Close();
                _writer?.Close();
                _client?.Close();
            }
            catch { }
            OnDisconnected?.Invoke();
        }
    }
}
