using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using ChatDesktopApp.Models;
using ChatDesktopApp.Services;
using Newtonsoft.Json;

namespace ChatDesktopApp.Forms
{
    public partial class MainForm : Form
    {
        private readonly TcpSocketClient _socketClient;
        private string _targetUser = "ALL"; // Mặc định là chat nhóm với tất cả thành viên

        // Timer ẩn nhãn thông báo "User đang nhập..." sau khi hết thời gian
        private readonly System.Windows.Forms.Timer _typingDisplayTimer;

        // Timer debounce kiểm soát việc gửi gói tin TYPING (chỉ gửi khi bắt đầu gõ và gửi STOP khi ngừng gõ)
        private readonly System.Windows.Forms.Timer _typingDebounceTimer;
        private bool _isCurrentlyTyping = false;

        public MainForm(TcpSocketClient socketClient)
        {
            InitializeComponent();
            _socketClient = socketClient;

            // Đăng ký sự kiện từ TcpSocketClient
            _socketClient.OnMessageReceived += HandleMessageReceived;
            _socketClient.OnDisconnected += HandleDisconnected;
            _socketClient.OnErrorOccurred += HandleNetworkError;

            // Cập nhật thông tin người dùng hiện tại
            lblCurrentUser.Text = $"👤 Bạn: {_socketClient.CurrentUsername}";
            UpdateTargetHeader();

            // Khởi tạo danh sách người dùng ban đầu
            lstUsers.Items.Clear();
            lstUsers.Items.Add("💬 TẤT CẢ (ALL)");
            lstUsers.SelectedIndex = 0;

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
            AppendSystemMessage("Chọn một người dùng bên trái để chat 1-1, hoặc chọn 'TẤT CẢ (ALL)' để chat nhóm.");
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
                    AppendChatMessage(msg);
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
        /// Cập nhật danh sách người dùng online từ gói tin USER_LIST.
        /// Content phía Java Server là JSON array: ["user1", "user2", ...]
        /// </summary>
        private void UpdateOnlineUserList(string userListJson)
        {
            try
            {
                var users = JsonConvert.DeserializeObject<List<string>>(userListJson) ?? new List<string>();

                string previouslySelected = _targetUser;

                lstUsers.BeginUpdate();
                lstUsers.Items.Clear();
                lstUsers.Items.Add("💬 TẤT CẢ (ALL)");

                int onlineCount = 0;
                bool previousTargetStillOnline = false;

                foreach (var u in users)
                {
                    if (!string.Equals(u, _socketClient.CurrentUsername, StringComparison.OrdinalIgnoreCase))
                    {
                        lstUsers.Items.Add(u);
                        onlineCount++;

                        if (string.Equals(u, previouslySelected, StringComparison.OrdinalIgnoreCase))
                        {
                            previousTargetStillOnline = true;
                        }
                    }
                }

                lblOnlineHeader.Text = $"DANH SÁCH ONLINE ({onlineCount + 1}):";

                // Giữ lại lựa chọn trước đó nếu người đó vẫn online, ngược lại về "ALL"
                if (previouslySelected != "ALL" && previousTargetStillOnline)
                {
                    int index = lstUsers.Items.IndexOf(previouslySelected);
                    if (index >= 0) lstUsers.SelectedIndex = index;
                }
                else
                {
                    lstUsers.SelectedIndex = 0;
                }

                lstUsers.EndUpdate();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Lỗi phân tích USER_LIST: {ex.Message}");
            }
        }

        /// <summary>
        /// Hiển thị thông báo trạng thái "User đang nhập..."
        /// </summary>
        private void HandleTypingIndicator(ChatMessage msg)
        {
            // Bỏ qua nếu là sự kiện của chính mình
            if (string.Equals(msg.Sender, _socketClient.CurrentUsername, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // Nếu người nhận là ALL hoặc gửi trực tiếp cho mình
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
        /// Hiển thị nội dung tin nhắn lên khung chat chính với định dạng phân biệt.
        /// </summary>
        private void AppendChatMessage(ChatMessage msg)
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

            // Ghi vào RichTextBox với màu sắc phân biệt
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

            // Tự động cuộn xuống dòng mới nhất
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

            // Gửi tín hiệu dừng gõ phím ngay khi gửi tin nhắn
            if (_isCurrentlyTyping)
            {
                _typingDebounceTimer.Stop();
                _isCurrentlyTyping = false;
                await _socketClient.SendTypingAsync(_targetUser, "STOP");
            }

            // Gửi tin nhắn qua Socket
            await _socketClient.SendChatMessageAsync(_targetUser, content);

            // Vì Java Server chỉ gửi CHAT_SINGLE tới người nhận (không echo lại người gửi),
            // nên ta tự hiển thị tin nhắn cá nhân gửi đi lên màn hình của mình
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
                AppendChatMessage(selfMsg);
            }

            txtInputMessage.Clear();
            txtInputMessage.Focus();
        }

        private void txtInputMessage_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true; // Ngăn tiếng 'ding' và xuống dòng
                btnSend.PerformClick();
            }
        }

        private async void txtInputMessage_TextChanged(object sender, EventArgs e)
        {
            if (!_socketClient.IsConnected) return;

            if (txtInputMessage.Text.Length > 0)
            {
                // Khi bắt đầu gõ: chỉ gửi START một lần
                if (!_isCurrentlyTyping)
                {
                    _isCurrentlyTyping = true;
                    await _socketClient.SendTypingAsync(_targetUser, "START");
                }

                // Reset timer debounce: Mỗi lần gõ tiếp thì dời thời điểm gửi STOP thêm 1.5s
                _typingDebounceTimer.Stop();
                _typingDebounceTimer.Start();
            }
            else
            {
                // Khi xóa hết nội dung ô nhập: gửi ngay STOP
                if (_isCurrentlyTyping)
                {
                    _typingDebounceTimer.Stop();
                    _isCurrentlyTyping = false;
                    await _socketClient.SendTypingAsync(_targetUser, "STOP");
                }
            }
        }

        private void lstUsers_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lstUsers.SelectedItem != null)
            {
                string selected = lstUsers.SelectedItem.ToString();
                if (selected.StartsWith("💬 TẤT CẢ"))
                {
                    _targetUser = "ALL";
                }
                else
                {
                    _targetUser = selected;
                }
                UpdateTargetHeader();
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

        private async void btnLogout_Click(object sender, EventArgs e)
        {
            var confirm = MessageBox.Show("Bạn có chắc chắn muốn đăng xuất?", "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm == DialogResult.Yes)
            {
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

            lblConnectionStatus.Text = "🔴 Mất kết nối";
            lblConnectionStatus.ForeColor = Color.Red;
            btnSend.Enabled = false;
            txtInputMessage.Enabled = false;

            AppendSystemMessage("Đã ngắt kết nối khỏi Server.");
        }

        private void HandleNetworkError(string error)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => HandleNetworkError(error)));
                return;
            }

            AppendSystemMessage($"[LỖI MẠNG]: {error}");
        }

        private async void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                _typingDebounceTimer?.Stop();
                _typingDisplayTimer?.Stop();

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
