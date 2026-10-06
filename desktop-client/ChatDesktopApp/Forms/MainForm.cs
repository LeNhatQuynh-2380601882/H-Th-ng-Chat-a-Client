using System;
using System.Collections.Generic;
using System.Windows.Forms;
using ChatDesktopApp.Models;
using ChatDesktopApp.Services;
using Newtonsoft.Json;

namespace ChatDesktopApp.Forms
{
    public partial class MainForm : Form
    {
        private readonly TcpSocketClient _socketClient;
        private string _targetUser = "ALL"; // Default to Broadcast
        private System.Windows.Forms.Timer _typingTimer;

        public MainForm(TcpSocketClient socketClient)
        {
            InitializeComponent();
            _socketClient = socketClient;
            _socketClient.OnMessageReceived += SocketClient_OnMessageReceived;
            _socketClient.OnDisconnected += SocketClient_OnDisconnected;

            lblCurrentUser.Text = $"User: {_socketClient.CurrentUsername}";
            lblTargetChat.Text = "Phòng Chat: TẤT CẢ (ALL)";

            _typingTimer = new System.Windows.Forms.Timer { Interval = 2000 };
            _typingTimer.Tick += (s, e) =>
            {
                lblTyping.Text = "";
                _typingTimer.Stop();
            };
        }

        private void SocketClient_OnMessageReceived(ChatMessage msg)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => SocketClient_OnMessageReceived(msg)));
                return;
            }

            switch (msg.Action)
            {
                case "USER_LIST":
                    UpdateUserList(msg.Content);
                    break;

                case "CHAT_GROUP":
                case "CHAT_SINGLE":
                    AppendChatMessage(msg);
                    break;

                case "TYPING":
                    if (!string.Equals(msg.Sender, _socketClient.CurrentUsername))
                    {
                        lblTyping.Text = $"{msg.Sender} đang gõ tin nhắn...";
                        _typingTimer.Stop();
                        _typingTimer.Start();
                    }
                    break;

                case "ERROR":
                    txtChatLog.AppendText($"[HỆ THỐNG LỖI]: {msg.Content}{Environment.NewLine}");
                    break;
            }
        }

        private void UpdateUserList(string userListJson)
        {
            try
            {
                var users = JsonConvert.DeserializeObject<List<string>>(userListJson);
                lstUsers.Items.Clear();
                lstUsers.Items.Add("TẤT CẢ (ALL)");

                if (users != null)
                {
                    foreach (var u in users)
                    {
                        if (!string.Equals(u, _socketClient.CurrentUsername))
                        {
                            lstUsers.Items.Add(u);
                        }
                    }
                }
            }
            catch { }
        }

        private void AppendChatMessage(ChatMessage msg)
        {
            string timeStr = DateTimeOffset.FromUnixTimeMilliseconds(msg.Timestamp).ToLocalTime().ToString("HH:mm:ss");
            string prefix = msg.Action == "CHAT_GROUP" ? "[NHÓM]" : "[CÁ NHÂN]";
            txtChatLog.AppendText($"[{timeStr}] {prefix} {msg.Sender}: {msg.Content}{Environment.NewLine}");
        }

        private async void btnSend_Click(object sender, EventArgs e)
        {
            string content = txtInputMessage.Text.Trim();
            if (string.IsNullOrEmpty(content)) return;

            await _socketClient.SendChatMessageAsync(_targetUser, content);
            txtInputMessage.Clear();
        }

        private async void txtInputMessage_TextChanged(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(txtInputMessage.Text))
            {
                await _socketClient.SendTypingStatusAsync(_targetUser);
            }
        }

        private void lstUsers_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lstUsers.SelectedItem != null)
            {
                string selected = lstUsers.SelectedItem.ToString();
                _targetUser = selected.StartsWith("TẤT CẢ") ? "ALL" : selected;
                lblTargetChat.Text = $"Đang chat với: {_targetUser}";
            }
        }

        private void SocketClient_OnDisconnected()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(SocketClient_OnDisconnected));
                return;
            }
            MessageBox.Show("Đã ngắt kết nối khỏi Server.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            Application.Exit();
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            _socketClient.Disconnect();
        }
    }
}
