package com.chatsystem.service;

import com.chatsystem.model.ChatMessage;
import com.chatsystem.model.MessageType;
import org.java_websocket.WebSocket;

import java.io.PrintWriter;
import java.util.*;
import java.util.concurrent.ConcurrentHashMap;

/**
 * Quản lý danh sách kết nối Client (TCP Socket & WebSocket), Session Token, Routing và Broadcast tin nhắn.
 * Đảm bảo đăng ký phiên nguyên tử (Atomic Registration), chống trùng username trên toàn hệ thống và
 * dọn đúng phiên khi ngắt kết nối (Proper Session Cleanup).
 */
public class ClientManager {
    private static final ClientManager instance = new ClientManager();

    // Map username -> TCP Client Writer
    private final Map<String, PrintWriter> tcpClients = new ConcurrentHashMap<>();
    
    // Map username -> WebSocket Connection
    private final Map<String, WebSocket> webClients = new ConcurrentHashMap<>();

    // Map session token -> username
    private final Map<String, String> sessionTokens = new ConcurrentHashMap<>();

    private ClientManager() {}

    public static ClientManager getInstance() {
        return instance;
    }

    /**
     * Kiểm tra username có hợp lệ không (không rỗng, không trùng từ khóa hệ thống).
     */
    public boolean isValidUsername(String username) {
        if (username == null) return false;
        String trimmed = username.trim();
        if (trimmed.isEmpty()) return false;
        if ("ALL".equalsIgnoreCase(trimmed) || "SERVER".equalsIgnoreCase(trimmed)) return false;
        return true;
    }

    /**
     * Kiểm tra username có đang online trên TCP hoặc WebSocket không (case-insensitive).
     */
    public synchronized boolean isUserOnline(String username) {
        if (username == null) return false;
        for (String u : tcpClients.keySet()) {
            if (u.equalsIgnoreCase(username)) return true;
        }
        for (String u : webClients.keySet()) {
            if (u.equalsIgnoreCase(username)) return true;
        }
        return false;
    }

    /**
     * Đăng ký nguyên tử (Atomic) cho TCP Client.
     * @return sessionToken nếu thành công, null nếu username đã online hoặc không hợp lệ.
     */
    public synchronized String registerTcpClient(String username, PrintWriter writer) {
        if (!isValidUsername(username) || writer == null) {
            return null;
        }
        if (isUserOnline(username)) {
            return null;
        }
        String token = UUID.randomUUID().toString();
        sessionTokens.put(token, username);
        tcpClients.put(username, writer);
        return token;
    }

    /**
     * Đăng ký nguyên tử (Atomic) cho Web Client.
     * @return sessionToken nếu thành công, null nếu username đã online hoặc không hợp lệ.
     */
    public synchronized String registerWebClient(String username, WebSocket conn) {
        if (!isValidUsername(username) || conn == null) {
            return null;
        }
        if (isUserOnline(username)) {
            return null;
        }
        String token = UUID.randomUUID().toString();
        sessionTokens.put(token, username);
        webClients.put(username, conn);
        return token;
    }

    public boolean validateToken(String token, String username) {
        if (token == null || username == null) return false;
        String registeredUser = sessionTokens.get(token);
        return registeredUser != null && registeredUser.equalsIgnoreCase(username);
    }

    /**
     * Gỡ phiên TCP Client an toàn: chỉ gỡ khi writer khớp với writer của phiên hiện tại.
     */
    public synchronized void removeTcpClient(String username, PrintWriter writer) {
        if (username == null || writer == null) return;
        PrintWriter currentWriter = tcpClients.get(username);
        if (currentWriter != null && currentWriter.equals(writer)) {
            tcpClients.remove(username);
            sessionTokens.values().removeIf(val -> val.equalsIgnoreCase(username));
            broadcastUserList();
        }
    }

    /**
     * Gỡ phiên TCP Client theo username (fallback).
     */
    public synchronized void removeTcpClient(String username) {
        if (username != null) {
            tcpClients.remove(username);
            sessionTokens.values().removeIf(val -> val.equalsIgnoreCase(username));
            broadcastUserList();
        }
    }

    /**
     * Gỡ phiên Web Client an toàn theo đúng đối tượng kết nối WebSocket.
     */
    public synchronized void removeWebClient(WebSocket conn) {
        if (conn == null) return;
        String usernameToRemove = null;
        for (Map.Entry<String, WebSocket> entry : webClients.entrySet()) {
            if (entry.getValue().equals(conn)) {
                usernameToRemove = entry.getKey();
                break;
            }
        }
        if (usernameToRemove != null) {
            webClients.remove(usernameToRemove);
            final String finalUser = usernameToRemove;
            sessionTokens.values().removeIf(val -> val.equalsIgnoreCase(finalUser));
            broadcastUserList();
        }
    }

    /**
     * Gửi tin nhắn đến đúng Client (1-1)
     */
    public boolean sendDirectMessage(ChatMessage message) {
        String receiver = message.getReceiver();
        String jsonPayload = message.toJson() + "\n"; // Framing '\n'
        boolean sent = false;

        // Gửi tới TCP Client (nếu nhận)
        PrintWriter tcpWriter = tcpClients.get(receiver);
        if (tcpWriter != null) {
            tcpWriter.print(jsonPayload);
            tcpWriter.flush();
            sent = true;
        }

        // Gửi tới WebSocket Client (nếu nhận)
        WebSocket wsConn = webClients.get(receiver);
        if (wsConn != null && wsConn.isOpen()) {
            wsConn.send(message.toJson());
            sent = true;
        }

        return sent;
    }

    /**
     * Gửi tin nhắn nhóm tới tất cả các Client (Broadcast)
     */
    public void broadcastMessage(ChatMessage message) {
        String jsonPayload = message.toJson() + "\n"; // Framing '\n'

        // Send to all TCP clients
        for (PrintWriter writer : tcpClients.values()) {
            try {
                writer.print(jsonPayload);
                writer.flush();
            } catch (Exception ignored) {}
        }

        // Send to all WebSocket clients
        for (WebSocket conn : webClients.values()) {
            try {
                if (conn.isOpen()) {
                    conn.send(message.toJson());
                }
            } catch (Exception ignored) {}
        }
    }

    /**
     * Phát tin nhắn cập nhật danh sách các user đang online
     */
    public synchronized void broadcastUserList() {
        Set<String> onlineUsers = new HashSet<>();
        onlineUsers.addAll(tcpClients.keySet());
        onlineUsers.addAll(webClients.keySet());

        ChatMessage userListMsg = new ChatMessage();
        userListMsg.setAction(MessageType.USER_LIST);
        userListMsg.setSender("SERVER");
        userListMsg.setReceiver("ALL");
        userListMsg.setContent(new com.google.gson.Gson().toJson(onlineUsers));

        broadcastMessage(userListMsg);
    }
}
