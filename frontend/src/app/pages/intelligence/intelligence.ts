import { ChangeDetectionStrategy, ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { BaseChartDirective } from 'ng2-charts';
import { ChartConfiguration, ChartData } from 'chart.js';

import { ClusterService } from '../../services/cluster.service';
import { ProductService } from '../../services/product.service';
import { JobsService } from '../../services/jobs.service';
import { MarketCluster } from '../../models/market.models';
import { PipelineStatusComponent } from '../../components/pipeline-status/pipeline-status';

interface Kpi { label: string; value: string; sub?: string; color: string; }

@Component({
  selector: 'app-intelligence',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, RouterLink, BaseChartDirective, PipelineStatusComponent],
  templateUrl: './intelligence.html',
})
export class IntelligencePage implements OnInit {
  private readonly clusterService = inject(ClusterService);
  private readonly productService = inject(ProductService);
  private readonly jobsService = inject(JobsService);
  private readonly cdr = inject(ChangeDetectorRef);

  loading = true;
  clusters: MarketCluster[] = [];
  topClusters: MarketCluster[] = [];
  totalJobs = 0;
  totalProducts = 0;
  kpis: Kpi[] = [];

  // ── Charts ───────────────────────────────────────────────────────────────────

  industryChart: ChartData<'pie'> = { labels: [], datasets: [{ data: [] }] };
  industryOptions: ChartConfiguration['options'] = {
    responsive: true,
    plugins: { legend: { position: 'right' } }
  };

  opportunityTypeChart: ChartData<'bar'> = { labels: [], datasets: [{ data: [], label: 'Clusters', backgroundColor: [] }] };
  oppTypeOptions: ChartConfiguration['options'] = {
    responsive: true,
    plugins: { legend: { display: false } },
    scales: { y: { beginAtZero: true, ticks: { stepSize: 1 } } }
  };

  priorityChart: ChartData<'bar'> = { labels: [], datasets: [{ data: [], label: 'Priority V2', backgroundColor: '#6366f1' }] };
  priorityOptions: ChartConfiguration['options'] = {
    indexAxis: 'y',
    responsive: true,
    plugins: { legend: { display: false } },
    scales: { x: { beginAtZero: true, max: 100 } }
  };

  scatterChart: ChartData<'scatter'> = { datasets: [] };
  scatterOptions: ChartConfiguration['options'] = {
    responsive: true,
    plugins: { legend: { display: false }, tooltip: {
      callbacks: {
        label: (ctx) => {
          const raw = ctx.raw as { x: number; y: number; label?: string };
          return `${raw.label ?? ''} — Jobs: ${raw.x}, Revenue: ${raw.y.toFixed(0)}`;
        }
      }
    }},
    scales: {
      x: { title: { display: true, text: 'Job Count' } },
      y: { title: { display: true, text: 'Revenue Potential' }, beginAtZero: true }
    }
  };

  ngOnInit(): void {
    forkJoin({
      clusters: this.clusterService.getClusters({ pageSize: 50 }),
      products: this.productService.getProducts({ pageSize: 1 }),
      jobs: this.jobsService.queryJobs({ pageSize: 1 })
    }).subscribe({
      next: ({ clusters, products, jobs }) => {
        this.clusters = clusters.items;
        this.totalJobs = jobs.totalCount;
        this.totalProducts = products.totalCount;
        this.topClusters = [...clusters.items]
          .sort((a, b) => b.priorityScoreV2 - a.priorityScoreV2)
          .slice(0, 10);

        this.buildKpis();
        this.buildCharts();
        this.loading = false;
        this.cdr.markForCheck();
      },
      error: () => {
        this.loading = false;
        this.cdr.markForCheck();
      }
    });
  }

  private buildKpis(): void {
    const c = this.clusters;
    const actionable = c.filter(x => x.isActionable).length;
    const ignore = c.filter(x => x.opportunityType === 'Ignore').length;
    const totalJobs = c.reduce((s, x) => s + x.jobCount, 0);
    const directRatio = c.length
      ? c.reduce((s, x) => s + x.directClientRatio, 0) / c.length
      : 0;
    const avgBOC = c.length
      ? c.reduce((s, x) => s + x.blueOceanScore, 0) / c.length
      : 0;
    const synthesized = c.filter(x => x.llmStatus === 'completed').length;

    this.kpis = [
      { label: 'Total Job Offers',   value: this.totalJobs.toString(),        color: 'text-slate-700' },
      { label: 'Insights Processed', value: totalJobs.toString(),             sub: `${c.length} clusters`, color: 'text-indigo-600' },
      { label: 'Actionable Clusters',value: actionable.toString(),            sub: `${ignore} ignored`, color: 'text-emerald-600' },
      { label: 'Products Generated', value: this.totalProducts.toString(),    color: 'text-violet-600' },
      { label: 'Direct Client Ratio',value: `${(directRatio * 100).toFixed(0)}%`, color: 'text-sky-600' },
      { label: 'Avg Blue Ocean',     value: avgBOC.toFixed(1),               color: 'text-amber-600' },
      { label: 'LLM Synthesized',    value: synthesized.toString(),           sub: `of ${actionable} actionable`, color: 'text-rose-600' },
      { label: 'High Opportunity',   value: c.filter(x => x.priorityScoreV2 >= 75).length.toString(), sub: 'Priority V2 ≥75', color: 'text-emerald-700' },
    ];
  }

  private buildCharts(): void {
    const c = this.clusters;

    // Industry distribution
    const byIndustry = c.reduce<Record<string, number>>((acc, x) => {
      acc[x.industry] = (acc[x.industry] ?? 0) + 1;
      return acc;
    }, {});
    this.industryChart = {
      labels: Object.keys(byIndustry),
      datasets: [{
        data: Object.values(byIndustry),
        backgroundColor: ['#6366f1','#10b981','#f59e0b','#ef4444','#3b82f6','#8b5cf6','#14b8a6','#f97316']
      }]
    };

    // Opportunity type distribution
    const byType: Record<string, number> = { MVPProduct: 0, QuickWin: 0, Consulting: 0, Ignore: 0 };
    c.forEach(x => { byType[x.opportunityType] = (byType[x.opportunityType] ?? 0) + 1; });
    this.opportunityTypeChart = {
      labels: Object.keys(byType),
      datasets: [{
        data: Object.values(byType),
        label: 'Clusters',
        backgroundColor: ['#8b5cf6','#10b981','#f59e0b','#cbd5e1']
      }]
    };

    // Top 10 by PriorityScoreV2 (horizontal bar)
    const top10 = [...c].sort((a, b) => b.priorityScoreV2 - a.priorityScoreV2).slice(0, 10);
    this.priorityChart = {
      labels: top10.map(x => x.label.length > 32 ? x.label.slice(0, 32) + '…' : x.label),
      datasets: [{
        data: top10.map(x => x.priorityScoreV2),
        label: 'Priority V2',
        backgroundColor: top10.map(x => x.priorityScoreV2 >= 75 ? '#10b981' : x.priorityScoreV2 >= 65 ? '#6366f1' : '#94a3b8')
      }]
    };

    // Scatter: JobCount vs RevenuePotential
    this.scatterChart = {
      datasets: [{
        label: 'Clusters',
        data: c.map(x => ({ x: x.jobCount, y: x.revenuePotential, label: x.label } as never)),
        backgroundColor: 'rgba(99,102,241,0.6)',
        pointRadius: 5,
        pointHoverRadius: 7
      }]
    };
  }

  fmt(v: number, d = 1): string { return v.toFixed(d); }

  fmtClose(v: number): string { return `${Math.round(v * 100)}%`; }
}
