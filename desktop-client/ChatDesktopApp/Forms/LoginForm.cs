using System;
using System.Windows.Forms;
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
                MessageBox.Show("Vui lòng nhập Tên đăng nhập!", "Cảnh báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            btnConnect.Enabled = false;
            lblStatus.Text = "Đang kết nối Server...";

            bool connected = await _socketClient.ConnectAsync(ip, port);
            if (connected)
            {
                _socketClient.OnMessageReceived += SocketClient_OnMessageReceived;
                await _socketClient.SendLoginAsync(username);
            }
            else
            {
                btnConnect.Enabled = true;
                lblStatus.Text = "Kết nối thất bại!";
            }
        }

        private void SocketClient_OnMessageReceived(Models.ChatMessage msg)
        {
            if (msg.Action == "LOGIN_SUCCESS")
            {
                _socketClient.OnMessageReceived -= SocketClient_OnMessageReceived;
                Invoke(new Action(() =>
                {
                    var mainForm = new MainForm(_socketClient);
                    mainForm.Show();
                    this.Hide();
                }));
            }
            else if (msg.Action == "ERROR" || msg.Action == "LOGIN_FAILED")
            {
                Invoke(new Action(() =>
                {
                    MessageBox.Show(msg.Content, "Lỗi đăng nhập", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    btnConnect.Enabled = true;
                    lblStatus.Text = "Đăng nhập thất bại.";
                    _socketClient.Disconnect();
                }));
            }
        }
    }
}
