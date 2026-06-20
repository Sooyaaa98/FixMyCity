// src/app/shared/components/nearby-complaints/nearby-complaints.component.ts
//
// Phase 8 (§5) — "Issues near you" widget.
//
// Asks for browser geolocation once, then hits /api/Complaint/GetNearby
// (Haversine-filtered server-side in usp_GetNearbyComplaints) and renders the
// closest open complaints sorted by distance ascending.
//
// Privacy / UX notes:
//   - We don't pre-request the location. The user clicks "Use my location"
//     so they understand why the prompt is appearing.
//   - We never store the location — it's just sent to the API once per click.
//   - The radius dropdown lets the user widen the search instead of building
//     a slider, keeping the UI lightweight.

import { Component, Input } from '@angular/core';
import { Router } from '@angular/router';
import { ComplaintService } from '../../../fmc-services/complaint.service';
import { ToastService }     from '../../../fmc-services/toast.service';

interface INearbyRow {
  complaintId:   number;
  title:         string;
  status:        string;
  criticality:   string;
  submittedAt:   string;
  categoryName?: string;
  localityName?: string;
  distanceKm:    number;
}

@Component({
  selector: 'app-nearby-complaints',
  templateUrl: './nearby-complaints.component.html',
  styleUrls: ['./nearby-complaints.component.css'],
})
export class NearbyComplaintsComponent {

  /** When provided we link a tapped row into the appropriate detail route
   *  (citizen-side default). */
  @Input() detailRoutePrefix = '/citizen/complaints';

  loading = false;
  hasResult = false;
  errorMessage = '';
  rows: INearbyRow[] = [];

  /** Currently-picked radius in kilometres. */
  radiusKm: 1 | 2 | 5 | 10 = 2;

  /** Last GPS coordinates we sent to the server — used only to allow a
   *  "Refresh" button to repeat the search with a new radius. */
  private lastLat: number | null = null;
  private lastLng: number | null = null;

  constructor(
    private complaintService: ComplaintService,
    private toast:            ToastService,
    private router:           Router,
  ) {}

  useMyLocation(): void {
    if (!('geolocation' in navigator)) {
      this.errorMessage = 'Your browser does not support geolocation.';
      return;
    }
    this.loading = true;
    this.errorMessage = '';
    navigator.geolocation.getCurrentPosition(
      (pos) => {
        this.lastLat = pos.coords.latitude;
        this.lastLng = pos.coords.longitude;
        this.fetchNearby();
      },
      (err) => {
        this.loading = false;
        const msg = err.code === err.PERMISSION_DENIED
          ? 'Location permission was denied. Please enable it in your browser settings.'
          : err.code === err.TIMEOUT
            ? 'Location lookup timed out. Please try again.'
            : 'Could not read your location.';
        this.errorMessage = msg;
      },
      { enableHighAccuracy: false, timeout: 8000, maximumAge: 300_000 }
    );
  }

  changeRadius(km: 1 | 2 | 5 | 10): void {
    this.radiusKm = km;
    if (this.lastLat != null && this.lastLng != null) this.fetchNearby();
  }

  private fetchNearby(): void {
    if (this.lastLat == null || this.lastLng == null) return;
    this.loading = true;
    this.complaintService.getNearby(this.lastLat, this.lastLng, this.radiusKm, 30)
      .subscribe({
        next: (rows) => {
          this.rows      = (rows ?? []) as INearbyRow[];
          this.hasResult = true;
          this.loading   = false;
        },
        error: () => {
          this.errorMessage = 'Could not load nearby complaints.';
          this.loading      = false;
        }
      });
  }

  goTo(id: number): void {
    this.router.navigate([this.detailRoutePrefix, id]);
  }
}
