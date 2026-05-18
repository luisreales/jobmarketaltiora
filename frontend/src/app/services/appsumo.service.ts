import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  AppSumoStats,
  AppSumoCategoryDto,
  AppSumoProductPagedResult,
  AppSumoReviewPagedResult,
  AppSumoScrapeRunDto,
  AppSumoProductQuery,
  AppSumoReviewQuery,
  StartScrapeRequest,
} from '../models/appsumo.models';

@Injectable({ providedIn: 'root' })
export class AppSumoService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/api/appsumo`;

  getStats(): Observable<AppSumoStats> {
    return this.http.get<AppSumoStats>(`${this.base}/stats`);
  }

  getCategories(): Observable<AppSumoCategoryDto[]> {
    return this.http.get<AppSumoCategoryDto[]>(`${this.base}/categories`);
  }

  getProducts(query: AppSumoProductQuery = {}): Observable<AppSumoProductPagedResult> {
    const params = this.toParams(query);
    return this.http.get<AppSumoProductPagedResult>(`${this.base}/products`, { params });
  }

  getReviews(query: AppSumoReviewQuery = {}): Observable<AppSumoReviewPagedResult> {
    const params = this.toParams(query);
    return this.http.get<AppSumoReviewPagedResult>(`${this.base}/reviews`, { params });
  }

  getRuns(): Observable<AppSumoScrapeRunDto[]> {
    return this.http.get<AppSumoScrapeRunDto[]>(`${this.base}/runs`);
  }

  startScrape(request: StartScrapeRequest): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.base}/scrape/start`, request);
  }

  private toParams(obj: object): HttpParams {
    let params = new HttpParams();
    for (const [k, v] of Object.entries(obj as Record<string, unknown>)) {
      if (v !== null && v !== undefined && v !== '') {
        params = params.set(k, String(v));
      }
    }
    return params;
  }
}
