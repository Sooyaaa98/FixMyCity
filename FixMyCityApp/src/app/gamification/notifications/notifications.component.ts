// src/app/gamification/notifications/notifications.component.ts

import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { GamificationService } from '../../fmc-services/gamification.service';
import { ToastService } from '../../fmc-services/toast.service';
import { SessionService } from '../../core/services/session.service';
import { INotification } from '../../fmc-interfaces/gamification.interface';

@Component({
  selector: 'app-notifications',
  templateUrl: './notifications.component.html',
  styleUrls: ['./notifications.component.css']
})
export class NotificationsComponent implements OnInit {

  notifications: INotification[] = [];
  isLoading = true;
  isMarking = false;

  private userId!: number;
  private role!: string;

  constructor(
    private gamificationService: GamificationService,
    private toast: ToastService,
    private session: SessionService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.userId = this.session.getUserId();
    this.role   = this.session.getRole();
    this.load();
  }

  load(): void {
    this.isLoading = true;
    this.gamificationService.getAllNotifications(this.userId).subscribe({
      next: (data) => {
        this.notifications = data.sort(
          (a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime()
        );
        this.isLoading = false;
      },
      error: () => { this.isLoading = false; }
    });
  }

  markAllRead(): void {
    if (this.isMarking) return;
    this.isMarking = true;

    this.gamificationService.markAllRead(this.userId).subscribe({
      next: (res) => {
        if (res.success) {
          this.notifications = this.notifications.map(n => ({ ...n, isRead: true }));
          this.toast.success('All notifications marked as read.');
        }
        this.isMarking = false;
      },
      error: () => { this.isMarking = false; }
    });
  }

  onNotificationClick(n: INotification): void {
    // Optimistic local mark
    if (!n.isRead) {
      n.isRead = true;
      this.gamificationService.markOneRead(n.notificationId).subscribe();
    }
    if (n.complaintId) {
      this.router.navigate(this.complaintRoute(n.complaintId));
    }
  }

  relativeTime(dateStr: string): string {
    const diff = Date.now() - new Date(dateStr).getTime();
    const mins = Math.floor(diff / 60000);
    if (mins < 1)   return 'just now';
    if (mins < 60)  return `${mins}m ago`;
    const hrs = Math.floor(mins / 60);
    if (hrs < 24)   return `${hrs}h ago`;
    const days = Math.floor(hrs / 24);
    if (days < 30)  return `${days}d ago`;
    return new Date(dateStr).toLocaleDateString('en-GB', { day: 'numeric', month: 'short' });
  }

  get unreadCount(): number {
    return this.notifications.filter(n => !n.isRead).length;
  }

  trackById(_: number, n: INotification): number { return n.notificationId; }

  private complaintRoute(complaintId: number): (string | number)[] {
    switch (this.role) {
      case 'Citizen':    return ['/citizen/complaints', complaintId];
      case 'Solver':     return ['/solver/complaints', complaintId];
      case 'SuperAdmin': return ['/admin/complaints', complaintId];
      default:           return ['/citizen/complaints', complaintId];
    }
  }
}
