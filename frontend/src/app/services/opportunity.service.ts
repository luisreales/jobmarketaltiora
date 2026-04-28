import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  Opportunity,
  OpportunityQuery,
  CreateProductFromOpportunityRequest,
  ProductSuggestion
} from '../models/market.models';
import { PagedMarketResponse } from '../models/market.models';

@Injectable({ providedIn: 'root' })
export class OpportunityService {
  private readonly base = `${environment.apiUrl}/api`;
  private readonly http = inject(HttpClient);

  getOpportunities(query: OpportunityQuery = {}): Observable<PagedMarketResponse<Opportunity>> {
    const params: Record<string, string> = {};
    if (query.llmStatus) params['llmStatus'] = query.llmStatus;
    if (query.page)      params['page']      = String(query.page);
    if (query.pageSize)  params['pageSize']  = String(query.pageSize);
    return this.http.get<PagedMarketResponse<Opportunity>>(`${this.base}/opportunities`, { params });
  }

  getOpportunity(id: number): Observable<Opportunity> {
    return this.http.get<Opportunity>(`${this.base}/opportunities/${id}`);
  }

  synthesizeIdeas(id: number): Observable<Opportunity> {
    return this.http.post<Opportunity>(`${this.base}/opportunities/${id}/synthesize-ideas`, {});
  }

  createFromJob(jobId: number): Observable<Opportunity> {
    return this.http.post<Opportunity>(`${this.base}/jobs/jobs/${jobId}/create-opportunity`, {});
  }

  createProductFromOpportunity(request: CreateProductFromOpportunityRequest): Observable<ProductSuggestion> {
    return this.http.post<ProductSuggestion>(`${this.base}/products/from-opportunity`, request);
  }

  deleteProduct(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/products/${id}`);
  }

  deleteOpportunity(opportunityId: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/opportunities/${opportunityId}`);
  }

  removeFromJob(jobId: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/jobs/jobs/${jobId}/remove-opportunity`);
  }
}
