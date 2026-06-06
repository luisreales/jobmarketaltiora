import { ChangeDetectionStrategy, ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { CommonModule, CurrencyPipe } from '@angular/common';
import { RevenueService } from '../../services/revenue.service';
import {
  RevenueSummaryDto,
  TopOpportunityDto,
  ServiceModelRevenueDto,
  serviceModelColor,
} from '../../models/revenue.models';

@Component({
  selector: 'app-revenue',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, CurrencyPipe],
  templateUrl: './revenue.html',
})
export class RevenuePage implements OnInit {
  private readonly svc = inject(RevenueService);
  private readonly cdr = inject(ChangeDetectorRef);

  loading = true;
  error = false;
  summary: RevenueSummaryDto | null = null;

  readonly serviceModelColor = serviceModelColor;

  ngOnInit(): void {
    this.svc.getSummary().subscribe({
      next: (data) => {
        this.summary = data;
        this.loading = false;
        this.cdr.markForCheck();
      },
      error: () => {
        this.error = true;
        this.loading = false;
        this.cdr.markForCheck();
      },
    });
  }

  funnelPct(value: number, total: number): number {
    if (total === 0) return 0;
    return Math.min(100, Math.round((value / total) * 100));
  }

  maxServiceValue(rows: ServiceModelRevenueDto[]): number {
    return rows.reduce((m, r) => Math.max(m, r.weightedValueUsd), 1);
  }

  barWidth(value: number, max: number): number {
    return Math.round((value / max) * 100);
  }

  trackByCluster(_: number, opp: TopOpportunityDto): number {
    return opp.clusterId;
  }

  urgencyClass(score: number): string {
    if (score >= 7) return 'text-red-600 font-semibold';
    if (score >= 5) return 'text-amber-600';
    return 'text-slate-500';
  }
}
