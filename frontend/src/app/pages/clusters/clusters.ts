import {
  ChangeDetectionStrategy, ChangeDetectorRef, Component, OnDestroy, OnInit, inject
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subject, debounceTime, distinctUntilChanged, takeUntil } from 'rxjs';
import { MatSnackBarModule } from '@angular/material/snack-bar';
import { ClusterService } from '../../services/cluster.service';
import { NotificationService } from '../../services/notification.service';
import {
  ClusterLead, ClusterLeadsQuery, MarketCluster, MarketClusterQuery
} from '../../models/market.models';

type SortField = 'priorityScoreV2' | 'revenuePotential' | 'buyingIntentScore' | 'jobCount' | 'estimatedCloseProbability' | 'blueOceanScore';

@Component({
  selector: 'app-clusters',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, FormsModule, MatSnackBarModule],
  templateUrl: './clusters.html',
})
export class ClustersPage implements OnInit, OnDestroy {
  private readonly clusterService = inject(ClusterService);
  private readonly notify = inject(NotificationService);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly destroy$ = new Subject<void>();
  private readonly search$ = new Subject<string>();

  clusters: MarketCluster[] = [];
  loading = false;
  page = 1;
  pageSize = 20;
  totalPages = 1;
  totalCount = 0;

  filterOpportunityType = '';
  filterIndustry = '';
  filterActionable = '';
  searchText = '';
  sortField: SortField = 'priorityScoreV2';

  expandedClusterId: number | null = null;
  clusterLeads: ClusterLead[] = [];
  leadsLoading = false;
  leadsPage = 1;
  leadsTotalPages = 1;

  synthesisingId: number | null = null;

  // Operations panel
  showOpsPanel = false;
  rebuildingClusters = false;
  backfillingInsights = false;
  cleaningUp = false;
  showCleanupConfirm = false;
  resynthesisingBatch = false;
  resynthesisTotal = 0;
  resynthesisDone = 0;
  resynthesisErrors = 0;

