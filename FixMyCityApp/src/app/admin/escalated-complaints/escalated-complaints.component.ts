// src/app/admin/escalated-complaints/escalated-complaints.component.ts

import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { AdminService } from '../../fmc-services/admin.service';
import { SessionService } from '../../core/services/session.service';
import { ToastService } from '../../fmc-services/toast.service';
import { IComplaint } from '../../fmc-interfaces/complaint.interface';
import { IDepartment } from '../../fmc-interfaces/pwg.interface';

@Component({
  selector: 'app-escalated-complaints',
  templateUrl: './escalated-complaints.component.html',
  styleUrls: ['./escalated-complaints.component.css']
})
export class EscalatedComplaintsComponent implements OnInit {

  complaints: IComplaint[] = [];
  departments: IDepartment[] = [];
  isLoading = true;
  errorMessage = '';

  // ── Action panel ──────────────────────────────────────────────────────────
  selectedComplaint: IComplaint | null = null;
  escalationLog: any[] = [];
  logLoading = false;

  reassignDeptId: number | null = null;
  reassignReason = '';
  reassigning = false;

  forceStatus = '';
  forceRemark = '';
  forcingStatus = false;

  readonly forceStatusOptions = ['Resolved', 'Rejected', 'Closed'];
  private adminId = 0;

  constructor(
    private adminService: AdminService,
    private session: SessionService,
    private toast: ToastService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.adminId = this.session.getUserId();
    this.loadEscalated();
    this.loadDepartments();
  }

  loadEscalated(): void {
    this.isLoading = true;
    this.errorMessage = '';
    this.adminService.getEscalatedComplaints().subscribe({
      next: (data) => { this.complaints = data; this.isLoading = false; },
      error: () => { this.errorMessage = 'Could not load escalated complaints.'; this.isLoading = false; }
    });
  }

  loadDepartments(): void {
    this.adminService.getAllDepartments().subscribe({
      next: (depts) => { this.departments = depts; },
      error: () => {}
    });
  }

  // ── Select a complaint to act on ─────────────────────────────────────────

  selectComplaint(c: IComplaint): void {
    if (this.selectedComplaint?.complaintId === c.complaintId) {
      this.selectedComplaint = null;
      this.escalationLog = [];
      return;
    }
    this.selectedComplaint = c;
    this.reassignDeptId = null;
    this.reassignReason = '';
    this.forceStatus = '';
    this.forceRemark = '';
    this.loadEscalationLog(c.complaintId);
  }

  loadEscalationLog(id: number): void {
    this.logLoading = true;
    this.adminService.getEscalationLog(id).subscribe({
      next: (log) => { this.escalationLog = log; this.logLoading = false; },
      error: () => { this.logLoading = false; }
    });
  }

  viewDetail(complaintId: number): void {
    this.router.navigate(['/admin/complaints', complaintId]);
  }

  // ── Reassign department ───────────────────────────────────────────────────

  doReassign(): void {
    if (!this.selectedComplaint || !this.reassignDeptId) {
      this.toast.warning('Please select a department.');
      return;
    }
    if (!this.reassignReason.trim()) {
      this.toast.warning('Please provide a reason for reassignment.');
      return;
    }
    this.reassigning = true;
    this.adminService.reassignDept({
      complaintId: this.selectedComplaint.complaintId,
      newDeptId:   this.reassignDeptId,
      adminUserId: this.adminId,
      reason:      this.reassignReason,
    }).subscribe({
      next: (res) => {
        this.reassigning = false;
        if (res.success) {
          this.toast.success('Department reassigned successfully.');
          this.reassignDeptId = null;
          this.reassignReason = '';
          this.loadEscalated();
        } else {
          this.toast.error('Reassignment failed.');
        }
      },
      error: () => { this.reassigning = false; this.toast.error('Server error.'); }
    });
  }

  // ── Force status change ───────────────────────────────────────────────────

  doForceStatus(): void {
    if (!this.selectedComplaint || !this.forceStatus) {
      this.toast.warning('Please select a target status.');
      return;
    }
    this.forcingStatus = true;
    this.adminService.bulkUpdateStatus({
      complaintIds: [this.selectedComplaint.complaintId],
      newStatus:    this.forceStatus,
      actorUserId:  this.adminId,
      remark:       this.forceRemark || `Admin force-${this.forceStatus.toLowerCase()}.`,
    }).subscribe({
      next: (res) => {
        this.forcingStatus = false;
        if (res.success) {
          this.toast.success(`Complaint marked as ${this.forceStatus}.`);
          this.selectedComplaint = null;
          this.loadEscalated();
        } else {
          this.toast.error('Status update failed.');
        }
      },
      error: () => { this.forcingStatus = false; this.toast.error('Server error.'); }
    });
  }
}
