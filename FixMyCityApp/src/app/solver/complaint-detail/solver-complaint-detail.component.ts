// src/app/solver/complaint-detail/solver-complaint-detail.component.ts

import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';

import { ComplaintService } from '../../fmc-services/complaint.service';
import { MlService } from '../../fmc-services/ml.service';
import { PaymentService } from '../../fmc-services/payment.service';
import { ToastService } from '../../fmc-services/toast.service';
import { SessionService } from '../../core/services/session.service';
import { IComplaint, IComplaintTimeline } from '../../fmc-interfaces/complaint.interface';
import { IMLScores, IComplaintTag } from '../../fmc-interfaces/ml.interface';

@Component({
  selector: 'app-solver-complaint-detail',
  templateUrl: './solver-complaint-detail.component.html',
  styleUrls: ['./solver-complaint-detail.component.css']
})
export class SolverComplaintDetailComponent implements OnInit {

  complaint: IComplaint | null = null;
  timeline: IComplaintTimeline[] = [];
  mlScores: IMLScores | null = null;
  tags: IComplaintTag[] = [];
  fundingTotal = 0;

  isLoading = true;
  errorMessage = '';

  statusForm!: FormGroup;
  dateForm!: FormGroup;

  readonly statusOptions = ['In Progress', 'Resolved', 'Rejected', 'Escalated'];

  private solverUserId!: number;

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private fb: FormBuilder,
    private complaintService: ComplaintService,
    private ml: MlService,
    private paymentService: PaymentService,
    private toast: ToastService,
    private session: SessionService
  ) {}

  ngOnInit(): void {
    this.solverUserId = this.session.getUserId();

    this.statusForm = this.fb.group({
      newStatus: ['', Validators.required],
      remark:    [''],
      resolutionFilePath: [''],
      resolutionFileName: ['']
    });

    this.dateForm = this.fb.group({
      estDate: ['', Validators.required]
    });

    const id = Number(this.route.snapshot.paramMap.get('id'));
    this.loadAll(id);
  }

  loadAll(id: number): void {
    this.isLoading = true;

    forkJoin({
      complaint: this.complaintService.getComplaintById(id),
      timeline:  this.complaintService.getTimeline(id).pipe(catchError(() => of([]))),
      mlScores:  this.ml.getMLScores(id).pipe(catchError(() => of(null))),
      tags:      this.ml.getTags(id).pipe(catchError(() => of([]))),
      funding:   this.paymentService.getFundingTotal(id).pipe(catchError(() => of({ success: false, complaintId: id, fundingTotal: 0 })))
    }).subscribe({
      next: ({ complaint, timeline, mlScores, tags, funding }) => {
        this.complaint   = complaint;
        this.timeline    = timeline;
        this.mlScores    = mlScores;
        this.tags        = tags;
        this.fundingTotal = funding.fundingTotal;
        this.isLoading   = false;
      },
      error: () => {
        this.errorMessage = 'Could not load complaint details.';
        this.isLoading    = false;
      }
    });
  }

  updateStatus(): void {
    if (this.statusForm.invalid || !this.complaint) return;

    this.complaintService.updateStatus({
      complaintId:         this.complaint.complaintId,
      solverUserId:        this.solverUserId,
      newStatus:           this.statusForm.value.newStatus,
      remark:              this.statusForm.value.remark,
      resolutionFilePath:  this.statusForm.value.resolutionFilePath,
      resolutionFileName:  this.statusForm.value.resolutionFileName
    }).subscribe({
      next: (res) => {
        if (res.success) {
          this.toast.success('Status updated successfully.');
          this.statusForm.reset();
          this.loadAll(this.complaint!.complaintId);
        } else {
          this.toast.error((res as any).message ?? 'Status update failed.');
        }
      },
      error: () => this.toast.error('Could not update status.')
    });
  }

  setEstimatedDate(): void {
    if (this.dateForm.invalid || !this.complaint) return;

    this.complaintService.setEstimatedDate({
      complaintId:  this.complaint.complaintId,
      solverUserId: this.solverUserId,
      estDate:      this.dateForm.value.estDate
    }).subscribe({
      next: (res) => {
        if (res.success) {
          this.toast.success('Estimated date saved.');
          this.loadAll(this.complaint!.complaintId);
          this.dateForm.reset();
        } else {
          this.toast.error((res as any).message ?? 'Date update failed.');
        }
      },
      error: () => this.toast.error('Could not save estimated date.')
    });
  }

  get priorityLabel(): string {
    const score = this.mlScores?.priorityScore;
    if (score == null) return '—';
    if (score >= 75) return 'High';
    if (score >= 45) return 'Medium';
    return 'Low';
  }

  get priorityClass(): string {
    const score = this.mlScores?.priorityScore;
    if (score == null) return '';
    if (score >= 75) return 'priority--high';
    if (score >= 45) return 'priority--medium';
    return 'priority--low';
  }

  goBack(): void {
    this.router.navigate(['/solver/complaints']);
  }
}
