import { ChangeDetectionStrategy, ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ClusterService } from '../../services/cluster.service';
import { MarketCluster } from '../../models/market.models';

interface SemanticGroup {
  key: string;
  clusters: MarketCluster[];
  totalJobs: number;
  avgPriorityV2: number;
}

@Component({
  selector: 'app-semantic-groups',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule],
  templateUrl: './semantic-groups.html',
})
export class SemanticGroupsPage implements OnInit {
  private readonly clusterService = inject(ClusterService);
  private readonly cdr = inject(ChangeDetectorRef);

  loading = true;
  groups: SemanticGroup[] = [];
  totalClusters = 0;
  embeddingsAvailable = false;
  fallbackMode = false;

  ngOnInit(): void {
    this.clusterService.getClusters({ pageSize: 200 }).subscribe({
      next: (resp) => {
        this.totalClusters = resp.totalCount;
        this.embeddingsAvailable = resp.items.some(c => c.semanticGroupKey != null);
        this.fallbackMode = !this.embeddingsAvailable && resp.items.length > 0;
        this.groups = this.buildGroups(resp.items);
        this.loading = false;
        this.cdr.markForCheck();
      },
      error: () => {
        this.loading = false;
        this.cdr.markForCheck();
      }
    });
  }

  private buildGroups(clusters: MarketCluster[]): SemanticGroup[] {
    const map = new Map<string, MarketCluster[]>();
    const getKey = (c: MarketCluster) =>
      this.embeddingsAvailable ? c.semanticGroupKey : (c.industry?.trim() || 'Unknown');

    for (const c of clusters) {
      const key = getKey(c);
      if (!key) continue;
      const existing = map.get(key) ?? [];
      existing.push(c);
      map.set(key, existing);
    }

    return Array.from(map.entries())
      .map(([key, items]) => ({
        key,
        clusters: items.sort((a, b) => b.priorityScoreV2 - a.priorityScoreV2),
        totalJobs: items.reduce((s, c) => s + c.jobCount, 0),
        avgPriorityV2: Math.round(items.reduce((s, c) => s + c.priorityScoreV2, 0) / items.length)
      }))
      .sort((a, b) => b.avgPriorityV2 - a.avgPriorityV2);
  }

  trackByGroup(_: number, g: SemanticGroup): string { return g.key; }
  trackByCluster(_: number, c: MarketCluster): number { return c.id; }
}
