import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { OpportunityIdeaService } from '../../services/opportunity-idea.service';
import { OpportunityService } from '../../services/opportunity.service';
import { AppSumoReviewForIdea, OpportunityIdea, Opportunity, UpdateOpportunityIdeaRequest } from '../../models/market.models';

@Component({
  selector: 'app-opportunity-ideas',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './opportunity-ideas.html',
})
export class OpportunityIdeas implements OnInit {
  private readonly ideaService        = inject(OpportunityIdeaService);
  private readonly opportunityService = inject(OpportunityService);

  ideas: OpportunityIdea[]   = [];
  opportunities: Opportunity[] = [];
  loading   = false;
  loadError = '';

  // Edit modal state
  editingIdea: OpportunityIdea | null = null;
  editForm: UpdateOpportunityIdeaRequest = { name: '', businessJustification: '', opportunityId: null };
  saving    = false;
  saveError = '';

  // Convert state
  convertingIds = new Set<string>();
  convertErrors = new Map<string, string>();

  // Drawer state
  drawerIdea: OpportunityIdea | null = null;
  drawerReviews: AppSumoReviewForIdea[] = [];
  drawerLoading = false;
  drawerError   = '';

  ngOnInit(): void {
    this.loadIdeas();
    this.loadOpportunities();
  }

  loadIdeas(): void {
    this.loading   = true;
    this.loadError = '';
    this.ideaService.getAll().subscribe({
      next:  (ideas) => { this.ideas = ideas; this.loading = false; },
      error: () => { this.loadError = 'Failed to load ideas.'; this.loading = false; }
    });
  }

  loadOpportunities(): void {
    this.opportunityService.getOpportunities({ pageSize: 100 }).subscribe({
      next:  (resp) => { this.opportunities = resp.items; },
      error: () => {}
    });
  }

  get linkedCount(): number    { return this.ideas.filter(i => i.opportunityId != null).length; }
  get unlinkedCount(): number  { return this.ideas.filter(i => i.opportunityId == null).length; }
  get appSumoCount(): number   { return this.ideas.filter(i => i.source === 'AppSumo').length; }

  sourceBadgeClass(source: string): string {
    switch (source) {
      case 'AppSumo':  return 'bg-orange-100 text-orange-800';
      case 'Upwork':   return 'bg-green-100 text-green-800';
      default:         return 'bg-blue-100 text-blue-800';
    }
  }

  tacoArray(n: number): unknown[] {
    return Array.from({ length: Math.max(0, n) });
  }

  openEdit(idea: OpportunityIdea): void {
    this.editingIdea = idea;
    this.editForm    = {
      name:                  idea.name,
      businessJustification: idea.businessJustification,
      opportunityId:         idea.opportunityId ?? null,
      source:                idea.source
    };
    this.saveError = '';
  }

  closeEdit(): void {
    this.editingIdea = null;
    this.saveError   = '';
  }

  saveEdit(): void {
    if (!this.editingIdea || this.saving) return;
    if (!this.editForm.name.trim()) { this.saveError = 'Name is required.'; return; }

    this.saving    = true;
    this.saveError = '';

    this.ideaService.update(this.editingIdea.id, this.editForm).subscribe({
      next: (updated) => {
        const idx = this.ideas.findIndex(i => i.id === updated.id);
        if (idx !== -1) this.ideas[idx] = updated;
        this.saving = false;
        this.closeEdit();
      },
      error: () => {
        this.saveError = 'Save failed. Please try again.';
        this.saving    = false;
      }
    });
  }

  convertIdea(idea: OpportunityIdea): void {
    if (this.convertingIds.has(idea.id) || idea.opportunityId != null) return;
    this.convertingIds.add(idea.id);
    this.convertErrors.delete(idea.id);

    this.ideaService.convert(idea.id).subscribe({
      next: (updated) => {
        const idx = this.ideas.findIndex(i => i.id === updated.id);
        if (idx !== -1) this.ideas[idx] = updated;
        this.convertingIds.delete(idea.id);
      },
      error: () => {
        this.convertErrors.set(idea.id, 'Convert failed.');
        this.convertingIds.delete(idea.id);
      }
    });
  }

  openDrawer(idea: OpportunityIdea): void {
    this.drawerIdea    = idea;
    this.drawerReviews = [];
    this.drawerError   = '';

    if (idea.appSumoProductId != null) {
      this.drawerLoading = true;
      this.ideaService.getReviews(idea.id).subscribe({
        next:  (reviews) => { this.drawerReviews = reviews; this.drawerLoading = false; },
        error: () => { this.drawerError = 'Failed to load reviews.'; this.drawerLoading = false; }
      });
    }
  }

  closeDrawer(): void {
    this.drawerIdea    = null;
    this.drawerReviews = [];
    this.drawerError   = '';
    this.drawerLoading = false;
  }

  opportunityLabel(opp: Opportunity): string {
    return `${opp.company} — ${opp.jobTitle}`;
  }
}
