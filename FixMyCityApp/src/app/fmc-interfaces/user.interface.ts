// src/app/fmc-interfaces/user.interface.ts
// Updated: Login response now includes JWT tokens.

import { IApiResponse } from './api-response.interface';

// ── Login (with JWT) ──────────────────────────────────────────────────────────
export interface ILoginResponse extends IApiResponse {
  // JWT tokens (new)
  accessToken:  string;
  refreshToken: string;
  expiresIn:    number;            // seconds until access token expires
  // User profile (unchanged)
  user: {
    userId:     number;
    fullName:   string;
    email:      string;
    roleId:     number;
    roleName:   string;
    localityId: number;
    deptId?:    number;
    orgId?:     number;
  };
  // Legacy flat fields (kept for backward compatibility with existing components)
  userId:     number;
  fullName:   string;
  email:      string;
  roleId:     number;
  roleName:   string;
  localityId: number;
  orgId?:     number;
  deptId?:    number;
}

// ── Profile ───────────────────────────────────────────────────────────────────
export interface IUserProfile {
  userId:       number;
  fullName:     string;
  email:        string;
  phone:        string;
  address:      string;
  localityId:   number;
  localityName?: string;
  aadhaarNo?:   string;
  isActive:     boolean;
  isApproved:   boolean;
  isBanned?:    boolean;
  roleId:       number;
  roleName:     string;
  orgId?:       number;
  deptId?:      number;
  points?:      number;
  // Populated when the backend includes the Role navigation (e.g. timeline
  // actor). Distinct from the flat roleName above which the legacy API/seed
  // sometimes populated and which we keep for compatibility.
  role?: { roleId: number; roleName: string };
}

// ── Registration Requests ─────────────────────────────────────────────────────
export interface IRegisterCitizenRequest {
  fullName:   string;
  email:      string;
  password:   string;
  phone:      string;
  address:    string;
  localityId: number;
  aadhaarNo:  string;
}

export interface IRegisterOrgRequest {
  fullName:       string;
  email:          string;
  password:       string;
  phone:          string;
  address:        string;
  localityId:     number;
  orgName:        string;
  orgType:        string;
  registrationNo: string;
  contactEmail:   string;
  contactPhone:   string;
}

export interface IRegisterDeptRequest {
  fullName:     string;
  email:        string;
  password:     string;
  phone:        string;
  address:      string;
  localityId:   number;
  deptName:     string;
  ministry:     string;
  categoryId:   number;
  contactEmail: string;
  contactPhone: string;
}

// ── Admin Decision Requests ───────────────────────────────────────────────────
export interface IDeptDecisionRequest {
  userId:      number;
  decision:    string;
  adminUserId: number;
}

export interface IOrgDecisionRequest {
  userId:      number;
  decision:    string;
  adminUserId: number;
}

export interface IDeactivateUserRequest {
  targetUserId: number;
  reason:       string;
  adminUserId:  number;
}

export interface IBanUserRequest {
  targetUserId: number;
  reason:       string;
  adminUserId:  number;
}

export interface IManualEscalationRequest {
  complaintId:         number;
  adminUserId:         number;
  reassignedToDeptId?: number;
  reason:              string;
}

// ── Auth Update Payloads ──────────────────────────────────────────────────────
export interface IChangePasswordRequest {
  userId:          number;
  currentPassword: string;
  newPassword:     string;
}

export interface IUpdateProfileRequest {
  userId:     number;
  fullName:   string;
  phone:      string;
  address:    string;
  localityId: number;
}
