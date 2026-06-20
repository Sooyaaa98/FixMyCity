// src/app/auth/register-organisation/register-organisation.component.ts

import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../fmc-services/auth.service';

@Component({
  selector: 'app-register-organisation',
  templateUrl: './register-organisation.component.html',
  styleUrls: ['./register-organisation.component.css']
})
export class RegisterOrganisationComponent implements OnInit {

  registerForm!: FormGroup;
  localities: any[] = [];
  isLoading = false;
  errorMessage = '';
  successMessage = '';
  // Must match the DB chk_Organisations_OrgType CHECK constraint
  // (00_Schema_Sprint2.sql) and 04_DB_Patch.sql expansion. Anything outside
  // this list fails the DB check, the SP catches the SqlException, and the
  // repository returns 0 — registration would silently no-op.
  readonly orgTypes = [
    'NGO',
    'Student Group',
    'CSR',
    'Welfare Group',
    'Community Association',
    'Other',
  ];

  constructor(private fb: FormBuilder, private authService: AuthService, private router: Router) {}

  ngOnInit(): void {
    this.registerForm = this.fb.group({
      fullName:       ['', [Validators.required, Validators.minLength(2)]],
      email:          ['', [Validators.required, Validators.email]],
      password:       ['', [Validators.required, Validators.minLength(6)]],
      // chk_Users_Phone CHECK (LEN(Phone) >= 10)
      phone:          ['', [Validators.required, Validators.pattern(/^[0-9]{10,15}$/)]],
      address:        ['', [Validators.required, Validators.minLength(5)]],
      localityId:     [null, Validators.required],
      orgName:        ['', [Validators.required, Validators.minLength(2)]],
      orgType:        ['', Validators.required],
      registrationNo: ['', Validators.required],
      contactEmail:   ['', [Validators.required, Validators.email]],
      contactPhone:   ['', [Validators.required, Validators.pattern(/^[0-9]{10,15}$/)]]
    });
    this.authService.getAllLocalities().subscribe({ next: (l) => this.localities = l });
  }

  onSubmit(): void {
    if (this.registerForm.invalid) { this.registerForm.markAllAsTouched(); return; }
    this.isLoading = true;
    const v = this.registerForm.value;
    this.authService.registerOrganisation({ ...v, localityId: Number(v.localityId) }).subscribe({
      next: (res) => {
        this.isLoading = false;
        if (res.success) {
          this.successMessage = 'Registration submitted! Awaiting admin approval.';
          setTimeout(() => this.router.navigate(['/login']), 2000);
        } else {
          this.errorMessage = res.message || 'Registration failed.';
        }
      },
      error: () => { this.isLoading = false; this.errorMessage = 'Server error. Try again.'; }
    });
  }
}
