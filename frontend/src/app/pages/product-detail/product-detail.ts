import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ProductService } from '../../services/product.service';
import { ProductSuggestion } from '../../models/market.models';

interface TacticalPlan {
  implementacion: string;
  requerimientos: string;
  tiempo_y_tecnologias: string;
  empresas_objetivo: string;
}

@Component({
  selector: 'app-product-detail',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './product-detail.html',
})
export class ProductDetail implements OnInit {
  product: ProductSuggestion | null = null;
  loading = false;
  notFound = false;
  uiMessage: string | null = null;
  uiMessageType: 'success' | 'error' | null = null;

  synthesisLoading = false;
  synthesisError: string | null = null;

  private readonly productService = inject(ProductService);
  private readonly route = inject(ActivatedRoute);

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    if (!id) { this.notFound = true; return; }
    this.loadProduct(id);
  }

  get tacticalPlan(): TacticalPlan | null {
    if (!this.product?.synthesisDetailJson) return null;
    try {
      return JSON.parse(this.product.synthesisDetailJson) as TacticalPlan;
    } catch {
      return null;
    }
  }

  generatePlan(): void {
    if (!this.product || this.synthesisLoading) return;
    this.synthesisLoading = true;
    this.synthesisError = null;
    this.uiMessage = null;
    this.uiMessageType = null;
    this.productService.synthesize(this.product.id).subscribe({
      next: (updated) => {
        this.loadProduct(updated.id, () => {
          this.synthesisLoading = false;
          this.uiMessage = 'Plan de ataque generado y sincronizado correctamente.';
          this.uiMessageType = 'success';
        });
      },
      error: () => {
        this.synthesisError = 'Error al generar el plan. Intenta de nuevo.';
        this.synthesisLoading = false;
        this.uiMessage = this.synthesisError;
        this.uiMessageType = 'error';
      }
    });
  }

  clearUiMessage(): void {
    this.uiMessage = null;
    this.uiMessageType = null;
  }

  techTokens(techFocus: string): string[] {
    return techFocus.split('+').map(t => t.trim()).filter(Boolean);
  }

  dealRange(): string {
    if (!this.product) return '';
    const { minDealSizeUsd, maxDealSizeUsd } = this.product;
    const fmt = (v: number) => v >= 1000 ? `$${(v / 1000).toFixed(0)}K` : `$${v}`;
    return `${fmt(minDealSizeUsd)} – ${fmt(maxDealSizeUsd)}`;
  }

  opportunityTypeClass(type: string): string {
    switch (type) {
      case 'MVPProduct':  return 'bg-purple-100 text-purple-700';
      case 'QuickWin':    return 'bg-green-100 text-green-700';
      case 'Consulting':  return 'bg-amber-100 text-amber-700';
      default:            return 'bg-slate-100 text-slate-500';
    }
  }

  private loadProduct(id: number, onFinish?: () => void): void {
    this.loading = true;
    this.productService.getProduct(id).subscribe({
      next: (p) => {
        this.product = p;
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
