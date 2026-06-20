// src/app/app-routing.module.ts

import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';

// ── Guards ────────────────────────────────────────────────────────────────
import { AuthGuard } from './core/guards/auth.guard';
import { RoleGuard } from './core/guards/role.guard';

// ── Layouts ───────────────────────────────────────────────────────────────
import { PublicLayoutComponent } from './layouts/public-layout/public-layout.component';
import { CitizenLayoutComponent } from './layouts/citizen-layout/citizen-layout.component';
import { SolverLayoutComponent } from './layouts/solver-layout/solver-layout.component';
import { PwgLayoutComponent } from './layouts/pwg-layout/pwg-layout.component';
import { AdminLayoutComponent } from './layouts/admin-layout/admin-layout.component';

// ── Auth ──────────────────────────────────────────────────────────────────
import { LoginComponent } from './auth/login/login.component';
import { RegisterCitizenComponent } from './auth/register-citizen/register-citizen.component';
import { RegisterOrganisationComponent } from './auth/register-organisation/register-organisation.component';
import { RegisterDepartmentComponent } from './auth/register-department/register-department.component';
// Phase 8 (§18)
import { ForgotPasswordComponent } from './auth/forgot-password/forgot-password.component';
import { ResetPasswordComponent } from './auth/reset-password/reset-password.component';

// ── Public ────────────────────────────────────────────────────────────────
import { LandingComponent } from './public/landing/landing.component';
import { NotFoundComponent } from './public/not-found/not-found.component';
// Phase 8 (§17) — public transparency portal
import { TransparencyComponent } from './public/transparency/transparency.component';

// ── Citizen ───────────────────────────────────────────────────────────────
import { CitizenHomeComponent } from './citizen/home/citizen-home.component';
import { SubmitComplaintComponent } from './citizen/submit-complaint/submit-complaint.component';
import { MyComplaintsComponent } from './citizen/my-complaints/my-complaints.component';
import { CitizenComplaintDetailComponent } from './citizen/complaint-detail/citizen-complaint-detail.component';
import { ScoreboardComponent } from './citizen/scoreboard/scoreboard.component';
import { InterestsComponent } from './citizen/interests/interests.component';

// ── Solver ────────────────────────────────────────────────────────────────
import { SolverDashboardComponent } from './solver/dashboard/solver-dashboard.component';
import { SolverComplaintListComponent } from './solver/complaint-list/solver-complaint-list.component';
import { SolverComplaintDetailComponent } from './solver/complaint-detail/solver-complaint-detail.component';
import { PwgRequestsComponent } from './solver/pwg-requests/pwg-requests.component';
import { SolverProfileComponent } from './solver/profile/solver-profile.component';

// ── PWG ───────────────────────────────────────────────────────────────────
import { OpenComplaintsComponent } from './pwg/open-complaints/open-complaints.component';
import { MyRequestsComponent } from './pwg/my-requests/my-requests.component';
import { ProgressUpdateComponent } from './pwg/progress-update/progress-update.component';
import { PwgProfileComponent } from './pwg/profile/pwg-profile.component';

// ── Admin ─────────────────────────────────────────────────────────────────
import { AdminDashboardComponent } from './admin/dashboard/admin-dashboard.component';
import { PendingApprovalsComponent } from './admin/pending-approvals/pending-approvals.component';
import { ManageUsersComponent } from './admin/manage-users/manage-users.component';
import { EscalatedComplaintsComponent } from './admin/escalated-complaints/escalated-complaints.component';
import { AdminComplaintListComponent } from './admin/complaint-list/admin-complaint-list.component';
import { AdminComplaintDetailComponent } from './admin/complaint-detail/admin-complaint-detail.component';
import { PwgReportsComponent } from './admin/pwg-reports/pwg-reports.component';
// Phase 8 (§6) — admin appeals review queue
import { AppealsComponent } from './admin/appeals/appeals.component';

// ── Shared ────────────────────────────────────────────────────────────────
import { UserProfileComponent } from './shared/components/user-profile/user-profile.component';

// ── Gamification ──────────────────────────────────────────────────────────
import { NotificationsComponent } from './gamification/notifications/notifications.component';
import { MyPointsComponent } from './gamification/my-points/my-points.component';
import { MyCertificatesComponent } from './gamification/my-certificates/my-certificates.component';

