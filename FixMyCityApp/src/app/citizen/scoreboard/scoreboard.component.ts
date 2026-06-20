// src/app/citizen/scoreboard/scoreboard.component.ts

import { Component, OnInit } from '@angular/core';
import { GamificationService } from '../../fmc-services/gamification.service';
import { SessionService } from '../../core/services/session.service';
import { IScoreboardEntry } from '../../fmc-interfaces/gamification.interface';

@Component({
  selector: 'app-scoreboard',
  templateUrl: './scoreboard.component.html',
  styleUrls: ['./scoreboard.component.css']
})
export class ScoreboardComponent implements OnInit {

  entries: IScoreboardEntry[] = [];
  isLoading = true;
  errorMessage = '';
  currentUserId!: number;
  private localityId!: number;

  constructor(
    private gamificationService: GamificationService,
    private session: SessionService
  ) {}

  ngOnInit(): void {
    this.localityId   = this.session.getLocalityId();
    this.currentUserId = this.session.getUserId();
    this.loadScoreboard();
  }

  loadScoreboard(): void {
    this.isLoading = true;
    this.gamificationService.getScoreboard(this.localityId).subscribe({
      next: (data) => { this.entries = data; this.isLoading = false; },
      error: () => { this.errorMessage = 'Could not load scoreboard.'; this.isLoading = false; }
    });
  }
}
