// src/app/citizen/complaint-detail/citizen-complaint-detail.component.ts

import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';

import { ComplaintService } from '../../fmc-services/complaint.service';
import { PaymentService } from '../../fmc-services/payment.service';
import { ToastService } from '../../fmc-services/toast.service';
import { SessionService } from '../../core/services/session.service';
import {
  IComplaint, IComplaintTimeline, IComplaintAttachment
} from '../../fmc-interfaces/complaint.interface';
import { IContribution } from '../../fmc-interfaces/payment.interface';
// Phase 8 — share / QR utilities (§4, §8).
import { shareOrCopy } from '../../shared/utils/share.util';
import { qrUrl, downloadQr } from '../../shared/utils/qr.util';

@Component({
  selector: 'app-citizen-complaint-detail',
  templateUrl: './citizen-complaint-detail.component.html',
  styleUrls: ['./citizen-complaint-detail.component.css']
})
export class CitizenComplaintDetailComponent implements OnInit {

  complaint: IComplaint | null = null;
  timeline: IComplaintTimeline[] = [];
  attachments: IComplaintAttachment[] = [];
  // Phase 8 (§3) — keep all attachments unfiltered so the before/after viewer
  // can pull out the Resolution-typed one.
  allAttachments: IComplaintAttachment[] = [];

  // Payment
  contributions: IContribution[] = [];
  fundingTotal = 0;
  paymentLoading = false;
  showContributeForm = false;
  contributeAmount: number | null = null;

  // Rating / reopen
  hasRated = false;
  lastStars = 0;
  ratingForm!: FormGroup;
  reopenForm!: FormGroup;

  isLoading = true;
  errorMessage = '';

  readonly starOptions = [1, 2, 3, 4, 5];

