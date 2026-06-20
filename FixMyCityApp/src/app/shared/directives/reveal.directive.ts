// src/app/shared/directives/reveal.directive.ts
// Phase 6 — Scroll-triggered fade-up reveal via IntersectionObserver.
// Adds `.fmc-reveal` to the host element and toggles `.visible` once the
// element enters the viewport. Optional `[revealDelay]` (1..4) applies one
// of the corresponding `.fmc-reveal-delay-N` classes for staggered groups.

import { Directive, ElementRef, Input, OnInit, OnDestroy } from '@angular/core';

@Directive({ selector: '[fmcReveal]' })
export class RevealDirective implements OnInit, OnDestroy {
  @Input() revealDelay: 0 | 1 | 2 | 3 | 4 = 0;

  private observer: IntersectionObserver | null = null;

  constructor(private el: ElementRef<HTMLElement>) {}

  ngOnInit(): void {
    const native = this.el.nativeElement;
    native.classList.add('fmc-reveal');
    if (this.revealDelay) {
      native.classList.add(`fmc-reveal-delay-${this.revealDelay}`);
    }

    // Reduced-motion users see the element in its visible state immediately.
    if (window.matchMedia?.('(prefers-reduced-motion: reduce)').matches) {
      native.classList.add('visible');
      return;
    }

    this.observer = new IntersectionObserver(
      ([entry]) => {
        if (entry.isIntersecting) {
          native.classList.add('visible');
          this.observer?.disconnect();
          this.observer = null;
        }
      },
      { threshold: 0.12 }
    );
    this.observer.observe(native);
  }

  ngOnDestroy(): void {
    this.observer?.disconnect();
    this.observer = null;
  }
}
