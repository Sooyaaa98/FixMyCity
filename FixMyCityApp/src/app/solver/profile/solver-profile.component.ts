// src/app/solver/profile/solver-profile.component.ts
//
// Personal info (name/phone/address) + password are handled by the shared
// <app-user-profile> at the top of the template. This component owns ONLY
// the department-specific fields (deptName/ministry/contactEmail/contactPhone/
// address/localityId).

import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { PwgService } from '../../fmc-services/pwg.service';
import { SessionService } from '../../core/services/session.service';
import { IDepartment } from '../../fmc-interfaces/pwg.interface';

@Component({
  selector: 'app-solver-profile',
  templateUrl: './solver-profile.component.html',
  styleUrls: ['./solver-profile.component.css']
})
export class SolverProfileComponent implements OnInit {

  profile: IDepartment | null = null;
  profileForm!: FormGroup;
  isLoading = true;
  isSaving = false;
  successMsg = '';
  errorMsg = '';

  constructor(
    private fb: FormBuilder,
    private pwgService: PwgService,
    private session: SessionService,
  ) { }

  ngOnInit(): void {
    this.profileForm = this.fb.group({
      deptName: ['', Validators.required],
      ministry: ['', Validators.required],
      contactEmail: ['', [Validators.required, Validators.email]],
      contactPhone: ['', [Validators.required, Validators.pattern(/^[0-9]{10,15}$/)]],
      address: ['', Validators.required],
      localityId: [null, Validators.required]
    });

    // ⚠️ Pass userId — controller calls GetDeptByUserId
    this.pwgService.getDeptProfile(this.session.getUserId()).subscribe({
      next: (data) => {
        this.profile = data;
        this.isLoading = false;
        this.profileForm.patchValue({
          deptName: data.deptName,
          ministry: data.ministry,
          contactEmail: data.contactEmail,
          contactPhone: data.contactPhone,
          address: data.address,
          localityId: data.localityId
        });
      },
      error: () => {
        this.errorMsg = 'Could not load department profile.';
        this.isLoading = false;
      }
    });
  }

  onSave(): void {
    if (this.profileForm.invalid || !this.profile) return;

    this.isSaving = true;
    this.successMsg = '';
    this.errorMsg = '';

    this.pwgService.updateDeptProfile({
      deptId: this.profile.deptId,
      deptName: this.profileForm.value.deptName,
      ministry: this.profileForm.value.ministry,
      contactEmail: this.profileForm.value.contactEmail,
      contactPhone: this.profileForm.value.contactPhone,
      address: this.profileForm.value.address,
      localityId: Number(this.profileForm.value.localityId)
    }).subscribe({
      next: (res) => {
        this.isSaving = false;
        if (res.success) {
          this.successMsg = 'Department profile updated successfully.';
          this.profileForm.markAsPristine();
        } else {
          this.errorMsg = res.message || 'Update failed.';
        }
      },
      error: () => {
        this.isSaving = false;
        this.errorMsg = 'Could not save changes. Please try again.';
      }
    });
  }
}
