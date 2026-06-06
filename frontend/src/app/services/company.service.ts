import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { CompanyProfileDto, CompanyRebuildResultDto } from '../models/company.models';
import { environment } from '../../environments/environment';

export interface CompanyQuery {
  search?: string;
  industry?: string;
  directClient?: boolean;
  hasAi?: boolean;
  hasCloudMigration?: boolean;
  sortBy?: string;
  page?: number;
  pageSize?: number;
}

@Injectable({ providedIn: 'root' })
export class CompanyService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/api/companies`;

  getAll(query: CompanyQuery = {}): Observable<CompanyProfileDto[]> {
    let params = new HttpParams();
    if (query.search) params = params.set('search', query.search);
    if (query.industry) params = params.set('industry', query.industry);
    if (query.directClient !== undefined) params = params.set('directClient', String(query.directClient));
    if (query.hasAi !== undefined) params = params.set('hasAi', String(query.hasAi));
    if (query.hasCloudMigration !== undefined) params = params.set('hasCloudMigration', String(query.hasCloudMigration));
    if (query.sortBy) params = params.set('sortBy', query.sortBy);
    if (query.page) params = params.set('page', String(query.page));
    if (query.pageSize) params = params.set('pageSize', String(query.pageSize));
    return this.http.get<CompanyProfileDto[]>(this.base, { params });
  }

  rebuild(): Observable<CompanyRebuildResultDto> {
    return this.http.post<CompanyRebuildResultDto>(`${this.base}/rebuild`, {});
  }
}
