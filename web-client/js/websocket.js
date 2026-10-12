/**
 * websocket.js – WebSocketClientManager
 * ===============================================================
 * Module quản lý kết nối WebSocket native (RFC 6455) với
 * Core Server Java (WebSocket Gateway, cổng mặc định 8887).
 *
 * Chức năng:
 *  - Kết nối / tự động kết nối lại (Exponential Backoff)
 *  - Đóng / mở gói tin JSON theo giao thức nhóm
 *  - Quản lý Session Token (nhận từ LOGIN_SUCCESS, đính kèm mọi gói)
 *  - Đo latency WebSocket (round-trip PING/PONG ứng dụng)
 *  - Phân phát sự kiện qua callback (onLoginSuccess, onMessageReceived …)
 *  - Chế độ Demo (offline): giả lập Server để kiểm thử UI không cần backend
 *
 * Người thực hiện: Trương Hoàng Phúc – MSSV 2380601760
 * Môn: Lập Trình Mạng – Nhóm 11
 */
class WebSocketClientManager {
    constructor() {
        /* ── Thông tin phiên ── */
        this.socket    = null;
        this.username  = '';
        this.token     = '';
        this.isConnected = false;

        /* ── Thông số kết nối ── */
        this.host = '';
        this.port = '';

        /* ── Auto-reconnect (Exponential Backoff) ── */
        this.autoReconnect       = true;
        this.reconnectAttempts   = 0;
        this.maxReconnectAttempts = 5;
        this.reconnectTimer      = null;
        this.isManualDisconnect  = false;

        /* ── Đo độ trễ (PING/PONG ứng dụng tầng JSON) ── */
        this._pingInterval  = null;
        this._pingTimestamp = 0;

        /* ── Chế độ Demo (offline) ── */
        this.demoMode       = false;
        this._demoUserList  = [];
        this._demoTimer     = null;

        /* ── Event callbacks (đặt từ app.js) ── */
        this.onLoginSuccess    = null;   // (msg) => {}
        this.onMessageReceived = null;   // (msg) => {}
        this.onUserListUpdated = null;   // (users[]) => {}
        this.onTyping          = null;   // (msg) => {}
        this.onError           = null;   // (errMsg) => {}
        this.onClose           = null;   // () => {}
        this.onLatencyUpdate   = null;   // (ms) => {}
        this.onStatusChange    = null;   // ('connecting'|'connected'|'disconnected') => {}
    }

    /* ═══════════════════════════════════════════
       1. QUẢN LÝ KẾT NỐI
    ═══════════════════════════════════════════ */

    /**
     * Mở kết nối WebSocket tới Gateway.
     * @param {string} host
     * @param {string|number} port
     * @param {string} username
     * @param {boolean} [demo=false] – Bật chế độ Demo (không cần Server thật)
     */
    connect(host, port, username, demo = false) {
        this.host     = host;
        this.port     = port;
        this.username = username;
        this.demoMode = demo;
        this.isManualDisconnect = false;

        this._emitStatus('connecting');

        if (demo) {
            this._startDemoMode();
            return;
        }

        const wsUrl = `ws://${host}:${port}`;
        try {
            this.socket = new WebSocket(wsUrl);

            this.socket.onopen = () => {
                this.isConnected       = true;
                this.reconnectAttempts = 0;
                clearTimeout(this.reconnectTimer);
                this._emitStatus('connected');
                this._sendLogin(username);
                this._startPingMonitor();
            };

            this.socket.onmessage = (event) => {
                this._handleIncomingData(event.data);
            };

            this.socket.onerror = () => {
                if (this.onError) this.onError('Không thể kết nối tới WebSocket Gateway.');
            };

            this.socket.onclose = () => {
                const wasConnected = this.isConnected;
                this.isConnected = false;
                this._stopPingMonitor();
                this._emitStatus('disconnected');
                if (this.onClose) this.onClose();
                if (wasConnected && !this.isManualDisconnect && this.autoReconnect) {
                    this._attemptReconnect();
                }
            };
        } catch (e) {
            if (this.onError) this.onError(`Không thể mở WebSocket: ${e.message}`);
        }
    }

    /** Ngắt kết nối thủ công (người dùng bấm Đăng xuất). */
    disconnect() {
        this.isManualDisconnect = true;
        clearTimeout(this.reconnectTimer);
        this._stopPingMonitor();

        if (this.demoMode) {
            this._stopDemoMode();
            return;
        }
        this._sendLogoutPacket();
        if (this.socket) this.socket.close();
    }