const routes: Routes = [

  // ── Default ──────────────────────────────────────────────────────────────
  { path: '', redirectTo: '/home', pathMatch: 'full' },

  // ── Public (no guard) ─────────────────────────────────────────────────────
  {
    path: '',
    component: PublicLayoutComponent,
    children: [
      { path: 'home', component: LandingComponent },
      { path: 'login', component: LoginComponent },
      { path: 'register/citizen', component: RegisterCitizenComponent },
      { path: 'register/organisation', component: RegisterOrganisationComponent },
      { path: 'register/department', component: RegisterDepartmentComponent },
      // Phase 4 (2026-05-19): explicit 404 surface replaces the previous
      // silent catch-all → /home redirect.
      { path: 'not-found', component: NotFoundComponent },
      // Phase 8 (§17) — public transparency portal (anonymous read-only).
      { path: 'transparency', component: TransparencyComponent },
      // Phase 8 (§18) — password-reset flow
      { path: 'forgot-password', component: ForgotPasswordComponent },
      { path: 'reset-password',  component: ResetPasswordComponent  },
    ]
  },

  // ── Notifications (any authenticated role) ────────────────────────────────
  { path: 'notifications', component: NotificationsComponent, canActivate: [AuthGuard] },

  // ── Citizen ───────────────────────────────────────────────────────────────
  {
    path: 'citizen',
    component: CitizenLayoutComponent,
    canActivate: [AuthGuard, RoleGuard],
    data: { roles: ['Citizen'] },
    children: [
      { path: '', redirectTo: 'home', pathMatch: 'full' },
      { path: 'home', component: CitizenHomeComponent },
      { path: 'profile', component: UserProfileComponent },
      { path: 'submit', component: SubmitComplaintComponent },
      { path: 'complaints', component: MyComplaintsComponent },
      { path: 'complaints/:id', component: CitizenComplaintDetailComponent },
      { path: 'scoreboard', component: ScoreboardComponent },
      { path: 'points', component: MyPointsComponent },
      { path: 'certificates', component: MyCertificatesComponent },
      { path: 'interests', component: InterestsComponent },    // §NEW Phase 3 — US26
      { path: 'notifications', component: NotificationsComponent },
    ]
  },

  // ── Solver ────────────────────────────────────────────────────────────────
  {
    path: 'solver',
    component: SolverLayoutComponent,
    canActivate: [AuthGuard, RoleGuard],
    data: { roles: ['Solver'] },
    children: [
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
      { path: 'dashboard', component: SolverDashboardComponent },
      { path: 'complaints', component: SolverComplaintListComponent },
      { path: 'complaints/:id', component: SolverComplaintDetailComponent },
      { path: 'pwg-requests', component: PwgRequestsComponent },
      { path: 'profile', component: SolverProfileComponent },
      { path: 'notifications', component: NotificationsComponent },
    ]
  },

  // ── PWG ───────────────────────────────────────────────────────────────────
  {
    path: 'pwg',
    component: PwgLayoutComponent,
    canActivate: [AuthGuard, RoleGuard],
    data: { roles: ['PWG'] },
    children: [
      { path: '', redirectTo: 'complaints', pathMatch: 'full' },
      { path: 'complaints', component: OpenComplaintsComponent },
      { path: 'requests', component: MyRequestsComponent },
      { path: 'progress/:complaintId', component: ProgressUpdateComponent },
      { path: 'profile', component: PwgProfileComponent },
      { path: 'notifications', component: NotificationsComponent },
    ]
  },

  // ── Admin ─────────────────────────────────────────────────────────────────
  {
    path: 'admin',
    component: AdminLayoutComponent,
    canActivate: [AuthGuard, RoleGuard],
    data: { roles: ['SuperAdmin'] },
    children: [
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
      { path: 'dashboard', component: AdminDashboardComponent },
      { path: 'profile', component: UserProfileComponent },
      { path: 'approvals', component: PendingApprovalsComponent },
      { path: 'users', component: ManageUsersComponent },
      { path: 'escalated', component: EscalatedComplaintsComponent },
      { path: 'notifications', component: NotificationsComponent },
      { path: 'complaints', component: AdminComplaintListComponent },
      { path: 'complaints/:id', component: AdminComplaintDetailComponent },
      { path: 'pwg-reports', component: PwgReportsComponent },  // §NEW Phase 6 — US63
      // Phase 8 (§6) — citizen appeals queue
      { path: 'appeals', component: AppealsComponent },
    ]
  },

  // ── Catch-all ─────────────────────────────────────────────────────────────
  // Phase 4 (2026-05-19): renders NotFoundComponent inside PublicLayout
  // instead of silently redirecting to /home. The previous behaviour made
  // typos look like nothing happened.
  { path: '**', component: PublicLayoutComponent, children: [{ path: '', component: NotFoundComponent }] }

];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule {}
