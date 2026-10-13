/**
 * app.js – Tầng Giao diện (Presentation Layer)
 * ===============================================================
 * Xử lý toàn bộ tương tác DOM, kết nối WebSocketClientManager,
 * quản lý trạng thái UI và hiển thị tin nhắn.
 *
 * Các vấn đề đã xử lý:
 *  ✅ Sửa lỗi tin nhắn chat nhóm bị lặp đôi (duplicate)
 *  ✅ Debounce gửi trạng thái TYPING (tối đa 1 gói / 2.5s)
 *  ✅ Lịch sử tin nhắn tách biệt theo target (ALL / từng user)
 *  ✅ Số tin nhắn chưa đọc trên sidebar
 *  ✅ Chế độ Demo không cần Server
 *  ✅ Hiển thị trạng thái kết nối realtime
 *  ✅ Xuất lịch sử chat ra file .txt
 *  ✅ Gửi / nhận tệp tin và hình ảnh (FILE_TRANSFER – Base64, tối đa 5MB)
 *  ✅ Tự động nhận diện và render hyperlink URL trong tin nhắn
 *
 * Người thực hiện: Trương Hoàng Phúc – MSSV 2380601760
 * Môn: Lập Trình Mạng – Nhóm 11
 */
document.addEventListener('DOMContentLoaded', () => {

    /* ════════════════════════════════════════════════
       0. KHỞI TẠO
    ════════════════════════════════════════════════ */
    const wsManager = new WebSocketClientManager();

    /* ── DOM References ── */
    const loginScreen     = document.getElementById('loginScreen');
    const chatScreen      = document.getElementById('chatScreen');
    const loginForm       = document.getElementById('loginForm');
    const usernameInput   = document.getElementById('usernameInput');
    const serverIpInput   = document.getElementById('serverIp');
    const serverPortInput = document.getElementById('serverPort');
    const chkDemo         = document.getElementById('chkDemo');
    const btnLogin        = document.getElementById('btnLogin');
    const loginStatus     = document.getElementById('loginStatus');

    const displayMyName   = document.getElementById('displayMyName');
    const myAvatar        = document.getElementById('myAvatar');
    const btnLogout       = document.getElementById('btnLogout');
    const userList        = document.getElementById('userList');
    const searchUser      = document.getElementById('searchUser');

    const chatHeaderTitle  = document.getElementById('chatHeaderTitle');
    const chatHeaderAvatar = document.getElementById('chatHeaderAvatar');
    const typingIndicator  = document.getElementById('typingIndicator');
    const chatMessages     = document.getElementById('chatMessages');
    const messageForm      = document.getElementById('messageForm');
    const messageInput     = document.getElementById('messageInput');
    const btnAttachFile    = document.getElementById('btnAttachFile');
    const fileInput        = document.getElementById('fileInput');
    const btnToggleSound   = document.getElementById('btnToggleSound');
    const soundIcon        = document.getElementById('soundIcon');
    const btnExportChat    = document.getElementById('btnExportChat');
    const pingValue        = document.getElementById('pingValue');
    const tokenStatus      = document.getElementById('tokenStatus');
    const emojiBar         = document.getElementById('emojiBar');
    const wsStatusDot      = document.getElementById('wsStatusDot');
    const wsStatusText     = document.getElementById('wsStatusText');

    /* ── Trạng thái ứng dụng ── */
    let activeTarget   = 'ALL';           // target đang chọn trong sidebar
    let soundEnabled   = true;

    // Lưu lịch sử riêng theo từng target để tránh duplicate & hỗ trợ lọc
    // chatLog[target] = [ {msg object}, ... ]
    const chatLog = { ALL: [] };

    // Số tin chưa đọc [target] = count
    const unreadCounts = {};

    // Debounce TYPING
    let typingDebounceTimer = null;
    const TYPING_DEBOUNCE_MS = 2500;

    // Typing display timer
    let typingDisplayTimer = null;

    /* ════════════════════════════════════════════════
       1. ÂM THANH THÔNG BÁO (Web Audio API)
    ════════════════════════════════════════════════ */
    function playNotificationSound() {
        if (!soundEnabled) return;
        try {
            const ctx  = new (window.AudioContext || window.webkitAudioContext)();
            const osc  = ctx.createOscillator();
            const gain = ctx.createGain();
            osc.type = 'sine';
            osc.frequency.setValueAtTime(587.33, ctx.currentTime);      // D5
            osc.frequency.exponentialRampToValueAtTime(880, ctx.currentTime + 0.12); // A5
            gain.gain.setValueAtTime(0.15, ctx.currentTime);
            gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + 0.28);
            osc.connect(gain);
            gain.connect(ctx.destination);
            osc.start();
            osc.stop(ctx.currentTime + 0.28);
        } catch (_) {/* AudioContext bị khóa trước khi có tương tác người dùng */}
    }

    /* ════════════════════════════════════════════════
       2. ĐIỀU KHIỂN NÚT ÂM THANH
    ════════════════════════════════════════════════ */
    if (btnToggleSound) {
        btnToggleSound.addEventListener('click', () => {
            soundEnabled = !soundEnabled;
            if (soundEnabled) {
                soundIcon.className = 'fa-solid fa-bell';
                btnToggleSound.classList.replace('btn-outline-danger', 'btn-outline-secondary');
            } else {
                soundIcon.className = 'fa-solid fa-bell-slash text-danger';
                btnToggleSound.classList.replace('btn-outline-secondary', 'btn-outline-danger');
            }
        });
    }

    /* ════════════════════════════════════════════════
       3. THANH EMOJI NHANH
    ════════════════════════════════════════════════ */
    if (emojiBar) {
        emojiBar.querySelectorAll('.emoji-btn').forEach(btn => {
            btn.addEventListener('click', () => {
                const emoji = btn.getAttribute('data-emoji');
                if (emoji && messageInput) {
                    messageInput.value += emoji;
                    messageInput.focus();
                }
            });
        });
    }

    /* ════════════════════════════════════════════════
       4. XUẤT LỊCH SỬ CHAT
    ════════════════════════════════════════════════ */
    if (btnExportChat) {
        btnExportChat.addEventListener('click', () => {
            const allMessages = Object.values(chatLog).flat();
            allMessages.sort((a, b) => (a.timestamp || 0) - (b.timestamp || 0));
            if (allMessages.length === 0) {
                alert('Chưa có tin nhắn nào để xuất lịch sử!');
                return;
            }
            const lines = allMessages.map(m => {
                const t = new Date(m.timestamp || Date.now()).toLocaleString('vi-VN');
                let type = '[Cá nhân]';
                if (m.action === 'CHAT_GROUP') type = '[Nhóm]';
                else if (m.action === 'FILE_TRANSFER') type = '[File/Ảnh]';
                let content = m.content;
                if (m.action === 'FILE_TRANSFER') {
                    try {
                        const p = typeof m.content === 'string' ? JSON.parse(m.content) : m.content;
                        content = p.fileName ? `[File: ${p.fileName}, ${p.fileSize ? (p.fileSize / 1024).toFixed(1) + ' KB' : ''}]` : m.content;
                    } catch (_) {}
                }
                return `[${t}] ${type} ${m.sender} → ${m.receiver}: ${content}`;
            });
            const blob = new Blob([lines.join('\n')], { type: 'text/plain;charset=utf-8' });
            const url  = URL.createObjectURL(blob);
            const a    = document.createElement('a');
            a.href     = url;
            a.download = `chat-log-${Date.now()}.txt`;
            a.click();
            URL.revokeObjectURL(url);
        });
    }

    /* ════════════════════════════════════════════════
       5. ĐĂNG NHẬP
    ════════════════════════════════════════════════ */
    loginForm.addEventListener('submit', (e) => {
        e.preventDefault();
        const username = usernameInput.value.trim();
        const host     = serverIpInput.value.trim();
        const port     = serverPortInput.value.trim();
        const demo     = chkDemo?.checked || false;

        if (!username) {
            setLoginStatus('Vui lòng nhập tên người dùng!', 'danger');
            return;
        }

        btnLogin.disabled = true;
        setLoginStatus(demo
            ? '⚡ Đang khởi động chế độ Demo (offline)…'
            : `🔗 Đang kết nối tới ws://${host}:${port}…`, 'info');

        wsManager.connect(host, port, username, demo);
    });

    function setLoginStatus(msg, type = 'info') {
        loginStatus.className = `mt-3 small text-${type}`;
        loginStatus.textContent = msg;
        loginStatus.classList.remove('display-none');
    }

    /* ════════════════════════════════════════════════
       6. WEBSOCKET CALLBACKS
    ════════════════════════════════════════════════ */

    /* ── 6a. Trạng thái kết nối ── */
    wsManager.onStatusChange = (status) => {
        if (!wsStatusDot || !wsStatusText) return;
        wsStatusDot.className = 'status-dot';
        switch (status) {
            case 'connecting':
                wsStatusDot.classList.add('connecting');
                wsStatusText.textContent = 'Đang kết nối…';
                break;
            case 'connected':
                wsStatusDot.classList.add('online');
                wsStatusText.textContent = 'Đã kết nối';
                break;
            case 'disconnected':
                wsStatusDot.classList.add('offline');
                wsStatusText.textContent = 'Mất kết nối';
                break;
        }
    };

    /* ── 6b. Đăng nhập thành công ── */
    wsManager.onLoginSuccess = (msg) => {
        loginScreen.classList.add('display-none');
        chatScreen.classList.remove('display-none');

        displayMyName.textContent = wsManager.username;
        myAvatar.textContent      = wsManager.username.charAt(0).toUpperCase();

        if (tokenStatus && wsManager.token) {
            tokenStatus.textContent = `Token: ${wsManager.token.substring(0, 8)}…`;
        }

        appendSystemMessage('Đã kết nối. Chào mừng ' + wsManager.username + ' 👋');
    };

    /* ── 6c. Đo độ trễ ── */
    wsManager.onLatencyUpdate = (latency) => {
        if (!pingValue) return;
        pingValue.textContent = `${latency} ms`;
        // Màu theo ngưỡng
        const badge = pingValue.parentElement;
        if (!badge) return;
        badge.classList.remove('text-success', 'text-warning', 'text-danger', 'text-info');
        if (latency < 50)       badge.classList.add('text-success');
        else if (latency < 150) badge.classList.add('text-warning');
        else                    badge.classList.add('text-danger');
    };

    /* ── 6d. Cập nhật danh sách người dùng online ── */
    wsManager.onUserListUpdated = (users) => {
        renderUserList(users);
    };

    /* ── 6e. Nhận tin nhắn ── */
    wsManager.onMessageReceived = (msg) => {
        // ── FIX: Tránh duplicate tin nhắn ──
        // Server broadcast CHAT_GROUP về cho TẤT CẢ kể cả người gửi.
        // app.js đã tự append tin của mình trong messageForm.submit → bỏ qua ở đây.
        // Trường _demo=true là echo từ Demo mode → cũng bỏ qua (đã append thủ công).
        if (msg._self || msg._demo) return;

        const key = _chatKey(msg);
        if (!chatLog[key]) chatLog[key] = [];
        chatLog[key].push(msg);

        if (key === activeTarget) {
            renderBubble(msg, false);
        } else {
            // Tin đến từ target không đang xem → tăng badge chưa đọc
            unreadCounts[key] = (unreadCounts[key] || 0) + 1;
            _updateUnreadBadge(key);
            playNotificationSound();
        }

        // Chỉ phát âm thanh nếu tin là từ người khác VÀ đang ở target này
        if (key === activeTarget && msg.sender !== wsManager.username) {
            playNotificationSound();
        }
    };

    /* ── 6f. Nhận tệp tin / hình ảnh (FILE_TRANSFER) ── */
    wsManager.onFileReceived = (msg) => {
        if (msg._self || msg._demo) return;

        const key = _chatKey(msg);
        if (!chatLog[key]) chatLog[key] = [];
        chatLog[key].push(msg);

        if (key === activeTarget) {
            renderBubble(msg, false);
        } else {
            unreadCounts[key] = (unreadCounts[key] || 0) + 1;
            _updateUnreadBadge(key);
            playNotificationSound();
        }

        if (key === activeTarget && msg.sender !== wsManager.username) {
            playNotificationSound();
        }
    };

    /* ── 6g. Trạng thái đang gõ ── */
    wsManager.onTyping = (msg) => {
        if (msg.sender === wsManager.username) return;
        typingIndicator.textContent = `${msg.sender} đang nhập tin nhắn…`;
        clearTimeout(typingDisplayTimer);
        typingDisplayTimer = setTimeout(() => {
            typingIndicator.textContent = '';
        }, 3000);
    };

    /* ── 6g. Lỗi ── */
    wsManager.onError = (errMsg) => {
        btnLogin.disabled = false;
        setLoginStatus(`❌ Lỗi: ${errMsg}`, 'danger');
    };

    /* ── 6h. Ngắt kết nối ── */
    wsManager.onClose = () => {
        loginScreen.classList.remove('display-none');
        chatScreen.classList.add('display-none');
        btnLogin.disabled = false;
        setLoginStatus('🔌 Đã ngắt kết nối khỏi Server.', 'secondary');
        // Reset state
        activeTarget = 'ALL';
        Object.keys(chatLog).forEach(k => { chatLog[k] = []; });
        Object.keys(unreadCounts).forEach(k => { delete unreadCounts[k]; });
        chatMessages.innerHTML = '';
    };

    /* ════════════════════════════════════════════════
       7. SIDEBAR – DANH SÁCH NGƯỜI DÙNG
    ════════════════════════════════════════════════ */

    /**
     * Render (hoặc cập nhật) danh sách người dùng online trên sidebar.
     * @param {string[]} users
     */
    function renderUserList(users) {
        // Build set mới
        const others = users.filter(u => u !== wsManager.username);

        userList.innerHTML = `
            <div class="user-item p-2 rounded mb-1 d-flex align-items-center gap-2 ${activeTarget === 'ALL' ? 'active' : ''}"
                 data-target="ALL">
                <div class="avatar bg-gradient-primary text-white rounded-circle">
                    <i class="fa-solid fa-users"></i>
                </div>
                <div class="flex-grow-1">
                    <div class="fw-semibold">TẤT CẢ (ALL)</div>
                    <div class="small text-muted">Chat nhóm Broadcast</div>
                </div>
                ${_unreadBadgeHtml('ALL')}
            </div>`;

        others.forEach(u => {
            const item = document.createElement('div');
            item.className = `user-item p-2 rounded mb-1 d-flex align-items-center gap-2 ${activeTarget === u ? 'active' : ''}`;
            item.setAttribute('data-target', u);
            item.innerHTML = `
                <div class="avatar bg-secondary text-white rounded-circle fw-bold">${u.charAt(0).toUpperCase()}</div>
                <div class="flex-grow-1">
                    <div class="fw-semibold text-truncate" style="max-width:160px;">${_escHtml(u)}</div>
                    <div class="small d-flex align-items-center gap-1">
                        <span class="status-dot online"></span>
                        <span class="text-success">Online</span>
                    </div>
                </div>
                ${_unreadBadgeHtml(u)}`;
            userList.appendChild(item);
        });

        // Gắn sự kiện click
        userList.querySelectorAll('.user-item').forEach(el => {
            el.addEventListener('click', () => _switchTarget(el.getAttribute('data-target')));
        });
    }

    /** Chuyển target chat (sidebar click). */
    function _switchTarget(target) {
        if (activeTarget === target) return;
        activeTarget = target;

        // Cập nhật active class sidebar
        userList.querySelectorAll('.user-item').forEach(el => {
            el.classList.toggle('active', el.getAttribute('data-target') === target);
        });

        // Reset unread
        if (unreadCounts[target]) {
            delete unreadCounts[target];
            _updateUnreadBadge(target);
        }

        // Cập nhật header
        if (target === 'ALL') {
            chatHeaderTitle.textContent = 'Phòng Chat Nhóm (ALL)';
            chatHeaderAvatar.innerHTML  = '<i class="fa-solid fa-users"></i>';
            chatHeaderAvatar.className  = 'avatar bg-gradient-primary text-white rounded-circle fs-5';
        } else {
            chatHeaderTitle.textContent = `Chat 1-1 với ${target}`;
            chatHeaderAvatar.textContent = target.charAt(0).toUpperCase();
            chatHeaderAvatar.className   = 'avatar bg-secondary text-white rounded-circle fw-bold';
        }

        // Render lại lịch sử của target đó
        chatMessages.innerHTML = '';
        appendSystemMessage(`Bạn đang xem cuộc trò chuyện với: ${target === 'ALL' ? 'Tất cả (ALL)' : target}`);
        const history = chatLog[target] || [];
        history.forEach(m => renderBubble(m, false));
    }

    /** Tìm key lưu lịch sử: với CHAT_GROUP / FILE_TRANSFER-ALL = 'ALL', CHAT_SINGLE/FILE_TRANSFER-1v1 = tên partner. */
    function _chatKey(msg) {
        if (msg.action === 'CHAT_GROUP') return 'ALL';
        // FILE_TRANSFER gửi nhóm
        if (msg.action === 'FILE_TRANSFER' && msg.receiver === 'ALL') return 'ALL';
        // CHAT_SINGLE hoặc FILE_TRANSFER 1-1: key = partner (người kia)
        if (msg.sender === wsManager.username) return msg.receiver;
        return msg.sender;
    }

    /** Cập nhật badge số chưa đọc trên sidebar item. */
    function _updateUnreadBadge(target) {
        const el = userList.querySelector(`[data-target="${CSS.escape(target)}"] .unread-badge`);
        const count = unreadCounts[target] || 0;
        if (el) {
            if (count > 0) {
                el.textContent = count > 99 ? '99+' : String(count);
                el.classList.remove('display-none');
            } else {
                el.classList.add('display-none');
            }
        }
    }

    function _unreadBadgeHtml(target) {
        const count = unreadCounts[target] || 0;
        return `<span class="unread-badge ${count > 0 ? '' : 'display-none'}">${count > 99 ? '99+' : count}</span>`;
    }

    /* ════════════════════════════════════════════════
       8. GỬI TIN NHẮN
    ════════════════════════════════════════════════ */
    messageForm.addEventListener('submit', (e) => {
        e.preventDefault();
        const text = messageInput.value.trim();
        if (!text || !wsManager.isConnected) return;

        // Gửi lên Server / Demo
        wsManager.sendChatMessage(activeTarget, text);

        // Append ngay lập tức cho người gửi (không chờ echo từ Server)
        // Đánh dấu _self=true để callback onMessageReceived bỏ qua
        const selfMsg = {
            _self:     true,
            action:    activeTarget === 'ALL' ? 'CHAT_GROUP' : 'CHAT_SINGLE',
            sender:    wsManager.username,
            receiver:  activeTarget,
            content:   text,
            timestamp: Date.now()
        };
        // Lưu vào log
        const key = _chatKey(selfMsg);
        if (!chatLog[key]) chatLog[key] = [];
        chatLog[key].push(selfMsg);

        renderBubble(selfMsg, false);
        messageInput.value = '';
    });

    /* ════════════════════════════════════════════════
       9. TRẠNG THÁI ĐANG GÕ (Debounce)
    ════════════════════════════════════════════════ */
    messageInput.addEventListener('input', () => {
        if (!wsManager.isConnected || messageInput.value.length === 0) return;

        // Debounce: chỉ gửi TYPING sau khi người dùng dừng gõ đủ thời gian,
        // hoặc ngay lập tức nếu lần đầu (timer chưa chạy).
        if (!typingDebounceTimer) {
            wsManager.sendTypingStatus(activeTarget);
        }
        clearTimeout(typingDebounceTimer);
        typingDebounceTimer = setTimeout(() => {
            typingDebounceTimer = null;
        }, TYPING_DEBOUNCE_MS);
    });

    /* ════════════════════════════════════════════════
       10. BỘ LỌC TÌM KIẾM USER TRÊN SIDEBAR
    ════════════════════════════════════════════════ */
    if (searchUser) {
        searchUser.addEventListener('input', () => {
            const q = searchUser.value.trim().toLowerCase();
            userList.querySelectorAll('.user-item').forEach(item => {
                const t = (item.getAttribute('data-target') || '').toLowerCase();
                item.style.display = (t === 'all' || t.includes(q)) ? '' : 'none';
            });
        });
    }

    /* ════════════════════════════════════════════════
       11. ĐĂNG XUẤT
    ════════════════════════════════════════════════ */
    if (btnLogout) {
        btnLogout.addEventListener('click', () => {
            wsManager.disconnect();
        });
    }

    /* ── Đính kèm gửi File / Hình ảnh ── */
    if (btnAttachFile && fileInput) {
        btnAttachFile.addEventListener('click', () => {
            if (!wsManager.isConnected && !wsManager.demoMode) {
                alert('Chưa kết nối tới Server!');
                return;
            }
            fileInput.value = '';
            fileInput.click();
        });

        fileInput.addEventListener('change', () => {
            const file = fileInput.files[0];
            if (!file) return;

            // Giới hạn 5MB tương tự Desktop Client
            if (file.size > 5 * 1024 * 1024) {
                alert('Dung lượng tệp tin vượt quá 5MB. Vui lòng chọn tệp nhỏ hơn!');
                return;
            }

            const reader = new FileReader();
            reader.onload = (e) => {
                const base64Data = e.target.result.split(',')[1];
                const isImage = file.type.startsWith('image/') || /\.(png|jpe?g|gif|webp|bmp)$/i.test(file.name);

                wsManager.sendFileMessage(activeTarget, file.name, file.size, isImage, base64Data);

                // Append tin nhắn cục bộ cho chính mình
                const payloadObj = {
                    fileName: file.name,
                    fileSize: file.size,
                    isImage: isImage,
                    base64Data: base64Data
                };
                const selfMsg = {
                    _self: true,
                    action: 'FILE_TRANSFER',
                    sender: wsManager.username,
                    receiver: activeTarget,
                    content: JSON.stringify(payloadObj),
                    timestamp: Date.now()
                };

                const key = _chatKey(selfMsg);
                if (!chatLog[key]) chatLog[key] = [];
                chatLog[key].push(selfMsg);

                renderBubble(selfMsg, false);
            };
            reader.readAsDataURL(file);
        });
    }

    /* ════════════════════════════════════════════════
       12. RENDER TIN NHẮN
    ════════════════════════════════════════════════ */

    /**
     * Tạo bubble tin nhắn và chèn vào khung chat.
     * @param {Object} msg
     * @param {boolean} [prepend=false] – Thêm lên đầu (dành cho load lịch sử ngược)
     */
    function renderBubble(msg, prepend = false) {
        const isSelf  = msg.sender === wsManager.username;
        const timeStr = new Date(msg.timestamp || Date.now())
            .toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' });

        let typeLabel = '[Cá nhân]';
        if (msg.action === 'CHAT_GROUP') typeLabel = '[Nhóm]';
        else if (msg.action === 'FILE_TRANSFER') typeLabel = '[File/Ảnh]';

        let bodyHtml = '';
        if (msg.action === 'FILE_TRANSFER') {
            let payload = null;
            try {
                payload = typeof msg.content === 'string' ? JSON.parse(msg.content) : msg.content;
            } catch (e) {
                payload = null;
            }

            if (payload && payload.fileName) {
                const fileSizeStr = _formatFileSize(payload.fileSize);
                if (payload.isImage && payload.base64Data) {
                    bodyHtml = `
                        <div class="msg-file-image my-1">
                            <img src="data:image/png;base64,${payload.base64Data}" alt="${_escHtml(payload.fileName)}" class="chat-img-preview" onclick="window.open(this.src, '_blank')">
                            <div class="small opacity-75 mt-1 d-flex align-items-center justify-content-between">
                                <span><i class="fa-solid fa-image me-1"></i>${_escHtml(payload.fileName)}</span>
                                <span>${fileSizeStr}</span>
                            </div>
                        </div>`;
                } else if (payload.base64Data) {
                    bodyHtml = `
                        <div class="msg-file-card p-2 d-flex align-items-center gap-3 my-1">
                            <div class="file-icon fs-3 text-info"><i class="fa-solid fa-file-lines"></i></div>
                            <div class="flex-grow-1 overflow-hidden">
                                <div class="fw-semibold text-truncate small" title="${_escHtml(payload.fileName)}">${_escHtml(payload.fileName)}</div>
                                <div class="small opacity-75">${fileSizeStr}</div>
                            </div>
                            <a href="data:application/octet-stream;base64,${payload.base64Data}" download="${_escHtml(payload.fileName)}" class="btn btn-sm btn-outline-info flex-shrink-0" title="Tải xuống tệp">
                                <i class="fa-solid fa-download"></i>
                            </a>
                        </div>`;
                } else {
                    bodyHtml = `<div class="msg-text">${_formatMessageText(msg.content)}</div>`;
                }
            } else {
                bodyHtml = `<div class="msg-text">${_formatMessageText(msg.content)}</div>`;
            }
        } else {
            bodyHtml = `<div class="msg-text">${_formatMessageText(msg.content)}</div>`;
        }

        const bubble = document.createElement('div');
        bubble.className = `message-bubble ${isSelf ? 'outgoing' : 'incoming'}`;
        bubble.innerHTML = `
            <div class="msg-sender">${isSelf ? 'Bạn' : _escHtml(msg.sender)}
                <span class="opacity-50 font-monospace" style="font-size:10px;font-weight:400"> ${typeLabel}</span>
            </div>
            ${bodyHtml}
            <div class="msg-meta">${timeStr}</div>`;

        if (prepend && chatMessages.firstChild) {
            chatMessages.insertBefore(bubble, chatMessages.firstChild);
        } else {
            chatMessages.appendChild(bubble);
            requestAnimationFrame(() => {
                chatMessages.scrollTop = chatMessages.scrollHeight;
            });
        }
    }

    /** Thêm thông báo hệ thống (dòng xám giữa màn hình chat). */
    function appendSystemMessage(text) {
        const div = document.createElement('div');
        div.className = 'system-message text-center my-2';
        div.innerHTML = `<span class="badge bg-secondary opacity-75">${_escHtml(text)}</span>`;
        chatMessages.appendChild(div);
        requestAnimationFrame(() => { chatMessages.scrollTop = chatMessages.scrollHeight; });
    }

    /* ════════════════════════════════════════════════
       13. HELPER
    ════════════════════════════════════════════════ */

    /** Escape HTML để chống XSS. */
    function _escHtml(text) {
        const d = document.createElement('div');
        d.textContent = String(text ?? '');
        return d.innerHTML;
    }

    /** Định dạng dung lượng byte thành B, KB, MB */
    function _formatFileSize(bytes) {
        if (!bytes || bytes < 1024) return (bytes || 0) + ' B';
        if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB';
        return (bytes / (1024 * 1024)).toFixed(2) + ' MB';
    }

    /** Tự động nhận diện hyperlink URL (http, https) */
    function _formatMessageText(text) {
        const escaped = _escHtml(text);
        const urlRegex = /(https?:\/\/[^\s<]+)/gi;
        return escaped.replace(urlRegex, (url) => {
            return `<a href="${url}" target="_blank" rel="noopener noreferrer" class="chat-link">${url}</a>`;
        }).replace(/\n/g, '<br>');
    }

}); // end DOMContentLoaded
