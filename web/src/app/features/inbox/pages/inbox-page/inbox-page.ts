import {
  ChangeDetectionStrategy,
  Component,
  OnDestroy,
  OnInit,
  inject,
  signal,
} from '@angular/core';
import { ConversationList } from '../../components/conversation-list/conversation-list';
import { MessageComposer } from '../../components/message-composer/message-composer';
import { MessageThread } from '../../components/message-thread/message-thread';
import { InboxStore } from '../../services/inbox.store';

@Component({
  selector: 'app-inbox-page',
  imports: [ConversationList, MessageComposer, MessageThread],
  providers: [InboxStore],
  templateUrl: './inbox-page.html',
  styleUrl: './inbox-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class InboxPage implements OnInit, OnDestroy {
  readonly store = inject(InboxStore);
  readonly draft = signal('');
  ngOnInit(): void {
    this.store.initialize();
  }
  ngOnDestroy(): void {
    this.store.destroy();
  }
  async sendMessage(): Promise<void> {
    const sent = await this.store.sendMessage(this.draft());
    if (sent) this.draft.set('');
  }
}
