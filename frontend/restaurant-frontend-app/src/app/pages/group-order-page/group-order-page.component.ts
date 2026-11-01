import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { Subject, forkJoin, interval, merge, of } from 'rxjs';
import { catchError, exhaustMap, filter, switchMap } from 'rxjs/operators';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { GroupOrdersService } from '@app/services/group-orders/group-orders.service';
import { ToastService } from '@app/services/toast/toast.service';
import { GuestInvite } from '@app/models/reservations/guest-invite.model';
import { Participant } from '@app/models/group-orders/participant.model';
import { consolidatePreOrder } from '@app/shared/helpers/consolidate-pre-order';
import { ParticipantCardComponent } from '@app/components/group-orders/participant-card/participant-card.component';
import { ModalComponent } from '@app/shared/custom-components/modal/modal.component';

@Component({
  selector: 'app-group-order-page',
  imports: [ParticipantCardComponent, DatePipe, CurrencyPipe, ModalComponent],
  templateUrl: './group-order-page.component.html',
  styleUrl: './group-order-page.component.scss',
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class GroupOrderPageComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly groupOrdersService = inject(GroupOrdersService);
  private readonly toastService = inject(ToastService);

  readonly reservationId = signal<string>('');
  readonly invite = signal<GuestInvite | null>(null);
  readonly participants = signal<Participant[]>([]);
  readonly ownerSessionId = signal<string>('');
  readonly groupStatus = signal<'Open' | 'Submitted' | 'Closed'>('Open');

  readonly isLoading = signal<boolean>(true);
  readonly errorStatus = signal<number | null>(null);
  readonly isConfirmModalOpen = signal<boolean>(false);
  readonly isSubmitting = signal<boolean>(false);
  

  readonly updatedAt = signal<Date | null>(null);
  readonly syncError = signal<boolean>(false);
  private readonly refreshTrigger$ = new Subject<void>();

  readonly statusBadgeText = computed(() =>
    this.groupStatus() === 'Submitted' ? 'Sent to Restaurant' : 'Collecting pre-orders',
  );

  readonly isSubmittedStatus = computed(() => this.groupStatus() === 'Submitted');
  readonly consolidated = computed(() => consolidatePreOrder(this.participants()));
  readonly grandTotal = computed(() => this.consolidated().total);
  readonly hasAnyDishes = computed(() => this.participants().some((p) => p.items.length > 0));
  
  readonly unsubmittedCount = computed(() => this.participants().filter((p) => !p.isSubmitted).length);
  readonly unsubmittedWarning = computed(() => {
    const count = this.unsubmittedCount();
    if (count === 0) return '';
    
    if (count === 1) {
      return `1 participant hasn't submitted their cart yet — their dishes won't be included in the order`;
    }
    
    return `${count} participants haven't submitted their cart yet — their dishes won't be included in the order`;
  });

  readonly canSubmit = computed(() => !this.isSubmittedStatus() && !this.isSubmitting() && this.consolidated().items.length > 0);

  constructor() {
    merge(
      this.refreshTrigger$,
      interval(15000).pipe(filter(() => !this.isSubmittedStatus()))
    )
      .pipe(
        takeUntilDestroyed(),
        filter(() => !this.isLoading() && !this.isSubmittedStatus()),
        exhaustMap(() => {
          const groupId = this.invite()?.GroupReservationId;
          if (!groupId) return of(null);
          
          return forkJoin({
            sessions: this.groupOrdersService.getSessions$(groupId, { silent: true }),
            status: this.groupOrdersService.getStatus$(groupId, { silent: true }),
          }).pipe(
            catchError(() => {
              this.syncError.set(true);
              return of(null);
            })
          );
        })
      )
      .subscribe((result) => {
        if (result) {
          this.syncError.set(false);
          this.participants.set(result.sessions.participants);
          this.groupStatus.set(result.status.status);
          this.updatedAt.set(new Date());
        }
      });
  }

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('reservationId') ?? '';
    this.reservationId.set(id);
    this.loadData();
  }

  loadData(): void {
    const resId = this.reservationId();
    this.isLoading.set(true);
    this.errorStatus.set(null);
    this.syncError.set(false);

    this.groupOrdersService
      .getGroupInvite$(resId)
      .pipe(
        switchMap((invite) => {
          this.invite.set(invite);
          const groupId = invite.GroupReservationId;
          return forkJoin({
            sessions: this.groupOrdersService.getSessions$(groupId),
            ownerId: this.groupOrdersService.getOwnerSessionId$(resId),
            status: this.groupOrdersService.getStatus$(groupId),
          });
        })
      )
      .subscribe({
        next: ({ sessions, ownerId, status }) => {
          this.participants.set(sessions.participants);
          this.ownerSessionId.set(ownerId);
          this.groupStatus.set(status.status);
          this.updatedAt.set(new Date());
          this.isLoading.set(false);
        },
        error: (err: unknown) => {
          const status = err instanceof HttpErrorResponse ? err.status : 500;
          this.errorStatus.set(status || 500);
          this.isLoading.set(false);
        },
      });
  }

  forceRefresh(): void {
    if (this.isSubmittedStatus() || this.isLoading()) return;
    this.refreshTrigger$.next();
  }

  openConfirmModal(): void {
    if (!this.canSubmit()) return;
    this.isConfirmModalOpen.set(true);
  }

  closeConfirmModal(): void {
    if (this.isSubmitting()) return;
    this.isConfirmModalOpen.set(false);
  }

  confirmSubmit(): void {
    if (this.isSubmitting()) return;
    const groupId = this.invite()?.GroupReservationId;
    const resId = this.reservationId();
    if (!groupId || !resId) return;

    this.isSubmitting.set(true);
    this.groupOrdersService.submit$(groupId, resId).subscribe({
      next: () => {
        this.isSubmitting.set(false);
        this.isConfirmModalOpen.set(false);
        this.groupStatus.set('Submitted');
        this.toastService.success('Pre-order sent to the restaurant');
      },
      error: () => {
        this.isSubmitting.set(false);
        this.isConfirmModalOpen.set(false);
      },
    });
  }
}