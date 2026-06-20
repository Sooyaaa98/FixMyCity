// src/app/layouts/pwg-layout/pwg-layout.component.ts

import { Component, OnInit } from '@angular/core';
import { SessionService } from '../../core/services/session.service';

@Component({
  selector: 'app-pwg-layout',
  templateUrl: './pwg-layout.component.html',
  styleUrls: ['./pwg-layout.component.css']
})
export class PwgLayoutComponent implements OnInit {
  userRole = '';

  constructor(private session: SessionService) { }

  ngOnInit(): void {
    this.userRole = this.session.getRole();
  }
}
