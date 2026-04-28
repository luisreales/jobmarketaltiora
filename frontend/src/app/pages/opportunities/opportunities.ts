import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { Opportunity } from '../../models/market.models';
import { OpportunityService } from '../../services/opportunity.service';

@Component({
  selector: 'app-opportunities',
  imports: [CommonModule, RouterLink],
  templateUrl: './opportunities.html',
  styleUrl: './opportunities.scss'
})
export class Opportunities implements OnInit {
  pipeline: Opportunity[] = [];
  pipelineLoading = false;
  pipelinePage = 1;
  pipelinePageSize = 20;
  pipelineTotalPages = 1;
  pipelineTotalCount = 0;

  deletingIds = new Set<number>();
  deleteErrors = new Map<number, string>();

  private readonly opportunityService = inject(OpportunityService);

  ngOnInit(): void {
    this.loadPipeline();
  }

  prevPipelinePage(): void {
    if (this.pipelinePage <= 1 || this.pipelineLoading) return;
    this.pipelinePage--;
    this.loadPipeline();
  }

  nextPipelinePage(): void {
    if (this.pipelinePage >= this.pipelineTotalPages || this.pipelineLoading) return;
    this.pipelinePage++;
    this.loadPipeline();
  }

  deleteOpportunity(id: number): void {
    if (this.deletingIds.has(id)) return;
    this.deletingIds.add(id);
    this.deleteErrors.delete(id);

    this.opportunityService.deleteOpportunity(id).subscribe({
      next: () => {
        this.deletingIds.delete(id);
        this.pipeline = this.pipeline.filter(o => o.id !== id);
        this.pipelineTotalCount = Math.max(0, this.pipelineTotalCount - 1);
      },
      error: () => {
        this.deletingIds.delete(id);
        this.deleteErrors.set(id, 'Delete failed. Retry?');
      }
    });
  }

  analysisStatusClass(llmStatus: string): string {
    switch (llmStatus) {
      case 'completed': return 'bg-green-100 text-green-700';
      case 'failed':    return 'bg-red-100 text-red-700';
      default:          return 'bg-amber-100 text-amber-700';
    }
  }

  stageClass(status: string): string {
    return status === 'converted'
      ? 'bg-emerald-100 text-emerald-700'
      : 'bg-blue-100 text-blue-700';
  }

  countByStatus(status: string): number {
    return this.pipeline.filter(o => o.status === status).length;
  }

  private loadPipeline(): void {
    this.pipelineLoading = true;
    this.opportunityService.getOpportunities({
      page: this.pipelinePage,
      pageSize: this.pipelinePageSize
    }).subscribe({
      next: (r) => {
        this.pipeline = r.items;
        this.pipelineTotalCount = r.totalCount;
        this.pipelineTotalPages = r.totalPages;
        this.pipelineLoading = false;
      },
      error: () => {
        this.pipeline = [];
        this.pipelineLoading = false;
      }
    });
  }
}
