import { Component, OnInit } from '@angular/core';
import { CommonModule, PercentPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ProductService } from '../../services/product.service';
import { ProductQuery, ProductSuggestion, UpdateProductRequest } from '../../models/market.models';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-products',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, PercentPipe],
  templateUrl: './products.html',
})
export class Products implements OnInit {
  readonly apiUrl = environment.apiUrl;

  products: ProductSuggestion[] = [];
  loading = false;
  page = 1;
  pageSize = 20;
  totalPages = 1;
  totalCount = 0;

  filterOpportunityType = '';
  filterIndustry = '';
  searchTerm = '';

  deletingProductIds = new Set<number>();
  editingProduct: ProductSuggestion | null = null;
  editModel: UpdateProductRequest = this.createEmptyEditModel();
  savingEdit = false;
  editError = '';
  exporting = false;
  selectedImageFile: File | null = null;
  imagePreviewUrl = '';
  selectedPopupImageUrl = '';
  selectedPopupProductName = '';

  constructor(private readonly productService: ProductService) {}

  ngOnInit(): void {
    this.loadProducts();
  }

  private createEmptyEditModel(): UpdateProductRequest {
    return {
      productName: '',
      productDescription: '',
      whyNow: '',
      offer: '',
      actionToday: '',
      techFocus: '',
      estimatedBuildDays: 0,
      minDealSizeUsd: 0,
      maxDealSizeUsd: 0,
      opportunityType: 'Manual',
      industry: 'Unknown',
      imageUrl: '',
      status: 'open'
    };
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

  countByType(type: string): number {
    return this.products.filter(p => p.opportunityType === type).length;
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
    this.loadProducts();
  }

  clearSearch(): void {
    this.searchTerm = '';
  }

  openEdit(product: ProductSuggestion, event: Event): void {
    event.preventDefault();
    event.stopPropagation();
    this.editingProduct = product;
    this.editError = '';
    this.selectedImageFile = null;
    this.editModel = {
      productName: product.productName,
      productDescription: product.productDescription,
      whyNow: product.whyNow,
      offer: product.offer,
      actionToday: product.actionToday,
      techFocus: product.techFocus,
      estimatedBuildDays: product.estimatedBuildDays,
      minDealSizeUsd: product.minDealSizeUsd,
      maxDealSizeUsd: product.maxDealSizeUsd,
      opportunityType: product.opportunityType,
      industry: product.industry,
      imageUrl: product.imageUrl ?? '',
      status: product.status
    };
    this.imagePreviewUrl = this.resolveImageUrl(product.imageUrl);
  }

  closeEdit(): void {
    this.editingProduct = null;
    this.editModel = this.createEmptyEditModel();
    this.selectedImageFile = null;
    this.imagePreviewUrl = '';
    this.editError = '';
    this.savingEdit = false;
  }

  onImageUrlChanged(): void {
    if (!this.selectedImageFile) {
      this.imagePreviewUrl = this.resolveImageUrl(this.editModel.imageUrl);
    }
  }

  openImagePreview(product: ProductSuggestion, event: Event): void {
    event.preventDefault();
    event.stopPropagation();

    const resolvedUrl = this.resolveImageUrl(product.imageUrl);
    if (!resolvedUrl) return;

    this.selectedPopupImageUrl = resolvedUrl;
    this.selectedPopupProductName = product.productName;
  }

  closeImagePreview(): void {
    this.selectedPopupImageUrl = '';
    this.selectedPopupProductName = '';
  }

  onImageSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;
    this.selectedImageFile = file;

    if (!file) {
      this.imagePreviewUrl = this.resolveImageUrl(this.editModel.imageUrl);
      return;
    }

    this.editModel.imageUrl = '';

    const reader = new FileReader();
    reader.onload = () => {
      this.imagePreviewUrl = typeof reader.result === 'string' ? reader.result : '';
    };
    reader.readAsDataURL(file);
  }

  saveEdit(): void {
    if (!this.editingProduct || this.savingEdit) return;

    const productId = this.editingProduct.id;
    this.savingEdit = true;
    this.editError = '';

    const saveProductDetails = (imageUrl?: string | null): void => {
      const request: UpdateProductRequest = {
        ...this.editModel,
        imageUrl: imageUrl ?? this.editModel.imageUrl
      };

      this.productService.updateProduct(productId, request).subscribe({
        next: (updated) => {
          this.applyUpdatedProduct(updated);
          this.closeEdit();
        },
        error: () => {
          this.savingEdit = false;
          this.editError = 'Could not save product changes.';
        }
      });
    };

    if (this.selectedImageFile) {
      this.productService.uploadProductImage(productId, this.selectedImageFile).subscribe({
        next: (uploaded) => {
          saveProductDetails(uploaded.imageUrl ?? '');
        },
        error: () => {
          this.savingEdit = false;
          this.editError = 'The image upload failed, so the changes were not saved.';
        }
      });
      return;
    }

    saveProductDetails();
  }

  private applyUpdatedProduct(updated: ProductSuggestion): void {
    this.products = this.products.map(p => p.id === updated.id ? updated : p);
  }

  exportProducts(): void {
    if (this.exporting) return;

    this.exporting = true;
    const query: ProductQuery = {
      page: this.page,
      pageSize: this.pageSize,
      opportunityType: this.filterOpportunityType || undefined,
      industry: this.filterIndustry || undefined,
    };

    this.productService.exportProductsCsv(query).subscribe({
      next: (blob) => {
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = `products-${new Date().toISOString().slice(0, 10)}.csv`;
        link.click();
        URL.revokeObjectURL(url);
        this.exporting = false;
      },
      error: () => {
        this.exporting = false;
      }
    });
  }

  deleteProduct(id: number, event: Event): void {
    event.preventDefault();
    event.stopPropagation();
    if (this.deletingProductIds.has(id)) return;
    if (!confirm('Delete this product permanently? This cannot be undone.')) return;

    this.deletingProductIds.add(id);
    this.productService.deleteProduct(id).subscribe({
      next: () => {
        this.deletingProductIds.delete(id);
        this.products = this.products.filter(p => p.id !== id);
        this.totalCount = Math.max(0, this.totalCount - 1);
      },
      error: () => {
        this.deletingProductIds.delete(id);
      }
    });
  }

  prevPage(): void {
    if (this.page > 1) { this.page--; this.loadProducts(); }
  }

  nextPage(): void {
    if (this.page < this.totalPages) { this.page++; this.loadProducts(); }
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

  resolveImageUrl(imageUrl?: string | null): string {
    if (!imageUrl) return '';
    if (imageUrl.startsWith('http://') || imageUrl.startsWith('https://') || imageUrl.startsWith('data:')) {
      return imageUrl;
    }
    return `${this.apiUrl}${imageUrl.startsWith('/') ? imageUrl : `/${imageUrl}`}`;
  }
}
