import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import { DishesService } from './dishes.service';

describe('DishesService', () => {
  let service: DishesService;
  let httpTestingController: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        { provide: API_BASE_URL, useValue: 'https://test.api' },
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });

    service = TestBed.inject(DishesService);
    httpTestingController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpTestingController.verify());

  it('requests the menu scoped to the reservation', () => {
    service.getDishesForReservation('reservation/1').subscribe();

    const request = httpTestingController.expectOne(
      'https://test.api/reservations/reservation%2F1/dishes'
    );
    expect(request.request.method).toBe('GET');
    request.flush([]);
  });
});
