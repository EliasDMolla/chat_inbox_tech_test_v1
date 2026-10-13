import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ConversationMessage } from '../../../../core/models/inbox.models';
import { MessageThread } from './message-thread';

describe('MessageThread', () => {
  it('renders inbound and outbound messages with distinct alignment', async () => {
    await TestBed.configureTestingModule({ imports: [MessageThread] }).compileComponents();
    const fixture: ComponentFixture<MessageThread> = TestBed.createComponent(MessageThread);
    const messages: ConversationMessage[] = [
      {
        id: 'in',
        conversationId: 'c1',
        direction: 'Inbound',
        text: 'Entrada',
        isRead: false,
        sentAt: '2026-10-12T20:00:00Z',
      },
      {
        id: 'out',
        conversationId: 'c1',
        direction: 'Outbound',
        text: 'Salida',
        isRead: true,
        sentAt: '2026-10-12T20:01:00Z',
      },
    ];
    fixture.componentRef.setInput('messages', messages);
    fixture.detectChanges();
    const rendered = fixture.nativeElement.querySelectorAll('.message');
    expect(rendered[0].classList.contains('message--outbound')).toBe(false);
    expect(rendered[1].classList.contains('message--outbound')).toBe(true);
  });
});
