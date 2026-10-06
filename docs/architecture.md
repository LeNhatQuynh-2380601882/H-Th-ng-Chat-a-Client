# ARCHITECTURE & PROTOCOL SPECIFICATION
## Hệ thống Chat Đa Client (Web/Desktop) với giao thức JSON

### 1. Sơ đồ Kiến trúc Tổng quan (System Architecture)

```
 [ Desktop Client ]                     [ Web Client ]
  (C# WinForms)                         (HTML/JS Native WS)
       |                                         |
       | TCP Socket (Port 8888)                  | WebSocket (Port 8887)
       | Delimiter '\n' Framing                  | JSON Text Frame
       v                                         v
+------------------------------------------------------------------+
|                      CORE SERVER (JAVA 17+)                       |
|                                                                  |
|  +---------------------------+    +---------------------------+  |
|  |     TcpServer (8888)      |    |  WebSocketGateway (8887)  |  |
|  |   (ServerSocket Pool)     |    |    (Java-WebSocket)       |  |
|  +-------------+-------------+    +-------------+-------------+  |
|                |                                |                |
|                +---------------+----------------+                |
|                                v                                 |
|                  +---------------------------+                   |
|                  |       ClientManager       |                   |
|                  | (Routing, Token, State)   |                   |
|                  +---------------------------+                   |
+------------------------------------------------------------------+
```

### 2. Giao thức Đóng gói & Framing (Delimiter '\n')

Do bản chất của giao thức TCP là stream-oriented (dòng byte liên tục), dữ liệu gửi qua Socket có thể bị phân mảnh (Fragmentation) hoặc dồn gói (Coalescing).

- **Giải pháp áp dụng:** Kỹ thuật **Delimiter-based Framing** sử dụng ký tự `\n` (LF - ASCII `0x0A`).
- **Phía gửi:** Chuyển đổi object tin nhắn thành chuỗi JSON -> Đính kèm `\n` ở cuối -> Gửi qua Socket.
- **Phía nhận:** Đọc dữ liệu từ Socket bằng `BufferedReader.readLine()` (Java) hoặc `StreamReader.ReadLineAsync()` (C#). Khi thấy ký tự `\n`, hệ thống tách chuỗi trọn vẹn và tiến hành JSON parsing.

### 3. Cấu trúc Gói tin JSON (JSON Message Spec)

#### 3.1. Đăng nhập (LOGIN)
```json
{
  "action": "LOGIN",
  "sender": "le_nhat_quynh",
  "content": "password123",
  "timestamp": 1711000000
}
```

Phản hồi từ Server khi thành công:
```json
{
  "action": "LOGIN_SUCCESS",
  "token": "SESSION_TOKEN_UUID_99812",
  "sender": "SERVER",
  "receiver": "le_nhat_quynh",
  "content": "Đăng nhập thành công!",
  "timestamp": 1711000005
}
```

#### 3.2. Gửi tin nhắn cá nhân (CHAT_SINGLE)
```json
{
  "action": "CHAT_SINGLE",
  "token": "SESSION_TOKEN_UUID_99812",
  "sender": "le_nhat_quynh",
  "receiver": "phan_quoc_hung",
  "content": "Xin chào Hùng! Bạn đã test thử app C# chưa?",
  "timestamp": 1711000010
}
```

#### 3.3. Gửi tin nhắn nhóm / Broadcast (CHAT_GROUP)
```json
{
  "action": "CHAT_GROUP",
  "token": "SESSION_TOKEN_UUID_99812",
  "sender": "le_nhat_quynh",
  "receiver": "ALL",
  "content": "Chào mừng cả nhóm 11 đến với hệ thống chat đa nền tảng!",
  "timestamp": 1711000020
}
```

#### 3.4. Trạng thái đang gõ phím (TYPING)
```json
{
  "action": "TYPING",
  "token": "SESSION_TOKEN_UUID_99812",
  "sender": "le_nhat_quynh",
  "receiver": "phan_quoc_hung",
  "content": "is_typing",
  "timestamp": 1711000025
}
```

#### 3.5. Cập nhật Danh sách Online (USER_LIST)
```json
{
  "action": "USER_LIST",
  "sender": "SERVER",
  "content": "[\"le_nhat_quynh\", \"phan_quoc_hung\", \"truong_hoang_phuc\"]",
  "timestamp": 1711000030
}
```

---

### 4. Cơ chế Bảo mật Session Token
Khi người dùng đăng nhập thành công, Server tạo ra một Session Token duy nhất (UUID) lưu vào bộ nhớ tạm. Mọi tin nhắn tiếp theo gửi từ Client bắt buộc phải có `token` hợp lệ trong JSON Header. Nút Server sẽ kiểm tra Token trước khi định tuyến tin nhắn, ngăn chặn mạo danh người dùng (Identity Spoofing).
