import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpContext } from '@angular/common/http';
import { Observable, map, switchMap } from 'rxjs';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import { GuestInvite } from '@app/models/reservations/guest-invite.model';
import { GroupSessionsResponse } from '@app/models/group-orders/group-sessions-response.model';
import { SKIP_ERROR_TOAST } from '@app/auth/skip-error-toast.context';

export interface GroupStatusResponse {
  currentParticipants: number;
  maxCapacity: number;
  status: 'Open' | 'Submitted' | 'Closed';
}

@Injectable({
  providedIn: 'root',
})
export class GroupOrdersService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  getGroupInvite$(reservationId: string): Observable<GuestInvite> {
    return this.http
      .get<{ token: string }>(`${this.baseUrl}/reservations/${reservationId}/invite-token`)
      .pipe(
        switchMap(({ token }) =>
          this.http.get<GuestInvite>(`${this.baseUrl}/guest/invites/${token}`),
        ),
      );
  }

  getSessions$(
    groupReservationId: string,
    options?: { silent?: boolean },
  ): Observable<GroupSessionsResponse> {
    const context = options?.silent
      ? new HttpContext().set(SKIP_ERROR_TOAST, true)
      : undefined;
    return this.http.get<GroupSessionsResponse>(
      `${this.baseUrl}/guest/groups/${groupReservationId}/sessions`,
      context ? { context } : {},
    );
  }

  getOwnerSessionId$(reservationId: string): Observable<string> {
    return this.http
      .get<{ guestSessionId: string }>(
        `${this.baseUrl}/reservations/${reservationId}/guest-session-id`,
      )
      .pipe(map((res) => res.guestSessionId));
  }

  getStatus$(
    groupReservationId: string,
    options?: { silent?: boolean },
  ): Observable<GroupStatusResponse> {
    const context = options?.silent
      ? new HttpContext().set(SKIP_ERROR_TOAST, true)
      : undefined;
    return this.http.get<GroupStatusResponse>(
      `${this.baseUrl}/guest/groups/${groupReservationId}/status`,
      context ? { context } : {},
    );
  }

  submit$(
    groupReservationId: string,
    reservationId: string,
  ): Observable<{ dishId: string; quantity: number }[]> {
    return this.http.post<{ dishId: string; quantity: number }[]>(
      `${this.baseUrl}/group-reservations/${groupReservationId}/submit`,
      { reservationId },
    );
  }
}