import { ComponentFixture, TestBed } from '@angular/core/testing';

import { PwgRequestsComponent } from './pwg-requests.component';

describe('PwgRequestsComponent', () => {
  let component: PwgRequestsComponent;
  let fixture: ComponentFixture<PwgRequestsComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ PwgRequestsComponent ]
    })
    .compileComponents();

    fixture = TestBed.createComponent(PwgRequestsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