  ngOnInit(): void {
    this.search$.pipe(
      debounceTime(300),
      distinctUntilChanged(),
      takeUntil(this.destroy$)
    ).subscribe(() => {
      this.page = 1;
      this.loadClusters();
    });

    this.loadClusters();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  onSearchInput(value: string): void {
    this.searchText = value;
    this.search$.next(value);
  }

  loadClusters(): void {
    this.loading = true;
    const query: MarketClusterQuery = {
      page: this.page,
      pageSize: this.pageSize,
      opportunityType: this.filterOpportunityType || undefined,
      industry: this.filterIndustry || undefined,
      isActionable: this.filterActionable === 'true' ? true
                  : this.filterActionable === 'false' ? false
                  : undefined,
    };

    this.clusterService.getClusters(query).subscribe({
      next: (resp) => {
        this.clusters = this.sortClusters(resp.items);
        this.totalCount = resp.totalCount;
        this.totalPages = resp.totalPages;
        this.loading = false;
        this.cdr.markForCheck();
      },
      error: () => {
        this.loading = false;
        this.cdr.markForCheck();
      }
    });
  }

  private sortClusters(items: MarketCluster[]): MarketCluster[] {
    return [...items].sort((a, b) => (b[this.sortField] as number) - (a[this.sortField] as number));
  }

  get visibleClusters(): MarketCluster[] {
    const term = this.searchText.trim().toLowerCase();
    if (!term) return this.clusters;
    return this.clusters.filter(c =>
      c.label.toLowerCase().includes(term) ||
      c.industry.toLowerCase().includes(term) ||
      c.painCategory.toLowerCase().includes(term) ||
      c.normalizedTechStack.toLowerCase().includes(term)
    );
  }

  applyFilters(): void {
    this.page = 1;
    this.loadClusters();
  }

  onSortChange(): void {
    this.clusters = this.sortClusters(this.clusters);
    this.cdr.markForCheck();
  }

  prevPage(): void {
    if (this.page > 1) { this.page--; this.loadClusters(); }
  }

  nextPage(): void {
    if (this.page < this.totalPages) { this.page++; this.loadClusters(); }
  }

  trackByCluster(_: number, c: MarketCluster): number { return c.id; }
  trackByLead(_: number, l: ClusterLead): number { return l.jobId; }

  toggleExpand(cluster: MarketCluster): void {
    if (this.expandedClusterId === cluster.id) {
      this.expandedClusterId = null;
      this.clusterLeads = [];
      return;
    }
    this.expandedClusterId = cluster.id;
    this.leadsPage = 1;
    this.loadLeads(cluster.id);
  }

  loadLeads(clusterId: number): void {
    this.leadsLoading = true;
    const query: ClusterLeadsQuery = { page: this.leadsPage, pageSize: 10 };
    this.clusterService.getClusterLeads(clusterId, query).subscribe({
      next: (resp) => {
        this.clusterLeads = resp.items;
        this.leadsTotalPages = resp.totalPages;
        this.leadsLoading = false;
        this.cdr.markForCheck();
      },
      error: () => {
        this.leadsLoading = false;
        this.cdr.markForCheck();
      }
    });
  }

  prevLeadsPage(): void {
    if (this.expandedClusterId && this.leadsPage > 1) {
      this.leadsPage--;
      this.loadLeads(this.expandedClusterId);
    }
  }

  nextLeadsPage(): void {
    if (this.expandedClusterId && this.leadsPage < this.leadsTotalPages) {
      this.leadsPage++;
      this.loadLeads(this.expandedClusterId);
    }
  }

  synthesize(cluster: MarketCluster, event: Event): void {
    event.stopPropagation();
    if (this.synthesisingId !== null) return;
    this.synthesisingId = cluster.id;

    this.clusterService.synthesize(cluster.id).subscribe({
      next: (updated) => {
        this.clusters = this.clusters.map(c => c.id === updated.id ? updated : c);
        this.synthesisingId = null;
        this.notify.success(`Cluster "${updated.label}" synthesized.`);
        this.cdr.markForCheck();
      },
      error: () => {
        this.synthesisingId = null;
        this.cdr.markForCheck();
      }
    });
  }

  // ── Operations panel ─────────────────────────────────────────────────────────

  rebuild(): void {
    if (this.rebuildingClusters) return;
    this.rebuildingClusters = true;

    this.clusterService.rebuild().subscribe({
      next: (result) => {
        this.rebuildingClusters = false;
        this.notify.success(`Rebuilt ${result.clustersUpserted} clusters — ${result.actionableClusters} actionable.`);
        this.loadClusters();
      },
      error: () => {
        this.rebuildingClusters = false;
        this.cdr.markForCheck();
      }
    });
  }

  backfill(): void {
    if (this.backfillingInsights) return;
    this.backfillingInsights = true;

    this.clusterService.backfillInsights().subscribe({
      next: (result) => {
        this.backfillingInsights = false;
        this.notify.success(`Backfilled ${result.backfilled} insights.`);
        this.cdr.markForCheck();
      },
      error: () => {
        this.backfillingInsights = false;
        this.cdr.markForCheck();
      }
    });
  }

  confirmCleanup(): void {
    this.showCleanupConfirm = true;
    this.cdr.markForCheck();
  }

  cancelCleanup(): void {
    this.showCleanupConfirm = false;
    this.cdr.markForCheck();
  }

  cleanup(): void {
    this.showCleanupConfirm = false;
    if (this.cleaningUp) return;
    this.cleaningUp = true;

    this.clusterService.cleanupSmallClusters(3).subscribe({
      next: (result) => {
        this.cleaningUp = false;
        this.notify.success(`Deleted ${result.deleted} low-signal clusters.`);
        this.loadClusters();
      },
      error: () => {
        this.cleaningUp = false;
        this.cdr.markForCheck();
      }
    });
  }

  async resynthesizeLegacy(): Promise<void> {
    if (this.resynthesisingBatch) return;

    const legacy = this.clusters.filter(
      c => c.llmStatus === 'completed' && c.llmConfidence == null
    );

    if (legacy.length === 0) {
      this.notify.info('No legacy clusters to re-synthesize.');
      return;
    }

    this.resynthesisingBatch = true;
    this.resynthesisTotal = legacy.length;
    this.resynthesisDone = 0;
    this.resynthesisErrors = 0;
    this.cdr.markForCheck();

    for (const cluster of legacy) {
      // Reset LLM status via synthesize endpoint requires pending status.
      // We fire synthesize and accept it may return cached — this is a
      // best-effort re-synthesis for legacy clusters already in pending state.
      await new Promise<void>(resolve => {
        this.clusterService.synthesize(cluster.id).subscribe({
          next: (updated) => {
            this.clusters = this.clusters.map(c => c.id === updated.id ? updated : c);
            this.resynthesisDone++;
            this.cdr.markForCheck();
            resolve();
          },
          error: () => {
            this.resynthesisErrors++;
            this.cdr.markForCheck();
            resolve();
          }
        });
      });
    }

    this.resynthesisingBatch = false;
    this.notify.success(`Re-synthesis complete: ${this.resynthesisDone} done, ${this.resynthesisErrors} errors.`);
    this.cdr.markForCheck();
  }

  get resynthesisProgress(): number {
    if (this.resynthesisTotal === 0) return 0;
    return Math.round(((this.resynthesisDone + this.resynthesisErrors) / this.resynthesisTotal) * 100);
  }

  // ── Display helpers ──────────────────────────────────────────────────────────

  opportunityTypeClass(type: string): string {
    switch (type) {
      case 'MVPProduct':  return 'bg-purple-100 text-purple-700';
      case 'QuickWin':    return 'bg-green-100 text-green-700';
      case 'Consulting':  return 'bg-amber-100 text-amber-700';
      case 'Ignore':      return 'bg-slate-100 text-slate-400';
      default:            return 'bg-slate-100 text-slate-500';
    }
  }

  llmStatusClass(status: string): string {
    switch (status) {
      case 'completed': return 'text-green-600';
      case 'failed':    return 'text-red-500';
      default:          return 'text-slate-400';
    }
  }

  confidenceBadge(c: MarketCluster): { label: string; css: string } {
    if (c.llmConfidence == null) return { label: 'Legacy synthesis', css: 'bg-slate-100 text-slate-500' };
    if (c.llmConfidence >= 0.8)  return { label: `${pct(c.llmConfidence)} confidence`, css: 'bg-emerald-100 text-emerald-700' };
    if (c.llmConfidence >= 0.6)  return { label: `${pct(c.llmConfidence)} confidence`, css: 'bg-amber-100 text-amber-700' };
    return { label: `${pct(c.llmConfidence)} confidence`, css: 'bg-red-100 text-red-600' };
  }

  frictionLabel(value: number): string {
    if (value <= 5) return 'Low Friction';
    if (value <= 40) return `${value.toFixed(0)}% friction`;
    return `${value.toFixed(0)}% friction`;
  }

  frictionBarClass(value: number): string {
    if (value <= 5) return 'bg-emerald-400';
    if (value <= 40) return 'bg-amber-400';
    return 'bg-red-400';
  }

  scoreBar(value: number, max = 100): number {
    return Math.min(100, Math.round((value / max) * 100));
  }

  fmt(value: number, decimals = 1): string {
    return value.toFixed(decimals);
  }

  fmtTam(tam: number): string {
    if (tam >= 1_000) return `$${(tam / 1_000).toFixed(1)}B`;
    if (tam >= 1) return `$${tam.toFixed(0)}M`;
    return '<$1M';
  }

  plural(count: number, singular: string, plural: string): string {
    return `${count} ${count === 1 ? singular : plural}`;
  }
}

function pct(v: number): string {
  return `${Math.round(v * 100)}%`;
}
