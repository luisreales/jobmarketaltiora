import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ClusterLead, MarketCluster, MarketTrend } from '../../models/market.models';
import { ClusterService } from '../../services/cluster.service';
import { MarketService } from '../../services/market.service';

@Component({
  selector: 'app-opportunities',
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './opportunities.html',
  styleUrl: './opportunities.scss'
})
export class Opportunities implements OnInit {
  // ── Clusters ─────────────────────────────────────────────────────────────────
  clusters: MarketCluster[] = [];
  loading = false;
  page = 1;
  pageSize = 12;
  totalPages = 1;
  totalCount = 0;

  // ── Filters ───────────────────────────────────────────────────────────────────
  filterOnlyActionable = false;
  filterOpportunityType = '';
  filterIndustry = '';
  filterMinBlueOcean: number | null = null;

  // ── Leads panel ───────────────────────────────────────────────────────────────
  selectedCluster: MarketCluster | null = null;
  leads: ClusterLead[] = [];
  leadsLoading = false;
  leadsPage = 1;
  leadsPageSize = 8;
  leadsTotalPages = 1;
  leadsTotalCount = 0;

  // ── Trends ────────────────────────────────────────────────────────────────────
  trends: MarketTrend[] = [];

  // ── Admin actions ─────────────────────────────────────────────────────────────
  rebuildLoading = false;
  rebuildResult: string | null = null;
  backfillLoading = false;
  backfillResult: string | null = null;
  cleanupLoading = false;
  cleanupResult: string | null = null;

  // ── On-demand LLM synthesis ───────────────────────────────────────────────────
  synthesisLoadingIds = new Set<number>();
  synthesisExpandedIds = new Set<number>();

  private readonly clusterService = inject(ClusterService);
  private readonly marketService  = inject(MarketService);
  private readonly router         = inject(Router);
  private readonly route          = inject(ActivatedRoute);

  // ── KPIs ──────────────────────────────────────────────────────────────────────

  get kpiActionable(): number {
    return this.clusters.filter(c => c.isActionable).length;
  }

  get kpiTopBlueOcean(): number {
    if (this.clusters.length === 0) return 0;
    return Math.max(...this.clusters.map(c => c.blueOceanScore));
  }

  get kpiAvgPriority(): number {
    if (this.clusters.length === 0) return 0;
    return this.clusters.reduce((s, c) => s + c.priorityScore, 0) / this.clusters.length;
  }

  get kpiTopTrend(): string {
    if (this.trends.length === 0) return 'N/A';
    return [...this.trends].sort((a, b) => b.trendPercentage - a.trendPercentage)[0].painCategory;
  }

  // ── Lifecycle ─────────────────────────────────────────────────────────────────

  ngOnInit(): void {
    this.restoreFromParams();
    this.loadClusters();
    this.loadTrends();
    if (this.selectedCluster) {
      this.loadLeads();
    }
  }

  // ── User actions ──────────────────────────────────────────────────────────────

  applyFilters(): void {
    this.page = 1;
    this.selectedCluster = null;
    this.leads = [];
    this.syncToParams();
    this.loadClusters();
  }

  openLeads(cluster: MarketCluster): void {
    if (this.selectedCluster?.id === cluster.id) {
      this.selectedCluster = null;
      this.leads = [];
      this.syncToParams();
      return;
    }
    this.selectedCluster = cluster;
    this.leadsPage = 1;
    this.syncToParams();
    this.loadLeads();
  }

  clearLeads(): void {
    this.selectedCluster = null;
    this.leads = [];
    this.leadsPage = 1;
    this.leadsTotalCount = 0;
    this.leadsTotalPages = 1;
    this.syncToParams();
  }

  prevPage(): void {
    if (this.page <= 1 || this.loading) return;
    this.page--;
    this.syncToParams();
    this.loadClusters();
  }

  nextPage(): void {
    if (this.page >= this.totalPages || this.loading) return;
    this.page++;
    this.syncToParams();
    this.loadClusters();
  }

