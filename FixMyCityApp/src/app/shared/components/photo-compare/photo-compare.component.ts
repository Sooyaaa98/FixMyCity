// src/app/shared/components/photo-compare/photo-compare.component.ts
//
// Phase 8 (§3) — Before / After photo viewer for resolved complaints.
// Renders the submission photo and resolution photo side-by-side with a
// draggable "swipe handle" overlay so the citizen can drag a vertical
// divider left/right to compare the two states.
//
// The component degrades gracefully:
//   - If either photo is missing it shows the available one without a slider.
//   - If both are missing it renders nothing (parent should hide the card).

import {
  Component, ElementRef, HostListener, Input, OnChanges, OnInit,
  SimpleChanges, ViewChild
} from '@angular/core';
import { environment } from '../../../../environments/environment';
import { IComplaintAttachment } from '../../../fmc-interfaces/complaint.interface';

@Component({
  selector: 'app-photo-compare',
  templateUrl: './photo-compare.component.html',
  styleUrls: ['./photo-compare.component.css'],
})
export class PhotoCompareComponent implements OnInit, OnChanges {

  /** All attachments for the complaint (Complaint + Resolution). */
  @Input() attachments: IComplaintAttachment[] = [];

  /** 0 → 100 slider position (percent from left). */
  sliderPct = 50;

  beforeUrl: string | null = null;
  afterUrl:  string | null = null;

  @ViewChild('frame', { static: false }) frame?: ElementRef<HTMLDivElement>;
  private dragging = false;

  ngOnInit(): void { this.recompute(); }
  ngOnChanges(c: SimpleChanges): void {
    if (c['attachments']) this.recompute();
  }

  private recompute(): void {
    // The DB stores `Complaint` / `Resolution` (TitleCase), but some endpoints
    // historically returned lower-case. Match case-insensitively to be safe.
    const norm = (s?: string) => (s ?? '').toLowerCase();
    const before = this.attachments.find(a => norm(a.attachmentType) === 'complaint');
    const after  = this.attachments.find(a => norm(a.attachmentType) === 'resolution');
    this.beforeUrl = before ? this.toUrl(before.filePath) : null;
    this.afterUrl  = after  ? this.toUrl(after.filePath)  : null;
  }

  private toUrl(filePath: string): string {
    if (!filePath) return '';
    if (filePath.startsWith('http')) return filePath;
    // The API exposes uploads via /uploads/<basename>.
    return `${environment.apiBaseUrl}/uploads/${encodeURIComponent(filePath)}`;
  }

  // ── Drag handle ─────────────────────────────────────────────────────────

  onPointerDown(ev: PointerEvent): void {
    this.dragging = true;
    (ev.target as HTMLElement).setPointerCapture?.(ev.pointerId);
    this.updateFromPointer(ev);
  }

  onPointerMove(ev: PointerEvent): void {
    if (!this.dragging) return;
    this.updateFromPointer(ev);
  }

  onPointerUp(ev: PointerEvent): void {
    this.dragging = false;
    (ev.target as HTMLElement).releasePointerCapture?.(ev.pointerId);
  }

  /** Keyboard accessibility — Left/Right adjusts the slider. */
  @HostListener('keydown.arrowLeft',  ['$event'])
  @HostListener('keydown.arrowRight', ['$event'])
  onKey(ev: KeyboardEvent): void {
    if (ev.key === 'ArrowLeft')  this.sliderPct = Math.max(0,   this.sliderPct - 4);
    if (ev.key === 'ArrowRight') this.sliderPct = Math.min(100, this.sliderPct + 4);
  }

  private updateFromPointer(ev: PointerEvent): void {
    const el = this.frame?.nativeElement;
    if (!el) return;
    const rect = el.getBoundingClientRect();
    const x    = Math.min(rect.width, Math.max(0, ev.clientX - rect.left));
    this.sliderPct = Math.round((x / rect.width) * 100);
  }
}
