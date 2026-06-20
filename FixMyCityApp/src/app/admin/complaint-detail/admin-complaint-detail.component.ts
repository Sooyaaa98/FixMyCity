// src/app/admin/complaint-detail/admin-complaint-detail.component.ts

import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';

import { ComplaintService } from '../../fmc-services/complaint.service';
import { IComplaint, IComplaintTimeline } from '../../fmc-interfaces/complaint.interface';

@Component({
  selector: 'app-admin-complaint-detail',
  templateUrl: './admin-complaint-detail.component.html',
  styleUrls: ['./admin-complaint-detail.component.css']
})
export class AdminComplaintDetailComponent implements OnInit {

  complaint: IComplaint | null = null;
  timeline: IComplaintTimeline[] = [];
  isLoading = true;
  errorMessage = '';

  private complaintId!: number;

  constructor(
    private complaintService: ComplaintService,
    private route: ActivatedRoute,
    private router: Router
  ) { }

  ngOnInit(): void {
    this.complaintId = Number(this.route.snapshot.paramMap.get('id'));
    if (!this.complaintId) {
      this.router.navigate(['/admin/complaints']);
      return;
    }
    this.loadComplaint();
  }

  loadComplaint(): void {
    this.isLoading = true;
    this.complaintService.getComplaintById(this.complaintId).subscribe({
      next: (data) => {
        this.complaint = data;
        this.loadTimeline();
      },
      error: () => {
        this.errorMessage = 'Could not load complaint.';
        this.isLoading = false;
      }
    });
  }

  loadTimeline(): void {
    this.complaintService.getTimeline(this.complaintId).subscribe({
      next: (data) => { this.timeline = data; this.isLoading = false; },
      error: () => { this.isLoading = false; }      // timeline failure is non-fatal
    });
  }

  goBack(): void {
    this.router.navigate(['/admin/complaints']);
  }
}
