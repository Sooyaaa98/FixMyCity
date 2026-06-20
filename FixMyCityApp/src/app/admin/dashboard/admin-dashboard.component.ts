// src/app/admin/dashboard/admin-dashboard.component.ts

import { Component, OnInit, OnDestroy } from '@angular/core';
import { Router } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';

import { AdminService } from '../../fmc-services/admin.service';
import { MlService } from '../../fmc-services/ml.service';
import { ToastService } from '../../fmc-services/toast.service';
import { IPlatformStats } from '../../fmc-interfaces/gamification.interface';

@Component({
  selector: 'app-admin-dashboard',
  templateUrl: './admin-dashboard.component.html',
  styleUrls: ['./admin-dashboard.component.css']
})
export class AdminDashboardComponent implements OnInit, OnDestroy {

  stats: IPlatformStats | null = null;
  isLoading = true;
  errorMessage = '';

  // Phase 8 (§9) Trend chart data
  trendRows: Array<{ date: string; count: number; resolved: number }> = [];
  trendDays: 7 | 30 | 90 = 30;

  // AI health
  aiOnline = false;
  aiChecking = false;

  // Stat cards config
  statCards: Array<{ label: string; value: () => number; icon: string; colorClass: string; route?: string; queryParam?: string }> = [];

  private refreshInterval: any;

  constructor(
    private adminService: AdminService,
    private ml: MlService,
    private toast: ToastService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.load();
    this.checkAIHealth();
    this.loadTrend();
    // Auto-refresh stats every 60s
    this.refreshInterval = setInterval(() => this.load(), 60000);
  }

  /** Phase 8 (§9) — pulls the daily count + resolved series from
   *  /api/Admin/GetComplaintTrend and feeds the SVG chart. */
  loadTrend(days?: 7 | 30 | 90): void {
    if (days) this.trendDays = days;
    this.adminService.getComplaintTrend(this.trendDays).subscribe({
      next: (rows) => { this.trendRows = (rows ?? []) as any; },
      error: () => { /* non-critical */ }
    });
  }

  ngOnDestroy(): void {
    clearInterval(this.refreshInterval);
  }

  load(): void {
    this.isLoading = true;
    this.adminService.getPlatformStats().subscribe({
      next: (data) => {
        this.stats = data;
        this.isLoading = false;
        this.buildStatCards();
      },
      error: () => {
        this.errorMessage = 'Could not load platform statistics.';
        this.isLoading = false;
      }
    });
  }

  private buildStatCards(): void {
    if (!this.stats) return;
    const s = this.stats;
    this.statCards = [
      { label: 'Total Complaints', value: () => s.totalComplaints, icon: 'bi bi-card-list',       colorClass: 'stat--primary',  route: '/admin/complaints', queryParam: '' },
      { label: 'Submitted',        value: () => s.submitted,       icon: 'bi bi-inbox',           colorClass: 'stat--warning',  route: '/admin/complaints', queryParam: 'Submitted' },
      { label: 'In Progress',      value: () => s.inProgress,      icon: 'bi bi-gear-fill',             colorClass: 'stat--info',     route: '/admin/complaints', queryParam: 'In Progress' },
      { label: 'Resolved',         value: () => s.resolved,        icon: 'bi bi-check-circle-fill',    colorClass: 'stat--success',  route: '/admin/complaints', queryParam: 'Resolved' },
      { label: 'Escalated',        value: () => s.escalated,       icon: 'bi bi-exclamation-triangle-fill',     colorClass: 'stat--danger',   route: '/admin/escalated' },
      { label: 'Rejected',         value: () => s.rejected,        icon: 'bi bi-x-circle-fill',    colorClass: 'stat--danger' },
      { label: 'Active Citizens',  value: () => s.totalCitizens,   icon: 'bi bi-people-fill',           colorClass: 'stat--primary' },
      { label: 'Departments',      value: () => s.totalSolvers,    icon: 'bi bi-building',        colorClass: 'stat--info' },
      { label: 'PWG Orgs',         value: () => s.totalPWG,        icon: 'bi bi-people-fill',           colorClass: 'stat--success' },
      { label: 'Linked Dupes',     value: () => s.linked,          icon: 'bi bi-link-45deg',            colorClass: 'stat--warning' },
    ];
  }

  navigateCard(card: any): void {
    if (!card.route) return;
    if (card.queryParam != null && card.queryParam !== '') {
      this.router.navigate([card.route], { queryParams: { status: card.queryParam } });
    } else {
      this.router.navigate([card.route]);
    }
  }

  // ── AI Health ─────────────────────────────────────────────────────────────

  checkAIHealth(): void {
    this.aiChecking = true;
    this.ml.checkAIHealth().subscribe({
      next: (res) => { this.aiOnline = res.aiServiceOnline; this.aiChecking = false; },
      error: () => { this.aiOnline = false; this.aiChecking = false; }
    });
  }

}
