import { DatePipe } from '@angular/common';
import {
  AfterViewChecked,
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  Input,
  OnChanges,
  SimpleChanges,
  ViewChild,
} from '@angular/core';
import { ConversationMessage } from '../../../../core/models/inbox.models';

@Component({
  selector: 'app-message-thread',
  imports: [DatePipe],
  templateUrl: './message-thread.html',
  styleUrl: './message-thread.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MessageThread implements OnChanges, AfterViewChecked {
  @Input({ required: true }) messages: ConversationMessage[] = [];
  @Input() conversationId: string | null = null;
  @Input() loading = false;
  @ViewChild('scrollContainer') private scrollContainer?: ElementRef<HTMLElement>;
  private scrollPending = false;
  ngOnChanges(changes: SimpleChanges): void {
    if (changes['conversationId'] || (changes['messages'] && this.isNearBottom()))
      this.scrollPending = true;
  }
  ngAfterViewChecked(): void {
    if (!this.scrollPending || !this.scrollContainer) return;
    const element = this.scrollContainer.nativeElement;
    element.scrollTop = element.scrollHeight;
    this.scrollPending = false;
  }
  private isNearBottom(): boolean {
    const element = this.scrollContainer?.nativeElement;
    return !element || element.scrollHeight - element.scrollTop - element.clientHeight < 120;
  }
}
