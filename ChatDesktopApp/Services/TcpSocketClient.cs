using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ChatDesktopApp.Models;

namespace ChatDesktopApp.Services
{
    /// <summary>
    /// Lớp quản lý kết nối TCP Socket tới Java Server.
    /// Sử dụng NetworkStream, Task chạy nền (ReceiveLoopAsync) và kỹ thuật Delimiter-based Framing (\n).
    /// </summary>
    public class TcpSocketClient
    {
        private TcpClient _tcpClient;
        private NetworkStream _stream;
        private CancellationTokenSource _cts;
        private bool _isConnected;
        private readonly object _disconnectLock = new object();

        // Các sự kiện callback thông báo trạng thái và dữ liệu tới UI
        public event Action<ChatMessage> OnMessageReceived;
        public event Action<string> OnErrorOccurred;
        public event Action OnDisconnected;
        public event Action OnConnected;

        public string CurrentUsername { get; private set; }
        public string SessionToken { get; set; }
        public bool IsConnected => _isConnected && _tcpClient != null && _tcpClient.Connected;

        /// <summary>
        /// Kết nối bất đồng bộ tới Java Server qua IP và Port.
        /// </summary>
        public async Task<bool> ConnectAsync(string ip, int port)
        {
            try
            {
                Disconnect(); // Đóng kết nối cũ nếu có

                _tcpClient = new TcpClient();
                await _tcpClient.ConnectAsync(ip, port);
                _stream = _tcpClient.GetStream();
                _isConnected = true;
                _cts = new CancellationTokenSource();

                OnConnected?.Invoke();

                // Khởi chạy Receive Loop trên background Task để không làm treo UI
                _ = Task.Run(() => ReceiveLoopAsync(_cts.Token));

                return true;
            }
            catch (SocketException ex)
            {
                OnErrorOccurred?.Invoke($"Không thể kết nối tới Server ({ip}:{port}): {ex.Message}");
                Disconnect();
                return false;
            }
            catch (Exception ex)
            {
                OnErrorOccurred?.Invoke($"Lỗi kết nối: {ex.Message}");
                Disconnect();
                return false;
            }
        }

        /// <summary>
        /// Gửi yêu cầu đăng nhập (LOGIN) với username.
        /// </summary>
        public async Task SendLoginAsync(string username)
        {
            CurrentUsername = username;
            var packet = new ChatMessage
            {
                Action = "LOGIN",
                Sender = username,
                Content = "login_desktop"
            };
            await SendMessageAsync(packet);
        }

        /// <summary>
        /// Gửi tin nhắn chat cá nhân (CHAT_SINGLE) hoặc chat nhóm (CHAT_GROUP).
        /// </summary>
        public async Task SendChatMessageAsync(string receiver, string content)
        {
            bool isGroup = string.Equals(receiver, "ALL", StringComparison.OrdinalIgnoreCase);
            var packet = new ChatMessage
            {
                Action = isGroup ? "CHAT_GROUP" : "CHAT_SINGLE",
                Token = SessionToken,
                Sender = CurrentUsername,
                Receiver = receiver,
                Content = content
            };
            await SendMessageAsync(packet);
        }

        /// <summary>
        /// Gửi gói tin thông báo trạng thái gõ phím (TYPING).
        /// </summary>
        public async Task SendTypingAsync(string receiver, string state = "START")
        {
            var packet = new ChatMessage
            {
                Action = "TYPING",
                Token = SessionToken,
                Sender = CurrentUsername,
                Receiver = receiver,
                Content = state
            };
            await SendMessageAsync(packet);
        }

        /// <summary>
        /// Gửi gói tin đăng xuất (LOGOUT).
        /// </summary>
        public async Task SendLogoutAsync()
        {
            if (!IsConnected) return;
            var packet = new ChatMessage
            {
                Action = "LOGOUT",
                Token = SessionToken,
                Sender = CurrentUsername
            };
            await SendMessageAsync(packet);
        }

        /// <summary>
        /// Đóng gói JSON kèm ký tự kết thúc \n và gửi qua NetworkStream.
        /// </summary>
        public async Task SendMessageAsync(ChatMessage message)
        {
            if (!IsConnected || _stream == null) return;

            try
            {
                string json = message.ToJson();
                // TCP Framing: Mọi gói tin JSON đều phải kết thúc bằng ký tự '\n' (LF)
                string packetWithDelimiter = json + "\n";
                byte[] data = Encoding.UTF8.GetBytes(packetWithDelimiter);

                await _stream.WriteAsync(data, 0, data.Length);
                await _stream.FlushAsync();
            }
            catch (IOException ex)
            {
                OnErrorOccurred?.Invoke($"Lỗi đường truyền khi gửi: {ex.Message}");
                Disconnect();
            }
            catch (SocketException ex)
            {
                OnErrorOccurred?.Invoke($"Lỗi Socket khi gửi: {ex.Message}");
                Disconnect();
            }
            catch (Exception ex)
            {
                OnErrorOccurred?.Invoke($"Lỗi gửi gói tin: {ex.Message}");
            }
        }

