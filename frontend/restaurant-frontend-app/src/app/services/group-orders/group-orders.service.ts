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

function asRecord(value: unknown, label: string): Record<string, unknown> {
  if (typeof value !== 'object' || value === null || Array.isArray(value)) {
    throw new Error(`Invalid ${label} response.`);
  }
  return value as Record<string, unknown>;
}

function field(record: Record<string, unknown>, camelCase: string, pascalCase: string): unknown {
  return record[camelCase] ?? record[pascalCase];
}

function requiredString(value: unknown, label: string): string {
  if (typeof value !== 'string') {
    throw new Error(`Invalid ${label} in group order response.`);
  }
  return value;
}

function requiredNumber(value: unknown, label: string): number {
  if (typeof value !== 'number' || !Number.isFinite(value)) {
    throw new Error(`Invalid ${label} in group order response.`);
  }
  return value;
}

function requiredBoolean(value: unknown, label: string): boolean {
  if (typeof value !== 'boolean') {
    throw new Error(`Invalid ${label} in group order response.`);
  }
  return value;
}

function participantType(value: unknown): 'Owner' | 'Guest' {
  const type = requiredString(value, 'participant type');
  if (type !== 'Owner' && type !== 'Guest') {
    throw new Error('Invalid participant type in group order response.');
  }
  return type;
}

function normalizeSessionsResponse(value: unknown): GroupSessionsResponse {
  const response = asRecord(value, 'group sessions');
  const rawParticipants = field(response, 'participants', 'Participants');
  if (!Array.isArray(rawParticipants)) {
    throw new Error('Invalid participants in group sessions response.');
  }

  const participants = rawParticipants.map(
    (rawParticipant): GroupSessionsResponse['participants'][number] => {
      const participant = asRecord(rawParticipant, 'participant');
      const rawItems = field(participant, 'items', 'Items');
      if (!Array.isArray(rawItems)) {
        throw new Error('Invalid participant items in group sessions response.');
      }

      return {
        id: requiredString(field(participant, 'id', 'Id'), 'participant id'),
        type: participantType(field(participant, 'type', 'Type')),
        name: requiredString(field(participant, 'name', 'Name'), 'participant name'),
        isSubmitted: requiredBoolean(
          field(participant, 'isSubmitted', 'IsSubmitted'),
          'participant submission status'
        ),
        total: requiredNumber(field(participant, 'total', 'Total'), 'participant total'),
        items: rawItems.map(
          (rawItem): GroupSessionsResponse['participants'][number]['items'][number] => {
            const item = asRecord(rawItem, 'participant item');
            const dish = asRecord(field(item, 'dish', 'Dish'), 'dish');
            const imageUrl = field(dish, 'imageUrl', 'ImageUrl');
            const weight = field(dish, 'weight', 'Weight');
            return {
              dish: {
                id: requiredString(field(dish, 'id', 'Id'), 'dish id'),
                name: requiredString(field(dish, 'name', 'Name'), 'dish name'),
                price: requiredNumber(field(dish, 'price', 'Price'), 'dish price'),
                ...(typeof imageUrl === 'string' ? { imageUrl } : {}),
                ...(typeof weight === 'string' ? { weight } : {}),
              },
              quantity: requiredNumber(field(item, 'quantity', 'Quantity'), 'item quantity'),
            };
          }
        ),
      };
    }
  );

  return {
    participants,
    total: requiredNumber(field(response, 'total', 'Total'), 'group total'),
  };
}

function normalizeStatusResponse(value: unknown): GroupStatusResponse {
  const response = asRecord(value, 'group status');
  const status = requiredString(field(response, 'status', 'Status'), 'status');
  if (status !== 'Open' && status !== 'Submitted' && status !== 'Closed') {
    throw new Error('Invalid status in group order response.');
  }
  return {
    currentParticipants: requiredNumber(
      field(response, 'currentParticipants', 'CurrentParticipants'),
      'current participants'
    ),
    maxCapacity: requiredNumber(field(response, 'maxCapacity', 'MaxCapacity'), 'max capacity'),
    status,
  };
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
          this.http.get<GuestInvite>(`${this.baseUrl}/guest/invites/${token}`)
        )
      );
  }

  getSessions$(
    groupReservationId: string,
    options?: { silent?: boolean }
  ): Observable<GroupSessionsResponse> {
    const context = options?.silent ? new HttpContext().set(SKIP_ERROR_TOAST, true) : undefined;
    return this.http
      .get<unknown>(
        `${this.baseUrl}/guest/groups/${groupReservationId}/sessions`,
        context ? { context } : {}
      )
      .pipe(map(normalizeSessionsResponse));
  }

  getOwnerSessionId$(reservationId: string, options?: { silent?: boolean }): Observable<string> {
    const context = options?.silent ? new HttpContext().set(SKIP_ERROR_TOAST, true) : undefined;
    return this.http
      .get<unknown>(
        `${this.baseUrl}/reservations/${reservationId}/guest-session-id`,
        context ? { context } : {}
      )
      .pipe(
        map((value) => {
          const response = asRecord(value, 'owner session');
          return requiredString(
            field(response, 'guestSessionId', 'GuestSessionId'),
            'owner session id'
          );
        })
      );
  }

  getStatus$(
    groupReservationId: string,
    options?: { silent?: boolean }
  ): Observable<GroupStatusResponse> {
    const context = options?.silent ? new HttpContext().set(SKIP_ERROR_TOAST, true) : undefined;
    return this.http
      .get<unknown>(
        `${this.baseUrl}/guest/groups/${groupReservationId}/status`,
        context ? { context } : {}
      )
      .pipe(map(normalizeStatusResponse));
  }

  submit$(
    groupReservationId: string,
    reservationId: string
  ): Observable<{ dishId: string; quantity: number }[]> {
    return this.http.post<{ dishId: string; quantity: number }[]>(
      `${this.baseUrl}/group-reservations/${groupReservationId}/submit`,
      { reservationId }
    );
  }
}
