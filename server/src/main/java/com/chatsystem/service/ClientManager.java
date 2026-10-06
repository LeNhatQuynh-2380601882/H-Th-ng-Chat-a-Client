package com.chatsystem.service;

import com.chatsystem.model.ChatMessage;
import com.chatsystem.model.MessageType;
import org.java_websocket.WebSocket;

import java.io.PrintWriter;
import java.util.*;
import java.util.concurrent.ConcurrentHashMap;

/**
 * Quản lý danh sách kết nối Client (TCP Socket & WebSocket), Session Token, Routing và Broadcast tin nhắn.
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
     * Tạo token đăng nhập ngẫu nhiên cho user
     */
    public String registerUserToken(String username) {
        String token = UUID.randomUUID().toString();
        sessionTokens.put(token, username);
        return token;
    }

    public boolean validateToken(String token, String username) {
        if (token == null || username == null) return false;
        return username.equals(sessionTokens.get(token));
    }

    public void addTcpClient(String username, PrintWriter writer) {
        tcpClients.put(username, writer);
        broadcastUserList();
    }

    public void removeTcpClient(String username) {
        if (username != null) {
            tcpClients.remove(username);
            sessionTokens.values().removeIf(val -> val.equals(username));
            broadcastUserList();
        }
    }

    public void addWebClient(String username, WebSocket conn) {
        webClients.put(username, conn);
        broadcastUserList();
    }

    public void removeWebClient(WebSocket conn) {
        String usernameToRemove = null;
        for (Map.Entry<String, WebSocket> entry : webClients.entrySet()) {
            if (entry.getValue().equals(conn)) {
                usernameToRemove = entry.getKey();
                break;
            }
        }
        if (usernameToRemove != null) {
            webClients.remove(usernameToRemove);
            sessionTokens.values().removeIf(val -> val.equals(usernameToRemove));
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
            writer.print(jsonPayload);
            writer.flush();
        }

        // Send to all WebSocket clients
        for (WebSocket conn : webClients.values()) {
            if (conn.isOpen()) {
                conn.send(message.toJson());
            }
        }
    }

    /**
     * Phát tin nhắn cập nhật danh sách các user đang online
     */
    public void broadcastUserList() {
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
