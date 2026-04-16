import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AiPromptLog, AiUsageSummary, AiWorkerStatus, LlmHealth } from '../../models/ai-audit.models';
import { AiAuditService } from '../../services/ai-audit.service';

@Component({
  selector: 'app-ai-audit',
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './ai-audit.html',
  styleUrl: './ai-audit.scss'
})
export class AiAudit implements OnInit {
  readonly promptTemplateKey = 'market-job-analysis';

  loading = false;
  logs: AiPromptLog[] = [];
  summary: AiUsageSummary | null = null;
  expandedLogId: number | null = null;

  promptLoading = false;
  promptSaving = false;
  promptTemplate = '';
  promptVersion = 'v1';
  promptSource = '';
  promptUpdatedAt: string | null = null;
  promptUpdatedBy: string | null = null;
  promptStatusMessage = '';

  workerStatus: AiWorkerStatus | null = null;
  workerStatusLoading = false;
  workerRunLoading = false;
  workerMessage = '';
  lastManualRunAt: string | null = null;
  lastManualProcessedJobs: number | null = null;
  llmHealth: LlmHealth | null = null;
  llmHealthLoading = false;

  page = 1;
  pageSize = 20;
  totalPages = 1;
  sortBy = 'createdAt';
  sortDirection: 'asc' | 'desc' = 'desc';

  provider = '';
  modelId = '';
  status = '';
  clusterId: number | null = null;
  windowDays = 7;

