// src/app/pwg/my-requests/my-requests.component.ts

import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { PwgService } from '../../fmc-services/pwg.service';
import { SessionService } from '../../core/services/session.service';
import { IPwgParticipationRequest } from '../../fmc-interfaces/pwg.interface';

@Component({
  selector: 'app-my-requests',
  templateUrl: './my-requests.component.html',
  styleUrls: ['./my-requests.component.css']
})
export class MyRequestsComponent implements OnInit {

  requests: IPwgParticipationRequest[] = [];
  isLoading = true;
  errorMessage = '';

  private orgId = 0;

  constructor(
    private pwgService: PwgService,
    private session: SessionService,
    private router: Router
  ) {}

  ngOnInit(): void {
    // Prefer session.getOrgId() (enriched after login via GetUserById)
    const sessionOrgId = this.session.getOrgId();
    if (sessionOrgId > 0) {
      this.orgId = sessionOrgId;
      this.loadRequests();
    } else {
      // Fallback: look up orgId from the profile API
      this.pwgService.getOrgProfile(this.session.getUserId()).subscribe({
        next: (profile) => {
          this.orgId = profile.orgId;
          this.loadRequests();
        },
        error: () => {
          this.errorMessage = 'Could not load organisation details. Please try again.';
          this.isLoading = false;
        }
      });
    }
  }

  loadRequests(): void {
    this.isLoading = true;
    this.pwgService.getRequestsByOrg(this.orgId).subscribe({
      next: (data) => { this.requests = data; this.isLoading = false; },
      error: () => { this.errorMessage = 'Could not load requests.'; this.isLoading = false; }
    });
  }

  goToProgress(complaintId: number): void {
    this.router.navigate(['/pwg/progress', complaintId]);
  }

  getStatusClass(status: string): string {
    return `badge-status badge-${status}`;
  }
}
