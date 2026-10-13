export type MessageDirection = 'Inbound' | 'Outbound';

export interface ConversationSummary {
  conversationId: string;
  contactId: string;
  displayName: string;
  username: string | null;
  lastMessageText: string;
  lastMessageAt: string;
  unreadCount: number;
}

export interface ConversationMessage {
  id: string;
  conversationId: string;
  direction: MessageDirection;
  text: string;
  isRead: boolean;
  sentAt: string;
}

export interface RealtimeMessage {
  id: string;
  direction: MessageDirection;
  text: string;
  sentAt: string;
  isRead: boolean;
}

export interface MessageRealtimeEvent {
  conversationId: string;
  message: RealtimeMessage;
}

export interface MessagesReadRealtimeEvent {
  conversationId: string;
  messageIds: string[];
}

export interface SendMessageRequest {
  text: string;
}

export type RealtimeConnectionState = 'connecting' | 'connected' | 'reconnecting' | 'disconnected';
