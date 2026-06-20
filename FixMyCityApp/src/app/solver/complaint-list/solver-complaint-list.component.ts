// src/app/solver/complaint-list/solver-complaint-list.component.ts

import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ComplaintService } from '../../fmc-services/complaint.service';
import { PwgService } from '../../fmc-services/pwg.service';
import { SessionService } from '../../core/services/session.service';
import { IComplaint } from '../../fmc-interfaces/complaint.interface';

@Component({
  selector: 'app-solver-complaint-list',
  templateUrl: './solver-complaint-list.component.html',
  styleUrls: ['./solver-complaint-list.component.css']
})
export class SolverComplaintListComponent implements OnInit {

  complaints: IComplaint[] = [];
  isLoading = true;
  errorMessage = '';

  filterStatus = '';
  filterCriticality = '';

  readonly statusOptions = ['', 'Submitted', 'In Progress', 'Resolved', 'Rejected', 'Escalated'];
  readonly criticalityOptions = ['', 'Low', 'Medium', 'High', 'Critical'];

  private deptId = 0;

  constructor(
    private complaintService: ComplaintService,
    private pwgService: PwgService,
    private session: SessionService,
    private route: ActivatedRoute,
    private router: Router
  ) {}

  ngOnInit(): void {
    // Support ?status= from dashboard card links
    this.route.queryParams.subscribe(params => {
      if (params['status']) this.filterStatus = params['status'];
    });

    // Get deptId: prefer session (enriched by login), fall back to API call
    const sessionDeptId = this.session.getDeptId();
    if (sessionDeptId > 0) {
      this.deptId = sessionDeptId;
      this.loadComplaints();
    } else {
      this.pwgService.getDeptProfile(this.session.getUserId()).subscribe({
        next: (dept) => { this.deptId = dept.deptId; this.loadComplaints(); },
        error: () => { this.errorMessage = 'Could not load department. Please try again.'; this.isLoading = false; }
      });
    }
  }

  loadComplaints(): void {
    this.isLoading = true;
    this.complaintService.getComplaintsByDept(
      this.deptId, this.filterStatus, undefined, this.filterCriticality
    ).subscribe({
      next: (data) => { this.complaints = data; this.isLoading = false; },
      error: () => { this.errorMessage = 'Could not load complaints.'; this.isLoading = false; }
    });
  }

  clearFilter(): void {
    this.filterStatus = '';
    this.filterCriticality = '';
    this.loadComplaints();
  }

  onCardClicked(complaintId: number): void {
    this.router.navigate(['/solver/complaints', complaintId]);
  }
}
