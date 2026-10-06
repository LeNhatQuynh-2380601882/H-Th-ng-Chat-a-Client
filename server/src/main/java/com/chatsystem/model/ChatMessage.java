package com.chatsystem.model;

import com.google.gson.Gson;

/**
 * Model đại diện cho 1 gói tin JSON trong hệ thống Chat.
 */
public class ChatMessage {
    private MessageType action;
    private String token;
    private String sender;
    private String receiver;
    private String content;
    private long timestamp;

    private static final Gson gson = new Gson();

    public ChatMessage() {
        this.timestamp = System.currentTimeMillis();
    }

    public ChatMessage(MessageType action, String sender, String receiver, String content) {
        this.action = action;
        this.sender = sender;
        this.receiver = receiver;
        this.content = content;
        this.timestamp = System.currentTimeMillis();
    }

    public MessageType getAction() {
        return action;
    }

    public void setAction(MessageType action) {
        this.action = action;
    }

    public String getToken() {
        return token;
    }

    public void setToken(String token) {
        this.token = token;
    }

    public String getSender() {
        return sender;
    }

    public void setSender(String sender) {
        this.sender = sender;
    }

    public String getReceiver() {
        return receiver;
    }

    public void setReceiver(String receiver) {
        this.receiver = receiver;
    }

    public String getContent() {
        return content;
    }

    public void setContent(String content) {
        this.content = content;
    }

    public long getTimestamp() {
        return timestamp;
    }

    public void setTimestamp(long timestamp) {
        this.timestamp = timestamp;
    }

    public String toJson() {
        return gson.toJson(this);
    }

    public static ChatMessage fromJson(String jsonStr) {
        try {
            return gson.fromJson(jsonStr, ChatMessage.class);
        } catch (Exception e) {
            return null;
        }
    }
}
