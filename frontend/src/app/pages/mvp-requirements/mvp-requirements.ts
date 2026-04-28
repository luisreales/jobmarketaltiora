import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MvpRequirementService } from '../../services/mvp-requirement.service';
import { ProductService } from '../../services/product.service';
import {
  MvpRequirementRecord,
  MvpRequirementQuery,
  ProductSuggestion
} from '../../models/market.models';

@Component({
  selector: 'app-mvp-requirements',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './mvp-requirements.html'
})
export class MvpRequirements implements OnInit {
  private readonly svc     = inject(MvpRequirementService);
  private readonly prodSvc = inject(ProductService);

  items: MvpRequirementRecord[] = [];
  products: ProductSuggestion[] = [];
  loading   = false;
  loadError = '';

  // Pagination
  page       = 1;
  pageSize   = 20;
  totalCount = 0;
  totalPages = 1;

  // Search
  searchText = '';

  // Generate form
  showGenerate  = false;
  generateForm  = { context: '', productId: null as number | null };
  generating    = false;
  generateError = '';

  // Link modal
  linkingItem: MvpRequirementRecord | null = null;
  linkProductId: number | null = null;
  linking   = false;
  linkError = '';

  // Expanded row
  expandedId: number | null = null;

  // Delete
  deletingIds = new Set<number>();

  ngOnInit(): void {
    this.load();
    this.loadProducts();
  }

  load(): void {
    this.loading   = true;
    this.loadError = '';
    const query: MvpRequirementQuery = {
      search: this.searchText.trim() || undefined,
      page: this.page,
      pageSize: this.pageSize
    };
    this.svc.getAll(query).subscribe({
      next: (r) => {
        this.items      = r.items;
        this.totalCount = r.totalCount;
        this.totalPages = r.totalPages;
        this.loading    = false;
      },
      error: () => { this.items = []; this.totalCount = 0; this.totalPages = 1; this.loading = false; }
    });
  }

  loadProducts(): void {
    this.prodSvc.getProducts({ pageSize: 100 }).subscribe({
      next:  (r) => { this.products = r.items; },
      error: () => {}
    });
  }

  onSearch(): void { this.page = 1; this.load(); }
  prevPage(): void { if (this.page > 1) { this.page--; this.load(); } }
  nextPage(): void { if (this.page < this.totalPages) { this.page++; this.load(); } }

  toggleExpand(id: number): void {
    this.expandedId = this.expandedId === id ? null : id;
  }

  parsedTechStack(json: string): string[] {
    try { return JSON.parse(json) as string[]; } catch { return []; }
  }

  parsedFeatures(json: string): string[] {
    try { return JSON.parse(json) as string[]; } catch { return []; }
  }

  // ── Generate ───────────────────────────────────────────────────────────────

  openGenerate(): void {
    this.showGenerate  = true;
    this.generateForm  = { context: '', productId: null };
    this.generateError = '';
  }

  closeGenerate(): void { this.showGenerate = false; this.generateError = ''; }

  submitGenerate(): void {
    if (!this.generateForm.context.trim()) { this.generateError = 'Context is required.'; return; }
    this.generating    = true;
    this.generateError = '';
    this.svc.generate({
      context: this.generateForm.context.trim(),
      productId: this.generateForm.productId
    }).subscribe({
      next: () => { this.generating = false; this.showGenerate = false; this.page = 1; this.load(); },
      error: () => { this.generateError = 'AI generation failed. Please try again.'; this.generating = false; }
    });
  }

  // ── Link to Product ────────────────────────────────────────────────────────

  openLink(item: MvpRequirementRecord): void {
    this.linkingItem  = item;
    this.linkProductId = item.productId ?? null;
    this.linkError    = '';
  }

  closeLink(): void { this.linkingItem = null; this.linkError = ''; }

  saveLink(): void {
    if (!this.linkingItem || this.linking) return;
    this.linking   = true;
    this.linkError = '';
    this.svc.link(this.linkingItem.id, this.linkProductId).subscribe({
      next: (updated) => {
        const idx = this.items.findIndex(i => i.id === updated.id);
        if (idx !== -1) this.items[idx] = updated;
        this.linking = false;
        this.closeLink();
      },
      error: () => { this.linkError = 'Link failed. Please try again.'; this.linking = false; }
    });
  }

  // ── Delete ─────────────────────────────────────────────────────────────────

  deleteItem(id: number): void {
    if (this.deletingIds.has(id)) return;
    this.deletingIds.add(id);
    this.svc.delete(id).subscribe({
      next: () => { this.deletingIds.delete(id); this.items = this.items.filter(i => i.id !== id); this.totalCount = Math.max(0, this.totalCount - 1); },
      error: () => { this.deletingIds.delete(id); }
    });
  }

  get linkedCount(): number   { return this.items.filter(i => i.productId != null).length; }
  get unlinkedCount(): number { return this.items.filter(i => i.productId == null).length; }
}