  prevLeadsPage(): void {
    if (this.leadsPage <= 1 || this.leadsLoading) return;
    this.leadsPage--;
    this.syncToParams();
    this.loadLeads();
  }

  nextLeadsPage(): void {
    if (this.leadsPage >= this.leadsTotalPages || this.leadsLoading) return;
    this.leadsPage++;
    this.syncToParams();
    this.loadLeads();
  }

  triggerRebuild(): void {
    if (this.rebuildLoading) return;
    this.rebuildLoading = true;
    this.rebuildResult = null;
    this.clusterService.rebuild().subscribe({
      next: (r) => {
        this.rebuildResult = `Clusters: ${r.clustersUpserted} upserted · ${r.actionableClusters} actionable`;
        this.rebuildLoading = false;
        this.loadClusters();
      },
      error: () => {
        this.rebuildResult = 'Error al reconstruir clusters';
        this.rebuildLoading = false;
      }
    });
  }

  triggerBackfill(): void {
    if (this.backfillLoading) return;
    this.backfillLoading = true;
    this.backfillResult = null;
    this.clusterService.backfillInsights().subscribe({
      next: (r) => {
        this.backfillResult = r.message;
        this.backfillLoading = false;
        this.loadClusters();
      },
      error: () => {
        this.backfillResult = 'Error en backfill';
        this.backfillLoading = false;
      }
    });
  }

  triggerCleanup(): void {
    if (this.cleanupLoading) return;
    this.cleanupLoading = true;
    this.cleanupResult = null;

    this.clusterService.cleanupSmallClusters(5).subscribe({
      next: (r) => {
        this.cleanupResult = r.message;
        this.cleanupLoading = false;
        this.loadClusters();
      },
      error: () => {
        this.cleanupResult = 'Error en cleanup de clusters';
        this.cleanupLoading = false;
      }
    });
  }

  synthesizeCluster(cluster: MarketCluster): void {
    if (this.synthesisLoadingIds.has(cluster.id)) return;

    // Toggle collapse if already expanded
    if (this.synthesisExpandedIds.has(cluster.id)) {
      this.synthesisExpandedIds.delete(cluster.id);
      return;
    }

    // If already synthesized, just expand
    if (cluster.llmStatus === 'completed' && cluster.synthesizedPain) {
      this.synthesisExpandedIds.add(cluster.id);
      return;
    }

    this.synthesisLoadingIds.add(cluster.id);
    this.clusterService.synthesize(cluster.id).subscribe({
      next: (updated) => {
        // Patch the cluster in the list with the synthesized data
        const idx = this.clusters.findIndex(c => c.id === cluster.id);
        if (idx !== -1) this.clusters[idx] = updated;
        this.synthesisExpandedIds.add(cluster.id);
        this.synthesisLoadingIds.delete(cluster.id);
      },
      error: () => {
        this.synthesisLoadingIds.delete(cluster.id);
      }
    });
  }

  isSynthesisLoading(cluster: MarketCluster): boolean {
    return this.synthesisLoadingIds.has(cluster.id);
  }

  isSynthesisExpanded(cluster: MarketCluster): boolean {
    return this.synthesisExpandedIds.has(cluster.id);
  }

  // ── Display helpers ───────────────────────────────────────────────────────────

  opportunityTypeClass(type: string): string {
    switch (type) {
      case 'MVPProduct': return 'bg-green-100 text-green-800';
      case 'QuickWin':   return 'bg-yellow-100 text-yellow-800';
      case 'Consulting': return 'bg-blue-100 text-blue-800';
      default:           return 'bg-slate-100 text-slate-500';
    }
  }

  llmStatusClass(status: string): string {
    switch (status) {
      case 'completed':    return 'bg-emerald-100 text-emerald-700';
      case 'done':         return 'bg-emerald-100 text-emerald-700';
      case 'failed':       return 'bg-red-100 text-red-600';
      case 'needs_review': return 'bg-orange-100 text-orange-700';
      case 'skipped':      return 'bg-slate-100 text-slate-500';
      default:             return 'bg-indigo-50 text-indigo-500';  // pending
    }
  }

