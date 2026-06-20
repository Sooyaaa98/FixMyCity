// src/app/auth/register-department/register-department.component.ts

import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../fmc-services/auth.service';
import { IIssueCategory } from '../../fmc-interfaces/complaint.interface';

@Component({
  selector: 'app-register-department',
  templateUrl: './register-department.component.html',
  styleUrls: ['./register-department.component.css']
})
export class RegisterDepartmentComponent implements OnInit {

  registerForm!: FormGroup;
  categories: IIssueCategory[] = [];
  localities: any[] = [];
  isLoading = false;
  errorMessage = '';
  successMessage = '';

  constructor(private fb: FormBuilder, private authService: AuthService, private router: Router) {}

  ngOnInit(): void {
    this.registerForm = this.fb.group({
      fullName:     ['', Validators.required],
      email:        ['', [Validators.required, Validators.email]],
      password:     ['', [Validators.required, Validators.minLength(6)]],
      phone:        ['', Validators.required],
      address:      ['', Validators.required],
      localityId:   [null, Validators.required],
      deptName:     ['', Validators.required],
      ministry:     ['', Validators.required],
      categoryId:   [null, Validators.required],
      contactEmail: ['', [Validators.required, Validators.email]],
      contactPhone: ['', Validators.required]
    });
    this.authService.getAllCategories().subscribe({ next: (c) => this.categories = c });
    this.authService.getAllLocalities().subscribe({ next: (l) => this.localities = l });
  }

  onSubmit(): void {
    if (this.registerForm.invalid) { this.registerForm.markAllAsTouched(); return; }
    this.isLoading = true;
    const v = this.registerForm.value;
    this.authService.registerDepartment({ ...v, localityId: Number(v.localityId), categoryId: Number(v.categoryId) }).subscribe({
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
