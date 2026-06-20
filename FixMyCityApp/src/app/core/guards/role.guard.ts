// src/app/core/guards/role.guard.ts

import { Injectable } from '@angular/core';
import { ActivatedRouteSnapshot, CanActivate, Router } from '@angular/router';
import { SessionService } from '../services/session.service';

@Injectable({ providedIn: 'root' })
export class RoleGuard implements CanActivate {

  constructor(
    private session: SessionService,
    private router: Router
  ) { }

  canActivate(route: ActivatedRouteSnapshot): boolean {
    const allowedRoles: string[] = route.data['roles'];
    const userRole = this.session.getRole();

    if (allowedRoles.includes(userRole)) {
      return true;
    }

    // §FIX: Instead of blindly redirecting to /login (which caused the logo bug),
    // route the user to their actual dashboard. This handles cases where a
    // Citizen accidentally lands on /admin, etc.
    this.navigateByRole(userRole);
    return false;
  }

  private navigateByRole(role: string): void {
    switch (role) {
      case 'SuperAdmin': this.router.navigate(['/admin/dashboard']); break;
      case 'Citizen': this.router.navigate(['/citizen/home']); break;
      case 'Solver': this.router.navigate(['/solver/dashboard']); break;
      case 'PWG': this.router.navigate(['/pwg/complaints']); break;
      default:
        // No valid role means no valid session → send to login
        this.router.navigate(['/login']);
    }
  }
}
