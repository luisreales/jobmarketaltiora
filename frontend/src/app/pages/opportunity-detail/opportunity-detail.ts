import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { OpportunityService } from '../../services/opportunity.service';
import { Opportunity, ProductIdea } from '../../models/market.models';

@Component({
  selector: 'app-opportunity-detail',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './opportunity-detail.html'
})
export class OpportunityDetail implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly opportunityService = inject(OpportunityService);

  opportunityId = 0;
  opportunity: Opportunity | null = null;
  loading = false;
  synthesizing = false;
  synthesisError = '';

  // Product idea creation state — keyed by idea.id slug for persistent deduplication
  creatingIdeaIds = new Set<string>();
  createErrors    = new Map<string, string>();

  // Local mirror of convertedIdeaIds from the server (updated optimistically on create)
  convertedIdeaIds = new Set<string>();

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    if (!id) { this.router.navigate(['/opportunities']); return; }
    this.opportunityId = id;
    this.loadOpportunity();
  }

  loadOpportunity(): void {
    this.loading = true;
    this.opportunityService.getOpportunity(this.opportunityId).subscribe({
      next: (o) => {
        this.opportunity = o;
        this.convertedIdeaIds = new Set(o.convertedIdeaIds ?? []);
        this.loading = false;
      },
      error: () => { this.loading = false; }
    });
  }

  analyzeOpportunity(): void {
    if (this.synthesizing) return;
    this.synthesizing = true;
    this.synthesisError = '';

    this.opportunityService.synthesizeIdeas(this.opportunityId).subscribe({
      next: (o) => {
        this.opportunity = o;
        this.convertedIdeaIds = new Set(o.convertedIdeaIds ?? []);
        this.synthesizing = false;
      },
      error: () => {
        this.synthesizing = false;
        this.synthesisError = 'AI analysis failed. Please try again.';
      }
    });
  }

  createProduct(idea: ProductIdea): void {
    const ideaKey = idea.id;
    if (this.creatingIdeaIds.has(ideaKey) || this.convertedIdeaIds.has(ideaKey)) return;
    this.creatingIdeaIds.add(ideaKey);
    this.createErrors.delete(ideaKey);

    this.opportunityService.createProductFromOpportunity({
      opportunityId: this.opportunityId,
      name: idea.name,
      shortTechnicalDescription: idea.businessJustification ?? idea.shortTechnicalDescription ?? idea.name,
      sourceIdeaId: idea.id
    }).subscribe({
      next: () => {
        this.creatingIdeaIds.delete(ideaKey);
        this.convertedIdeaIds.add(ideaKey);
      },
      error: (err) => {
        this.creatingIdeaIds.delete(ideaKey);
        if (err?.status === 409) {
          this.convertedIdeaIds.add(ideaKey);
          this.createErrors.delete(ideaKey);
          return;
        }
        this.createErrors.set(ideaKey, 'Failed. Retry?');
      }
    });
  }

  get productIdeas(): ProductIdea[] {
    if (!this.opportunity?.productIdeasJson) return [];
    try {
      return JSON.parse(this.opportunity.productIdeasJson) as ProductIdea[];
    } catch {
      return [];
    }
  }

  get statusBadgeClass(): string {
    switch (this.opportunity?.llmStatus) {
      case 'completed': return 'bg-green-100 text-green-700';
      case 'failed':    return 'bg-red-100 text-red-700';
      default:          return 'bg-amber-100 text-amber-700';
    }
  }

  goBack(): void {
    this.router.navigate(['/opportunities']);
  }
}
