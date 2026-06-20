// src/app/pwg/profile/pwg-profile.component.ts
//
// Personal info (name/phone/address) + password are handled by the shared
// <app-user-profile> at the top of the template. This component owns ONLY
// the organisation-specific fields (orgName/contactEmail/contactPhone/address).

import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { PwgService } from '../../fmc-services/pwg.service';
import { SessionService } from '../../core/services/session.service';
import { IOrganisation } from '../../fmc-interfaces/pwg.interface';

@Component({
  selector: 'app-pwg-profile',
  templateUrl: './pwg-profile.component.html',
  styleUrls: ['./pwg-profile.component.css']
})
export class PwgProfileComponent implements OnInit {

  profile: IOrganisation | null = null;
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
      orgName: ['', Validators.required],
      contactEmail: ['', [Validators.required, Validators.email]],
      contactPhone: ['', [Validators.required, Validators.pattern(/^[0-9]{10,15}$/)]],
      address: ['', Validators.required]
    });

    // ⚠️ Pass userId — controller calls GetOrgByUserId
    this.pwgService.getOrgProfile(this.session.getUserId()).subscribe({
      next: (data) => {
        this.profile = data;
        this.isLoading = false;
        this.profileForm.patchValue({
          orgName: data.orgName,
          contactEmail: data.contactEmail,
          contactPhone: data.contactPhone,
          address: data.address
        });
      },
      error: () => {
        this.errorMsg = 'Could not load organisation profile.';
        this.isLoading = false;
      }
    });
  }

  onSave(): void {
    if (this.profileForm.invalid || !this.profile) return;

    this.isSaving = true;
    this.successMsg = '';
    this.errorMsg = '';

    this.pwgService.updateOrgProfile({
      orgId: this.profile.orgId,
      orgName: this.profileForm.value.orgName,
      contactEmail: this.profileForm.value.contactEmail,
      contactPhone: this.profileForm.value.contactPhone,
      address: this.profileForm.value.address
    }).subscribe({
      next: (res) => {
        this.isSaving = false;
        if (res.success) {
          this.successMsg = 'Organisation profile updated successfully.';
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
