namespace ChatDesktopApp.Forms
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.pnlLeft = new System.Windows.Forms.Panel();
            this.btnLogout = new System.Windows.Forms.Button();
            this.lstUsers = new System.Windows.Forms.ListBox();
            this.lblOnlineHeader = new System.Windows.Forms.Label();
            this.lblConnectionStatus = new System.Windows.Forms.Label();
            this.lblCurrentUser = new System.Windows.Forms.Label();
            this.pnlChatHeader = new System.Windows.Forms.Panel();
            this.lblTargetSubtitle = new System.Windows.Forms.Label();
            this.lblTargetChat = new System.Windows.Forms.Label();
            this.txtChatLog = new System.Windows.Forms.RichTextBox();
            this.lblTyping = new System.Windows.Forms.Label();
            this.txtInputMessage = new System.Windows.Forms.TextBox();
            this.btnSend = new System.Windows.Forms.Button();
            this.pnlLeft.SuspendLayout();
            this.pnlChatHeader.SuspendLayout();
            this.SuspendLayout();
            // 
            // pnlLeft
            // 
            this.pnlLeft.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(242)))), ((int)(((byte)(245)))));
            this.pnlLeft.Controls.Add(this.btnLogout);
            this.pnlLeft.Controls.Add(this.lstUsers);
            this.pnlLeft.Controls.Add(this.lblOnlineHeader);
            this.pnlLeft.Controls.Add(this.lblConnectionStatus);
            this.pnlLeft.Controls.Add(this.lblCurrentUser);
            this.pnlLeft.Dock = System.Windows.Forms.DockStyle.Left;
            this.pnlLeft.Location = new System.Drawing.Point(0, 0);
            this.pnlLeft.Name = "pnlLeft";
            this.pnlLeft.Padding = new System.Windows.Forms.Padding(12);
            this.pnlLeft.Size = new System.Drawing.Size(220, 501);
            this.pnlLeft.TabIndex = 0;
            // 
            // btnLogout
            // 
            this.btnLogout.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(220)))), ((int)(((byte)(53)))), ((int)(((byte)(69)))));
            this.btnLogout.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnLogout.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.btnLogout.FlatAppearance.BorderSize = 0;
            this.btnLogout.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnLogout.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.btnLogout.ForeColor = System.Drawing.Color.White;
            this.btnLogout.Location = new System.Drawing.Point(12, 459);
            this.btnLogout.Name = "btnLogout";
            this.btnLogout.Size = new System.Drawing.Size(196, 30);
            this.btnLogout.TabIndex = 4;
            this.btnLogout.Text = "ĐĂNG XUẤT";
            this.btnLogout.UseVisualStyleBackColor = false;
            this.btnLogout.Click += new System.EventHandler(this.btnLogout_Click);
            // 
            // lstUsers
            // 
            this.lstUsers.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lstUsers.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.lstUsers.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular);
            this.lstUsers.FormattingEnabled = true;
            this.lstUsers.ItemHeight = 17;
            this.lstUsers.Location = new System.Drawing.Point(12, 98);
            this.lstUsers.Name = "lstUsers";
            this.lstUsers.Size = new System.Drawing.Size(196, 342);
            this.lstUsers.TabIndex = 3;
            this.lstUsers.SelectedIndexChanged += new System.EventHandler(this.lstUsers_SelectedIndexChanged);
            // 
            // lblOnlineHeader
            // 
            this.lblOnlineHeader.AutoSize = true;
            this.lblOnlineHeader.Font = new System.Drawing.Font("Segoe UI", 8.5F, System.Drawing.FontStyle.Bold);
            this.lblOnlineHeader.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(100)))), ((int)(((byte)(110)))), ((int)(((byte)(120)))));
            this.lblOnlineHeader.Location = new System.Drawing.Point(12, 75);
            this.lblOnlineHeader.Name = "lblOnlineHeader";
            this.lblOnlineHeader.Size = new System.Drawing.Size(126, 15);
            this.lblOnlineHeader.TabIndex = 2;
            this.lblOnlineHeader.Text = "DANH SÁCH ONLINE:";
            // 
            // lblConnectionStatus
            // 
            this.lblConnectionStatus.AutoSize = true;
            this.lblConnectionStatus.Font = new System.Drawing.Font("Segoe UI", 8.5F, System.Drawing.FontStyle.Bold);
            this.lblConnectionStatus.ForeColor = System.Drawing.Color.ForestGreen;
            this.lblConnectionStatus.Location = new System.Drawing.Point(12, 45);
            this.lblConnectionStatus.Name = "lblConnectionStatus";
            this.lblConnectionStatus.Size = new System.Drawing.Size(84, 15);
            this.lblConnectionStatus.TabIndex = 1;
            this.lblConnectionStatus.Text = "🟢 Đã kết nối";
            // 
            // lblCurrentUser
            // 
            this.lblCurrentUser.AutoEllipsis = true;
            this.lblCurrentUser.Font = new System.Drawing.Font("Segoe UI", 11F, System.Drawing.FontStyle.Bold);
            this.lblCurrentUser.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(20)))), ((int)(((byte)(30)))), ((int)(((byte)(80)))));
            this.lblCurrentUser.Location = new System.Drawing.Point(10, 15);
            this.lblCurrentUser.Name = "lblCurrentUser";
            this.lblCurrentUser.Size = new System.Drawing.Size(200, 24);
            this.lblCurrentUser.TabIndex = 0;
            this.lblCurrentUser.Text = "👤 Bạn: [...]";
            // 
            // pnlChatHeader
            // 
            this.pnlChatHeader.BackColor = System.Drawing.Color.White;
            this.pnlChatHeader.Controls.Add(this.lblTargetSubtitle);
            this.pnlChatHeader.Controls.Add(this.lblTargetChat);
            this.pnlChatHeader.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlChatHeader.Location = new System.Drawing.Point(220, 0);
            this.pnlChatHeader.Name = "pnlChatHeader";
            this.pnlChatHeader.Padding = new System.Windows.Forms.Padding(16, 10, 16, 10);
            this.pnlChatHeader.Size = new System.Drawing.Size(614, 60);
            this.pnlChatHeader.TabIndex = 1;
            // 
            // lblTargetSubtitle
            // 
            this.lblTargetSubtitle.AutoSize = true;
            this.lblTargetSubtitle.Font = new System.Drawing.Font("Segoe UI", 8.5F);
            this.lblTargetSubtitle.ForeColor = System.Drawing.Color.Gray;
            this.lblTargetSubtitle.Location = new System.Drawing.Point(16, 35);
            this.lblTargetSubtitle.Name = "lblTargetSubtitle";
            this.lblTargetSubtitle.Size = new System.Drawing.Size(183, 15);
            this.lblTargetSubtitle.TabIndex = 1;
            this.lblTargetSubtitle.Text = "Tin nhắn gửi tới tất cả thành viên";
            // 
            // lblTargetChat
            // 
            this.lblTargetChat.AutoSize = true;
            this.lblTargetChat.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold);
            this.lblTargetChat.ForeColor = System.Drawing.Color.MidnightBlue;
            this.lblTargetChat.Location = new System.Drawing.Point(15, 10);
            this.lblTargetChat.Name = "lblTargetChat";
            this.lblTargetChat.Size = new System.Drawing.Size(217, 21);
            this.lblTargetChat.TabIndex = 0;
            this.lblTargetChat.Text = "Phòng Chat: TẤT CẢ (ALL)";
            // 
            // txtChatLog
            // 
            this.txtChatLog.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtChatLog.BackColor = System.Drawing.Color.White;
            this.txtChatLog.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.txtChatLog.Font = new System.Drawing.Font("Segoe UI", 9.75F);
            this.txtChatLog.Location = new System.Drawing.Point(232, 66);
            this.txtChatLog.Name = "txtChatLog";
            this.txtChatLog.ReadOnly = true;
            this.txtChatLog.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Vertical;
            this.txtChatLog.Size = new System.Drawing.Size(590, 360);
            this.txtChatLog.TabIndex = 2;
            this.txtChatLog.Text = "";
            // 
            // lblTyping
            // 
            this.lblTyping.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.lblTyping.AutoSize = true;
            this.lblTyping.Font = new System.Drawing.Font("Segoe UI", 8.5F, System.Drawing.FontStyle.Italic);
            this.lblTyping.ForeColor = System.Drawing.Color.DarkGreen;
            this.lblTyping.Location = new System.Drawing.Point(232, 432);
            this.lblTyping.Name = "lblTyping";
            this.lblTyping.Size = new System.Drawing.Size(0, 15);
            this.lblTyping.TabIndex = 3;
            // 
            // txtInputMessage
            // 
            this.txtInputMessage.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtInputMessage.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.txtInputMessage.Location = new System.Drawing.Point(232, 456);
            this.txtInputMessage.Name = "txtInputMessage";
            this.txtInputMessage.PlaceholderText = "Nhập nội dung tin nhắn... (Nhấn Enter để gửi)";
            this.txtInputMessage.Size = new System.Drawing.Size(490, 25);
            this.txtInputMessage.TabIndex = 4;
            this.txtInputMessage.TextChanged += new System.EventHandler(this.txtInputMessage_TextChanged);
            this.txtInputMessage.KeyDown += new System.Windows.Forms.KeyEventHandler(this.txtInputMessage_KeyDown);
            // 
            // btnSend
            // 
            this.btnSend.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSend.BackColor = System.Drawing.Color.MidnightBlue;
            this.btnSend.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnSend.FlatAppearance.BorderSize = 0;
            this.btnSend.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnSend.Font = new System.Drawing.Font("Segoe UI", 9.5F, System.Drawing.FontStyle.Bold);
            this.btnSend.ForeColor = System.Drawing.Color.White;
            this.btnSend.Location = new System.Drawing.Point(730, 455);
            this.btnSend.Name = "btnSend";
            this.btnSend.Size = new System.Drawing.Size(92, 27);
            this.btnSend.TabIndex = 5;
            this.btnSend.Text = "GỬI";
            this.btnSend.UseVisualStyleBackColor = false;
            this.btnSend.Click += new System.EventHandler(this.btnSend_Click);
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(235)))), ((int)(((byte)(238)))), ((int)(((byte)(242)))));
            this.ClientSize = new System.Drawing.Size(834, 501);
            this.Controls.Add(this.btnSend);
            this.Controls.Add(this.txtInputMessage);
            this.Controls.Add(this.lblTyping);
            this.Controls.Add(this.txtChatLog);
            this.Controls.Add(this.pnlChatHeader);
            this.Controls.Add(this.pnlLeft);
            this.MinimumSize = new System.Drawing.Size(700, 450);
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Hệ Thống Chat - Desktop Client (WinForms)";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainForm_FormClosing);
            this.pnlLeft.ResumeLayout(false);
            this.pnlLeft.PerformLayout();
            this.pnlChatHeader.ResumeLayout(false);
            this.pnlChatHeader.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private System.Windows.Forms.Panel pnlLeft;
        private System.Windows.Forms.Label lblCurrentUser;
        private System.Windows.Forms.Label lblConnectionStatus;
        private System.Windows.Forms.Label lblOnlineHeader;
        private System.Windows.Forms.ListBox lstUsers;
        private System.Windows.Forms.Button btnLogout;
        private System.Windows.Forms.Panel pnlChatHeader;
        private System.Windows.Forms.Label lblTargetChat;
        private System.Windows.Forms.Label lblTargetSubtitle;
        private System.Windows.Forms.RichTextBox txtChatLog;
        private System.Windows.Forms.Label lblTyping;
        private System.Windows.Forms.TextBox txtInputMessage;
        private System.Windows.Forms.Button btnSend;
    }
}
