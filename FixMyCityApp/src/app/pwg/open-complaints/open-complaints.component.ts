// src/app/pwg/open-complaints/open-complaints.component.ts

import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { PwgService } from '../../fmc-services/pwg.service';
import { AuthService } from '../../fmc-services/auth.service';
import { ToastService } from '../../fmc-services/toast.service';
import { SessionService } from '../../core/services/session.service';
import { IComplaint, IIssueCategory } from '../../fmc-interfaces/complaint.interface';

@Component({
  selector: 'app-open-complaints',
  templateUrl: './open-complaints.component.html',
  styleUrls: ['./open-complaints.component.css']
})
export class OpenComplaintsComponent implements OnInit {

  complaints: IComplaint[] = [];
  categories: IIssueCategory[] = [];
  isLoading = true;
  errorMessage = '';

  filterCategoryId?: number;
  filterCriticality = '';
  readonly criticalityOptions = ['', 'Low', 'Medium', 'High', 'Critical'];

  // Inline request form state
  activeRequestComplaintId: number | null = null;
  requestNote = '';
  requestLoading = false;
  requestMessage = '';
  requestError = '';

  private orgId!: number;

  constructor(
    private pwgService: PwgService,
    private authService: AuthService,
    private toast: ToastService,
    private session: SessionService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.orgId = this.session.getOrgId();
    this.loadComplaints();
    this.authService.getAllCategories().subscribe({ next: (c) => this.categories = c });
  }

  loadComplaints(): void {
    this.isLoading = true;
    this.errorMessage = '';
    this.pwgService.getOpenComplaints(
      this.filterCategoryId, undefined, this.filterCriticality || undefined
    ).subscribe({
      next: (d) => { this.complaints = d; this.isLoading = false; },
      error: () => {
        this.errorMessage = 'Could not load open complaints.';
        this.isLoading = false;
      }
    });
  }

  clearFilter(): void {
    this.filterCategoryId = undefined;
    this.filterCriticality = '';
    this.loadComplaints();
  }

  // viewDetail opens the inline participation form (PWG has no separate detail view)
  viewDetail(complaintId: number): void {
    this.openRequestForm(complaintId);
  }

  openRequestForm(complaintId: number): void {
    this.activeRequestComplaintId = complaintId;
    this.requestNote = '';
    this.requestMessage = '';
    this.requestError = '';
  }

  cancelRequest(): void {
    this.activeRequestComplaintId = null;
    this.requestError = '';
  }

  submitRequest(complaintId: number): void {
    this.requestLoading = true;
    this.requestMessage = '';
    this.requestError = '';

    this.pwgService.submitParticipationRequest({
      complaintId,
      orgId: this.orgId,
      requestNote: this.requestNote || undefined
    }).subscribe({
      next: (res) => {
        this.requestLoading = false;
        if (res.success) {
          this.requestMessage = `Request #${res.requestId} submitted successfully!`;
          this.activeRequestComplaintId = null;
          this.toast.success(`Participation request #${res.requestId} submitted.`);
        } else {
          this.requestError = 'Submission failed — you may have already requested this.';
        }
      },
      error: () => {
        this.requestLoading = false;
        this.requestError = 'Could not submit request. Please try again.';
      }
    });
  }
}
