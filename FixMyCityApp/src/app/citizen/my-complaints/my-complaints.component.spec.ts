import { ComponentFixture, TestBed } from '@angular/core/testing';

import { MyComplaintsComponent } from './my-complaints.component';

describe('MyComplaintsComponent', () => {
  let component: MyComplaintsComponent;
  let fixture: ComponentFixture<MyComplaintsComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ MyComplaintsComponent ]
    })
    .compileComponents();

    fixture = TestBed.createComponent(MyComplaintsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
