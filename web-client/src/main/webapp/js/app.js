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
        // Server thật broadcast lại cho cả người gửi nhưng KHÔNG có cờ _self → lọc theo sender.
        if (msg.sender === wsManager.username) return;

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
        if (msg.sender === wsManager.username) return; // echo broadcast của chính mình

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

    /* ════════════════════════════════════════════════
       11b. GỬI FILE / HÌNH ẢNH
       – Nút 📎 (chọn nhiều tệp), dán ảnh Ctrl+V, kéo-thả vào khung chat
    ════════════════════════════════════════════════ */
    const MAX_FILE_SIZE = 5 * 1024 * 1024;               // 5MB – đồng bộ với Desktop Client
    const IMAGE_EXT_RE  = /\.(png|jpe?g|gif|webp|bmp|svg)$/i;

    /**
     * Đọc 1 tệp → Base64 → gửi FILE_TRANSFER và hiển thị ngay cho người gửi.
     * @param {File} file
     */
    function sendFile(file) {
        if (!file) return;
        if (!wsManager.isConnected) {
            alert('Chưa kết nối tới Server!');
            return;
        }
        if (file.size > MAX_FILE_SIZE) {
            appendSystemMessage(`⚠️ "${file.name}" (${_formatFileSize(file.size)}) vượt quá 5MB – không gửi được.`);
            return;
        }

        // Chốt người nhận tại thời điểm chọn tệp (tránh đổi tab trong lúc đang đọc file)
        const target = activeTarget;
        const reader = new FileReader();

        reader.onload = (e) => {
            const base64Data = String(e.target.result).split(',')[1] || '';
            const mimeType   = file.type || _guessMime(file.name);
            const isImage    = mimeType.startsWith('image/') || IMAGE_EXT_RE.test(file.name);

            wsManager.sendFileMessage(target, file.name, file.size, isImage, base64Data, mimeType);

            const selfMsg = {
                _self:     true,
                action:    'FILE_TRANSFER',
                sender:    wsManager.username,
                receiver:  target,
                content:   JSON.stringify({ fileName: file.name, fileSize: file.size, isImage, base64Data, mimeType }),
                timestamp: Date.now()
            };
            const key = _chatKey(selfMsg);
            if (!chatLog[key]) chatLog[key] = [];
            chatLog[key].push(selfMsg);
            if (key === activeTarget) renderBubble(selfMsg, false);
        };
        reader.onerror = () => appendSystemMessage(`❌ Không đọc được tệp "${file.name}".`);
        reader.readAsDataURL(file);
    }

    /* ── Nút 📎 ── */
    if (btnAttachFile && fileInput) {
        btnAttachFile.addEventListener('click', () => {
            if (!wsManager.isConnected) {
                alert('Chưa kết nối tới Server!');
                return;
            }
            fileInput.value = '';
            fileInput.click();
        });

        fileInput.addEventListener('change', () => {
            Array.from(fileInput.files || []).forEach(sendFile);
            fileInput.value = '';
        });
    }

    /* ── Dán ảnh / tệp từ clipboard (Ctrl+V) vào ô nhập ── */
    messageInput.addEventListener('paste', (e) => {
        const files = Array.from(e.clipboardData?.files || []);
        if (files.length === 0) return;             // dán chữ / link bình thường
        e.preventDefault();
        files.forEach((f, i) => {
            // Ảnh chụp màn hình thường tên "image.png" → đặt tên dễ phân biệt hơn
            if (/^image\.\w+$/i.test(f.name)) {
                const ext = f.name.split('.').pop();
                f = new File([f], `anh-dan-${Date.now()}${i ? '-' + i : ''}.${ext}`, { type: f.type });
            }
            sendFile(f);
        });
    });

    /* ── Kéo-thả tệp vào khung chat ── */
    const chatMain = document.querySelector('.chat-main');
    if (chatMain) {
        let dragDepth = 0;
        const hasFiles = (e) => Array.from(e.dataTransfer?.types || []).includes('Files');

        chatMain.addEventListener('dragenter', (e) => {
            if (!hasFiles(e)) return;
            e.preventDefault();
            dragDepth++;
            chatMain.classList.add('drag-over');
        });
        chatMain.addEventListener('dragover', (e) => {
            if (!hasFiles(e)) return;
            e.preventDefault();
            e.dataTransfer.dropEffect = 'copy';
        });
        chatMain.addEventListener('dragleave', () => {
            dragDepth = Math.max(0, dragDepth - 1);
            if (dragDepth === 0) chatMain.classList.remove('drag-over');
        });
        chatMain.addEventListener('drop', (e) => {
            if (!hasFiles(e)) return;
            e.preventDefault();
            dragDepth = 0;
            chatMain.classList.remove('drag-over');
            Array.from(e.dataTransfer.files || []).forEach(sendFile);
        });
    }

    /* ── Click trong khung chat: xem ảnh lớn / tải tệp (event delegation) ── */
    chatMessages.addEventListener('click', (e) => {
        const el = e.target.closest('[data-action]');
        if (!el) return;
        const action = el.getAttribute('data-action');

        if (action === 'view-image') {
            e.preventDefault();
            _openLightbox(el.getAttribute('src'), el.getAttribute('alt') || '');
        } else if (action === 'download') {
            e.preventDefault();
            const bubble = el.closest('.message-bubble');
            const payload = bubble && _parseFilePayload(bubble._msg?.content);
            if (payload) _downloadPayload(payload);
        }
    });

    /* ── Lightbox xem ảnh ── */
    const lightbox      = document.getElementById('imageLightbox');
    const lightboxImg   = document.getElementById('lightboxImg');
    const lightboxCap   = document.getElementById('lightboxCaption');

    function _openLightbox(src, caption) {
        if (!lightbox || !lightboxImg) { window.open(src, '_blank', 'noopener'); return; }
        lightboxImg.src = src;
        lightboxImg.alt = caption;
        if (lightboxCap) lightboxCap.textContent = caption;
        lightbox.classList.remove('display-none');
    }
    function _closeLightbox() {
        if (!lightbox) return;
        lightbox.classList.add('display-none');
        lightboxImg.removeAttribute('src');
    }
    if (lightbox) {
        lightbox.addEventListener('click', (e) => {
            if (e.target !== lightboxImg) _closeLightbox();
        });
        document.addEventListener('keydown', (e) => {
            if (e.key === 'Escape') _closeLightbox();
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

        const bodyHtml = msg.action === 'FILE_TRANSFER'
            ? _buildFileBody(msg)
            : `<div class="msg-text">${_formatMessageText(msg.content)}</div>`;

        const bubble = document.createElement('div');
        bubble.className = `message-bubble ${isSelf ? 'outgoing' : 'incoming'}`;
        bubble._msg = msg; // giữ tham chiếu để nút "Tải xuống" lấy lại payload
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

    /**
     * Tự động nhận diện link (http://, https://, www.) – an toàn XSS.
     * Escape từng đoạn riêng (không chèn chuỗi đã escape vào href) và loại dấu ' " < >
     * khỏi URL để không thể "thoát" khỏi thuộc tính HTML.
     * Link trỏ tới ảnh (.png/.jpg/.gif/.webp…) được hiển thị kèm ảnh xem trước.
     */
    function _formatMessageText(text) {
        const raw = String(text ?? '');
        const urlRegex = /\b(?:https?:\/\/|www\.)[^\s<>"'`]+/gi;
        let html = '';
        let last = 0;
        const imagePreviews = [];

        for (const m of raw.matchAll(urlRegex)) {
            let url = m[0];
            // Bỏ dấu câu dính cuối link: "xem https://abc.com." → không lấy dấu chấm
            const trail = url.match(/[.,!?;:)\]]+$/);
            if (trail) url = url.slice(0, -trail[0].length);
            if (!url) continue;

            const href = /^www\./i.test(url) ? `https://${url}` : url;
            html += _escHtml(raw.slice(last, m.index));
            html += `<a href="${_escAttr(href)}" target="_blank" rel="noopener noreferrer" class="chat-link">${_escHtml(url)}</a>`;
            last = m.index + url.length;

            if (/\.(png|jpe?g|gif|webp|bmp|svg)(\?[^#]*)?(#.*)?$/i.test(href) && imagePreviews.length < 3) {
                imagePreviews.push(href);
            }
        }
        html += _escHtml(raw.slice(last));
        html = html.replace(/\n/g, '<br>');

        imagePreviews.forEach(src => {
            html += `<div class="mt-1"><img src="${_escAttr(src)}" alt="Ảnh từ link" class="chat-img-preview chat-link-img"
                         data-action="view-image" loading="lazy" referrerpolicy="no-referrer"
                         onerror="this.parentElement.remove()"></div>`;
        });
        return html;
    }

    /** Escape cho giá trị thuộc tính HTML. */
    function _escAttr(text) {
        return String(text ?? '')
            .replace(/&/g, '&amp;').replace(/"/g, '&quot;').replace(/'/g, '&#39;')
            .replace(/</g, '&lt;').replace(/>/g, '&gt;');
    }

    /* ── FILE_TRANSFER helpers ── */

    const MIME_BY_EXT = {
        png: 'image/png', jpg: 'image/jpeg', jpeg: 'image/jpeg', gif: 'image/gif', webp: 'image/webp',
        bmp: 'image/bmp', svg: 'image/svg+xml', pdf: 'application/pdf', txt: 'text/plain',
        zip: 'application/zip', rar: 'application/vnd.rar', '7z': 'application/x-7z-compressed',
        doc: 'application/msword', docx: 'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
        xls: 'application/vnd.ms-excel', xlsx: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
        ppt: 'application/vnd.ms-powerpoint', pptx: 'application/vnd.openxmlformats-officedocument.presentationml.presentation',
        mp3: 'audio/mpeg', mp4: 'video/mp4', json: 'application/json', csv: 'text/csv'
    };

    function _fileExt(name) {
        const m = String(name || '').match(/\.([a-z0-9]+)$/i);
        return m ? m[1].toLowerCase() : '';
    }

    function _guessMime(name) {
        return MIME_BY_EXT[_fileExt(name)] || 'application/octet-stream';
    }

    /** Icon FontAwesome theo loại tệp. */
    function _fileIcon(name) {
        const ext = _fileExt(name);
        if (ext === 'pdf') return 'fa-file-pdf text-danger';
        if (['doc', 'docx'].includes(ext)) return 'fa-file-word text-primary';
        if (['xls', 'xlsx', 'csv'].includes(ext)) return 'fa-file-excel text-success';
        if (['ppt', 'pptx'].includes(ext)) return 'fa-file-powerpoint text-warning';
        if (['zip', 'rar', '7z'].includes(ext)) return 'fa-file-zipper text-warning';
        if (['mp3', 'wav', 'ogg'].includes(ext)) return 'fa-file-audio text-info';
        if (['mp4', 'mov', 'avi', 'mkv'].includes(ext)) return 'fa-file-video text-info';
        if (['js', 'java', 'cs', 'html', 'css', 'json', 'xml', 'py'].includes(ext)) return 'fa-file-code text-info';
        return 'fa-file-lines text-info';
    }

    /**
     * Parse + kiểm tra payload FILE_TRANSFER (định dạng chung với Desktop: fileName, fileSize, isImage, base64Data).
     * Trả về null nếu không hợp lệ → hiển thị như text thường.
     */
    function _parseFilePayload(content) {
        let p;
        try {
            p = typeof content === 'string' ? JSON.parse(content) : content;
        } catch (_) {
            return null;
        }
        if (!p || typeof p.fileName !== 'string' || typeof p.base64Data !== 'string') return null;
        // Chỉ chấp nhận ký tự Base64 hợp lệ → không thể chèn mã HTML qua src="data:..."
        if (!/^[A-Za-z0-9+/]*={0,2}$/.test(p.base64Data)) return null;
        if (p.mimeType && !/^[\w.+-]+\/[\w.+-]+$/.test(p.mimeType)) p.mimeType = '';
        return p;
    }

    /** HTML phần thân bubble cho FILE_TRANSFER (ảnh xem trước hoặc thẻ tệp). */
    function _buildFileBody(msg) {
        const p = _parseFilePayload(msg.content);
        if (!p) return `<div class="msg-text">${_formatMessageText(msg.content)}</div>`;

        const name = _escHtml(p.fileName);
        const size = _formatFileSize(p.fileSize);
        const mime = p.mimeType || _guessMime(p.fileName);

        if (p.isImage) {
            const imgMime = mime.startsWith('image/') ? mime : 'image/png';
            return `
                <div class="msg-file-image my-1">
                    <img src="data:${imgMime};base64,${p.base64Data}" alt="${_escAttr(p.fileName)}"
                         class="chat-img-preview" data-action="view-image" title="Bấm để xem ảnh lớn">
                    <div class="small opacity-75 mt-1 d-flex align-items-center justify-content-between gap-2">
                        <span class="text-truncate"><i class="fa-solid fa-image me-1"></i>${name}</span>
                        <span class="flex-shrink-0">${size}
                            <a href="#" data-action="download" class="ms-2 text-info" title="Tải ảnh xuống"><i class="fa-solid fa-download"></i></a>
                        </span>
                    </div>
                </div>`;
        }

        return `
            <div class="msg-file-card p-2 d-flex align-items-center gap-3 my-1">
                <div class="file-icon fs-3"><i class="fa-solid ${_fileIcon(p.fileName)}"></i></div>
                <div class="flex-grow-1 overflow-hidden">
                    <div class="fw-semibold text-truncate small" title="${_escAttr(p.fileName)}">${name}</div>
                    <div class="small opacity-75">${size}</div>
                </div>
                <button type="button" data-action="download" class="btn btn-sm btn-outline-info flex-shrink-0" title="Tải xuống tệp">
                    <i class="fa-solid fa-download"></i>
                </button>
            </div>`;
    }

    /** Giải mã Base64 → Blob → tải xuống với đúng tên tệp. */
    function _downloadPayload(p) {
        try {
            const bin = atob(p.base64Data);
            const bytes = new Uint8Array(bin.length);
            for (let i = 0; i < bin.length; i++) bytes[i] = bin.charCodeAt(i);
            const blob = new Blob([bytes], { type: p.mimeType || _guessMime(p.fileName) });
            const url = URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = p.fileName || 'tep-tin';
            document.body.appendChild(a);
            a.click();
            a.remove();
            setTimeout(() => URL.revokeObjectURL(url), 1000);
        } catch (e) {
            console.error('[FILE] Lỗi giải mã tệp:', e);
            alert('Không thể tải tệp – dữ liệu bị lỗi.');
        }
    }

}); // end DOMContentLoaded