  growthArrow(rate: number): string {
    if (rate > 0.05) return '↑';
    if (rate < -0.05) return '↓';
    return '→';
  }

  growthClass(rate: number): string {
    if (rate > 0.05) return 'text-green-600';
    if (rate < -0.05) return 'text-red-500';
    return 'text-slate-400';
  }

  techTokens(stack: string): string[] {
    if (!stack || stack === 'Unknown') return [];
    return stack.split(',').map(t => t.trim()).filter(Boolean);
  }

  getJobDetailQueryParams(): Record<string, string> {
    return { returnUrl: this.router.url };
  }

  // ── Private ───────────────────────────────────────────────────────────────────

  private loadClusters(): void {
    this.loading = true;
    this.clusterService.getClusters({
      page: this.page,
      pageSize: this.pageSize,
      isActionable: this.filterOnlyActionable ? true : undefined,
      opportunityType: this.filterOpportunityType || undefined,
      industry: this.filterIndustry || undefined,
      minBlueOceanScore: this.filterMinBlueOcean ?? undefined
    }).subscribe({
      next: (r) => {
        this.clusters = r.items;
        this.totalCount = r.totalCount;
        this.totalPages = r.totalPages;
        this.loading = false;
      },
      error: () => {
        this.clusters = [];
        this.loading = false;
      }
    });
  }

  private loadLeads(): void {
    if (!this.selectedCluster) return;
    this.leadsLoading = true;
    this.clusterService.getClusterLeads(this.selectedCluster.id, {
      page: this.leadsPage,
      pageSize: this.leadsPageSize
    }).subscribe({
      next: (r) => {
        this.leads = r.items;
        this.leadsTotalCount = r.totalCount;
        this.leadsTotalPages = r.totalPages;
        this.leadsLoading = false;
      },
      error: () => {
        this.leads = [];
        this.leadsLoading = false;
      }
    });
  }

  private loadTrends(): void {
    this.marketService.getTrends({ windowDays: 14 }).subscribe({
      next: (r) => { this.trends = r; },
      error: () => { this.trends = []; }
    });
  }

  private restoreFromParams(): void {
    const p = this.route.snapshot.queryParamMap;
    const pg = Number(p.get('page'));
    if (Number.isFinite(pg) && pg > 0) this.page = pg;

    const oa = p.get('onlyActionable');
    if (oa === 'true') this.filterOnlyActionable = true;

    const ot = p.get('opportunityType');
    if (ot) this.filterOpportunityType = ot;

    const ind = p.get('industry');
    if (ind) this.filterIndustry = ind;

    const mb = Number(p.get('minBlueOcean'));
    if (Number.isFinite(mb) && mb > 0) this.filterMinBlueOcean = mb;

    const cid = Number(p.get('clusterId'));
    if (Number.isFinite(cid) && cid > 0) {
      // Restore selected cluster ID — full object loads after getClusters resolves
      this.leadsPage = Number(p.get('leadsPage')) || 1;
    }
  }

  private syncToParams(): void {
    const params: Record<string, string | null> = {
      page:             this.page > 1 ? String(this.page) : null,
      onlyActionable:   this.filterOnlyActionable ? 'true' : null,
      opportunityType:  this.filterOpportunityType || null,
      industry:         this.filterIndustry || null,
      minBlueOcean:     this.filterMinBlueOcean != null ? String(this.filterMinBlueOcean) : null,
      clusterId:        this.selectedCluster ? String(this.selectedCluster.id) : null,
      leadsPage:        this.selectedCluster && this.leadsPage > 1 ? String(this.leadsPage) : null
    };
    this.router.navigate([], { relativeTo: this.route, queryParams: params, replaceUrl: true });
  }
}
