import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  IndustryTechDto,
  PagedTechnologyResponse,
  TechRebuildResultDto,
  TechnologyDetailDto,
  TechnologyDto,
  TechnologyGraphDto
} from '../models/technology.models';

@Injectable({ providedIn: 'root' })
export class TechnologyService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/api/technologies`;

  getTechnologies(opts: {
    search?: string;
    category?: string;
    lifecycleStage?: string;
    isAiRelated?: boolean;
    page?: number;
    pageSize?: number;
    sortBy?: string;
  } = {}): Observable<PagedTechnologyResponse> {
    let params = new HttpParams()
      .set('page', String(opts.page ?? 1))
      .set('pageSize', String(opts.pageSize ?? 50));

    if (opts.search)         params = params.set('search', opts.search);
    if (opts.category)       params = params.set('category', opts.category);
    if (opts.lifecycleStage) params = params.set('lifecycleStage', opts.lifecycleStage);
    if (opts.isAiRelated != null) params = params.set('isAiRelated', String(opts.isAiRelated));
    if (opts.sortBy)         params = params.set('sortBy', opts.sortBy);

    return this.http.get<PagedTechnologyResponse>(this.base, { params });
  }

  getTrending(): Observable<TechnologyDto[]> {
    return this.http.get<TechnologyDto[]>(`${this.base}/trending`);
  }

  getEmerging(): Observable<TechnologyDto[]> {
    return this.http.get<TechnologyDto[]>(`${this.base}/emerging`);
  }

  getDeclining(): Observable<TechnologyDto[]> {
    return this.http.get<TechnologyDto[]>(`${this.base}/declining`);
  }

  getAi(): Observable<TechnologyDto[]> {
    return this.http.get<TechnologyDto[]>(`${this.base}/ai`);
  }

  getGraph(): Observable<TechnologyGraphDto> {
    return this.http.get<TechnologyGraphDto>(`${this.base}/graph`);
  }

  getById(id: number): Observable<TechnologyDetailDto> {
    return this.http.get<TechnologyDetailDto>(`${this.base}/${id}`);
  }

  getIndustries(): Observable<IndustryTechDto[]> {
    return this.http.get<IndustryTechDto[]>(`${environment.apiUrl}/api/trends/industries`);
  }

  rebuild(): Observable<TechRebuildResultDto> {
    return this.http.post<TechRebuildResultDto>(`${this.base}/rebuild`, {});
  }
}
