using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Media;
using System.Windows.Forms;
using ChatDesktopApp.Models;
using ChatDesktopApp.Services;
using Newtonsoft.Json;

namespace ChatDesktopApp.Forms
{
    public partial class MainForm : Form
    {
        private readonly TcpSocketClient _socketClient;
        private readonly LocalStorageService _storageService;
        private string _targetUser = "ALL"; // Mặc định là chat nhóm (Broadcast)

        // Lưu trữ lịch sử tin nhắn tách biệt theo từng user/kênh ("ALL", "Alice", "Bob"...)
        private readonly Dictionary<string, List<ChatMessage>> _chatHistory;

        // Số tin nhắn chưa đọc đối với các user không đang được active
        private readonly Dictionary<string, int> _unreadCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        // Danh sách toàn bộ user đang online nhận từ USER_LIST để phục vụ tìm kiếm/lọc
        private readonly List<string> _allOnlineUsers = new List<string>();

        // Cache lưu các file đính kèm đã nhận để người dùng có thể click tải/lưu: Key = downloadToken
        private readonly Dictionary<string, FilePayload> _receivedFiles = new Dictionary<string, FilePayload>();

        // Timer ẩn nhãn thông báo "User đang nhập..."
        private readonly System.Windows.Forms.Timer _typingDisplayTimer;

        // Timer debounce kiểm soát việc gửi gói tin TYPING
        private readonly System.Windows.Forms.Timer _typingDebounceTimer;
        private bool _isCurrentlyTyping = false;

        // Timer tự động lưu lịch sử mỗi 60 giây, tránh mất dữ liệu khi ứng dụng bị crash
        private readonly System.Windows.Forms.Timer _autoSaveTimer;

        // Quản lý tải tệp: chống click lặp và lưu token menu ngữ cảnh
        private long _lastDownloadClickTicks = 0;
        private string _activeContextMenuDownloadToken = null;

        public MainForm(TcpSocketClient socketClient)
        {
            InitializeComponent();
            this.DoubleBuffered = true;
            _socketClient = socketClient;
            SetupModernStyles();

            // 6. Nạp lịch sử chat cục bộ (Local Storage)
            _storageService = new LocalStorageService(_socketClient.CurrentUsername);
            _chatHistory = _storageService.LoadHistory();
            if (!_chatHistory.ContainsKey("ALL"))
            {
                _chatHistory["ALL"] = new List<ChatMessage>();
            }

            // Gán icon mặc định cho NotifyIcon từ Form icon
            try
            {
                notifyIconApp.Icon = this.Icon ?? SystemIcons.Application;
            }
            catch { }

            // Đăng ký sự kiện từ TcpSocketClient
            _socketClient.OnMessageReceived += HandleMessageReceived;
            _socketClient.OnDisconnected    += HandleDisconnected;
            _socketClient.OnErrorOccurred   += HandleNetworkError;
            _socketClient.OnReconnecting    += HandleReconnecting;
            _socketClient.OnConnected       += HandleConnected;
            _socketClient.OnLoginSuccess    += HandleLoginSuccess;
            _socketClient.OnLoginFailed     += HandleLoginFailed;

            // Cập nhật thông tin người dùng hiện tại
            lblCurrentUser.Text = $"👤 Bạn: {_socketClient.CurrentUsername}";
            UpdateTargetHeader();

            // Khởi tạo danh sách người dùng ban đầu
            lstUsers.Items.Clear();
            lstUsers.Items.Add("💬 TẤT CẢ (ALL)");
            lstUsers.SelectedIndex = 0;

            // Nạp lịch sử cũ của kênh ALL lên giao diện
            RenderChatLogForTarget("ALL");

            // Cấu hình Timer hiển thị typing của đối phương (tự tắt sau 2.5s)
            _typingDisplayTimer = new System.Windows.Forms.Timer { Interval = 2500 };
            _typingDisplayTimer.Tick += (s, e) =>
            {
                lblTyping.Text = "";
                _typingDisplayTimer.Stop();
            };

            // Cấu hình Timer debounce gửi trạng thái typing của chính mình (1.5s ngừng gõ -> gửi STOP)
            _typingDebounceTimer = new System.Windows.Forms.Timer { Interval = 1500 };
            _typingDebounceTimer.Tick += async (s, e) =>
            {
                _typingDebounceTimer.Stop();
                if (_isCurrentlyTyping)
                {
                    _isCurrentlyTyping = false;
                    await _socketClient.SendTypingAsync(_targetUser, "STOP");
                }
            };

            // Cấu hình Timer tự động lưu lịch sử (mỗi 60 giây)
            _autoSaveTimer = new System.Windows.Forms.Timer { Interval = 60_000 };
            _autoSaveTimer.Tick += (s, e) =>
            {
                try { _storageService.SaveHistory(_chatHistory); }
                catch { }
            };
            _autoSaveTimer.Start();

            AppendSystemMessage("Chào mừng bạn đến với Hệ Thống Chat Đa Nền Tảng!");
            AppendSystemMessage("▶ Chọn một người dùng bên trái để chat 1-1, hoặc chọn 'TẤT CẢ (ALL)' để chat nhóm.");
            AppendSystemMessage($"🛡️ Session Token: {_socketClient.SessionToken?.Substring(0, 8)}...");
        }

        private void SetupModernStyles()
        {
            AttachFocusEffect(txtSearchUser, Color.FromArgb(15, 23, 42), Color.FromArgb(30, 41, 59));
            AttachFocusEffect(txtSearchMessage, Color.FromArgb(15, 23, 42), Color.FromArgb(30, 41, 59));
            AttachFocusEffect(txtInputMessage, Color.FromArgb(30, 41, 59), Color.FromArgb(40, 53, 76));
        }

        private void AttachFocusEffect(TextBox control, Color normalColor, Color focusColor)
        {
            control.GotFocus += (s, e) => control.BackColor = focusColor;
            control.LostFocus += (s, e) => control.BackColor = normalColor;
        }

        #region Xử lý Gói tin nhận từ Server (UI Thread-Safe)

        private void HandleMessageReceived(ChatMessage msg)
        {
            if (IsDisposed || Disposing) return;
            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke(new Action(() => HandleMessageReceived(msg)));
                }
                catch (ObjectDisposedException) { }
                catch (InvalidOperationException) { }
                return;
            }

