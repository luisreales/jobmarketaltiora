import { Component, OnInit } from '@angular/core';
import { CommonModule, PercentPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ProductService } from '../../services/product.service';
import { ProductGenerateResult, ProductQuery, ProductSuggestion } from '../../models/market.models';

@Component({
  selector: 'app-products',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, PercentPipe],
  templateUrl: './products.html',
})
export class Products implements OnInit {
  products: ProductSuggestion[] = [];
  loading = false;
  page = 1;
  pageSize = 20;
  totalPages = 1;
  totalCount = 0;

  filterOpportunityType = '';
  filterIndustry = '';
  searchTerm = '';

  generateLoading = false;
  generateResult: string | null = null;

  constructor(private readonly productService: ProductService) {}

  ngOnInit(): void {
    this.loadProducts();
  }

  get filteredProducts(): ProductSuggestion[] {
    const term = this.searchTerm.trim().toLowerCase();
    if (!term) return this.products;
    return this.products.filter(p =>
      p.productName.toLowerCase().includes(term) ||
      p.techFocus.toLowerCase().includes(term) ||
      p.industry.toLowerCase().includes(term) ||
      p.whyNow.toLowerCase().includes(term)
    );
  }

  get topThree(): ProductSuggestion[] {
    return this.filteredProducts.slice(0, 3);
  }

  get restProducts(): ProductSuggestion[] {
    return this.filteredProducts.slice(3);
  }

  get totalDealPotential(): string {
    const total = this.topThree.reduce((sum, p) => sum + p.maxDealSizeUsd, 0);
    return total >= 1000 ? `$${(total / 1000).toFixed(0)}K` : `$${total}`;
  }

  get avgBuildDays(): number {
    if (this.topThree.length === 0) return 0;
    return Math.round(this.topThree.reduce((sum, p) => sum + p.estimatedBuildDays, 0) / this.topThree.length);
  }

  loadProducts(): void {
    this.loading = true;
    const query: ProductQuery = {
      page: this.page,
      pageSize: this.pageSize,
      opportunityType: this.filterOpportunityType || undefined,
      industry: this.filterIndustry || undefined,
    };

    this.productService.getProducts(query).subscribe({
      next: (resp) => {
        this.products = resp.items;
        this.totalCount = resp.totalCount;
        this.totalPages = resp.totalPages;
        this.loading = false;
      },
      error: () => { this.loading = false; }
    });
  }

  applyFilters(): void {
    this.page = 1;
    this.generateResult = null;
    this.loadProducts();
  }

  clearSearch(): void {
    this.searchTerm = '';
  }

  prevPage(): void {
    if (this.page > 1) { this.page--; this.loadProducts(); }
  }

  nextPage(): void {
    if (this.page < this.totalPages) { this.page++; this.loadProducts(); }
  }

  triggerGenerate(): void {
    this.generateLoading = true;
    this.generateResult = null;

    this.productService.generate().subscribe({
      next: (result: ProductGenerateResult) => {
        this.generateResult = `${result.productsGenerated} productos generados · ${result.actionableClusters} clusters accionables`;
        this.generateLoading = false;
        this.page = 1;
        this.loadProducts();
      },
      error: () => {
        this.generateResult = 'Error al generar productos. Verifica que existan clusters accionables.';
        this.generateLoading = false;
      }
    });
  }

  opportunityTypeClass(type: string): string {
    switch (type) {
      case 'MVPProduct':  return 'bg-purple-100 text-purple-700';
      case 'QuickWin':    return 'bg-green-100 text-green-700';
      case 'Consulting':  return 'bg-amber-100 text-amber-700';
      default:            return 'bg-slate-100 text-slate-500';
    }
  }

  dealRange(p: ProductSuggestion): string {
    const fmt = (v: number) => v >= 1000 ? `$${(v / 1000).toFixed(0)}K` : `$${v}`;
    return `${fmt(p.minDealSizeUsd)}–${fmt(p.maxDealSizeUsd)}`;
  }

  urgencyLabel(score: number): string {
    if (score >= 8) return 'Crítica';
    if (score >= 7) return 'Alta';
    if (score >= 5) return 'Media';
    return 'Normal';
  }

  urgencyClass(score: number): string {
    if (score >= 7) return 'text-red-600 font-semibold';
    if (score >= 5) return 'text-amber-600';
    return 'text-slate-500';
  }

  techTokens(techFocus: string): string[] {
    return techFocus.split('+').map(t => t.trim()).filter(Boolean);
  }
}
