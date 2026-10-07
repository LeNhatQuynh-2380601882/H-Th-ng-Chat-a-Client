# HỆ THỐNG CHAT ĐA CLIENT (WEB/DESKTOP) VỚI GIAO THỨC JSON

**Học phần:** Lập trình mạng máy tính  
**Trường / Khoa:** HUTECH - Khoa Công nghệ Thông tin  
**Lớp:** 23DTHA6 | **Mã nhóm:** 11  
**Tên đề tài:** Xây dựng hệ thống chat đa nền tảng (Desktop/Web) Client-Server với giao thức đóng gói JSON trên kết nối TCP Socket & WebSocket.

---

## 👨‍💻 Danh sách thành viên & Bảng phân công đóng góp

| MSSV | Họ và Tên | Vai trò | Công việc đã thực hiện | Merge Requests / PR |
| :--- | :--- | :--- | :--- | :--- |
| **2380601882** | **Lê Nhật Quỳnh** | Nhóm trưởng | Khởi tạo khung sườn dự án, Xây dựng Core Server (TCP Socket & WebSocket Gateway), C# WinForms Desktop Client, HTML/CSS/JS Web Client | !1 |
| **2380600781** | **Phan Quốc Hùng** | Thành viên | Chi tiết công việc sẽ được phân công sau khi duyệt khung sườn | - |
| **2380601760** | **Trương Hoàng Phúc** | Thành viên | Xây dựng giao diện Web Chat Client (HTML5, CSS3, Bootstrap 5), lập trình WebSocket Client Native kết nối Gateway, xử lý đóng/mở gói JSON và kiểm thử liên nền tảng | !3 |

---

## 🏗 Cấu trúc Dự án (Mono-repo Architecture)

```plaintext
chat-system-json/
├── .gitignore                    # Cấu hình loại bỏ file rác (Java, C#, Web, IDEs)
├── README.md                     # Tài liệu giới thiệu & Bảng đóng góp đồ án
├── docs/                         # Tài liệu kỹ thuật & Mô tả Giao thức
│   └── architecture.md           # Sơ đồ kiến trúc & Chi tiết gói tin JSON Framing
├── server/                       # Java Core Server (TCP Socket + WebSocket Gateway)
│   ├── pom.xml                   # Cấu hình Maven dependencies (Gson, Java-WebSocket)
│   └── src/main/java/com/chatsystem/
│       ├── ServerMain.java       # Khởi chạy TCP Server (port 8888) & WebSocket (port 8887)
│       ├── model/                # Model dữ liệu gói tin JSON (ChatMessage, MessageType)
│       ├── socket/               # TCP Socket Listener & ClientHandler (\n framing)
│       ├── websocket/            # WebSocket Gateway Server cho Web Client
│       └── service/              # Quản lý danh sách Client, Session Token, Routing, Broadcast
├── desktop-client/               # C# WinForms Client (.NET 8.0 / .NET Framework)
│   ├── ChatDesktopApp.sln        # Visual Studio Solution
│   └── ChatDesktopApp/
│       ├── Program.cs            # Entry point ứng dụng WinForms
│       ├── Forms/                # Form Đăng nhập & Form Phòng Chat
│       ├── Services/             # TcpSocketClient.cs (TCP Socket, StreamReader \n)
│       └── Models/               # ChatMessage.cs
└── web-client/                   # Web Client (HTML5 / CSS3 / JavaScript)
    ├── index.html                # Giao diện Đăng nhập & Chat Room (Bootstrap 5 + Custom Glassmorphism)
    ├── css/style.css             # Style CSS hiện đại, responsive, hiệu ứng mượt mà
    └── js/
        ├── websocket.js          # Trình quản lý WebSocket API (kết nối, gửi JSON \n, auto reconnect)
        └── app.js                # Xử lý giao diện DOM, sự kiện gõ phím (typing), render tin nhắn
```

---

## ⚙️ Đặc tả Kỹ thuật & Giao thức

### 1. Chuẩn hóa Gói tin JSON
Toàn bộ dữ liệu truyền nhận giữa các Client và Server được đóng gói dưới định dạng JSON tiêu chuẩn:
```json
{
  "action": "LOGIN | CHAT_SINGLE | CHAT_GROUP | TYPING | LOGOUT",
  "token": "SESSION_TOKEN_STRING",
  "sender": "username",
  "receiver": "target_username_or_all",
  "content": "Nội dung tin nhắn",
  "timestamp": 1711000000
}
```

### 2. Kỹ thuật Xử lý Phân mảnh / Dồn gói TCP (Framing Protocol)
* **Quy tắc:** Mọi chuỗi JSON khi truyền qua TCP Socket hoặc WebSocket đều được đính kèm ký tự kết thúc Delimiter `\n` (LF) ở cuối gói.
* **Xử lý phía nhận:** Phía Server và C# Client đọc luồng mạng theo từng dòng (`ReadLine` / `BufferedReader.readLine()`) để đảm bảo parse trọn vẹn từng gói JSON độc lập.

### 3. Gateway WebSocket cho Web Client
* Trình duyệt web (JavaScript Native Browser) kết nối tới cổng WebSocket Server (`ws://localhost:8887`).
* Core Server Java đóng vai trò Gateway trung chuyển tin nhắn đồng bộ giữa Desktop Client (TCP Socket `8888`) và Web Client (`8887`).

---

## 🚀 Hướng dẫn Khởi chạy Hệ thống

### 1. Khởi chạy Server Java
```bash
cd server
mvn clean compile exec:java -Dexec.mainClass="com.chatsystem.ServerMain"
```
*(TCP Server sẽ lắng nghe tại `port 8888`, WebSocket Gateway lắng nghe tại `port 8887`)*

### 2. Khởi chạy Desktop Client (C# WinForms)
* Mở `desktop-client/ChatDesktopApp.sln` bằng Visual Studio.
* Nhấn `F5` hoặc `Ctrl + F5` để build và chạy ứng dụng.

### 3. Khởi chạy Web Client
* Mở trực tiếp file `web-client/index.html` trên trình duyệt (Chrome, Edge, Firefox) hoặc dùng Live Server.
