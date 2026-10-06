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

    let activeTarget = 'ALL';
    let typingTimer = null;

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
    };

    wsManager.onUserListUpdated = (users) => {
        renderUserList(users);
    };

    wsManager.onMessageReceived = (msg) => {
        appendMessage(msg);
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

    // Append Message to UI
    function appendMessage(msg) {
        const isSelf = msg.sender === wsManager.username;
        const timeStr = new Date(msg.timestamp).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
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
