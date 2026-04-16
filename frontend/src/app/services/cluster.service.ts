import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  ClusterLead,
  ClusterLeadsQuery,
  ClusterRebuildResult,
  MarketCluster,
  MarketClusterQuery,
  PagedMarketResponse
} from '../models/market.models';


@Injectable({ providedIn: 'root' })
export class ClusterService {
  private readonly base = `${environment.apiUrl}/api/market/clusters`;

  constructor(private readonly http: HttpClient) {}

  getClusters(query: MarketClusterQuery): Observable<PagedMarketResponse<MarketCluster>> {
    let params = new HttpParams()
      .set('page', String(query.page ?? 1))
      .set('pageSize', String(query.pageSize ?? 20));

    if (query.minBlueOceanScore != null) {
      params = params.set('minBlueOceanScore', String(query.minBlueOceanScore));
    }
    if (query.painCategory) {
      params = params.set('painCategory', query.painCategory);
    }
    if (query.industry) {
      params = params.set('industry', query.industry);
    }
    if (query.opportunityType) {
      params = params.set('opportunityType', query.opportunityType);
    }
    if (query.isActionable != null) {
      params = params.set('isActionable', String(query.isActionable));
    }

    return this.http.get<PagedMarketResponse<MarketCluster>>(this.base, { params });
  }

  getClusterLeads(clusterId: number, query: ClusterLeadsQuery): Observable<PagedMarketResponse<ClusterLead>> {
    let params = new HttpParams()
      .set('page', String(query.page ?? 1))
      .set('pageSize', String(query.pageSize ?? 10));

    if (query.minLeadScore != null) {
      params = params.set('minLeadScore', String(query.minLeadScore));
    }

    return this.http.get<PagedMarketResponse<ClusterLead>>(
      `${this.base}/${clusterId}/leads`,
      { params }
    );
  }

  rebuild(): Observable<ClusterRebuildResult> {
    return this.http.post<ClusterRebuildResult>(`${this.base}/rebuild`, {});
  }

  backfillInsights(): Observable<{ backfilled: number; errors: number; message: string }> {
    return this.http.post<{ backfilled: number; errors: number; message: string }>(
      `${this.base}/backfill-insights`,
      {}
    );
  }

  cleanupSmallClusters(maxJobCount = 5): Observable<{ deleted: number; inspected: number; message: string }> {
    return this.http.delete<{ deleted: number; inspected: number; message: string }>(
      `${this.base}/cleanup`,
      { params: { maxJobCount: String(maxJobCount) } }
    );
  }

  synthesize(clusterId: number): Observable<MarketCluster> {
    return this.http.post<MarketCluster>(`${this.base}/${clusterId}/synthesize`, {});
  }
}
