// src/app/shared/components/user-profile/user-profile.component.ts

import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { AuthService } from '../../../fmc-services/auth.service';
import { ToastService } from '../../../fmc-services/toast.service';
import { SessionService } from '../../../core/services/session.service';

interface IProfileView {
  userId: number;
  fullName: string;
  email: string;
  phone: string;
  address: string;
  localityId: number;
  localityName?: string;
  isActive: boolean;
  isApproved: boolean;
  isBanned: boolean;
  roleId: number;
  roleName: string;
  orgId?: number;
  deptId?: number;
  points?: number;
}

@Component({
  selector: 'app-user-profile',
  templateUrl: './user-profile.component.html',
  styleUrls: ['./user-profile.component.css']
})
export class UserProfileComponent implements OnInit {

  profile: IProfileView | null = null;
  localities: any[] = [];
  profileForm!: FormGroup;
  passwordForm!: FormGroup;

  isEditing = false;
  isLoading = true;
  isSaving = false;
  isChangingPassword = false;
  showCurrentPassword = false;
  showNewPassword = false;
  showConfirmPassword = false;

  constructor(
    private fb: FormBuilder,
    private authService: AuthService,
    private toast: ToastService,
    private session: SessionService
  ) {}

  ngOnInit(): void {
    this.buildForms();
    this.authService.getAllLocalities().subscribe({ next: (l) => this.localities = l });
    this.loadProfile();
  }

  private buildForms(): void {
    this.profileForm = this.fb.group({
      fullName:   ['', [Validators.required, Validators.minLength(2)]],
      email:      [{ value: '', disabled: true }],
      phone:      ['', Validators.required],
      address:    ['', Validators.required],
      localityId: [null, Validators.required],
      roleName:   [{ value: '', disabled: true }]
    });
    this.profileForm.disable();

    this.passwordForm = this.fb.group({
      currentPassword: ['', Validators.required],
      newPassword:     ['', [Validators.required, Validators.minLength(6)]],
      confirmPassword: ['', Validators.required]
    }, {
      validators: (g: FormGroup) =>
        g.get('newPassword')?.value === g.get('confirmPassword')?.value
          ? null : { mismatch: true }
    });
  }

  loadProfile(): void {
    this.isLoading = true;
    this.authService.getUserById(this.session.getUserId()).subscribe({
      next: (raw: any) => {
        // Backend returns PascalCase — normalise defensively
        this.profile = {
          userId:       raw.UserId      ?? raw.userId      ?? 0,
          fullName:     raw.FullName    ?? raw.fullName    ?? '',
          email:        raw.Email       ?? raw.email       ?? '',
          phone:        raw.Phone       ?? raw.phone       ?? '',
          address:      raw.Address     ?? raw.address     ?? '',
          localityId:   raw.LocalityId  ?? raw.localityId  ?? 0,
          localityName: raw.LocalityName ?? raw.localityName,
          isActive:     raw.IsActive    ?? raw.isActive    ?? true,
          isApproved:   raw.IsApproved  ?? raw.isApproved  ?? true,
          isBanned:     raw.IsBanned    ?? raw.isBanned    ?? false,
          roleId:       raw.RoleId      ?? raw.roleId      ?? 0,
          roleName:     raw.RoleName    ?? raw.roleName    ?? '',
          orgId:        raw.OrgId       ?? raw.orgId,
          deptId:       raw.DeptId      ?? raw.deptId,
          points:       raw.Points      ?? raw.points      ?? 0,
        };
        this.profileForm.patchValue({
          fullName:   this.profile.fullName,
          email:      this.profile.email,
          phone:      this.profile.phone,
          address:    this.profile.address,
          localityId: this.profile.localityId,
          roleName:   this.profile.roleName
        });
        this.isLoading = false;
      },
      error: () => {
        this.toast.error('Could not load profile.');
        this.isLoading = false;
      }
    });
  }

  startEdit(): void {
    this.profileForm.enable();
    this.profileForm.get('email')?.disable();
    this.profileForm.get('roleName')?.disable();
    this.isEditing = true;
  }

  cancelEdit(): void {
    this.profileForm.disable();
    this.isEditing = false;
    if (this.profile) {
      this.profileForm.patchValue({
        fullName: this.profile.fullName,
        phone:    this.profile.phone,
        address:  this.profile.address,
        localityId: this.profile.localityId
      });
    }
  }

  saveProfile(): void {
    if (this.profileForm.invalid) return;
    this.isSaving = true;
    const v = this.profileForm.getRawValue();
    this.authService.updateProfile({
      userId:     this.session.getUserId(),
      fullName:   v.fullName,
      phone:      v.phone,
      address:    v.address,
      localityId: Number(v.localityId)
    }).subscribe({
      next: (res) => {
        this.isSaving = false;
        if (res.success) {
          this.toast.success('Profile updated successfully.');
          this.isEditing = false;
          this.profileForm.disable();
          this.loadProfile();
        } else {
          this.toast.error(res.message ?? 'Update failed.');
        }
      },
      error: () => { this.isSaving = false; }
    });
  }

  savePassword(): void {
    if (this.passwordForm.invalid || this.passwordForm.errors?.['mismatch']) return;
    const v = this.passwordForm.value;
    this.authService.changePassword({
      userId:          this.session.getUserId(),
      currentPassword: v.currentPassword,
      newPassword:     v.newPassword
    }).subscribe({
      next: (res) => {
        if (res.success) {
          this.toast.success('Password changed successfully.');
          this.isChangingPassword = false;
          this.passwordForm.reset();
        } else {
          this.toast.error(res.message ?? 'Password change failed.');
        }
      }
    });
  }
}
