// src/app/shared/components/map-view/map-view.component.ts
// Leaflet is loaded via CDN in index.html — no npm install needed.
// Declare types via declare const L.

import {
  Component, OnInit, OnDestroy, OnChanges,
  Input, SimpleChanges, AfterViewInit, ElementRef, ViewChild
} from '@angular/core';
import { IComplaint } from '../../../fmc-interfaces/complaint.interface';

declare const L: any;

const STATUS_COLORS: Record<string, string> = {
  'Submitted':   '#f59e0b',
  'In Progress': '#2563eb',
  'Resolved':    '#10b981',
  'Rejected':    '#ef4444',
  'Escalated':   '#7c3aed',
  'Re-opened':   '#f97316',
  'Linked':      '#64748b',
};

@Component({
  selector: 'app-map-view',
  templateUrl: './map-view.component.html',
  styleUrls: ['./map-view.component.css']
})
export class MapViewComponent implements OnInit, AfterViewInit, OnChanges, OnDestroy {

  @ViewChild('mapContainer') mapContainer!: ElementRef<HTMLDivElement>;

  @Input() complaints: IComplaint[] = [];
  @Input() height = '420px';
  /**
   * Phase 8 (§13) — when true, overlays a semi-transparent gradient circle
   * for every complaint. Where many circles overlap, the additive opacity
   * creates a visual density "heatmap" without needing the leaflet.heat plugin.
   * Cheap, no extra dep, and good enough for ≤ a few hundred points.
   */
  @Input() heatmap = false;

  private map: any;
  private markersLayer: any;
  private heatLayer: any;
  private mapReady = false;

  // Default: Bengaluru city centre
  readonly legendEntries = [
    { label: 'Submitted',   color: '#f59e0b' },
    { label: 'In Progress', color: '#2563eb' },
    { label: 'Resolved',    color: '#10b981' },
    { label: 'Rejected',    color: '#ef4444' },
    { label: 'Escalated',   color: '#7c3aed' },
  ];

  get geoCount(): number {
    return this.complaints.filter(c => c.latitude != null && c.longitude != null).length;
  }

  private readonly DEFAULT_LAT = 12.9716;
  private readonly DEFAULT_LNG = 77.5946;
  private readonly DEFAULT_ZOOM = 12;

  ngOnInit(): void {}

  ngAfterViewInit(): void {
    // Short delay to ensure the container is rendered
    setTimeout(() => this.initMap(), 50);
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (!this.mapReady) return;
    if (changes['complaints'] || changes['heatmap']) {
      this.renderMarkers();
      this.renderHeatmap();
    }
  }

  ngOnDestroy(): void {
    if (this.map) {
      this.map.remove();
      this.map = null;
    }
  }

  private initMap(): void {
    if (typeof L === 'undefined') {
      console.warn('[MapView] Leaflet not loaded. Add CDN to index.html.');
      return;
    }

    this.map = L.map(this.mapContainer.nativeElement, {
      center: [this.DEFAULT_LAT, this.DEFAULT_LNG],
      zoom: this.DEFAULT_ZOOM
    });

    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
      attribution: '© OpenStreetMap contributors',
      maxZoom: 18
    }).addTo(this.map);

    this.markersLayer = L.layerGroup().addTo(this.map);
    this.heatLayer    = L.layerGroup().addTo(this.map);

    this.mapReady = true;
    this.renderMarkers();
    this.renderHeatmap();
  }

  // ── Phase 8 (§13) — Heatmap overlay ─────────────────────────────────────
  // Implemented with overlapping low-opacity radial circles so we get the
  // intended density effect without dragging in leaflet.heat. The radius is
  // expressed in metres so it auto-scales with zoom — what the user sees as
  // a "blob" is always ~250 m on the ground.
  private renderHeatmap(): void {
    if (!this.heatLayer) return;
    this.heatLayer.clearLayers();
    if (!this.heatmap) return;

    const valid = this.complaints.filter(c => c.latitude != null && c.longitude != null);
    valid.forEach(c => {
      // Hue derived from criticality so the heatmap doubles as a severity map.
      const colour =
        c.criticality === 'Critical' ? '#dc2626'
      : c.criticality === 'High'     ? '#f97316'
      : c.criticality === 'Medium'   ? '#facc15'
      : '#22c55e';

      const blob = L.circle([c.latitude, c.longitude], {
        radius: 250,
        color: colour,
        weight: 0,
        fillColor: colour,
        fillOpacity: 0.25,    // overlapping circles compound to ~1.0
        interactive: false,
      });
      this.heatLayer.addLayer(blob);
    });
  }

  private renderMarkers(): void {
    if (!this.markersLayer) return;
    this.markersLayer.clearLayers();

    const valid = this.complaints.filter(
      c => c.latitude != null && c.longitude != null
    );

    valid.forEach(c => {
      const color = STATUS_COLORS[c.status] ?? '#64748b';
      const icon = L.divIcon({
        className: '',
        html: `<div style="
          width:14px;height:14px;border-radius:50%;
          background:${color};border:2px solid #fff;
          box-shadow:0 1px 4px rgba(0,0,0,.35)">
        </div>`,
        iconSize: [14, 14],
        iconAnchor: [7, 7]
      });

      const marker = L.marker([c.latitude, c.longitude], { icon });
      marker.bindPopup(`
        <div style="min-width:180px">
          <strong>#${c.complaintId}</strong><br>
          ${c.title}<br>
          <span style="font-size:11px;color:#64748b">${c.status} · ${c.criticality}</span>
        </div>
      `);
      this.markersLayer.addLayer(marker);
    });

    if (valid.length > 0) {
      const bounds = L.latLngBounds(valid.map(c => [c.latitude, c.longitude]));
      this.map.fitBounds(bounds, { padding: [30, 30], maxZoom: 15 });
    }
  }

}
