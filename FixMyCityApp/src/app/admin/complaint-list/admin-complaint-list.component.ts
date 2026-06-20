// src/app/admin/complaint-list/admin-complaint-list.component.ts

import { Component, OnInit, OnDestroy } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { Subscription } from 'rxjs';

import { AdminService } from '../../fmc-services/admin.service';
import { IComplaint } from '../../fmc-interfaces/complaint.interface';

@Component({
  selector: 'app-admin-complaint-list',
  templateUrl: './admin-complaint-list.component.html',
  styleUrls: ['./admin-complaint-list.component.css']
})
export class AdminComplaintListComponent implements OnInit, OnDestroy {

  complaints: IComplaint[] = [];
  isLoading = true;
  errorMessage = '';

  // Two-way bound to the status dropdown
  filterStatus = '';

  readonly statusOptions = [
    '', 'Submitted', 'In Progress', 'Resolved',
    'Rejected', 'Escalated', 'Reopened'
  ];

  private querySub!: Subscription;

  constructor(
    private adminService: AdminService,
    private route: ActivatedRoute,
    private router: Router
  ) { }

  ngOnInit(): void {
    // §FIX: react to query-param changes (dashboard cards set ?status=X)
    this.querySub = this.route.queryParams.subscribe(params => {
      this.filterStatus = params['status'] ?? '';
      this.loadComplaints();
    });
  }

  ngOnDestroy(): void {
    this.querySub?.unsubscribe();
  }

  loadComplaints(): void {
    this.isLoading = true;
    this.errorMessage = '';
    this.adminService.getAdminComplaints(this.filterStatus).subscribe({
      next: (data) => { this.complaints = data || []; this.isLoading = false; },
      error: () => { this.errorMessage = 'Could not load complaints.'; this.isLoading = false; }
    });
  }

  /** Called when the dropdown changes — updates the URL so the browser back
   *  button restores the correct filter. */
  onStatusChange(): void {
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { status: this.filterStatus || null },
      queryParamsHandling: 'merge'
    });
  }

  clearFilter(): void {
    this.router.navigate(['/admin/complaints']);
  }

  onCardClicked(complaintId: number): void {
    this.router.navigate(['/admin/complaints', complaintId]);
  }

  // ── Derived label for the page header ────────────────────────────────
  get pageTitle(): string {
    return this.filterStatus
      ? `${this.filterStatus} Complaints`
      : 'All Complaints';
  }
}
