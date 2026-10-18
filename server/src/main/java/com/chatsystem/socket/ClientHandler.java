package com.chatsystem.socket;

import com.chatsystem.model.ChatMessage;
import com.chatsystem.model.MessageType;
import com.chatsystem.service.ClientManager;

import java.io.BufferedReader;
import java.io.InputStreamReader;
import java.io.OutputStreamWriter;
import java.io.PrintWriter;
import java.net.Socket;
import java.nio.charset.StandardCharsets;

/**
 * Xử lý kết nối TCP Socket của 1 Desktop Client trong Thread riêng biệt.
 * Bắt buộc xử lý framing bằng ký tự phân cách '\n'.
 * Tích hợp B1/B2: Kiểm tra trùng username nguyên tử và dọn đúng phiên khi ngắt kết nối.
 */
public class ClientHandler implements Runnable {
    private final Socket socket;
    private final ClientManager clientManager = ClientManager.getInstance();
    private String currentUsername;
    private PrintWriter writer;

    public ClientHandler(Socket socket) {
        this.socket = socket;
    }

    @Override
    public void run() {
        try (
            BufferedReader reader = new BufferedReader(
                new InputStreamReader(socket.getInputStream(), StandardCharsets.UTF_8));
            PrintWriter outWriter = new PrintWriter(
                new OutputStreamWriter(socket.getOutputStream(), StandardCharsets.UTF_8), true)
        ) {
            this.writer = outWriter;
            String rawLine;

            // Đọc dữ liệu theo từng dòng (Kỹ thuật Delimiter \n Framing)
            while ((rawLine = reader.readLine()) != null) {
                if (rawLine.trim().isEmpty()) continue;

                ChatMessage msg = ChatMessage.fromJson(rawLine);
                if (msg == null) {
                    sendError("Gói tin JSON không hợp lệ");
                    continue;
                }

                handleMessage(msg);
            }
        } catch (Exception e) {
            System.err.println("Lỗi kết nối TCP client (" + currentUsername + "): " + e.getMessage());
        } finally {
            if (currentUsername != null && writer != null) {
                clientManager.removeTcpClient(currentUsername, writer);
                System.out.println("Client TCP ngắt kết nối: " + currentUsername);
            }
            try { socket.close(); } catch (Exception ignored) {}
        }
    }

    private void handleMessage(ChatMessage msg) {
        MessageType action = msg.getAction();

        if (action == MessageType.LOGIN) {
            String username = msg.getSender();
            if (username == null || username.trim().isEmpty()) {
                sendError("Tên người dùng không được để trống!");
                return;
            }

            String sessionToken = clientManager.registerTcpClient(username, writer);
            if (sessionToken == null) {
                sendError("Tên đăng nhập '" + username + "' đã được sử dụng hoặc không hợp lệ. Vui lòng chọn tên khác.");
                return;
            }

            this.currentUsername = username;
            ChatMessage resp = new ChatMessage(MessageType.LOGIN_SUCCESS, "SERVER", username, "Đăng nhập thành công!");
            resp.setToken(sessionToken);
            sendJson(resp);
            clientManager.broadcastUserList();
            System.out.println("TCP Client đăng nhập thành công: " + username);
            return;
        }

        // Kiểm tra Session Token với các hành động khác
        if (!clientManager.validateToken(msg.getToken(), msg.getSender())) {
            sendError("Session Token không hợp lệ hoặc đã hết hạn!");
            return;
        }

        switch (action) {
            case CHAT_SINGLE:
                boolean delivered = clientManager.sendDirectMessage(msg);
                if (!delivered) {
                    sendError("Người nhận '" + msg.getReceiver() + "' không online.");
                }
                break;

            case CHAT_GROUP:
                clientManager.broadcastMessage(msg);
                break;

            case TYPING:
                if ("ALL".equalsIgnoreCase(msg.getReceiver())) {
                    clientManager.broadcastMessage(msg);
                } else {
                    clientManager.sendDirectMessage(msg);
                }
                break;

            case FILE_TRANSFER:
                if ("ALL".equalsIgnoreCase(msg.getReceiver())) {
                    clientManager.broadcastMessage(msg);
                } else {
                    boolean sent = clientManager.sendDirectMessage(msg);
                    if (!sent) {
                        sendError("Người nhận '" + msg.getReceiver() + "' không online.");
                    }
                }
                break;

            case LOGOUT:
                clientManager.removeTcpClient(currentUsername, writer);
                this.currentUsername = null;
                break;

            default:
                sendError("Hành động không hỗ trợ.");
                break;
        }
    }

    private void sendJson(ChatMessage msg) {
        if (writer != null) {
            writer.print(msg.toJson() + "\n");
            writer.flush();
        }
    }

    private void sendError(String errorMsg) {
        ChatMessage err = new ChatMessage(MessageType.ERROR, "SERVER", currentUsername, errorMsg);
        sendJson(err);
    }
}
