import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { OpportunityIdeaService } from '../../services/opportunity-idea.service';
import { OpportunityService } from '../../services/opportunity.service';
import { OpportunityIdea, Opportunity, UpdateOpportunityIdeaRequest } from '../../models/market.models';

@Component({
  selector: 'app-opportunity-ideas',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './opportunity-ideas.html',
})
export class OpportunityIdeas implements OnInit {
  private readonly ideaService     = inject(OpportunityIdeaService);
  private readonly opportunityService = inject(OpportunityService);

  ideas: OpportunityIdea[]   = [];
  opportunities: Opportunity[] = [];
  loading   = false;
  loadError = '';

  // Edit modal state
  editingIdea: OpportunityIdea | null = null;
  editForm: UpdateOpportunityIdeaRequest = { name: '', businessJustification: '', opportunityId: null };
  saving     = false;
  saveError  = '';

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
      error: () => {}  // dropdown degrades gracefully
    });
  }

  get linkedCount(): number   { return this.ideas.filter(i => i.opportunityId != null).length; }
  get unlinkedCount(): number { return this.ideas.filter(i => i.opportunityId == null).length; }

  openEdit(idea: OpportunityIdea): void {
    this.editingIdea = idea;
    this.editForm    = {
      name:                 idea.name,
      businessJustification: idea.businessJustification,
      opportunityId:        idea.opportunityId ?? null
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

  opportunityLabel(opp: Opportunity): string {
    return `${opp.company} — ${opp.jobTitle}`;
  }
}
