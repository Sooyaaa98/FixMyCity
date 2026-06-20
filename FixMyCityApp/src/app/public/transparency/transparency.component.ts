// src/app/public/transparency/transparency.component.ts
//
// Phase 8 (§17) — Public transparency portal.
//
// Anonymous read-only complaint feed served from /api/Public/*. Designed so
// citizens, journalists and oversight bodies can audit civic activity without
// creating an account.
//
// UX:
//   - Filter by locality / category / status / keyword
//   - Paginated server-side (page 1 by default, "Load more" appends)
//   - Each row shows the truncated description, the upvote count and links
//     to the (auth-gated) detail page — clicking it bumps the visitor to
//     /login with a returnUrl so they can sign up and see the full thread.

import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import {
  PublicService, IPublicFeedRow, IPublicLocality
} from '../../fmc-services/public.service';

@Component({
  selector: 'app-transparency',
  templateUrl: './transparency.component.html',
  styleUrls: ['./transparency.component.css'],
})
export class TransparencyComponent implements OnInit {

  rows: IPublicFeedRow[] = [];
  localities: IPublicLocality[] = [];
  categories: Array<{ categoryId: number; categoryName: string }> = [];

  filterLocality: number | null = null;
  filterCategory: number | null = null;
  filterStatus = '';
  keyword = '';

  pageNum = 1;
  readonly pageSize = 12;
  loading = false;
  reachedEnd = false;
  errorMsg = '';

  readonly statusOptions = ['', 'Submitted', 'In Progress', 'Resolved', 'Rejected', 'Escalated'];

  constructor(private publicSvc: PublicService, private router: Router) {}

  ngOnInit(): void {
    this.publicSvc.getLocalities().subscribe(l => this.localities = l ?? []);
    this.publicSvc.getCategories().subscribe(c => this.categories = (c ?? []) as any);
    this.load();
  }

  applyFilter(): void { this.pageNum = 1; this.reachedEnd = false; this.rows = []; this.load(); }

  clearFilter(): void {
    this.filterLocality = null;
    this.filterCategory = null;
    this.filterStatus   = '';
    this.keyword        = '';
    this.applyFilter();
  }

  load(append = false): void {
    this.loading = true;
    this.errorMsg = '';
    this.publicSvc.getFeed({
      localityId: this.filterLocality ?? undefined,
      categoryId: this.filterCategory ?? undefined,
      status:     this.filterStatus || undefined,
      keyword:    this.keyword.trim() || undefined,
      pageNum:    this.pageNum,
      pageSize:   this.pageSize,
    }).subscribe({
      next: (rows) => {
        const next = rows ?? [];
        if (next.length < this.pageSize) this.reachedEnd = true;
        this.rows = append ? this.rows.concat(next) : next;
        this.loading = false;
      },
      error: () => {
        this.errorMsg = 'Could not load the transparency feed. Please try again.';
        this.loading  = false;
      }
    });
  }

  loadMore(): void {
    if (this.loading || this.reachedEnd) return;
    this.pageNum += 1;
    this.load(true);
  }

  /** Anonymous visitors don't have a citizen route — funnel them through login. */
  viewDetails(complaintId: number): void {
    this.router.navigate(['/login'], {
      queryParams: { returnUrl: `/citizen/complaints/${complaintId}` }
    });
  }
}
