// src/app/citizen/my-complaints/my-complaints.component.ts

import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { ComplaintService } from '../../fmc-services/complaint.service';
import { SessionService } from '../../core/services/session.service';
import { IComplaint } from '../../fmc-interfaces/complaint.interface';
// Phase 8 (§10) — CSV export.
import { exportCsv } from '../../shared/utils/export.util';

@Component({
  selector: 'app-my-complaints',
  templateUrl: './my-complaints.component.html',
  styleUrls: ['./my-complaints.component.css']
})
export class MyComplaintsComponent implements OnInit {

  complaints: IComplaint[] = [];
  isLoading = true;
  errorMessage = '';

  filterStatus = '';
  readonly statusOptions = ['', 'Submitted', 'In Progress', 'Resolved', 'Rejected', 'Escalated', 'Re-opened'];

  private citizenUserId!: number;

  constructor(
    private complaintService: ComplaintService,
    private session: SessionService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.citizenUserId = this.session.getUserId();
    this.loadAll();
  }

  loadAll(): void {
    this.isLoading = true;
    this.complaintService.getComplaintsByCitizen(this.citizenUserId).subscribe({
      next: (data) => { this.complaints = data; this.isLoading = false; },
      error: () => { this.errorMessage = 'Could not load complaints.'; this.isLoading = false; }
    });
  }

  applyFilter(): void {
    if (!this.filterStatus) { this.loadAll(); return; }
    this.isLoading = true;
    // FilterComplaints: pass localityId as undefined (no locality filter on this view)
    this.complaintService.filterComplaints(this.citizenUserId, this.filterStatus, undefined).subscribe({
      next: (data) => { this.complaints = data; this.isLoading = false; },
      error: () => { this.errorMessage = 'Filter failed.'; this.isLoading = false; }
    });
  }

  clearFilter(): void {
    this.filterStatus = '';
    this.loadAll();
  }

  onCardClicked(complaintId: number): void {
    this.router.navigate(['/citizen/complaints', complaintId]);
  }

  get filteredComplaints(): IComplaint[] {
    if (!this.filterStatus) return this.complaints;
    return this.complaints.filter(c => c.status === this.filterStatus);
  }

  /**
   * Phase 8 (§10) — download the currently filtered list as a CSV.
   * Picks the most-useful columns (no nested objects) and a date-stamped
   * filename so downloads don't collide.
   */
  exportCsv(): void {
    const rows = this.filteredComplaints.map(c => ({
      complaintId:  c.complaintId,
      title:        c.title,
      category:     c.category?.categoryName ?? '',
      locality:     c.locality?.localityName ?? '',
      address:      c.address,
      status:       c.status,
      criticality:  c.criticality,
      submittedAt:  c.submittedAt,
      resolvedAt:   c.resolvedAt ?? '',
      department:   c.department?.deptName ?? '',
    }));
    const stamp = new Date().toISOString().slice(0, 10);
    exportCsv(`my-complaints-${stamp}.csv`, rows, [
      { key: 'complaintId', label: 'ID' },
      { key: 'title',       label: 'Title' },
      { key: 'category',    label: 'Category' },
      { key: 'locality',    label: 'Locality' },
      { key: 'address',     label: 'Address' },
      { key: 'status',      label: 'Status' },
      { key: 'criticality', label: 'Criticality' },
      { key: 'submittedAt', label: 'Submitted At' },
      { key: 'resolvedAt',  label: 'Resolved At' },
      { key: 'department',  label: 'Department' },
    ]);
  }
}
