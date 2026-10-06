/**
 * WebSocketClientManager: Quản lý kết nối WebSocket native với Core Server Java Gateway.
 */
class WebSocketClientManager {
    constructor() {
        this.socket = null;
        this.username = '';
        this.token = '';
        this.isConnected = false;

        // Event callbacks
        this.onLoginSuccess = null;
        this.onMessageReceived = null;
        this.onUserListUpdated = null;
        this.onTyping = null;
        this.onError = null;
        this.onClose = null;
    }

    connect(host, port, username) {
        this.username = username;
        const wsUrl = `ws://${host}:${port}`;

        try {
            this.socket = new WebSocket(wsUrl);

            this.socket.onopen = () => {
                this.isConnected = true;
                this.sendLogin(username);
            };

            this.socket.onmessage = (event) => {
                this.handleIncomingData(event.data);
            };

            this.socket.onerror = (err) => {
                if (this.onError) this.onError('Lỗi kết nối WebSocket Server');
            };

            this.socket.onclose = () => {
                this.isConnected = false;
                if (this.onClose) this.onClose();
            };
        } catch (e) {
            if (this.onError) this.onError(`Không thể mở WebSocket: ${e.message}`);
        }
    }

    sendLogin(username) {
        const payload = {
            action: 'LOGIN',
            sender: username,
            content: 'login_web'
        };
        this.sendJson(payload);
    }

    sendChatMessage(receiver, content) {
        const action = (receiver === 'ALL') ? 'CHAT_GROUP' : 'CHAT_SINGLE';
        const payload = {
            action: action,
            token: this.token,
            sender: this.username,
            receiver: receiver,
            content: content
        };
        this.sendJson(payload);
    }

    sendTypingStatus(receiver) {
        const payload = {
            action: 'TYPING',
            token: this.token,
            sender: this.username,
            receiver: receiver,
            content: 'is_typing'
        };
        this.sendJson(payload);
    }

    sendJson(dataObj) {
        if (this.socket && this.socket.readyState === WebSocket.OPEN) {
            this.socket.send(JSON.stringify(dataObj));
        }
    }

    handleIncomingData(rawString) {
        try {
            const msg = JSON.parse(rawString);
            if (!msg || !msg.action) return;

            switch (msg.action) {
                case 'LOGIN_SUCCESS':
                    this.token = msg.token;
                    if (this.onLoginSuccess) this.onLoginSuccess(msg);
                    break;

                case 'USER_LIST':
                    if (this.onUserListUpdated) {
                        const users = JSON.parse(msg.content);
                        this.onUserListUpdated(users);
                    }
                    break;

                case 'CHAT_SINGLE':
                case 'CHAT_GROUP':
                    if (this.onMessageReceived) this.onMessageReceived(msg);
                    break;

                case 'TYPING':
                    if (this.onTyping) this.onTyping(msg);
                    break;

                case 'ERROR':
                    if (this.onError) this.onError(msg.content);
                    break;
            }
        } catch (e) {
            console.error('JSON Parse Error:', e);
        }
    }

    disconnect() {
        if (this.socket) {
            this.socket.close();
        }
    }
}
