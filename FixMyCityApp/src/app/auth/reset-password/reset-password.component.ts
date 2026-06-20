// src/app/auth/reset-password/reset-password.component.ts
//
// Phase 8 (§18) — Step 2 of the reset flow.
//
// Reads the `?token=…` query string, verifies it via /VerifyResetToken
// (cheap pre-flight so we can fail fast on stale links), then lets the
// citizen submit a new password.

import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { ToastService } from '../../fmc-services/toast.service';

@Component({
  selector: 'app-reset-password',
  templateUrl: './reset-password.component.html',
  styleUrls: ['./reset-password.component.css']
})
export class ResetPasswordComponent implements OnInit {

  form: FormGroup;
  tokenValid: boolean | null = null;   // null = pending, true/false after verify
  tokenChecking = false;
  submitting = false;
  done = false;

  constructor(
    private fb: FormBuilder,
    private http: HttpClient,
    private route: ActivatedRoute,
    private router: Router,
    private toast: ToastService,
  ) {
    this.form = this.fb.group({
      newPassword: ['', [Validators.required, Validators.minLength(8)]],
      confirm:     ['', [Validators.required]],
    }, { validators: this.matchValidator });
  }

  ngOnInit(): void {
    const token = this.route.snapshot.queryParamMap.get('token') ?? '';
    if (!token) { this.tokenValid = false; return; }
    this.tokenChecking = true;
    const headers = new HttpHeaders({ 'X-Public': 'true' });
    this.http.post<any>(`${environment.apiBaseUrl}/api/Auth/VerifyResetToken`,
      { token, newPassword: '' }, { headers }).subscribe({
      next: (r) => {
        this.tokenValid = !!r?.valid;
        this.tokenChecking = false;
      },
      error: () => {
        this.tokenValid = false;
        this.tokenChecking = false;
      }
    });
  }

  submit(): void {
    const token = this.route.snapshot.queryParamMap.get('token') ?? '';
    if (this.form.invalid || !token) {
      this.form.markAllAsTouched();
      return;
    }
    this.submitting = true;
    const headers = new HttpHeaders({ 'X-Public': 'true' });
    this.http.post<any>(`${environment.apiBaseUrl}/api/Auth/ResetPassword`, {
      token,
      newPassword: this.form.value.newPassword,
    }, { headers }).subscribe({
      next: (r) => {
        this.submitting = false;
        if (r?.success) {
          this.done = true;
          this.toast.success('Password updated. You can now sign in.');
          setTimeout(() => this.router.navigate(['/login']), 1500);
        } else {
          this.toast.error(r?.message ?? 'Reset failed. Please request a new link.');
        }
      },
      error: () => {
        this.submitting = false;
        this.toast.error('Reset failed. Please request a new link.');
      }
    });
  }

  private matchValidator(g: any): any {
    const p = g.get('newPassword')?.value;
    const c = g.get('confirm')?.value;
    return p && c && p !== c ? { mismatch: true } : null;
  }
}
