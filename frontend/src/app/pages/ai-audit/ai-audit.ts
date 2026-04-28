import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { AiPromptLog, AiUsageSummary } from '../../models/ai-audit.models';
import { AiAuditService } from '../../services/ai-audit.service';

@Component({
  selector: 'app-ai-audit',
  imports: [CommonModule, FormsModule],
  templateUrl: './ai-audit.html',
  styleUrl: './ai-audit.scss'
})
export class AiAudit implements OnInit {
  loading = false;
  logs: AiPromptLog[] = [];
  summary: AiUsageSummary | null = null;
  expandedLogId: number | null = null;

  page = 1;
  pageSize = 20;
  totalPages = 1;
  totalCount = 0;
  sortBy = 'createdAt';
  sortDirection: 'asc' | 'desc' = 'desc';

  provider = '';
  modelId = '';
  status = '';
  windowDays = 7;

  clearCacheLoading = false;
  clearCacheResult = '';
  deleteAllLoading = false;
  deleteAllResult = '';

  private readonly aiAuditService = inject(AiAuditService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  ngOnInit(): void {
    this.restoreStateFromQueryParams();
    this.loadSummary();
    this.loadLogs();
  }

  applyFilters(): void {
    this.page = 1;
    this.syncStateToQueryParams();
    this.loadSummary();
    this.loadLogs();
  }

  changeSorting(field: string): void {
    if (this.loading) return;
    if (this.sortBy === field) {
      this.sortDirection = this.sortDirection === 'asc' ? 'desc' : 'asc';
    } else {
      this.sortBy = field;
      this.sortDirection = field === 'createdAt' ? 'desc' : 'asc';
    }
    this.page = 1;
    this.syncStateToQueryParams();
    this.loadLogs();
  }

  getSortMarker(field: string): string {
    if (this.sortBy !== field) return '';
    return this.sortDirection === 'asc' ? ' ↑' : ' ↓';
  }

  previousPage(): void {
    if (this.page <= 1 || this.loading) return;
    this.page -= 1;
    this.syncStateToQueryParams();
    this.loadLogs();
  }

  nextPage(): void {
    if (this.page >= this.totalPages || this.loading) return;
    this.page += 1;
    this.syncStateToQueryParams();
    this.loadLogs();
  }

  toggleDetails(logId: number): void {
    this.expandedLogId = this.expandedLogId === logId ? null : logId;
  }

  statusClass(status: string): string {
    if (status === 'success') return 'bg-green-100 text-green-700';
    if (status === 'failed' || status === 'error') return 'bg-red-100 text-red-700';
    if (status === 'cached') return 'bg-blue-100 text-blue-700';
    return 'bg-slate-100 text-slate-600';
  }

  clearCache(): void {
    if (this.clearCacheLoading) return;
    this.clearCacheLoading = true;
    this.clearCacheResult = '';
    this.aiAuditService.clearCachedLogs().subscribe({
      next: (r) => {
        this.clearCacheResult = `Deleted ${r.deletedCount} cached records.`;
        this.clearCacheLoading = false;
        this.page = 1;
        this.loadSummary();
        this.loadLogs();
      },
      error: () => {
        this.clearCacheResult = 'Failed to clear cache.';
        this.clearCacheLoading = false;
      }
    });
  }

  deleteAllLogs(): void {
    if (this.deleteAllLoading) return;
    this.deleteAllLoading = true;
    this.deleteAllResult = '';
    this.aiAuditService.deleteAllLogs().subscribe({
      next: (r) => {
        this.deleteAllResult = `Deleted ${r.deletedCount} records.`;
        this.deleteAllLoading = false;
        this.page = 1;
        this.loadSummary();
        this.loadLogs();
      },
      error: () => {
        this.deleteAllResult = 'Failed to delete logs.';
        this.deleteAllLoading = false;
      }
    });
  }

  private restoreStateFromQueryParams(): void {
    const qp = this.route.snapshot.queryParamMap;
    const pageParam = Number(qp.get('page'));
    if (Number.isFinite(pageParam) && pageParam > 0) this.page = pageParam;
    const p = qp.get('provider'); if (p) this.provider = p;
    const m = qp.get('modelId'); if (m) this.modelId = m;
    const s = qp.get('status'); if (s) this.status = s;
    const sb = qp.get('sortBy'); if (sb) this.sortBy = sb;
    const sd = qp.get('sortDirection');
    if (sd === 'asc' || sd === 'desc') this.sortDirection = sd;
    const wd = Number(qp.get('windowDays'));
    if (Number.isFinite(wd) && wd > 0) this.windowDays = wd;
  }

  private syncStateToQueryParams(): void {
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: {
        page: this.page > 1 ? String(this.page) : null,
        sortBy: this.sortBy !== 'createdAt' ? this.sortBy : null,
        sortDirection: this.sortDirection !== 'desc' ? this.sortDirection : null,
        provider: this.provider.trim() || null,
        modelId: this.modelId.trim() || null,
        status: this.status.trim() || null,
        windowDays: this.windowDays !== 7 ? String(this.windowDays) : null
      },
      replaceUrl: true
    });
  }

  private loadSummary(): void {
    this.aiAuditService.getSummary(this.windowDays).subscribe({
      next: (r) => { this.summary = r; },
      error: () => { this.summary = null; }
    });
  }

  private loadLogs(): void {
    this.loading = true;
    this.aiAuditService.getLogs({
      page: this.page,
      pageSize: this.pageSize,
      sortBy: this.sortBy,
      sortDirection: this.sortDirection,
      provider: this.provider || undefined,
      modelId: this.modelId || undefined,
      status: this.status || undefined
    }).subscribe({
      next: (r) => {
        this.logs = r.items;
        this.totalPages = r.totalPages;
        this.totalCount = r.totalCount;
        this.sortBy = r.sortBy || this.sortBy;
        this.sortDirection = r.sortDirection || this.sortDirection;
        if (this.expandedLogId && !this.logs.some(l => l.id === this.expandedLogId)) {
          this.expandedLogId = null;
        }
        this.loading = false;
      },
      error: () => { this.logs = []; this.loading = false; }
    });
  }
}
