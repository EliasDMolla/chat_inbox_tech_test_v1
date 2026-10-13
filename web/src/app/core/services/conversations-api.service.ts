import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  ConversationMessage,
  ConversationSummary,
  SendMessageRequest,
} from '../models/inbox.models';

@Injectable({ providedIn: 'root' })
export class ConversationsApiService {
  private readonly http = inject(HttpClient);
  private readonly conversationsUrl = `${environment.apiUrl}/api/conversations`;

  getConversations(): Observable<ConversationSummary[]> {
    return this.http.get<ConversationSummary[]>(this.conversationsUrl);
  }

  getMessages(conversationId: string): Observable<ConversationMessage[]> {
    return this.http.get<ConversationMessage[]>(
      `${this.conversationsUrl}/${conversationId}/messages`,
    );
  }

  markAsRead(conversationId: string): Observable<void> {
    return this.http.post<void>(`${this.conversationsUrl}/${conversationId}/read`, null);
  }

  sendMessage(
    conversationId: string,
    request: SendMessageRequest,
  ): Observable<ConversationMessage> {
    return this.http.post<ConversationMessage>(
      `${this.conversationsUrl}/${conversationId}/messages`,
      request,
    );
  }
}
