// src/app/pwg/progress-update/progress-update.component.ts

import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { PwgService } from '../../fmc-services/pwg.service';
import { SessionService } from '../../core/services/session.service';

@Component({
  selector: 'app-progress-update',
  templateUrl: './progress-update.component.html',
  styleUrls: ['./progress-update.component.css']
})
export class ProgressUpdateComponent implements OnInit {

  progressForm!: FormGroup;
  complaintId!: number;
  isLoading = false;
  successMsg = '';
  errorMsg = '';

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private fb: FormBuilder,
    private pwgService: PwgService,
    private session: SessionService
  ) { }

  ngOnInit(): void {
    this.complaintId = Number(this.route.snapshot.paramMap.get('complaintId'));

    this.progressForm = this.fb.group({
      progressNote: ['', Validators.required],
      photoPath: ['']             // §10 Rec #10: plain text path for now
    });
  }

  onSubmit(): void {
    if (this.progressForm.invalid) return;

    this.isLoading = true;
    this.successMsg = '';
    this.errorMsg = '';

    this.pwgService.progressUpdate({
      complaintId: this.complaintId,
      pwgUserId: this.session.getUserId(),   // ⚠️ maps to PWGUserId in backend
      progressNote: this.progressForm.value.progressNote,
      photoPath: this.progressForm.value.photoPath || undefined
    }).subscribe({
      next: (res) => {
        this.isLoading = false;
        if (res.success) {
          this.successMsg = 'Progress update posted successfully!';
          setTimeout(() => this.router.navigate(['/pwg/requests']), 1800);
        } else {
          this.errorMsg = res.message || 'Update failed. Please try again.';
        }
      },
      error: () => {
        this.isLoading = false;
        this.errorMsg = 'Could not reach server. Please try again.';
      }
    });
  }

  goBack(): void {
    this.router.navigate(['/pwg/requests']);
  }
}
