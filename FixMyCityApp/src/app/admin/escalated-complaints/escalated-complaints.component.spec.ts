import { ComponentFixture, TestBed } from '@angular/core/testing';

import { EscalatedComplaintsComponent } from './escalated-complaints.component';

describe('EscalatedComplaintsComponent', () => {
  let component: EscalatedComplaintsComponent;
  let fixture: ComponentFixture<EscalatedComplaintsComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ EscalatedComplaintsComponent ]
    })
    .compileComponents();

    fixture = TestBed.createComponent(EscalatedComplaintsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
