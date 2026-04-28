import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  CommercialStrategyRecord,
  CommercialStrategyQuery,
  GenerateCommercialStrategyRequest,
  PagedMarketResponse
} from '../models/market.models';

@Injectable({ providedIn: 'root' })
export class CommercialStrategyService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/api/commercial-strategies`;

  getAll(query: CommercialStrategyQuery): Observable<PagedMarketResponse<CommercialStrategyRecord>> {
    let params = new HttpParams();
    if (query.search)     params = params.set('search', query.search);
    if (query.productId != null) params = params.set('productId', query.productId);
    if (query.page)       params = params.set('page', query.page);
    if (query.pageSize)   params = params.set('pageSize', query.pageSize);
    return this.http.get<PagedMarketResponse<CommercialStrategyRecord>>(this.base, { params });
  }

  getById(id: number): Observable<CommercialStrategyRecord> {
    return this.http.get<CommercialStrategyRecord>(`${this.base}/${id}`);
  }

  generate(request: GenerateCommercialStrategyRequest): Observable<CommercialStrategyRecord> {
    return this.http.post<CommercialStrategyRecord>(`${this.base}/generate`, request);
  }

  link(id: number, productId: number | null): Observable<CommercialStrategyRecord> {
    return this.http.patch<CommercialStrategyRecord>(`${this.base}/${id}/link`, { productId });
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }
}
