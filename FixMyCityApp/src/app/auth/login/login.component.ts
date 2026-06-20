// src/app/auth/login/login.component.ts

import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../fmc-services/auth.service';
import { SessionService } from '../../core/services/session.service';

@Component({
  selector: 'app-login',
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.css']
})
export class LoginComponent implements OnInit {

  loginForm!: FormGroup;
  isLoading = false;
  errorMessage = '';
  showPassword = false;

  constructor(
    private fb: FormBuilder,
    private authService: AuthService,
    private session: SessionService,
    private router: Router
  ) { }

  ngOnInit(): void {
    // §FIX: If already authenticated, skip the login page entirely.
    // Prevents the "logo goes to /login" UX problem when opening a fresh tab.
    if (this.session.isLoggedIn()) {
      this.navigateByRole(this.session.getRole());
      return;
    }

    this.loginForm = this.fb.group({
      email: ['', [Validators.required, Validators.email]],
      password: ['', Validators.required]
    });
  }

  onSubmit(): void {
    if (this.loginForm.invalid) return;

    this.isLoading = true;
    this.errorMessage = '';

    this.authService.login(this.loginForm.value).subscribe({
      next: (res) => {
        this.isLoading = false;
        if (res.success) {
          // AuthService.login already saved the session via tap().
          // The API returns roleName under res.user (camelCase nested).
          // Fall back to the flat property for backward compatibility with any
          // future API shape change, then to the saved session as last resort.
          const roleName = res.user?.roleName ?? res.roleName ?? this.session.getRole();
          this.navigateByRole(roleName);
        } else {
          this.errorMessage = res.message || 'Invalid credentials. Please try again.';
        }
      },
      error: () => {
        this.isLoading = false;
        this.errorMessage = 'Unable to reach server. Please try again.';
      }
    });
  }

  private navigateByRole(roleName: string): void {
    switch (roleName) {
      case 'SuperAdmin': this.router.navigate(['/admin/dashboard']); break;
      case 'Citizen': this.router.navigate(['/citizen/home']); break;
      case 'Solver': this.router.navigate(['/solver/dashboard']); break;
      case 'PWG': this.router.navigate(['/pwg/complaints']); break;
      default: this.router.navigate(['/login']);
    }
  }

  // ── Demo SSO sign-in ──────────────────────────────────────────────────────
  // Posts to /api/Auth/SSOLogin. The backend either finds an existing SSO user,
  // links SSO to an existing email, or creates a fresh citizen via
  // usp_SSOLoginOrCreate. Real OAuth flows go here in production.
  signInWithGoogle(): void {
    const email = (window.prompt(
      'Demo SSO — enter the email address Google would have returned:',
      'demo.user@example.com'
    ) || '').trim();
    if (!email) return;

    const fullName = (window.prompt(
      'Demo SSO — enter your full name:',
      'Demo User'
    ) || '').trim() || 'Demo User';

    const ssoExternalId = `google-demo-${btoa(email).replace(/=+$/, '')}`;

    this.isLoading = true;
    this.errorMessage = '';

    this.authService.ssoLogin({
      ssoProvider: 'Google',
      ssoExternalId,
      email,
      fullName,
    }).subscribe({
      next: (res) => {
        this.isLoading = false;
        if (res.success) {
          const roleName = res.user?.roleName ?? res.roleName ?? this.session.getRole();
          this.navigateByRole(roleName);
        } else {
          this.errorMessage = res.message || 'SSO sign-in failed.';
        }
      },
      error: () => {
        this.isLoading = false;
        this.errorMessage = 'Unable to reach server for SSO.';
      }
    });
  }
}
