import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { ThemeService } from './core/services/theme.service';
import { PwaService } from './core/services/pwa.service';
import { routeAnimations } from './core/route-animations';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.css'],
  animations: [routeAnimations]
})
export class AppComponent {
  title = 'FixMyCityApp';

  // Eagerly instantiate ThemeService so the [data-theme] attribute is
  // applied to <html> before the first paint, instead of lazily when the
  // theme-toggle button is rendered. This avoids a brief light-mode flash
  // for users whose stored preference is "dark".
  // Phase 8 — also init PwaService so the service worker registers and we
  // capture the deferred install prompt the moment the browser fires it.
  constructor(_theme: ThemeService, pwa: PwaService) {
    void _theme;
    pwa.init();
  }

  /**
   * Phase 6 — feed @routeAnimations a stable key per route so the
   * fade+slide transition fires on every navigation.
   *
   * ── Root-cause fix (Phase 7, 2026-05-20) ──────────────────────────────
   * RouterOutlet exposes `activatedRoute` / `activatedRouteData` as Angular
   * getters that THROW `NG04012: Outlet is not activated` whenever they are
   * accessed before the outlet's first activation (i.e. during the very
   * first change-detection tick, between guards finishing and the matched
   * component being instantiated, or during a redirect cycle from an
   * `ngOnInit` like `LandingComponent`'s "already-logged-in" route).
   *
   * Optional chaining (`outlet?.activatedRouteData?.…`) does NOT help: the
   * `?.` operator short-circuits when the LEFT side is nullish, but the
   * getter still runs and still throws. The supported guard is the
   * `isActivated: boolean` property — it is stable, never throws, and is
   * the exact check the framework itself uses internally.
   *
   * When the outlet is inactive we return the literal `'empty'`, which the
   * `routeAnimations` trigger handles via its `* <=> *` transition (no
   * special-case needed). This means the user never sees a crash and the
   * animation correctly fires once the outlet activates.
   */
  prepareRoute(outlet: RouterOutlet | null): string {
    if (!outlet || !outlet.isActivated) {
      return 'empty';
    }
    // Both reads below are now safe: `isActivated` guards the getters.
    const data = outlet.activatedRouteData;
    if (data && typeof data['animation'] === 'string') {
      return data['animation'] as string;
    }
    const firstSegment = outlet.activatedRoute?.snapshot?.url?.[0]?.path;
    return firstSegment ?? 'route';
  }
}
