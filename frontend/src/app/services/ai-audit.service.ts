import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  AiWorkerRunNowResult,
  AiWorkerStatus,
  LlmHealth,
  AiPromptTemplate,
  AiPromptLogsQuery,
  AiUsageSummary,
  PagedAiPromptLogResponse,
  UpdateAiPromptTemplateRequest
} from '../models/ai-audit.models';

@Injectable({
  providedIn: 'root'
})
export class AiAuditService {
  private readonly aiBaseUrl = `${environment.apiUrl}/api/ai`;

  constructor(private readonly http: HttpClient) {}

  getLogs(query: AiPromptLogsQuery): Observable<PagedAiPromptLogResponse> {
    return this.http.get<PagedAiPromptLogResponse>(`${this.aiBaseUrl}/logs`, {
      params: {
        page: String(query.page ?? 1),
        pageSize: String(query.pageSize ?? 20),
        sortBy: query.sortBy ?? 'createdAt',
        sortDirection: query.sortDirection ?? 'desc',
        provider: query.provider ?? '',
        modelId: query.modelId ?? '',
        status: query.status ?? '',
        clusterId: query.clusterId != null ? String(query.clusterId) : '',
        fromDate: query.fromDate ?? '',
        toDate: query.toDate ?? ''
      }
    });
  }

  getSummary(windowDays = 7): Observable<AiUsageSummary> {
    return this.http.get<AiUsageSummary>(`${this.aiBaseUrl}/summary`, {
      params: {
        windowDays: String(windowDays)
      }
    });
  }

  getPromptTemplate(key: string): Observable<AiPromptTemplate> {
    return this.http.get<AiPromptTemplate>(`${this.aiBaseUrl}/prompts/${encodeURIComponent(key)}`);
  }

  updatePromptTemplate(key: string, request: UpdateAiPromptTemplateRequest): Observable<AiPromptTemplate> {
    return this.http.put<AiPromptTemplate>(`${this.aiBaseUrl}/prompts/${encodeURIComponent(key)}`, request);
  }

  getWorkerStatus(): Observable<AiWorkerStatus> {
    return this.http.get<AiWorkerStatus>(`${this.aiBaseUrl}/worker-status`);
  }

  runWorkerNow(): Observable<AiWorkerRunNowResult> {
    return this.http.post<AiWorkerRunNowResult>(`${this.aiBaseUrl}/worker/run-now`, {});
  }

  getLlmHealth(): Observable<LlmHealth> {
    return this.http.get<LlmHealth>(`${this.aiBaseUrl}/llm-health`);
  }
}
