// src/app/shared/components/navbar/navbar.component.ts

import { Component, HostListener, Input, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { SessionService } from '../../../core/services/session.service';

@Component({
  selector: 'app-navbar',
  templateUrl: './navbar.component.html',
  styleUrls: ['./navbar.component.css']
})
export class NavbarComponent implements OnInit {

  /** Passed by each layout. We also read from session as a failsafe. */
  @Input() userRole: string = '';

  userId = 0;
  fullName = '';
  email = '';

  /** Phase 6 §6 — flips on as soon as the page is scrolled past 24px so the
   *  navbar can switch to its frosted-glass state via CSS class binding. */
  scrolled = false;

  @HostListener('window:scroll')
  onScroll(): void {
    this.scrolled = window.scrollY > 24;
  }

  constructor(
    private session: SessionService,
    private router: Router
  ) { }

  ngOnInit(): void {
    const user = this.session.getUser();
    this.userId = user?.userId ?? 0;
    this.fullName = user?.fullName ?? '';
    this.email = user?.email ?? '';

    // §FIX: If parent hasn't passed @Input yet (or public layout has no role),
    // fall back to reading directly from the session so the brand link is correct.
    if (!this.userRole) {
      this.userRole = this.session.getRole();
    }
  }

  // ── Computed routes ──────────────────────────────────────────────────────

  /** Brand logo destination — always points to the user's own home. */
  get brandRoute(): string {
    switch (this.userRole) {
      case 'Citizen': return '/citizen/home';
      case 'Solver': return '/solver/dashboard';
      case 'PWG': return '/pwg/complaints';
      case 'SuperAdmin': return '/admin/dashboard';
      default: return '/login';
    }
  }

  /** Profile page route — null if no profile page exists for this role. */
  get profileRoute(): string | null {
    switch (this.userRole) {
      case 'Solver': return '/solver/profile';
      case 'PWG': return '/pwg/profile';
      case 'Citizen': return '/citizen/profile';
      case 'SuperAdmin': return '/admin/profile';
      default: return null;
    }
  }

  // ── Actions ──────────────────────────────────────────────────────────────

  onLogout(): void {
    this.session.clearSession();
    this.router.navigate(['/login']);
  }
}
