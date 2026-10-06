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
            this.lblCurrentUser = new System.Windows.Forms.Label();
            this.lblTargetChat = new System.Windows.Forms.Label();
            this.lblUserListHeader = new System.Windows.Forms.Label();
            this.lstUsers = new System.Windows.Forms.ListBox();
            this.txtChatLog = new System.Windows.Forms.TextBox();
            this.txtInputMessage = new System.Windows.Forms.TextBox();
            this.btnSend = new System.Windows.Forms.Button();
            this.lblTyping = new System.Windows.Forms.Label();
            this.SuspendLayout();

            // lblCurrentUser
            this.lblCurrentUser.AutoSize = true;
            this.lblCurrentUser.Font = new System.Drawing.Font("Segoe UI", 11F, System.Drawing.FontStyle.Bold);
            this.lblCurrentUser.ForeColor = System.Drawing.Color.DarkBlue;
            this.lblCurrentUser.Location = new System.Drawing.Point(15, 15);
            this.lblCurrentUser.Name = "lblCurrentUser";
            this.lblCurrentUser.Size = new System.Drawing.Size(95, 20);
            this.lblCurrentUser.TabIndex = 0;
            this.lblCurrentUser.Text = "User: [...]";

            // lblTargetChat
            this.lblTargetChat.AutoSize = true;
            this.lblTargetChat.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.lblTargetChat.ForeColor = System.Drawing.Color.DimGray;
            this.lblTargetChat.Location = new System.Drawing.Point(220, 16);
            this.lblTargetChat.Name = "lblTargetChat";
            this.lblTargetChat.Size = new System.Drawing.Size(165, 19);
            this.lblTargetChat.TabIndex = 1;
            this.lblTargetChat.Text = "Phòng Chat: TẤT CẢ (ALL)";

            // lblUserListHeader
            this.lblUserListHeader.AutoSize = true;
            this.lblUserListHeader.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.lblUserListHeader.Location = new System.Drawing.Point(15, 50);
            this.lblUserListHeader.Name = "lblUserListHeader";
            this.lblUserListHeader.Size = new System.Drawing.Size(135, 15);
            this.lblUserListHeader.TabIndex = 2;
            this.lblUserListHeader.Text = "DANH SÁCH ONLINE:";

            // lstUsers
            this.lstUsers.FormattingEnabled = true;
            this.lstUsers.ItemHeight = 15;
            this.lstUsers.Location = new System.Drawing.Point(15, 70);
            this.lstUsers.Name = "lstUsers";
            this.lstUsers.Size = new System.Drawing.Size(180, 334);
            this.lstUsers.TabIndex = 3;
            this.lstUsers.SelectedIndexChanged += new System.EventHandler(this.lstUsers_SelectedIndexChanged);

            // txtChatLog
            this.txtChatLog.BackColor = System.Drawing.Color.White;
            this.txtChatLog.Font = new System.Drawing.Font("Consolas", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.txtChatLog.Location = new System.Drawing.Point(215, 45);
            this.txtChatLog.Multiline = true;
            this.txtChatLog.Name = "txtChatLog";
            this.txtChatLog.ReadOnly = true;
            this.txtChatLog.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtChatLog.Size = new System.Drawing.Size(550, 320);
            this.txtChatLog.TabIndex = 4;

            // lblTyping
            this.lblTyping.AutoSize = true;
            this.lblTyping.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Italic);
            this.lblTyping.ForeColor = System.Drawing.Color.Green;
            this.lblTyping.Location = new System.Drawing.Point(215, 370);
            this.lblTyping.Name = "lblTyping";
            this.lblTyping.Size = new System.Drawing.Size(0, 13);
            this.lblTyping.TabIndex = 5;

            // txtInputMessage
            this.txtInputMessage.Location = new System.Drawing.Point(215, 385);
            this.txtInputMessage.Name = "txtInputMessage";
            this.txtInputMessage.Size = new System.Drawing.Size(450, 23);
            this.txtInputMessage.TabIndex = 6;
            this.txtInputMessage.TextChanged += new System.EventHandler(this.txtInputMessage_TextChanged);

            // btnSend
            this.btnSend.BackColor = System.Drawing.Color.RoyalBlue;
            this.btnSend.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.btnSend.ForeColor = System.Drawing.Color.White;
            this.btnSend.Location = new System.Drawing.Point(675, 384);
            this.btnSend.Name = "btnSend";
            this.btnSend.Size = new System.Drawing.Size(90, 25);
            this.btnSend.TabIndex = 7;
            this.btnSend.Text = "GỬI";
            this.btnSend.UseVisualStyleBackColor = false;
            this.btnSend.Click += new System.EventHandler(this.btnSend_Click);

            // MainForm
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(780, 420);
            this.Controls.Add(this.btnSend);
            this.Controls.Add(this.txtInputMessage);
            this.Controls.Add(this.lblTyping);
            this.Controls.Add(this.txtChatLog);
            this.Controls.Add(this.lstUsers);
            this.Controls.Add(this.lblUserListHeader);
            this.Controls.Add(this.lblTargetChat);
            this.Controls.Add(this.lblCurrentUser);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Phòng Chat - System Client (TCP Socket)";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainForm_FormClosing);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private System.Windows.Forms.Label lblCurrentUser;
        private System.Windows.Forms.Label lblTargetChat;
        private System.Windows.Forms.Label lblUserListHeader;
        private System.Windows.Forms.ListBox lstUsers;
        private System.Windows.Forms.TextBox txtChatLog;
        private System.Windows.Forms.Label lblTyping;
        private System.Windows.Forms.TextBox txtInputMessage;
        private System.Windows.Forms.Button btnSend;
    }
}
