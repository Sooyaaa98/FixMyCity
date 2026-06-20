import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ComplaintCardComponent } from './complaint-card.component';

describe('ComplaintCardComponent', () => {
  let component: ComplaintCardComponent;
  let fixture: ComponentFixture<ComplaintCardComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ ComplaintCardComponent ]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ComplaintCardComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
