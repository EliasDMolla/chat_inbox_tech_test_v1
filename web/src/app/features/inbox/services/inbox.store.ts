import { HttpErrorResponse } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { firstValueFrom, Subscription } from 'rxjs';
import {
  ConversationMessage,
  ConversationSummary,
  MessageRealtimeEvent,
  MessagesReadRealtimeEvent,
  RealtimeConnectionState,
} from '../../../core/models/inbox.models';
import { ConversationsApiService } from '../../../core/services/conversations-api.service';
import { ConversationsRealtimeService } from '../../../core/services/conversations-realtime.service';

@Injectable()
export class InboxStore {
  private readonly api = inject(ConversationsApiService);
  private readonly realtime = inject(ConversationsRealtimeService);
  private readonly subscriptions = new Subscription();
  private readonly readInFlight = new Set<string>();
  private readonly inboundVersions = new Map<string, number>();
  private messagesRequest?: Subscription;
  private conversationsRequest?: Subscription;
  private initialized = false;

  readonly conversations = signal<ConversationSummary[]>([]);
  readonly selectedConversationId = signal<string | null>(null);
  readonly messages = signal<ConversationMessage[]>([]);
  readonly conversationsLoading = signal(false);
  readonly messagesLoading = signal(false);
  readonly sendingMessage = signal(false);
  readonly realtimeConnectionState = signal<RealtimeConnectionState>('disconnected');
  readonly error = signal<string | null>(null);
  readonly selectedConversation = computed(
    () =>
      this.conversations().find((item) => item.conversationId === this.selectedConversationId()) ??
      null,
  );

  initialize(): void {
    if (this.initialized) return;
    this.initialized = true;
    this.bindRealtimeEvents();
    this.loadConversations();
    void this.realtime.start();
  }

  destroy(): void {
    this.messagesRequest?.unsubscribe();
    this.conversationsRequest?.unsubscribe();
    this.subscriptions.unsubscribe();
    void this.realtime.stop();
  }

  dismissError(): void {
    this.error.set(null);
  }
  retryConversations(): void {
    this.loadConversations();
  }

  selectConversation(conversationId: string): void {
    if (conversationId === this.selectedConversationId() && !this.messagesLoading()) return;
    this.selectedConversationId.set(conversationId);
    this.messages.set([]);
    this.loadMessages(conversationId);
    const conversation = this.conversations().find(
      (item) => item.conversationId === conversationId,
    );
    if (conversation && conversation.unreadCount > 0) this.markAsRead(conversationId);
  }

  async sendMessage(text: string): Promise<boolean> {
    const conversationId = this.selectedConversationId();
    const normalizedText = text.trim();
    if (
      !conversationId ||
      this.sendingMessage() ||
      normalizedText.length === 0 ||
      normalizedText.length > 4096
    )
      return false;
    this.sendingMessage.set(true);
    this.error.set(null);
    try {
      const message = await firstValueFrom(
        this.api.sendMessage(conversationId, { text: normalizedText }),
      );
      if (this.selectedConversationId() === conversationId) this.addMessage(message);
      return true;
    } catch (error: unknown) {
      this.error.set(this.messageForSendError(error));
      return false;
    } finally {
      this.sendingMessage.set(false);
    }
  }

  private bindRealtimeEvents(): void {
    this.subscriptions.add(
      this.realtime.connectionState$.subscribe((state) => this.realtimeConnectionState.set(state)),
    );
    this.subscriptions.add(
      this.realtime.messageReceived$.subscribe((event) => this.handleMessageReceived(event)),
    );
    this.subscriptions.add(
      this.realtime.messageSent$.subscribe((event) => this.handleMessageSent(event)),
    );
    this.subscriptions.add(
      this.realtime.conversationUpdated$.subscribe((conversation) =>
        this.upsertConversation(conversation),
      ),
    );
    this.subscriptions.add(
      this.realtime.messagesRead$.subscribe((event) => this.handleMessagesRead(event)),
    );
    this.subscriptions.add(this.realtime.reconnected$.subscribe(() => this.rehydrate()));
  }

  private loadConversations(): void {
    this.conversationsRequest?.unsubscribe();
    this.conversationsLoading.set(true);
    this.conversationsRequest = this.api.getConversations().subscribe({
      next: (items) => {
        this.conversations.set(items);
        this.conversationsLoading.set(false);
      },
      error: () => {
        this.conversationsLoading.set(false);
        this.error.set('No se pudieron cargar las conversaciones. Intentá nuevamente.');
      },
    });
  }

