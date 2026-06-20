// src/app/shared/directives/ripple.directive.ts
// Phase 6 — Material-style ink ripple on click. Self-cleans on animationend.
// Honours prefers-reduced-motion (no ripple) so the click still fires the
// underlying handler immediately for those users.

import { Directive, ElementRef, HostListener, OnInit } from '@angular/core';

@Directive({ selector: '[fmcRipple]' })
export class RippleDirective implements OnInit {

  private enabled = true;

  constructor(private el: ElementRef<HTMLElement>) {}

  ngOnInit(): void {
    if (window.matchMedia?.('(prefers-reduced-motion: reduce)').matches) {
      this.enabled = false;
      return;
    }
    const native = this.el.nativeElement;
    // Ripple positions absolutely inside the host; require the host to clip.
    const pos = getComputedStyle(native).position;
    if (pos === 'static' || pos === '') native.style.position = 'relative';
    native.style.overflow = 'hidden';
  }

  @HostListener('click', ['$event'])
  onClick(e: MouseEvent) {
    if (!this.enabled) return;
    const btn = this.el.nativeElement;
    // Remove any existing ripple so rapid clicks don't pile up.
    btn.querySelector('.fmc-ripple-span')?.remove();

    const d = Math.max(btn.clientWidth, btn.clientHeight);
    const rect = btn.getBoundingClientRect();
    const span = document.createElement('span');
    span.className = 'fmc-ripple-span';
    span.style.cssText = `
      position: absolute;
      width: ${d}px; height: ${d}px;
      left: ${e.clientX - rect.left - d / 2}px;
      top:  ${e.clientY - rect.top  - d / 2}px;
      border-radius: 50%;
      background: rgba(255, 255, 255, 0.38);
      animation: fmc-ripple 0.55s ease-out forwards;
      pointer-events: none;
    `;
    btn.appendChild(span);
    span.addEventListener('animationend', () => span.remove(), { once: true });
  }
}
