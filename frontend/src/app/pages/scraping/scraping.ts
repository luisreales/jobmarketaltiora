import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ScrapingService, DataQualityReport, PurgeResult } from '../../services/scraping.service';
import { AppSumoService } from '../../services/appsumo.service';
import { PromptAiService } from '../../services/prompt-ai.service';
import { AiAuditService } from '../../services/ai-audit.service';
import { AuthService, ProviderAuthStatus } from '../../services/auth.service';
import { AiPromptTemplate, AiWorkerRunNowResult, AiWorkerStatus } from '../../models/ai-audit.models';
import {
  AppSumoStats,
  AppSumoScrapeRunDto,
  StartScrapeRequest,
} from '../../models/appsumo.models';

@Component({
  selector: 'app-scraping',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './scraping.html',
  styleUrls: ['./scraping.css']
})
export class ScrapingComponent implements OnInit, OnDestroy {
  // ── LinkedIn ──────────────────────────────────────────────────────────────
  linkedinQuery     = '.NET';
  linkedinLocation  = 'Remote';
  linkedinLimit     = 500;
  linkedinStartPage = 1;
  linkedinEndPage   = 20;
  linkedinProviders = ['linkedin'];
  linkedinPromptKey = 'market-job-analysis';
  linkedinAutoAnalyze = false;
  isLinkedInScraping = false;
  linkedinResult: { savedCount: number; totalFound: number; updatedCount: number; executedAtUtc: string; activeKey?: string } | null = null;
  linkedinError: string | null = null;

  // ── Upwork ────────────────────────────────────────────────────────────────
  upworkQuery    = '.NET';
  upworkLocation = 'Remote';
  upworkLimit    = 20;
  upworkPages    = 2;
  upworkProviders = ['upwork'];
  upworkPromptKey = 'market-job-analysis';
  upworkAutoAnalyze = false;
  isUpworkScraping = false;
  upworkResult: { savedCount: number; totalFound: number; updatedCount: number; executedAtUtc: string; activeKey?: string } | null = null;
  upworkError: string | null = null;

  // ── Multi-provider ────────────────────────────────────────────────────────
  multiQuery    = '.NET';
  multiLocation = 'Remote';
  multiLimit    = 20;
  selectedProviders: string[] = [];
  multiPromptKey = 'market-job-analysis';
  multiAutoAnalyze = false;
  isMultiScraping = false;
  multiResult: { savedCount: number; totalFound: number; updatedCount: number; executedAtUtc: string; activeKey?: string } | null = null;
  multiError: string | null = null;
  availableProviders = ['linkedin', 'indeed', 'upwork'];

  // ── AppSumo ───────────────────────────────────────────────────────────────
  appsumoStats: AppSumoStats | null = null;
  appsumoRuns: AppSumoScrapeRunDto[] = [];
  appsumoStatsLoading = false;
  appsumoScraping     = false;
  appsumoError: string | null = null;
  appsumoSuccess: string | null = null;

  appsumoForm: StartScrapeRequest = {
    startCategorySlug: null,
    dryRun: false,
    maxProducts: 0,
  };

  appsumoReviewSearch = '';
  appsumoReviewRating: number | null = null;

  // ── LinkedIn Auth ─────────────────────────────────────────────────────────
  linkedinAuthStatus: ProviderAuthStatus | null = null;
  linkedinAuthLoading = false;
  linkedinLoginLoading = false;
  linkedinLoginError: string | null = null;

  // ── Upwork Auth ───────────────────────────────────────────────────────────
  upworkAuthStatus: ProviderAuthStatus | null = null;
  upworkAuthLoading = false;
  upworkLoginError: string | null = null;

  // ── Data Quality ──────────────────────────────────────────────────────────
  dataQuality: DataQualityReport | null = null;
  dataQualityLoading = false;
  purgeLoading = false;
  purgeDryRun = true;
  purgeStaleDays = 30;
  purgeResult: PurgeResult | null = null;
  purgeError: string | null = null;

  // ── AI Analysis (shared) ──────────────────────────────────────────────────
  analysisPrompts: AiPromptTemplate[] = [];
  analysisPromptsLoading = false;
  workerStatus: AiWorkerStatus | null = null;
  isRunningAnalysis = false;
  analysisResult: AiWorkerRunNowResult | null = null;
  analysisError: string | null = null;

  constructor(
    private scrapingService: ScrapingService,
    private appsumoService: AppSumoService,
    private promptAiService: PromptAiService,
    private aiAuditService: AiAuditService,
    private authService: AuthService,
  ) {}

  ngOnInit(): void {
    this.selectedProviders = [...this.linkedinProviders];
    this.loadLinkedInAuthStatus();
    this.loadUpworkAuthStatus();
    this.loadDataQuality();
    this.loadAppSumoStats();
    this.loadAppSumoRuns();
    this.loadAnalysisPrompts();
    this.loadWorkerStatus();
  }

  ngOnDestroy(): void {}

  // ── LinkedIn Auth methods ─────────────────────────────────────────────────

  loadLinkedInAuthStatus(): void {
    this.linkedinAuthLoading = true;
    this.authService.getStatus('linkedin').subscribe({
      next: (s) => { this.linkedinAuthStatus = s; this.linkedinAuthLoading = false; },
      error: ()  => { this.linkedinAuthLoading = false; },
    });
  }

