import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { PagedMarketResponse, ProductGenerateResult, ProductQuery, ProductSuggestion, UpdateProductRequest } from '../models/market.models';

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

  synthesizeStrategy(id: number): Observable<ProductSuggestion> {
    return this.http.post<ProductSuggestion>(`${this.base}/${id}/synthesize-strategy`, {});
  }

  createFromCluster(clusterId: number): Observable<ProductSuggestion> {
    return this.http.post<ProductSuggestion>(`${this.base}/from-cluster/${clusterId}`, {});
  }

  synthesizeTechnicalMvp(id: number): Observable<ProductSuggestion> {
    return this.http.post<ProductSuggestion>(`${this.base}/${id}/synthesize-technical-mvp`, {});
  }

  closeProduct(id: number): Observable<ProductSuggestion> {
    return this.http.patch<ProductSuggestion>(`${this.base}/${id}/close`, {});
  }

  updateProduct(id: number, request: UpdateProductRequest): Observable<ProductSuggestion> {
    return this.http.put<ProductSuggestion>(`${this.base}/${id}`, request);
  }

  uploadProductImage(id: number, file: File): Observable<ProductSuggestion> {
    const formData = new FormData();
    formData.append('image', file);
    return this.http.post<ProductSuggestion>(`${this.base}/${id}/image`, formData);
  }

  exportProductsCsv(query: ProductQuery): Observable<Blob> {
    let params = new HttpParams()
      .set('page', String(query.page ?? 1))
      .set('pageSize', String(query.pageSize ?? 20));

    if (query.opportunityType) {
      params = params.set('opportunityType', query.opportunityType);
    }
    if (query.industry) {
      params = params.set('industry', query.industry);
    }

    return this.http.get(`${this.base}/export`, {
      params,
      responseType: 'blob'
    });
  }

  deleteProduct(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }
}
