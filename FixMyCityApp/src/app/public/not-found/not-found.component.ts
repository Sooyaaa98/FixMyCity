// src/app/public/not-found/not-found.component.ts
// Phase 4 (2026-05-19): proper 404 surface. The router used to redirect every
// unknown URL to /home silently, which made typos look like nothing happened.
// This page tells the user the route was not found and offers a single
// role-aware "Take me home" button.

import { Component } from '@angular/core';
import { Router } from '@angular/router';
import { SessionService } from '../../core/services/session.service';

@Component({
  selector: 'app-not-found',
  templateUrl: './not-found.component.html',
  styleUrls: ['./not-found.component.css']
})
export class NotFoundComponent {

  attemptedUrl: string;

  constructor(private router: Router, private session: SessionService) {
    // Capture the URL that triggered this component (already routed away from)
    this.attemptedUrl = this.router.url;
  }

  goHome(): void {
    if (!this.session.isLoggedIn()) {
      this.router.navigate(['/home']);
      return;
    }
    const role = this.session.getRole();
    switch (role) {
      case 'SuperAdmin': this.router.navigate(['/admin/dashboard']); break;
      case 'Solver':     this.router.navigate(['/solver/dashboard']); break;
      case 'PWG':        this.router.navigate(['/pwg/complaints']);   break;
      case 'Citizen':    this.router.navigate(['/citizen/home']);     break;
      default:           this.router.navigate(['/home']);
    }
  }
}
