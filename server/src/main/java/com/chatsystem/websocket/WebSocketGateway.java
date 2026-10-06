package com.chatsystem.websocket;

import com.chatsystem.model.ChatMessage;
import com.chatsystem.model.MessageType;
import com.chatsystem.service.ClientManager;
import org.java_websocket.WebSocket;
import org.java_websocket.handshake.ClientHandshake;
import org.java_websocket.server.WebSocketServer;

import java.net.InetSocketAddress;

/**
 * WebSocket Gateway cho phép Web Client (HTML/JS Trình duyệt) kết nối thời gian thực.
 */
public class WebSocketGateway extends WebSocketServer {

    private final ClientManager clientManager = ClientManager.getInstance();

    public WebSocketGateway(int port) {
        super(new InetSocketAddress(port));
    }

    @Override
    public void onOpen(WebSocket conn, ClientHandshake handshake) {
        System.out.println(">>> [WebSocket Gateway] Kết nối mới từ: " + conn.getRemoteSocketAddress());
    }

    @Override
    public void onClose(WebSocket conn, int code, String reason, boolean remote) {
        clientManager.removeWebClient(conn);
        System.out.println(">>> [WebSocket Gateway] Đã ngắt kết nối: " + conn.getRemoteSocketAddress());
    }

    @Override
    public void onMessage(WebSocket conn, String messageStr) {
        ChatMessage msg = ChatMessage.fromJson(messageStr);
        if (msg == null) {
            sendError(conn, "Gói tin JSON không hợp lệ.");
            return;
        }

        MessageType action = msg.getAction();

        if (action == MessageType.LOGIN) {
            String username = msg.getSender();
            if (username == null || username.trim().isEmpty()) {
                sendError(conn, "Tên người dùng không được để trống!");
                return;
            }

            String sessionToken = clientManager.registerUserToken(username);
            clientManager.addWebClient(username, conn);

            ChatMessage resp = new ChatMessage(MessageType.LOGIN_SUCCESS, "SERVER", username, "Đăng nhập WebSocket thành công!");
            resp.setToken(sessionToken);
            conn.send(resp.toJson());
            System.out.println("Web Client đăng nhập thành công: " + username);
            return;
        }

        // Validate Token
        if (!clientManager.validateToken(msg.getToken(), msg.getSender())) {
            sendError(conn, "Session Token không hợp lệ!");
            return;
        }

        switch (action) {
            case CHAT_SINGLE:
                boolean delivered = clientManager.sendDirectMessage(msg);
                if (!delivered) {
                    sendError(conn, "Người nhận '" + msg.getReceiver() + "' không online.");
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

            case LOGOUT:
                clientManager.removeWebClient(conn);
                break;

            default:
                sendError(conn, "Hành động không hỗ trợ.");
                break;
        }
    }

    @Override
    public void onError(WebSocket conn, Exception ex) {
        System.err.println("Lỗi WebSocket Gateway: " + ex.getMessage());
    }

    @Override
    public void onStart() {
        System.out.println(">>> [WebSocket Gateway] Server đã sẵn sàng lắng nghe tại cổng: " + getPort());
    }

    private void sendError(WebSocket conn, String errorMsg) {
        if (conn != null && conn.isOpen()) {
            ChatMessage err = new ChatMessage(MessageType.ERROR, "SERVER", null, errorMsg);
            conn.send(err.toJson());
        }
    }
}
