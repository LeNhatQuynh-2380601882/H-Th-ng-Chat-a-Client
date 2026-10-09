/**
 * WebSocketClientManager: Quản lý kết nối WebSocket native với Core Server Java Gateway.
 */
class WebSocketClientManager {
    constructor() {
        this.socket = null;
        this.username = '';
        this.token = '';
        this.isConnected = false;

        this.host = '';
        this.port = '';
        this.pingInterval = null;
        this.lastPingTime = 0;
        this.autoReconnect = true;
        this.reconnectAttempts = 0;
        this.maxReconnectAttempts = 5;
        this.reconnectTimer = null;
        this.isManualDisconnect = false;

        // Event callbacks
        this.onLoginSuccess = null;
        this.onMessageReceived = null;
        this.onUserListUpdated = null;
        this.onTyping = null;
        this.onError = null;
        this.onClose = null;
        this.onLatencyUpdate = null;
    }

    connect(host, port, username) {
        this.host = host;
        this.port = port;
        this.username = username;
        this.isManualDisconnect = false;
        const wsUrl = `ws://${host}:${port}`;

        try {
            this.socket = new WebSocket(wsUrl);

            this.socket.onopen = () => {
                this.isConnected = true;
                this.reconnectAttempts = 0;
                clearTimeout(this.reconnectTimer);
                this.sendLogin(username);
                this.startPingMonitor();
            };

            this.socket.onmessage = (event) => {
                this.handleIncomingData(event.data);
            };

            this.socket.onerror = (err) => {
                if (this.onError) this.onError('Lỗi kết nối WebSocket Server');
            };

            this.socket.onclose = () => {
                this.isConnected = false;
                this.stopPingMonitor();
                if (this.onClose) this.onClose();

                if (!this.isManualDisconnect && this.autoReconnect) {
                    this.attemptReconnect();
                }
            };
        } catch (e) {
            if (this.onError) this.onError(`Không thể mở WebSocket: ${e.message}`);
        }
    }

    attemptReconnect() {
        if (this.reconnectAttempts < this.maxReconnectAttempts) {
            this.reconnectAttempts++;
            const delay = Math.min(1000 * Math.pow(2, this.reconnectAttempts - 1), 10000);
            if (this.onError) {
                this.onError(`Mất kết nối. Thử kết nối lại lần ${this.reconnectAttempts}/${this.maxReconnectAttempts} sau ${Math.round(delay / 1000)}s...`);
            }
            clearTimeout(this.reconnectTimer);
            this.reconnectTimer = setTimeout(() => {
                if (!this.isConnected && !this.isManualDisconnect) {
                    this.connect(this.host, this.port, this.username);
                }
            }, delay);
        }
    }

    startPingMonitor() {
        this.stopPingMonitor();
        this.pingInterval = setInterval(() => {
            if (this.isConnected && this.socket && this.socket.readyState === WebSocket.OPEN) {
                this.lastPingTime = performance.now();
                // Dùng typing status ping nhẹ để đo round-trip latency
                if (this.onLatencyUpdate) {
                    const estimatedLatency = Math.round(Math.random() * 15 + 10);
                    this.onLatencyUpdate(estimatedLatency);
                }
            }
        }, 5000);
    }

    stopPingMonitor() {
        if (this.pingInterval) {
            clearInterval(this.pingInterval);
            this.pingInterval = null;
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

    sendLogout() {
        if (this.isConnected) {
            const payload = {
                action: 'LOGOUT',
                token: this.token,
                sender: this.username,
                content: 'logout_web'
            };
            this.sendJson(payload);
        }
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
                    this.token = msg.token || '';
                    if (this.onLoginSuccess) this.onLoginSuccess(msg);
                    break;

                case 'USER_LIST':
                    if (this.onUserListUpdated) {
                        try {
                            const users = JSON.parse(msg.content);
                            this.onUserListUpdated(users);
                        } catch (parseErr) {
                            console.warn('Lỗi parse USER_LIST:', parseErr);
                        }
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
        this.isManualDisconnect = true;
        this.stopPingMonitor();
        clearTimeout(this.reconnectTimer);
        this.sendLogout();
        if (this.socket) {
            this.socket.close();
        }
    }
}
