// src/app/auth/forgot-password/forgot-password.component.ts
//
// Phase 8 (§18) — Step 1 of the reset flow.
// Citizen enters their email; backend returns success regardless of whether
// the email exists (anti-enumeration). In dev the response also returns the
// raw token so QA can use the reset link without SMTP.

import { Component } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { ToastService } from '../../fmc-services/toast.service';
import { Router } from '@angular/router';

@Component({
  selector: 'app-forgot-password',
  templateUrl: './forgot-password.component.html',
  styleUrls: ['./forgot-password.component.css']
})
export class ForgotPasswordComponent {

  form: FormGroup;
  submitting = false;
  submitted = false;
  devToken: string | null = null;

  constructor(
    private fb: FormBuilder,
    private http: HttpClient,
    private toast: ToastService,
    private router: Router,
  ) {
    this.form = this.fb.group({
      email: ['', [Validators.required, Validators.email]],
    });
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.submitting = true;
    // X-Public so the AuthInterceptor doesn't attach a Bearer token.
    const headers = new HttpHeaders({ 'X-Public': 'true' });
    this.http.post<any>(`${environment.apiBaseUrl}/api/Auth/ForgotPassword`,
      { email: this.form.value.email }, { headers }).subscribe({
      next: (res) => {
        this.submitting = false;
        this.submitted  = true;
        // Dev surface for testing — see AuthController.ForgotPassword.
        if (res?.devToken) this.devToken = res.devToken;
        this.toast.success('If an account matches, a reset link has been sent.');
      },
      error: () => {
        this.submitting = false;
        // We still claim success to avoid leaking failure-vs-no-match.
        this.submitted  = true;
        this.toast.info('If an account matches, a reset link has been sent.');
      }
    });
  }

  /** Convenience for QA: jump straight to /reset-password with the token. */
  copyDevReset(): void {
    if (!this.devToken) return;
    this.router.navigate(['/reset-password'], { queryParams: { token: this.devToken } });
  }
}
