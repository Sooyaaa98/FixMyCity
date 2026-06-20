// src/app/app.module.ts
// Updated: registers AuthInterceptor BEFORE HttpErrorInterceptor so JWT is
// attached before error handling runs.
// AuthInterceptor is provided as a singleton (useExisting via forwardRef trick
// is not needed here — we use a direct class reference).

import { NgModule } from '@angular/core';
import { BrowserModule } from '@angular/platform-browser';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { HTTP_INTERCEPTORS, HttpClientModule } from '@angular/common/http';
import { ReactiveFormsModule, FormsModule } from '@angular/forms';

import { AppRoutingModule } from './app-routing.module';

import { AuthInterceptor } from './core/interceptors/auth.interceptor';
import { HttpErrorInterceptor } from './core/interceptors/http-error.interceptor';

import { AppComponent } from './app.component';

import { PublicLayoutComponent } from './layouts/public-layout/public-layout.component';
import { CitizenLayoutComponent } from './layouts/citizen-layout/citizen-layout.component';
import { SolverLayoutComponent } from './layouts/solver-layout/solver-layout.component';
import { PwgLayoutComponent } from './layouts/pwg-layout/pwg-layout.component';
import { AdminLayoutComponent } from './layouts/admin-layout/admin-layout.component';

import { NavbarComponent } from './shared/components/navbar/navbar.component';
import { NotificationBellComponent } from './shared/components/notification-bell/notification-bell.component';
import { ComplaintCardComponent } from './shared/components/complaint-card/complaint-card.component';
import { StatusBadgeComponent } from './shared/components/status-badge/status-badge.component';
import { TimelineComponent } from './shared/components/timeline/timeline.component';
import { UserProfileComponent } from './shared/components/user-profile/user-profile.component';
import { ToastComponent } from './shared/components/toast/toast.component';
import { LoadingSpinnerComponent } from './shared/components/loading-spinner/loading-spinner.component';
import { EmptyStateComponent } from './shared/components/empty-state/empty-state.component';
import { AiHintPanelComponent } from './shared/components/ai-hint-panel/ai-hint-panel.component';
import { ChatbotWidgetComponent } from './shared/components/chatbot-widget/chatbot-widget.component';
import { MapViewComponent } from './shared/components/map-view/map-view.component';

import { LoginComponent } from './auth/login/login.component';
import { RegisterCitizenComponent } from './auth/register-citizen/register-citizen.component';
import { RegisterOrganisationComponent } from './auth/register-organisation/register-organisation.component';
import { RegisterDepartmentComponent } from './auth/register-department/register-department.component';
// Phase 8 (§18) password reset flow
import { ForgotPasswordComponent } from './auth/forgot-password/forgot-password.component';
import { ResetPasswordComponent } from './auth/reset-password/reset-password.component';

import { CitizenHomeComponent } from './citizen/home/citizen-home.component';
import { SubmitComplaintComponent } from './citizen/submit-complaint/submit-complaint.component';
import { MyComplaintsComponent } from './citizen/my-complaints/my-complaints.component';
import { CitizenComplaintDetailComponent } from './citizen/complaint-detail/citizen-complaint-detail.component';
import { ScoreboardComponent } from './citizen/scoreboard/scoreboard.component';
import { InterestsComponent } from './citizen/interests/interests.component';

import { SolverDashboardComponent } from './solver/dashboard/solver-dashboard.component';
import { SolverComplaintListComponent } from './solver/complaint-list/solver-complaint-list.component';
import { SolverComplaintDetailComponent } from './solver/complaint-detail/solver-complaint-detail.component';
import { SolverProfileComponent } from './solver/profile/solver-profile.component';
import { PwgRequestsComponent } from './solver/pwg-requests/pwg-requests.component';

import { AdminDashboardComponent } from './admin/dashboard/admin-dashboard.component';
import { AdminComplaintListComponent } from './admin/complaint-list/admin-complaint-list.component';
import { AdminComplaintDetailComponent } from './admin/complaint-detail/admin-complaint-detail.component';
import { EscalatedComplaintsComponent } from './admin/escalated-complaints/escalated-complaints.component';
import { ManageUsersComponent } from './admin/manage-users/manage-users.component';
import { PendingApprovalsComponent } from './admin/pending-approvals/pending-approvals.component';
import { PwgReportsComponent } from './admin/pwg-reports/pwg-reports.component';

import { MyPointsComponent } from './gamification/my-points/my-points.component';
import { MyCertificatesComponent } from './gamification/my-certificates/my-certificates.component';
import { NotificationsComponent } from './gamification/notifications/notifications.component';

