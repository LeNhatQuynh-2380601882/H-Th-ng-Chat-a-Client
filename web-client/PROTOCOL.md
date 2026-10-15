# Giao thức JSON Web Client ⇄ Server Java ⇄ Desktop Client

**Quy tắc chung**
- Kết nối qua WebSocket (`ws://host:port`). Mỗi thông điệp là **một dòng JSON UTF-8 kết thúc bằng `\n`**.
- Web Client mã hoá bằng `JSON.stringify(obj) + "\n"` và giải mã bằng `JSON.parse(line)` cho từng dòng.
- Trường `time` có dạng `HH:mm` (giờ địa phương của người gửi). Nếu thiếu, Web Client dùng giờ nhận.
- Tên phòng (`room`) là chuỗi id ngắn, ví dụ `chung`, `hoctap`, `giaitri`.

## Client → Server

| type | Trường | Ví dụ |
|---|---|---|
| `login` | `username`, `password` | `{"type":"login","username":"Phuc","password":"1234"}` |
| `join` | `room` | `{"type":"join","room":"chung"}` |
| `message` | `room`, `text`, `time` | `{"type":"message","room":"chung","text":"Xin chào","time":"09:15"}` |
| `typing` | `room`, `typing` (true/false) | `{"type":"typing","room":"chung","typing":true}` |
| `logout` | — | `{"type":"logout"}` |

## Server → Client

| type | Trường bắt buộc | Trường tuỳ chọn |
|---|---|---|
| `login_ok` | `username` | `users` `[{name,online}]`, `rooms` `[{id,title,icon,desc}]`, `history` `{roomId:[{from,text,time}]}` |
| `login_error` | — | `message` |
| `message` | `from`, `text` | `room`, `time` |
| `typing` | `from`, `typing` | `room` |
| `users` | `users` `[{name,online}]` | — |
| `history` | `room`, `messages` `[{from,text,time}]` | — |
| `system` | `text` | `room` |
| `error` | — | `message` |

## Lưu ý để hai client tương thích
1. Server nên **broadcast `message` và `typing` cho mọi client khác trong phòng** (không cần gửi lại cho người gửi; nếu có gửi lại, Web Client tự bỏ qua tin có `from` trùng tên mình).
2. Server nên đặt `from` = `username` đã đăng nhập, không tin giá trị client tự gửi.
3. Desktop Client cần gửi/nhận **đúng tên trường ở trên** (phân biệt hoa thường) và kết thúc mỗi thông điệp bằng `\n`.
4. Một khung WebSocket có thể chứa nhiều dòng JSON; bên nhận phải tách theo `\n`.
5. Khi nhận `type` lạ hoặc thiếu trường bắt buộc, Web Client ghi cảnh báo vào Console (`Thông điệp lệch giao thức`) nhưng không bị dừng.

## Cách kiểm tra tương thích
Mở `ws-tester.html`, kết nối tới server, gửi từng loại thông điệp và xem Desktop Client có nhận đúng không (và ngược lại: nhắn từ Desktop, xem dòng JSON thô hiện ở khung nhật ký có báo ✔ hợp lệ hay ✖ lệch giao thức).
