// src/app/shared/components/loading-spinner/loading-spinner.component.ts

import { Component, Input } from '@angular/core';

@Component({
  selector: 'app-loading-spinner',
  templateUrl: './loading-spinner.component.html',
  styleUrls: ['./loading-spinner.component.css']
})
export class LoadingSpinnerComponent {

  /** 'page' — full-page overlay, 'inline' — inline block inside a section */
  @Input() mode: 'page' | 'inline' = 'inline';

  /** Message shown below the spinner */
  @Input() message: string = 'Loading…';
}
