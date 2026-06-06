import { ChangeDetectionStrategy, ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { forkJoin } from 'rxjs';
import { TechnologyService } from '../../services/technology.service';
import { TechnologyDto, categoryColor, lifecycleColor } from '../../models/technology.models';

@Component({
  selector: 'app-trends',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule],
  templateUrl: './trends.html',
})
export class TrendsPage implements OnInit {
  private readonly svc = inject(TechnologyService);
  private readonly cdr = inject(ChangeDetectorRef);

  loading = true;
  trending: TechnologyDto[] = [];
  emerging: TechnologyDto[] = [];
  declining: TechnologyDto[] = [];
  aiTechs: TechnologyDto[] = [];

  readonly lifecycleColor = lifecycleColor;
  readonly categoryColor = categoryColor;

  ngOnInit(): void {
    forkJoin({
      trending: this.svc.getTrending(),
      emerging: this.svc.getEmerging(),
      declining: this.svc.getDeclining(),
      ai: this.svc.getAi(),
    }).subscribe({
      next: ({ trending, emerging, declining, ai }) => {
        this.trending = trending;
        this.emerging = emerging;
        this.declining = declining;
        this.aiTechs = ai;
        this.loading = false;
        this.cdr.markForCheck();
      },
      error: () => {
        this.loading = false;
        this.cdr.markForCheck();
      }
    });
  }

  get growingCount(): number {
    return this.trending.filter(t => t.momentumScore > 10).length;
  }

  get emergingCount(): number {
    return this.emerging.length;
  }

  get decliningCount(): number {
    return this.declining.length;
  }

  get totalTracked(): number {
    const all = new Set([
      ...this.trending.map(t => t.id),
      ...this.emerging.map(t => t.id),
      ...this.declining.map(t => t.id),
    ]);
    return all.size;
  }

  momentumBar(score: number): number {
    return Math.min(100, Math.abs(score));
  }

  formatMomentum(score: number): string {
    return score >= 0 ? `+${score.toFixed(0)}` : `${score.toFixed(0)}`;
  }

  daysSince(dateStr: string): number {
    return Math.floor((Date.now() - new Date(dateStr).getTime()) / 86400000);
  }

  trackByTech(_: number, t: TechnologyDto): number { return t.id; }
}
