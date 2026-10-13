import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ConversationSummary } from '../../../../core/models/inbox.models';
import { ConversationList } from './conversation-list';

const conversations: ConversationSummary[] = [
  {
    conversationId: 'conversation-1',
    contactId: 'contact-1',
    displayName: 'Ada Lovelace',
    username: 'ada',
    lastMessageText: 'Hola desde Telegram',
    lastMessageAt: '2026-10-12T20:00:00Z',
    unreadCount: 3,
  },
];

describe('ConversationList', () => {
  let fixture: ComponentFixture<ConversationList>;
  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [ConversationList] }).compileComponents();
    fixture = TestBed.createComponent(ConversationList);
    fixture.componentRef.setInput('conversations', conversations);
    fixture.detectChanges();
  });

  it('renders conversations in the sidebar', () => {
    expect(fixture.nativeElement.textContent).toContain('Ada Lovelace');
    expect(fixture.nativeElement.textContent).toContain('Hola desde Telegram');
  });

  it('shows the unread badge', () => {
    const badge: HTMLElement = fixture.nativeElement.querySelector('.unread-badge');
    expect(badge.textContent?.trim()).toBe('3');
  });
});