  // Phase 8 (§6) — Appeal state
  appealReason = '';
  appealLoading = false;
  myAppealForThis: any = null;   // last filed appeal for this complaint, if any
  showAppealForm = false;

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private fb: FormBuilder,
    private complaintService: ComplaintService,
    private paymentService: PaymentService,
    private toast: ToastService,
    private session: SessionService
  ) {}

  ngOnInit(): void {
    this.ratingForm = this.fb.group({
      stars:   [null, Validators.required],
      comment: ['']
    });
    this.reopenForm = this.fb.group({
      reason: ['', [Validators.required, Validators.minLength(10)]]
    });

    const id = Number(this.route.snapshot.paramMap.get('id'));
    this.loadAll(id);
    this.loadMyAppeals(id);
  }

  /** Phase 8 (§6) — show "Already appealed" vs the appeal form. */
  private loadMyAppeals(complaintId: number): void {
    const uid = this.session.getUserId();
    if (!uid) return;
    this.complaintService.getMyAppeals(uid).subscribe({
      next: (rows) => {
        this.myAppealForThis = (rows ?? []).find((a: any) => a.complaintId === complaintId) ?? null;
      },
      error: () => { /* silent */ }
    });
  }

  submitAppeal(): void {
    const text = this.appealReason.trim();
    if (!text || text.length < 10 || !this.complaint) return;
    this.appealLoading = true;
    this.complaintService.submitAppeal({
      complaintId:   this.complaint.complaintId,
      citizenUserId: this.session.getUserId(),
      reason:        text,
    }).subscribe({
      next: (r) => {
        this.appealLoading = false;
        if (r?.success) {
          this.toast.success('Appeal filed. An administrator will review it shortly.');
          this.showAppealForm = false;
          this.appealReason = '';
          this.loadMyAppeals(this.complaint!.complaintId);
        } else {
          this.toast.error('Could not file appeal.');
        }
      },
      error: () => {
        this.appealLoading = false;
        this.toast.error('Could not file appeal.');
      }
    });
  }

  // ── Load ──────────────────────────────────────────────────────────────────

  loadAll(id: number): void {
    this.isLoading = true;

    forkJoin({
      complaint:   this.complaintService.getComplaintById(id),
      timeline:    this.complaintService.getTimeline(id).pipe(catchError(() => of([]))),
      attachments: this.complaintService.getAttachments(id, 'complaint').pipe(catchError(() => of([]))),
      // Phase 8 (§3) — also fetch the resolution photo so the before/after
      // slider can render once the complaint is resolved.
      allAttachments: this.complaintService.getAttachments(id, '').pipe(catchError(() => of([]))),
      funding:     this.paymentService.getFundingTotal(id).pipe(catchError(() => of({ success: false, complaintId: id, fundingTotal: 0 }))),
      contributions: this.paymentService.getContributionsByComplaint(id).pipe(catchError(() => of([])))
    }).subscribe({
      next: ({ complaint, timeline, attachments, allAttachments, funding, contributions }) => {
        this.complaint      = complaint;
        this.timeline       = timeline;
        this.attachments    = attachments;
        this.allAttachments = allAttachments;
        this.fundingTotal   = funding.fundingTotal;
        this.contributions  = contributions;
        this.isLoading      = false;
      },
      error: () => {
        this.errorMessage = 'Could not load complaint details.';
        this.isLoading    = false;
      }
    });
  }

  // ── Rating ────────────────────────────────────────────────────────────────

  setStars(n: number): void {
    this.ratingForm.patchValue({ stars: n });
  }

  submitRating(): void {
    if (this.ratingForm.invalid || !this.complaint) return;

    this.complaintService.rateComplaint({
      complaintId:   this.complaint.complaintId,
      citizenUserId: this.session.getUserId(),
      stars:         this.ratingForm.value.stars,
      comment:       this.ratingForm.value.comment
    }).subscribe({
      next: (res) => {
        if (res.success) {
          this.hasRated  = true;
          this.lastStars = this.ratingForm.value.stars;
          this.toast.success('Thank you for your rating!');
        } else {
          this.toast.error((res as any).message ?? 'Rating failed.');
        }
      },
      error: () => this.toast.error('Could not submit rating.')
    });
  }

  // ── Reopen ────────────────────────────────────────────────────────────────

  submitReopen(): void {
    if (this.reopenForm.invalid || !this.complaint) return;

    this.complaintService.reopenComplaint({
      complaintId:   this.complaint.complaintId,
      citizenUserId: this.session.getUserId(),
      reason:        this.reopenForm.value.reason
    }).subscribe({
      next: (res) => {
        if (res.success) {
          this.toast.success('Complaint reopened successfully.');
          this.loadAll(this.complaint!.complaintId);
        } else {
          this.toast.error((res as any).message ?? 'Reopen failed.');
        }
      },
      error: () => this.toast.error('Could not reopen complaint.')
    });
  }

  // ── Payment ───────────────────────────────────────────────────────────────

  async openContributePanel(): Promise<void> {
    this.showContributeForm = true;
  }

  async pay(): Promise<void> {
    if (!this.contributeAmount || this.contributeAmount < 1 || !this.complaint) return;

    this.paymentLoading = true;
    const amount = this.contributeAmount;

    try {
      // Phase 5 (2026-05-19): single-call end-to-end flow.
      // contributeViaRazorpay handles: server-side order creation →
      // open the Razorpay modal → forward order_id/payment_id/signature to
      // the server-side verify endpoint → persist the contribution row with
      // PaymentStatus = Success. Throws on cancel / signature failure.
      const result = await this.paymentService.contributeViaRazorpay(
        this.complaint.complaintId,
        this.session.getUserId(),
        amount,
        {
          name:    this.session.getFullName(),
          email:   this.session.getEmail(),
        },
      );

      this.toast.success(
        result.demoMode
          ? `Demo: ₹${amount} contributed (no real money moved).`
          : `₹${amount} contributed successfully — thank you!`
      );
      this.fundingTotal += amount;
      this.showContributeForm = false;
      this.contributeAmount = null;

      // Refresh the contributions list so the new row shows up immediately.
      this.paymentService.getContributionsByComplaint(this.complaint.complaintId)
        .subscribe(c => this.contributions = c);

    } catch (err: any) {
      const msg = (err?.message ?? '').toString();
      if (msg === 'Payment cancelled.') {
        // User dismissed the Razorpay modal — silent, not an error.
      } else {
        this.toast.error(msg || 'Payment failed. Please try again.');
      }
    } finally {
      this.paymentLoading = false;
    }
  }

  cancelContribute(): void {
    this.showContributeForm = false;
    this.contributeAmount = null;
  }

  // ── Helpers ───────────────────────────────────────────────────────────────

  goBack(): void {
    this.router.navigate(['/citizen/complaints']);
  }

  get canRate(): boolean {
    return this.complaint?.status === 'Resolved' && !this.hasRated;
  }

  get canReopen(): boolean {
    return this.hasRated && this.lastStars < 3;
  }

  get isOwner(): boolean {
    return this.complaint?.citizenUserId === this.session.getUserId();
  }

  // ── Phase 8 — Share + QR (§4, §8) ───────────────────────────────────────

  /** Public URL for this complaint — uses location.origin so prod/dev both work. */
  get publicUrl(): string {
    if (!this.complaint) return '';
    return `${window.location.origin}/citizen/complaints/${this.complaint.complaintId}`;
  }

  /** Image src for inline rendering of the QR code. */
  get qrImageSrc(): string {
    return this.publicUrl ? qrUrl(this.publicUrl, 200, 'M') : '';
  }

  /** UI affordance: native-share sheet on mobile, clipboard copy elsewhere. */
  async shareThis(): Promise<void> {
    if (!this.complaint) return;
    const title = `Complaint #${this.complaint.complaintId} — ${this.complaint.title}`;
    const res   = await shareOrCopy(this.publicUrl, title, this.complaint.description);
    if (res.method === 'copied')      this.toast.success('Link copied to clipboard.');
    else if (res.method === 'shared') this.toast.info('Opened native share sheet.');
    else                              this.toast.error('Could not share. Please copy the URL manually.');
  }

  /** Trigger a PNG download of the QR code (§8 — useful for posters / printouts). */
  downloadQrCode(): void {
    if (!this.complaint) return;
    downloadQr(this.publicUrl, `complaint-${this.complaint.complaintId}-qr.png`);
  }
}
