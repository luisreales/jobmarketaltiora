import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ProductService } from '../../services/product.service';
import { CommercialStrategy, ProductSuggestion, TechnicalMvp } from '../../models/market.models';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-product-detail',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './product-detail.html',
})
export class ProductDetail implements OnInit {
  readonly apiUrl = environment.apiUrl;
  readonly defaultProductImageUrl = `data:image/svg+xml;charset=UTF-8,${encodeURIComponent(`
    <svg xmlns="http://www.w3.org/2000/svg" width="1200" height="800" viewBox="0 0 1200 800">
      <rect width="1200" height="800" fill="#e2e8f0"/>
      <rect x="80" y="80" width="1040" height="640" rx="28" fill="#f8fafc" stroke="#cbd5e1" stroke-width="8"/>
      <text x="600" y="340" text-anchor="middle" font-family="Arial, sans-serif" font-size="96">🖼️</text>
      <text x="600" y="430" text-anchor="middle" font-family="Arial, sans-serif" font-size="42" fill="#475569">No product image available</text>
      <text x="600" y="485" text-anchor="middle" font-family="Arial, sans-serif" font-size="28" fill="#64748b">Upload an image or add an image URL from the products editor</text>
    </svg>
  `)}`;

  product: ProductSuggestion | null = null;
  loading = false;
  notFound = false;
  productImageFailed = false;
  selectedPopupImageUrl = '';

  strategyLoading = false;
  strategyError: string | null = null;

  technicalMvpLoading = false;
  technicalMvpError: string | null = null;

  closeLoading = false;

  private readonly productService = inject(ProductService);
  private readonly route = inject(ActivatedRoute);

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    if (!id) { this.notFound = true; return; }
    this.loadProduct(id);
  }

  // ── Getters ──────────────────────────────────────────────────────────────────

  get commercialStrategy(): CommercialStrategy | null {
    if (!this.product?.synthesisDetailJson) return null;
    try {
      const parsed = JSON.parse(this.product.synthesisDetailJson) as Record<string, unknown>;
      if ('realBusinessProblem' in parsed) return parsed as unknown as CommercialStrategy;
      return null;
    } catch {
      return null;
    }
  }

  get technicalMvp(): TechnicalMvp | null {
    if (!this.product?.technicalMvpJson) return null;
    try {
      return JSON.parse(this.product.technicalMvpJson) as TechnicalMvp;
    } catch {
      return null;
    }
  }

  get isClosed(): boolean {
    return this.product?.status === 'closed';
  }

  get displayImageUrl(): string {
    if (this.productImageFailed) return this.defaultProductImageUrl;
    return this.resolveImageUrl(this.product?.imageUrl) || this.defaultProductImageUrl;
  }

  // ── Actions ──────────────────────────────────────────────────────────────────

  generateStrategy(): void {
    if (!this.product || this.strategyLoading) return;
    this.strategyLoading = true;
    this.strategyError = null;
    this.productService.synthesizeStrategy(this.product.id).subscribe({
      next: (updated) => {
        this.loadProduct(updated.id, () => { this.strategyLoading = false; });
      },
      error: () => {
        this.strategyError = 'AI strategy generation failed. Please try again.';
        this.strategyLoading = false;
      }
    });
  }

  generateTechnicalMvp(): void {
    if (!this.product || this.technicalMvpLoading) return;
    this.technicalMvpLoading = true;
    this.technicalMvpError = null;
    this.productService.synthesizeTechnicalMvp(this.product.id).subscribe({
      next: (updated) => {
        this.loadProduct(updated.id, () => { this.technicalMvpLoading = false; });
      },
      error: () => {
        this.technicalMvpError = 'AI technical analysis failed. Please try again.';
        this.technicalMvpLoading = false;
      }
    });
  }

  closeProduct(): void {
    if (!this.product || this.closeLoading || this.isClosed) return;
    this.closeLoading = true;
    this.productService.closeProduct(this.product.id).subscribe({
      next: (updated) => {
        this.loadProduct(updated.id, () => { this.closeLoading = false; });
      },
      error: () => {
        this.closeLoading = false;
      }
    });
  }

  openImagePreview(): void {
    this.selectedPopupImageUrl = this.displayImageUrl;
  }

  closeImagePreview(): void {
    this.selectedPopupImageUrl = '';
  }

  onProductImageError(): void {
    this.productImageFailed = true;
  }

  // ── Display helpers ───────────────────────────────────────────────────────────

  techTokens(techFocus: string): string[] {
    if (!techFocus || techFocus === 'To be defined') return [];
    return techFocus.split(/[+,]/).map(t => t.trim()).filter(Boolean);
  }

  dealRange(): string {
    if (!this.product) return '';
    const { minDealSizeUsd, maxDealSizeUsd } = this.product;
    if (minDealSizeUsd === 0 && maxDealSizeUsd === 0) return 'Custom quote';
    const fmt = (v: number) => v >= 1000 ? `$${(v / 1000).toFixed(0)}K` : `$${v}`;
    return `${fmt(minDealSizeUsd)} – ${fmt(maxDealSizeUsd)}`;
  }

  opportunityTypeClass(type: string): string {
    switch (type) {
      case 'MVPProduct':  return 'bg-purple-100 text-purple-700';
      case 'QuickWin':    return 'bg-green-100 text-green-700';
      case 'Consulting':  return 'bg-amber-100 text-amber-700';
      case 'Manual':      return 'bg-blue-100 text-blue-700';
      default:            return 'bg-slate-100 text-slate-500';
    }
  }

  resolveImageUrl(imageUrl?: string | null): string {
    if (!imageUrl) return '';
    if (imageUrl.startsWith('http://') || imageUrl.startsWith('https://') || imageUrl.startsWith('data:')) {
      return imageUrl;
    }
    return `${this.apiUrl}${imageUrl.startsWith('/') ? imageUrl : `/${imageUrl}`}`;
  }

  private loadProduct(id: number, onFinish?: () => void): void {
    this.loading = true;
    this.productService.getProduct(id).subscribe({
      next: (p) => {
        this.product = p;
        this.productImageFailed = false;
        this.notFound = false;
        this.loading = false;
        onFinish?.();
      },
      error: () => {
        this.notFound = true;
        this.loading = false;
        onFinish?.();
      }
    });
  }
}
