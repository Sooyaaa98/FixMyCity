import { ComponentFixture, TestBed } from '@angular/core/testing';

import { SubmitComplaintComponent } from './submit-complaint.component';

describe('SubmitComplaintComponent', () => {
  let component: SubmitComplaintComponent;
  let fixture: ComponentFixture<SubmitComplaintComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ SubmitComplaintComponent ]
    })
    .compileComponents();

    fixture = TestBed.createComponent(SubmitComplaintComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
