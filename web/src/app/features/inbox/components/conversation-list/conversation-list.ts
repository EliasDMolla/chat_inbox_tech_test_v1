import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output } from '@angular/core';
import { ConversationSummary } from '../../../../core/models/inbox.models';

@Component({
  selector: 'app-conversation-list',
  imports: [DatePipe],
  templateUrl: './conversation-list.html',
  styleUrl: './conversation-list.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ConversationList {
  @Input({ required: true }) conversations: ConversationSummary[] = [];
  @Input() selectedConversationId: string | null = null;
  @Input() loading = false;
  @Output() readonly conversationSelected = new EventEmitter<string>();

  initials(displayName: string): string {
    return displayName
      .split(/\s+/)
      .filter(Boolean)
      .slice(0, 2)
      .map((part) => part[0]?.toUpperCase())
      .join('');
  }
}
