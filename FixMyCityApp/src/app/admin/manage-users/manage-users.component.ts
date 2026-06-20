// src/app/admin/manage-users/manage-users.component.ts

import { Component, OnInit } from '@angular/core';
import { AdminService } from '../../fmc-services/admin.service';
import { ToastService } from '../../fmc-services/toast.service';
import { SessionService } from '../../core/services/session.service';
import { IUserProfile } from '../../fmc-interfaces/user.interface';

type RoleTab   = 'Citizen' | 'Solver' | 'PWG';
type ModalMode = 'deactivate' | 'ban' | 'reactivate';

@Component({
  selector: 'app-manage-users',
  templateUrl: './manage-users.component.html',
  styleUrls: ['./manage-users.component.css']
})
export class ManageUsersComponent implements OnInit {

  activeRole: RoleTab = 'Citizen';
  users: IUserProfile[] = [];
  isLoading = false;
  readonly roleTabs: RoleTab[] = ['Citizen', 'Solver', 'PWG'];
  private adminId = 0;

  // ── Search ────────────────────────────────────────────────────────────────
  searchQuery = '';

  get filteredUsers(): IUserProfile[] {
    const q = this.searchQuery.trim().toLowerCase();
    if (!q) return this.users;
    return this.users.filter(u =>
      String(u.userId).includes(q) ||
      u.fullName.toLowerCase().includes(q) ||
      u.email.toLowerCase().includes(q)
    );
  }

  // ── Action Modal ──────────────────────────────────────────────────────────
  showModal    = false;
  modalMode: ModalMode | null = null;
  modalUser: IUserProfile | null = null;
  modalReason  = '';
  modalBusy    = false;

  get modalTitle(): string {
    switch (this.modalMode) {
      case 'deactivate': return `Deactivate "${this.modalUser?.fullName}"`;
      case 'ban':        return `Ban "${this.modalUser?.fullName}"`;
      case 'reactivate': return `Reactivate "${this.modalUser?.fullName}"`;
      default:           return '';
    }
  }

  get modalDescription(): string {
    switch (this.modalMode) {
      case 'deactivate': return 'This will prevent the user from logging in. You can reactivate them later.';
      case 'ban':        return 'This permanently blocks the account. The user will be marked as banned and cannot log in.';
      case 'reactivate': return 'This will restore full access to the account, clearing any deactivation or ban.';
      default:           return '';
    }
  }

  get modalConfirmClass(): string {
    return this.modalMode === 'ban' ? 'btn-danger' : 'btn-primary';
  }

  constructor(
    private adminService: AdminService,
    private toast: ToastService,
    private session: SessionService
  ) {}

  ngOnInit(): void {
    this.adminId = this.session.getUserId();
    this.loadUsers();
  }

  setRole(role: RoleTab): void {
    this.activeRole = role;
    this.searchQuery = '';
    this.loadUsers();
  }

  loadUsers(): void {
    this.isLoading = true;
    this.adminService.getUsersByRole(this.activeRole).subscribe({
      next: (data) => { this.users = data; this.isLoading = false; },
      error: () => { this.isLoading = false; this.toast.error('Could not load users.'); }
    });
  }

  // ── Open modal ────────────────────────────────────────────────────────────

  openDeactivate(user: IUserProfile): void {
    this.modalUser   = user;
    this.modalMode   = 'deactivate';
    this.modalReason = '';
    this.showModal   = true;
  }

  openBan(user: IUserProfile): void {
    this.modalUser   = user;
    this.modalMode   = 'ban';
    this.modalReason = '';
    this.showModal   = true;
  }

  openReactivate(user: IUserProfile): void {
    this.modalUser   = user;
    this.modalMode   = 'reactivate';
    this.modalReason = '';
    this.showModal   = true;
  }

  closeModal(): void {
    this.showModal  = false;
    this.modalUser  = null;
    this.modalMode  = null;
    this.modalBusy  = false;
  }

  // ── Confirm action ────────────────────────────────────────────────────────

  confirmAction(): void {
    if (!this.modalUser || !this.modalMode) return;
    if (!this.modalReason.trim()) {
      this.toast.warning('Please provide a reason.');
      return;
    }

    this.modalBusy = true;
    const payload = {
      targetUserId: this.modalUser.userId,
      reason:       this.modalReason,
      adminUserId:  this.adminId,
    };

    const obs$ =
      this.modalMode === 'deactivate' ? this.adminService.deactivateUser(payload) :
      this.modalMode === 'ban'        ? this.adminService.banUser(payload)        :
                                        this.adminService.reactivateUser(payload);

    const successMsg =
      this.modalMode === 'deactivate' ? `${this.modalUser.fullName} deactivated.`  :
      this.modalMode === 'ban'        ? `${this.modalUser.fullName} banned.`        :
                                        `${this.modalUser.fullName} reactivated.`;

    obs$.subscribe({
      next: (res) => {
        this.modalBusy = false;
        if (res.success) {
          this.toast.success(successMsg);
          this.closeModal();
          this.loadUsers();
        } else {
          this.toast.error((res as any).message ?? 'Action failed.');
        }
      },
      error: () => {
        this.modalBusy = false;
        this.toast.error('Server error — please try again.');
      }
    });
  }

  // ── Helpers ───────────────────────────────────────────────────────────────

  userStatusLabel(u: IUserProfile): string {
    if (u.isBanned)  return 'Banned';
    if (!u.isActive) return 'Inactive';
    return 'Active';
  }

  userStatusClass(u: IUserProfile): string {
    if (u.isBanned)  return 'badge-Rejected';
    if (!u.isActive) return 'badge-Pending';
    return 'badge-Resolved';
  }
}