  loginLinkedIn(): void {
    this.linkedinLoginLoading = true;
    this.linkedinLoginError = null;
    this.authService.login('linkedin').subscribe({
      next: (s) => {
        this.linkedinLoginLoading = false;
        this.linkedinAuthStatus = s;
        this.loadLinkedInAuthStatus();
      },
      error: (err) => {
        this.linkedinLoginLoading = false;
        const detail = err?.error?.detail ?? err?.error?.message ?? err?.message ?? '';
        if (err?.status === 409) {
          this.linkedinLoginError = 'LinkedIn requires manual verification (captcha/checkpoint). '
            + 'Set Jobs__Playwright__LoginHeadless=false and restart the backend, '
            + 'or run the backend locally so a browser window opens.';
        } else {
          this.linkedinLoginError = detail || 'LinkedIn login failed.';
        }
      },
    });
  }

  logoutLinkedIn(): void {
    this.authService.logout('linkedin').subscribe({
      next: (s) => { this.linkedinAuthStatus = s; },
      error: ()  => { this.loadLinkedInAuthStatus(); },
    });
  }

  // ── Upwork Auth methods ───────────────────────────────────────────────────

  loadUpworkAuthStatus(): void {
    this.upworkAuthLoading = true;
    this.authService.getStatus('upwork').subscribe({
      next: (s) => { this.upworkAuthStatus = s; this.upworkAuthLoading = false; },
      error: ()  => { this.upworkAuthLoading = false; },
    });
  }

  logoutUpwork(): void {
    this.authService.logout('upwork').subscribe({
      next: (s) => { this.upworkAuthStatus = s; },
      error: ()  => { this.loadUpworkAuthStatus(); },
    });
  }

  // ── Data Quality methods ──────────────────────────────────────────────────

  loadDataQuality(): void {
    this.dataQualityLoading = true;
    this.scrapingService.getDataQuality().subscribe({
      next: (r) => { this.dataQuality = r; this.dataQualityLoading = false; },
      error: ()  => { this.dataQualityLoading = false; },
    });
  }

  runPurge(): void {
    this.purgeLoading = true;
    this.purgeResult = null;
    this.purgeError  = null;
    this.scrapingService.purgeJobs(this.purgeDryRun, this.purgeStaleDays).subscribe({
      next: (r) => {
        this.purgeLoading = false;
        this.purgeResult  = r;
        if (!r.dryRun) this.loadDataQuality();
      },
      error: (err) => {
        this.purgeLoading = false;
        this.purgeError   = err?.error?.detail ?? err?.message ?? 'Purge failed.';
      },
    });
  }

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
        analysisPromptKey: this.linkedinPromptKey,
      }).toPromise();
      this.linkedinResult = result ? {
        savedCount: result.savedCount,
        totalFound: result.totalFound,
        updatedCount: result.totalFound - result.savedCount,
        executedAtUtc: result.executedAtUtc,
        activeKey: result.activeAnalysisPromptKey,
      } : null;
      if (this.linkedinAutoAnalyze) this.runAnalysis();
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
        limit: this.upworkPages * 10,
        startPage: 1,
        endPage: this.upworkPages,
        providers: this.upworkProviders, showBrowser: true,
        analysisPromptKey: this.upworkPromptKey,
      }).toPromise();
      this.upworkResult = result ? {
        savedCount: result.savedCount,
        totalFound: result.totalFound,
        updatedCount: (result.touchedCount ?? result.totalFound) - result.savedCount,
        executedAtUtc: result.executedAtUtc,
        activeKey: result.activeAnalysisPromptKey,
      } : null;
      if (this.upworkAutoAnalyze) this.runAnalysis();
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
        analysisPromptKey: this.multiPromptKey,
      }).toPromise();
      this.multiResult = result ? {
        savedCount: result.savedCount,
        totalFound: result.totalFound,
        updatedCount: result.totalFound - result.savedCount,
        executedAtUtc: result.executedAtUtc,
        activeKey: result.activeAnalysisPromptKey,
      } : null;
      if (this.multiAutoAnalyze) this.runAnalysis();
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

  // ── AI Analysis methods ───────────────────────────────────────────────────

  loadAnalysisPrompts(): void {
    this.analysisPromptsLoading = true;
    this.promptAiService.getAll().subscribe({
      next: (prompts) => {
        this.analysisPrompts = prompts.filter(p => p.isActive);
        this.analysisPromptsLoading = false;
      },
      error: () => { this.analysisPromptsLoading = false; },
    });
  }

  loadWorkerStatus(): void {
    this.aiAuditService.getWorkerStatus().subscribe({
      next: (s) => { this.workerStatus = s; },
      error: () => {},
    });
  }

  runAnalysis(): void {
    this.isRunningAnalysis = true;
    this.analysisError = null;
    this.analysisResult = null;
    this.aiAuditService.runWorkerNow().subscribe({
      next: (result) => {
        this.isRunningAnalysis = false;
        this.analysisResult = result;
        this.loadWorkerStatus();
      },
      error: (err) => {
        this.isRunningAnalysis = false;
        this.analysisError = err?.error?.message ?? err?.message ?? 'Analysis worker failed.';
      },
    });
  }
}
