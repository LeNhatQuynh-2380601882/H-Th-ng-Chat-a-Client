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

        public LoginForm()
        {
            InitializeComponent();
            _socketClient = new TcpSocketClient();
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
                SetStatus(errorMsg, Color.Red);
                txtUsername.Focus();
                txtUsername.SelectAll();
                return;
            }

            if (string.IsNullOrEmpty(ip))
            {
                SetStatus("Vui lòng nhập địa chỉ Server IP!", Color.Red);
                txtServerIp.Focus();
                return;
            }

            // ── Đăng ký event handlers ─────────────────────────────────────
            _socketClient.OnMessageReceived += HandleLoginResponse;
            _socketClient.OnErrorOccurred   += HandleNetworkError;

            btnConnect.Enabled = false;
            SetStatus("Đang kết nối tới Java Server...", Color.DarkOrange);

            bool connected = await _socketClient.ConnectAsync(ip, port);
            if (connected)
            {
                SetStatus("Đang xác thực thông tin đăng nhập...", Color.DarkOrange);
                await _socketClient.SendLoginAsync(username);
            }
            else
            {
                CleanupLoginHandlers();
                btnConnect.Enabled = true;
                SetStatus("Kết nối thất bại! Hãy chắc chắn Server đang chạy.", Color.Red);
            }
        }

        private void HandleLoginResponse(ChatMessage msg)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => HandleLoginResponse(msg)));
                return;
            }

            if (msg.Action == "LOGIN_SUCCESS")
            {
                CleanupLoginHandlers();
                SetStatus("Đăng nhập thành công! Đang vào phòng chat...", Color.Green);

                // Đăng ký OnReconnecting trước khi chuyển sang MainForm
                _socketClient.OnReconnecting += HandleReconnecting;

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
                SetStatus($"Đăng nhập thất bại: {msg.Content}", Color.Red);
                MessageBox.Show(
                    msg.Content ?? "Tên đăng nhập không hợp lệ hoặc đã tồn tại.",
                    "Lỗi đăng nhập",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void HandleNetworkError(string errorMessage)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => HandleNetworkError(errorMessage)));
                return;
            }

            CleanupLoginHandlers();
            btnConnect.Enabled = true;
            SetStatus($"Lỗi: {errorMessage}", Color.Red);
        }

        /// <summary>
        /// Xử lý sự kiện auto-reconnect — hiển thị trên LoginForm khi nó vẫn visible.
        /// </summary>
        private void HandleReconnecting(int attempt)
        {
            // Không cần hiển thị trên LoginForm (MainForm đã xử lý)
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
