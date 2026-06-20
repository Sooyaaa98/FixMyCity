// src/app/shared/components/timeline/timeline.component.ts
// Phase 6 §14 — staggered entry-into-view animation.

import { AfterViewInit, Component, ElementRef, Input, QueryList, ViewChildren } from '@angular/core';
import { IComplaintTimeline } from '../../../fmc-interfaces/complaint.interface';

@Component({
  selector: 'app-timeline',
  templateUrl: './timeline.component.html',
  styleUrls: ['./timeline.component.css']
})
export class TimelineComponent implements AfterViewInit {
  @Input() entries: IComplaintTimeline[] = [];

  @ViewChildren('timelineEntry') entryEls!: QueryList<ElementRef<HTMLElement>>;

  ngAfterViewInit(): void {
    // Respect reduced motion — show every entry immediately for those users.
    const reduce = window.matchMedia?.('(prefers-reduced-motion: reduce)').matches;
    this.entryEls.forEach((el, index) => {
      if (reduce) {
        el.nativeElement.classList.add('visible');
      } else {
        setTimeout(() => el.nativeElement.classList.add('visible'), index * 120);
      }
    });
    // If entries arrive after view-init (async load), animate them too.
    this.entryEls.changes.subscribe((q: QueryList<ElementRef<HTMLElement>>) => {
      q.forEach((el, index) => {
        if (el.nativeElement.classList.contains('visible')) return;
        if (reduce) {
          el.nativeElement.classList.add('visible');
        } else {
          setTimeout(() => el.nativeElement.classList.add('visible'), index * 120);
        }
      });
    });
  }
}
