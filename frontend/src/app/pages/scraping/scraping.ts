import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ScrapingService } from '../../services/scraping.service';
import { AppSumoService } from '../../services/appsumo.service';
import {
  AppSumoStats,
  AppSumoScrapeRunDto,
  StartScrapeRequest,
} from '../../models/appsumo.models';

interface ScrapeRequest {
  query: string;
  location: string;
  limit: number;
  providers: string[];
  totalPaging?: number;
  startPage?: number;
  endPage?: number;
  showBrowser?: boolean;
}

interface ScrapeResult {
  savedCount: number;
  totalFound: number;
  timestamp: string;
}

@Component({
  selector: 'app-scraping',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './scraping.html',
  styleUrls: ['./scraping.css']
})
export class ScrapingComponent implements OnInit, OnDestroy {
  // ── LinkedIn ──────────────────────────────────────────────────────────────
  linkedinQuery    = '.NET';
  linkedinLocation = 'Remote';
  linkedinLimit    = 500;
  linkedinStartPage = 1;
  linkedinEndPage   = 20;
  linkedinProviders = ['linkedin'];
  isLinkedInScraping = false;
  linkedinResult: ScrapeResult | null = null;
  linkedinError: string | null = null;

  // ── Upwork ────────────────────────────────────────────────────────────────
  upworkQuery    = '.NET';
  upworkLocation = 'Remote';
  upworkLimit    = 20;
  upworkProviders = ['upwork'];
  isUpworkScraping = false;
  upworkResult: ScrapeResult | null = null;
  upworkError: string | null = null;

  // ── Multi-provider ────────────────────────────────────────────────────────
  multiQuery    = '.NET';
  multiLocation = 'Remote';
  multiLimit    = 20;
  selectedProviders: string[] = [];
  isMultiScraping = false;
  multiResult: ScrapeResult | null = null;
  multiError: string | null = null;
  availableProviders = ['linkedin', 'indeed', 'upwork'];

  // ── AppSumo ───────────────────────────────────────────────────────────────
  appsumoStats: AppSumoStats | null = null;
  appsumoRuns: AppSumoScrapeRunDto[] = [];
  appsumoStatsLoading = false;
  appsumoScraping     = false;
  appsumoError: string | null = null;
  appsumoSuccess: string | null = null;

  // AppSumo form
  appsumoForm: StartScrapeRequest = {
    startCategorySlug: null,
    dryRun: false,
    maxProducts: 0,
  };

  // AppSumo review search (preview panel)
  appsumoReviewSearch = '';
  appsumoReviewRating: number | null = null;

  constructor(
    private scrapingService: ScrapingService,
    private appsumoService: AppSumoService,
  ) {}

  ngOnInit(): void {
    this.selectedProviders = [...this.linkedinProviders];
    this.loadAppSumoStats();
    this.loadAppSumoRuns();
  }

  ngOnDestroy(): void {}

  // ── AppSumo methods ───────────────────────────────────────────────────────

  loadAppSumoStats(): void {
    this.appsumoStatsLoading = true;
    this.appsumoService.getStats().subscribe({
      next: (stats) => { this.appsumoStats = stats; this.appsumoStatsLoading = false; },
      error: ()      => { this.appsumoStats = null;  this.appsumoStatsLoading = false; },
    });
  }

  loadAppSumoRuns(): void {
    this.appsumoService.getRuns().subscribe({
      next: (runs) => { this.appsumoRuns = runs; },
      error: ()    => { this.appsumoRuns = []; },
    });
  }

  startAppSumoScrape(): void {
    this.appsumoScraping = true;
    this.appsumoError    = null;
    this.appsumoSuccess  = null;

    const req: StartScrapeRequest = {
      startCategorySlug: this.appsumoForm.startCategorySlug || null,
      dryRun:            this.appsumoForm.dryRun ?? false,
      maxProducts:       this.appsumoForm.maxProducts ?? 0,
    };

    this.appsumoService.startScrape(req).subscribe({
      next: (res) => {
        this.appsumoScraping = false;
        this.appsumoSuccess  = res.message;
        this.loadAppSumoRuns();
        // Refresh stats after short delay so the run row appears
        setTimeout(() => this.loadAppSumoStats(), 2000);
      },
      error: (err) => {
        this.appsumoScraping = false;
        this.appsumoError    = err?.error?.message ?? err?.message ?? 'Failed to start scrape.';
      },
    });
  }

