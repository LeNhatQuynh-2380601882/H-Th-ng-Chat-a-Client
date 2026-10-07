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
            string ip = txtServerIp.Text.Trim();
            int port = (int)numPort.Value;

            if (string.IsNullOrEmpty(username))
            {
                MessageBox.Show("Vui lòng nhập Tên đăng nhập (Nickname)!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtUsername.Focus();
                return;
            }

            if (string.IsNullOrEmpty(ip))
            {
                MessageBox.Show("Vui lòng nhập địa chỉ Server IP!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtServerIp.Focus();
                return;
            }

            btnConnect.Enabled = false;
            lblStatus.ForeColor = Color.DarkOrange;
            lblStatus.Text = "Đang kết nối tới Java Server...";

            // Đăng ký nhận phản hồi từ Server
            _socketClient.OnMessageReceived += HandleLoginResponse;
            _socketClient.OnErrorOccurred += HandleNetworkError;

            bool connected = await _socketClient.ConnectAsync(ip, port);
            if (connected)
            {
                lblStatus.Text = "Đang xác thực thông tin đăng nhập...";
                await _socketClient.SendLoginAsync(username);
            }
            else
            {
                CleanupLoginHandlers();
                btnConnect.Enabled = true;
                lblStatus.ForeColor = Color.Red;
                lblStatus.Text = "Kết nối thất bại! Hãy chắc chắn Server đã chạy.";
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

                lblStatus.ForeColor = Color.Green;
                lblStatus.Text = "Đăng nhập thành công! Đang vào phòng chat...";

                // Mở MainForm và ẩn LoginForm
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
                lblStatus.ForeColor = Color.Red;
                lblStatus.Text = $"Đăng nhập thất bại: {msg.Content}";
                MessageBox.Show(msg.Content ?? "Tên đăng nhập không hợp lệ hoặc đã bị lỗi.", "Lỗi đăng nhập", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            lblStatus.ForeColor = Color.Red;
            lblStatus.Text = "Lỗi kết nối!";
        }

        private void CleanupLoginHandlers()
        {
            _socketClient.OnMessageReceived -= HandleLoginResponse;
            _socketClient.OnErrorOccurred -= HandleNetworkError;
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
