// src/app/layouts/admin-layout/admin-layout.component.ts

import { Component, OnInit } from '@angular/core';
import { SessionService } from '../../core/services/session.service';

@Component({
  selector: 'app-admin-layout',
  templateUrl: './admin-layout.component.html',
  styleUrls: ['./admin-layout.component.css']
})
export class AdminLayoutComponent implements OnInit {
  userRole = '';

  constructor(private session: SessionService) { }

  ngOnInit(): void {
    this.userRole = this.session.getRole();
  }
}
