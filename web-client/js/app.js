document.addEventListener('DOMContentLoaded', () => {
    const wsManager = new WebSocketClientManager();

    // DOM Elements
    const loginScreen = document.getElementById('loginScreen');
    const chatScreen = document.getElementById('chatScreen');
    const loginForm = document.getElementById('loginForm');
    const usernameInput = document.getElementById('usernameInput');
    const serverIpInput = document.getElementById('serverIp');
    const serverPortInput = document.getElementById('serverPort');
    const btnLogin = document.getElementById('btnLogin');
    const loginStatus = document.getElementById('loginStatus');

    const displayMyName = document.getElementById('displayMyName');
    const myAvatar = document.getElementById('myAvatar');
    const btnLogout = document.getElementById('btnLogout');
    const userList = document.getElementById('userList');
    const searchUser = document.getElementById('searchUser');

    const chatHeaderTitle = document.getElementById('chatHeaderTitle');
    const chatHeaderAvatar = document.getElementById('chatHeaderAvatar');
    const typingIndicator = document.getElementById('typingIndicator');
    const chatMessages = document.getElementById('chatMessages');
    const messageForm = document.getElementById('messageForm');
    const messageInput = document.getElementById('messageInput');
    const btnAttachFile = document.getElementById('btnAttachFile');
    const fileInput = document.getElementById('fileInput');

    let activeTarget = 'ALL';
    let currentOnlineUsers = [];

    // ─── W4: Lịch sử tin nhắn theo từng cuộc trò chuyện & Unread Badge ─
    // Key: 'ALL' hoặc username ('Bob', 'Alice'...). Value: Array of message objects
    const conversationHistory = { 'ALL': [] };
    const unreadCounts = {};

    // ─── W1/W2: Typing Indicator State ──────────────────────────────
    let _isTyping = false;          // Web client đang ở trạng thái gõ chưa gửi STOP
    let _typingDebounceTimer = null; // Timer debounce gửi STOP sau 1.5s ngừng gõ
    let _typingDisplayTimer = null;  // Timer tắt indicator khi không nhận STOP (timeout 3s)

    /**
     * Reset toàn bộ trạng thái typing của mình (gửi STOP nếu đang typing, dọn timer).
     * @param {boolean} sendStop - Nếu true và đang typing, gửi gói STOP cho phía đối diện.
     * @param {string}  targetOverride - target nhận STOP (mặc định dùng activeTarget hiện tại).
     */
    function resetTypingState(sendStop = true, targetOverride = null) {
        if (_typingDebounceTimer !== null) {
            clearTimeout(_typingDebounceTimer);
            _typingDebounceTimer = null;
        }
        if (_isTyping) {
            _isTyping = false;
            if (sendStop && wsManager.isConnected) {
                const target = targetOverride !== null ? targetOverride : activeTarget;
                wsManager.sendTypingStatus(target, 'STOP');
            }
        }
    }

    function clearIncomingTypingDisplay() {
        typingIndicator.textContent = '';
        if (_typingDisplayTimer !== null) {
            clearTimeout(_typingDisplayTimer);
            _typingDisplayTimer = null;
        }
    }

    // ─── Login Form Submit ──────────────────────────────────────────
    loginForm.addEventListener('submit', (e) => {
        e.preventDefault();
        const username = usernameInput.value.trim();
        const host = serverIpInput.value.trim();
        const port = serverPortInput.value.trim();

        if (!username) return;

        btnLogin.disabled = true;
        loginStatus.classList.remove('display-none');
        loginStatus.className = 'mt-3 small text-info';
        loginStatus.textContent = 'Đang kết nối tới WebSocket Gateway...';

        wsManager.connect(host, port, username);
    });

    // ─── WebSocket Callbacks ─────────────────────────────────────────
    wsManager.onLoginSuccess = (msg) => {
        loginScreen.classList.add('display-none');
        chatScreen.classList.remove('display-none');
        displayMyName.textContent = wsManager.username;
        myAvatar.textContent = wsManager.username.charAt(0).toUpperCase();

        // Khởi tạo phòng ALL ban đầu
        activeTarget = 'ALL';
        updateChatHeader();
        renderConversation('ALL');
    };

    wsManager.onUserListUpdated = (users) => {
        currentOnlineUsers = Array.isArray(users) ? users : [];
        renderUserList();
    };

    wsManager.onMessageReceived = (msg) => {
        addMessageToConversation(msg);
    };

    wsManager.onFileReceived = (msg) => {
        addMessageToConversation(msg);
    };

    // W1+W2: Xử lý typing chỉ khi thuộc cuộc trò chuyện hiện tại
    wsManager.onTyping = (msg) => {
        if (msg.sender === wsManager.username) return;

        // Kiểm tra xem gói tin typing có thuộc conversation đang mở không
        let isRelevant = false;
        if (activeTarget === 'ALL') {
            isRelevant = (msg.receiver === 'ALL');
        } else {
            isRelevant = (msg.sender === activeTarget && (msg.receiver === wsManager.username || !msg.receiver));
        }

        if (!isRelevant) return;

        const state = (msg.content || '').toUpperCase();
        if (state === 'STOP') {
            clearIncomingTypingDisplay();
        } else {
            typingIndicator.textContent = `✍️ ${msg.sender} đang nhập tin nhắn...`;
            if (_typingDisplayTimer !== null) {
                clearTimeout(_typingDisplayTimer);
            }
            _typingDisplayTimer = setTimeout(() => {
                clearIncomingTypingDisplay();
            }, 3000);
        }
    };

    wsManager.onError = (errMsg) => {
        btnLogin.disabled = false;
        loginStatus.classList.remove('display-none');
        loginStatus.className = 'mt-3 small text-danger';
        loginStatus.textContent = `Lỗi: ${errMsg}`;
    };

    wsManager.onClose = () => {
        resetTypingState(false);
        clearIncomingTypingDisplay();

        loginScreen.classList.remove('display-none');
        chatScreen.classList.add('display-none');
        btnLogin.disabled = false;
        loginStatus.textContent = 'Đã ngắt kết nối.';
    };

    // ─── W4: Quản lý và nạp tin nhắn theo cuộc trò chuyện ────────────
    function getConversationKey(msg) {
        if (msg.action === 'CHAT_GROUP' || msg.receiver === 'ALL') {
            return 'ALL';
        }
        if (msg.sender === wsManager.username) {
            return msg.receiver;
        }
        return msg.sender;
    }

    function addMessageToConversation(msg) {
        const convKey = getConversationKey(msg);
        if (!conversationHistory[convKey]) {
            conversationHistory[convKey] = [];
        }

        const normalizedMsg = {
            sender: msg.sender,
            receiver: msg.receiver,
            content: msg.content,
            action: msg.action,
            timestamp: msg.timestamp || Date.now()
        };

        conversationHistory[convKey].push(normalizedMsg);

        if (convKey === activeTarget) {
            appendMessageToDOM(normalizedMsg);
        } else {
            unreadCounts[convKey] = (unreadCounts[convKey] || 0) + 1;
            renderUserList();
        }
    }

    function renderConversation(targetKey) {
        chatMessages.innerHTML = '';
        const list = conversationHistory[targetKey] || [];

        if (list.length === 0) {
            const emptyNotice = document.createElement('div');
            emptyNotice.className = 'system-message text-center my-4';
            const text = (targetKey === 'ALL')
                ? 'Chưa có tin nhắn trong phòng Chat Nhóm (ALL).'
                : `Chưa có tin nhắn với ${targetKey}. Hãy gửi lời chào!`;
            emptyNotice.innerHTML = `<span class="badge bg-secondary opacity-75 px-3 py-2">${text}</span>`;
            chatMessages.appendChild(emptyNotice);
        } else {
            list.forEach(msg => appendMessageToDOM(msg));
        }

        chatMessages.scrollTop = chatMessages.scrollHeight;
    }

    // ─── W5: Hiển thị Bong bóng Tin nhắn và File Đính kèm ─────────────
    function appendMessageToDOM(msg) {
        const isSelf = msg.sender === wsManager.username;
        const timeStr = new Date(msg.timestamp).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
        const bubble = document.createElement('div');
        bubble.className = `message-bubble ${isSelf ? 'outgoing' : 'incoming'}`;

        const senderLabel = isSelf ? 'Bạn' : msg.sender;
        const typeLabel = (msg.action === 'CHAT_GROUP' || msg.receiver === 'ALL') ? '[Nhóm]' : '[Cá nhân]';

        let bodyHtml = '';

        if (msg.action === 'FILE_TRANSFER') {
            let filePayload = null;
            try {
                filePayload = typeof msg.content === 'object' ? msg.content : JSON.parse(msg.content);
            } catch (e) {
                filePayload = null;
            }

            if (filePayload && filePayload.base64Data) {
                const fileNameSafe = escapeHtml(filePayload.fileName || 'file');
                const fileSizeFormatted = formatBytes(filePayload.fileSize || 0);

                if (filePayload.isImage) {
                    const imgSrc = `data:image/*;base64,${filePayload.base64Data}`;
                    bodyHtml = `
                        <div class="file-attachment image-attachment mb-2">
                            <img src="${imgSrc}" class="img-fluid rounded border border-secondary shadow-sm" style="max-height: 250px; object-fit: contain; cursor: pointer;" alt="${fileNameSafe}">
                        </div>
                        <div class="d-flex align-items-center justify-content-between gap-2 small">
                            <span class="text-truncate fw-semibold" style="max-width: 180px;">${fileNameSafe}</span>
                            <span class="text-muted">${fileSizeFormatted}</span>
                            <button type="button" class="btn btn-sm btn-outline-info py-0 px-2 btn-download-file">
                                <i class="fa-solid fa-download"></i> Tải ảnh
                            </button>
                        </div>
                    `;
                } else {
                    bodyHtml = `
                        <div class="file-attachment p-2 rounded bg-dark bg-opacity-50 border border-secondary d-flex align-items-center gap-3 mb-2">
                            <i class="fa-solid fa-file-arrow-down fa-2x text-warning"></i>
                            <div class="flex-grow-1 overflow-hidden">
                                <div class="fw-bold text-truncate" style="max-width: 200px;">${fileNameSafe}</div>
                                <div class="text-muted small">${fileSizeFormatted}</div>
                            </div>
                            <button type="button" class="btn btn-sm btn-primary py-1 px-3 btn-download-file">
                                <i class="fa-solid fa-download me-1"></i> Tải về
                            </button>
                        </div>
                    `;
                }

                // Gắn listener tải file sau khi tạo bubble
                setTimeout(() => {
                    const btnDownload = bubble.querySelector('.btn-download-file');
                    const imgElem = bubble.querySelector('img');
                    if (btnDownload) {
                        btnDownload.addEventListener('click', () => {
                            downloadBase64(filePayload.base64Data, filePayload.fileName || 'downloaded_file');
                        });
                    }
                    if (imgElem) {
                        imgElem.addEventListener('click', () => {
                            downloadBase64(filePayload.base64Data, filePayload.fileName || 'downloaded_file');
                        });
                    }
                }, 0);
            } else {
                bodyHtml = `<div class="text-muted fst-italic">[Tệp đính kèm không khả dụng]</div>`;
            }
        } else {
            bodyHtml = `<div>${escapeHtml(msg.content)}</div>`;
        }

        bubble.innerHTML = `
            <div class="fw-bold small mb-1">${senderLabel} <span class="opacity-50 font-monospace" style="font-size:10px">${typeLabel}</span></div>
            ${bodyHtml}
            <div class="msg-meta text-end mt-1">${timeStr}</div>
        `;

        chatMessages.appendChild(bubble);
        chatMessages.scrollTop = chatMessages.scrollHeight;
    }

    function downloadBase64(base64Data, fileName) {
        try {
            const link = document.createElement('a');
            link.href = 'data:application/octet-stream;base64,' + base64Data;
            link.download = fileName;
            document.body.appendChild(link);
            link.click();
            document.body.removeChild(link);
        } catch (err) {
            alert('Không thể tải file: ' + err.message);
        }
    }

    function formatBytes(bytes) {
        if (!bytes || bytes === 0) return '0 B';
        const k = 1024;
        const sizes = ['B', 'KB', 'MB', 'GB'];
        const i = Math.floor(Math.log(bytes) / Math.log(k));
        return parseFloat((bytes / Math.pow(k, i)).toFixed(1)) + ' ' + sizes[i];
    }

    function escapeHtml(text) {
        if (!text) return '';
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    // ─── Render Online User List & Switch Conversation ──────────────
    function updateChatHeader() {
        if (activeTarget === 'ALL') {
            chatHeaderTitle.textContent = 'Phòng Chat Nhóm (ALL)';
            chatHeaderAvatar.innerHTML = '<i class="fa-solid fa-users"></i>';
        } else {
            chatHeaderTitle.textContent = `Chat 1-1 với ${activeTarget}`;
            chatHeaderAvatar.innerHTML = activeTarget.charAt(0).toUpperCase();
        }
    }

    function renderUserList() {
        const query = (searchUser.value || '').trim().toLowerCase();

        // 1. ALL Item
        const allUnread = unreadCounts['ALL'] || 0;
        const allUnreadBadge = allUnread > 0
            ? `<span class="badge bg-danger rounded-pill">${allUnread}</span>`
            : '';

        userList.innerHTML = `
            <div class="user-item p-2 rounded mb-1 d-flex align-items-center justify-content-between gap-2 ${activeTarget === 'ALL' ? 'active' : ''}" data-target="ALL">
                <div class="d-flex align-items-center gap-2">
                    <div class="avatar bg-gradient-primary text-white rounded-circle"><i class="fa-solid fa-users"></i></div>
                    <div>
                        <div class="fw-semibold">TẤT CẢ (ALL)</div>
                        <div class="small text-muted">Chat nhóm Broadcast</div>
                    </div>
                </div>
                ${allUnreadBadge}
            </div>
        `;

        // 2. Các user online
        currentOnlineUsers.forEach(u => {
            if (u !== wsManager.username) {
                if (query && !u.toLowerCase().includes(query)) {
                    return; // Bỏ qua nếu không khớp từ khóa tìm kiếm
                }

                const count = unreadCounts[u] || 0;
                const unreadBadge = count > 0
                    ? `<span class="badge bg-danger rounded-pill">${count}</span>`
                    : '';

                const item = document.createElement('div');
                item.className = `user-item p-2 rounded mb-1 d-flex align-items-center justify-content-between gap-2 ${activeTarget === u ? 'active' : ''}`;
                item.setAttribute('data-target', u);
                item.innerHTML = `
                    <div class="d-flex align-items-center gap-2">
                        <div class="avatar bg-secondary text-white rounded-circle fw-bold">${u.charAt(0).toUpperCase()}</div>
                        <div>
                            <div class="fw-semibold">${u}</div>
                            <div class="small text-success"><i class="fa-solid fa-circle me-1" style="font-size: 8px;"></i>Online</div>
                        </div>
                    </div>
                    ${unreadBadge}
                `;
                userList.appendChild(item);
            }
        });

        // 3. Gắn sự kiện click chuyển conversation
        userList.querySelectorAll('.user-item').forEach(el => {
            el.addEventListener('click', () => {
                const newTarget = el.getAttribute('data-target');

                if (newTarget !== activeTarget) {
                    // Dọn typing state của mình (gửi STOP cho target cũ)
                    resetTypingState(true, activeTarget);
                    // Dọn typing display của người khác trên header
                    clearIncomingTypingDisplay();

                    activeTarget = newTarget;
                    // Xóa unread count của conversation mới chọn
                    unreadCounts[activeTarget] = 0;

                    updateChatHeader();
                    renderConversation(activeTarget);
                    renderUserList();
                }
            });
        });
    }

    searchUser.addEventListener('input', () => {
        renderUserList();
    });

    // ─── Gửi tin nhắn text (W1/W2 debounce + STOP trước khi gửi) ─────
    messageForm.addEventListener('submit', (e) => {
        e.preventDefault();
        const text = messageInput.value.trim();
        if (!text) return;

        // Gửi STOP typing trước khi gửi tin nhắn (nếu đang ở trạng thái typing)
        if (_isTyping) {
            clearTimeout(_typingDebounceTimer);
            _typingDebounceTimer = null;
            _isTyping = false;
            wsManager.sendTypingStatus(activeTarget, 'STOP');
        }

        wsManager.sendChatMessage(activeTarget, text);

        const selfMsg = {
            sender: wsManager.username,
            receiver: activeTarget,
            content: text,
            action: activeTarget === 'ALL' ? 'CHAT_GROUP' : 'CHAT_SINGLE',
            timestamp: Date.now()
        };
        addMessageToConversation(selfMsg);

        messageInput.value = '';
    });

    // Debounce gõ phím gửi START/STOP
    messageInput.addEventListener('input', () => {
        if (!wsManager.isConnected) return;

        if (messageInput.value.length > 0) {
            if (!_isTyping) {
                _isTyping = true;
                wsManager.sendTypingStatus(activeTarget, 'START');
            }
            clearTimeout(_typingDebounceTimer);
            _typingDebounceTimer = setTimeout(() => {
                if (_isTyping) {
                    _isTyping = false;
                    wsManager.sendTypingStatus(activeTarget, 'STOP');
                }
                _typingDebounceTimer = null;
            }, 1500);
        } else {
            if (_isTyping) {
                clearTimeout(_typingDebounceTimer);
                _typingDebounceTimer = null;
                _isTyping = false;
                wsManager.sendTypingStatus(activeTarget, 'STOP');
            }
        }
    });

    // ─── Gửi File từ Web sang Desktop / Web khác (W5) ───────────────
    btnAttachFile.addEventListener('click', () => {
        fileInput.click();
    });

    fileInput.addEventListener('change', (e) => {
        const file = e.target.files && e.target.files[0];
        if (!file) return;

        // Giới hạn an toàn 15MB để tránh quá tải bộ nhớ socket
        if (file.size > 15 * 1024 * 1024) {
            alert('Kích thước tệp vượt quá giới hạn 15MB!');
            fileInput.value = '';
            return;
        }

        const reader = new FileReader();
        reader.onload = () => {
            const resultStr = reader.result; // DataURL: data:mime;base64,....
            const base64Content = resultStr.split(',')[1];
            const isImage = file.type.startsWith('image/');

            const payload = {
                fileName: file.name,
                fileSize: file.size,
                isImage: isImage,
                base64Data: base64Content
            };

            wsManager.sendFile(activeTarget, payload);

            const selfFileMsg = {
                sender: wsManager.username,
                receiver: activeTarget,
                content: payload,
                action: 'FILE_TRANSFER',
                timestamp: Date.now()
            };
            addMessageToConversation(selfFileMsg);

            fileInput.value = '';
        };

        reader.onerror = () => {
            alert('Lỗi đọc tệp tin!');
            fileInput.value = '';
        };

        reader.readAsDataURL(file);
    });

    btnLogout.addEventListener('click', () => {
        resetTypingState(true, activeTarget);
        wsManager.disconnect();
    });
});
