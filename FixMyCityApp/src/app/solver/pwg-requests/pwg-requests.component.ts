// src/app/solver/pwg-requests/pwg-requests.component.ts

import { Component, OnInit } from '@angular/core';
import { PwgService } from '../../fmc-services/pwg.service';
import { SessionService } from '../../core/services/session.service';
import { IPwgParticipationRequest } from '../../fmc-interfaces/pwg.interface';

type ActiveTab = 'pending' | 'all';

@Component({
  selector: 'app-pwg-requests',
  templateUrl: './pwg-requests.component.html',
  styleUrls: ['./pwg-requests.component.css']
})
export class PwgRequestsComponent implements OnInit {

  activeTab: ActiveTab = 'pending';

  pendingRequests: IPwgParticipationRequest[] = [];
  allRequests: IPwgParticipationRequest[] = [];

  isLoadingPending = true;
  isLoadingAll = true;

  actionMessage = '';
  actionError = '';

  // Tracks which request has the decision note input open
  activeDecisionId: number | null = null;
  decisionNote = '';

  private solverUserId!: number;

  constructor(
    private pwgService: PwgService,
    private session: SessionService
  ) { }

  ngOnInit(): void {
    this.solverUserId = this.session.getUserId();
    this.loadPending();
    this.loadAll();
  }

  setTab(tab: ActiveTab): void {
    this.activeTab = tab;
    this.actionMessage = '';
    this.actionError = '';
  }

  loadPending(): void {
    this.isLoadingPending = true;
    this.pwgService.getPendingRequestsForSolver(this.solverUserId).subscribe({
      next: (data) => { this.pendingRequests = data; this.isLoadingPending = false; },
      error: () => { this.actionError = 'Could not load pending requests.'; this.isLoadingPending = false; }
    });
  }

  loadAll(): void {
    this.isLoadingAll = true;
    this.pwgService.getAllRequestsForSolver(this.solverUserId).subscribe({
      next: (data) => { this.allRequests = data; this.isLoadingAll = false; },
      error: () => { this.actionError = 'Could not load all requests.'; this.isLoadingAll = false; }
    });
  }

  openDecision(requestId: number): void {
    this.activeDecisionId = requestId;
    this.decisionNote = '';
    this.actionMessage = '';
    this.actionError = '';
  }

  cancelDecision(): void {
    this.activeDecisionId = null;
  }

  decide(req: IPwgParticipationRequest, decision: 'Approved' | 'Rejected'): void {
    this.actionMessage = '';
    this.actionError = '';

    this.pwgService.resolvePWGRequest({
      requestId: req.requestId,
      solverUserId: this.solverUserId,
      decision,
      decisionNote: this.decisionNote || undefined
    }).subscribe({
      next: (res) => {
        if (res.success) {
          this.actionMessage = `Request from "${req.organisation?.orgName || 'Org #' + req.orgId}" ${decision}.`;
          this.activeDecisionId = null;
          // Remove from pending list, refresh all list
          this.pendingRequests = this.pendingRequests.filter(r => r.requestId !== req.requestId);
          this.loadAll();
        } else {
          this.actionError = res.message || 'Decision failed.';
        }
      },
      error: () => this.actionError = 'Could not record decision. Please try again.'
    });
  }
}
