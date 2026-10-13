import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-message-composer',
  imports: [FormsModule],
  templateUrl: './message-composer.html',
  styleUrl: './message-composer.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MessageComposer {
  @Input() value = '';
  @Input() sending = false;
  @Input() disabled = false;
  @Output() readonly valueChange = new EventEmitter<string>();
  @Output() readonly sendRequested = new EventEmitter<void>();
  get canSend(): boolean {
    const length = this.value.trim().length;
    return !this.disabled && !this.sending && length > 0 && length <= 4096;
  }
  onKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      if (this.canSend) this.sendRequested.emit();
    }
  }
}
