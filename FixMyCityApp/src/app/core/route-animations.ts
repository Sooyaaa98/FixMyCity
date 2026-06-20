// src/app/core/route-animations.ts
// Phase 6 — global router fade+slide transition.
// Wired in app.component.ts via animations: [routeAnimations] and applied to
// the <router-outlet> wrapper as [@routeAnimations]="prepareRoute(outlet)".

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
