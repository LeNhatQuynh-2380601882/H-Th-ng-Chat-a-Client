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

        public MainForm(TcpSocketClient socketClient)
        {
            InitializeComponent();
            _socketClient = socketClient;

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

            AppendSystemMessage("Chào mừng bạn đến với Hệ Thống Chat Đa Nền Tảng!");
            AppendSystemMessage("▶ Chọn một người dùng bên trái để chat 1-1, hoặc chọn 'TẤT CẢ (ALL)' để chat nhóm.");
            AppendSystemMessage($"🛡️ Session Token: {_socketClient.SessionToken?.Substring(0, 8)}...");
        }

        #region Xử lý Gói tin nhận từ Server (UI Thread-Safe)

        private void HandleMessageReceived(ChatMessage msg)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => HandleMessageReceived(msg)));
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
            if (payload == null) return;

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

            bool isRelevant = string.Equals(msg.Receiver, "ALL", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(msg.Receiver, _socketClient.CurrentUsername, StringComparison.OrdinalIgnoreCase);

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
                headerColor = Color.DarkBlue;
            }
            else if (msg.Action == "FILE_TRANSFER")
            {
                prefix = "[FILE]";
                headerColor = Color.DarkSlateGray;
            }
            else
            {
                prefix = "[CÁ NHÂN]";
                headerColor = Color.DarkMagenta;
            }

            string senderDisplay = isMe ? "Bạn" : msg.Sender;
            if (!isMe && msg.Action == "CHAT_SINGLE")
            {
                senderDisplay = $"{msg.Sender} -> Bạn";
            }

            txtChatLog.SelectionStart = txtChatLog.TextLength;
            txtChatLog.SelectionLength = 0;

            // In timestamp và tiền tố
            txtChatLog.SelectionColor = Color.Gray;
            txtChatLog.AppendText($"[{timeStr}] ");

            txtChatLog.SelectionColor = headerColor;
            txtChatLog.SelectionFont = new Font(txtChatLog.Font, FontStyle.Bold);
            txtChatLog.AppendText($"{prefix} {senderDisplay}: ");

            // In nội dung tin nhắn
            txtChatLog.SelectionColor = Color.Black;
            txtChatLog.SelectionFont = new Font(txtChatLog.Font, FontStyle.Regular);
            txtChatLog.AppendText($"{msg.Content}{Environment.NewLine}");

            txtChatLog.ScrollToCaret();
        }

        private void AppendSystemMessage(string content)
        {
            string timeStr = DateTime.Now.ToString("HH:mm:ss");

            txtChatLog.SelectionStart = txtChatLog.TextLength;
            txtChatLog.SelectionLength = 0;

            txtChatLog.SelectionColor = Color.DimGray;
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

            if (!_socketClient.IsConnected)
            {
                MessageBox.Show("Mất kết nối với Server! Không thể gửi tin nhắn.", "Lỗi kết nối", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
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
        /// 1. Nút Gửi File / Hình ảnh
        /// </summary>
        private async void btnSendFile_Click(object sender, EventArgs e)
        {
            if (!_socketClient.IsConnected)
            {
                MessageBox.Show("Mất kết nối với Server! Không thể gửi file.", "Lỗi kết nối", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (var ofd = new OpenFileDialog())
            {
                ofd.Title = "Chọn tệp tin hoặc hình ảnh để gửi";
                ofd.Filter = "Tất cả các tệp (*.*)|*.*|Hình ảnh (*.png;*.jpg;*.jpeg;*.gif)|*.png;*.jpg;*.jpeg;*.gif";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        var fileInfo = new FileInfo(ofd.FileName);
                        if (fileInfo.Length > 5 * 1024 * 1024) // Giới hạn 5MB cho socket JSON
                        {
                            MessageBox.Show("Dung lượng tệp tin vượt quá 5MB. Vui lòng chọn tệp nhỏ hơn!", "Cảnh báo kích thước", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }

                        byte[] bytes = File.ReadAllBytes(ofd.FileName);
                        string ext = fileInfo.Extension.ToLowerInvariant();
                        bool isImage = ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".gif" || ext == ".bmp";

                        await _socketClient.SendFileAsync(_targetUser, fileInfo.Name, bytes, isImage);

                        // Tự lưu và hiển thị cho chính mình
                        string tokenKey = $"download://{Guid.NewGuid().ToString("N").Substring(0, 8)}";
                        _receivedFiles[tokenKey] = new FilePayload
                        {
                            FileName = fileInfo.Name,
                            FileSize = bytes.Length,
                            IsImage = isImage,
                            Base64Data = Convert.ToBase64String(bytes)
                        };

                        string fileTypeIcon = isImage ? "🖼️ [HÌNH ẢNH]" : "📁 [TỆP TIN]";
                        string fileSizeStr = FormatFileSize(bytes.Length);
                        string displayContent = $"{fileTypeIcon} {fileInfo.Name} ({fileSizeStr})\n👉 Bấm vào liên kết để lưu file: {tokenKey}";

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

            // Hiển thị ngay phía trên nút Emoji
            menu.Show(btnEmoji, new Point(0, -menu.Height));
        }

        /// <summary>
        /// 3. Xử lý click vào link trong RichTextBox (mở web hoặc tải file)
        /// </summary>
        private void txtChatLog_LinkClicked(object sender, LinkClickedEventArgs e)
        {
            string url = e.LinkText;
            if (url.StartsWith("download://"))
            {
                if (_receivedFiles.TryGetValue(url, out var payload))
                {
                    using (var sfd = new SaveFileDialog())
                    {
                        sfd.FileName = payload.FileName;
                        sfd.Filter = "Tất cả các tệp (*.*)|*.*";
                        if (sfd.ShowDialog() == DialogResult.OK)
                        {
                            try
                            {
                                byte[] fileBytes = Convert.FromBase64String(payload.Base64Data);
                                File.WriteAllBytes(sfd.FileName, fileBytes);
                                MessageBox.Show("Đã lưu tệp tin thành công!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show($"Lỗi khi lưu tệp: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }
                    }
                }
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
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                btnSend.PerformClick();
            }
        }

        private async void txtInputMessage_TextChanged(object sender, EventArgs e)
        {
            if (!_socketClient.IsConnected) return;

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
        private void lstUsers_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lstUsers.SelectedItem != null)
            {
                string rawUser = ExtractRawUsername(lstUsers.SelectedItem.ToString());
                _targetUser = rawUser;

                // Xóa unread badge của user này
                if (_unreadCounts.ContainsKey(_targetUser))
                {
                    _unreadCounts[_targetUser] = 0;
                    RefreshUserListUI();
                }

                UpdateTargetHeader();
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

        private void HandleDisconnected()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(HandleDisconnected));
                return;
            }

            lblConnectionStatus.Text      = "🔴 Mất kết nối";
            lblConnectionStatus.ForeColor = Color.Red;
            btnSend.Enabled               = false;
            btnSendFile.Enabled           = false;
            txtInputMessage.Enabled       = false;

            AppendSystemMessage("⚠️ Mất kết nối. Hệ thống đang tự động thử kết nối lại...");
        }

        private void HandleReconnecting(int attempt)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => HandleReconnecting(attempt)));
                return;
            }

            lblConnectionStatus.Text      = $"🟡 Đang kết nối lại... ({attempt}/3)";
            lblConnectionStatus.ForeColor = Color.DarkOrange;
            AppendSystemMessage($"🔄 Đang thử kết nối lại lần {attempt}/3...");
        }

        private void HandleNetworkError(string error)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => HandleNetworkError(error)));
                return;
            }

            if (_socketClient.IsConnected)
            {
                lblConnectionStatus.Text      = "🟢 Đã kết nối";
                lblConnectionStatus.ForeColor = Color.ForestGreen;
                btnSend.Enabled               = true;
                btnSendFile.Enabled           = true;
                txtInputMessage.Enabled       = true;
            }

            AppendSystemMessage($"[LỖI MẠNG]: {error}");
        }

        private async void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                // 6. Tự động lưu lịch sử tin nhắn
                _storageService.SaveHistory(_chatHistory);

                _typingDebounceTimer?.Stop();
                _typingDisplayTimer?.Stop();

                // Ẩn notifyIcon khi tắt app tránh icon ma (ghost icon) ở Taskbar tray
                if (notifyIconApp != null)
                {
                    notifyIconApp.Visible = false;
                    notifyIconApp.Dispose();
                }

                // Hủy đăng ký sự kiện tránh rò rỉ sự kiện
                _socketClient.OnMessageReceived -= HandleMessageReceived;
                _socketClient.OnDisconnected    -= HandleDisconnected;
                _socketClient.OnErrorOccurred   -= HandleNetworkError;
                _socketClient.OnReconnecting    -= HandleReconnecting;

                if (_socketClient.IsConnected)
                {
                    await _socketClient.SendLogoutAsync();
                }
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
