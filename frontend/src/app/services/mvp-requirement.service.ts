import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  MvpRequirementRecord,
  MvpRequirementQuery,
  GenerateMvpRequirementRequest,
  PagedMarketResponse
} from '../models/market.models';

@Injectable({ providedIn: 'root' })
export class MvpRequirementService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/api/mvp-requirements`;

  getAll(query: MvpRequirementQuery): Observable<PagedMarketResponse<MvpRequirementRecord>> {
    let params = new HttpParams();
    if (query.search)     params = params.set('search', query.search);
    if (query.productId != null) params = params.set('productId', query.productId);
    if (query.page)       params = params.set('page', query.page);
    if (query.pageSize)   params = params.set('pageSize', query.pageSize);
    return this.http.get<PagedMarketResponse<MvpRequirementRecord>>(this.base, { params });
  }

  getById(id: number): Observable<MvpRequirementRecord> {
    return this.http.get<MvpRequirementRecord>(`${this.base}/${id}`);
  }

  generate(request: GenerateMvpRequirementRequest): Observable<MvpRequirementRecord> {
    return this.http.post<MvpRequirementRecord>(`${this.base}/generate`, request);
  }

  link(id: number, productId: number | null): Observable<MvpRequirementRecord> {
    return this.http.patch<MvpRequirementRecord>(`${this.base}/${id}/link`, { productId });
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }
}
