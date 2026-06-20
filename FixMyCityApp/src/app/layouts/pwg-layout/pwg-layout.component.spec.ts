import { ComponentFixture, TestBed } from '@angular/core/testing';

import { PwgLayoutComponent } from './pwg-layout.component';

describe('PwgLayoutComponent', () => {
  let component: PwgLayoutComponent;
  let fixture: ComponentFixture<PwgLayoutComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ PwgLayoutComponent ]
    })
    .compileComponents();

    fixture = TestBed.createComponent(PwgLayoutComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
