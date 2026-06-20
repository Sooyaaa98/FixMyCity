// src/app/shared/components/empty-state/empty-state.component.ts
//
// Phase 6 upgrade (2026-05-20)
//
// Accepts a full Bootstrap Icons class string as the `icon` input
// (e.g. "bi bi-inbox") so the template can render it via `[class]="icon"`
// without needing to know the icon family. Backwards-compatible: passing
// "bi-inbox" alone still renders correctly because Bootstrap Icons CSS
// keys off the leaf class.
//
// Also gains a `message` alias for `subtitle` so call sites can use either
// name — both inputs flow through to the same template slot.

import { Component, Input, Output, EventEmitter } from '@angular/core';

@Component({
  selector: 'app-empty-state',
  templateUrl: './empty-state.component.html',
  styleUrls: ['./empty-state.component.css']
})
export class EmptyStateComponent {

  /** Bootstrap Icons class — e.g. "bi bi-inbox". */
  @Input() icon: string = 'bi bi-inbox';

  @Input() title: string = 'Nothing here yet';

  /** Long-form body copy under the title. `message` is an alias for `subtitle`. */
  @Input() subtitle: string = '';
  @Input() set message(v: string) { this.subtitle = v; }
  get message(): string { return this.subtitle; }

  @Input() actionLabel: string = '';

  @Output() action = new EventEmitter<void>();

  onAction(): void { this.action.emit(); }
}
