import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ProgressUpdateComponent } from './progress-update.component';

describe('ProgressUpdateComponent', () => {
  let component: ProgressUpdateComponent;
  let fixture: ComponentFixture<ProgressUpdateComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ ProgressUpdateComponent ]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ProgressUpdateComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
