using System;
using System.Drawing;
using System.Windows.Forms;
using ChatDesktopApp.Models;
using ChatDesktopApp.Services;

namespace ChatDesktopApp.Forms
{
    public partial class LoginForm : Form
    {
        private readonly TcpSocketClient _socketClient;

        // Bảng màu trạng thái Dark Mode có độ tương phản cao
        private static readonly Color ColorStatusSuccess = Color.FromArgb(52, 211, 153); // Emerald (#34D399)
        private static readonly Color ColorStatusError   = Color.FromArgb(248, 113, 113); // Coral (#F87171)
        private static readonly Color ColorStatusWarning = Color.FromArgb(251, 191, 36);  // Amber (#FBBF24)
        private static readonly Color ColorInputNormal   = Color.FromArgb(16, 22, 34);   // Dark Slate Recessed
        private static readonly Color ColorInputFocus    = Color.FromArgb(26, 35, 53);   // Highlight Focus Slate

        public LoginForm()
        {
            InitializeComponent();
            _socketClient = new TcpSocketClient();
            SetupModernStyles();
        }

        private void SetupModernStyles()
        {
            // Hiệu ứng Focus/Blur cho các ô nhập liệu
            AttachFocusEffect(txtUsername);
            AttachFocusEffect(txtServerIp);
            AttachFocusEffect(numPort);
        }

        private void AttachFocusEffect(Control control)
        {
            control.GotFocus += (s, e) => control.BackColor = ColorInputFocus;
            control.LostFocus += (s, e) => control.BackColor = ColorInputNormal;
        }

        private async void btnConnect_Click(object sender, EventArgs e)
        {
            string username = txtUsername.Text.Trim();
            string ip       = txtServerIp.Text.Trim();
            int    port     = (int)numPort.Value;

            // ── Validate Username ──────────────────────────────────────────
            // Dùng ValidateUsername từ TcpSocketClient — nhất quán với giao thức
            var (isValid, errorMsg) = TcpSocketClient.ValidateUsername(username);
            if (!isValid)
            {
                SetStatus(errorMsg, ColorStatusError);
                txtUsername.Focus();
                txtUsername.SelectAll();
                return;
            }

            if (string.IsNullOrEmpty(ip))
            {
                SetStatus("Vui lòng nhập địa chỉ Server IP!", ColorStatusError);
                txtServerIp.Focus();
                return;
            }

            // ── Đăng ký event handlers ─────────────────────────────────────
            CleanupLoginHandlers(); // Đảm bảo không bị trùng lặp handler
            _socketClient.OnMessageReceived += HandleLoginResponse;
            _socketClient.OnErrorOccurred   += HandleNetworkError;

            btnConnect.Enabled = false;
            btnConnect.Text    = "ĐANG KẾT NỐI...";
            SetStatus("Đang kết nối tới Java Server...", ColorStatusWarning);

            bool connected = await _socketClient.ConnectAsync(ip, port);
            if (connected)
            {
                SetStatus("Đang xác thực thông tin đăng nhập...", ColorStatusWarning);
                await _socketClient.SendLoginAsync(username);
            }
            else
            {
                CleanupLoginHandlers();
                btnConnect.Enabled = true;
                btnConnect.Text    = "ĐĂNG NHẬP VÀO PHÒNG CHAT";
                SetStatus("Kết nối thất bại! Hãy chắc chắn Server đang chạy.", ColorStatusError);
            }
        }

        private void HandleLoginResponse(ChatMessage msg)
        {
            if (IsDisposed || Disposing) return;
            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke(new Action(() => HandleLoginResponse(msg)));
                }
                catch (ObjectDisposedException) { }
                catch (InvalidOperationException) { }
                return;
            }

            if (msg.Action == "LOGIN_SUCCESS")
            {
                CleanupLoginHandlers();
                SetStatus("Đăng nhập thành công! Đang vào phòng chat...", ColorStatusSuccess);

                var mainForm = new MainForm(_socketClient);
                mainForm.FormClosed += (s, args) => this.Close();
                mainForm.Show();
                this.Hide();
            }
            else if (msg.Action == "LOGIN_FAILED" || msg.Action == "ERROR")
            {
                CleanupLoginHandlers();
                _socketClient.Disconnect();

                btnConnect.Enabled = true;
                btnConnect.Text    = "ĐĂNG NHẬP VÀO PHÒNG CHAT";
                SetStatus($"Đăng nhập thất bại: {msg.Content}", ColorStatusError);
                MessageBox.Show(
                    msg.Content ?? "Tên đăng nhập không hợp lệ hoặc đã tồn tại.",
                    "Lỗi đăng nhập",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void HandleNetworkError(string errorMessage)
        {
            if (IsDisposed || Disposing) return;
            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke(new Action(() => HandleNetworkError(errorMessage)));
                }
                catch (ObjectDisposedException) { }
                catch (InvalidOperationException) { }
                return;
            }

            CleanupLoginHandlers();
            btnConnect.Enabled = true;
            btnConnect.Text    = "ĐĂNG NHẬP VÀO PHÒNG CHAT";
            SetStatus($"Lỗi: {errorMessage}", ColorStatusError);
        }

        private void CleanupLoginHandlers()
        {
            _socketClient.OnMessageReceived -= HandleLoginResponse;
            _socketClient.OnErrorOccurred   -= HandleNetworkError;
        }

        private void SetStatus(string text, Color color)
        {
            lblStatus.Text      = text;
            lblStatus.ForeColor = color;
        }

        private void txtUsername_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                btnConnect.PerformClick();
            }
        }
    }
}
