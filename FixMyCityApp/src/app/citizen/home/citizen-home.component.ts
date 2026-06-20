// src/app/citizen/home/citizen-home.component.ts

import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';

import { ComplaintService } from '../../fmc-services/complaint.service';
import { MlService } from '../../fmc-services/ml.service';
import { SessionService } from '../../core/services/session.service';
import { IComplaint } from '../../fmc-interfaces/complaint.interface';

@Component({
  selector: 'app-citizen-home',
  templateUrl: './citizen-home.component.html',
  styleUrls: ['./citizen-home.component.css']
})
export class CitizenHomeComponent implements OnInit {

  // Locality feed
  feedComplaints: IComplaint[] = [];
  feedLoading = true;
  feedError = '';

  // AI recommendations
  recommendedComplaints: IComplaint[] = [];
  recsLoading = false;
  recsError = '';

  fullName = '';
  localityId = 0;
  userId = 0;

  activeTab: 'feed' | 'recommended' | 'activity' = 'feed';

  constructor(
    private complaintService: ComplaintService,
    private ml: MlService,
    private session: SessionService,
    private router: Router
  ) {}

  ngOnInit(): void {
    const user = this.session.getUser();
    this.fullName  = user?.fullName  ?? '';
    this.localityId = user?.localityId ?? 0;
    this.userId    = user?.userId    ?? 0;

    this.loadFeed();
    this.loadRecommendations();
  }

  loadFeed(): void {
    this.feedLoading = true;
    this.feedError = '';

    this.complaintService.getLocalityFeed(this.localityId).subscribe({
      next: (data) => { this.feedComplaints = data; this.feedLoading = false; },
      error: () => { this.feedError = 'Could not load locality feed.'; this.feedLoading = false; }
    });
  }

  loadRecommendations(): void {
    if (!this.userId) return;
    this.recsLoading = true;

    // 1. Get recommended complaintIds from AI
    this.ml.getRecommendedComplaints(this.userId, 8).pipe(
      catchError(() => of([]))
    ).subscribe(recs => {
      if (recs.length === 0) {
        this.recsLoading = false;
        return;
      }

      // 2. Fetch complaint details for each id
      const requests = recs.slice(0, 8).map(r =>
        this.complaintService.getComplaintById(r.complaintId).pipe(catchError(() => of(null)))
      );

      forkJoin(requests).subscribe(results => {
        this.recommendedComplaints = results.filter((c): c is IComplaint => !!c);
        this.recsLoading = false;
      });
    });
  }

  onCardClicked(complaintId: number): void {
    this.router.navigate(['/citizen/complaints', complaintId]);
  }

  goToSubmit(): void {
    this.router.navigate(['/citizen/submit']);
  }

  setTab(tab: 'feed' | 'recommended' | 'activity'): void {
    this.activeTab = tab as any;
  }
}
