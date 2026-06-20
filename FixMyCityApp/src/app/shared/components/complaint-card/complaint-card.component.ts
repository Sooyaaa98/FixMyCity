// src/app/shared/components/complaint-card/complaint-card.component.ts

import { Component, EventEmitter, Input, Output } from '@angular/core';
import { IComplaint } from '../../../fmc-interfaces/complaint.interface';

@Component({
  selector: 'app-complaint-card',
  templateUrl: './complaint-card.component.html',
  styleUrls: ['./complaint-card.component.css']
})
export class ComplaintCardComponent {
  @Input() complaint!: IComplaint;
  @Output() cardClicked = new EventEmitter<number>();

  onCardClick(): void {
    this.cardClicked.emit(this.complaint.complaintId);
  }
}
