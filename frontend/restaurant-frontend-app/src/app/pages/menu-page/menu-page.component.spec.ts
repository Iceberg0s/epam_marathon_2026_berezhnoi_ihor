import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MenuPageComponent } from './menu-page.component';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import { ActivatedRoute, Params, Router } from '@angular/router';
import { ToastService } from '@app/services/toast/toast.service';
import { BehaviorSubject, of } from 'rxjs';
import { DishesService } from '@app/services/dishes/dishes.service';
import { PreOrdersService } from '@app/services/pre-orders/pre-orders.service';
import { DishListResponse } from '@app/models/dishes/dish-list-response.model';

describe('MenuPageComponent', () => {
  let component: MenuPageComponent;
  let fixture: ComponentFixture<MenuPageComponent>;
  let routeParams: BehaviorSubject<Params>;
  let dishesService: jasmine.SpyObj<DishesService>;

  beforeEach(async () => {
    routeParams = new BehaviorSubject<Params>({});
    dishesService = jasmine.createSpyObj<DishesService>('DishesService', [
      'getDishes',
      'getDishesForReservation',
    ]);
    dishesService.getDishes.and.returnValue(of([]));
    dishesService.getDishesForReservation.and.returnValue(of([]));

    await TestBed.configureTestingModule({
      providers: [
        { provide: API_BASE_URL, useValue: 'https://test.api' },
        { provide: ToastService, useValue: { error: jasmine.createSpy('error') } },
        { provide: DishesService, useValue: dishesService },
        { provide: PreOrdersService, useValue: {} },
        { provide: ActivatedRoute, useValue: { queryParams: routeParams.asObservable() } },
        { provide: Router, useValue: { navigate: jasmine.createSpy('navigate') } },
      ],
      imports: [MenuPageComponent],
    })
      .overrideComponent(MenuPageComponent, { set: { providers: [] } })
      .compileComponents();

    fixture = TestBed.createComponent(MenuPageComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('limits pre-order menu dishes to those available at the reservation location', () => {
    const locationDish = createDish('location-dish');
    const otherLocationDish = createDish('other-location-dish');
    dishesService.getDishesForReservation.and.returnValue(of([locationDish]));
    dishesService.getDishes.and.returnValue(of([otherLocationDish, locationDish]));

    routeParams.next({ preOrder: 'true', reservationId: 'reservation-1' });

    expect(dishesService.getDishesForReservation).toHaveBeenCalledWith('reservation-1');
    expect(component.dishes.map((dish) => dish.id)).toEqual(['location-dish']);
  });

  function createDish(id: string): DishListResponse {
    return {
      id,
      name: id,
      imageUrl: '',
      price: 10,
      state: 'Available',
      weight: '100g',
      dishType: 'Main course',
      description: '',
      calories: '100',
      proteins: '10',
      carbohydrates: '10',
      fats: '10',
      vitamins: '10',
    };
  }
});