    /* ── Tự động kết nối lại (Exponential Backoff: 1s → 2s → 4s → … max 16s) ── */
    _attemptReconnect() {
        if (this.reconnectAttempts >= this.maxReconnectAttempts) return;
        this.reconnectAttempts++;
        const delay = Math.min(1000 * Math.pow(2, this.reconnectAttempts - 1), 16000);
        if (this.onError) {
            this.onError(`Mất kết nối. Thử lại lần ${this.reconnectAttempts}/${this.maxReconnectAttempts} sau ${Math.round(delay / 1000)}s…`);
        }
        clearTimeout(this.reconnectTimer);
        this.reconnectTimer = setTimeout(() => {
            if (!this.isConnected && !this.isManualDisconnect) {
                this.connect(this.host, this.port, this.username, false);
            }
        }, delay);
    }

    /* ═══════════════════════════════════════════
       2. ĐO ĐỘ TRỄ (PING/PONG ở tầng ứng dụng JSON)
    ═══════════════════════════════════════════ */

    _startPingMonitor() {
        this._stopPingMonitor();
        this._pingInterval = setInterval(() => {
            if (this.isConnected && this.socket?.readyState === WebSocket.OPEN) {
                this._pingTimestamp = performance.now();
                // Gửi gói PING ứng dụng (server sẽ không crash vì không nhận ra action "PING",
                // nhưng sẽ reply ERROR → ta dùng thời gian từ gửi→nhận ERROR để đo RTT)
                const pingPacket = {
                    action: 'PING',
                    sender: this.username,
                    token:  this.token,
                    timestamp: Date.now()
                };
                try {
                    this.socket.send(JSON.stringify(pingPacket));
                } catch (_) {}
            }
        }, 8000);
    }

    _stopPingMonitor() {
        if (this._pingInterval) {
            clearInterval(this._pingInterval);
            this._pingInterval = null;
        }
    }

    /* ═══════════════════════════════════════════
       3. GỬI GÓI TIN (Đóng gói JSON – Outbound)
    ═══════════════════════════════════════════ */

    /**
     * [Private] Gửi gói tin JSON qua WebSocket (hoặc xử lý trong Demo mode).
     * @param {Object} dataObj
     */
    _sendJson(dataObj) {
        if (this.demoMode) {
            this._demoHandleOutbound(dataObj);
            return;
        }
        if (this.socket && this.socket.readyState === WebSocket.OPEN) {
            this.socket.send(JSON.stringify(dataObj));
        }
    }

    /** Gói tin LOGIN */
    _sendLogin(username) {
        this._sendJson({
            action:  'LOGIN',
            sender:  username,
            content: 'login_web'
        });
    }

    /**
     * Gửi tin nhắn chat.
     * @param {string} receiver – 'ALL' hoặc username cụ thể
     * @param {string} content
     */
    sendChatMessage(receiver, content) {
        const action = (receiver === 'ALL') ? 'CHAT_GROUP' : 'CHAT_SINGLE';
        this._sendJson({
            action,
            token:    this.token,
            sender:   this.username,
            receiver,
            content,
            timestamp: Date.now()
        });
    }

    /**
     * Gửi trạng thái đang gõ (TYPING) – đã được Debounce từ app.js.
     * @param {string} receiver
     */
    sendTypingStatus(receiver) {
        this._sendJson({
            action:   'TYPING',
            token:    this.token,
            sender:   this.username,
            receiver,
            content:  'is_typing'
        });
    }

    /** [Private] Gửi gói LOGOUT trước khi đóng socket. */
    _sendLogoutPacket() {
        if (this.isConnected) {
            this._sendJson({
                action:  'LOGOUT',
                token:   this.token,
                sender:  this.username,
                content: 'logout_web'
            });
        }
    }

    /* ═══════════════════════════════════════════
       4. XỬ LÝ GÓI TIN ĐẾN (Mở gói JSON – Inbound)
    ═══════════════════════════════════════════ */

