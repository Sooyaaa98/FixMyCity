// src/app/shared/components/ai-hint-panel/ai-hint-panel.component.ts

import { Component, Input, Output, EventEmitter } from '@angular/core';
import { ICategorySuggestion, IDuplicateResult } from '../../../fmc-interfaces/ml.interface';

@Component({
  selector: 'app-ai-hint-panel',
  templateUrl: './ai-hint-panel.component.html',
  styleUrls: ['./ai-hint-panel.component.css']
})
export class AiHintPanelComponent {

  /** Category suggestions from POST /api/ML/CategorizeText */
  @Input() suggestions: ICategorySuggestion[] = [];

  /** Duplicate detection result from POST /api/ML/CheckDuplicates */
  @Input() duplicateResult: IDuplicateResult | null = null;

  /** Whether the AI service is loading */
  @Input() loading = false;

  /** Whether the panel should be shown at all */
  @Input() visible = false;

  /** Emitted when user accepts a suggested category */
  @Output() categoryAccepted = new EventEmitter<number>();

  /** Emitted when user clicks "Submit Anyway" on duplicate warning */
  @Output() submitAnyway = new EventEmitter<void>();

  get hasSuggestions(): boolean {
    return this.suggestions.length > 0;
  }

  get hasDuplicates(): boolean {
    return !!this.duplicateResult && this.duplicateResult.count > 0;
  }

  acceptCategory(categoryId: number): void {
    this.categoryAccepted.emit(categoryId);
  }

  onSubmitAnyway(): void {
    this.submitAnyway.emit();
  }

  confidencePercent(confidence: number): number {
    return Math.round(confidence * 100);
  }
}
