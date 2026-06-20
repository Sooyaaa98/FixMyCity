// src/app/solver/dashboard/solver-dashboard.component.ts

import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { ComplaintService } from '../../fmc-services/complaint.service';
import { PwgService } from '../../fmc-services/pwg.service';
import { SessionService } from '../../core/services/session.service';
import { IComplaint } from '../../fmc-interfaces/complaint.interface';

@Component({
  selector: 'app-solver-dashboard',
  templateUrl: './solver-dashboard.component.html',
  styleUrls: ['./solver-dashboard.component.css']
})
export class SolverDashboardComponent implements OnInit {

  complaints: IComplaint[] = [];
  isLoading = true;
  errorMessage = '';

  get totalCount(): number    { return this.complaints.length; }
  get pendingCount(): number  { return this.complaints.filter(c => c.status === 'Submitted').length; }
  get inProgressCount(): number { return this.complaints.filter(c => c.status === 'In Progress').length; }
  get resolvedCount(): number { return this.complaints.filter(c => c.status === 'Resolved').length; }
  get escalatedCount(): number { return this.complaints.filter(c => c.status === 'Escalated').length; }

  private deptId = 0;

  constructor(
    private complaintService: ComplaintService,
    private pwgService: PwgService,
    private session: SessionService,
    private router: Router
  ) {}

  ngOnInit(): void {
    // Prefer deptId from session (enriched via GetUserById during login)
    const sessionDeptId = this.session.getDeptId();
    if (sessionDeptId > 0) {
      this.deptId = sessionDeptId;
      this.loadComplaints();
    } else {
      // Fallback: API call to get department profile
      this.pwgService.getDeptProfile(this.session.getUserId()).subscribe({
        next: (profile) => { this.deptId = profile.deptId; this.loadComplaints(); },
        error: () => { this.errorMessage = 'Could not load department. Please try again.'; this.isLoading = false; }
      });
    }
  }

  loadComplaints(): void {
    this.isLoading = true;
    this.complaintService.getComplaintsByDept(this.deptId).subscribe({
      next: (data) => { this.complaints = data; this.isLoading = false; },
      error: () => { this.errorMessage = 'Could not load complaints.'; this.isLoading = false; }
    });
  }

  onCardClicked(complaintId: number): void {
    this.router.navigate(['/solver/complaints', complaintId]);
  }

  navigateTo(path: string): void {
    this.router.navigate([path]);
  }
}