  getRatingLabel(rating: number): string {
    const labels: Record<number, string> = { 1: '1 🌮', 2: '2 🌮🌮', 3: '3 🌮🌮🌮' };
    return labels[rating] ?? `${rating}`;
  }

  runStatusClass(status: string): string {
    const map: Record<string, string> = {
      Running:   'bg-blue-100 text-blue-700',
      Completed: 'bg-emerald-100 text-emerald-700',
      Failed:    'bg-red-100 text-red-700',
      Cancelled: 'bg-slate-100 text-slate-600',
    };
    return map[status] ?? 'bg-slate-100 text-slate-600';
  }

  // ── Existing provider methods ─────────────────────────────────────────────

  onProviderToggle(provider: string): void {
    if (this.selectedProviders.includes(provider)) {
      this.selectedProviders = this.selectedProviders.filter(p => p !== provider);
    } else {
      this.selectedProviders.push(provider);
    }
  }

  isProviderSelected(provider: string): boolean {
    return this.selectedProviders.includes(provider);
  }

  async scrapeLinkedIn(): Promise<void> {
    if (!this.linkedinQuery.trim()) { this.linkedinError = 'Please enter a search query'; return; }
    this.isLinkedInScraping = true;
    this.linkedinError = null;
    this.linkedinResult = null;
    try {
      const result = await this.scrapingService.scrapeLinkedIn({
        query: this.linkedinQuery, location: this.linkedinLocation,
        limit: this.linkedinLimit, startPage: this.linkedinStartPage,
        endPage: this.linkedinEndPage, providers: this.linkedinProviders,
      }).toPromise();
      this.linkedinResult = result || null;
    } catch (error: any) {
      this.linkedinError = error?.error?.detail || error?.message || 'Error scraping LinkedIn';
    } finally { this.isLinkedInScraping = false; }
  }

  async scrapeUpwork(): Promise<void> {
    if (!this.upworkQuery.trim()) { this.upworkError = 'Please enter a search query'; return; }
    this.isUpworkScraping = true;
    this.upworkError = null;
    this.upworkResult = null;
    try {
      const result = await this.scrapingService.scrapeUpwork({
        query: this.upworkQuery, location: this.upworkLocation,
        limit: this.upworkLimit, providers: this.upworkProviders, showBrowser: true,
      }).toPromise();
      this.upworkResult = result || null;
    } catch (error: any) {
      const msg = error?.error?.detail || error?.message || 'Error scraping Upwork';
      this.upworkError = msg.includes('scraper API') || msg.includes('Timeout')
        ? 'Upwork scraper service is not running. Start it with: docker-compose up scraper-api'
        : msg;
    } finally { this.isUpworkScraping = false; }
  }

  async scrapeMultiProvider(): Promise<void> {
    if (!this.multiQuery.trim()) { this.multiError = 'Please enter a search query'; return; }
    if (this.selectedProviders.length === 0) { this.multiError = 'Please select at least one provider'; return; }
    this.isMultiScraping = true;
    this.multiError = null;
    this.multiResult = null;
    try {
      const result = await this.scrapingService.scrapeMultiProvider({
        query: this.multiQuery, location: this.multiLocation,
        limit: this.multiLimit, providers: this.selectedProviders,
      }).toPromise();
      this.multiResult = result || null;
    } catch (error: any) {
      const msg = error?.error?.detail || error?.message || 'Error scraping jobs';
      this.multiError = msg.includes('scraper API') || msg.includes('Timeout')
        ? 'Upwork scraper service is not running. Start it with: docker-compose up scraper-api'
        : msg;
    } finally { this.isMultiScraping = false; }
  }

  formatDate(dateString: string): string {
    try { return new Date(dateString).toLocaleString(); } catch { return dateString; }
  }

  formatDuration(start: string, end: string | null): string {
    if (!end) return '—';
    const ms = new Date(end).getTime() - new Date(start).getTime();
    const s  = Math.round(ms / 1000);
    if (s < 60) return `${s}s`;
    return `${Math.floor(s / 60)}m ${s % 60}s`;
  }
}
