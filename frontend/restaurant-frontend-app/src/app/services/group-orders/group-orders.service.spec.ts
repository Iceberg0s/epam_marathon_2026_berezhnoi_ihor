// Рівень 2 · сервіс GroupOrdersService: URL, метод, тіло кожного запиту.
// Запуск: npm run test:hw
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import { GroupSessionsResponse } from '@app/models/group-orders/group-sessions-response.model';
import { GroupOrdersService } from './group-orders.service';

const BASE_URL = 'https://api.test';

describe('Рівень 2 · GroupOrdersService', () => {
  let service: GroupOrdersService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: BASE_URL },
      ],
    });
    service = TestBed.inject(GroupOrdersService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('getGroupInvite$: reservationId → invite-token → invite', () => {
    let groupId = '';
    service.getGroupInvite$('res-1').subscribe((invite) => (groupId = invite.GroupReservationId));

    http.expectOne(`${BASE_URL}/reservations/res-1/invite-token`).flush({ token: 'tok' });
    http.expectOne(`${BASE_URL}/guest/invites/tok`).flush({ GroupReservationId: 'grp-1' });

    expect(groupId).toBe('grp-1');
  });

  it('getSessions$: GET /guest/groups/{id}/sessions', () => {
    service.getSessions$('grp-1').subscribe();
    const req = http.expectOne(`${BASE_URL}/guest/groups/grp-1/sessions`);
    expect(req.request.method).toBe('GET');
    req.flush({ participants: [], total: 0 });
  });

  it('normalizes PascalCase group-session responses from older deployments', () => {
    let response: GroupSessionsResponse | undefined;
    service.getSessions$('grp-1').subscribe((value) => (response = value));

    http.expectOne(`${BASE_URL}/guest/groups/grp-1/sessions`).flush({
      Participants: [
        {
          Id: 'owner-1',
          Type: 'Owner',
          Name: 'Owner',
          IsSubmitted: true,
          Total: 12,
          Items: [{ Dish: { Id: 'dish-1', Name: 'Salad', Price: 12 }, Quantity: 1 }],
        },
      ],
      Total: 12,
    });

    expect(response!).toEqual({
      participants: [
        {
          id: 'owner-1',
          type: 'Owner',
          name: 'Owner',
          isSubmitted: true,
          total: 12,
          items: [{ dish: { id: 'dish-1', name: 'Salad', price: 12 }, quantity: 1 }],
        },
      ],
      total: 12,
    });
  });

  it('getOwnerSessionId$: повертає рядок guestSessionId', () => {
    let id = '';
    service.getOwnerSessionId$('res-1').subscribe((value) => (id = value));
    const req = http.expectOne(`${BASE_URL}/reservations/res-1/guest-session-id`);
    expect(req.request.method).toBe('GET');
    req.flush({ guestSessionId: 'owner-1' });
    expect(id).toBe('owner-1');
  });

  it('getStatus$: GET /guest/groups/{id}/status', () => {
    service.getStatus$('grp-1').subscribe();
    const req = http.expectOne(`${BASE_URL}/guest/groups/grp-1/status`);
    expect(req.request.method).toBe('GET');
    req.flush({ currentParticipants: 3, maxCapacity: 5, status: 'Open' });
  });

  it('normalizes PascalCase group status responses from older deployments', () => {
    let response: unknown;
    service.getStatus$('grp-1').subscribe((value) => (response = value));
    http.expectOne(`${BASE_URL}/guest/groups/grp-1/status`).flush({
      CurrentParticipants: 2,
      MaxCapacity: 3,
      Status: 'Open',
    });

    expect(response).toEqual({ currentParticipants: 2, maxCapacity: 3, status: 'Open' });
  });

  it('submit$: POST /group-reservations/{id}/submit з тілом { reservationId }', () => {
    service.submit$('grp-1', 'res-1').subscribe();
    const req = http.expectOne(`${BASE_URL}/group-reservations/grp-1/submit`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ reservationId: 'res-1' });
    req.flush([]);
  });
});
