// src/app/admin/pending-approvals/pending-approvals.component.ts

import { Component, OnInit } from '@angular/core';
import { AdminService } from '../../fmc-services/admin.service';
import { SessionService } from '../../core/services/session.service';
import { IDepartment, IOrganisation } from '../../fmc-interfaces/pwg.interface';

type ActiveTab = 'depts' | 'orgs';

@Component({
  selector: 'app-pending-approvals',
  templateUrl: './pending-approvals.component.html',
  styleUrls: ['./pending-approvals.component.css']
})
export class PendingApprovalsComponent implements OnInit {

  activeTab: ActiveTab = 'depts';

  depts: IDepartment[] = [];
  orgs: IOrganisation[] = [];

  isLoadingDepts = true;
  isLoadingOrgs = true;

  actionMessage = '';
  actionError = '';

  private adminUserId!: number;

  constructor(
    private adminService: AdminService,
    private session: SessionService
  ) { }

  ngOnInit(): void {
    this.adminUserId = this.session.getUserId();
    this.loadDepts();
    this.loadOrgs();
  }

  // ── Data Loading ─────────────────────────────────────────────────────────
  loadDepts(): void {
    this.isLoadingDepts = true;
    this.adminService.getPendingDepartments().subscribe({
      next: (data) => { this.depts = data; this.isLoadingDepts = false; },
      error: () => { this.actionError = 'Could not load pending departments.'; this.isLoadingDepts = false; }
    });
  }

  loadOrgs(): void {
    this.isLoadingOrgs = true;
    this.adminService.getPendingOrganisations().subscribe({
      next: (data) => { this.orgs = data; this.isLoadingOrgs = false; },
      error: () => { this.actionError = 'Could not load pending organisations.'; this.isLoadingOrgs = false; }
    });
  }

  // ── Tab switching ─────────────────────────────────────────────────────────
  setTab(tab: ActiveTab): void {
    this.activeTab = tab;
    this.actionMessage = '';
    this.actionError = '';
  }

  // ── Dept Decisions ────────────────────────────────────────────────────────
  // ⚠️ Pass dept.userId — NOT dept.deptId (verified from controller)
  decideDept(dept: IDepartment, decision: 'Approved' | 'Rejected'): void {
    this.actionMessage = '';
    this.actionError = '';

    this.adminService.decideDeptRegistration({
      userId: dept.userId,
      decision,
      adminUserId: this.adminUserId
    }).subscribe({
      next: (res) => {
        if (res.success) {
          this.actionMessage = `Department "${dept.deptName}" ${decision}.`;
          this.depts = this.depts.filter(d => d.deptId !== dept.deptId);
        } else {
          this.actionError = res.message || 'Decision failed.';
        }
      },
      error: () => this.actionError = 'Could not record decision.'
    });
  }

  // ── Org Decisions ─────────────────────────────────────────────────────────
  // ⚠️ Pass org.userId — NOT org.orgId (verified from controller)
  decideOrg(org: IOrganisation, decision: 'Approved' | 'Rejected'): void {
    this.actionMessage = '';
    this.actionError = '';

    this.adminService.decideOrgRegistration({
      userId: org.userId,
      decision,
      adminUserId: this.adminUserId
    }).subscribe({
      next: (res) => {
        if (res.success) {
          this.actionMessage = `Organisation "${org.orgName}" ${decision}.`;
          this.orgs = this.orgs.filter(o => o.orgId !== org.orgId);
        } else {
          this.actionError = res.message || 'Decision failed.';
        }
      },
      error: () => this.actionError = 'Could not record decision.'
    });
  }
}
