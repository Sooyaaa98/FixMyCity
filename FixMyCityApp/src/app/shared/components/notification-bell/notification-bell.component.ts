// src/app/shared/components/notification-bell/notification-bell.component.ts
// §CHANGES:
//   1. goToComplaint() — role-based route (was hardcoded to /citizen)
//   2. goToComplaint() — calls markOneRead() + removes from local array (badge decreases)
//   3. markAllReadFromBell() — new method triggered from dropdown button
//   4. Polling unchanged (30s getUnreadNotifications)

import { Component, Input, OnInit, OnDestroy, HostListener, ElementRef } from '@angular/core';
import { Router } from '@angular/router';
import { Subject, interval } from 'rxjs';
import { switchMap, startWith, takeUntil } from 'rxjs/operators';

import { GamificationService } from '../../../fmc-services/gamification.service';
import { SessionService } from '../../../core/services/session.service';
import { INotification } from '../../../fmc-interfaces/gamification.interface';

@Component({
  selector: 'app-notification-bell',
  templateUrl: './notification-bell.component.html',
  styleUrls: ['./notification-bell.component.css']
})
export class NotificationBellComponent implements OnInit, OnDestroy {

  @Input() userId!: number;

  notifications: INotification[] = [];
  isOpen = false;
  isMarking = false;

  private destroy$ = new Subject<void>();

  constructor(
    private gamificationService: GamificationService,
    private session: SessionService,
    private router: Router,
    private elementRef: ElementRef
  ) { }

  // ── Close on outside click ────────────────────────────────────────────────
  @HostListener('document:click', ['$event'])
  onDocumentClick(event: Event): void {
    if (!this.elementRef.nativeElement.contains(event.target)) {
      this.isOpen = false;
    }
  }

  ngOnInit(): void {
    // Poll unread notifications every 30s — badge count = notifications.length
    interval(30000)
      .pipe(
        startWith(0),
        switchMap(() => this.gamificationService.getUnreadNotifications(this.userId)),
        takeUntil(this.destroy$)
      )
      .subscribe({
        next: (data) => this.notifications = data,
        error: () => { }   // silently ignore poll errors
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  // ── Derived ───────────────────────────────────────────────────────────────
  /** Badge count = number of unread notifications in local array. */
  get unreadCount(): number {
    return this.notifications.length;   // all items from getUnreadNotifications are unread
  }

  // ── Actions ───────────────────────────────────────────────────────────────
  toggleDropdown(): void {
    this.isOpen = !this.isOpen;
  }

  viewAll(): void {
    this.isOpen = false;
    this.router.navigate(['/notifications']);
  }

  /**
   * §FIX 1: Role-based navigation — no longer hardcoded to /citizen.
   * §FIX 2: Marks notification as read → removes from local array → badge decreases.
   */
  goToComplaint(notification: INotification): void {
    this.isOpen = false;

    // Optimistic update: remove from unread list immediately so badge decreases
    this.notifications = this.notifications.filter(
      n => n.notificationId !== notification.notificationId
    );

    // Fire-and-forget server update
    this.gamificationService.markOneRead(notification.notificationId).subscribe();

    // Navigate to the role-appropriate complaint view
    if (notification.complaintId) {
      this.router.navigate(this.getComplaintRoute(notification.complaintId));
    }
  }

  /**
   * §NEW: Mark all read directly from the bell dropdown.
   * Clears the local array so badge drops to 0 immediately.
   */
  markAllReadFromBell(): void {
    if (this.isMarking || this.notifications.length === 0) return;
    this.isMarking = true;

    this.gamificationService.markAllRead(this.userId).subscribe({
      next: () => {
        this.notifications = [];   // badge → 0
        this.isOpen = false;
        this.isMarking = false;
      },
      error: () => { this.isMarking = false; }
    });
  }

  // ── Private helpers ───────────────────────────────────────────────────────
  /**
   * Returns the correct complaint detail route for the current user's role.
   * PWG has no complaint detail page — sends to their list instead.
   */
  private getComplaintRoute(complaintId: number): (string | number)[] {
    switch (this.session.getRole()) {
      case 'Citizen': return ['/citizen/complaints', complaintId];
      case 'Solver': return ['/solver/complaints', complaintId];
      case 'SuperAdmin': return ['/admin/complaints', complaintId];
      case 'PWG': return ['/pwg/complaints'];       // no detail route for PWG
      default: return ['/citizen/complaints', complaintId];
    }
  }
}
