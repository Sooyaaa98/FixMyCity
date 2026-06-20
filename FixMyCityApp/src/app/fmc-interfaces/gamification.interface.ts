// src/app/fmc-interfaces/gamification.interface.ts

export interface IPlatformStats {
  totalComplaints: number;
  submitted: number;
  inProgress: number;
  resolved: number;
  rejected: number;
  reopened: number;
  escalated: number;
  linked: number;
  activeUsers: number;
  totalCitizens: number;
  totalSolvers: number;
  totalPWG: number;
  totalAdmins: number;
}

// ScoreboardEntry DTO from DAL — localityName is the string from the JOIN
export interface IScoreboardEntry {
  userId: number;
  fullName: string;
  localityName: string;
  points: number;
  rank: number;
  snapshotAt?: string;
}

export interface IUserPoint {
  pointsId: number;
  userId: number;
  points: number;
  lastUpdated: string;
}

export interface ICertificate {
  certificateId: number;
  userId: number;
  milestone: string;
  verificationCode: string;
  issuedAt: string;
  filePath?: string;
}

export interface INotification {
  notificationId: number;
  userId: number;
  complaintId?: number;
  message: string;
  isRead: boolean;
  isArchived?: boolean;
  createdAt: string;
}
