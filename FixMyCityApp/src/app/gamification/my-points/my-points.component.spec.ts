import { ComponentFixture, TestBed } from '@angular/core/testing';

import { MyPointsComponent } from './my-points.component';

describe('MyPointsComponent', () => {
  let component: MyPointsComponent;
  let fixture: ComponentFixture<MyPointsComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ MyPointsComponent ]
    })
    .compileComponents();

    fixture = TestBed.createComponent(MyPointsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
