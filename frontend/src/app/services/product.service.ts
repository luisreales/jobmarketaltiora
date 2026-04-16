import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { PagedMarketResponse, ProductGenerateResult, ProductQuery, ProductSuggestion } from '../models/market.models';

@Injectable({ providedIn: 'root' })
export class ProductService {
  private readonly base = `${environment.apiUrl}/api/products`;

  constructor(private readonly http: HttpClient) {}

  getProducts(query: ProductQuery): Observable<PagedMarketResponse<ProductSuggestion>> {
    let params = new HttpParams()
      .set('page', String(query.page ?? 1))
      .set('pageSize', String(query.pageSize ?? 20));

    if (query.opportunityType) {
      params = params.set('opportunityType', query.opportunityType);
    }
    if (query.industry) {
      params = params.set('industry', query.industry);
    }

    return this.http.get<PagedMarketResponse<ProductSuggestion>>(this.base, { params });
  }

  getProduct(id: number): Observable<ProductSuggestion> {
    return this.http.get<ProductSuggestion>(`${this.base}/${id}`);
  }

  generate(): Observable<ProductGenerateResult> {
    return this.http.post<ProductGenerateResult>(`${this.base}/generate`, {});
  }

  synthesize(id: number): Observable<ProductSuggestion> {
    return this.http.post<ProductSuggestion>(`${this.base}/${id}/synthesize`, {});
  }
}
