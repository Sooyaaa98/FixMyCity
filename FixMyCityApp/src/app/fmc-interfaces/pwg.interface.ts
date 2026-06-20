// src/app/fmc-interfaces/pwg.interface.ts

import { IComplaint } from './complaint.interface';

// ── Department (full shape — Admin + Solver) ──────────────────────────────
export interface IDepartment {
  deptId: number;
  userId: number;
  deptName: string;
  ministry: string;
  categoryId: number;
  contactEmail: string;
  contactPhone: string;
  address: string;
  localityId: number;
  approvalStatus: string;       // 'Pending' | 'Approved' | 'Rejected'
  approvedAt?: string;
  createdAt: string;
  updatedAt: string;
  user?: {
    userId: number;
    fullName: string;
    email: string;
    phone: string;
    isActive: boolean;
  };
  locality?: {
    localityId: number;
    localityName: string;
    city?: string;
    state?: string;
  };
  category?: {
    categoryId: number;
    categoryName: string;
  };
}

// ── Organisation (full shape — Admin + PWG) ───────────────────────────────
export interface IOrganisation {
  orgId: number;
  userId: number;
  orgName: string;
  orgType: string;
  registrationNo: string;
  contactEmail: string;
  contactPhone: string;
  address: string;
  approvalStatus: string;       // 'Pending' | 'Approved' | 'Rejected'
  approvedAt?: string;
  createdAt: string;
  updatedAt: string;
  user?: {
    userId: number;
    fullName: string;
    email: string;
    phone: string;
    isActive: boolean;
  };
  locality?: {
    localityId: number;
    localityName: string;
    city?: string;
    state?: string;
  };
  category?: {
    categoryId: number;
    categoryName: string;
  };
}

// ── PWG Participation Request ─────────────────────────────────────────────
export interface IPwgParticipationRequest {
  requestId: number;
  complaintId: number;
  orgId: number;
  solverUserId: number;
  status: string;               // 'Pending' | 'Approved' | 'Rejected'
  requestNote?: string;
  decisionNote?: string;
  requestedAt: string;
  decidedAt?: string;
  complaint?: IComplaint;
  organisation?: IOrganisation;
}

// ── PWG Report (US63) ─────────────────────────────────────────────────────
export interface IPwgReport {
  reportId: number;
  complaintId: number;
  reportedByUserId: number;
  reportContent: string;
  status: string;               // 'Open' | 'Reviewed' | 'Closed'
  adminAction?: string;         // 'Warned' | 'Suspended' | 'Removed' | 'Dismissed'
  adminNote?: string;
  createdAt: string;
  reviewedAt?: string;
}

// ── PWG Request Payloads ──────────────────────────────────────────────────

export interface ISubmitParticipationRequest {
  complaintId: number;
  orgId: number;
  requestNote?: string;
}

// Verified: PWGProgressUpdateRequest.cs — uses PWGUserId
export interface IProgressUpdateRequest {
  complaintId: number;
  pwgUserId: number;
  progressNote: string;
  photoPath?: string;
  photoFileName?: string;
  photoFileSizeKB?: number;
}

export interface IResolvePwgRequest {
  requestId: number;
  solverUserId: number;
  decision: string;             // 'Approved' | 'Rejected'
  decisionNote?: string;
}

// ── Admin: PWG Report Requests ─────────────────────────────────────────────
// Verified: ReviewPWGReportRequest.cs
export interface IReviewPwgReportRequest {
  reportId: number;
  adminUserId: number;
  adminAction: string;          // 'Warned' | 'Suspended' | 'Removed' | 'Dismissed'
  adminNote: string;
  finalClose: boolean;
}

// Verified: ClosePWGReportRequest.cs
export interface IClosePwgReportRequest {
  reportId: number;
  adminUserId: number;
  closeNote: string;
}

// ── Profile Update Payloads ───────────────────────────────────────────────

export interface IUpdateDeptProfileRequest {
  deptId: number;
  deptName: string;
  ministry: string;
  contactEmail: string;
  contactPhone: string;
  address: string;
  localityId: number;
}

export interface IUpdateOrgProfileRequest {
  orgId: number;
  orgName: string;
  contactEmail: string;
  contactPhone: string;
  address: string;
}
