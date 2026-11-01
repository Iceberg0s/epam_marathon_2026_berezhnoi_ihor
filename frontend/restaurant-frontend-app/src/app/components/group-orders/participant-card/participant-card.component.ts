import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { Participant } from '@app/models/group-orders/participant.model';
import { CurrencyPipe } from '@angular/common';

@Component({
  selector: 'app-participant-card',
  imports: [CurrencyPipe],
  templateUrl: './participant-card.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class ParticipantCardComponent {
  // що картка отримує ззовні — як параметр функції
  readonly participant = input.required<Participant>();
  readonly isMe = input<boolean>(false);


  // рахується саме, коли змінюється participant
  readonly title = computed(() => {
    if (this.isMe()) {
      return 'My Pre-Order';
    }
    const person = this.participant();
    return person.type === 'Owner' ? `Owner: ${person.name}` : `Guest: ${person.name}`;
  });
}