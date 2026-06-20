import { ComponentFixture, TestBed } from '@angular/core/testing';

import { RegisterCitizenComponent } from './register-citizen.component';

describe('RegisterCitizenComponent', () => {
  let component: RegisterCitizenComponent;
  let fixture: ComponentFixture<RegisterCitizenComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ RegisterCitizenComponent ]
    })
    .compileComponents();

    fixture = TestBed.createComponent(RegisterCitizenComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
