import { ComponentFixture, TestBed } from '@angular/core/testing';

import { RegisterOrganisationComponent } from './register-organisation.component';

describe('RegisterOrganisationComponent', () => {
  let component: RegisterOrganisationComponent;
  let fixture: ComponentFixture<RegisterOrganisationComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ RegisterOrganisationComponent ]
    })
    .compileComponents();

    fixture = TestBed.createComponent(RegisterOrganisationComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
