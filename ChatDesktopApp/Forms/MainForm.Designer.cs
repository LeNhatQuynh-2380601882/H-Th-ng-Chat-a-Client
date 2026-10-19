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
            this.components = new System.ComponentModel.Container();
            this.pnlLeft = new System.Windows.Forms.Panel();
            this.txtSearchUser = new System.Windows.Forms.TextBox();
            this.btnLogout = new System.Windows.Forms.Button();
            this.lstUsers = new System.Windows.Forms.ListBox();
            this.lblOnlineHeader = new System.Windows.Forms.Label();
            this.lblConnectionStatus = new System.Windows.Forms.Label();
            this.lblCurrentUser = new System.Windows.Forms.Label();
            this.pnlChatHeader = new System.Windows.Forms.Panel();
            this.txtSearchMessage = new System.Windows.Forms.TextBox();
            this.lblTargetSubtitle = new System.Windows.Forms.Label();
            this.lblTargetChat = new System.Windows.Forms.Label();
            this.txtChatLog = new System.Windows.Forms.RichTextBox();
            this.ctxChatLogMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.menuItemDownload = new System.Windows.Forms.ToolStripMenuItem();
            this.menuItemReply = new System.Windows.Forms.ToolStripMenuItem();
            this.menuItemCopy = new System.Windows.Forms.ToolStripMenuItem();
            this.pnlReply = new System.Windows.Forms.Panel();
            this.btnCancelReply = new System.Windows.Forms.Button();
            this.lblReplyPreview = new System.Windows.Forms.Label();
            this.lblTyping = new System.Windows.Forms.Label();
            this.txtInputMessage = new System.Windows.Forms.TextBox();
            this.btnSend = new System.Windows.Forms.Button();
            this.btnSendFile = new System.Windows.Forms.Button();
            this.btnEmoji = new System.Windows.Forms.Button();
            this.notifyIconApp = new System.Windows.Forms.NotifyIcon(this.components);
            this.pnlLeft.SuspendLayout();
            this.pnlChatHeader.SuspendLayout();
            this.ctxChatLogMenu.SuspendLayout();
            this.pnlReply.SuspendLayout();
            this.SuspendLayout();
            // 
            // pnlLeft
            // 
            this.pnlLeft.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(22)))), ((int)(((byte)(29)))), ((int)(((byte)(43)))));
            this.pnlLeft.Controls.Add(this.txtSearchUser);
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
            // txtSearchUser
            // 
            this.txtSearchUser.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtSearchUser.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(15)))), ((int)(((byte)(23)))), ((int)(((byte)(42)))));
            this.txtSearchUser.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtSearchUser.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.txtSearchUser.ForeColor = System.Drawing.Color.White;
            this.txtSearchUser.Location = new System.Drawing.Point(12, 95);
            this.txtSearchUser.Name = "txtSearchUser";
            this.txtSearchUser.PlaceholderText = "🔍 Tìm bạn bè...";
            this.txtSearchUser.Size = new System.Drawing.Size(196, 23);
            this.txtSearchUser.TabIndex = 2;
            this.txtSearchUser.TextChanged += new System.EventHandler(this.txtSearchUser_TextChanged);
            // 
            // btnLogout
            // 
            this.btnLogout.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(190)))), ((int)(((byte)(18)))), ((int)(((byte)(60)))));
            this.btnLogout.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnLogout.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.btnLogout.FlatAppearance.BorderSize = 0;
            this.btnLogout.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(225)))), ((int)(((byte)(29)))), ((int)(((byte)(72)))));
            this.btnLogout.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(159)))), ((int)(((byte)(18)))), ((int)(((byte)(57)))));
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
            this.lstUsers.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(41)))), ((int)(((byte)(59)))));
            this.lstUsers.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.lstUsers.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular);
            this.lstUsers.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(226)))), ((int)(((byte)(232)))), ((int)(((byte)(240)))));
            this.lstUsers.FormattingEnabled = true;
            this.lstUsers.ItemHeight = 17;
            this.lstUsers.Location = new System.Drawing.Point(12, 124);
            this.lstUsers.Name = "lstUsers";
            this.lstUsers.Size = new System.Drawing.Size(196, 316);
            this.lstUsers.TabIndex = 3;
            this.lstUsers.SelectedIndexChanged += new System.EventHandler(this.lstUsers_SelectedIndexChanged);
            // 
            // lblOnlineHeader
            // 
            this.lblOnlineHeader.AutoSize = true;
            this.lblOnlineHeader.Font = new System.Drawing.Font("Segoe UI", 8.5F, System.Drawing.FontStyle.Bold);
            this.lblOnlineHeader.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(148)))), ((int)(((byte)(163)))), ((int)(((byte)(184)))));
            this.lblOnlineHeader.Location = new System.Drawing.Point(12, 75);
            this.lblOnlineHeader.Name = "lblOnlineHeader";
            this.lblOnlineHeader.Size = new System.Drawing.Size(126, 15);
            this.lblOnlineHeader.TabIndex = 1;
            this.lblOnlineHeader.Text = "DANH SÁCH ONLINE:";
            // 
            // lblConnectionStatus
            // 
            this.lblConnectionStatus.AutoSize = true;
            this.lblConnectionStatus.Font = new System.Drawing.Font("Segoe UI", 8.5F, System.Drawing.FontStyle.Bold);
            this.lblConnectionStatus.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(211)))), ((int)(((byte)(153)))));
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
            this.lblCurrentUser.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(241)))), ((int)(((byte)(245)))), ((int)(((byte)(249)))));
            this.lblCurrentUser.Location = new System.Drawing.Point(10, 15);
            this.lblCurrentUser.Name = "lblCurrentUser";
            this.lblCurrentUser.Size = new System.Drawing.Size(200, 24);
            this.lblCurrentUser.TabIndex = 0;
            this.lblCurrentUser.Text = "👤 Bạn: [...]";
            // 
            // pnlChatHeader
            // 
            this.pnlChatHeader.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(41)))), ((int)(((byte)(59)))));
            this.pnlChatHeader.Controls.Add(this.txtSearchMessage);
            this.pnlChatHeader.Controls.Add(this.lblTargetSubtitle);
            this.pnlChatHeader.Controls.Add(this.lblTargetChat);
            this.pnlChatHeader.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlChatHeader.Location = new System.Drawing.Point(220, 0);
            this.pnlChatHeader.Name = "pnlChatHeader";
            this.pnlChatHeader.Padding = new System.Windows.Forms.Padding(16, 10, 16, 10);
            this.pnlChatHeader.Size = new System.Drawing.Size(614, 60);
            this.pnlChatHeader.TabIndex = 1;
            // 
            // txtSearchMessage
            // 
            this.txtSearchMessage.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.txtSearchMessage.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(15)))), ((int)(((byte)(23)))), ((int)(((byte)(42)))));
            this.txtSearchMessage.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtSearchMessage.Font = new System.Drawing.Font("Segoe UI", 8.5F);
            this.txtSearchMessage.ForeColor = System.Drawing.Color.White;
            this.txtSearchMessage.Location = new System.Drawing.Point(440, 18);
            this.txtSearchMessage.Name = "txtSearchMessage";
            this.txtSearchMessage.PlaceholderText = "🔍 Tìm tin nhắn...";
            this.txtSearchMessage.Size = new System.Drawing.Size(160, 23);
            this.txtSearchMessage.TabIndex = 2;
            this.txtSearchMessage.TextChanged += new System.EventHandler(this.txtSearchMessage_TextChanged);
            // 
            // lblTargetSubtitle
            // 
            this.lblTargetSubtitle.AutoSize = true;
            this.lblTargetSubtitle.Font = new System.Drawing.Font("Segoe UI", 8.5F);
            this.lblTargetSubtitle.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(148)))), ((int)(((byte)(163)))), ((int)(((byte)(184)))));
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
            this.lblTargetChat.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(248)))), ((int)(((byte)(250)))), ((int)(((byte)(252)))));
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
            this.txtChatLog.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(15)))), ((int)(((byte)(23)))), ((int)(((byte)(42)))));
            this.txtChatLog.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.txtChatLog.ContextMenuStrip = this.ctxChatLogMenu;
            this.txtChatLog.Font = new System.Drawing.Font("Segoe UI Emoji", 10F);
            this.txtChatLog.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(241)))), ((int)(((byte)(245)))), ((int)(((byte)(249)))));
            this.txtChatLog.Location = new System.Drawing.Point(232, 66);
            this.txtChatLog.Name = "txtChatLog";
            this.txtChatLog.ReadOnly = true;
            this.txtChatLog.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Vertical;
            this.txtChatLog.Size = new System.Drawing.Size(590, 355);
            this.txtChatLog.TabIndex = 2;
            this.txtChatLog.Text = "";
            this.txtChatLog.LinkClicked += new System.Windows.Forms.LinkClickedEventHandler(this.txtChatLog_LinkClicked);
            this.txtChatLog.DoubleClick += new System.EventHandler(this.txtChatLog_DoubleClick);
            this.txtChatLog.MouseClick += new System.Windows.Forms.MouseEventHandler(this.txtChatLog_MouseClick);
            this.txtChatLog.MouseMove += new System.Windows.Forms.MouseEventHandler(this.txtChatLog_MouseMove);
            // 
            // ctxChatLogMenu
            // 
            this.ctxChatLogMenu.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(41)))), ((int)(((byte)(59)))));
            this.ctxChatLogMenu.ForeColor = System.Drawing.Color.White;
            this.ctxChatLogMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.menuItemDownload,
            this.menuItemReply,
            this.menuItemCopy});
            this.ctxChatLogMenu.Name = "ctxChatLogMenu";
            this.ctxChatLogMenu.Size = new System.Drawing.Size(215, 70);
            this.ctxChatLogMenu.Opening += new System.ComponentModel.CancelEventHandler(this.ctxChatLogMenu_Opening);
            // 
            // menuItemDownload
            // 
            this.menuItemDownload.ForeColor = System.Drawing.Color.White;
            this.menuItemDownload.Name = "menuItemDownload";
            this.menuItemDownload.Size = new System.Drawing.Size(214, 22);
            this.menuItemDownload.Text = "💾 Tải / Lưu tệp tin này...";
            this.menuItemDownload.Click += new System.EventHandler(this.menuItemDownload_Click);
            // 
            // menuItemReply
            // 
            this.menuItemReply.ForeColor = System.Drawing.Color.White;
            this.menuItemReply.Name = "menuItemReply";
            this.menuItemReply.Size = new System.Drawing.Size(214, 22);
            this.menuItemReply.Text = "↩️ Trả lời tin nhắn này";
            this.menuItemReply.Click += new System.EventHandler(this.menuItemReply_Click);
            // 
            // menuItemCopy
            // 
            this.menuItemCopy.ForeColor = System.Drawing.Color.White;
            this.menuItemCopy.Name = "menuItemCopy";
            this.menuItemCopy.Size = new System.Drawing.Size(214, 22);
            this.menuItemCopy.Text = "📋 Sao chép nội dung";
            this.menuItemCopy.Click += new System.EventHandler(this.menuItemCopy_Click);
            // 
            // pnlReply
            // 
            this.pnlReply.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.pnlReply.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(41)))), ((int)(((byte)(59)))));
            this.pnlReply.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.pnlReply.Controls.Add(this.btnCancelReply);
            this.pnlReply.Controls.Add(this.lblReplyPreview);
            this.pnlReply.Location = new System.Drawing.Point(232, 423);
            this.pnlReply.Name = "pnlReply";
            this.pnlReply.Size = new System.Drawing.Size(590, 26);
            this.pnlReply.TabIndex = 8;
            this.pnlReply.Visible = false;
            // 
            // lblReplyPreview
            // 
            this.lblReplyPreview.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lblReplyPreview.AutoEllipsis = true;
            this.lblReplyPreview.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Italic);
            this.lblReplyPreview.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(96)))), ((int)(((byte)(165)))), ((int)(((byte)(250)))));
            this.lblReplyPreview.Location = new System.Drawing.Point(6, 3);
            this.lblReplyPreview.Name = "lblReplyPreview";
            this.lblReplyPreview.Size = new System.Drawing.Size(550, 18);
            this.lblReplyPreview.TabIndex = 0;
            this.lblReplyPreview.Text = "↩️ Đang trả lời...";
            // 
            // btnCancelReply
            // 
            this.btnCancelReply.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancelReply.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnCancelReply.FlatAppearance.BorderSize = 0;
            this.btnCancelReply.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(51)))), ((int)(((byte)(65)))), ((int)(((byte)(85)))));
            this.btnCancelReply.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnCancelReply.Font = new System.Drawing.Font("Segoe UI", 8F, System.Drawing.FontStyle.Bold);
            this.btnCancelReply.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(148)))), ((int)(((byte)(163)))), ((int)(((byte)(184)))));
            this.btnCancelReply.Location = new System.Drawing.Point(560, 1);
            this.btnCancelReply.Name = "btnCancelReply";
            this.btnCancelReply.Size = new System.Drawing.Size(24, 22);
            this.btnCancelReply.TabIndex = 1;
            this.btnCancelReply.Text = "✕";
            this.btnCancelReply.UseVisualStyleBackColor = true;
            this.btnCancelReply.Click += new System.EventHandler(this.btnCancelReply_Click);
            // 
            // lblTyping
            // 
            this.lblTyping.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.lblTyping.AutoSize = true;
            this.lblTyping.Font = new System.Drawing.Font("Segoe UI", 8.5F, System.Drawing.FontStyle.Italic);
            this.lblTyping.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(211)))), ((int)(((byte)(153)))));
            this.lblTyping.Location = new System.Drawing.Point(232, 427);
            this.lblTyping.Name = "lblTyping";
            this.lblTyping.Size = new System.Drawing.Size(0, 15);
            this.lblTyping.TabIndex = 3;
            // 
            // txtInputMessage
            // 
            this.txtInputMessage.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtInputMessage.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(41)))), ((int)(((byte)(59)))));
            this.txtInputMessage.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtInputMessage.Font = new System.Drawing.Font("Segoe UI Emoji", 10.5F);
            this.txtInputMessage.ForeColor = System.Drawing.Color.White;
            this.txtInputMessage.Location = new System.Drawing.Point(328, 454);
            this.txtInputMessage.Name = "txtInputMessage";
            this.txtInputMessage.PlaceholderText = "Nhập nội dung tin nhắn... (Nhấn Enter để gửi)";
            this.txtInputMessage.Size = new System.Drawing.Size(394, 26);
            this.txtInputMessage.TabIndex = 4;
            this.txtInputMessage.TextChanged += new System.EventHandler(this.txtInputMessage_TextChanged);
            this.txtInputMessage.KeyDown += new System.Windows.Forms.KeyEventHandler(this.txtInputMessage_KeyDown);
            // 
            // btnSend
            // 
            this.btnSend.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSend.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(59)))), ((int)(((byte)(130)))), ((int)(((byte)(246)))));
            this.btnSend.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnSend.FlatAppearance.BorderSize = 0;
            this.btnSend.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(37)))), ((int)(((byte)(99)))), ((int)(((byte)(235)))));
            this.btnSend.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(29)))), ((int)(((byte)(78)))), ((int)(((byte)(216)))));
            this.btnSend.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnSend.Font = new System.Drawing.Font("Segoe UI", 9.5F, System.Drawing.FontStyle.Bold);
            this.btnSend.ForeColor = System.Drawing.Color.White;
            this.btnSend.Location = new System.Drawing.Point(730, 452);
            this.btnSend.Name = "btnSend";
            this.btnSend.Size = new System.Drawing.Size(92, 32);
            this.btnSend.TabIndex = 5;
            this.btnSend.Text = "GỬI";
            this.btnSend.UseVisualStyleBackColor = false;
            this.btnSend.Click += new System.EventHandler(this.btnSend_Click);
            // 
            // btnSendFile
            // 
            this.btnSendFile.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnSendFile.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(13)))), ((int)(((byte)(148)))), ((int)(((byte)(136)))));
            this.btnSendFile.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnSendFile.FlatAppearance.BorderSize = 0;
            this.btnSendFile.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(15)))), ((int)(((byte)(118)))), ((int)(((byte)(110)))));
            this.btnSendFile.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(17)))), ((int)(((byte)(94)))), ((int)(((byte)(89)))));
            this.btnSendFile.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnSendFile.Font = new System.Drawing.Font("Segoe UI Emoji", 12F, System.Drawing.FontStyle.Regular);
            this.btnSendFile.ForeColor = System.Drawing.Color.White;
            this.btnSendFile.Location = new System.Drawing.Point(232, 452);
            this.btnSendFile.Name = "btnSendFile";
            this.btnSendFile.Size = new System.Drawing.Size(42, 32);
            this.btnSendFile.TabIndex = 6;
            this.btnSendFile.Text = "📎";
            this.btnSendFile.UseVisualStyleBackColor = false;
            this.btnSendFile.Click += new System.EventHandler(this.btnSendFile_Click);
            // 
            // btnEmoji
            // 
            this.btnEmoji.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnEmoji.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(217)))), ((int)(((byte)(119)))), ((int)(((byte)(6)))));
            this.btnEmoji.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnEmoji.FlatAppearance.BorderSize = 0;
            this.btnEmoji.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(180)))), ((int)(((byte)(83)))), ((int)(((byte)(9)))));
            this.btnEmoji.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(146)))), ((int)(((byte)(64)))), ((int)(((byte)(14)))));
            this.btnEmoji.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnEmoji.Font = new System.Drawing.Font("Segoe UI Emoji", 12F, System.Drawing.FontStyle.Regular);
            this.btnEmoji.ForeColor = System.Drawing.Color.White;
            this.btnEmoji.Location = new System.Drawing.Point(280, 452);
            this.btnEmoji.Name = "btnEmoji";
            this.btnEmoji.Size = new System.Drawing.Size(42, 32);
            this.btnEmoji.TabIndex = 7;
            this.btnEmoji.Text = "😊";
            this.btnEmoji.UseVisualStyleBackColor = false;
            this.btnEmoji.Click += new System.EventHandler(this.btnEmoji_Click);
            // 
            // notifyIconApp
            // 
            this.notifyIconApp.Text = "ChatDesktopApp";
            this.notifyIconApp.Visible = true;
            this.notifyIconApp.DoubleClick += new System.EventHandler(this.notifyIconApp_DoubleClick);
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(15)))), ((int)(((byte)(23)))), ((int)(((byte)(42)))));
            this.ClientSize = new System.Drawing.Size(834, 501);
            this.Controls.Add(this.pnlReply);
            this.Controls.Add(this.btnEmoji);
            this.Controls.Add(this.btnSendFile);
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
            this.ctxChatLogMenu.ResumeLayout(false);
            this.pnlReply.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private System.Windows.Forms.Panel pnlLeft;
        private System.Windows.Forms.Label lblCurrentUser;
        private System.Windows.Forms.Label lblConnectionStatus;
        private System.Windows.Forms.Label lblOnlineHeader;
        private System.Windows.Forms.TextBox txtSearchUser;
        private System.Windows.Forms.ListBox lstUsers;
        private System.Windows.Forms.Button btnLogout;
        private System.Windows.Forms.Panel pnlChatHeader;
        private System.Windows.Forms.TextBox txtSearchMessage;
        private System.Windows.Forms.Label lblTargetChat;
        private System.Windows.Forms.Label lblTargetSubtitle;
        private System.Windows.Forms.RichTextBox txtChatLog;
        private System.Windows.Forms.Label lblTyping;
        private System.Windows.Forms.TextBox txtInputMessage;
        private System.Windows.Forms.Button btnSend;
        private System.Windows.Forms.Button btnSendFile;
        private System.Windows.Forms.Button btnEmoji;
        private System.Windows.Forms.NotifyIcon notifyIconApp;
        private System.Windows.Forms.Panel pnlReply;
        private System.Windows.Forms.Label lblReplyPreview;
        private System.Windows.Forms.Button btnCancelReply;
        private System.Windows.Forms.ContextMenuStrip ctxChatLogMenu;
        private System.Windows.Forms.ToolStripMenuItem menuItemDownload;
        private System.Windows.Forms.ToolStripMenuItem menuItemReply;
        private System.Windows.Forms.ToolStripMenuItem menuItemCopy;
    }
}