    /**
     * Phân tích chuỗi JSON nhận từ Server và phân phát tới callback tương ứng.
     * @param {string} rawString
     */
    _handleIncomingData(rawString) {
        let msg;
        try {
            msg = JSON.parse(rawString);
        } catch (e) {
            console.error('[WS] JSON parse error:', e, rawString);
            return;
        }
        if (!msg || !msg.action) return;

        switch (msg.action) {
            /* ── Đăng nhập thành công: lưu token ── */
            case 'LOGIN_SUCCESS':
                this.token = msg.token || '';
                if (this.onLoginSuccess) this.onLoginSuccess(msg);
                break;

            /* ── Danh sách người dùng online ── */
            case 'USER_LIST':
                if (this.onUserListUpdated) {
                    try {
                        const users = typeof msg.content === 'string'
                            ? JSON.parse(msg.content)
                            : msg.content;
                        this.onUserListUpdated(Array.isArray(users) ? users : []);
                    } catch (_) {
                        console.warn('[WS] USER_LIST parse error');
                    }
                }
                break;

            /* ── Tin nhắn chat ── */
            case 'CHAT_GROUP':
            case 'CHAT_SINGLE':
                if (this.onMessageReceived) this.onMessageReceived(msg);
                break;

            /* ── Trạng thái gõ phím ── */
            case 'TYPING':
                if (this.onTyping) this.onTyping(msg);
                break;

            /* ── Phản hồi lỗi (cũng dùng để đo RTT cho PING) ── */
            case 'ERROR':
                /* Nếu là phản hồi cho gói PING → đo RTT */
                if (this._pingTimestamp > 0) {
                    const rtt = Math.round(performance.now() - this._pingTimestamp);
                    if (this.onLatencyUpdate) this.onLatencyUpdate(rtt);
                    this._pingTimestamp = 0;
                } else {
                    if (this.onError) this.onError(msg.content || 'Lỗi từ Server.');
                }
                break;

            default:
                console.debug('[WS] Unhandled action:', msg.action);
        }
    }

    /* ═══════════════════════════════════════════
       5. CHẾ ĐỘ DEMO (Offline – không cần Server)
    ═══════════════════════════════════════════ */

    _startDemoMode() {
        this.isConnected = true;
        this._emitStatus('connected');
        this._demoUserList = [this.username, 'Demo_ThuNghiem', 'Hung_Desktop'];

        /* Giả lập LOGIN_SUCCESS sau 400ms */
        setTimeout(() => {
            this.token = 'DEMO-TOKEN-OFFLINE';
            if (this.onLoginSuccess) {
                this.onLoginSuccess({
                    action: 'LOGIN_SUCCESS',
                    token:  this.token,
                    sender: 'SERVER',
                    receiver: this.username,
                    content: '[Demo] Đăng nhập thành công (Offline Mode)'
                });
            }
            /* Giả lập USER_LIST */
            if (this.onUserListUpdated) this.onUserListUpdated(this._demoUserList);

            /* Giả lập tin nhắn chào từ hệ thống sau 1.5s */
            this._demoTimer = setTimeout(() => {
                if (this.onMessageReceived) {
                    this.onMessageReceived({
                        action:    'CHAT_GROUP',
                        sender:    'Demo_ThuNghiem',
                        receiver:  'ALL',
                        content:   '👋 Xin chào! Đây là chế độ Demo offline. Server Java chưa chạy.',
                        timestamp: Date.now()
                    });
                }
            }, 1500);

            /* Giả lập latency */
            this._pingInterval = setInterval(() => {
                if (this.onLatencyUpdate) this.onLatencyUpdate(Math.floor(Math.random() * 12 + 8));
            }, 5000);
        }, 400);
    }

    _stopDemoMode() {
        this.isConnected = false;
        clearTimeout(this._demoTimer);
        this._stopPingMonitor();
        this._emitStatus('disconnected');
        if (this.onClose) this.onClose();
    }

    /**
     * Trong Demo mode: gói tin "gửi đi" → echo lại như Server giả.
     */
    _demoHandleOutbound(pkt) {
        if (pkt.action === 'CHAT_GROUP' || pkt.action === 'CHAT_SINGLE') {
            /* Echo ngược về để hiển thị (giả lập broadcast từ server) */
            setTimeout(() => {
                if (this.onMessageReceived) {
                    this.onMessageReceived({ ...pkt, _demo: true });
                }
            }, 120);
        }
        if (pkt.action === 'TYPING') {
            /* Demo: không làm gì thêm */
        }
        if (pkt.action === 'LOGOUT') {
            this._stopDemoMode();
        }
    }

    /* ═══════════════════════════════════════════
       6. HELPER
    ═══════════════════════════════════════════ */

    _emitStatus(status) {
        if (this.onStatusChange) this.onStatusChange(status);
    }
}
