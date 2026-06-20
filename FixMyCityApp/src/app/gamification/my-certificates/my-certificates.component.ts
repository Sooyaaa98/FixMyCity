// src/app/gamification/my-certificates/my-certificates.component.ts

import { Component, OnInit } from '@angular/core';
import { GamificationService } from '../../fmc-services/gamification.service';
import { ToastService } from '../../fmc-services/toast.service';
import { SessionService } from '../../core/services/session.service';
import { ICertificate } from '../../fmc-interfaces/gamification.interface';

@Component({
  selector: 'app-my-certificates',
  templateUrl: './my-certificates.component.html',
  styleUrls: ['./my-certificates.component.css']
})
export class MyCertificatesComponent implements OnInit {

  certificates: ICertificate[] = [];
  isLoading = true;

  constructor(
    private gamificationService: GamificationService,
    private toast: ToastService,
    private session: SessionService
  ) {}

  ngOnInit(): void {
    this.gamificationService.getCertificates(this.session.getUserId()).subscribe({
      next: (data) => { this.certificates = data; this.isLoading = false; },
      error: () => { this.isLoading = false; this.toast.error('Could not load certificates.'); }
    });
  }

  /**
   * Triggers an authenticated PDF download via QuestPdfService on the API.
   * cert.filePath (a static file URL) is no longer used — the PDF is generated
   * on demand from /api/Report/CertificatePdf?certificateId=N. The seed leaves
   * FilePath = NULL on purpose; checking it would hide the Download button.
   */
  download(cert: ICertificate): void {
    this.gamificationService.downloadCertificatePdf(cert.certificateId, cert.milestone);
  }

  milestoneIcon(milestone: string): string {
    const m = milestone.toLowerCase();
    if (m.includes('champion')) return '🏆';
    if (m.includes('active'))   return '🥇';
    if (m.includes('contrib'))  return '🥈';
    if (m.includes('start'))    return '🥉';
    return '🎖️';
  }

  trackById(_: number, c: ICertificate): number { return c.certificateId; }
}
