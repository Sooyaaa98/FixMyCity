// src/app/layouts/citizen-layout/citizen-layout.component.ts

import { Component, OnInit } from '@angular/core';
import { SessionService } from '../../core/services/session.service';

@Component({
  selector: 'app-citizen-layout',
  templateUrl: './citizen-layout.component.html',
  styleUrls: ['./citizen-layout.component.css']
})
export class CitizenLayoutComponent implements OnInit {
  userRole = '';

  constructor(private session: SessionService) { }

  ngOnInit(): void {
    this.userRole = this.session.getRole();
  }
}
