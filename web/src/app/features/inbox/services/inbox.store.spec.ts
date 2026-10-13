import { TestBed } from '@angular/core/testing';
import { BehaviorSubject, of, Subject } from 'rxjs';
import {
  ConversationMessage,
  ConversationSummary,
  MessageRealtimeEvent,
  MessagesReadRealtimeEvent,
  RealtimeConnectionState,
} from '../../../core/models/inbox.models';
import { ConversationsApiService } from '../../../core/services/conversations-api.service';
import { ConversationsRealtimeService } from '../../../core/services/conversations-realtime.service';
import { InboxStore } from './inbox.store';

const firstConversation: ConversationSummary = {
  conversationId: 'c1',
  contactId: 'contact-1',
  displayName: 'Ada',
  username: 'ada',
  lastMessageText: 'Primero',
  lastMessageAt: '2026-10-12T20:00:00Z',
  unreadCount: 0,
};
const message: ConversationMessage = {
  id: 'm1',
  conversationId: 'c1',
  direction: 'Outbound',
  text: 'Respuesta',
  isRead: true,
  sentAt: '2026-10-12T20:01:00Z',
};

class RealtimeStub {
  readonly connectionState$ = new BehaviorSubject<RealtimeConnectionState>('connected');
  readonly messageReceived$ = new Subject<MessageRealtimeEvent>();
  readonly messageSent$ = new Subject<MessageRealtimeEvent>();
  readonly conversationUpdated$ = new Subject<ConversationSummary>();
  readonly messagesRead$ = new Subject<MessagesReadRealtimeEvent>();
  readonly reconnected$ = new Subject<void>();
  start = vi.fn(() => Promise.resolve());
  stop = vi.fn(() => Promise.resolve());
}

describe('InboxStore', () => {
  let store: InboxStore;
  let realtime: RealtimeStub;
  let api: {
    getConversations: ReturnType<typeof vi.fn>;
    getMessages: ReturnType<typeof vi.fn>;
    markAsRead: ReturnType<typeof vi.fn>;
    sendMessage: ReturnType<typeof vi.fn>;
  };

  beforeEach(() => {
    realtime = new RealtimeStub();
    api = {
      getConversations: vi.fn(() => of([firstConversation])),
      getMessages: vi.fn(() => of([message])),
      markAsRead: vi.fn(() => of(undefined)),
      sendMessage: vi.fn(() => of(message)),
    };
    TestBed.configureTestingModule({
      providers: [
        InboxStore,
        { provide: ConversationsApiService, useValue: api },
        { provide: ConversationsRealtimeService, useValue: realtime },
      ],
    });
    store = TestBed.inject(InboxStore);
    store.initialize();
  });

  afterEach(() => store.destroy());

  it('loads the selected conversation history', () => {
    store.selectConversation('c1');
    expect(api.getMessages).toHaveBeenCalledWith('c1');
    expect(store.messages()).toEqual([message]);
  });

  it('adds MessageReceived only once', () => {
    store.selectConversation('c1');
    const inbound: MessageRealtimeEvent = {
      conversationId: 'c1',
      message: {
        id: 'm2',
        direction: 'Inbound',
        text: 'Nuevo',
        isRead: false,
        sentAt: '2026-10-12T20:02:00Z',
      },
    };
    realtime.messageReceived$.next(inbound);
    realtime.messageReceived$.next(inbound);
    expect(store.messages().filter((item) => item.id === 'm2')).toHaveLength(1);
  });

  it('does not duplicate MessageSent already added from HTTP', async () => {
    store.selectConversation('c1');
    await store.sendMessage('Respuesta');
    realtime.messageSent$.next({ conversationId: 'c1', message });
    expect(store.messages().filter((item) => item.id === 'm1')).toHaveLength(1);
  });

  it('updates and reorders a conversation from ConversationUpdated', () => {
    const newer: ConversationSummary = {
      ...firstConversation,
      conversationId: 'c2',
      contactId: 'contact-2',
      displayName: 'Grace',
      lastMessageAt: '2026-10-12T21:00:00Z',
      unreadCount: 2,
    };
    realtime.conversationUpdated$.next(newer);
    expect(store.conversations()[0]).toEqual(newer);
    expect(store.conversations()[0].unreadCount).toBe(2);
  });

  it('rehydrates conversations and selected history after reconnect', () => {
    store.selectConversation('c1');
    realtime.reconnected$.next();
    expect(api.getConversations).toHaveBeenCalledTimes(2);
    expect(api.getMessages).toHaveBeenCalledTimes(2);
  });
});
