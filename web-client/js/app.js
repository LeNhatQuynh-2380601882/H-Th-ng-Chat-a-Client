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
    const btnToggleSound = document.getElementById('btnToggleSound');
    const soundIcon = document.getElementById('soundIcon');
    const btnExportChat = document.getElementById('btnExportChat');
    const pingValue = document.getElementById('pingValue');
    const tokenStatus = document.getElementById('tokenStatus');
    const emojiBar = document.getElementById('emojiBar');

    let activeTarget = 'ALL';
    let typingTimer = null;
    let soundEnabled = true;
    let chatHistory = [];

    // Notification Sound Synthesizer via Web Audio API
    function playNotificationSound() {
        if (!soundEnabled) return;
        try {
            const ctx = new (window.AudioContext || window.webkitAudioContext)();
            const osc = ctx.createOscillator();
            const gain = ctx.createGain();
            osc.type = 'sine';
            osc.frequency.setValueAtTime(587.33, ctx.currentTime); // D5
            osc.frequency.exponentialRampToValueAtTime(880, ctx.currentTime + 0.12); // A5
            gain.gain.setValueAtTime(0.15, ctx.currentTime);
            gain.gain.exponentialRampToValueAtTime(0.01, ctx.currentTime + 0.25);
            osc.connect(gain);
            gain.connect(ctx.destination);
            osc.start();
            osc.stop(ctx.currentTime + 0.25);
        } catch (e) {
            // Audio context may be restricted before interaction
        }
    }

    // Toggle Sound Button
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

    // Quick Emoji Bar Handler
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

    // Export Chat History
    if (btnExportChat) {
        btnExportChat.addEventListener('click', () => {
            if (chatHistory.length === 0) {
                alert('Chưa có tin nhắn nào để xuất lịch sử!');
                return;
            }
            const logContent = chatHistory.map(m => `[${new Date(m.timestamp).toLocaleString()}] [${m.sender} -> ${m.receiver}]: ${m.content}`).join('\n');
            const blob = new Blob([logContent], { type: 'text/plain;charset=utf-8' });
            const url = URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = `chat-log-${Date.now()}.txt`;
            a.click();
            URL.revokeObjectURL(url);
        });
    }

    // Login Form Submit
    loginForm.addEventListener('submit', (e) => {
        e.preventDefault();
        const username = usernameInput.value.trim();
        const host = serverIpInput.value.trim();
        const port = serverPortInput.value.trim();

        if (!username) return;

        btnLogin.disabled = true;
        loginStatus.classList.remove('display-none');
        loginStatus.textContent = 'Đang kết nối tới WebSocket Gateway...';

        wsManager.connect(host, port, username);
    });

    // WebSocket Callbacks
    wsManager.onLoginSuccess = (msg) => {
        loginScreen.classList.add('display-none');
        chatScreen.classList.remove('display-none');
        displayMyName.textContent = wsManager.username;
        myAvatar.textContent = wsManager.username.charAt(0).toUpperCase();
        if (tokenStatus && wsManager.token) {
            tokenStatus.textContent = `Token: ${wsManager.token.substring(0, 8)}...`;
        }
    };

    wsManager.onLatencyUpdate = (latency) => {
        if (pingValue) {
            pingValue.textContent = `${latency} ms`;
        }
    };

    wsManager.onUserListUpdated = (users) => {
        renderUserList(users);
    };

    wsManager.onMessageReceived = (msg) => {
        appendMessage(msg);
        if (msg.sender !== wsManager.username) {
            playNotificationSound();
        }
    };

    wsManager.onTyping = (msg) => {
        if (msg.sender !== wsManager.username) {
            typingIndicator.textContent = `${msg.sender} đang nhập tin nhắn...`;
            clearTimeout(typingTimer);
            typingTimer = setTimeout(() => {
                typingIndicator.textContent = '';
            }, 2500);
        }
    };

    wsManager.onError = (errMsg) => {
        btnLogin.disabled = false;
        loginStatus.classList.remove('display-none');
        loginStatus.className = 'mt-3 small text-danger';
        loginStatus.textContent = `Lỗi: ${errMsg}`;
    };

    wsManager.onClose = () => {
        loginScreen.classList.remove('display-none');
        chatScreen.classList.add('display-none');
        btnLogin.disabled = false;
        loginStatus.textContent = 'Đã ngắt kết nối.';
    };

    // Render Online User List
    function renderUserList(users) {
        // Keep ALL item
        userList.innerHTML = `
            <div class="user-item p-2 rounded mb-1 d-flex align-items-center gap-2 ${activeTarget === 'ALL' ? 'active' : ''}" data-target="ALL">
                <div class="avatar bg-gradient-primary text-white rounded-circle"><i class="fa-solid fa-users"></i></div>
                <div class="flex-grow-1">
                    <div class="fw-semibold">TẤT CẢ (ALL)</div>
                    <div class="small text-muted">Chat nhóm Broadcast</div>
                </div>
            </div>
        `;

        users.forEach(u => {
            if (u !== wsManager.username) {
                const item = document.createElement('div');
                item.className = `user-item p-2 rounded mb-1 d-flex align-items-center gap-2 ${activeTarget === u ? 'active' : ''}`;
                item.setAttribute('data-target', u);
                item.innerHTML = `
                    <div class="avatar bg-secondary text-white rounded-circle fw-bold">${u.charAt(0).toUpperCase()}</div>
                    <div class="flex-grow-1">
                        <div class="fw-semibold">${u}</div>
                        <div class="small text-success"><i class="fa-solid fa-circle me-1" style="font-size: 8px;"></i>Online</div>
                    </div>
                `;
                userList.appendChild(item);
            }
        });

        // Add Click event to items
        document.querySelectorAll('.user-item').forEach(el => {
            el.addEventListener('click', () => {
                document.querySelectorAll('.user-item').forEach(i => i.classList.remove('active'));
                el.classList.add('active');
                activeTarget = el.getAttribute('data-target');

                if (activeTarget === 'ALL') {
                    chatHeaderTitle.textContent = 'Phòng Chat Nhóm (ALL)';
                    chatHeaderAvatar.innerHTML = '<i class="fa-solid fa-users"></i>';
                } else {
                    chatHeaderTitle.textContent = `Chat 1-1 với ${activeTarget}`;
                    chatHeaderAvatar.innerHTML = activeTarget.charAt(0).toUpperCase();
                }
            });
        });
    }

    // Send Message
    messageForm.addEventListener('submit', (e) => {
        e.preventDefault();
        const text = messageInput.value.trim();
        if (!text) return;

        wsManager.sendChatMessage(activeTarget, text);
        
        // Append sent message locally for immediate UI update
        const selfMsg = {
            sender: wsManager.username,
            receiver: activeTarget,
            content: text,
            action: activeTarget === 'ALL' ? 'CHAT_GROUP' : 'CHAT_SINGLE',
            timestamp: Date.now()
        };
        appendMessage(selfMsg);

        messageInput.value = '';
    });

    // Send Typing Event on Input
    messageInput.addEventListener('input', () => {
        if (messageInput.value.length > 0) {
            wsManager.sendTypingStatus(activeTarget);
        }
    });

    // Filter users on search input
    if (searchUser) {
        searchUser.addEventListener('input', () => {
            const query = searchUser.value.trim().toLowerCase();
            document.querySelectorAll('#userList .user-item').forEach(item => {
                const target = item.getAttribute('data-target');
                if (target === 'ALL') {
                    item.style.display = '';
                } else {
                    item.style.display = target.toLowerCase().includes(query) ? '' : 'none';
                }
            });
        });
    }

    // Append Message to UI
    function appendMessage(msg) {
        chatHistory.push(msg);
        const isSelf = msg.sender === wsManager.username;
        const timeStr = new Date(msg.timestamp || Date.now()).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
        const bubble = document.createElement('div');
        bubble.className = `message-bubble ${isSelf ? 'outgoing' : 'incoming'}`;

        const senderLabel = isSelf ? 'Bạn' : msg.sender;
        const typeLabel = msg.action === 'CHAT_GROUP' ? '[Nhóm]' : '[Cá nhân]';

        bubble.innerHTML = `
            <div class="fw-bold small mb-1">${senderLabel} <span class="opacity-50 font-monospace" style="font-size:10px">${typeLabel}</span></div>
            <div>${escapeHtml(msg.content)}</div>
            <div class="msg-meta text-end">${timeStr}</div>
        `;

        chatMessages.appendChild(bubble);
        chatMessages.scrollTop = chatMessages.scrollHeight;
    }

    function escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    btnLogout.addEventListener('click', () => {
        wsManager.disconnect();
    });
});
