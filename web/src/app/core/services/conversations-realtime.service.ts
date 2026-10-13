import { Injectable } from '@angular/core';
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr';
import { BehaviorSubject, Subject } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  ConversationSummary,
  MessageRealtimeEvent,
  MessagesReadRealtimeEvent,
  RealtimeConnectionState,
} from '../models/inbox.models';

@Injectable({ providedIn: 'root' })
export class ConversationsRealtimeService {
  private readonly connection: HubConnection;
  private readonly connectionStateSubject = new BehaviorSubject<RealtimeConnectionState>(
    'disconnected',
  );
  private readonly messageReceivedSubject = new Subject<MessageRealtimeEvent>();
  private readonly messageSentSubject = new Subject<MessageRealtimeEvent>();
  private readonly conversationUpdatedSubject = new Subject<ConversationSummary>();
  private readonly messagesReadSubject = new Subject<MessagesReadRealtimeEvent>();
  private readonly reconnectedSubject = new Subject<void>();
  private startPromise: Promise<void> | null = null;
  private retryTimer: ReturnType<typeof setTimeout> | null = null;
  private stopped = false;

  readonly connectionState$ = this.connectionStateSubject.asObservable();
  readonly messageReceived$ = this.messageReceivedSubject.asObservable();
  readonly messageSent$ = this.messageSentSubject.asObservable();
  readonly conversationUpdated$ = this.conversationUpdatedSubject.asObservable();
  readonly messagesRead$ = this.messagesReadSubject.asObservable();
  readonly reconnected$ = this.reconnectedSubject.asObservable();

  constructor() {
    this.connection = new HubConnectionBuilder()
      .withUrl(`${environment.apiUrl}/hubs/conversations`)
      .withAutomaticReconnect([0, 2_000, 5_000, 10_000])
      .configureLogging(LogLevel.Warning)
      .build();

    this.connection.on('MessageReceived', (event: MessageRealtimeEvent) =>
      this.messageReceivedSubject.next(event),
    );
    this.connection.on('MessageSent', (event: MessageRealtimeEvent) =>
      this.messageSentSubject.next(event),
    );
    this.connection.on('ConversationUpdated', (event: ConversationSummary) =>
      this.conversationUpdatedSubject.next(event),
    );
    this.connection.on('MessagesRead', (event: MessagesReadRealtimeEvent) =>
      this.messagesReadSubject.next(event),
    );
    this.connection.onreconnecting(() => this.connectionStateSubject.next('reconnecting'));
    this.connection.onreconnected(() => {
      this.connectionStateSubject.next('connected');
      this.reconnectedSubject.next();
    });
    this.connection.onclose(() => {
      this.connectionStateSubject.next('disconnected');
      this.scheduleInitialRetry();
    });
  }

  start(): Promise<void> {
    this.stopped = false;
    if (this.connection.state !== HubConnectionState.Disconnected) return Promise.resolve();
    if (this.startPromise) return this.startPromise;
    this.connectionStateSubject.next('connecting');
    this.startPromise = this.connection
      .start()
      .then(() => this.connectionStateSubject.next('connected'))
      .catch(() => {
        this.connectionStateSubject.next('disconnected');
        this.scheduleInitialRetry();
      })
      .finally(() => {
        this.startPromise = null;
      });
    return this.startPromise;
  }

  async stop(): Promise<void> {
    this.stopped = true;
    if (this.retryTimer) {
      clearTimeout(this.retryTimer);
      this.retryTimer = null;
    }
    if (this.connection.state !== HubConnectionState.Disconnected) await this.connection.stop();
  }

  private scheduleInitialRetry(): void {
    if (this.stopped || this.retryTimer) return;
    this.retryTimer = setTimeout(() => {
      this.retryTimer = null;
      void this.start();
    }, 5_000);
  }
}
