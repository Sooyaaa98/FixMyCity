// src/app/fmc-interfaces/complaint.interface.ts

import { IUserProfile } from './user.interface';

// ── Category ──────────────────────────────────────────────────────────────
export interface IIssueCategory {
  categoryId: number;
  categoryName: string;
  description?: string;
}

// ── Locality ──────────────────────────────────────────────────────────────
export interface ILocality {
  localityId: number;
  localityName: string;
  city: string;
  state: string;
  isActive: boolean;
}

// ── Department (lightweight ref) ──────────────────────────────────────────
export interface IDepartmentRef {
  deptId: number;
  deptName: string;
  ministry: string;
  localityId: number;
}

// ── Complaint ─────────────────────────────────────────────────────────────
// F14: localityId is an FK (int) — NOT a locality string
// Criticality: 'Low' | 'Medium' | 'High' | 'Critical'
// Status: 'Submitted' | 'In Progress' | 'Resolved' | 'Rejected' | 'Escalated' | 'Re-opened' | 'Linked'
export interface IComplaint {
  complaintId: number;
  citizenUserId: number;
  deptId?: number;
  categoryId: number;
  title: string;
  description: string;
  localityId: number;           // FK — use locality.localityName for display
  address: string;
  criticality: string;
  status: string;
  latitude?: number;
  longitude?: number;
  linkedToComplaintId?: number;
  estimatedResDate?: string;
  submittedAt: string;
  updatedAt: string;
  resolvedAt?: string;
  // Navigation objects (populated by EF Include)
  category?: IIssueCategory;
  department?: IDepartmentRef;
  citizen?: IUserProfile;
  locality?: ILocality;
}

// ── Timeline ──────────────────────────────────────────────────────────────
export interface IComplaintTimeline {
  timelineId: number;
  complaintId: number;
  actorUserId: number;
  oldStatus?: string;
  newStatus: string;
  remark?: string;
  photoPath?: string;
  createdAt: string;
  actor?: IUserProfile;
}

// ── Rating ────────────────────────────────────────────────────────────────
export interface IComplaintRating {
  ratingId: number;
  complaintId: number;
  citizenUserId: number;
  stars: number;
  comment?: string;
  ratedAt: string;
}

// ── Attachment ────────────────────────────────────────────────────────────
export interface IComplaintAttachment {
  attachmentId: number;
  complaintId: number;
  uploadedByUserId?: number;
  attachmentType: string;       // 'complaint' | 'resolution' | 'pwg_progress'
  filePath: string;
  fileName: string;
  fileSizeKB?: number;
  timelineId?: number;
  uploadedAt: string;
}

// ── Submit Response ────────────────────────────────────────────────────────
export interface ISubmitComplaintResponse {
  success: boolean;
  complaintId: number;
  message?: string;
  error?: string;
}

// ── Request Payloads ──────────────────────────────────────────────────────

export interface ISubmitComplaintRequest {
  citizenUserId: number;
  categoryId: number;            // short on backend, number in TS is fine
  title: string;
  description: string;
  localityId: number;            // FK — confirmed from SubmitComplaintRequest.cs
  address: string;
  criticality: string;           // 'Low' | 'Medium' | 'High' | 'Critical'
  latitude?: number;
  longitude?: number;
  filePath?: string;
  fileName?: string;
  fileSizeKB?: number;
}

export interface IUpdateStatusRequest {
  complaintId: number;
  solverUserId: number;
  newStatus: string;
  remark?: string;
  resolutionFilePath?: string;
  resolutionFileName?: string;
  resolutionFileSizeKB?: number;
}

export interface IUpdateEstDateRequest {
  complaintId: number;
  solverUserId: number;
  estDate: string;              // ISO date string
}

export interface IRateComplaintRequest {
  complaintId: number;
  citizenUserId: number;
  stars: number;                // 1–5
  comment?: string;
}

export interface IReopenComplaintRequest {
  complaintId: number;
  citizenUserId: number;
  reason: string;
}

export interface IAddAttachmentRequest {
  complaintId: number;
  uploadedByUserId: number;
  attachmentType: string;
  filePath: string;
  fileName: string;
  fileSizeKB?: number;
  timelineId?: number;
}

export interface ILinkDuplicateRequest {
  originalComplaintId: number;
  linkedComplaintId: number;
  linkedByUserId: number;
}
