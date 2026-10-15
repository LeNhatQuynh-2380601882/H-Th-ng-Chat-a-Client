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
    /// Kỹ thuật: Delimiter-based Framing (\n), async/await, CancellationToken.
    /// Cải tiến v2: Thêm ValidateUsername, timeout kết nối 5s, auto-reconnect exponential backoff.
    /// </summary>
    public class TcpSocketClient
    {
        // ─── Cấu hình Auto-Reconnect ───────────────────────────────────────
        private const int MaxReconnectAttempts = 3;
        private const int ReconnectBaseDelayMs = 2000; // 2s → 4s → 8s (nhân đôi mỗi lần)

        // ─── Tài nguyên mạng ───────────────────────────────────────────────
        private TcpClient _tcpClient;
        private NetworkStream _stream;
        private CancellationTokenSource _cts;
        private bool _isConnected;
        private readonly object _disconnectLock = new object();

        // ─── Thông tin phiên (để reconnect) ────────────────────────────────
        private string _lastIp;
        private int _lastPort;
        private bool _autoReconnect = false; // Chỉ bật sau LOGIN_SUCCESS

        // ─── Sự kiện callback tới UI ───────────────────────────────────────
        public event Action<ChatMessage> OnMessageReceived;
        public event Action<string> OnErrorOccurred;
        public event Action OnDisconnected;
        public event Action OnConnected;
        public event Action<int> OnReconnecting; // Tham số: số lần thử hiện tại

        // ─── Thuộc tính trạng thái ─────────────────────────────────────────
        public string CurrentUsername { get; private set; }
        public string SessionToken { get; set; }
        public bool IsConnected => _isConnected && _tcpClient != null && _tcpClient.Connected;

        // ══════════════════════════════════════════════════════════════════
        // PHẦN 1: VALIDATION USERNAME
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Kiểm tra username hợp lệ theo quy tắc giao thức:
        /// - Độ dài 2-32 ký tự
        /// - KHÔNG chứa ký tự \n, \r, \t (sẽ phá vỡ TCP \n Framing)
        /// </summary>
        public static (bool isValid, string errorMsg) ValidateUsername(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                return (false, "Tên đăng nhập không được để trống!");

            username = username.Trim();

            if (username.Length < 2)
                return (false, "Tên đăng nhập phải có ít nhất 2 ký tự!");

            if (username.Length > 32)
                return (false, "Tên đăng nhập không được vượt quá 32 ký tự!");

            // Quan trọng: ký tự \n sẽ phá vỡ kỹ thuật Delimiter Framing của giao thức
            if (username.IndexOfAny(new char[] { '\n', '\r', '\t', '{', '}' }) >= 0)
                return (false, "Tên đăng nhập chứa ký tự không hợp lệ!");

            return (true, null);
        }

        // ══════════════════════════════════════════════════════════════════
        // PHẦN 2: KẾT NỐI
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Kết nối bất đồng bộ tới Java Server qua IP và Port.
        /// Timeout 5 giây để tránh treo UI khi Server không phản hồi.
        /// </summary>
        public async Task<bool> ConnectAsync(string ip, int port)
        {
            try
            {
                Disconnect(); // Đóng kết nối cũ nếu có

                // Lưu thông tin để dùng khi auto-reconnect
                _lastIp   = ip;
                _lastPort = port;

                _tcpClient = new TcpClient();

                // Timeout 5 giây: tránh treo UI khi Server không phản hồi
                var connectTask = _tcpClient.ConnectAsync(ip, port);
                if (await Task.WhenAny(connectTask, Task.Delay(5000)) != connectTask)
                    throw new SocketException(10060); // WSAETIMEDOUT
                await connectTask; // propagate exception nếu có lỗi

                _stream      = _tcpClient.GetStream();
                _isConnected = true;
                _cts         = new CancellationTokenSource();

                OnConnected?.Invoke();

                // Khởi chạy Receive Loop ở background — không block UI Thread
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

        // ══════════════════════════════════════════════════════════════════
        // PHẦN 3: GỬI GÓI TIN
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Gửi yêu cầu đăng nhập (LOGIN) với username và clientType = DESKTOP.
        /// </summary>
        public async Task SendLoginAsync(string username)
        {
            CurrentUsername = username;
            var packet = new ChatMessage
            {
                Action     = "LOGIN",
                Sender     = username,
                Content    = "login_desktop",
                ClientType = "DESKTOP"
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
        /// Gửi tệp tin hoặc hình ảnh được đóng gói Base64 qua gói tin FILE_TRANSFER.
        /// </summary>
        public async Task SendFileAsync(string receiver, string fileName, byte[] fileBytes, bool isImage)
        {
            var payload = new FilePayload
            {
                FileName = fileName,
                FileSize = fileBytes.Length,
                IsImage = isImage,
                Base64Data = Convert.ToBase64String(fileBytes)
            };

            var packet = new ChatMessage
            {
                Action = "FILE_TRANSFER",
                Token = SessionToken,
                Sender = CurrentUsername,
                Receiver = receiver,
                Content = payload.ToJson()
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
                // ── TCP Framing Rule ─────────────────────────────────────────────────
                // Mọi gói tin JSON PHẢI kết thúc bằng '\n' (LF, ASCII 0x0A).
                // BufferedReader.readLine() phía Java dùng '\n' làm delimiter.
                // ─────────────────────────────────────────────────────────────
                byte[] data = Encoding.UTF8.GetBytes(json + "\n");

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
                System.Diagnostics.Debug.WriteLine($"[TcpSocketClient] Lỗi gửi gói tin: {ex.Message}");
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // PHẦN 4: NHẬN DỮ LIỆU (Background Loop)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Vòng lặp nhận dữ liệu chạy ở background Task.
        /// Xử lý phân mảnh TCP (Fragmentation) và dồn gói (Coalescing) bằng bộ đệm byte.
        /// Ký tự '\n' (ASCII 10) là delimiter tách các gói JSON hoàn chỉnh.
        /// Đảm bảo tính toàn vẹn chuỗi UTF-8 kể cả tiếng Việt có dấu.
        /// </summary>
        private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
        {
            // Tăng buffer lên 8192 bytes: xử lý tốt hơn khi dồn nhiều gói
            byte[]     readBuffer   = new byte[8192];
            List<byte> pendingBytes = new List<byte>(8192);

            try
            {
                while (_isConnected && !cancellationToken.IsCancellationRequested)
                {
                    int bytesRead = await _stream.ReadAsync(readBuffer, 0, readBuffer.Length, cancellationToken);

                    // bytesRead == 0 → Server đóng kết nối gracefully (FIN packet)
                    if (bytesRead <= 0) break;

                    // Tích lũy bytes vào bộ đệm chờ xử lý
                    for (int i = 0; i < bytesRead; i++)
                        pendingBytes.Add(readBuffer[i]);

                    // Xử lý toàn bộ gói tin hoàn chỉnh trong bộ đệm
                    ProcessPendingBuffer(pendingBytes);
                }
            }
            catch (OperationCanceledException)
            {
                // Task bị hủy chủ động — hành vi bình thường khi Disconnect()
            }
            catch (IOException ex)
            {
                if (_isConnected)
                    OnErrorOccurred?.Invoke($"Mất kết nối với Server: {ex.Message}");
            }
            catch (SocketException ex)
            {
                if (_isConnected)
                    OnErrorOccurred?.Invoke($"Lỗi Socket: {ex.Message}");
            }
            catch (Exception ex)
            {
                if (_isConnected)
                    OnErrorOccurred?.Invoke($"Lỗi luồng nhận dữ liệu: {ex.Message}");
            }
            finally
            {
                bool wasConnected = _isConnected;
                Disconnect();

                // Mất kết nối bất ngờ (không phải logout chủ động) → thử reconnect
                if (wasConnected && _autoReconnect)
                    _ = Task.Run(() => TryReconnectAsync());
            }
        }

        /// <summary>
        /// Quét và tách toàn bộ gói JSON hoàn chỉnh từ bộ đệm byte dựa trên delimiter '\n'.
        /// Xử lý đúng cả TCP fragmentation lẫn coalescing (dồn gói).
        /// </summary>
        private void ProcessPendingBuffer(List<byte> pendingBytes)
        {
            while (true)
            {
                // Tìm ky tự '\n' (ASCII 10) — delimiter của giao thức
                int newlineIndex = pendingBytes.IndexOf((byte)'\n');
                if (newlineIndex == -1) break; // Chưa đủ gói — chờ bytes tiếp theo

                // Tính độ dài gói (loại bỏ '\r' nếu có — xử lý CRLF từ Windows)
                int packetLength = newlineIndex;
                if (packetLength > 0 && pendingBytes[packetLength - 1] == (byte)'\r')
                    packetLength--;

                if (packetLength > 0)
                {
                    byte[] packetBytes = pendingBytes.GetRange(0, packetLength).ToArray();
                    ParseAndDispatch(packetBytes);
                }

                // Xóa gói đã xử lý khỏi bộ đệm (kể cả '\n')
                pendingBytes.RemoveRange(0, newlineIndex + 1);
            }
        }

        /// <summary>
        /// Decode bytes → UTF-8 string → ChatMessage → phát sự kiện lên UI.
        /// </summary>
        private void ParseAndDispatch(byte[] packetBytes)
        {
            try
            {
                string jsonStr = Encoding.UTF8.GetString(packetBytes);
                ChatMessage msg = ChatMessage.FromJson(jsonStr);
                if (msg == null) return;

                // Bật auto-reconnect sau khi xác thực thành công
                if (msg.Action == "LOGIN_SUCCESS" && !string.IsNullOrEmpty(msg.Token))
                {
                    SessionToken   = msg.Token;
                    _autoReconnect = true;
                }

                OnMessageReceived?.Invoke(msg);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TcpSocketClient] Lỗi parse JSON: {ex.Message}");
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // PHẦN 5: AUTO-RECONNECT (Exponential Backoff)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Thử kết nối lại sau khi mất kết nối bất ngờ.
        /// Chiến lược Exponential Backoff: 2s → 4s → 8s (tối đa 3 lần).
        /// Nếu thất bại hết, thông báo UI để người dùng kết nối thủ công.
        /// </summary>
        private async Task TryReconnectAsync()
        {
            for (int attempt = 1; attempt <= MaxReconnectAttempts; attempt++)
            {
                OnReconnecting?.Invoke(attempt); // Thông báo UI: đang thử lần thứ N

                // Exponential backoff: 2s, 4s, 8s
                int delayMs = ReconnectBaseDelayMs * (int)Math.Pow(2, attempt - 1);
                await Task.Delay(delayMs);

                bool success = await ConnectAsync(_lastIp, _lastPort);
                if (success)
                {
                    // Kết nối lại thành công → đăng nhập lại với username cũ
                    await SendLoginAsync(CurrentUsername);
                    return;
                }
            }

            // Hết lần thử → thông báo UI
            _autoReconnect = false;
            OnErrorOccurred?.Invoke(
                $"Không thể kết nối lại sau {MaxReconnectAttempts} lần thử. " +
                "Vui lòng kiểm tra Server.");
        }

        // ══════════════════════════════════════════════════════════════════
        // PHẦN 6: NGẮT KẾT NỐI
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Ngắt kết nối và giải phóng toàn bộ tài nguyên mạng một cách an toàn.
        /// Thread-safe nhờ lock(_disconnectLock) — tránh race condition khi gọi đồng thời
        /// từ ReceiveLoopAsync (background thread) và UI thread.
        /// </summary>
        public void Disconnect()
        {
            lock (_disconnectLock)
            {
                if (!_isConnected) return;
                _isConnected = false;

                try { _cts?.Cancel(); }       catch { }
                try { _stream?.Close(); }     catch { }
                try { _tcpClient?.Close(); }  catch { }

                _cts       = null;
                _stream    = null;
                _tcpClient = null;

                OnDisconnected?.Invoke();
            }
        }
    }
}
