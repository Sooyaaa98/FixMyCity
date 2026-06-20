// src/app/public/landing/landing.component.ts
//
// Phase 6 — interactive landing page:
//   §4  Animated stat counters on scroll
//   §5  Hero mouse-spotlight (CSS `--mx` / `--my` custom properties)
//   §11 Hero typing animation across two lines
//   §12 Particle canvas background

import {
  Component, OnInit, OnDestroy, AfterViewInit,
  ElementRef, HostListener, ViewChild, ViewChildren, QueryList
} from '@angular/core';
import { Router } from '@angular/router';
import { SessionService } from '../../core/services/session.service';

@Component({
  selector: 'app-landing',
  templateUrl: './landing.component.html',
  styleUrls: ['./landing.component.css']
})
export class LandingComponent implements OnInit, AfterViewInit, OnDestroy {

  // ── Static content ──────────────────────────────────────────────────────
  steps = [
    { icon: 'bi bi-person-plus-fill',    label: 'Login / Sign Up',           desc: 'Create your free citizen account in under a minute.' },
    { icon: 'bi bi-pencil-square',       label: 'Raise a Complaint',         desc: 'Report potholes, water leaks, power outages and more.' },
    { icon: 'bi bi-shuffle',              label: 'Auto-Routed to Authorities', desc: 'Complaints reach the right department automatically.' },
    { icon: 'bi bi-wrench-adjustable',    label: 'Issue Gets Resolved',       desc: 'Field teams are assigned and work is tracked live.' },
    { icon: 'bi bi-star-fill',            label: 'Rate the Fix',              desc: 'Earn points and certificates for civic participation.' },
  ];

  benefits = [
    { icon: 'bi bi-lightning-charge-fill', title: 'Fast Resolution',           desc: 'Average turnaround of 3–5 working days for standard complaints.' },
    { icon: 'bi bi-geo-alt-fill',           title: 'Locality-Aware',            desc: 'Complaints routed by locality for precise, local accountability.' },
    { icon: 'bi bi-shield-fill-check',      title: 'Fully Transparent',         desc: 'Track every status change — from submission to resolved.' },
    { icon: 'bi bi-trophy-fill',            title: 'Gamified Civic Engagement', desc: 'Earn points, climb scoreboards, receive recognition certificates.' },
  ];

  currentYear = new Date().getFullYear();

  // ── Phase 6 hero state ─────────────────────────────────────────────────
  @ViewChild('heroSection')   heroRef!: ElementRef<HTMLElement>;
  @ViewChild('particleCanvas') canvasRef?: ElementRef<HTMLCanvasElement>;
  @ViewChildren('statNum')     statEls!: QueryList<ElementRef<HTMLElement>>;

  heroLines     = ['Fix Your City.', 'Raise Your Voice.'];
  heroDisplayed = '';
  private typingTimer: ReturnType<typeof setTimeout> | null = null;
  private particleAnimId = 0;
  private statObserver: IntersectionObserver | null = null;
  private reducedMotion = false;

  constructor(
    private session: SessionService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.reducedMotion = window.matchMedia?.('(prefers-reduced-motion: reduce)').matches ?? false;

    // Redirect already-authenticated users to their dashboard.
    if (this.session.isLoggedIn()) {
      const role = this.session.getRole();
      switch (role) {
        case 'SuperAdmin': this.router.navigate(['/admin/dashboard']); break;
        case 'Citizen':    this.router.navigate(['/citizen/home']);    break;
        case 'Solver':     this.router.navigate(['/solver/dashboard']); break;
        case 'PWG':        this.router.navigate(['/pwg/complaints']);   break;
        default:           this.router.navigate(['/login']);
      }
      return;
    }

    if (this.reducedMotion) {
      this.heroDisplayed = this.heroLines.join('\n');
    } else {
      this.startTyping(0, 0);
    }
  }

  ngAfterViewInit(): void {
    if (this.reducedMotion) return;
    this.observeStats();
    this.initParticles();
  }

  ngOnDestroy(): void {
    if (this.typingTimer) clearTimeout(this.typingTimer);
    if (this.particleAnimId) cancelAnimationFrame(this.particleAnimId);
    this.statObserver?.disconnect();
  }

  // ── §5 Mouse-spotlight ─────────────────────────────────────────────────
  @HostListener('mousemove', ['$event'])
  onMouseMove(e: MouseEvent): void {
    if (!this.heroRef || this.reducedMotion) return;
    const el = this.heroRef.nativeElement;
    const { left, top } = el.getBoundingClientRect();
    el.style.setProperty('--mx', (e.clientX - left) + 'px');
    el.style.setProperty('--my', (e.clientY - top)  + 'px');
  }

  // ── §11 Hero typing ────────────────────────────────────────────────────
  private startTyping(lineIndex: number, charIndex: number): void {
    if (lineIndex >= this.heroLines.length) return;
    const line = this.heroLines[lineIndex];
    if (charIndex <= line.length) {
      this.heroDisplayed =
        this.heroLines.slice(0, lineIndex).join('\n')
        + (lineIndex > 0 ? '\n' : '')
        + line.slice(0, charIndex);
      this.typingTimer = setTimeout(() => this.startTyping(lineIndex, charIndex + 1), 48);
    } else {
      this.typingTimer = setTimeout(() => this.startTyping(lineIndex + 1, 0), 320);
    }
  }

  /** Helper for the [innerHTML] binding so we don't have to escape \n in the template. */
  get heroDisplayedHtml(): string {
    return (this.heroDisplayed || '').replace(/\n/g, '<br>');
  }

  // ── §4 Stat counters ───────────────────────────────────────────────────
  private observeStats(): void {
    if (!this.statEls) return;
    this.statObserver = new IntersectionObserver((entries, obs) => {
      entries.forEach(entry => {
        if (entry.isIntersecting) {
          this.startCounter(entry.target as HTMLElement);
          obs.unobserve(entry.target);
        }
      });
    }, { threshold: 0.5 });
    this.statEls.forEach(el => this.statObserver!.observe(el.nativeElement));
  }

  private startCounter(el: HTMLElement): void {
    const target  = parseInt(el.dataset['target'] ?? '0', 10);
    const suffix  = el.dataset['suffix'] ?? '';
    const duration = 1400;
    const start = performance.now();
    const tick = (now: number) => {
      const elapsed  = Math.min(now - start, duration);
      const progress = elapsed / duration;
      const eased    = 1 - Math.pow(1 - progress, 3);
      el.textContent = Math.round(eased * target).toLocaleString() + suffix;
      if (elapsed < duration) requestAnimationFrame(tick);
    };
    requestAnimationFrame(tick);
  }

  // ── §12 Particle background ────────────────────────────────────────────
  private initParticles(): void {
    if (!this.canvasRef || !this.heroRef) return;
    const canvas = this.canvasRef.nativeElement;
    const hero   = this.heroRef.nativeElement;
    canvas.width  = hero.offsetWidth;
    canvas.height = hero.offsetHeight;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;

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
        n.x += n.vx; n.y += n.vy;
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

  @HostListener('window:resize')
  onWindowResize(): void {
    if (!this.canvasRef || !this.heroRef || this.reducedMotion) return;
    const canvas = this.canvasRef.nativeElement;
    const hero   = this.heroRef.nativeElement;
    canvas.width  = hero.offsetWidth;
    canvas.height = hero.offsetHeight;
  }
}
