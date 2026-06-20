// src/app/admin/pwg-reports/pwg-reports.component.ts
// US63: Admin reviews PWG reports, applies action (Warn/Suspend/Remove/Dismiss), closes reports.

import { Component, OnInit } from '@angular/core';
import { AdminService } from '../../fmc-services/admin.service';
import { ToastService } from '../../fmc-services/toast.service';
import { SessionService } from '../../core/services/session.service';
import { IPwgReport } from '../../fmc-interfaces/pwg.interface';

@Component({
  selector: 'app-pwg-reports',
  templateUrl: './pwg-reports.component.html',
  styleUrls: ['./pwg-reports.component.css']
})
export class PwgReportsComponent implements OnInit {

  reports: IPwgReport[] = [];
  isLoading = true;
  filterStatus = '';

  // Review modal state
  selectedReport: IPwgReport | null = null;
  reviewAction = '';
  reviewNote   = '';
  finalClose   = false;
  reviewing    = false;

  readonly filterOptions = ['', 'Open', 'Reviewed', 'Closed'];
  readonly actionOptions = ['Warned', 'Suspended', 'Removed', 'Dismissed'];

  private adminId = 0;

  constructor(
    private adminService: AdminService,
    private toast: ToastService,
    private session: SessionService
  ) {}

  ngOnInit(): void {
    this.adminId = this.session.getUserId();
    this.load();
  }

  load(): void {
    this.isLoading = true;
    this.adminService.getAllPwgReports(this.filterStatus).subscribe({
      next: (data) => { this.reports = data || []; this.isLoading = false; },
      error: () => { this.isLoading = false; }
    });
  }

  openReview(report: IPwgReport): void {
    this.selectedReport = report;
    this.reviewAction   = '';
    this.reviewNote     = '';
    this.finalClose     = false;
  }

  closeModal(): void {
    this.selectedReport = null;
  }

  submitReview(): void {
    if (!this.selectedReport || !this.reviewAction || !this.reviewNote.trim()) {
      this.toast.warning('Please select an action and add a note.');
      return;
    }

    this.reviewing = true;

    this.adminService.reviewPwgReport({
      reportId:    this.selectedReport.reportId,
      adminUserId: this.adminId,
      adminAction: this.reviewAction,
      adminNote:   this.reviewNote,
      finalClose:  this.finalClose
    }).subscribe({
      next: (res) => {
        if (res.success) {
          this.toast.success('Report reviewed successfully.');
          this.closeModal();
          this.load();
        } else {
          this.toast.error('Review failed. Please try again.');
        }
        this.reviewing = false;
      },
      error: () => { this.reviewing = false; }
    });
  }

  closeReport(report: IPwgReport): void {
    if (!confirm(`Close report #${report.reportId}?`)) return;

    this.adminService.closePwgReport({
      reportId:    report.reportId,
      adminUserId: this.adminId,
      closeNote:   'Closed by admin.'
    }).subscribe({
      next: (res) => {
        if (res.success) {
          this.toast.success('Report closed.');
          this.load();
        }
      }
    });
  }

  statusClass(status: string): string {
    const map: Record<string,string> = {
      Open:     'badge-Submitted',
      Reviewed: 'badge-In Progress',
      Closed:   'badge-Resolved'
    };
    return map[status] ?? '';
  }

  trackById(_: number, r: IPwgReport): number { return r.reportId; }
}
