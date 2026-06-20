import { ComponentFixture, TestBed } from '@angular/core/testing';

import { SolverLayoutComponent } from './solver-layout.component';

describe('SolverLayoutComponent', () => {
  let component: SolverLayoutComponent;
  let fixture: ComponentFixture<SolverLayoutComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ SolverLayoutComponent ]
    })
    .compileComponents();

    fixture = TestBed.createComponent(SolverLayoutComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
