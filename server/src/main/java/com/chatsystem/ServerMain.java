package com.chatsystem;

import com.chatsystem.socket.TcpServer;
import com.chatsystem.websocket.WebSocketGateway;

/**
 * Main Entry Point của Core Server Java.
 * Khởi chạy đồng thời TCP Socket Server (Port 8888) và WebSocket Gateway (Port 8887).
 */
public class ServerMain {
    public static final int TCP_PORT = 8888;
    public static final int WEBSOCKET_PORT = 8887;

    public static void main(String[] args) {
        System.out.println("=================================================");
        System.out.println("   HỆ THỐNG CHAT ĐA CLIENT - CORE SERVER (JAVA)  ");
        System.out.println("   Lập trình mạng máy tính - Nhóm 11 - 23DTHA6    ");
        System.out.println("=================================================");

        // 1. Khởi chạy TCP Server cho Desktop Client
        TcpServer tcpServer = new TcpServer(TCP_PORT);
        Thread tcpThread = new Thread(tcpServer, "TcpServer-Thread");
        tcpThread.start();

        // 2. Khởi chạy WebSocket Gateway cho Web Client
        WebSocketGateway webSocketGateway = new WebSocketGateway(WEBSOCKET_PORT);
        webSocketGateway.start();

        Runtime.getRuntime().addShutdownHook(new Thread(() -> {
            System.out.println(">>> Đang dừng Server...");
            tcpServer.stop();
            try {
                webSocketGateway.stop();
            } catch (Exception ignored) {}
            System.out.println(">>> Server đã dừng hoàn toàn.");
        }));
    }
}