        /// <summary>
        /// Vòng lặp nhận dữ liệu chạy nền (Background Receive Loop).
        /// Xử lý phân mảnh byte stream và dồn gói TCP bằng bộ đệm phân tách theo ký tự '\n'.
        /// Đảm bảo tính toàn vẹn của chuỗi UTF-8 tiếng Việt.
        /// </summary>
        private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
        {
            byte[] readBuffer = new byte[4096];
            List<byte> pendingBytes = new List<byte>();

            try
            {
                while (_isConnected && !cancellationToken.IsCancellationRequested)
                {
                    int bytesRead = await _stream.ReadAsync(readBuffer, 0, readBuffer.Length, cancellationToken);
                    
                    // Nếu ReadAsync trả về 0 nghĩa là Server đã chủ động đóng kết nối (Graceful shutdown)
                    if (bytesRead <= 0)
                    {
                        break;
                    }

                    // Tích lũy các byte vừa nhận được vào bộ đệm pendingBytes
                    for (int i = 0; i < bytesRead; i++)
                    {
                        pendingBytes.Add(readBuffer[i]);
                    }

                    // Quét và tách toàn bộ các gói tin kết thúc bằng '\n' (ASCII 10)
                    while (true)
                    {
                        int newlineIndex = pendingBytes.IndexOf((byte)'\n');
                        if (newlineIndex == -1)
                        {
                            // Chưa có ký tự '\n' đầy đủ trong bộ đệm -> chờ lần đọc tiếp theo (Xử lý phân mảnh TCP)
                            break;
                        }

                        // Lấy độ dài chuỗi byte loại trừ '\n' và ký tự '\r' nếu có
                        int packetLength = newlineIndex;
                        if (packetLength > 0 && pendingBytes[packetLength - 1] == (byte)'\r')
                        {
                            packetLength--;
                        }

                        byte[] packetBytes = pendingBytes.GetRange(0, packetLength).ToArray();
                        // Xóa gói đã xử lý và ký tự '\n' ra khỏi bộ đệm
                        pendingBytes.RemoveRange(0, newlineIndex + 1);

                        if (packetBytes.Length > 0)
                        {
                            try
                            {
                                string jsonStr = Encoding.UTF8.GetString(packetBytes);
                                ChatMessage msg = ChatMessage.FromJson(jsonStr);
                                if (msg != null)
                                {
                                    // Tự động lưu Token khi đăng nhập thành công
                                    if (msg.Action == "LOGIN_SUCCESS" && !string.IsNullOrEmpty(msg.Token))
                                    {
                                        SessionToken = msg.Token;
                                    }

                                    // Phát sự kiện nhận tin nhắn tới tầng trên
                                    OnMessageReceived?.Invoke(msg);
                                }
                            }
                            catch (Exception parseEx)
                            {
                                System.Diagnostics.Debug.WriteLine($"Lỗi giải mã JSON: {parseEx.Message}");
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Task bị hủy bình thường khi người dùng ngắt kết nối
            }
            catch (IOException ex)
            {
                if (_isConnected)
                {
                    OnErrorOccurred?.Invoke($"Mất kết nối với Server (IOException): {ex.Message}");
                }
            }
            catch (SocketException ex)
            {
                if (_isConnected)
                {
                    OnErrorOccurred?.Invoke($"Lỗi kết nối Socket: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                if (_isConnected)
                {
                    OnErrorOccurred?.Invoke($"Lỗi luồng nhận dữ liệu: {ex.Message}");
                }
            }
            finally
            {
                Disconnect();
            }
        }

        /// <summary>
        /// Ngắt kết nối và giải phóng tài nguyên mạng an toàn.
        /// </summary>
        public void Disconnect()
        {
            lock (_disconnectLock)
            {
                if (!_isConnected) return;
                _isConnected = false;

                try { _cts?.Cancel(); } catch { }
                try { _stream?.Close(); } catch { }
                try { _tcpClient?.Close(); } catch { }

                _cts = null;
                _stream = null;
                _tcpClient = null;

                OnDisconnected?.Invoke();
            }
        }
    }
}
