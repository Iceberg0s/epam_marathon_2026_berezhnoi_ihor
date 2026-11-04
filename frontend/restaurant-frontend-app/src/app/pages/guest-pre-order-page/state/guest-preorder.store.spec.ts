import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AuthenticationService } from '@app/auth/services/authentication.service';
import { GuestInvite } from '@app/models/reservations/guest-invite.model';
import { GroupOrdersService } from '@app/services/group-orders/group-orders.service';
import { GuestInviteService } from '@app/services/reservation/guest-invite.service';
import { GuestSessionsService } from '@app/services/guest-sessions/guest-sessions.service';
import { GuestPreorderStore } from './guest-preorder.store';

describe('GuestPreorderStore', () => {
  let store: GuestPreorderStore;
  let authenticationService: jasmine.SpyObj<AuthenticationService>;
  let groupOrdersService: jasmine.SpyObj<GroupOrdersService>;
  let guestInviteService: jasmine.SpyObj<GuestInviteService>;
  let router: jasmine.SpyObj<Router>;
  const invite: GuestInvite = {
    GroupReservationId: 'group-1',
    ReservationId: 'reservation-1',
    LocationId: 'location-1',
    RestaurantName: 'Green & Tasty',
    LocationAddress: '48 Rustaveli Avenue',
    StartTime: '2026-11-03T12:00:00Z',
    EndTime: '2026-11-03T14:00:00Z',
    MaxGuestCount: 3,
    TableNumber: 4,
    InviterName: 'Owner',
  };

  beforeEach(() => {
    authenticationService = jasmine.createSpyObj<AuthenticationService>('AuthenticationService', [
      'getTokens',
    ]);
    groupOrdersService = jasmine.createSpyObj<GroupOrdersService>('GroupOrdersService', [
      'getOwnerSessionId$',
    ]);
    guestInviteService = jasmine.createSpyObj<GuestInviteService>('GuestInviteService', [
      'getInvite$',
    ]);
    router = jasmine.createSpyObj<Router>('Router', ['navigate']);
    router.navigate.and.resolveTo(true);

    TestBed.configureTestingModule({
      providers: [
        GuestPreorderStore,
        { provide: AuthenticationService, useValue: authenticationService },
        { provide: GroupOrdersService, useValue: groupOrdersService },
        { provide: GuestInviteService, useValue: guestInviteService },
        {
          provide: GuestSessionsService,
          useValue: jasmine.createSpyObj('GuestSessionsService', [
            'createSession',
            'getMenu',
            'addDish',
            'removeDish',
            'submit',
          ]),
        },
        { provide: Router, useValue: router },
      ],
    });
    store = TestBed.inject(GuestPreorderStore);
  });

  it('redirects an authenticated reservation owner to the group-order page', async () => {
    authenticationService.getTokens.and.returnValue({
      idToken: 'id-token',
      accessToken: 'access-token',
    });
    guestInviteService.getInvite$.and.returnValue(of(invite));
    groupOrdersService.getOwnerSessionId$.and.returnValue(of('owner-session'));

    await store.loadInviteByToken('invite-token');

    expect(store.invite()).toBeNull();
    expect(groupOrdersService.getOwnerSessionId$).toHaveBeenCalledOnceWith(
      'reservation-1',
      { silent: true },
    );
    expect(router.navigate).toHaveBeenCalledOnceWith([
      '/reservations',
      'reservation-1',
      'group-order',
    ]);
  });

  it('keeps authenticated non-owners on the guest invite when owner verification is forbidden', async () => {
    authenticationService.getTokens.and.returnValue({
      idToken: 'id-token',
      accessToken: 'access-token',
    });
    guestInviteService.getInvite$.and.returnValue(of(invite));
    groupOrdersService.getOwnerSessionId$.and.returnValue(
      throwError(() => new HttpErrorResponse({ status: 403 })),
    );

    await store.loadInviteByToken('invite-token');

    expect(store.invite()).toEqual(invite);
    expect(store.inviteError()).toBeNull();
    expect(router.navigate).not.toHaveBeenCalled();
  });
});
