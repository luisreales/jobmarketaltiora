import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  MarketLead,
  MarketLeadsQuery,
  MarketOpportunity,
  MarketOpportunityQuery,
  MarketTrend,
  MarketTrendsQuery,
  PagedMarketResponse
} from '../models/market.models';

@Injectable({
  providedIn: 'root'
})
export class MarketService {
  private readonly marketBaseUrl = `${environment.apiUrl}/api/market`;

  constructor(private readonly http: HttpClient) {}

  getOpportunities(query: MarketOpportunityQuery): Observable<PagedMarketResponse<MarketOpportunity>> {
    return this.http.get<PagedMarketResponse<MarketOpportunity>>(`${this.marketBaseUrl}/opportunities`, {
      params: {
        fromDate: query.fromDate ?? '',
        toDate: query.toDate ?? '',
        source: query.source ?? '',
        minOpportunityScore: query.minOpportunityScore != null ? String(query.minOpportunityScore) : '',
        minUrgencyScore: query.minUrgencyScore != null ? String(query.minUrgencyScore) : '',
        page: String(query.page ?? 1),
        pageSize: String(query.pageSize ?? 20)
      }
    });
  }

  getLeads(query: MarketLeadsQuery): Observable<PagedMarketResponse<MarketLead>> {
    return this.http.get<PagedMarketResponse<MarketLead>>(`${this.marketBaseUrl}/leads`, {
      params: {
        painPoint: query.painPoint ?? '',
        source: query.source ?? '',
        minScore: query.minScore != null ? String(query.minScore) : '',
        page: String(query.page ?? 1),
        pageSize: String(query.pageSize ?? 20)
      }
    });
  }

  getTrends(query: MarketTrendsQuery): Observable<MarketTrend[]> {
    return this.http.get<MarketTrend[]>(`${this.marketBaseUrl}/trends`, {
      params: {
        windowDays: String(query.windowDays ?? 14),
        source: query.source ?? ''
      }
    });
  }
}
