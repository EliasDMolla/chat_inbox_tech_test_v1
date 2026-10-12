import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./features/inbox/pages/inbox-page/inbox-page').then(
        (component) => component.InboxPage,
      ),
  },
  { path: '**', redirectTo: '' },
];
