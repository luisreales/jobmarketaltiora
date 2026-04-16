export interface AiPromptLogsQuery {
  page?: number;
  pageSize?: number;
  sortBy?: string;
  sortDirection?: 'asc' | 'desc';
  provider?: string;
  modelId?: string;
  status?: string;
  clusterId?: number;
  fromDate?: string;
  toDate?: string;
}

export interface AiPromptLog {
  id: number;
  jobId: number | null;
  clusterId: number | null;
  provider: string;
  modelId: string;
  promptVersion: string;
  promptHash: string;
  promptText: string;
  responseText: string;
  cacheHit: boolean;
  isSuccess: boolean;
  status: string;
  errorMessage: string | null;
  promptTokens: number;
  completionTokens: number;
  totalTokens: number;
  latencyMs: number;
  createdAt: string;
}

export interface AiUsageSummary {
  fromDate: string;
  toDate: string;
  totalCalls: number;
  successCalls: number;
  failedCalls: number;
  cacheHits: number;
  totalTokens: number;
  averageLatencyMs: number;
}

export interface PagedAiPromptLogResponse {
  items: AiPromptLog[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  sortBy: string;
  sortDirection: 'asc' | 'desc';
}

export interface AiPromptTemplate {
  key: string;
  template: string;
  version: string;
  isActive: boolean;
  source: string;
  updatedAt: string | null;
  updatedBy: string | null;
}

export interface UpdateAiPromptTemplateRequest {
  template: string;
  version?: string;
  isActive: boolean;
  updatedBy?: string;
}

export interface AiWorkerStatus {
  isRunning: boolean;
  lastStartedAt: string | null;
  lastCompletedAt: string | null;
  lastProcessedJobs: number;
  lastOutcome: string;
  lastError: string | null;
  intervalSeconds: number;
  batchSize: number;
  lastTrigger: string;
}

export interface AiWorkerRunNowResult {
  processedJobs: number;
  startedAt: string;
  completedAt: string;
  trigger: string;
}

export interface LlmHealth {
  isConfigured: boolean;
  isHealthy: boolean;
  state: 'healthy' | 'degraded' | 'not-configured';
  modelId: string;
  lastSuccessAt: string | null;
  lastFailureAt: string | null;
  message: string;
}
