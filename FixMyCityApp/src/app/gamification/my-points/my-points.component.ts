// src/app/gamification/my-points/my-points.component.ts

import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { GamificationService } from '../../fmc-services/gamification.service';
import { SessionService } from '../../core/services/session.service';
import { IUserPoint } from '../../fmc-interfaces/gamification.interface';

@Component({
  selector: 'app-my-points',
  templateUrl: './my-points.component.html',
  styleUrls: ['./my-points.component.css']
})
export class MyPointsComponent implements OnInit {

  userPoints: IUserPoint | null = null;
  isLoading = true;
  errorMessage = '';
  fullName = '';

  constructor(
    private gamificationService: GamificationService,
    private session: SessionService,
    private router: Router
  ) { }

  ngOnInit(): void {
    this.fullName = this.session.getUser()?.fullName ?? '';

    this.gamificationService.getUserPoints(this.session.getUserId()).subscribe({
      next: (data) => {
        this.userPoints = data;
        this.isLoading = false;
      },
      error: () => {
        // Not an error state — citizen may simply have no points record yet
        this.userPoints = null;
        this.isLoading = false;
      }
    });
  }

  goToScoreboard(): void {
    this.router.navigate(['/citizen/scoreboard']);
  }

  // Milestone label based on point total
  getMilestoneLabel(points: number): string {
    if (points >= 500) return '🏆 Champion Citizen';
    if (points >= 200) return '🥇 Active Reporter';
    if (points >= 100) return '🥈 Contributor';
    if (points >= 50) return '🥉 Getting Started';
    return '🌱 New Member';
  }

  getProgressPercent(points: number): number {
    const milestones = [50, 100, 200, 500];
    const next = milestones.find(m => m > points);
    if (!next) return 100;
    const prev = milestones[milestones.indexOf(next) - 1] ?? 0;
    return Math.round(((points - prev) / (next - prev)) * 100);
  }

  getNextMilestone(points: number): number | null {
    const milestones = [50, 100, 200, 500];
    return milestones.find(m => m > points) ?? null;
  }
}
