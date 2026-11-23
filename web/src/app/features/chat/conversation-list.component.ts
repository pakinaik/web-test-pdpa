import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Conversation } from '../../core/api.models';

@Component({
  selector: 'app-conversation-list',
  imports: [RouterLink],
  template: `
    <div class="card">
      <a routerLink="/chat"><button type="button">+ สนทนาใหม่</button></a>
      <ul class="conv-list">
        @for (c of conversations(); track c.id) {
          <li>
            <a [routerLink]="['/chat', c.id]" [class.active]="c.id === activeId()">{{ c.title || '(ใหม่)' }}</a>
          </li>
        }
      </ul>
    </div>
  `,
})
export class ConversationListComponent {
  conversations = input.required<Conversation[]>();
  activeId = input<string | null>(null);
}
