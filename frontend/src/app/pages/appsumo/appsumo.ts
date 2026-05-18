import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { AppSumoService } from '../../services/appsumo.service';
import {
  AppSumoStats,
  AppSumoScrapeRunDto,
  AppSumoReviewDto,
  AppSumoProductDto,
  StartScrapeRequest,
} from '../../models/appsumo.models';

@Component({
  selector: 'app-appsumo',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './appsumo.html',
})
export class AppSumoPage implements OnInit {
  private readonly svc = inject(AppSumoService);

  // Stats
  stats: AppSumoStats | null = null;
  statsLoading = false;

  // Runs
  runs: AppSumoScrapeRunDto[] = [];
  runsLoading = false;

  // Scrape form
  form: StartScrapeRequest = { startCategorySlug: null, dryRun: false, maxProducts: 0 };
  scraping    = false;
  scrapeError: string | null = null;
  scrapeOk:   string | null = null;

  // Review preview
  reviews: AppSumoReviewDto[] = [];
  reviewsLoading  = false;
  reviewSearch    = '';
  reviewRating: number | null = null;
  reviewPage      = 1;
  reviewTotalPages = 1;
  reviewTotal     = 0;

  // Product preview
  products: AppSumoProductDto[] = [];
  productsLoading = false;
  productSearch   = '';
  productPage     = 1;
  productTotalPages = 1;
  productTotal    = 0;

  // Tabs
  activeTab: 'runs' | 'reviews' | 'products' = 'runs';

  ngOnInit(): void {
    this.loadStats();
    this.loadRuns();
  }

  // ── Stats & Runs ──────────────────────────────────────────────────────────

  loadStats(): void {
    this.statsLoading = true;
    this.svc.getStats().subscribe({
      next: s  => { this.stats = s; this.statsLoading = false; },
      error: () => { this.stats = null; this.statsLoading = false; },
    });
  }

  loadRuns(): void {
    this.runsLoading = true;
    this.svc.getRuns().subscribe({
      next: r  => { this.runs = r; this.runsLoading = false; },
      error: () => { this.runs = []; this.runsLoading = false; },
    });
  }

  // ── Scrape control ────────────────────────────────────────────────────────

  startScrape(): void {
    this.scraping    = true;
    this.scrapeError = null;
    this.scrapeOk    = null;

    this.svc.startScrape({
      startCategorySlug: this.form.startCategorySlug || null,
      dryRun:            this.form.dryRun ?? false,
      maxProducts:       this.form.maxProducts ?? 0,
    }).subscribe({
      next: res => {
        this.scraping = false;
        this.scrapeOk = res.message;
        this.loadRuns();
        setTimeout(() => this.loadStats(), 2000);
      },
      error: err => {
        this.scraping    = false;
        this.scrapeError = err?.error?.message ?? err?.message ?? 'Failed to start scrape.';
      },
    });
  }

  // ── Reviews tab ───────────────────────────────────────────────────────────

  loadReviews(page = 1): void {
    this.reviewsLoading = true;
    this.reviewPage     = page;
    this.svc.getReviews({
      search:     this.reviewSearch.trim() || undefined,
      tacoRating: this.reviewRating ?? undefined,
      page,
      pageSize:   20,
    }).subscribe({
      next: r => {
        this.reviews          = r.items;
        this.reviewTotal      = r.totalCount;
        this.reviewTotalPages = r.totalPages;
        this.reviewsLoading   = false;
      },
      error: () => { this.reviews = []; this.reviewsLoading = false; },
    });
  }

  onReviewSearch(): void { this.loadReviews(1); }

  // ── Products tab ──────────────────────────────────────────────────────────

  loadProducts(page = 1): void {
    this.productsLoading = true;
    this.productPage     = page;
    this.svc.getProducts({
      search: this.productSearch.trim() || undefined,
      page,
      pageSize: 20,
    }).subscribe({
      next: r => {
        this.products          = r.items;
        this.productTotal      = r.totalCount;
        this.productTotalPages = r.totalPages;
        this.productsLoading   = false;
      },
      error: () => { this.products = []; this.productsLoading = false; },
    });
  }

  onProductSearch(): void { this.loadProducts(1); }

  switchTab(tab: 'runs' | 'reviews' | 'products'): void {
    this.activeTab = tab;
    if (tab === 'reviews'  && this.reviews.length  === 0) this.loadReviews(1);
    if (tab === 'products' && this.products.length === 0) this.loadProducts(1);
  }

  // ── Helpers ───────────────────────────────────────────────────────────────

  getRatingLabel(r: number): string {
    return ['', '1 🌮', '2 🌮🌮', '3 🌮🌮🌮'][r] ?? `${r}`;
  }

  runStatusClass(s: string): string {
    return ({ Running: 'bg-blue-100 text-blue-700', Completed: 'bg-emerald-100 text-emerald-700',
              Failed:  'bg-red-100 text-red-700',   Cancelled: 'bg-slate-100 text-slate-600' } as Record<string,string>)[s]
      ?? 'bg-slate-100 text-slate-600';
  }

  scrapeStatusClass(s: string): string {
    return ({ Done:    'bg-emerald-100 text-emerald-700', Failed: 'bg-red-100 text-red-600',
              Pending: 'bg-amber-100 text-amber-700',     Skipped: 'bg-slate-100 text-slate-500' } as Record<string,string>)[s]
      ?? 'bg-slate-100 text-slate-500';
  }

  formatDate(d: string | null): string {
    if (!d) return '—';
    try { return new Date(d).toLocaleString(); } catch { return d; }
  }

  formatDuration(start: string, end: string | null): string {
    if (!end) return '—';
    const s = Math.round((new Date(end).getTime() - new Date(start).getTime()) / 1000);
    return s < 60 ? `${s}s` : `${Math.floor(s / 60)}m ${s % 60}s`;
  }
}