            switch (msg.Action)
            {
                case "USER_LIST":
                    UpdateOnlineUserList(msg.Content);
                    break;

                case "CHAT_GROUP":
                case "CHAT_SINGLE":
                    ProcessIncomingMessage(msg);
                    break;

                case "FILE_TRANSFER":
                    ProcessIncomingFileMessage(msg);
                    break;

                case "TYPING":
                    HandleTypingIndicator(msg);
                    break;

                case "LOGIN_SUCCESS":
                    HandleLoginSuccess();
                    break;

                case "LOGIN_FAILED":
                    HandleLoginFailed(msg.Content);
                    break;

                case "ERROR":
                    AppendSystemMessage($"LỖI TỪ SERVER: {msg.Content}");
                    break;

                case "SYSTEM":
                    AppendSystemMessage(msg.Content);
                    break;
            }
        }

        /// <summary>
        /// 2. Phân loại tin nhắn vào từng tab/user tương ứng, tăng Unread badge và phát âm thanh/thông báo
        /// </summary>
        private void ProcessIncomingMessage(ChatMessage msg)
        {
            bool isMe = string.Equals(msg.Sender, _socketClient.CurrentUsername, StringComparison.OrdinalIgnoreCase);
            string conversationKey;

            if (msg.Action == "CHAT_GROUP" || string.Equals(msg.Receiver, "ALL", StringComparison.OrdinalIgnoreCase))
            {
                // Đối với file gửi nhóm, vì bên gửi đã tự lưu & render trong btnSendFile nên bỏ qua gói echo
                if (isMe && msg.Action == "FILE_TRANSFER") return;
                conversationKey = "ALL";
            }
            else
            {
                // Tin nhắn 1-1: nếu người khác gửi thì key là Sender, nếu mình gửi thì key là Receiver
                conversationKey = isMe ? msg.Receiver : msg.Sender;
            }

            // Lưu vào kho lịch sử của kênh/user đó
            if (!_chatHistory.ContainsKey(conversationKey))
            {
                _chatHistory[conversationKey] = new List<ChatMessage>();
            }
            _chatHistory[conversationKey].Add(msg);

            // Nếu tin nhắn thuộc về cuộc hội thoại đang mở -> render ngay lên màn hình
            if (string.Equals(conversationKey, _targetUser, StringComparison.OrdinalIgnoreCase))
            {
                AppendChatMessageToView(msg);
            }
            else
            {
                // Tăng unread badge cho tab đó
                if (!isMe)
                {
                    if (!_unreadCounts.ContainsKey(conversationKey)) _unreadCounts[conversationKey] = 0;
                    _unreadCounts[conversationKey]++;
                    RefreshUserListUI();
                }
            }

            // 4. Phát âm thanh và hiện thông báo Balloon nếu không phải tin của chính mình
            if (!isMe)
            {
                PlayNotificationSound();
                ShowNotificationBanner(msg.Sender, msg.Content);
            }
        }

        /// <summary>
        /// 1. Xử lý gói tin nhận File/Hình ảnh (FILE_TRANSFER)
        /// </summary>
        private void ProcessIncomingFileMessage(ChatMessage msg)
        {
            var payload = FilePayload.FromJson(msg.Content);
            if (payload == null || string.IsNullOrWhiteSpace(payload.FileName) || string.IsNullOrWhiteSpace(payload.Base64Data))
                return;

            // Kiểm tra an toàn dung lượng khai báo (từ chối giá trị âm hoặc > 15MB)
            if (payload.FileSize < 0 || payload.FileSize > 15 * 1024 * 1024)
                return;

            // Khử độc tên file hiển thị (chống Path Traversal và ký tự đặc biệt)
            payload.FileName = SanitizeFileName(payload.FileName);

            string tokenKey = $"download://{Guid.NewGuid().ToString("N").Substring(0, 8)}";
            _receivedFiles[tokenKey] = payload;

            // Định dạng thông báo nội dung file để hiển thị và lưu lịch sử
            string fileTypeIcon = payload.IsImage ? "🖼️ [HÌNH ẢNH]" : "📁 [TỆP TIN]";
            string fileSizeStr = FormatFileSize(payload.FileSize);
            string displayContent = $"{fileTypeIcon} {payload.FileName} ({fileSizeStr})\n👉 Bấm vào liên kết để lưu file: {tokenKey}";

            var displayMsg = new ChatMessage
            {
                Action = msg.Action,
                Sender = msg.Sender,
                Receiver = msg.Receiver,
                Content = displayContent,
                Timestamp = msg.Timestamp
            };

            ProcessIncomingMessage(displayMsg);
        }

        /// <summary>
        /// Cập nhật danh sách người dùng online từ gói tin USER_LIST
        /// </summary>
        private void UpdateOnlineUserList(string userListJson)
        {
            try
            {
                var users = JsonConvert.DeserializeObject<List<string>>(userListJson) ?? new List<string>();

                _allOnlineUsers.Clear();
                foreach (var u in users)
                {
                    if (!string.Equals(u, _socketClient.CurrentUsername, StringComparison.OrdinalIgnoreCase))
                    {
                        _allOnlineUsers.Add(u);
                    }
                }

                lblOnlineHeader.Text = $"DANH SÁCH ONLINE ({_allOnlineUsers.Count + 1}):";
                RefreshUserListUI();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Lỗi phân tích USER_LIST: {ex.Message}");
            }
        }

        /// <summary>
        /// 5. Làm mới danh sách hiển thị có hỗ trợ Lọc tìm kiếm và hiển thị Unread Badge
        /// </summary>
        private void RefreshUserListUI()
        {
            string searchKeyword = txtSearchUser.Text.Trim().ToLowerInvariant();
            string previouslySelected = _targetUser;

            lstUsers.BeginUpdate();
            lstUsers.Items.Clear();

            // Mục TẤT CẢ (ALL)
            int unreadAll = _unreadCounts.ContainsKey("ALL") ? _unreadCounts["ALL"] : 0;
            string allTitle = unreadAll > 0 ? $"💬 TẤT CẢ (ALL)  ({unreadAll})" : "💬 TẤT CẢ (ALL)";
            lstUsers.Items.Add(allTitle);

            foreach (var user in _allOnlineUsers)
            {
                if (string.IsNullOrEmpty(searchKeyword) || user.ToLowerInvariant().Contains(searchKeyword))
                {
                    int unread = _unreadCounts.ContainsKey(user) ? _unreadCounts[user] : 0;
                    string userTitle = unread > 0 ? $"👤 {user}  ({unread})" : $"👤 {user}";
                    lstUsers.Items.Add(userTitle);
                }
            }

            // Giữ lại vị trí đã chọn
            bool restored = false;
            for (int i = 0; i < lstUsers.Items.Count; i++)
            {
                string itemText = lstUsers.Items[i].ToString();
                if (ExtractRawUsername(itemText).Equals(previouslySelected, StringComparison.OrdinalIgnoreCase))
                {
                    lstUsers.SelectedIndex = i;
                    restored = true;
                    break;
                }
            }

            if (!restored && lstUsers.Items.Count > 0)
            {
                lstUsers.SelectedIndex = 0;
            }

            lstUsers.EndUpdate();
        }

        private string ExtractRawUsername(string itemText)
        {
            if (string.IsNullOrEmpty(itemText)) return "ALL";
            if (itemText.StartsWith("💬 TẤT CẢ")) return "ALL";

            // Bỏ tiền tố "👤 " và phần badge " (x)"
            string name = itemText.Replace("👤 ", "").Trim();
            int parenIdx = name.IndexOf(" (");
            if (parenIdx > 0)
            {
                name = name.Substring(0, parenIdx).Trim();
            }
            return name;
        }

        /// <summary>
        /// Hiển thị thông báo trạng thái "User đang nhập..."
        /// </summary>
        private void HandleTypingIndicator(ChatMessage msg)
        {
            if (string.Equals(msg.Sender, _socketClient.CurrentUsername, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            bool isRelevant;
            if (_targetUser.Equals("ALL", StringComparison.OrdinalIgnoreCase))
            {
                isRelevant = string.Equals(msg.Receiver, "ALL", StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                isRelevant = string.Equals(msg.Sender, _targetUser, StringComparison.OrdinalIgnoreCase) &&
                             string.Equals(msg.Receiver, _socketClient.CurrentUsername, StringComparison.OrdinalIgnoreCase);
            }

            if (!isRelevant) return;

            string state = msg.Content ?? "";
            if (state.Equals("STOP", StringComparison.OrdinalIgnoreCase))
            {
                lblTyping.Text = "";
                _typingDisplayTimer.Stop();
            }
            else
            {
                lblTyping.Text = $"✍️ {msg.Sender} đang nhập tin nhắn...";
                _typingDisplayTimer.Stop();
                _typingDisplayTimer.Start();
            }
        }

        /// <summary>
        /// 2. Nạp lại toàn bộ lịch sử tin nhắn của kênh/người dùng hiện tại
        /// </summary>
        private void RenderChatLogForTarget(string targetKey)
        {
            txtChatLog.Clear();
            if (!_chatHistory.ContainsKey(targetKey)) return;

            var list = _chatHistory[targetKey];
            foreach (var m in list)
            {
                AppendChatMessageToView(m);
            }
        }

        /// <summary>
        /// In 1 tin nhắn lên txtChatLog
        /// </summary>
        private void AppendChatMessageToView(ChatMessage msg)
        {
            string timeStr = DateTimeOffset.FromUnixTimeMilliseconds(msg.Timestamp).ToLocalTime().ToString("HH:mm:ss");
            bool isMe = string.Equals(msg.Sender, _socketClient.CurrentUsername, StringComparison.OrdinalIgnoreCase);

            Color headerColor;
            string prefix;

            if (msg.Action == "CHAT_GROUP")
            {
                prefix = "[NHÓM]";
                headerColor = Color.FromArgb(96, 165, 250); // Sky Blue #60A5FA
            }
            else if (msg.Action == "FILE_TRANSFER")
            {
                prefix = "[FILE]";
                headerColor = Color.FromArgb(45, 212, 191); // Teal #2DD4BF
            }
            else
            {
                prefix = "[CÁ NHÂN]";
                headerColor = Color.FromArgb(192, 132, 252); // Soft Violet #C084FC
            }

            string senderDisplay = isMe ? "Bạn" : msg.Sender;
            if (!isMe && msg.Action == "CHAT_SINGLE")
            {
                senderDisplay = $"{msg.Sender} -> Bạn";
            }

            txtChatLog.SelectionStart = txtChatLog.TextLength;
            txtChatLog.SelectionLength = 0;

            // In timestamp và tiền tố
            txtChatLog.SelectionColor = Color.FromArgb(148, 163, 184); // Slate #94A3B8
            txtChatLog.AppendText($"[{timeStr}] ");

            // Dùng using để giải phóng Font ngay sau khi dùng, tránh rò rỉ GDI handle
            using (var boldFont = new Font(txtChatLog.Font, FontStyle.Bold))
            {
                txtChatLog.SelectionColor = headerColor;
                txtChatLog.SelectionFont = boldFont;
                txtChatLog.AppendText($"{prefix} {senderDisplay}: ");
            }

            // In nội dung tin nhắn
            string content = msg.Content ?? "";
            if (content.StartsWith("💬 [Trả lời"))
            {
                int newlineIdx = content.IndexOf('\n');
                if (newlineIdx > 0)
                {
                    string quoteHeader = content.Substring(0, newlineIdx).Trim();
                    string actualMessage = content.Substring(newlineIdx + 1);

                    // Xuống dòng và in phần trích dẫn với lề thụt vào và màu xám xanh nghiêng
                    txtChatLog.AppendText(Environment.NewLine);
                    using (var italicFont = new Font(txtChatLog.Font, FontStyle.Italic))
                    {
                        txtChatLog.SelectionColor = Color.FromArgb(148, 163, 184); // Slate #94A3B8
                        txtChatLog.SelectionFont = italicFont;
                        txtChatLog.AppendText($"   {quoteHeader}{Environment.NewLine}");
                    }

                    // In nội dung trả lời chính
                    using (var regularFont = new Font(txtChatLog.Font, FontStyle.Regular))
                    {
                        txtChatLog.SelectionColor = Color.FromArgb(241, 245, 249); // White #F1F5F9
                        txtChatLog.SelectionFont = regularFont;
                        txtChatLog.AppendText($"   👉 {actualMessage}{Environment.NewLine}");
                    }
                }
                else
                {
                    using (var regularFont = new Font(txtChatLog.Font, FontStyle.Regular))
                    {
                        txtChatLog.SelectionColor = Color.FromArgb(241, 245, 249); // White #F1F5F9
                        txtChatLog.SelectionFont = regularFont;
                        txtChatLog.AppendText($"{content}{Environment.NewLine}");
                    }
                }
            }
            else
            {
                using (var regularFont = new Font(txtChatLog.Font, FontStyle.Regular))
                {
                    txtChatLog.SelectionColor = Color.FromArgb(241, 245, 249); // White #F1F5F9
                    txtChatLog.SelectionFont = regularFont;
                    txtChatLog.AppendText($"{content}{Environment.NewLine}");
                }
            }

            txtChatLog.ScrollToCaret();
        }

        private void AppendSystemMessage(string content)
        {
            if (IsDisposed || Disposing) return;
            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke(new Action(() => AppendSystemMessage(content)));
                }
                catch (ObjectDisposedException) { }
                catch (InvalidOperationException) { }
                return;
            }

            string timeStr = DateTime.Now.ToString("HH:mm:ss");

            txtChatLog.SelectionStart = txtChatLog.TextLength;
            txtChatLog.SelectionLength = 0;

            txtChatLog.SelectionColor = Color.FromArgb(251, 191, 36); // Amber #FBBF24
            txtChatLog.SelectionFont = new Font(txtChatLog.Font, FontStyle.Italic);
            txtChatLog.AppendText($"[{timeStr}] [HỆ THỐNG] {content}{Environment.NewLine}");

            txtChatLog.ScrollToCaret();
        }

        #endregion

        #region Tương tác Người dùng & Gửi Tin nhắn

        private async void btnSend_Click(object sender, EventArgs e)
        {
            string content = txtInputMessage.Text.Trim();
            if (string.IsNullOrEmpty(content)) return;

            if (!_socketClient.IsConnected || !_socketClient.IsAuthenticated)
            {
                MessageBox.Show("Mất kết nối hoặc phiên làm việc chưa được xác thực! Không thể gửi tin nhắn.", "Lỗi kết nối", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Gắn trích dẫn Reply nếu đang ở chế độ trả lời tin nhắn
            if (!string.IsNullOrEmpty(_replySender) && !string.IsNullOrEmpty(_replyQuote))
            {
                string replyHeader = $"💬 [Trả lời @{_replySender}: \"{_replyQuote}\"]";
                content = $"{replyHeader}\n{content}";
                CancelReply();
            }

            if (_isCurrentlyTyping)
            {
                _typingDebounceTimer.Stop();
                _isCurrentlyTyping = false;
                await _socketClient.SendTypingAsync(_targetUser, "STOP");
            }

            await _socketClient.SendChatMessageAsync(_targetUser, content);

            // Tin nhắn cá nhân: server chỉ gửi người nhận, tự lưu và render phía người gửi
            if (_targetUser != "ALL")
            {
                var selfMsg = new ChatMessage
                {
                    Action = "CHAT_SINGLE",
                    Sender = _socketClient.CurrentUsername,
                    Receiver = _targetUser,
                    Content = content,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };

                if (!_chatHistory.ContainsKey(_targetUser))
                {
                    _chatHistory[_targetUser] = new List<ChatMessage>();
                }
                _chatHistory[_targetUser].Add(selfMsg);
                AppendChatMessageToView(selfMsg);
            }

            txtInputMessage.Clear();
            txtInputMessage.Focus();
        }

        /// <summary>
        /// 1. Nút Gửi File / Hình ảnh với kiểm tra an toàn toàn diện (Giai đoạn 2A)
        /// </summary>
        private async void btnSendFile_Click(object sender, EventArgs e)
        {
            if (!_socketClient.IsConnected || !_socketClient.IsAuthenticated)
            {
                MessageBox.Show("Mất kết nối hoặc phiên làm việc chưa được xác thực! Không thể gửi file.", "Lỗi kết nối", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (var ofd = new OpenFileDialog())
            {
                ofd.Title = "Chọn tệp tin hoặc hình ảnh để gửi";
                ofd.Filter = "Tất cả các tệp (*.*)|*.*|Hình ảnh (*.png;*.jpg;*.jpeg;*.gif;*.bmp)|*.png;*.jpg;*.jpeg;*.gif;*.bmp|Tài liệu (*.pdf;*.txt;*.docx;*.xlsx;*.zip;*.rar)|*.pdf;*.txt;*.docx;*.xlsx;*.zip;*.rar";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        if (!File.Exists(ofd.FileName))
                        {
                            MessageBox.Show("Tệp tin được chọn không tồn tại hoặc đã bị xóa!", "Lỗi tệp tin", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }

                        var fileInfo = new FileInfo(ofd.FileName);

                        // 1. Kiểm tra tệp rỗng
                        if (fileInfo.Length <= 0)
                        {
                            MessageBox.Show("Không thể gửi tệp tin rỗng (0 bytes)!", "Cảnh báo tệp rỗng", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }

                        // 2. Giới hạn 5MB cho socket JSON
                        if (fileInfo.Length > 5 * 1024 * 1024)
                        {
                            MessageBox.Show("Dung lượng tệp tin vượt quá 5MB. Vui lòng chọn tệp nhỏ hơn!", "Cảnh báo kích thước", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }

                        // 3. Kiểm tra phần mở rộng tệp nguy hiểm (Chặn thực thi mã độc)
                        string ext = fileInfo.Extension.ToLowerInvariant();
                        if (DangerousExtensions.Contains(ext))
                        {
                            MessageBox.Show(
                                $"Vì lý do an toàn, hệ sinh thái chat từ chối gửi tệp có định dạng thực thi nguy hiểm ('{ext}').\n\nVui lòng nén tệp tin hoặc sử dụng định dạng tài liệu an toàn (ảnh, txt, pdf, docx, zip...).",
                                "Cảnh báo bảo mật tệp tin",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Warning);
                            return;
                        }

                        byte[] bytes = File.ReadAllBytes(ofd.FileName);
                        bool isImage = ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".gif" || ext == ".bmp";
                        string safeFileName = SanitizeFileName(fileInfo.Name);

                        await _socketClient.SendFileAsync(_targetUser, safeFileName, bytes, isImage);

                        // Tự lưu và hiển thị cho chính mình
                        string tokenKey = $"download://{Guid.NewGuid().ToString("N").Substring(0, 8)}";
                        _receivedFiles[tokenKey] = new FilePayload
                        {
                            FileName = safeFileName,
                            FileSize = bytes.Length,
                            IsImage = isImage,
                            Base64Data = Convert.ToBase64String(bytes)
                        };

                        string fileTypeIcon = isImage ? "🖼️ [HÌNH ẢNH]" : "📁 [TỆP TIN]";
                        string fileSizeStr = FormatFileSize(bytes.Length);
                        string displayContent = $"{fileTypeIcon} {safeFileName} ({fileSizeStr})\n👉 Bấm vào liên kết để lưu file: {tokenKey}";

                        var selfMsg = new ChatMessage
                        {
                            Action = "FILE_TRANSFER",
                            Sender = _socketClient.CurrentUsername,
                            Receiver = _targetUser,
                            Content = displayContent,
                            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                        };

                        string key = _targetUser;
                        if (!_chatHistory.ContainsKey(key)) _chatHistory[key] = new List<ChatMessage>();
                        _chatHistory[key].Add(selfMsg);
                        AppendChatMessageToView(selfMsg);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Lỗi khi đọc file: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        /// <summary>
        /// 3. Bộ chọn Emoji Popup với font Segoe UI Emoji sắc nét
        /// </summary>
        private void btnEmoji_Click(object sender, EventArgs e)
        {
            var menu = new ContextMenuStrip();
            menu.Font = new Font("Segoe UI Emoji", 13F, FontStyle.Regular);
            menu.ShowImageMargin = false;
            menu.BackColor = Color.FromArgb(30, 41, 59);
            menu.ForeColor = Color.White;

            string[] emojis = { "😀", "😂", "😍", "👍", "❤️", "🎉", "🔥", "👋", "👏", "⭐", "😎", "🥳" };

            foreach (var emo in emojis)
            {
                var item = menu.Items.Add(emo);
                item.Click += (s, ev) =>
                {
                    txtInputMessage.SelectedText = emo;
                    txtInputMessage.Focus();
                };
            }

            // ContextMenuStrip.Height = 0 trước khi được render lần đầu.
            // Dùng tọa độ màn hình của cạnh trên nút để định vị đúng phía trên nút Emoji.
            var screenPos = btnEmoji.PointToScreen(new Point(0, 0));
            menu.Show(screenPos.X, screenPos.Y);
        }

        /// <summary>
        /// 3. Xử lý click vào link trong RichTextBox (mở web hoặc tải file an toàn - Giai đoạn 2A)
        /// </summary>
        private void txtChatLog_LinkClicked(object sender, LinkClickedEventArgs e)
        {
            string url = e.LinkText;
            if (url.StartsWith("download://"))
            {
                if (_receivedFiles.TryGetValue(url, out var payload))
                {
                    if (payload == null || string.IsNullOrWhiteSpace(payload.Base64Data))
                    {
                        MessageBox.Show("Dữ liệu tệp đính kèm không khả dụng hoặc đã bị xóa.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    // payload hợp lệ → tiếp tục xử lý bên dưới

                    string safeDefaultName = SanitizeFileName(payload.FileName);
                    using (var sfd = new SaveFileDialog())
                    {
                        sfd.Title = "Lưu tệp tin đính kèm";
                        sfd.FileName = safeDefaultName;
                        sfd.Filter = "Tất cả các tệp (*.*)|*.*";
                        sfd.OverwritePrompt = false; // Tự kiểm soát ghi đè để có tùy chọn tự đổi tên an toàn

                        if (sfd.ShowDialog() == DialogResult.OK)
                        {
                            try
                            {
                                string targetDir = Path.GetDirectoryName(sfd.FileName);
                                if (string.IsNullOrEmpty(targetDir) || !Directory.Exists(targetDir))
                                {
                                    MessageBox.Show("Thư mục lưu trữ không hợp lệ hoặc không tồn tại!", "Lỗi đường dẫn", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                    return;
                                }

                                // 1. Xác thực và giải mã Base64 an toàn bằng khối try-catch
                                byte[] fileBytes;
                                try
                                {
                                    fileBytes = Convert.FromBase64String(payload.Base64Data);
                                }
                                catch (FormatException ex)
                                {
                                    MessageBox.Show($"Dữ liệu tệp không đúng định dạng Base64 hợp lệ: {ex.Message}", "Lỗi dữ liệu", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                    return;
                                }

                                if (fileBytes == null || fileBytes.Length == 0)
                                {
                                    MessageBox.Show("Tệp tin nhận được không có dữ liệu (0 bytes).", "Cảnh báo tệp rỗng", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                    return;
                                }

                                // 2. Chống ghi đè mất file: hỏi xác nhận hoặc tự đổi tên _(1), _(2)
                                string destinationPath = sfd.FileName;
                                if (File.Exists(destinationPath))
                                {
                                    var overwriteChoice = MessageBox.Show(
                                        $"Tệp '{Path.GetFileName(destinationPath)}' đã tồn tại trong thư mục đích.\n\n- Chọn 'Yes' để ghi đè tệp cũ.\n- Chọn 'No' để tự động tạo tên mới (không ghi đè).\n- Chọn 'Cancel' để hủy lưu.",
                                        "Xác nhận lưu tệp",
                                        MessageBoxButtons.YesNoCancel,
                                        MessageBoxIcon.Question);

                                    if (overwriteChoice == DialogResult.Cancel)
                                    {
                                        return;
                                    }
                                    else if (overwriteChoice == DialogResult.No)
                                    {
                                        destinationPath = GetUniqueFilePath(destinationPath);
                                    }
                                }

                                File.WriteAllBytes(destinationPath, fileBytes);
                                MessageBox.Show($"Đã lưu tệp tin thành công tại:\n{destinationPath}", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                            catch (UnauthorizedAccessException ex)
                            {
                                MessageBox.Show($"Không có quyền ghi vào thư mục được chọn: {ex.Message}", "Lỗi phân quyền", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show($"Lỗi khi lưu tệp: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }
                    }
                }
            }
            else if (url.Contains("download://"))
            {
                // Link download:// từ lịch sử được nạp lại sau khi khởi động app lại
                // _receivedFiles chỉ tồn tại trong phiên hiện tại → thông báo rõ ràng
                MessageBox.Show(
                    "Tệp tin này thuộc phiên làm việc cũ.\n\nDữ liệu tệp không được lưu vào ổ đĩa (chỉ giữ trong RAM trong lúc chạy).\nBạn cần yêu cầu người gửi gửi lại tệp.",
                    "Tệp tin không khả dụng",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            else
            {
                try
                {
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Không thể mở liên kết: {ex.Message}");
                }
            }
        }

        private void txtInputMessage_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape && pnlReply.Visible)
            {
                e.SuppressKeyPress = true;
                CancelReply();
                return;
            }

            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                btnSend.PerformClick();
            }
        }

        private async void txtInputMessage_TextChanged(object sender, EventArgs e)
        {
            if (!_socketClient.IsConnected || !_socketClient.IsAuthenticated) return;

            if (txtInputMessage.Text.Length > 0)
            {
                if (!_isCurrentlyTyping)
                {
                    _isCurrentlyTyping = true;
                    await _socketClient.SendTypingAsync(_targetUser, "START");
                }
                _typingDebounceTimer.Stop();
                _typingDebounceTimer.Start();
            }
            else
            {
                if (_isCurrentlyTyping)
                {
                    _typingDebounceTimer.Stop();
                    _isCurrentlyTyping = false;
                    await _socketClient.SendTypingAsync(_targetUser, "STOP");
                }
            }
        }

        /// <summary>
        /// 2. Khi chọn user khác, chuyển kênh chat và xóa unread badge
        /// </summary>
        private async void lstUsers_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lstUsers.SelectedItem != null)
            {
                string rawUser = ExtractRawUsername(lstUsers.SelectedItem.ToString());
                if (!string.Equals(rawUser, _targetUser, StringComparison.OrdinalIgnoreCase))
                {
                    if (_isCurrentlyTyping)
                    {
                        _typingDebounceTimer.Stop();
                        _isCurrentlyTyping = false;
                        await _socketClient.SendTypingAsync(_targetUser, "STOP");
                    }
                    lblTyping.Text = "";
                    _typingDisplayTimer.Stop();
                }

                _targetUser = rawUser;

                // Xóa unread badge của user này
                if (_unreadCounts.ContainsKey(_targetUser))
                {
                    _unreadCounts[_targetUser] = 0;
                    RefreshUserListUI();
                }

                UpdateTargetHeader();
                CancelReply(); // Hủy preview Reply khi chuyển sang cuộc trò chuyện khác
                RenderChatLogForTarget(_targetUser);
            }
        }

        private void UpdateTargetHeader()
        {
            if (_targetUser == "ALL")
            {
                lblTargetChat.Text = "Phòng Chat: TẤT CẢ (ALL)";
                lblTargetSubtitle.Text = "Tin nhắn gửi tới tất cả thành viên trong hệ thống (Broadcast)";
            }
            else
            {
                lblTargetChat.Text = $"Chat 1-1 với: {_targetUser}";
                lblTargetSubtitle.Text = $"Tin nhắn riêng tư chỉ gửi tới '{_targetUser}'";
            }
        }

        /// <summary>
        /// 5. Lọc danh sách bạn bè
        /// </summary>
        private void txtSearchUser_TextChanged(object sender, EventArgs e)
        {
            RefreshUserListUI();
        }

        /// <summary>
        /// 5. Tìm kiếm tin nhắn trong cuộc trò chuyện hiện tại
        /// </summary>
        private void txtSearchMessage_TextChanged(object sender, EventArgs e)
        {
            string keyword = txtSearchMessage.Text.Trim();
            if (string.IsNullOrEmpty(keyword))
            {
                RenderChatLogForTarget(_targetUser);
                return;
            }

            txtChatLog.Clear();
            if (_chatHistory.ContainsKey(_targetUser))
            {
                foreach (var m in _chatHistory[_targetUser])
                {
                    if (m.Content != null && m.Content.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        AppendChatMessageToView(m);
                    }
                }
            }
        }

        /// <summary>
        /// 4. Phát âm thanh thông báo
        /// </summary>
        private void PlayNotificationSound()
        {
            try
            {
                SystemSounds.Asterisk.Play();
            }
            catch { }
        }

        /// <summary>
        /// 4. Hiển thị thông báo Balloon Tip
        /// </summary>
        private void ShowNotificationBanner(string sender, string content)
        {
            try
            {
                if (this.WindowState == FormWindowState.Minimized || !this.Focused)
                {
                    string preview = content?.Length > 50 ? content.Substring(0, 50) + "..." : content;
                    notifyIconApp.ShowBalloonTip(3000, $"Tin nhắn mới từ {sender}", preview ?? "", ToolTipIcon.Info);
                }
            }
            catch { }
        }

        private void notifyIconApp_DoubleClick(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Normal;
            this.Show();
            this.BringToFront();
        }

        private static string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            return $"{bytes / (1024.0 * 1024.0):F2} MB";
        }

        #region Safe File Handling Helpers (Giai đoạn 2A)

        /// <summary>
        /// Danh sách đen các phần mở rộng tệp thực thi / kịch bản tiềm ẩn rủi ro mã độc
        /// </summary>
        private static readonly HashSet<string> DangerousExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".exe", ".bat", ".cmd", ".vbs", ".ps1", ".scr", ".msi", ".dll",
            ".com", ".pif", ".reg", ".hta", ".cpl", ".wsf", ".vbe", ".jse", ".lnk"
        };

        /// <summary>
        /// Làm sạch tên tệp tin, loại bỏ ký tự điều hướng (Path Traversal) và ký tự không hợp lệ trên Windows
        /// </summary>
        private static string SanitizeFileName(string rawFileName)
        {
            if (string.IsNullOrWhiteSpace(rawFileName))
                return "downloaded_file";

            // 1. Tước bỏ mọi thông tin đường dẫn thư mục (vd ../ hoặc C:\...)
            string cleanName = Path.GetFileName(rawFileName);

            // 2. Thay thế các ký tự không hợp lệ cho hệ thống tệp tin bằng dấu gạch dưới
            char[] invalidChars = Path.GetInvalidFileNameChars();
            foreach (char c in invalidChars)
            {
                cleanName = cleanName.Replace(c, '_');
            }

            cleanName = cleanName.Trim();
            if (string.IsNullOrWhiteSpace(cleanName))
                return "downloaded_file";

            return cleanName;
        }

        /// <summary>
        /// Tự động sinh tên đường dẫn duy nhất với hậu tố _(1), _(2)... khi tệp đã tồn tại, tránh ghi đè mất dữ liệu
        /// </summary>
        private static string GetUniqueFilePath(string filePath)
        {
            string dir = Path.GetDirectoryName(filePath);
            string nameWithoutExt = Path.GetFileNameWithoutExtension(filePath);
            string ext = Path.GetExtension(filePath);
            int counter = 1;

            string newPath;
            do
            {
                newPath = Path.Combine(dir, $"{nameWithoutExt}_({counter}){ext}");
                counter++;
            } while (File.Exists(newPath));

            return newPath;
        }

        #endregion

        #region Xử lý Reply / Trả Lời Tin Nhắn (Giai đoạn 2C)

        private string _replySender = null;
        private string _replyQuote = null;

        /// <summary>
        /// Kích hoạt chế độ Reply tin nhắn, hiển thị thanh xem trước trên ô nhập
        /// </summary>
        private void StartReply(string sender, string rawContent)
        {
            if (string.IsNullOrWhiteSpace(rawContent)) return;

            // Làm sạch nội dung: chuyển các ký tự xuống dòng thành khoảng trắng để trích dẫn gọn gàng
            string cleanQuote = rawContent.Replace("\r", " ").Replace("\n", " ").Trim();
            while (cleanQuote.Contains("  "))
            {
                cleanQuote = cleanQuote.Replace("  ", " ");
            }

            // Nếu tin được trích dẫn lại chính là một tin reply cũ, chỉ trích dẫn phần nội dung mới
            if (cleanQuote.StartsWith("💬 [Trả lời"))
            {
                int quoteEndIdx = cleanQuote.IndexOf(']');
                if (quoteEndIdx > 0 && quoteEndIdx < cleanQuote.Length - 1)
                {
                    cleanQuote = cleanQuote.Substring(quoteEndIdx + 1).Trim();
                    if (cleanQuote.StartsWith("👉 "))
                    {
                        cleanQuote = cleanQuote.Substring(3).Trim();
                    }
                }
            }

            // Giới hạn độ dài trích dẫn tối đa 60 ký tự
            if (cleanQuote.Length > 60)
            {
                cleanQuote = cleanQuote.Substring(0, 57) + "...";
            }

            _replySender = string.IsNullOrWhiteSpace(sender) ? "thành viên" : sender.Trim();
            _replyQuote = cleanQuote;

            lblReplyPreview.Text = $"↩️ Đang trả lời @{_replySender}: \"{_replyQuote}\"";
            pnlReply.Visible = true;
            pnlReply.BringToFront();

            txtInputMessage.Focus();
        }

        /// <summary>
        /// Hủy bỏ chế độ Reply và ẩn thanh xem trước
        /// </summary>
        private void CancelReply()
        {
            _replySender = null;
            _replyQuote = null;
            if (pnlReply != null)
            {
                pnlReply.Visible = false;
            }
            if (lblReplyPreview != null)
            {
                lblReplyPreview.Text = "";
            }
        }

        private void btnCancelReply_Click(object sender, EventArgs e)
        {
            CancelReply();
        }

        private void ctxChatLogMenu_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Kiểm tra token tải file tại vị trí chuột
            Point mousePos = txtChatLog.PointToClient(Cursor.Position);
            string downloadToken = FindDownloadTokenAtPosition(mousePos);
            _activeContextMenuDownloadToken = downloadToken;
            menuItemDownload.Visible = !string.IsNullOrEmpty(downloadToken);

            string selectedText = txtChatLog.SelectedText;
            if (!string.IsNullOrWhiteSpace(selectedText))
            {
                menuItemReply.Text = "↩️ Trả lời đoạn văn bản đã chọn";
                menuItemReply.Enabled = true;
                menuItemCopy.Enabled = true;
            }
            else
            {
                int charIdx = txtChatLog.GetCharIndexFromPosition(mousePos);
                int lineIdx = txtChatLog.GetLineFromCharIndex(charIdx);
                if (lineIdx >= 0 && lineIdx < txtChatLog.Lines.Length && !string.IsNullOrWhiteSpace(txtChatLog.Lines[lineIdx]))
                {
                    menuItemReply.Text = "↩️ Trả lời tin nhắn này";
                    // Không cho Reply dòng file
                    menuItemReply.Enabled = string.IsNullOrEmpty(downloadToken);
                    menuItemCopy.Enabled = false;
                }
                else
                {
                    menuItemReply.Enabled = false;
                    menuItemCopy.Enabled = false;
                }
            }
        }

        private void menuItemReply_Click(object sender, EventArgs e)
        {
            string selectedText = txtChatLog.SelectedText;
            if (!string.IsNullOrWhiteSpace(selectedText))
            {
                // Cố gắng trích xuất sender từ vùng văn bản đã bôi đen
                // bằng cách lấy dòng đầu tiên của selection để phân tích
                int selStart = txtChatLog.SelectionStart;
                int lineIdxSel = txtChatLog.GetLineFromCharIndex(selStart);
                string senderFromSelection = "thành viên";
                if (lineIdxSel >= 0 && lineIdxSel < txtChatLog.Lines.Length)
                {
                    senderFromSelection = ExtractSenderFromLogLine(txtChatLog.Lines[lineIdxSel]);
                }
                StartReply(senderFromSelection, selectedText);
                return;
            }

            Point mousePos = txtChatLog.PointToClient(Cursor.Position);
            int charIdx = txtChatLog.GetCharIndexFromPosition(mousePos);
            int lineIdx = txtChatLog.GetLineFromCharIndex(charIdx);
            if (lineIdx >= 0 && lineIdx < txtChatLog.Lines.Length)
            {
                ParseAndStartReplyFromLine(txtChatLog.Lines[lineIdx]);
            }
        }

        private void menuItemCopy_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(txtChatLog.SelectedText))
            {
                Clipboard.SetText(txtChatLog.SelectedText);
            }
        }

        private void txtChatLog_DoubleClick(object sender, EventArgs e)
        {
            Point mousePos = txtChatLog.PointToClient(Cursor.Position);
            // Bỏ qua double-click trên dòng liên kết tải file
            if (!string.IsNullOrEmpty(FindDownloadTokenAtPosition(mousePos))) return;

            int charIdx = txtChatLog.GetCharIndexFromPosition(mousePos);
            int lineIdx = txtChatLog.GetLineFromCharIndex(charIdx);
            if (lineIdx >= 0 && lineIdx < txtChatLog.Lines.Length)
            {
                ParseAndStartReplyFromLine(txtChatLog.Lines[lineIdx]);
            }
        }

        // ── Xử lý tải file ──────────────────────────────────────────────────────

        private void txtChatLog_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            string tokenKey = FindDownloadTokenAtPosition(e.Location);
            if (!string.IsNullOrEmpty(tokenKey))
                ProcessDownloadToken(tokenKey);
        }

        private void txtChatLog_MouseMove(object sender, MouseEventArgs e)
        {
            string tokenKey = FindDownloadTokenAtPosition(e.Location);
            txtChatLog.Cursor = string.IsNullOrEmpty(tokenKey) ? Cursors.IBeam : Cursors.Hand;
        }

        private void menuItemDownload_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(_activeContextMenuDownloadToken))
                ProcessDownloadToken(_activeContextMenuDownloadToken);
        }

        /// <summary>
        /// Tìm download token tại vị trí pt (tọa độ client trong txtChatLog).
        /// Trả về null/empty nếu không có.
        /// </summary>
        private string FindDownloadTokenAtPosition(Point pt)
        {
            try
            {
                int charIdx = txtChatLog.GetCharIndexFromPosition(pt);
                int lineIdx = txtChatLog.GetLineFromCharIndex(charIdx);
                if (lineIdx < 0 || lineIdx >= txtChatLog.Lines.Length) return null;
                string line = txtChatLog.Lines[lineIdx];
                // Tìm token dạng download://GUID hoặc download://any-token
                int start = line.IndexOf("download://", StringComparison.Ordinal);
                if (start < 0) return null;
                int end = start + "download://".Length;
                while (end < line.Length && !char.IsWhiteSpace(line[end])) end++;
                string token = line.Substring(start, end - start);
                return _receivedFiles.ContainsKey(token) ? token : null;
            }
            catch { return null; }
        }

        /// <summary>
        /// Xử lý tải và lưu file từ token download://
        /// </summary>
        private void ProcessDownloadToken(string tokenKey)
        {
            // Debounce 800 ms chống click liên tiếp
            long now = DateTime.UtcNow.Ticks;
            if ((now - _lastDownloadClickTicks) < TimeSpan.TicksPerMillisecond * 800)
                return;
            _lastDownloadClickTicks = now;

            if (!_receivedFiles.TryGetValue(tokenKey, out FilePayload payload))
            {
                MessageBox.Show("Không tìm thấy dữ liệu tệp tin.", "Lỗi",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrEmpty(payload.Base64Data))
            {
                MessageBox.Show("Dữ liệu tệp tin bị trống.", "Lỗi",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            byte[] fileBytes;
            try { fileBytes = Convert.FromBase64String(payload.Base64Data); }
            catch
            {
                MessageBox.Show("Dữ liệu tệp tin bị hỏng (Base64 không hợp lệ).", "Lỗi",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            using (var dlg = new SaveFileDialog())
            {
                dlg.Title      = "Lưu tệp tin nhận được";
                dlg.FileName   = string.IsNullOrWhiteSpace(payload.FileName) ? "file" : payload.FileName;
                dlg.Filter     = "Tất cả tệp tin (*.*)|*.*";
                dlg.DefaultExt = Path.GetExtension(payload.FileName)?.TrimStart('.') ?? "";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    string savePath = GetUniqueFilePath(dlg.FileName);
                    File.WriteAllBytes(savePath, fileBytes);
                    MessageBox.Show($"Đã lưu: {savePath}", "Lưu file thành công",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);

                    // Mở thư mục chứa file sau khi lưu
                    Process.Start("explorer.exe", $"/select,\"{savePath}\"");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Không thể lưu tệp tin:\n{ex.Message}", "Lỗi",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }



        /// <summary>
        /// Phân tích một dòng text từ txtChatLog để trích xuất người gửi và nội dung tin nhắn
        /// </summary>
        private void ParseAndStartReplyFromLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return;
            if (line.Contains("[HỆ THỐNG]")) return;

            string sender = "thành viên";
            string content = line.Trim();

            // Nhận diện dòng bắt đầu có timestamp: [HH:mm:ss] [LOẠI] Sender: Content
            int secondBracket = line.IndexOf("] ", line.IndexOf(']') + 1);
            if (secondBracket > 0 && secondBracket + 2 < line.Length)
            {
                string rest = line.Substring(secondBracket + 2);
                int colonIdx = rest.IndexOf(": ");
                if (colonIdx > 0)
                {
                    sender = rest.Substring(0, colonIdx).Trim();
                    int arrowIdx = sender.IndexOf(" -> ");
                    if (arrowIdx > 0)
                    {
                        sender = sender.Substring(0, arrowIdx).Trim();
                    }
                    content = rest.Substring(colonIdx + 2).Trim();
                }
            }
            else
            {
                // Dòng trả lời hoặc dòng thứ 2
                if (content.StartsWith("👉 "))
                {
                    content = content.Substring(3).Trim();
                }
                int colonIdx = content.IndexOf(": ");
                if (colonIdx > 0 && colonIdx < 30)
                {
                    sender = content.Substring(0, colonIdx).Trim();
                    content = content.Substring(colonIdx + 2).Trim();
                }
            }

            StartReply(sender, content);
        }

        /// <summary>
        /// Trích xuất tên người gửi từ một dòng log theo định dạng: [HH:mm:ss] [LOẠI] Sender: ...
        /// </summary>
        private string ExtractSenderFromLogLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return "thành viên";
            int secondBracket = line.IndexOf("] ", line.IndexOf(']') + 1);
            if (secondBracket > 0 && secondBracket + 2 < line.Length)
            {
                string rest = line.Substring(secondBracket + 2);
                int colonIdx = rest.IndexOf(": ");
                if (colonIdx > 0)
                {
                    string senderPart = rest.Substring(0, colonIdx).Trim();
                    int arrowIdx = senderPart.IndexOf(" -> ");
                    if (arrowIdx > 0) senderPart = senderPart.Substring(0, arrowIdx).Trim();
                    return string.IsNullOrWhiteSpace(senderPart) ? "thành viên" : senderPart;
                }
            }
            return "thành viên";
        }

        #endregion

        private async void btnLogout_Click(object sender, EventArgs e)
        {
            var confirm = MessageBox.Show("Bạn có chắc chắn muốn đăng xuất?", "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm == DialogResult.Yes)
            {
                // 6. Lưu lịch sử trước khi thoát
                _storageService.SaveHistory(_chatHistory);
                await _socketClient.SendLogoutAsync();
                _socketClient.Disconnect();
                this.Close();
            }
        }

        #endregion

        #region Xử lý Mất kết nối & Đóng Form

        private void HandleConnected()
        {
            if (IsDisposed || Disposing) return;
            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke(new Action(HandleConnected));
                }
                catch (ObjectDisposedException) { }
                catch (InvalidOperationException) { }
                return;
            }

            // TCP đã kết nối nhưng chưa xác thực xong -> giữ nguyên các nút gửi bị vô hiệu hóa
            lblConnectionStatus.Text      = "🟡 Đang xác thực phiên...";
            lblConnectionStatus.ForeColor = Color.FromArgb(251, 191, 36);
            btnSend.Enabled               = false;
            btnSendFile.Enabled           = false;
            txtInputMessage.Enabled       = false;
        }

        private void HandleLoginSuccess()
        {
            if (IsDisposed || Disposing) return;
            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke(new Action(HandleLoginSuccess));
                }
                catch (ObjectDisposedException) { }
                catch (InvalidOperationException) { }
                return;
            }

            lblConnectionStatus.Text      = "🟢 Đã kết nối";
            lblConnectionStatus.ForeColor = Color.FromArgb(52, 211, 153);
            btnSend.Enabled               = true;
            btnSendFile.Enabled           = true;
            txtInputMessage.Enabled       = true;

            AppendSystemMessage("✅ Đã kết nối lại và khôi phục phiên làm việc thành công!");
        }

        private void HandleLoginFailed(string reason)
        {
            if (IsDisposed || Disposing) return;
            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke(new Action(() => HandleLoginFailed(reason)));
                }
                catch (ObjectDisposedException) { }
                catch (InvalidOperationException) { }
                return;
            }

            lblConnectionStatus.Text      = "🔴 Đăng nhập lại thất bại";
            lblConnectionStatus.ForeColor = Color.FromArgb(248, 113, 113);
            btnSend.Enabled               = false;
            btnSendFile.Enabled           = false;
            txtInputMessage.Enabled       = false;

            AppendSystemMessage($"❌ Đăng nhập lại thất bại: {reason ?? "Tên đăng nhập không hợp lệ hoặc đã tồn tại"}. Vui lòng đăng nhập lại.");
        }

        private void HandleDisconnected()
        {
            if (IsDisposed || Disposing) return;
            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke(new Action(HandleDisconnected));
                }
                catch (ObjectDisposedException) { }
                catch (InvalidOperationException) { }
                return;
            }

            lblConnectionStatus.Text      = "🔴 Mất kết nối";
            lblConnectionStatus.ForeColor = Color.FromArgb(248, 113, 113);
            btnSend.Enabled               = false;
            btnSendFile.Enabled           = false;
            txtInputMessage.Enabled       = false;

            AppendSystemMessage("⚠️ Mất kết nối. Hệ thống đang tự động thử kết nối lại...");
        }

        private void HandleReconnecting(int attempt)
        {
            if (IsDisposed || Disposing) return;
            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke(new Action(() => HandleReconnecting(attempt)));
                }
                catch (ObjectDisposedException) { }
                catch (InvalidOperationException) { }
                return;
            }

            lblConnectionStatus.Text      = $"🟡 Đang kết nối lại... ({attempt}/3)";
            lblConnectionStatus.ForeColor = Color.FromArgb(251, 191, 36);
            btnSend.Enabled               = false;
            btnSendFile.Enabled           = false;
            txtInputMessage.Enabled       = false;

            AppendSystemMessage($"🔄 Đang thử kết nối lại lần {attempt}/3...");
        }

        private void HandleNetworkError(string error)
        {
            if (IsDisposed || Disposing) return;
            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke(new Action(() => HandleNetworkError(error)));
                }
                catch (ObjectDisposedException) { }
                catch (InvalidOperationException) { }
                return;
            }

            // Ghi nhận lỗi mạng, KHÔNG tự ý chuyển trạng thái sang "Đã kết nối"
            AppendSystemMessage($"[LỖI MẠNG]: {error}");
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                // 1. Tự động lưu lịch sử tin nhắn
                _storageService.SaveHistory(_chatHistory);

                // 2. Dừng và hủy toàn bộ timer (bao gồm auto-save)
                _autoSaveTimer?.Stop();
                _autoSaveTimer?.Dispose();
                _typingDebounceTimer?.Stop();
                _typingDisplayTimer?.Stop();
                _typingDebounceTimer?.Dispose();
                _typingDisplayTimer?.Dispose();

                // 3. Ẩn notifyIcon khi tắt app tránh icon ma (ghost icon) ở Taskbar tray
                if (notifyIconApp != null)
                {
                    notifyIconApp.Visible = false;
                    notifyIconApp.Dispose();
                }

                // 4. Hủy đăng ký toàn bộ sự kiện tránh rò rỉ bộ nhớ
                _socketClient.OnMessageReceived -= HandleMessageReceived;
                _socketClient.OnDisconnected    -= HandleDisconnected;
                _socketClient.OnErrorOccurred   -= HandleNetworkError;
                _socketClient.OnReconnecting    -= HandleReconnecting;
                _socketClient.OnConnected       -= HandleConnected;
                _socketClient.OnLoginSuccess    -= HandleLoginSuccess;
                _socketClient.OnLoginFailed     -= HandleLoginFailed;

                // 5. Ngắt kết nối socket và hủy ngay lập tức các tác vụ reconnect đang chờ
                _socketClient.Disconnect();
            }
            catch
            {
                // Bỏ qua lỗi dọn dẹp khi đang tắt form
            }
        }

        #endregion
    }
}
