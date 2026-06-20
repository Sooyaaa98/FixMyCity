// src/app/shared/components/trend-chart/trend-chart.component.ts
//
// Phase 8 (§9) — minimalist SVG line/bar trend chart.
//
// Why SVG instead of a charting library?
//   - The dashboard needs ONE chart. Pulling in Chart.js / ngx-charts would
//     add ~200 KB to the bundle for a single widget.
//   - The data shape is tiny (~30 daily rows). A plain SVG renders instantly,
//     scales with the container, and styles via CSS variables for dark mode.
//
// API: bind `[data]="rows"` where rows = [{ date, count, resolved }, …].

import {
  Component, ElementRef, Input, OnChanges, SimpleChanges, ViewChild, AfterViewInit
} from '@angular/core';

interface ITrendRow { date: string; count: number; resolved: number; }

@Component({
  selector: 'app-trend-chart',
  templateUrl: './trend-chart.component.html',
  styleUrls: ['./trend-chart.component.css'],
})
export class TrendChartComponent implements OnChanges, AfterViewInit {

  @Input() data: ITrendRow[] = [];
  @Input() height = 240;
  @Input() title  = 'Complaint volume';

  @ViewChild('host', { static: true }) host!: ElementRef<HTMLDivElement>;

  // Rendered geometry (computed in build())
  width = 600;
  totalPath = '';
  resolvedPath = '';
  axisLabels: { x: number; text: string }[] = [];
  yLabels:    { y: number; text: string }[] = [];
  bars:       { x: number; y: number; h: number; resolved: boolean }[] = [];
  maxValue   = 0;
  resolvedRate = 0;

  ngAfterViewInit(): void {
    // Initial measure
    this.width = Math.max(320, this.host.nativeElement.clientWidth || 600);
    this.build();
    // Re-render on container resize so the chart stays sharp on the admin
    // dashboard's responsive grid.
    if ('ResizeObserver' in window) {
      const ro = new ResizeObserver(() => {
        this.width = Math.max(320, this.host.nativeElement.clientWidth || 600);
        this.build();
      });
      ro.observe(this.host.nativeElement);
    }
  }

  ngOnChanges(c: SimpleChanges): void {
    if (c['data']) this.build();
  }

  private build(): void {
    const rows = this.data ?? [];
    if (rows.length === 0) {
      this.totalPath = this.resolvedPath = '';
      this.axisLabels = []; this.yLabels = []; this.bars = [];
      this.maxValue = 0; this.resolvedRate = 0;
      return;
    }

    const padL = 36, padR = 12, padT = 12, padB = 22;
    const innerW = this.width - padL - padR;
    const innerH = this.height - padT - padB;

    const max = Math.max(1, ...rows.map(r => r.count));
    const step = innerW / Math.max(1, rows.length - 1);

    // Lines
    const xy = (i: number, v: number) => ({
      x: padL + i * step,
      y: padT + innerH - (v / max) * innerH,
    });
    const linePath = (key: 'count' | 'resolved') => rows
      .map((r, i) => {
        const p = xy(i, (r as any)[key]);
        return `${i === 0 ? 'M' : 'L'}${p.x.toFixed(1)},${p.y.toFixed(1)}`;
      })
      .join(' ');

    this.totalPath    = linePath('count');
    this.resolvedPath = linePath('resolved');

    // Light bars in the background for the total — gives an "area-ish" feel
    // without a big fill region.
    this.bars = rows.map((r, i) => {
      const p = xy(i, r.count);
      return { x: p.x - 1.5, y: p.y, h: padT + innerH - p.y, resolved: false };
    });

    // X-axis labels: 5 evenly-spaced ticks
    const ticks = Math.min(rows.length, 5);
    this.axisLabels = Array.from({ length: ticks }, (_, t) => {
      const i = Math.round((t / (ticks - 1 || 1)) * (rows.length - 1));
      return { x: padL + i * step, text: rows[i].date?.slice(5, 10) ?? '' };  // MM-DD
    });

    // Y-axis labels: 0, 50% max, max
    this.yLabels = [0, Math.round(max / 2), max].map(v => ({
      y: padT + innerH - (v / max) * innerH,
      text: String(v),
    }));

    // Summary stats
    const totalSum    = rows.reduce((s, r) => s + r.count, 0);
    const resolvedSum = rows.reduce((s, r) => s + r.resolved, 0);
    this.maxValue     = max;
    this.resolvedRate = totalSum > 0 ? Math.round((resolvedSum / totalSum) * 100) : 0;
  }
}
