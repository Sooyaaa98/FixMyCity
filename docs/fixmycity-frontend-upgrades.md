# FixMyCity — Frontend Upgrade Instructions for Claude Code

> **Context:** This is an Angular 15 project (`fix-my-city-app`) using Bootstrap 5, Font Awesome 4 (`fa fa-*`), and plain CSS per-component. The backend is ASP.NET Core. All file paths below are relative to `src/`.
>
> **How to use this file:** Work through each section in order. Each task is self-contained with exact file paths, complete code to add or replace, and a ✅ acceptance check. Do not skip the "Global Setup" section — everything else depends on it.

---

## Table of Contents

1. [Global Setup](#1-global-setup)
2. [Icon System — Replace Font Awesome with Bootstrap Icons](#2-icon-system)
3. [Scroll-Triggered Card Animations](#3-scroll-triggered-card-animations)
4. [Animated Stat Counters on Landing](#4-animated-stat-counters)
5. [Hero Mouse-Spotlight Effect](#5-hero-mouse-spotlight)
6. [Glassmorphism Navbar on Scroll](#6-glassmorphism-navbar)
7. [Card 3D Tilt Directive](#7-card-3d-tilt-directive)
8. [Button Ripple Directive](#8-button-ripple-directive)
9. [Skeleton Loading Screens](#9-skeleton-loading-screens)
10. [Page Route Transition Animations](#10-route-transitions)
11. [Hero Typing Animation](#11-hero-typing-animation)
12. [Animated Particle Background on Hero](#12-particle-background)
13. [Toast Slide-in Animation Upgrade](#13-toast-animation-upgrade)
14. [Timeline Stagger Animation](#14-timeline-stagger)
15. [Complaint Card Meta Chips — Icon Upgrade](#15-complaint-card-meta-chips)
16. [Scoreboard Animated Rank Badges](#16-scoreboard-rank-badges)
17. [Form Input Focus Glow](#17-form-input-focus-glow)
18. [Chatbot FAB Pulse Animation](#18-chatbot-fab-pulse)
19. [Empty State Illustration Upgrade](#19-empty-state-upgrade)
20. [Dark Mode Support](#20-dark-mode)

---

## 1. Global Setup

These changes are required before anything else. Do them first.

### 1a. Add Bootstrap Icons CDN

In `src/index.html`, add this line inside `<head>`, directly after the existing Bootstrap CSS link:

```html
<link
  rel="stylesheet"
  href="https://cdn.jsdelivr.net/npm/bootstrap-icons@1.11.3/font/bootstrap-icons.min.css"
/>
```

### 1b. Add CSS animation utilities to global stylesheet

In `src/styles.css`, append the following block at the very end of the file:

```css
/* ── Global animation utilities ──────────────────────────────────────── */
@keyframes fmc-fade-up {
  from { opacity: 0; transform: translateY(18px); }
  to   { opacity: 1; transform: translateY(0); }
}

@keyframes fmc-fade-in {
  from { opacity: 0; }
  to   { opacity: 1; }
}

@keyframes fmc-shimmer {
  0%   { background-position: -600px 0; }
  100% { background-position:  600px 0; }
}

@keyframes fmc-ripple {
  to { transform: scale(2.8); opacity: 0; }
}

@keyframes fmc-pulse-ring {
  0%   { transform: scale(1);    opacity: 0.7; }
  100% { transform: scale(1.65); opacity: 0; }
}

@keyframes fmc-count-in {
  from { opacity: 0; transform: translateY(6px); }
  to   { opacity: 1; transform: translateY(0); }
}

/* Scroll-reveal base state — applied by IntersectionObserver */
.fmc-reveal {
  opacity: 0;
  transform: translateY(18px);
  transition: opacity 0.45s ease, transform 0.45s ease;
}
.fmc-reveal.visible {
  opacity: 1;
  transform: translateY(0);
}
.fmc-reveal-delay-1 { transition-delay: 0.1s; }
.fmc-reveal-delay-2 { transition-delay: 0.2s; }
.fmc-reveal-delay-3 { transition-delay: 0.3s; }
.fmc-reveal-delay-4 { transition-delay: 0.4s; }

/* Skeleton shimmer base */
.fmc-skeleton {
  background: linear-gradient(
    90deg,
    var(--fmc-border) 25%,
    var(--fmc-bg) 50%,
    var(--fmc-border) 75%
  );
  background-size: 600px 100%;
  animation: fmc-shimmer 1.5s infinite;
  border-radius: 6px;
}
```

### 1c. Add `BrowserAnimationsModule` to AppModule

In `src/app/app.module.ts`:

```typescript
// Add to imports at top of file:
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';

// Add to the imports array in @NgModule:
BrowserAnimationsModule,
```

✅ **Check:** `ng serve` compiles without errors before proceeding.

---

## 2. Icon System

Replace all Font Awesome 4 (`fa fa-*`) icon classes with Bootstrap Icons (`bi bi-*`). Below is the complete mapping for every icon used in the project.

### 2a. Global find-and-replace mapping

Run these replacements across **all** `.html` files in `src/app/`:

| Old class | New class |
|---|---|
| `fa fa-map-marker` | `bi bi-geo-alt-fill` |
| `fa fa-home` | `bi bi-house-door-fill` |
| `fa fa-list-alt` | `bi bi-card-list` |
| `fa fa-trophy` | `bi bi-trophy-fill` |
| `fa fa-star` | `bi bi-star-fill` |
| `fa fa-certificate` | `bi bi-patch-check-fill` |
| `fa fa-tachometer` | `bi bi-speedometer2` |
| `fa fa-handshake-o` | `bi bi-handshake` |
| `fa fa-folder-open` | `bi bi-folder2-open` |
| `fa fa-paper-plane` | `bi bi-send-fill` |
| `fa fa-exclamation-triangle` | `bi bi-exclamation-triangle-fill` |
| `fa fa-list` | `bi bi-list-ul` |
| `fa fa-clipboard` | `bi bi-clipboard2-check` |
| `fa fa-users` | `bi bi-people-fill` |
| `fa fa-bell` | `bi bi-bell-fill` |
| `fa fa-user-circle-o` | `bi bi-person-circle` |
| `fa fa-user` | `bi bi-person-fill` |
| `fa fa-sign-out` | `bi bi-box-arrow-right` |
| `fa fa-sign-in` | `bi bi-box-arrow-in-right` |
| `fa fa-edit` | `bi bi-pencil-square` |
| `fa fa-search` | `bi bi-search` |
| `fa fa-user-plus` | `bi bi-person-plus-fill` |
| `fa fa-bars` | `bi bi-list` |
| `fa fa-times` | `bi bi-x-lg` |
| `fa fa-envelope` | `bi bi-envelope-fill` |
| `fa fa-lock` | `bi bi-lock-fill` |
| `fa fa-eye` | `bi bi-eye-fill` |
| `fa fa-eye-slash` | `bi bi-eye-slash-fill` |
| `fa fa-exclamation-circle` | `bi bi-exclamation-circle-fill` |
| `fa fa-check-circle` | `bi bi-check-circle-fill` |
| `fa fa-robot` | `bi bi-robot` |
| `fa fa-comments` | `bi bi-chat-dots-fill` |
| `fa fa-check` | `bi bi-check-lg` |
| `fa fa-info-circle` | `bi bi-info-circle-fill` |
| `fa fa-warning` | `bi bi-exclamation-triangle-fill` |
| `fa fa-upload` | `bi bi-cloud-upload-fill` |
| `fa fa-map` | `bi bi-map-fill` |
| `fa fa-location-arrow` | `bi bi-crosshair` |
| `fa fa-spinner fa-spin` | `bi bi-arrow-repeat` |
| `fa fa-refresh` | `bi bi-arrow-clockwise` |

### 2b. Update status badge icons in `toast.component.ts`

In `src/app/shared/components/toast/toast.component.ts`, update the `iconFor()` method:

```typescript
iconFor(type: string): string {
  const map: Record<string, string> = {
    success: 'bi bi-check-circle-fill',
    error:   'bi bi-x-circle-fill',
    warning: 'bi bi-exclamation-triangle-fill',
    info:    'bi bi-info-circle-fill',
  };
  return map[type] ?? 'bi bi-bell-fill';
}
```

### 2c. Update empty-state component

In `src/app/shared/components/empty-state/empty-state.component.ts`, change the icon input binding so it accepts `bi bi-*` class strings directly. The HTML template should render `<i [class]="icon"></i>` instead of `<i class="fa {{ icon }}"></i>`.

✅ **Check:** Every icon in the app renders visibly. No empty boxes or broken icons.

---

## 3. Scroll-Triggered Card Animations

Cards and list items fade-slide up as they enter the viewport.

### 3a. Create a reusable `RevealDirective`

Create new file `src/app/shared/directives/reveal.directive.ts`:

```typescript
import { Directive, ElementRef, Input, OnInit } from '@angular/core';

@Directive({ selector: '[fmcReveal]' })
export class RevealDirective implements OnInit {
  @Input() revealDelay = 0;

  constructor(private el: ElementRef) {}

  ngOnInit() {
    const native = this.el.nativeElement as HTMLElement;
    native.classList.add('fmc-reveal');
    if (this.revealDelay) {
      native.classList.add(`fmc-reveal-delay-${this.revealDelay}`);
    }

    const observer = new IntersectionObserver(
      ([entry]) => {
        if (entry.isIntersecting) {
          native.classList.add('visible');
          observer.disconnect();
        }
      },
      { threshold: 0.12 }
    );
    observer.observe(native);
  }
}
```

### 3b. Register the directive

In `src/app/app.module.ts`, import and declare `RevealDirective`:

```typescript
import { RevealDirective } from './shared/directives/reveal.directive';

// Add to declarations array:
RevealDirective,
```

### 3c. Apply to complaint cards

In `src/app/shared/components/complaint-card/complaint-card.component.html`, add `fmcReveal` to the root `<div>`:

```html
<div [class]="'fmc-card crit-' + complaint.criticality + ' complaint-card'"
     fmcReveal
     (click)="onCardClick()"
     ...>
```

### 3d. Apply to landing page step and benefit cards

In `src/app/public/landing/landing.component.html`, update both `*ngFor` loops:

```html
<!-- Step cards -->
<div class="col-12 col-sm-6 col-lg-4"
     *ngFor="let step of steps; let i = index"
     fmcReveal
     [revealDelay]="(i % 3) + 1">

<!-- Benefit cards -->
<div class="col-12 col-sm-6 col-lg-3"
     *ngFor="let b of benefits; let i = index"
     fmcReveal
     [revealDelay]="(i % 4) + 1">
```

✅ **Check:** Reload the landing page, scroll slowly — cards fade up one by one as they enter view.

---

## 4. Animated Stat Counters

The three stats on the landing hero (`10,000+`, `3–5 Days`, `50+ Depts`) count up from zero on scroll.

### 4a. Update `landing.component.ts`

Add the following method and `ViewChild` references. The component should import `AfterViewInit`, `ElementRef`, and `ViewChildren`:

```typescript
import { Component, OnInit, AfterViewInit, QueryList, ElementRef, ViewChildren } from '@angular/core';

// Inside the class, add:
@ViewChildren('statNum') statEls!: QueryList<ElementRef>;

ngAfterViewInit() {
  const observer = new IntersectionObserver(
    (entries) => {
      entries.forEach(entry => {
        if (entry.isIntersecting) {
          this.startCounter(entry.target as HTMLElement);
          observer.unobserve(entry.target);
        }
      });
    },
    { threshold: 0.5 }
  );
  this.statEls.forEach(el => observer.observe(el.nativeElement));
}

startCounter(el: HTMLElement) {
  const target = parseInt(el.dataset['target'] ?? '0', 10);
  const suffix = el.dataset['suffix'] ?? '';
  const duration = 1400;
  const start = performance.now();
  const animate = (now: number) => {
    const elapsed = Math.min(now - start, duration);
    const progress = elapsed / duration;
    const eased = 1 - Math.pow(1 - progress, 3);
    const current = Math.round(eased * target);
    el.textContent = current.toLocaleString() + suffix;
    if (elapsed < duration) requestAnimationFrame(animate);
  };
  requestAnimationFrame(animate);
}
```

### 4b. Update stat strip HTML

In `src/app/public/landing/landing.component.html`, replace the `.lp-stat-strip` block:

```html
<div class="lp-stat-strip row g-0 mt-5">
  <div class="col-4 lp-stat">
    <div class="lp-stat-num" #statNum data-target="10000" data-suffix="+">10,000+</div>
    <div class="lp-stat-label">Complaints Resolved</div>
  </div>
  <div class="col-4 lp-stat">
    <div class="lp-stat-num">3–5 Days</div>
    <div class="lp-stat-label">Average Resolution</div>
  </div>
  <div class="col-4 lp-stat">
    <div class="lp-stat-num" #statNum data-target="50" data-suffix="+">50+</div>
    <div class="lp-stat-label">Connected Agencies</div>
  </div>
</div>
```

✅ **Check:** Scroll hero stats into view — numbers animate up from 0.

---

## 5. Hero Mouse-Spotlight Effect

A subtle radial glow follows the cursor inside the hero section.

### 5a. Update `landing.component.ts`

Add imports and host listener:

```typescript
import { Component, OnInit, AfterViewInit, HostListener, ElementRef, ViewChild, QueryList, ViewChildren } from '@angular/core';

// Add ViewChild for hero:
@ViewChild('heroSection') heroRef!: ElementRef;

@HostListener('mousemove', ['$event'])
onMouseMove(e: MouseEvent) {
  if (!this.heroRef) return;
  const el = this.heroRef.nativeElement as HTMLElement;
  const { left, top } = el.getBoundingClientRect();
  el.style.setProperty('--mx', (e.clientX - left) + 'px');
  el.style.setProperty('--my', (e.clientY - top) + 'px');
}
```

### 5b. Add `#heroSection` ref and `::after` overlay

In `landing.component.html`, update the opening hero tag:

```html
<section class="lp-hero" #heroSection>
```

In `landing.component.css`, update `.lp-hero` and add the overlay:

```css
.lp-hero {
  --mx: 50%;
  --my: 50%;
  background: linear-gradient(140deg, #0f3d6e 0%, #1a6bbd 55%, #1e88e5 100%);
  color: #fff;
  padding: 80px 0 60px;
  position: relative;
  overflow: hidden;
}

.lp-hero::after {
  content: '';
  position: absolute;
  inset: 0;
  background: radial-gradient(
    320px circle at var(--mx) var(--my),
    rgba(255, 255, 255, 0.07),
    transparent 70%
  );
  pointer-events: none;
  transition: background 0.08s ease;
}
```

✅ **Check:** Move mouse around the hero section — a subtle light follows the cursor.

---

## 6. Glassmorphism Navbar on Scroll

The navbar becomes frosted glass when the page is scrolled.

### 6a. Update `navbar.component.ts`

```typescript
import { Component, HostListener, OnInit } from '@angular/core';

// Add inside class:
scrolled = false;

@HostListener('window:scroll')
onScroll() {
  this.scrolled = window.scrollY > 24;
}
```

### 6b. Update `navbar.component.html`

Change the `<nav>` opening tag to bind the `scrolled` class:

```html
<nav class="navbar navbar-expand-lg fmc-navbar sticky-top"
     [class.fmc-navbar--scrolled]="scrolled">
```

### 6c. Update `navbar.component.css`

Add the scrolled state styles:

```css
.fmc-navbar {
  transition: background 0.3s ease, backdrop-filter 0.3s ease, box-shadow 0.3s ease;
}

.fmc-navbar--scrolled {
  background: rgba(255, 255, 255, 0.72) !important;
  backdrop-filter: blur(16px);
  -webkit-backdrop-filter: blur(16px);
  box-shadow: 0 2px 24px rgba(0, 0, 0, 0.08) !important;
  border-bottom-color: rgba(226, 232, 240, 0.6) !important;
}
```

✅ **Check:** On any page with scrollable content, scroll down — navbar becomes translucent glass.

---

## 7. Card 3D Tilt Directive

Step cards and benefit cards on the landing page tilt in 3D to follow the mouse.

### 7a. Create `tilt.directive.ts`

Create `src/app/shared/directives/tilt.directive.ts`:

```typescript
import { Directive, ElementRef, HostListener } from '@angular/core';

@Directive({ selector: '[fmcTilt]' })
export class TiltDirective {
  constructor(private el: ElementRef) {
    const native = this.el.nativeElement as HTMLElement;
    native.style.transition = 'transform 0.08s ease, box-shadow 0.08s ease';
    native.style.willChange = 'transform';
  }

  @HostListener('mousemove', ['$event'])
  onMove(e: MouseEvent) {
    const el = this.el.nativeElement as HTMLElement;
    const { left, top, width, height } = el.getBoundingClientRect();
    const x = (e.clientX - left) / width  - 0.5;
    const y = (e.clientY - top)  / height - 0.5;
    el.style.transform =
      `perspective(700px) rotateY(${x * 10}deg) rotateX(${-y * 10}deg) scale(1.025)`;
    el.style.boxShadow = `${-x * 8}px ${y * 8}px 24px rgba(0,0,0,0.12)`;
  }

  @HostListener('mouseleave')
  onLeave() {
    const el = this.el.nativeElement as HTMLElement;
    el.style.transform = '';
    el.style.boxShadow = '';
  }
}
```

### 7b. Register directive

In `src/app/app.module.ts`, add to declarations:

```typescript
import { TiltDirective } from './shared/directives/tilt.directive';
// Add TiltDirective to declarations array
```

### 7c. Apply to landing step cards

In `landing.component.html`, add `fmcTilt` to step cards:

```html
<div class="lp-step-card" fmcTilt>
```

And benefit cards:

```html
<div class="lp-benefit-card text-center" fmcTilt>
```

✅ **Check:** Hover over step/benefit cards — they tilt in 3D following the mouse.

---

## 8. Button Ripple Directive

Material-style ink ripple on every primary button click.

### 8a. Create `ripple.directive.ts`

Create `src/app/shared/directives/ripple.directive.ts`:

```typescript
import { Directive, ElementRef, HostListener } from '@angular/core';

@Directive({ selector: '[fmcRipple]' })
export class RippleDirective {
  constructor(private el: ElementRef) {
    const native = this.el.nativeElement as HTMLElement;
    native.style.position = 'relative';
    native.style.overflow = 'hidden';
  }

  @HostListener('click', ['$event'])
  onClick(e: MouseEvent) {
    const btn = this.el.nativeElement as HTMLElement;
    const existing = btn.querySelector('.fmc-ripple-span');
    if (existing) existing.remove();

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
    span.addEventListener('animationend', () => span.remove());
  }
}
```

### 8b. Register directive

In `src/app/app.module.ts`:

```typescript
import { RippleDirective } from './shared/directives/ripple.directive';
// Add RippleDirective to declarations array
```

### 8c. Apply to all primary buttons

Add `fmcRipple` to:
- All `<a class="lp-btn-primary ...">` tags in `landing.component.html`
- All `<button type="submit" ...>` tags in auth forms (login, register)
- The "Raise a Complaint" submit button in `submit-complaint.component.html`

✅ **Check:** Click any primary button — a white ripple expands from the click point.

---

## 9. Skeleton Loading Screens

Replace the spinner in complaint list views with shimmer skeleton cards.

### 9a. Create `skeleton-card.component`

Create `src/app/shared/components/skeleton-card/skeleton-card.component.ts`:

```typescript
import { Component } from '@angular/core';

@Component({
  selector: 'app-skeleton-card',
  template: `
    <div class="sk-card">
      <div class="sk-row sk-row--between">
        <div class="fmc-skeleton" style="height:12px; width:60px;"></div>
        <div class="fmc-skeleton" style="height:20px; width:70px; border-radius:20px;"></div>
      </div>
      <div class="fmc-skeleton sk-title"></div>
      <div class="fmc-skeleton sk-line"></div>
      <div class="fmc-skeleton sk-line sk-line--short"></div>
      <div class="sk-chips">
        <div class="fmc-skeleton sk-chip"></div>
        <div class="fmc-skeleton sk-chip"></div>
        <div class="fmc-skeleton sk-chip"></div>
      </div>
    </div>
  `,
  styles: [`
    .sk-card {
      background: var(--fmc-surface);
      border-radius: var(--fmc-radius-lg);
      border: 1px solid var(--fmc-border);
      padding: 18px 20px;
      margin-bottom: 12px;
    }
    .sk-row { display: flex; align-items: center; margin-bottom: 12px; }
    .sk-row--between { justify-content: space-between; }
    .sk-title  { height: 16px; width: 55%; margin-bottom: 10px; }
    .sk-line   { height: 12px; width: 90%; margin-bottom: 6px; }
    .sk-line--short { width: 70%; }
    .sk-chips  { display: flex; gap: 8px; margin-top: 10px; }
    .sk-chip   { height: 22px; width: 80px; border-radius: 20px; }
  `]
})
export class SkeletonCardComponent {}
```

### 9b. Register the component

In `src/app/app.module.ts`:

```typescript
import { SkeletonCardComponent } from './shared/components/skeleton-card/skeleton-card.component';
// Add SkeletonCardComponent to declarations array
```

### 9c. Use in complaint list components

In `src/app/citizen/my-complaints/my-complaints.component.html` and any other complaint list views, replace the `<app-loading-spinner>` with:

```html
<!-- Replace this: -->
<app-loading-spinner *ngIf="isLoading"></app-loading-spinner>

<!-- With this: -->
<ng-container *ngIf="isLoading">
  <app-skeleton-card *ngFor="let i of [1,2,3,4,5]"></app-skeleton-card>
</ng-container>
```

✅ **Check:** Loading state shows shimmering card outlines instead of a spinner.

---

## 10. Route Transitions

Smooth fade+slide animation between all route navigations. `@angular/animations` is already installed.

### 10a. Create `route-animations.ts`

Create `src/app/core/route-animations.ts`:

```typescript
import { trigger, transition, style, animate, query, group } from '@angular/animations';

export const routeAnimations = trigger('routeAnimations', [
  transition('* <=> *', [
    query(':enter', [
      style({ opacity: 0, transform: 'translateY(10px)' })
    ], { optional: true }),
    group([
      query(':leave', [
        animate('160ms ease-in',
          style({ opacity: 0, transform: 'translateY(-6px)' }))
      ], { optional: true }),
      query(':enter', [
        animate('220ms 80ms ease-out',
          style({ opacity: 1, transform: 'translateY(0)' }))
      ], { optional: true }),
    ])
  ])
]);
```

### 10b. Update `app.component.ts`

```typescript
import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { routeAnimations } from './core/route-animations';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.css'],
  animations: [routeAnimations]
})
export class AppComponent {
  title = 'FixMyCityApp';

  prepareRoute(outlet: RouterOutlet) {
    return outlet?.activatedRouteData?.['animation'] ?? outlet?.activatedRoute?.snapshot?.url?.[0]?.path;
  }
}
```

### 10c. Update `app.component.html`

```html
<div [@routeAnimations]="prepareRoute(outlet)" style="min-height: 100vh;">
  <router-outlet #outlet="outlet"></router-outlet>
</div>
```

✅ **Check:** Navigate between pages — content fades out up and new content fades in from below.

---

## 11. Hero Typing Animation

The hero headline types itself character by character on page load.

### 11a. Update `landing.component.ts`

Add to the class:

```typescript
heroLines = ['Fix Your City.', 'Raise Your Voice.'];
heroDisplayed = '';
private typingTimer: ReturnType<typeof setTimeout> | null = null;

ngOnInit() {
  // ... existing ngOnInit code ...
  this.startTyping(0, 0);
}

private startTyping(lineIndex: number, charIndex: number) {
  if (lineIndex >= this.heroLines.length) return;
  const line = this.heroLines[lineIndex];

  if (charIndex <= line.length) {
    this.heroDisplayed =
      this.heroLines.slice(0, lineIndex).join('\n') +
      (lineIndex > 0 ? '\n' : '') +
      line.slice(0, charIndex);
    this.typingTimer = setTimeout(
      () => this.startTyping(lineIndex, charIndex + 1), 48
    );
  } else {
    this.typingTimer = setTimeout(
      () => this.startTyping(lineIndex + 1, 0), 320
    );
  }
}

ngOnDestroy() {
  if (this.typingTimer) clearTimeout(this.typingTimer);
}
```

### 11b. Update `landing.component.html`

Replace the static `<h1>` headline:

```html
<!-- Replace: -->
<h1 class="lp-hero-title">Fix Your City.<br>Raise Your Voice.</h1>

<!-- With: -->
<h1 class="lp-hero-title lp-hero-title--typing">
  <span [innerHTML]="heroDisplayed.replace('\n', '<br>')"></span>
  <span class="lp-cursor" aria-hidden="true">|</span>
</h1>
```

### 11c. Add cursor styles to `landing.component.css`

```css
.lp-hero-title--typing {
  min-height: 4em;
}

.lp-cursor {
  display: inline-block;
  margin-left: 2px;
  color: rgba(255, 255, 255, 0.85);
  animation: blink 0.9s step-end infinite;
}

@keyframes blink {
  0%, 100% { opacity: 1; }
  50%       { opacity: 0; }
}
```

✅ **Check:** Landing page hero headline types out on load with a blinking cursor.

---

## 12. Particle Background

Animated floating dots with connecting lines on the hero section — a city-grid visual metaphor.

### 12a. Update `landing.component.html`

Add a `<canvas>` element as the first child of the `.lp-hero` section:

```html
<section class="lp-hero" #heroSection>
  <canvas #particleCanvas class="lp-particles" aria-hidden="true"></canvas>
  <div class="container">
    <!-- existing hero content -->
  </div>
</section>
```

### 12b. Update `landing.component.ts`

Add the canvas animation:

```typescript
@ViewChild('particleCanvas') canvasRef!: ElementRef<HTMLCanvasElement>;

private particleAnimId = 0;

ngAfterViewInit() {
  // ... existing ngAfterViewInit code (stat counters, etc.) ...
  this.initParticles();
}

private initParticles() {
  const canvas = this.canvasRef.nativeElement;
  const hero   = this.heroRef.nativeElement as HTMLElement;
  canvas.width  = hero.offsetWidth;
  canvas.height = hero.offsetHeight;
  const ctx = canvas.getContext('2d')!;

  const count = Math.floor((canvas.width * canvas.height) / 14000);
  const nodes = Array.from({ length: count }, () => ({
    x:  Math.random() * canvas.width,
    y:  Math.random() * canvas.height,
    vx: (Math.random() - 0.5) * 0.45,
    vy: (Math.random() - 0.5) * 0.45,
  }));

  const draw = () => {
    ctx.clearRect(0, 0, canvas.width, canvas.height);
    for (const n of nodes) {
      n.x += n.vx;
      n.y += n.vy;
      if (n.x < 0 || n.x > canvas.width)  n.vx *= -1;
      if (n.y < 0 || n.y > canvas.height) n.vy *= -1;

      ctx.beginPath();
      ctx.arc(n.x, n.y, 1.6, 0, Math.PI * 2);
      ctx.fillStyle = 'rgba(255,255,255,0.45)';
      ctx.fill();

      for (const m of nodes) {
        const d = Math.hypot(n.x - m.x, n.y - m.y);
        if (d < 110 && d > 0) {
          ctx.beginPath();
          ctx.moveTo(n.x, n.y);
          ctx.lineTo(m.x, m.y);
          ctx.strokeStyle = `rgba(255,255,255,${0.09 * (1 - d / 110)})`;
          ctx.lineWidth = 0.5;
          ctx.stroke();
        }
      }
    }
    this.particleAnimId = requestAnimationFrame(draw);
  };
  draw();
}

ngOnDestroy() {
  cancelAnimationFrame(this.particleAnimId);
  if (this.typingTimer) clearTimeout(this.typingTimer);
}
```

### 12c. Add canvas styles to `landing.component.css`

```css
.lp-particles {
  position: absolute;
  inset: 0;
  width: 100%;
  height: 100%;
  pointer-events: none;
  z-index: 0;
}

.lp-hero .container {
  position: relative;
  z-index: 1;
}
```

✅ **Check:** Hero shows animated floating dots with connecting lines in the background.

---

## 13. Toast Animation Upgrade

Add slide-in-from-right and slide-out-to-right animations for toasts.

### 13a. Update `toast.component.css`

Replace the existing `@keyframes fmc-toast-in` and `.fmc-toast` animation with:

```css
.fmc-toast {
  /* existing styles ... */
  animation: fmc-toast-in 0.3s cubic-bezier(0.34, 1.56, 0.64, 1) forwards;
}

.fmc-toast.leaving {
  animation: fmc-toast-out 0.22s ease-in forwards;
}

@keyframes fmc-toast-in {
  from {
    opacity: 0;
    transform: translateX(60px) scale(0.94);
  }
  to {
    opacity: 1;
    transform: translateX(0) scale(1);
  }
}

@keyframes fmc-toast-out {
  to {
    opacity: 0;
    transform: translateX(60px) scale(0.9);
    max-height: 0;
    padding: 0;
    margin: 0;
  }
}
```

### 13b. Update `toast.component.ts` dismiss method

Update the `dismiss()` method to play the leave animation first:

```typescript
dismiss(id: string) {
  const el = document.querySelector(`[data-toast-id="${id}"]`) as HTMLElement;
  if (el) {
    el.classList.add('leaving');
    el.addEventListener('animationend', () => {
      this.toastService.dismiss(id);
    }, { once: true });
  } else {
    this.toastService.dismiss(id);
  }
}
```

Add `[attr.data-toast-id]="toast.id"` to the `.fmc-toast` div in `toast.component.html`.

✅ **Check:** Toasts slide in from the right and slide out when dismissed.

---

## 14. Timeline Stagger Animation

Complaint history timeline entries animate in one by one.

### 14a. Update `timeline.component.css`

Add these styles:

```css
.timeline-entry {
  opacity: 0;
  transform: translateX(-12px);
  transition: opacity 0.35s ease, transform 0.35s ease;
}

.timeline-entry.visible {
  opacity: 1;
  transform: translateX(0);
}
```

### 14b. Update `timeline.component.ts`

```typescript
import { Component, Input, AfterViewInit, ElementRef, QueryList, ViewChildren } from '@angular/core';

@ViewChildren('timelineEntry') entries!: QueryList<ElementRef>;

ngAfterViewInit() {
  this.entries.forEach((el, index) => {
    setTimeout(() => {
      el.nativeElement.classList.add('visible');
    }, index * 120);
  });
}
```

### 14c. Add template ref to `timeline.component.html`

Add `#timelineEntry` to each entry:

```html
<div class="timeline-entry" #timelineEntry *ngFor="let entry of entries">
```

✅ **Check:** Open a complaint detail — history entries slide in left to right in sequence.

---

## 15. Complaint Card Meta Chips — Icon Upgrade

Replace the raw emoji chips with proper Bootstrap Icon + text badges.

### 15a. Update `complaint-card.component.html`

Replace the `.card-meta` section:

```html
<div class="card-meta">
  <span class="meta-item" *ngIf="complaint.locality?.localityName">
    <i class="bi bi-geo-alt-fill" aria-hidden="true"></i>
    {{ complaint.locality!.localityName }}
  </span>
  <span class="meta-item" *ngIf="complaint.category">
    <i class="bi bi-tag-fill" aria-hidden="true"></i>
    {{ complaint.category.categoryName }}
  </span>
  <span class="meta-item">
    <i class="bi bi-calendar3" aria-hidden="true"></i>
    {{ complaint.submittedAt | date: 'dd MMM yyyy' }}
  </span>
  <span class="meta-item criticality-tag">
    <i class="bi bi-lightning-charge-fill" aria-hidden="true"></i>
    {{ complaint.criticality }}
  </span>
</div>
```

### 15b. Update `complaint-card.component.css`

```css
.meta-item {
  font-size: 12px;
  color: var(--fmc-text-muted);
  background: var(--fmc-surface-2);
  padding: 3px 9px;
  border-radius: 20px;
  border: 1px solid var(--fmc-border);
  white-space: nowrap;
  display: inline-flex;
  align-items: center;
  gap: 5px;
}

.meta-item i {
  font-size: 11px;
  color: var(--fmc-primary);
}

.criticality-tag i {
  color: var(--fmc-accent);
}
```

✅ **Check:** Complaint cards show clean icon+text chips instead of raw emoji.

---

## 16. Scoreboard Animated Rank Badges

Top 3 rank badges pulse/glow to distinguish them from the rest.

### 16a. Update scoreboard component CSS

In `src/app/citizen/scoreboard/scoreboard.component.css`, add:

```css
.rank-badge {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 32px;
  height: 32px;
  border-radius: 50%;
  font-size: 18px;
}

tr:first-child .rank-badge {
  animation: fmc-pulse-ring 1.4s ease-out infinite;
  background: rgba(255, 215, 0, 0.15);
  box-shadow: 0 0 0 0 rgba(255, 215, 0, 0.5);
}

.current-user {
  background: var(--fmc-primary-light);
  font-weight: 600;
}

.current-user td {
  color: var(--fmc-primary-dark);
}

.you-tag {
  font-size: 11px;
  background: var(--fmc-primary-light);
  color: var(--fmc-primary);
  padding: 2px 8px;
  border-radius: 20px;
  margin-left: 6px;
  font-weight: 600;
}
```

✅ **Check:** Scoreboard shows the top rank badge with a pulsing glow.

---

## 17. Form Input Focus Glow

Inputs get a branded glow ring on focus — more interactive, professional feel.

### 17a. Update `styles.css`

Find the existing `.fmc-input` style block and add the focus rule:

```css
.fmc-input:focus {
  outline: none;
  border-color: var(--fmc-primary);
  box-shadow:
    0 0 0 3px rgba(37, 99, 235, 0.15),
    0 1px 3px rgba(0, 0, 0, 0.06);
  transition: box-shadow 0.2s ease, border-color 0.2s ease;
}

.fmc-input:hover:not(:focus) {
  border-color: var(--fmc-border-strong);
}

.fmc-input.error:focus {
  box-shadow:
    0 0 0 3px rgba(239, 68, 68, 0.15),
    0 1px 3px rgba(0, 0, 0, 0.06);
}
```

✅ **Check:** Click any form input — a soft blue glow ring appears around it.

---

## 18. Chatbot FAB Pulse Animation

The chatbot floating action button pulses to draw attention when unread messages exist.

### 18a. Update `chatbot-widget.component.css`

Add:

```css
.chatbot-fab {
  transition: transform 0.2s ease, box-shadow 0.2s ease;
}

.chatbot-fab:hover {
  transform: scale(1.08);
}

.chatbot-fab:active {
  transform: scale(0.96);
}

.chatbot-fab.has-unread::before {
  content: '';
  position: absolute;
  inset: -4px;
  border-radius: 50%;
  border: 2px solid currentColor;
  opacity: 0;
  animation: fmc-pulse-ring 1.6s ease-out infinite;
}
```

### 18b. Update `chatbot-widget.component.html`

Add class binding to the FAB button:

```html
<button
  class="chatbot-fab"
  [class.has-unread]="unreadBadge"
  type="button"
  (click)="toggle()"
  ...>
```

✅ **Check:** When chatbot has unread messages, the button pulses with a ring animation.

---

## 19. Empty State Upgrade

Replace the plain text empty state with a more visually polished component.

### 19a. Update `empty-state.component.html`

```html
<div class="fmc-empty-state">
  <div class="fmc-empty-icon" aria-hidden="true">
    <i [class]="'bi ' + icon"></i>
  </div>
  <h5 class="fmc-empty-title">{{ title }}</h5>
  <p class="fmc-empty-desc" *ngIf="message">{{ message }}</p>
  <ng-content></ng-content>
</div>
```

### 19b. Update `empty-state.component.css`

```css
.fmc-empty-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 56px 24px;
  text-align: center;
  animation: fmc-fade-up 0.4s ease forwards;
}

.fmc-empty-icon {
  width: 72px;
  height: 72px;
  background: var(--fmc-primary-light);
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  margin-bottom: 20px;
  font-size: 30px;
  color: var(--fmc-primary);
}

.fmc-empty-title {
  font-size: 16px;
  font-weight: 600;
  color: var(--fmc-text);
  margin-bottom: 8px;
}

.fmc-empty-desc {
  font-size: 14px;
  color: var(--fmc-text-muted);
  max-width: 320px;
  line-height: 1.6;
  margin: 0 0 20px;
}
```

✅ **Check:** Empty states have a centered icon circle and animate in on load.

---

## 20. Dark Mode Support

Add a CSS-variable-based dark mode that activates based on system preference.

### 20a. Update `styles.css`

After the existing `:root` block, add:

```css
@media (prefers-color-scheme: dark) {
  :root {
    /* Surfaces */
    --fmc-bg:        #0f172a;
    --fmc-surface:   #1e293b;
    --fmc-surface-2: #263244;
    --fmc-border:    #334155;
    --fmc-border-strong: #475569;

    /* Text */
    --fmc-text:        #f1f5f9;
    --fmc-text-muted:  #94a3b8;
    --fmc-text-light:  #64748b;

    /* Primary — slightly lighter for dark bg readability */
    --fmc-primary:       #60a5fa;
    --fmc-primary-dark:  #3b82f6;
    --fmc-primary-light: rgba(96, 165, 250, 0.12);

    /* Accent */
    --fmc-accent: #fbbf24;

    /* Status */
    --fmc-success:       #34d399;
    --fmc-success-light: rgba(52, 211, 153, 0.12);
    --fmc-warning:       #fbbf24;
    --fmc-warning-light: rgba(251, 191, 36, 0.12);
    --fmc-danger:        #f87171;
    --fmc-danger-light:  rgba(248, 113, 113, 0.12);

    /* Shadows */
    --fmc-shadow:    0 1px 3px rgba(0,0,0,0.3), 0 1px 2px rgba(0,0,0,0.2);
    --fmc-shadow-md: 0 4px 16px rgba(0,0,0,0.4), 0 2px 6px rgba(0,0,0,0.2);
    --fmc-shadow-lg: 0 10px 30px rgba(0,0,0,0.5), 0 4px 10px rgba(0,0,0,0.3);
  }
}
```

### 20b. Fix hardcoded colors

Search `src/styles.css`, `landing.component.css`, and `login.component.css` for any hardcoded hex colors like `#fff`, `#0d1b2a`, `#212529`, `#6c757d` and replace them with the closest CSS variable:

| Hardcoded | Replace with |
|---|---|
| `#fff` / `white` | `var(--fmc-surface)` |
| `#0d1b2a` | `var(--fmc-bg)` |
| `#212529` | `var(--fmc-text)` |
| `#6c757d` | `var(--fmc-text-muted)` |
| `rgba(0,0,0,0.2)` overlays | keep as-is (semantic) |

✅ **Check:** Switch OS to dark mode — entire app adapts without white flashes.

---

## Summary — File Change Index

| File | Sections changed |
|---|---|
| `src/index.html` | §1a, §2 |
| `src/styles.css` | §1b, §17a, §20a |
| `src/app/app.module.ts` | §1c, §3b, §7b, §8b, §9b |
| `src/app/app.component.ts` | §10b |
| `src/app/app.component.html` | §10c |
| `src/app/core/route-animations.ts` | §10a (new file) |
| `src/app/shared/directives/reveal.directive.ts` | §3a (new file) |
| `src/app/shared/directives/tilt.directive.ts` | §7a (new file) |
| `src/app/shared/directives/ripple.directive.ts` | §8a (new file) |
| `src/app/shared/components/skeleton-card/*` | §9a (new component) |
| `src/app/public/landing/landing.component.ts` | §4a, §5a, §11a, §12b |
| `src/app/public/landing/landing.component.html` | §3d, §4b, §5b, §11b, §12a |
| `src/app/public/landing/landing.component.css` | §5b, §11c, §12c |
| `src/app/shared/components/navbar/navbar.component.ts` | §6a |
| `src/app/shared/components/navbar/navbar.component.html` | §6b, §2 |
| `src/app/shared/components/navbar/navbar.component.css` | §6c |
| `src/app/shared/components/complaint-card/complaint-card.component.html` | §3c, §15a |
| `src/app/shared/components/complaint-card/complaint-card.component.css` | §15b |
| `src/app/shared/components/toast/toast.component.html` | §13b, §2b |
| `src/app/shared/components/toast/toast.component.ts` | §2b, §13b |
| `src/app/shared/components/toast/toast.component.css` | §13a |
| `src/app/shared/components/timeline/timeline.component.html` | §14c |
| `src/app/shared/components/timeline/timeline.component.ts` | §14b |
| `src/app/shared/components/timeline/timeline.component.css` | §14a |
| `src/app/shared/components/empty-state/empty-state.component.html` | §19a |
| `src/app/shared/components/empty-state/empty-state.component.css` | §19b |
| `src/app/shared/components/chatbot-widget/chatbot-widget.component.html` | §18b |
| `src/app/shared/components/chatbot-widget/chatbot-widget.component.css` | §18a |
| `src/app/citizen/scoreboard/scoreboard.component.css` | §16a |
| All `.html` files | §2a (icon swap) |
