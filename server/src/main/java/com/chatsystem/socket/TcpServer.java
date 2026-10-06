package com.chatsystem.socket;

import java.net.ServerSocket;
import java.net.Socket;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

/**
 * ServerSocket lắng nghe các kết nối TCP từ Desktop Client.
 */
public class TcpServer implements Runnable {
    private final int port;
    private final ExecutorService threadPool = Executors.newCachedThreadPool();
    private boolean running = true;

    public TcpServer(int port) {
        this.port = port;
    }

    @Override
    public void run() {
        try (ServerSocket serverSocket = new ServerSocket(port)) {
            System.out.println(">>> [TCP Server] Đang lắng nghe kết nối tại cổng: " + port);

            while (running) {
                Socket socket = serverSocket.accept();
                threadPool.execute(new ClientHandler(socket));
            }
        } catch (Exception e) {
            System.err.println("Lỗi ServerSocket: " + e.getMessage());
        } finally {
            threadPool.shutdown();
        }
    }

    public void stop() {
        this.running = false;
    }
}