  private readonly aiAuditService = inject(AiAuditService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  ngOnInit(): void {
    this.restoreStateFromQueryParams();
    this.loadAll();
  }

  savePromptTemplate(): void {
    if (!this.promptTemplate.trim() || this.promptSaving) {
      return;
    }

    this.promptSaving = true;
    this.promptStatusMessage = '';

    this.aiAuditService.updatePromptTemplate(this.promptTemplateKey, {
      template: this.promptTemplate.trim(),
      version: this.promptVersion.trim() || undefined,
      isActive: true,
      updatedBy: 'ai-audit-ui'
    }).subscribe({
      next: (response) => {
        this.promptTemplate = response.template;
        this.promptVersion = response.version;
        this.promptSource = response.source;
        this.promptUpdatedAt = response.updatedAt;
        this.promptUpdatedBy = response.updatedBy;
        this.promptStatusMessage = 'Prompt guardado correctamente.';
        this.promptSaving = false;
      },
      error: () => {
        this.promptStatusMessage = 'No se pudo guardar el prompt.';
        this.promptSaving = false;
      }
    });
  }

  runWorkerNow(): void {
    if (this.workerRunLoading) {
      return;
    }

    const runStart = Date.now();
    this.workerRunLoading = true;
    this.workerMessage = 'Ejecutando worker manual...';

    this.aiAuditService.runWorkerNow().subscribe({
      next: (result) => {
        this.lastManualRunAt = result.completedAt;
        this.lastManualProcessedJobs = result.processedJobs;
        this.workerMessage = `Ejecucion manual completada. Jobs procesados: ${result.processedJobs}.`;
        this.stopWorkerRunLoadingWithDelay(runStart);
        this.loadWorkerStatus();
        this.loadSummary();
        this.loadLogs();
      },
      error: () => {
        this.workerMessage = 'No se pudo ejecutar el worker manualmente.';
        this.stopWorkerRunLoadingWithDelay(runStart);
      }
    });
  }

  get workerIntervalLabel(): string {
    if (!this.workerStatus) {
      return 'No disponible';
    }

    const seconds = this.workerStatus.intervalSeconds;
    if (seconds % 60 === 0) {
      const minutes = seconds / 60;
      return `Cada ${minutes} minuto${minutes === 1 ? '' : 's'} (${seconds}s)`;
    }

    return `Cada ${seconds} segundos`;
  }

  applyFilters(): void {
    this.page = 1;
    this.syncStateToQueryParams();
    this.loadAll();
  }

  changeSorting(field: string): void {
    if (this.loading) {
      return;
    }

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
    if (this.sortBy !== field) {
      return '';
    }

    return this.sortDirection === 'asc' ? ' ↑' : ' ↓';
  }

  get llmHealthIsGreen(): boolean {
    return this.llmHealth?.isHealthy === true;
  }

  previousPage(): void {
    if (this.page <= 1 || this.loading) {
      return;
    }

    this.page -= 1;
    this.syncStateToQueryParams();
    this.loadLogs();
  }

  nextPage(): void {
    if (this.page >= this.totalPages || this.loading) {
      return;
    }

    this.page += 1;
    this.syncStateToQueryParams();
    this.loadLogs();
  }

  toggleDetails(logId: number): void {
    this.expandedLogId = this.expandedLogId === logId ? null : logId;
  }

  hasResponse(log: AiPromptLog): boolean {
    return Boolean(log.responseText?.trim());
  }

  hasPrompt(log: AiPromptLog): boolean {
    return Boolean(log.promptText?.trim());
  }

  hasError(log: AiPromptLog): boolean {
    return Boolean(log.errorMessage?.trim());
  }

  hasCluster(log: AiPromptLog): boolean {
    return log.clusterId != null && log.clusterId > 0;
  }

  clusterDetailQuery(clusterId: number | null): Record<string, string> | null {
    if (!clusterId || clusterId <= 0) {
      return null;
    }

    return { clusterId: String(clusterId) };
  }

  private restoreStateFromQueryParams(): void {
    const queryParams = this.route.snapshot.queryParamMap;

    const pageParam = Number(queryParams.get('page'));
    if (Number.isFinite(pageParam) && pageParam > 0) {
      this.page = pageParam;
    }

    const providerParam = queryParams.get('provider');
    if (providerParam) {
      this.provider = providerParam;
    }

    const sortByParam = queryParams.get('sortBy');
    if (sortByParam) {
      this.sortBy = sortByParam;
    }

    const sortDirectionParam = queryParams.get('sortDirection');
    if (sortDirectionParam === 'asc' || sortDirectionParam === 'desc') {
      this.sortDirection = sortDirectionParam;
    }

    const statusParam = queryParams.get('status');
    if (statusParam) {
      this.status = statusParam;
    }

    const modelParam = queryParams.get('modelId');
    if (modelParam) {
      this.modelId = modelParam;
    }

    const clusterIdParam = Number(queryParams.get('clusterId'));
    if (Number.isFinite(clusterIdParam) && clusterIdParam > 0) {
      this.clusterId = clusterIdParam;
    }

    const windowDaysParam = Number(queryParams.get('windowDays'));
    if (Number.isFinite(windowDaysParam) && windowDaysParam > 0) {
      this.windowDays = windowDaysParam;
    }
  }

  private syncStateToQueryParams(): void {
    const queryParams: Record<string, string | null> = {
      page: this.page > 1 ? String(this.page) : null,
      sortBy: this.sortBy !== 'createdAt' ? this.sortBy : null,
      sortDirection: this.sortDirection !== 'desc' ? this.sortDirection : null,
      provider: this.provider.trim() || null,
      modelId: this.modelId.trim() || null,
      status: this.status.trim() || null,
      clusterId: this.clusterId != null && this.clusterId > 0 ? String(this.clusterId) : null,
      windowDays: this.windowDays !== 7 ? String(this.windowDays) : null
    };

    this.router.navigate([], {
      relativeTo: this.route,
      queryParams,
      replaceUrl: true
    });
  }

  private loadAll(): void {
    this.loadPromptTemplate();
    this.loadWorkerStatus();
    this.loadLlmHealth();
    this.loadSummary();
    this.loadLogs();
  }

  private loadLlmHealth(): void {
    this.llmHealthLoading = true;

    this.aiAuditService.getLlmHealth().subscribe({
      next: (response) => {
        this.llmHealth = response;
        this.llmHealthLoading = false;
      },
      error: () => {
        this.llmHealth = null;
        this.llmHealthLoading = false;
      }
    });
  }

  private loadWorkerStatus(): void {
    this.workerStatusLoading = true;

    this.aiAuditService.getWorkerStatus().subscribe({
      next: (response) => {
        this.workerStatus = response;
        this.workerStatusLoading = false;
      },
      error: () => {
        this.workerStatus = null;
        this.workerStatusLoading = false;
      }
    });
  }

  private stopWorkerRunLoadingWithDelay(runStartUnixMs: number): void {
    const minVisibleMs = 900;
    const elapsed = Date.now() - runStartUnixMs;
    const remaining = Math.max(0, minVisibleMs - elapsed);

    window.setTimeout(() => {
      this.workerRunLoading = false;
    }, remaining);
  }

  private loadPromptTemplate(): void {
    this.promptLoading = true;
    this.promptStatusMessage = '';

    this.aiAuditService.getPromptTemplate(this.promptTemplateKey).subscribe({
      next: (response) => {
        this.promptTemplate = response.template;
        this.promptVersion = response.version;
        this.promptSource = response.source;
        this.promptUpdatedAt = response.updatedAt;
        this.promptUpdatedBy = response.updatedBy;
        this.promptLoading = false;
      },
      error: () => {
        this.promptTemplate = '';
        this.promptLoading = false;
        this.promptStatusMessage = 'No se pudo cargar la configuración de prompt.';
      }
    });
  }

  private loadSummary(): void {
    this.aiAuditService.getSummary(this.windowDays).subscribe({
      next: (response) => {
        this.summary = response;
      },
      error: () => {
        this.summary = null;
      }
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
      status: this.status || undefined,
      clusterId: this.clusterId ?? undefined
    }).subscribe({
      next: (response) => {
        this.logs = response.items;
        this.totalPages = response.totalPages;
        this.sortBy = response.sortBy || this.sortBy;
        this.sortDirection = response.sortDirection || this.sortDirection;
        if (this.expandedLogId && !this.logs.some((log) => log.id === this.expandedLogId)) {
          this.expandedLogId = null;
        }
        this.loading = false;
      },
      error: () => {
        this.logs = [];
        this.loading = false;
      }
    });
  }
}
