// src/app/auth/register-citizen/register-citizen.component.ts

import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../fmc-services/auth.service';

@Component({
  selector: 'app-register-citizen',
  templateUrl: './register-citizen.component.html',
  styleUrls: ['./register-citizen.component.css']
})
export class RegisterCitizenComponent implements OnInit {

  registerForm!: FormGroup;
  localities: any[] = [];
  isLoading = false;
  errorMessage = '';
  successMessage = '';

  constructor(
    private fb: FormBuilder,
    private authService: AuthService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.registerForm = this.fb.group({
      fullName:   ['', [Validators.required, Validators.minLength(2)]],
      email:      ['', [Validators.required, Validators.email]],
      password:   ['', [Validators.required, Validators.minLength(6)]],
      // 10-digit phone (matches `chk_Users_Phone CHECK (LEN(Phone) >= 10)` in the schema;
      // 10–15 digits to allow country codes if a user types e.g. 911234567890).
      phone:      ['', [Validators.required, Validators.pattern(/^[0-9]{10,15}$/)]],
      address:    ['', [Validators.required, Validators.minLength(5)]],
      localityId: [null, Validators.required],
      // 12-digit Aadhaar (matches `chk_Users_Aadhaar CHECK (LEN(AadhaarNo) = 12)`)
      aadhaarNo:  ['', [Validators.required, Validators.pattern(/^[0-9]{12}$/)]]
    });

    this.authService.getAllLocalities().subscribe({
      next: (locs) => this.localities = locs,
      error: () => this.errorMessage = 'Could not load localities. Please refresh.'
    });
  }

  onSubmit(): void {
    if (this.registerForm.invalid) { this.registerForm.markAllAsTouched(); return; }
    this.isLoading = true;
    this.errorMessage = '';
    this.successMessage = '';

    const v = this.registerForm.value;
    this.authService.registerCitizen({ ...v, localityId: Number(v.localityId) }).subscribe({
      next: (res) => {
        this.isLoading = false;
        if (res.success) {
          this.successMessage = 'Registration successful! Redirecting to login…';
          setTimeout(() => this.router.navigate(['/login']), 1800);
        } else {
          this.errorMessage = res.message || 'Registration failed. Please try again.';
        }
      },
      error: () => { this.isLoading = false; this.errorMessage = 'Unable to reach server. Please try again.'; }
    });
  }

  // ── Demo SSO sign-up / sign-in ────────────────────────────────────────────
  // For demo purposes this generates a stable synthetic Google identity for a
  // single browser. In production swap the prompt for a real Google OAuth flow
  // (the backend usp_SSOLoginOrCreate already handles "existing SSO user",
  // "link SSO to existing email", and "create new citizen via SSO").
  signUpWithGoogle(): void {
    const email = (window.prompt(
      'Demo SSO — enter the email address Google would have returned:',
      'demo.user@example.com'
    ) || '').trim();
    if (!email) return;

    const fullName = (window.prompt(
      'Demo SSO — enter your full name:',
      'Demo User'
    ) || '').trim() || 'Demo User';

    // Stable per-email external id — replays don't create duplicates
    const ssoExternalId = `google-demo-${btoa(email).replace(/=+$/, '')}`;

    this.isLoading = true;
    this.errorMessage = '';
    this.successMessage = '';

    this.authService.ssoLogin({
      ssoProvider: 'Google',
      ssoExternalId,
      email,
      fullName,
    }).subscribe({
      next: (res) => {
        this.isLoading = false;
        if (res.success) {
          this.successMessage = 'SSO sign-in successful — redirecting…';
          setTimeout(() => this.router.navigate(['/citizen/home']), 1200);
        } else {
          this.errorMessage = res.message || 'SSO sign-in failed.';
        }
      },
      error: () => {
        this.isLoading = false;
        this.errorMessage = 'Unable to reach server for SSO. Please try again.';
      }
    });
  }
}