  private loadMessages(conversationId: string): void {
    this.messagesRequest?.unsubscribe();
    this.messagesLoading.set(true);
    this.messagesRequest = this.api.getMessages(conversationId).subscribe({
      next: (messages) => {
        if (this.selectedConversationId() !== conversationId) return;
        this.messages.set(this.sortMessages(messages));
        this.messagesLoading.set(false);
      },
      error: () => {
        if (this.selectedConversationId() !== conversationId) return;
        this.messagesLoading.set(false);
        this.error.set('No se pudo cargar el historial de esta conversación.');
      },
    });
  }

  private markAsRead(conversationId: string): void {
    if (this.readInFlight.has(conversationId)) return;
    this.readInFlight.add(conversationId);
    const inboundVersion = this.inboundVersions.get(conversationId) ?? 0;
    this.api.markAsRead(conversationId).subscribe({
      next: () => {
        const receivedWhileReading =
          (this.inboundVersions.get(conversationId) ?? 0) !== inboundVersion;
        if (!receivedWhileReading) {
          this.setConversationUnread(conversationId, 0);
          if (this.selectedConversationId() === conversationId) {
            this.messages.update((messages) =>
              messages.map((message) =>
                message.direction === 'Inbound' ? { ...message, isRead: true } : message,
              ),
            );
          }
        }
      },
      error: () => {
        this.readInFlight.delete(conversationId);
        this.error.set('No se pudo marcar la conversación como leída.');
      },
      complete: () => {
        this.readInFlight.delete(conversationId);
        if (
          this.selectedConversationId() === conversationId &&
          (this.inboundVersions.get(conversationId) ?? 0) !== inboundVersion
        ) {
          this.markAsRead(conversationId);
        }
      },
    });
  }

  private handleMessageReceived(event: MessageRealtimeEvent): void {
    this.inboundVersions.set(
      event.conversationId,
      (this.inboundVersions.get(event.conversationId) ?? 0) + 1,
    );
    if (event.conversationId !== this.selectedConversationId()) return;
    this.addMessage({ ...event.message, conversationId: event.conversationId });
    if (!event.message.isRead) this.markAsRead(event.conversationId);
  }

  private handleMessageSent(event: MessageRealtimeEvent): void {
    if (event.conversationId === this.selectedConversationId()) {
      this.addMessage({ ...event.message, conversationId: event.conversationId });
    }
  }

  private handleMessagesRead(event: MessagesReadRealtimeEvent): void {
    this.setConversationUnread(event.conversationId, 0);
    if (event.conversationId !== this.selectedConversationId()) return;
    const readIds = new Set(event.messageIds);
    this.messages.update((messages) =>
      messages.map((message) => (readIds.has(message.id) ? { ...message, isRead: true } : message)),
    );
  }

  private addMessage(message: ConversationMessage): void {
    this.messages.update((messages) => {
      if (messages.some((item) => item.id === message.id)) return messages;
      return this.sortMessages([...messages, message]);
    });
  }

  private upsertConversation(conversation: ConversationSummary): void {
    this.conversations.update((items) =>
      [
        ...items.filter((item) => item.conversationId !== conversation.conversationId),
        conversation,
      ].sort(
        (left, right) =>
          new Date(right.lastMessageAt).getTime() - new Date(left.lastMessageAt).getTime(),
      ),
    );
  }

  private setConversationUnread(conversationId: string, unreadCount: number): void {
    this.conversations.update((items) =>
      items.map((item) =>
        item.conversationId === conversationId ? { ...item, unreadCount } : item,
      ),
    );
  }

  private rehydrate(): void {
    this.loadConversations();
    const conversationId = this.selectedConversationId();
    if (conversationId) this.loadMessages(conversationId);
  }

  private sortMessages(messages: ConversationMessage[]): ConversationMessage[] {
    return [...messages].sort(
      (left, right) => new Date(left.sentAt).getTime() - new Date(right.sentAt).getTime(),
    );
  }

  private messageForSendError(error: unknown): string {
    if (!(error instanceof HttpErrorResponse))
      return 'No se pudo enviar el mensaje. Revisá tu conexión.';
    switch (error.status) {
      case 400:
        return 'El mensaje no es válido. Revisá el texto e intentá nuevamente.';
      case 404:
        return 'La conversación ya no está disponible.';
      case 502:
        return 'No se pudo enviar el mensaje a Telegram.';
      case 503:
        return 'Telegram no está disponible temporalmente.';
      default:
        return 'No se pudo enviar el mensaje. Revisá tu conexión.';
    }
  }
}
