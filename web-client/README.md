# Tài Liệu Kỹ Thuật Web Client (Nhóm 11)

**Học phần:** Lập trình mạng máy tính  
**Người thực hiện:** Trương Hoàng Phúc (MSSV: `2380601760`)  
**Vị trí:** Lập trình viên Web Client (HTML5 / CSS3 / JavaScript Native)

---

## 1. Giới thiệu tổng quan

Mô-đun Web Client là ứng dụng web hoạt động trực tiếp trên trình duyệt, giao tiếp thời gian thực với **Core Server (Java WebSocket Gateway)** thông qua chuẩn **WebSocket (RFC 6455)** và trao đổi dữ liệu chéo nền tảng với **Desktop Client (C# WinForms)**.

### Điểm nổi bật kỹ thuật:
- **Chuẩn kết nối Native:** Sử dụng trực tiếp `new WebSocket('ws://host:port')` chuẩn của trình duyệt (không phụ thuộc thư viện bên ngoài).
- **Phân tách mô hình rõ ràng:** 
  - `websocket.js`: Quản lý tầng mạng (Network Layer), phiên làm việc (Session Token), cơ chế tự kết nối lại (Auto-reconnect với Exponential Backoff), theo dõi độ trễ mạng (Ping/Latency Monitor).
  - `app.js`: Quản lý tầng giao diện (Presentation Layer), phân phối sự kiện DOM, thanh emoji nhanh, bộ tổng hợp âm thanh tin nhắn (Web Audio API), xuất lịch sử chat.
- **Responsive & Glassmorphism UI:** Thiết kế hiện đại với Bootstrap 5, FontAwesome và hiệu ứng kính mờ (Backdrop filter blur).

---

## 2. Cấu trúc thư mục mô-đun

```plaintext
web-client/
├── index.html          # Giao diện chính (Màn hình Đăng nhập & Khung Chat thời gian thực)
├── ws-tester.html      # Công cụ kiểm thử độc lập các gói tin JSON gửi/nhận thô
├── css/
│   └── style.css       # Định dạng giao diện, hiệu ứng bong bóng chat, thanh cuộn, emoji
├── js/
│   ├── websocket.js    # WebSocketClientManager (quản lý kết nối, đóng/mở gói JSON, token)
│   └── app.js          # Logic giao diện, sự kiện gõ phím, âm thanh, xuất log
└── README.md           # Tài liệu kỹ thuật chi tiết của mô-đun
```

---

## 3. Đặc tả Giao thức JSON Đóng gói (Protocol Specification)

Mọi gói tin truyền qua kênh WebSocket đều sử dụng chuỗi JSON mã hóa UTF-8 với định dạng:

### A. Gói Đăng nhập (`LOGIN`)
- **Web Client gửi lên:**
  ```json
  {
    "action": "LOGIN",
    "sender": "2380601760_TruongHoangPhuc",
    "content": "login_web"
  }
  ```
- **Server phản hồi thành công (`LOGIN_SUCCESS`):**
  ```json
  {
    "action": "LOGIN_SUCCESS",
    "token": "d98f7e2a-1b4c-4e89-9a21-abcdef123456",
    "sender": "SERVER",
    "receiver": "2380601760_TruongHoangPhuc",
    "content": "Xác thực thành công",
    "timestamp": 1711000000
  }
  ```
  *(Web Client tự động lưu `token` này vào bộ nhớ phiên để chứng thực tất cả các yêu cầu tiếp theo).*

---

### B. Gói Tin nhắn Chat Nhóm (`CHAT_GROUP`) & Cá nhân (`CHAT_SINGLE`)
- **Gửi tin nhắn nhóm:**
  ```json
  {
    "action": "CHAT_GROUP",
    "token": "d98f7e2a-1b4c-4e89-9a21-abcdef123456",
    "sender": "2380601760_TruongHoangPhuc",
    "receiver": "ALL",
    "content": "Chào nhóm, tôi đã kết nối thành công từ Web Client!",
    "timestamp": 1711000050
  }
  ```
- **Gửi tin nhắn 1-1 tới Desktop Client (Hùng):**
  ```json
  {
    "action": "CHAT_SINGLE",
    "token": "d98f7e2a-1b4c-4e89-9a21-abcdef123456",
    "sender": "2380601760_TruongHoangPhuc",
    "receiver": "2380600781_PhanQuocHung",
    "content": "Chào bạn, đây là tin nhắn cá nhân từ Web.",
    "timestamp": 1711000080
  }
  ```

---

### C. Gói Trạng thái đang nhập (`TYPING`)
- Khi người dùng gõ vào ô nhập tin nhắn, sự kiện `input` kích hoạt gửi gói tin:
  ```json
  {
    "action": "TYPING",
    "token": "d98f7e2a-1b4c-4e89-9a21-abcdef123456",
    "sender": "2380601760_TruongHoangPhuc",
    "receiver": "ALL",
    "content": "is_typing"
  }
  ```
- Phía nhận hiển thị thông báo `[Tên] đang nhập tin nhắn...` và tự động tắt sau 2.5 giây.

---

### D. Gói Đăng xuất (`LOGOUT`)
- Khi nhấn nút Đăng xuất / Rời phòng:
  ```json
  {
    "action": "LOGOUT",
    "token": "d98f7e2a-1b4c-4e89-9a21-abcdef123456",
    "sender": "2380601760_TruongHoangPhuc",
    "content": "logout_web"
  }
  ```

---

## 4. Hướng dẫn Chạy và Kiểm thử

1. **Khởi chạy bình thường:** Mở file `web-client/index.html` bằng trình duyệt web bất kỳ (Chrome / Edge / Firefox).
2. **Kiểm thử giao thức nâng cao:** Mở file `web-client/ws-tester.html` để theo dõi nhật ký JSON chi tiết.
3. **Cấu hình mạng:**
   - Test nội bộ (Localhost): `Host: 127.0.0.1`, `Port: 8887`.
   - Test liên máy qua ZeroTier: `Host: <IP_ZeroTier_Server>`, `Port: 8887`.