import { PwgProfileComponent } from './pwg/profile/pwg-profile.component';
import { MyRequestsComponent } from './pwg/my-requests/my-requests.component';
import { OpenComplaintsComponent } from './pwg/open-complaints/open-complaints.component';
import { ProgressUpdateComponent } from './pwg/progress-update/progress-update.component';

import { LandingComponent } from './public/landing/landing.component';
import { NotFoundComponent } from './public/not-found/not-found.component';
import { ThemeToggleComponent } from './shared/components/theme-toggle/theme-toggle.component';

// Phase 6 — approved frontend upgrades
import { RevealDirective } from './shared/directives/reveal.directive';
import { TiltDirective }   from './shared/directives/tilt.directive';
import { RippleDirective } from './shared/directives/ripple.directive';
import { SkeletonCardComponent } from './shared/components/skeleton-card/skeleton-card.component';

// Phase 8 — feature-suggestion wave
import { SlaPipe, SlaBadgeClassPipe } from './shared/pipes/sla.pipe';
import { PhotoCompareComponent }      from './shared/components/photo-compare/photo-compare.component';
import { NearbyComplaintsComponent }  from './shared/components/nearby-complaints/nearby-complaints.component';
import { UpvoteButtonComponent }      from './shared/components/upvote-button/upvote-button.component';
import { CommentsThreadComponent }    from './shared/components/comments-thread/comments-thread.component';
import { InternalNotesComponent }     from './shared/components/internal-notes/internal-notes.component';
import { ActivityFeedComponent }      from './shared/components/activity-feed/activity-feed.component';
import { TrendChartComponent }        from './shared/components/trend-chart/trend-chart.component';
import { TransparencyComponent }      from './public/transparency/transparency.component';
import { AppealsComponent }           from './admin/appeals/appeals.component';

@NgModule({
  declarations: [
    AppComponent,
    PublicLayoutComponent, CitizenLayoutComponent, SolverLayoutComponent,
    PwgLayoutComponent, AdminLayoutComponent,
    NavbarComponent, NotificationBellComponent, ComplaintCardComponent,
    StatusBadgeComponent, TimelineComponent, UserProfileComponent,
    ToastComponent, LoadingSpinnerComponent, EmptyStateComponent,
    AiHintPanelComponent, ChatbotWidgetComponent, MapViewComponent,
    LoginComponent, RegisterCitizenComponent, RegisterOrganisationComponent,
    RegisterDepartmentComponent,
    // Phase 8 (§18)
    ForgotPasswordComponent,
    ResetPasswordComponent,
    CitizenHomeComponent, SubmitComplaintComponent, MyComplaintsComponent,
    CitizenComplaintDetailComponent, ScoreboardComponent, InterestsComponent,
    SolverDashboardComponent, SolverComplaintListComponent, SolverComplaintDetailComponent,
    SolverProfileComponent, PwgRequestsComponent,
    AdminDashboardComponent, AdminComplaintListComponent, AdminComplaintDetailComponent,
    EscalatedComplaintsComponent, ManageUsersComponent, PendingApprovalsComponent,
    PwgReportsComponent,
    MyPointsComponent, MyCertificatesComponent, NotificationsComponent,
    PwgProfileComponent, MyRequestsComponent, OpenComplaintsComponent, ProgressUpdateComponent,
    LandingComponent,
    NotFoundComponent,
    ThemeToggleComponent,
    // Phase 6
    RevealDirective,
    TiltDirective,
    RippleDirective,
    SkeletonCardComponent,
    // Phase 8
    SlaPipe,
    SlaBadgeClassPipe,
    PhotoCompareComponent,
    NearbyComplaintsComponent,
    UpvoteButtonComponent,
    CommentsThreadComponent,
    InternalNotesComponent,
    ActivityFeedComponent,
    TrendChartComponent,
    TransparencyComponent,
    AppealsComponent,
  ],
  imports: [
    BrowserModule,
    BrowserAnimationsModule,   // Phase 6 — required for @angular/animations triggers
    AppRoutingModule,
    HttpClientModule,
    ReactiveFormsModule,
    FormsModule,
  ],
  providers: [
    // AuthInterceptor must be a singleton so the refresh-queuing logic works
    // and so the refreshFn injected by AuthService is visible to the HTTP pipeline.
    // useExisting reuses the same instance — useClass would create a separate
    // one whose refreshFn is never set, silently breaking token refresh.
    AuthInterceptor,
    {
      // 1st: attach JWT Bearer token (and handle 401 / token refresh)
      provide:     HTTP_INTERCEPTORS,
      useExisting: AuthInterceptor,
      multi:       true,
    },
    {
      // 2nd: handle other HTTP errors and show toasts
      provide:  HTTP_INTERCEPTORS,
      useClass: HttpErrorInterceptor,
      multi:    true,
    },
  ],
  bootstrap: [AppComponent],
})
export class AppModule {}
