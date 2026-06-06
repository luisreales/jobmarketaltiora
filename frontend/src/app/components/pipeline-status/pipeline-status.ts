import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MarketCluster } from '../../models/market.models';

export interface PipelineStage {
  label: string;
  status: 'ok' | 'warn' | 'blocked' | 'unknown';
  detail: string;
}

@Component({
  selector: 'app-pipeline-status',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule],
  template: `
    <div class="bg-white border border-slate-200 rounded-xl p-4">
      <h3 class="text-xs font-semibold text-slate-500 uppercase tracking-wide mb-3">Pipeline Status</h3>
      <div class="space-y-1.5">
        @for (stage of stages; track stage.label) {
          <div class="flex items-center gap-3">
            <span class="w-2 h-2 rounded-full flex-shrink-0 {{ dotClass(stage.status) }}"></span>
            <span class="text-xs font-medium text-slate-700 w-36 flex-shrink-0">{{ stage.label }}</span>
            <span class="text-xs text-slate-400 truncate">{{ stage.detail }}</span>
          </div>
        }
      </div>
    </div>
  `
})
export class PipelineStatusComponent {
  @Input() set clusters(value: MarketCluster[]) { this._clusters = value; this.buildStages(); }
  @Input() set totalJobs(value: number) { this._totalJobs = value; this.buildStages(); }
  @Input() set totalProducts(value: number) { this._totalProducts = value; this.buildStages(); }

  private _clusters: MarketCluster[] = [];
  private _totalJobs = 0;
  private _totalProducts = 0;

  stages: PipelineStage[] = [];

  private buildStages(): void {
    const c = this._clusters;
    const hasInsights = c.length > 0;
    const hasActionable = c.some(x => x.isActionable);
    const hasEmbeddings = c.some(x => x.semanticGroupKey != null);
    const hasV2 = c.some(x => x.priorityScoreV2 > 0);
    const hasSynthesis = c.some(x => x.llmStatus === 'completed');
    const pendingInsights = this._totalJobs > 0 && c.length === 0;

    this.stages = [
      {
        label: 'Worker 1',
        status: hasInsights ? 'ok' : pendingInsights ? 'warn' : 'unknown',
        detail: hasInsights
          ? `${c.reduce((s, x) => s + x.jobCount, 0)} insights processed`
          : 'No insights yet'
      },
      {
        label: 'Cluster Engine',
        status: c.length > 0 ? 'ok' : 'warn',
        detail: c.length > 0 ? `${c.length} clusters built` : 'No clusters — run rebuild'
      },
      {
        label: 'Semantic Clustering',
        status: hasEmbeddings ? 'ok' : 'blocked',
        detail: hasEmbeddings
          ? `${c.filter(x => x.semanticGroupKey).length} groups assigned`
          : 'EmbeddingModelId not configured'
      },
      {
        label: 'Opportunity Engine',
        status: hasV2 ? 'ok' : hasActionable ? 'warn' : 'unknown',
        detail: hasV2
          ? `${c.filter(x => x.priorityScoreV2 > 0).length} clusters enriched`
          : 'PriorityScoreV2 not computed yet'
      },
      {
        label: 'LLM Synthesis',
        status: hasSynthesis ? 'ok' : hasActionable ? 'warn' : 'unknown',
        detail: hasSynthesis
          ? `${c.filter(x => x.llmStatus === 'completed').length} synthesized`
          : 'No clusters synthesized yet'
      },
      {
        label: 'Product Generator',
        status: this._totalProducts > 0 ? 'ok' : 'warn',
        detail: this._totalProducts > 0
          ? `${this._totalProducts} products generated`
          : 'No products generated yet'
      }
    ];
  }

  dotClass(status: PipelineStage['status']): string {
    switch (status) {
      case 'ok':      return 'bg-emerald-500';
      case 'warn':    return 'bg-amber-400';
      case 'blocked': return 'bg-red-500';
      default:        return 'bg-slate-300';
    }
  }
}
