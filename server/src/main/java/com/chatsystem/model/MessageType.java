package com.chatsystem.model;

/**
 * Các loại hành động (Action) hỗ trợ trong giao thức JSON của hệ thống Chat.
 */
public enum MessageType {
    LOGIN,          // Client gửi yêu cầu đăng nhập
    LOGIN_SUCCESS,  // Server phản hồi thành công kèm Session Token
    LOGIN_FAILED,   // Server phản hồi thất bại
    CHAT_SINGLE,    // Tin nhắn cá nhân 1-1
    CHAT_GROUP,     // Tin nhắn nhóm (Broadcast)
    TYPING,         // Trạng thái đang gõ phím ("User is typing...")
    USER_LIST,      // Danh sách người dùng online
    LOGOUT,         // Client đăng xuất
    SYSTEM,         // Thông báo hệ thống
    ERROR           // Thông báo lỗi gói tin / token không hợp lệ
}
