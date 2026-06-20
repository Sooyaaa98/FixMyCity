// src/app/citizen/interests/interests.component.ts
// US26: Citizen can set category + locality interests for AI recommendations.

import { Component, OnInit } from '@angular/core';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';

import { MlService } from '../../fmc-services/ml.service';
import { AuthService } from '../../fmc-services/auth.service';
import { ToastService } from '../../fmc-services/toast.service';
import { SessionService } from '../../core/services/session.service';
import { IUserInterest } from '../../fmc-interfaces/ml.interface';
import { IIssueCategory } from '../../fmc-interfaces/complaint.interface';

@Component({
  selector: 'app-interests',
  templateUrl: './interests.component.html',
  styleUrls: ['./interests.component.css']
})
export class InterestsComponent implements OnInit {

  categories: IIssueCategory[] = [];
  localities: any[] = [];
  interests: IUserInterest[] = [];

  isLoading = true;
  saving = false;

  userId = 0;

  constructor(
    private ml: MlService,
    private auth: AuthService,
    private toast: ToastService,
    private session: SessionService
  ) {}

  ngOnInit(): void {
    this.userId = this.session.getUserId();
    this.load();
  }

  load(): void {
    this.isLoading = true;

    forkJoin({
      categories: this.auth.getAllCategories().pipe(catchError(() => of([]))),
      localities: this.auth.getAllLocalities().pipe(catchError(() => of([]))),
      interests:  this.ml.getUserInterests(this.userId)
    }).subscribe({
      next: ({ categories, localities, interests }) => {
        this.categories = categories;
        this.localities = localities;
        this.interests  = interests;
        this.isLoading  = false;
      },
      error: () => { this.isLoading = false; }
    });
  }

  // ── Category interests ────────────────────────────────────────────────────

  hasCategoryInterest(categoryId: number): boolean {
    return this.interests.some(i => i.categoryId === categoryId);
  }

  toggleCategory(categoryId: number): void {
    if (this.saving) return;
    this.saving = true;

    if (this.hasCategoryInterest(categoryId)) {
      this.ml.removeUserInterest({ userId: this.userId, categoryId }).subscribe({
        next: (res) => {
          if (res.success) {
            this.interests = this.interests.filter(i => i.categoryId !== categoryId);
            this.toast.info('Category interest removed.');
          }
          this.saving = false;
        },
        error: () => { this.saving = false; }
      });
    } else {
      this.ml.addUserInterest({ userId: this.userId, categoryId }).subscribe({
        next: (res) => {
          if (res.success) {
            this.interests = [...this.interests, { interestId: 0, userId: this.userId, categoryId }];
            this.toast.success('Category interest saved.');
          }
          this.saving = false;
        },
        error: () => { this.saving = false; }
      });
    }
  }

  // ── Locality interests ────────────────────────────────────────────────────

  hasLocalityInterest(localityId: number): boolean {
    return this.interests.some(i => i.preferredLocalityId === localityId);
  }

  toggleLocality(localityId: number): void {
    if (this.saving) return;
    this.saving = true;

    if (this.hasLocalityInterest(localityId)) {
      this.ml.removeUserInterest({ userId: this.userId, preferredLocalityId: localityId }).subscribe({
        next: (res) => {
          if (res.success) {
            this.interests = this.interests.filter(i => i.preferredLocalityId !== localityId);
            this.toast.info('Locality interest removed.');
          }
          this.saving = false;
        },
        error: () => { this.saving = false; }
      });
    } else {
      this.ml.addUserInterest({ userId: this.userId, preferredLocalityId: localityId }).subscribe({
        next: (res) => {
          if (res.success) {
            this.interests = [...this.interests, { interestId: 0, userId: this.userId, preferredLocalityId: localityId }];
            this.toast.success('Locality interest saved.');
          }
          this.saving = false;
        },
        error: () => { this.saving = false; }
      });
    }
  }

  get categoryCount(): number {
    return this.interests.filter(i => i.categoryId != null).length;
  }

  get localityCount(): number {
    return this.interests.filter(i => i.preferredLocalityId != null).length;
  }
}
