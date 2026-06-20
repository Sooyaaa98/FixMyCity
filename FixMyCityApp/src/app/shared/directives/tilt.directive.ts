// src/app/shared/directives/tilt.directive.ts
// Phase 6 — Subtle 3D mouse-tracking tilt for marketing cards.
// No-op for users with prefers-reduced-motion: reduce or coarse pointers
// (touch devices) so it never fights mobile UX.

import { Directive, ElementRef, HostListener, OnDestroy, OnInit } from '@angular/core';

@Directive({ selector: '[fmcTilt]' })
export class TiltDirective implements OnInit, OnDestroy {
  private enabled = true;

  constructor(private el: ElementRef<HTMLElement>) {}

  ngOnInit(): void {
    const reduce = window.matchMedia?.('(prefers-reduced-motion: reduce)').matches;
    const coarse = window.matchMedia?.('(pointer: coarse)').matches;
    if (reduce || coarse) {
      this.enabled = false;
      return;
    }
    const native = this.el.nativeElement;
    native.style.transition = 'transform 0.08s ease, box-shadow 0.08s ease';
    native.style.willChange = 'transform';
  }

  @HostListener('mousemove', ['$event'])
  onMove(e: MouseEvent) {
    if (!this.enabled) return;
    const el = this.el.nativeElement;
    const { left, top, width, height } = el.getBoundingClientRect();
    const x = (e.clientX - left) / width  - 0.5;
    const y = (e.clientY - top)  / height - 0.5;
    el.style.transform =
      `perspective(700px) rotateY(${x * 10}deg) rotateX(${-y * 10}deg) scale(1.025)`;
    el.style.boxShadow = `${-x * 8}px ${y * 8}px 24px rgba(0, 0, 0, 0.12)`;
  }

  @HostListener('mouseleave')
  onLeave() {
    if (!this.enabled) return;
    const el = this.el.nativeElement;
    el.style.transform = '';
    el.style.boxShadow = '';
  }

  ngOnDestroy(): void {
    // Reset any inline styles we set so future ng-content swaps don't inherit.
    const el = this.el.nativeElement;
    el.style.transform = '';
    el.style.boxShadow = '';
    el.style.willChange = '';
  }
}
