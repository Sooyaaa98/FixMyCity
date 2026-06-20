# FixMyCity — New Feature Suggestions

> **How to read this:** Every suggestion is grounded in what already exists in the codebase. Each entry lists what backend endpoints/services are already available, what needs to be built new, and a difficulty rating.
>
> **Roles in the system:** Citizen · Solver · PWG (Public Works Group / NGO) · SuperAdmin

---

## Table of Contents

1. [Citizen — Upvote / Support Complaints](#1-upvote--support-complaints)
2. [Citizen — Complaint Drafts with Autosave](#2-complaint-drafts-with-autosave)
3. [Citizen — Before / After Photo Viewer](#3-before--after-photo-viewer)
4. [Citizen — Share Complaint via WhatsApp / Social](#4-share-complaint)
5. [Citizen — "Near Me" Complaints via Geolocation](#5-near-me-complaints)
6. [Citizen — Appeal a Rejected Complaint](#6-appeal-rejected-complaint)
7. [Citizen — Public Complaint Comments / Discussion](#7-public-complaint-comments)
8. [Citizen — QR Code for Complaint](#8-qr-code-for-complaint)
9. [Admin — Analytics Dashboard with Charts](#9-analytics-dashboard)
10. [Admin — Export Complaints to CSV](#10-export-to-csv)
11. [Admin — Bulk Complaint Actions](#11-bulk-actions)
12. [Admin — Manual Complaint Assignment to Department](#12-manual-assignment)
13. [Admin — Locality Heatmap](#13-locality-heatmap)
14. [Solver — SLA Countdown Timer per Complaint](#14-sla-countdown)
15. [Solver — Internal Notes on Complaints](#15-internal-notes)
16. [Solver — Bulk Status Update](#16-bulk-status-update)
17. [Public — Transparency Portal (No Login Required)](#17-transparency-portal)
18. [PWA — Offline Support & Install Prompt](#18-pwa-offline)
19. [Auth — Forgot Password / Email Verification](#19-forgot-password)
20. [All Roles — Activity Feed on Profile](#20-activity-feed)

---

## 1. Upvote / Support Complaints

**What it does:** Citizens can upvote complaints raised by others in their locality to signal urgency. Complaints with high upvotes get a priority boost — solvers can sort by "most supported."

**Already exists:**
- `getLocalityFeed()` — already fetches complaints in a citizen's locality
- `IComplaint` interface can be extended with `upvoteCount`
- Point system and gamification hooks are already wired

**What needs to be built:**

*Backend (.NET API):*
```
POST /api/Complaint/UpvoteComplaint  { complaintId, citizenUserId }
GET  /api/Complaint/GetUpvoteCount?complaintId=x
```
Add `Upvotes` table: `(UpvoteId, ComplaintId, CitizenUserId, CreatedAt)` with unique constraint on `(ComplaintId, CitizenUserId)`.

*Frontend (Angular):*
- Add upvote button to `complaint-card.component.html`:
```html
<button class="upvote-btn" [class.upvoted]="complaint.hasUpvoted"
        (click)="toggleUpvote($event, complaint)">
  <i class="bi bi-arrow-up-circle-fill"></i>
  <span>{{ complaint.upvoteCount }}</span>
</button>
```
- Add `upvoteComplaint()` method to `ComplaintService`
- Optimistic UI update (update count immediately, revert on error)
- Award +2 points when a citizen's complaint reaches 10 upvotes (hook into existing gamification)

**Difficulty:** ⭐⭐ Medium  
**Impact:** High — makes the platform feel social and boosts civic engagement

---

## 2. Complaint Drafts with Autosave

**What it does:** The submit complaint form auto-saves to `localStorage` as the citizen types. If they close the tab or navigate away, a "Resume draft?" banner appears next time they visit `/citizen/submit`.

**Already exists:**
- `submit-complaint.component.ts` has `FormGroup` with all fields already defined
- `SessionService` pattern already exists for local storage access

**What needs to be built:**

*Frontend only — no backend changes needed:*

In `submit-complaint.component.ts`:
```typescript
private readonly DRAFT_KEY = 'fmc_complaint_draft';

// In ngOnInit, after form init:
this.loadDraft();

// Subscribe to value changes:
this.submitForm.valueChanges.pipe(
  debounceTime(1200),
  takeUntil(this.destroy$)
).subscribe(val => this.saveDraft(val));

saveDraft(val: any) {
  localStorage.setItem(this.DRAFT_KEY, JSON.stringify({
    ...val,
    savedAt: new Date().toISOString()
  }));
}

loadDraft() {
  const raw = localStorage.getItem(this.DRAFT_KEY);
  if (!raw) return;
  const draft = JSON.parse(raw);
  this.hasDraft = true;
  this.draftSavedAt = draft.savedAt;
}

restoreDraft() {
  const raw = localStorage.getItem(this.DRAFT_KEY);
  if (!raw) return;
  const { savedAt, ...formVal } = JSON.parse(raw);
  this.submitForm.patchValue(formVal);
  this.hasDraft = false;
}

clearDraft() {
  localStorage.removeItem(this.DRAFT_KEY);
  this.hasDraft = false;
}

// In onSubmit(), call clearDraft() on success
```

Add a draft banner to `submit-complaint.component.html`:
```html
<div class="draft-banner fmc-card" *ngIf="hasDraft">
  <i class="bi bi-clock-history"></i>
  You have an unsaved draft from {{ draftSavedAt | date:'dd MMM, h:mm a' }}
  <button (click)="restoreDraft()">Resume</button>
  <button (click)="clearDraft()">Discard</button>
</div>
```

**Difficulty:** ⭐ Easy  
**Impact:** High — prevents data loss; solves a real pain point

---

## 3. Before / After Photo Viewer

**What it does:** On the complaint detail page, the citizen's original complaint photo (stored in `IComplaintAttachment` with `attachmentType = 'complaint'`) is shown side-by-side with the PWG/Solver's resolution photo (`attachmentType = 'resolution'`). A draggable slider lets users reveal the before vs after.

**Already exists:**
- `getAttachments(complaintId, type)` already exists in `ComplaintService`
- `IComplaintAttachment` has `filePath`, `fileName`, `attachmentType`
- Resolution photos are uploaded by the Solver when marking as Resolved via `IUpdateStatusRequest`
- Citizen complaint detail at `/citizen/complaints/:id` already shows the timeline

**What needs to be built:**

*Frontend only:*

Create `src/app/shared/components/photo-compare/photo-compare.component.ts`:
```typescript
@Component({
  selector: 'app-photo-compare',
  template: `
    <div class="compare-wrapper" (mousemove)="onMove($event)"
         (touchmove)="onTouch($event)" #wrap>
      <img [src]="beforeUrl" class="compare-img compare-img--before" alt="Before" />
      <div class="compare-overlay" [style.width.%]="position">
        <img [src]="afterUrl" class="compare-img compare-img--after" alt="After" />
      </div>
      <div class="compare-handle" [style.left.%]="position">
        <div class="compare-handle-line"></div>
        <div class="compare-handle-circle">
          <i class="bi bi-arrows-expand"></i>
        </div>
      </div>
      <span class="compare-label compare-label--before">Before</span>
      <span class="compare-label compare-label--after">After</span>
    </div>
  `
})
export class PhotoCompareComponent {
  @Input() beforeUrl = '';
  @Input() afterUrl  = '';
  @ViewChild('wrap') wrapRef!: ElementRef;
  position = 50;

  onMove(e: MouseEvent) {
    const rect = this.wrapRef.nativeElement.getBoundingClientRect();
    this.position = Math.min(95, Math.max(5,
      ((e.clientX - rect.left) / rect.width) * 100
    ));
  }
}
```

Use in `citizen-complaint-detail.component.html`:
```html
<app-photo-compare
  *ngIf="beforePhoto && afterPhoto"
  [beforeUrl]="beforePhoto.filePath"
  [afterUrl]="afterPhoto.filePath">
</app-photo-compare>
```

**Difficulty:** ⭐⭐ Medium  
**Impact:** High — creates a powerful "proof of resolution" visual that builds citizen trust

---

## 4. Share Complaint

**What it does:** A share button on the complaint detail page generates a direct link and opens native share options — WhatsApp, copy link, Twitter/X. No backend needed; uses the Web Share API with a fallback.

**Already exists:**
- Each complaint has a stable URL at `/citizen/complaints/:id`
- Toast service is available for "Link copied!" confirmation

**What needs to be built:**

*Frontend only:*

Add to `citizen-complaint-detail.component.ts`:
```typescript
shareComplaint() {
  const url  = `${window.location.origin}/citizen/complaints/${this.complaint.complaintId}`;
  const text = `🏙️ Help fix this issue in ${this.complaint.locality?.localityName}: "${this.complaint.title}"`;

  if (navigator.share) {
    navigator.share({ title: 'FixMyCity', text, url });
  } else {
    navigator.clipboard.writeText(url).then(() =>
      this.toast.show('Link copied to clipboard!', 'success')
    );
  }
}
```

Add share buttons to the complaint detail HTML:
```html
<div class="share-row">
  <span class="share-label">Share:</span>
  <a [href]="'https://wa.me/?text=' + whatsappText" target="_blank"
     class="share-btn share-btn--whatsapp" title="Share on WhatsApp">
    <i class="bi bi-whatsapp"></i>
  </a>
  <a [href]="'https://twitter.com/intent/tweet?text=' + tweetText" target="_blank"
     class="share-btn share-btn--twitter">
    <i class="bi bi-twitter-x"></i>
  </a>
  <button class="share-btn share-btn--copy" (click)="shareComplaint()">
    <i class="bi bi-link-45deg"></i> Copy Link
  </button>
</div>
```

**Difficulty:** ⭐ Easy  
**Impact:** Medium — grows organic reach and awareness without any infrastructure cost

---

## 5. "Near Me" Complaints

**What it does:** A "Near Me" button on the citizen home page uses the browser's Geolocation API to find complaints within a configurable radius (e.g., 2 km). Shows results on the existing `MapViewComponent`.

**Already exists:**
- `MapViewComponent` already renders `IComplaint[]` with Leaflet markers
- `getMapComplaints(localityId?)` exists in `ComplaintService`
- `IComplaint` has `latitude` and `longitude` fields

**What needs to be built:**

*Frontend — new method in `CitizenHomeComponent`:*
```typescript
findNearMe() {
  this.locating = true;
  navigator.geolocation.getCurrentPosition(
    pos => {
      const { latitude, longitude } = pos.coords;
      this.userLat = latitude;
      this.userLng = longitude;
      this.locating = false;
      this.showNearbyComplaints(latitude, longitude, 2); // 2 km radius
    },
    err => {
      this.toast.show('Location access denied. Enable it in browser settings.', 'warning');
      this.locating = false;
    }
  );
}

private showNearbyComplaints(lat: number, lng: number, radiusKm: number) {
  const all = this.allComplaints; // already loaded list
  this.nearbyComplaints = all.filter(c => {
    if (!c.latitude || !c.longitude) return false;
    return this.haversineKm(lat, lng, c.latitude, c.longitude) <= radiusKm;
  });
  this.mapMode = 'nearby';
}

private haversineKm(lat1: number, lng1: number, lat2: number, lng2: number): number {
  const R = 6371;
  const dLat = (lat2 - lat1) * Math.PI / 180;
  const dLng = (lng2 - lng1) * Math.PI / 180;
  const a = Math.sin(dLat/2)**2 + Math.cos(lat1*Math.PI/180) *
            Math.cos(lat2*Math.PI/180) * Math.sin(dLng/2)**2;
  return R * 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1-a));
}
```

*Backend — optional but recommended:*
```
GET /api/Complaint/GetNearbyComplaints?lat=x&lng=y&radiusKm=2
```
Uses SQL Server geography or Haversine formula for server-side filtering.

**Difficulty:** ⭐⭐ Medium  
**Impact:** High — one of the most useful features for civic engagement; makes the app feel location-aware

---

## 6. Appeal a Rejected Complaint

**What it does:** When a complaint is `Rejected`, the citizen sees an "Appeal Rejection" button (distinct from `ReopenComplaint`). The appeal goes to the SuperAdmin for review with a mandatory reason field. Admin sees a dedicated "Appeals" tab.

**Already exists:**
- `ReopenComplaint` endpoint and `IReopenComplaintRequest` already exist, but reopen is for resolved complaints — appeals are for rejected ones
- `IComplaintTimeline` can record the appeal event
- Admin complaint list already exists

**What needs to be built:**

*Backend (.NET API):*
```
POST /api/Complaint/AppealComplaint  { complaintId, citizenUserId, appealReason }
GET  /api/Admin/GetPendingAppeals
PUT  /api/Admin/ResolveAppeal  { complaintId, adminUserId, decision, note }
```
Add `Appeals` table: `(AppealId, ComplaintId, CitizenUserId, Reason, Status, AdminNote, CreatedAt, ResolvedAt)`

*Frontend:*
- In `citizen-complaint-detail.component.html`, show appeal button only when `complaint.status === 'Rejected'` and no prior appeal exists:
```html
<button class="btn-warning" *ngIf="canAppeal" (click)="openAppealModal()">
  <i class="bi bi-flag-fill"></i> Appeal Rejection
</button>
```
- Add appeal modal with textarea for reason
- Admin route: `admin/appeals` with approve/reject controls

**Difficulty:** ⭐⭐⭐ Hard  
**Impact:** High — critical for citizen trust; without appeals, "Rejected" feels like a dead end

---

## 7. Public Complaint Comments / Discussion

**What it does:** Citizens can leave public comments on any complaint — adding context, updates from the ground, or showing solidarity. Solvers can post official replies visible to everyone. Not anonymous — tied to user account.

**Already exists:**
- `IComplaintTimeline` shows solver remarks but is internal
- `IUserProfile` is available on each complaint for display

**What needs to be built:**

*Backend (.NET API):*
```
POST /api/Complaint/AddComment  { complaintId, userId, commentText }
GET  /api/Complaint/GetComments?complaintId=x
```
New `ComplaintComments` table: `(CommentId, ComplaintId, UserId, CommentText, IsOfficialReply, CreatedAt)`

*Frontend:*
Create `src/app/shared/components/comment-thread/comment-thread.component.ts`:
```typescript
@Component({ selector: 'app-comment-thread', ... })
export class CommentThreadComponent implements OnInit {
  @Input() complaintId!: number;
  comments: IComment[] = [];
  newComment = '';
  submitting = false;

  submit() {
    if (!this.newComment.trim()) return;
    this.submitting = true;
    this.complaintService.addComment({
      complaintId: this.complaintId,
      userId: this.session.getUserId(),
      commentText: this.newComment
    }).subscribe(() => {
      this.newComment = '';
      this.loadComments();
    });
  }
}
```

Template shows avatar, name, time, and comment text. Solver comments get an "Official Reply" badge.

**Difficulty:** ⭐⭐⭐ Hard  
**Impact:** Very high — transforms the app from a ticketing system into a civic community platform

---

## 8. QR Code for Complaint

**What it does:** Each complaint detail page has a "QR Code" button that generates a scannable QR linking directly to that complaint URL. Useful for offline community boards, printed notices, or sharing with non-smartphone users.

**Already exists:**
- Complaints have stable URLs at `/citizen/complaints/:id`
- No backend changes needed

**What needs to be built:**

*Frontend only — install one package:*
```bash
npm install qrcode --save
npm install @types/qrcode --save-dev
```

Add to `citizen-complaint-detail.component.ts`:
```typescript
import QRCode from 'qrcode';

async generateQR() {
  const url = `${window.location.origin}/citizen/complaints/${this.complaint.complaintId}`;
  this.qrDataUrl = await QRCode.toDataURL(url, {
    width: 220,
    color: { dark: '#1e3a5f', light: '#ffffff' }
  });
  this.showQrModal = true;
}
```

Show QR in a modal with a download button:
```html
<div class="qr-modal" *ngIf="showQrModal">
  <img [src]="qrDataUrl" alt="QR Code" />
  <a [href]="qrDataUrl" [download]="'complaint-' + complaint.complaintId + '.png'"
     class="btn-secondary">
    <i class="bi bi-download"></i> Download PNG
  </a>
</div>
```

**Difficulty:** ⭐ Easy  
**Impact:** Medium — surprisingly useful for community groups and local print notices

---

## 9. Analytics Dashboard with Charts

**What it does:** Replace the admin stat cards grid (which only shows plain numbers) with interactive charts: complaint trend over time, category breakdown pie chart, status distribution, and resolution rate by department.

**Already exists:**
- `IPlatformStats` interface already has all the numbers (`totalComplaints`, `inProgress`, `resolved`, `rejected`, etc.)
- Admin dashboard already fetches this data
- No new backend endpoints needed for basic charts; timeline data may need a new endpoint

**What needs to be built:**

*Backend — one new endpoint:*
```
GET /api/Admin/GetComplaintTrend?days=30
```
Returns: `[{ date: '2025-05-01', count: 12 }, ...]`

*Frontend — use Chart.js (already in package.json):*

Add to `admin-dashboard.component.html` below the stat cards:
```html
<div class="charts-grid">
  <div class="fmc-card chart-card">
    <h5>Complaints Over Time (30 Days)</h5>
    <canvas id="trendChart"></canvas>
  </div>
  <div class="fmc-card chart-card">
    <h5>Status Breakdown</h5>
    <canvas id="statusChart"></canvas>
  </div>
  <div class="fmc-card chart-card">
    <h5>Category Distribution</h5>
    <canvas id="categoryChart"></canvas>
  </div>
</div>
```

In `admin-dashboard.component.ts`:
```typescript
import { Chart, registerables } from 'chart.js';
Chart.register(...registerables);

private buildStatusChart() {
  new Chart('statusChart', {
    type: 'doughnut',
    data: {
      labels: ['Submitted', 'In Progress', 'Resolved', 'Rejected', 'Escalated'],
      datasets: [{
        data: [
          this.stats.submitted, this.stats.inProgress,
          this.stats.resolved, this.stats.rejected, this.stats.escalated
        ],
        backgroundColor: ['#f59e0b','#2563eb','#10b981','#ef4444','#7c3aed']
      }]
    }
  });
}
```

**Difficulty:** ⭐⭐ Medium  
**Impact:** Very high — admins currently fly blind; charts expose actionable insights immediately

---

## 10. Export Complaints to CSV

**What it does:** Admin and Solver dashboards get an "Export CSV" button that downloads all currently filtered/visible complaints as a spreadsheet. Pure frontend — no backend changes.

**Already exists:**
- All complaint data is already in-memory in the component arrays
- `IComplaint` has all necessary fields

**What needs to be built:**

*Frontend — create a reusable utility:*

Create `src/app/core/services/export.service.ts`:
```typescript
@Injectable({ providedIn: 'root' })
export class ExportService {
  exportComplaintsCSV(complaints: IComplaint[], filename = 'complaints') {
    const headers = [
      'ID','Title','Status','Category','Locality',
      'Address','Criticality','Submitted','Resolved'
    ];
    const rows = complaints.map(c => [
      c.complaintId, `"${c.title}"`, c.status,
      c.category?.categoryName ?? '', c.locality?.localityName ?? '',
      `"${c.address}"`, c.criticality,
      c.submittedAt, c.resolvedAt ?? ''
    ]);

    const csv = [headers, ...rows].map(r => r.join(',')).join('\n');
    const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' });
    const url  = URL.createObjectURL(blob);
    const a    = document.createElement('a');
    a.href     = url;
    a.download = `${filename}_${new Date().toISOString().slice(0,10)}.csv`;
    a.click();
    URL.revokeObjectURL(url);
  }
}
```

Inject and call from any complaint list component:
```typescript
exportCsv() {
  this.exportService.exportComplaintsCSV(this.complaints, 'fixmycity_complaints');
}
```

**Difficulty:** ⭐ Easy  
**Impact:** High — the first thing any admin will ask for in a real deployment

---

## 11. Bulk Complaint Actions

**What it does:** Admin and Solver complaint list gets checkboxes on each row. A floating action bar appears when ≥1 complaint is selected, offering: Bulk Escalate, Bulk Assign to Department, Bulk Export.

**Already exists:**
- `AdminComplaintListComponent` already has an `IComplaint[]` array
- `updateStatus()` in `ComplaintService` can be called per-complaint
- `ExportService` from feature #10 handles export

**What needs to be built:**

*Backend — one new endpoint for efficiency:*
```
PUT /api/Admin/BulkUpdateStatus  { complaintIds: number[], newStatus: string, adminUserId: number }
```

*Frontend:*

Add to `admin-complaint-list.component.ts`:
```typescript
selectedIds = new Set<number>();

toggleSelect(id: number) {
  this.selectedIds.has(id) ? this.selectedIds.delete(id) : this.selectedIds.add(id);
}

toggleSelectAll() {
  if (this.selectedIds.size === this.complaints.length)
    this.selectedIds.clear();
  else
    this.complaints.forEach(c => this.selectedIds.add(c.complaintId));
}

bulkEscalate() {
  this.adminService.bulkUpdateStatus({
    complaintIds: [...this.selectedIds],
    newStatus: 'Escalated',
    adminUserId: this.session.getUserId()
  }).subscribe(() => {
    this.toast.show(`${this.selectedIds.size} complaints escalated`, 'success');
    this.selectedIds.clear();
    this.load();
  });
}
```

Floating action bar HTML (fixed bottom of page when selection > 0):
```html
<div class="bulk-bar" *ngIf="selectedIds.size > 0">
  <span>{{ selectedIds.size }} selected</span>
  <button (click)="bulkEscalate()">
    <i class="bi bi-exclamation-triangle-fill"></i> Escalate
  </button>
  <button (click)="bulkExport()">
    <i class="bi bi-download"></i> Export CSV
  </button>
  <button (click)="selectedIds.clear()">Cancel</button>
</div>
```

**Difficulty:** ⭐⭐ Medium  
**Impact:** High — saves admins enormous time in high-volume scenarios

---

## 12. Manual Complaint Assignment to Department

**What it does:** Admin can manually reassign a complaint to a different department (overriding the AI auto-assignment). Shows a dropdown of all `Approved` departments filtered by complaint category.

**Already exists:**
- `overrideAIDecision()` in `MlService` exists and sets `deptId` override
- `IDepartment` interface exists in `pwg.interface.ts`
- `IComplaint` has a `deptId` field

**What needs to be built:**

*Backend:*
```
PUT /api/Admin/ReassignComplaint  { complaintId, newDeptId, adminUserId, reason }
```

*Frontend — add to `admin-complaint-detail.component.html`:*
```html
<div class="assign-section fmc-card" *ngIf="isAdmin">
  <h6><i class="bi bi-diagram-3-fill"></i> Department Assignment</h6>
  <p class="text-muted">Currently: <strong>{{ complaint.department?.deptName ?? 'Unassigned' }}</strong></p>
  <select [(ngModel)]="newDeptId" class="fmc-input">
    <option *ngFor="let d of eligibleDepts" [value]="d.deptId">
      {{ d.deptName }} — {{ d.ministry }}
    </option>
  </select>
  <input [(ngModel)]="assignReason" class="fmc-input mt-2"
         placeholder="Reason for reassignment (required)" />
  <button class="btn-primary mt-2" (click)="reassign()"
          [disabled]="!newDeptId || !assignReason">
    <i class="bi bi-arrow-left-right"></i> Reassign
  </button>
</div>
```

**Difficulty:** ⭐⭐ Medium  
**Impact:** Medium — essential for edge cases where AI mis-categorizes complaints

---

## 13. Locality Heatmap

**What it does:** Admin dashboard gets a visual heatmap tab — a colour-coded map where each locality is shaded by complaint density (darker = more open complaints). Uses the existing `MapViewComponent` + Leaflet heatmap plugin.

**Already exists:**
- `MapViewComponent` is already set up with Leaflet (`declare const L: any`)
- `getMapComplaints()` returns all complaints with `latitude`/`longitude`
- `getGeoClusters()` in `MlService` returns cluster data per locality

**What needs to be built:**

*Frontend — add Leaflet.heat to index.html:*
```html
<script src="https://cdn.jsdelivr.net/npm/leaflet.heat@0.2.0/dist/leaflet-heat.js"></script>
```

In `admin-dashboard.component.ts`:
```typescript
private buildHeatmap(complaints: IComplaint[]) {
  const points = complaints
    .filter(c => c.latitude && c.longitude)
    .map(c => [c.latitude!, c.longitude!, 0.5]);  // [lat, lng, intensity]
  L.heatLayer(points, {
    radius: 30, blur: 20, maxZoom: 14,
    gradient: { 0.2: '#10b981', 0.5: '#f59e0b', 0.8: '#ef4444' }
  }).addTo(this.heatMap);
}
```

**Difficulty:** ⭐⭐ Medium  
**Impact:** High — gives admins instant spatial awareness of problem hotspots

---

## 14. SLA Countdown Timer per Complaint

**What it does:** Solver complaint list shows a countdown badge next to each complaint — "3 days left" or "Overdue by 2 days" — based on `estimatedResDate`. Overdue complaints turn red and are auto-sorted to the top.

**Already exists:**
- `IComplaint` has `estimatedResDate` (set via `setEstimatedDate()`)
- `SolverComplaintListComponent` already fetches the complaint list

**What needs to be built:**

*Frontend — create a pipe:*

Create `src/app/shared/pipes/sla.pipe.ts`:
```typescript
@Pipe({ name: 'sla' })
export class SlaPipe implements PipeTransform {
  transform(estDate: string | undefined): { label: string; status: 'ok' | 'warning' | 'overdue' } {
    if (!estDate) return { label: 'No deadline set', status: 'ok' };
    const diff = Math.ceil(
      (new Date(estDate).getTime() - Date.now()) / (1000 * 60 * 60 * 24)
    );
    if (diff < 0)  return { label: `Overdue by ${Math.abs(diff)}d`, status: 'overdue' };
    if (diff <= 2) return { label: `${diff}d left`, status: 'warning' };
    return { label: `${diff}d left`, status: 'ok' };
  }
}
```

In solver complaint list HTML:
```html
<ng-container *ngIf="complaint.estimatedResDate | sla as sla">
  <span class="sla-badge sla-badge--{{ sla.status }}">
    <i class="bi bi-alarm"></i> {{ sla.label }}
  </span>
</ng-container>
```

CSS:
```css
.sla-badge--ok      { background: var(--fmc-success-light); color: var(--fmc-success); }
.sla-badge--warning { background: var(--fmc-warning-light); color: var(--fmc-warning); }
.sla-badge--overdue { background: var(--fmc-danger-light);  color: var(--fmc-danger); animation: fmc-pulse-ring 1.4s ease-out infinite; }
```

**Difficulty:** ⭐ Easy  
**Impact:** High — directly improves resolution times; makes accountability visible

---

## 15. Internal Notes on Complaints

**What it does:** Solvers and Admins can add private notes to a complaint — visible only to Solver/Admin roles, never to the citizen. Useful for internal coordination, e.g. "waiting for PWG contractor to confirm."

**Already exists:**
- `IComplaintTimeline` exists but remarks are citizen-visible
- Role guards (`RoleGuard`) are set up correctly for access control

**What needs to be built:**

*Backend:*
```
POST /api/Complaint/AddInternalNote  { complaintId, solverUserId, noteText }
GET  /api/Complaint/GetInternalNotes?complaintId=x  [Solver/Admin only]
```
New `InternalNotes` table: `(NoteId, ComplaintId, CreatedByUserId, NoteText, CreatedAt)`

*Frontend — add to solver and admin complaint detail views:*
```html
<div class="internal-notes-section" *ngIf="isSolverOrAdmin">
  <h6><i class="bi bi-lock-fill"></i> Internal Notes <span class="badge-private">Private</span></h6>
  <div class="note-item" *ngFor="let note of internalNotes">
    <span class="note-author">{{ note.author?.fullName }}</span>
    <span class="note-time">{{ note.createdAt | date:'dd MMM, h:mm a' }}</span>
    <p>{{ note.noteText }}</p>
  </div>
  <textarea [(ngModel)]="newNote" class="fmc-input" rows="2"
            placeholder="Add a private note..."></textarea>
  <button class="btn-secondary" (click)="addNote()" [disabled]="!newNote.trim()">
    Add Note
  </button>
</div>
```

**Difficulty:** ⭐⭐ Medium  
**Impact:** High — essential for real-world team coordination; one of the most-requested features in issue-tracking tools

---

## 16. Bulk Status Update (Solver)

**What it does:** Solver complaint list gets checkboxes + a floating bar to mark multiple complaints as "In Progress" or "Resolved" at once. Especially useful when a solver handles a cluster of related issues.

**Already exists:**
- `updateStatus()` in `ComplaintService` works per-complaint
- Solver complaint list has the full `IComplaint[]` array loaded

**What needs to be built:**

Very similar to Feature #11 but scoped to the Solver role. Reuse the same `BulkUpdateStatus` backend endpoint. Frontend implementation mirrors the admin bulk action UI but in `solver-complaint-list.component.ts`.

Key difference: Solver can only update complaints assigned to their department (`deptId`), enforced server-side.

**Difficulty:** ⭐ Easy (reuses backend from #11)  
**Impact:** Medium — saves time for high-volume solvers

---

## 17. Transparency Portal (Public, No Login Required)

**What it does:** A new public route `/public/complaints` shows all complaints as a read-only searchable, filterable feed — no login required. Citizens can see what others are reporting. This builds public trust and attracts new users.

**Already exists:**
- `getLocalityFeed()` and `search()` in `ComplaintService` can be made public
- `PublicLayoutComponent` already exists for unauthenticated pages
- `ComplaintCardComponent` is fully reusable

**What needs to be built:**

*Backend — make one endpoint anonymous:*

Remove `[Authorize]` from `GetLocalityFeed` and `Search` endpoints, or create a dedicated public endpoint:
```
GET /api/Public/GetPublicFeed?localityId=x&keyword=y&status=z&page=1&pageSize=20
```

*Frontend — new component:*

Add route to `app-routing.module.ts`:
```typescript
{ path: 'transparency', component: TransparencyPortalComponent }
```

New `src/app/public/transparency/transparency-portal.component.html`:
```html
<div class="transparency-header">
  <h2>Civic Complaints Tracker</h2>
  <p>All complaints are publicly visible. <a routerLink="/register/citizen">Register</a> to raise your own.</p>
</div>
<div class="filter-bar">
  <input [(ngModel)]="keyword" (ngModelChange)="search()" placeholder="Search complaints..." class="fmc-input">
  <select [(ngModel)]="statusFilter" (change)="search()" class="fmc-input">
    <option value="">All Statuses</option>
    <option *ngFor="let s of statuses" [value]="s">{{ s }}</option>
  </select>
</div>
<app-complaint-card *ngFor="let c of complaints" [complaint]="c" [readonly]="true">
</app-complaint-card>
```

Add a "View Public Feed" link to the landing page navbar.

**Difficulty:** ⭐⭐ Medium  
**Impact:** Very high — the single biggest trust-builder for a civic tech platform; also great for SEO

---

## 18. PWA — Offline Support & Install Prompt

**What it does:** Citizens can install FixMyCity as a home-screen app (Android/iOS). Basic pages load offline via service worker caching. An unobtrusive "Install App" banner appears after 3 visits.

**Already exists:**
- Angular 15 has `@angular/service-worker` available
- The app already uses JWT (stored in session), compatible with service worker

**What needs to be built:**

*1. Add service worker:*
```bash
ng add @angular/pwa
```
This automatically creates `ngsw-config.json`, updates `app.module.ts`, and adds `manifest.webmanifest`.

*2. Configure `ngsw-config.json` for key routes:*
```json
{
  "index": "/index.html",
  "assetGroups": [
    { "name": "app", "installMode": "prefetch",
      "resources": { "files": ["/favicon.ico", "/index.html", "/*.css", "/*.js"] }
    }
  ],
  "dataGroups": [
    { "name": "complaints-api", "urls": ["/api/Complaint/**"],
      "cacheConfig": { "maxSize": 100, "maxAge": "30m", "strategy": "freshness" }
    }
  ]
}
```

*3. Add install banner component:*
```typescript
// src/app/shared/components/install-prompt/install-prompt.component.ts
@Component({ selector: 'app-install-prompt', ... })
export class InstallPromptComponent {
  deferredPrompt: any;
  showBanner = false;

  @HostListener('window:beforeinstallprompt', ['$event'])
  onBeforeInstall(e: Event) {
    e.preventDefault();
    this.deferredPrompt = e;
    const visits = parseInt(localStorage.getItem('fmc_visits') ?? '0', 10) + 1;
    localStorage.setItem('fmc_visits', String(visits));
    if (visits >= 3) this.showBanner = true;
  }

  install() {
    this.deferredPrompt?.prompt();
    this.showBanner = false;
  }
}
```

**Difficulty:** ⭐⭐ Medium  
**Impact:** High — dramatically improves retention; makes the app feel native on mobile

---

## 19. Forgot Password / Email Verification

**What it does:** A "Forgot Password?" link on the login page triggers an email with a reset link. New citizen accounts require email verification before first login.

**Already exists:**
- Auth flow has Login, Register, RefreshToken endpoints
- `ILoginResponse` and `IRegisterCitizenRequest` exist
- Citizen registration exists at `/register/citizen`

**What needs to be built:**

*Backend (.NET API):*
```
POST /api/Auth/ForgotPassword   { email }
POST /api/Auth/ResetPassword    { token, newPassword }
POST /api/Auth/VerifyEmail      { token }
GET  /api/Auth/ResendVerification?email=x
```
Use ASP.NET Core Data Protection tokens or GUID tokens stored in DB with expiry.

*Frontend — new Angular components:*

`src/app/auth/forgot-password/forgot-password.component.html`:
```html
<div class="login-wrapper">
  <div class="login-card">
    <div class="login-logo">
      <div class="login-logo-icon"><i class="bi bi-lock-fill"></i></div>
      <h2>Reset Password</h2>
    </div>
    <div class="fmc-form-group">
      <label class="fmc-label">Email Address</label>
      <input [(ngModel)]="email" type="email" class="fmc-input"
             placeholder="Enter your registered email" />
    </div>
    <button class="lp-btn-primary w-100" (click)="submit()" [disabled]="loading">
      <span *ngIf="loading"><i class="bi bi-arrow-repeat"></i></span>
      Send Reset Link
    </button>
    <a routerLink="/login" class="d-block text-center mt-3">Back to Login</a>
  </div>
</div>
```

Add routes:
```typescript
{ path: 'forgot-password', component: ForgotPasswordComponent },
{ path: 'reset-password',  component: ResetPasswordComponent },
{ path: 'verify-email',    component: VerifyEmailComponent },
```

Update login component — add link below the form:
```html
<a routerLink="/forgot-password" class="forgot-link">Forgot password?</a>
```

**Difficulty:** ⭐⭐⭐ Hard (email infrastructure needed)  
**Impact:** Critical — without this, any citizen who forgets their password is permanently locked out

---

## 20. Activity Feed on Profile

**What it does:** The User Profile page gets a chronological "Activity" tab showing everything the user has done — complaints submitted, points earned, certificates issued, upvotes given, comments posted. Replaces the current static profile which only shows editable fields.

**Already exists:**
- `IComplaintTimeline` has actor data
- `ICertificate` and `IUserPoint` are fetchable per user
- `INotification` partially covers this but is notification-only
- `UserProfileComponent` exists but only shows form fields

**What needs to be built:**

*Backend — one aggregate endpoint:*
```
GET /api/User/GetActivityFeed?userId=x&page=1&pageSize=20
```
Returns unified list of: `{ eventType, description, relatedId, createdAt }` sorted by date descending. Sources: `ComplaintTimeline`, `UserPoints`, `Certificates`, `Notifications`.

*Frontend — tab in `user-profile.component.html`:*
```html
<ul class="nav nav-tabs mt-4">
  <li class="nav-item"><a class="nav-link" [class.active]="tab==='profile'" (click)="tab='profile'">Profile</a></li>
  <li class="nav-item"><a class="nav-link" [class.active]="tab==='activity'" (click)="tab='activity'">Activity</a></li>
</ul>

<div *ngIf="tab === 'activity'" class="activity-feed">
  <div class="activity-item" *ngFor="let event of activityFeed">
    <div class="activity-icon activity-icon--{{ event.eventType }}">
      <i class="bi" [ngClass]="iconForEvent(event.eventType)"></i>
    </div>
    <div class="activity-body">
      <p class="activity-desc">{{ event.description }}</p>
      <span class="activity-time">{{ event.createdAt | date:'dd MMM yyyy, h:mm a' }}</span>
    </div>
  </div>
</div>
```

**Difficulty:** ⭐⭐ Medium  
**Impact:** Medium — makes profiles feel alive; increases the "identity" aspect of civic participation

---

## Quick Reference — Effort vs Impact Matrix

| # | Feature | Difficulty | Impact | Backend Needed? |
|---|---|---|---|---|
| 2 | Complaint Drafts | ⭐ Easy | 🔥 High | No |
| 10 | Export to CSV | ⭐ Easy | 🔥 High | No |
| 14 | SLA Countdown | ⭐ Easy | 🔥 High | No |
| 4 | Share Complaint | ⭐ Easy | ✅ Medium | No |
| 8 | QR Code | ⭐ Easy | ✅ Medium | No |
| 16 | Solver Bulk Update | ⭐ Easy | ✅ Medium | Reuse #11 |
| 3 | Before/After Photo | ⭐⭐ Medium | 🔥 High | No |
| 5 | Near Me Complaints | ⭐⭐ Medium | 🔥 High | Optional |
| 9 | Analytics Charts | ⭐⭐ Medium | 🔥🔥 Very High | 1 endpoint |
| 11 | Bulk Admin Actions | ⭐⭐ Medium | 🔥 High | 1 endpoint |
| 12 | Manual Assignment | ⭐⭐ Medium | ✅ Medium | 1 endpoint |
| 13 | Locality Heatmap | ⭐⭐ Medium | 🔥 High | No |
| 15 | Internal Notes | ⭐⭐ Medium | 🔥 High | 2 endpoints |
| 17 | Transparency Portal | ⭐⭐ Medium | 🔥🔥 Very High | 1 endpoint |
| 18 | PWA / Install | ⭐⭐ Medium | 🔥 High | No |
| 20 | Activity Feed | ⭐⭐ Medium | ✅ Medium | 1 endpoint |
| 1 | Complaint Upvote | ⭐⭐ Medium | 🔥 High | 2 endpoints |
| 6 | Appeal Rejection | ⭐⭐⭐ Hard | 🔥 High | 3 endpoints |
| 7 | Comments/Discussion | ⭐⭐⭐ Hard | 🔥🔥 Very High | 2 endpoints |
| 19 | Forgot Password | ⭐⭐⭐ Hard | 🚨 Critical | 4 endpoints |

---

## Recommended Starting Order

**Week 1 — No backend needed, immediate value:**
Features 2 (Drafts) → 10 (CSV Export) → 14 (SLA Timer) → 4 (Share) → 8 (QR)

**Week 2 — Small backend additions, high visibility:**
Features 9 (Analytics Charts) → 17 (Transparency Portal) → 13 (Heatmap) → 3 (Before/After Photos)

**Week 3 — Medium complexity, builds the community layer:**
Features 1 (Upvotes) → 15 (Internal Notes) → 5 (Near Me) → 11 (Bulk Actions)

**Week 4+ — Significant backend work, transformative features:**
Features 7 (Comments) → 19 (Forgot Password) → 6 (Appeals) → 18 (PWA)
