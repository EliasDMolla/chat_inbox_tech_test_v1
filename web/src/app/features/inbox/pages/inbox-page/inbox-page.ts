import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
  selector: 'app-inbox-page',
  templateUrl: './inbox-page.html',
  styleUrl: './inbox-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class InboxPage {}
