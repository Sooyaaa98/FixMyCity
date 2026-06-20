// src/app/layouts/solver-layout/solver-layout.component.ts

import { Component, OnInit } from '@angular/core';
import { SessionService } from '../../core/services/session.service';

@Component({
  selector: 'app-solver-layout',
  templateUrl: './solver-layout.component.html',
  styleUrls: ['./solver-layout.component.css']
})
export class SolverLayoutComponent implements OnInit {
  userRole = '';

  constructor(private session: SessionService) { }

  ngOnInit(): void {
    this.userRole = this.session.getRole();
  }
}
