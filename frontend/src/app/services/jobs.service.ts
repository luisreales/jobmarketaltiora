import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  JobsQueryRequest,
  LinkedInAuthStatus,
  LinkedInJobDetail,
  LinkedInJobSummary,
  LinkedInSearchRequest,
  LinkedInSearchResponse,
  PagedJobSummaryResponse,
  UpworkScrapeResponse
} from '../models/job.models';

@Injectable({
  providedIn: 'root'
})
export class JobsService {
  private readonly jobsBaseUrl = `${environment.apiUrl}/api/jobs`;
  private readonly authBaseUrl = `${environment.apiUrl}/api/auth`;

  constructor(private readonly http: HttpClient) {}

  connect(): Observable<LinkedInAuthStatus> {
    return this.http.post<LinkedInAuthStatus>(`${this.authBaseUrl}/login`, {
      provider: 'linkedin',
      username: '',
      password: ''
    });
  }

  connectProvider(provider: 'linkedin' | 'upwork'): Observable<LinkedInAuthStatus> {
    return this.http.post<LinkedInAuthStatus>(`${this.authBaseUrl}/login`, {
      provider,
      username: '',
      password: ''
    });
  }

  disconnect(): Observable<LinkedInAuthStatus> {
    return this.http.post<LinkedInAuthStatus>(`${this.authBaseUrl}/logout/linkedin`, {});
  }

  getAuthStatus(): Observable<LinkedInAuthStatus> {
    return this.http.get<LinkedInAuthStatus>(`${this.authBaseUrl}/status/linkedin`);
  }

  getProviderAuthStatus(provider: 'linkedin' | 'upwork'): Observable<LinkedInAuthStatus> {
    return this.http.get<LinkedInAuthStatus>(`${this.authBaseUrl}/status/${provider}`);
  }

  scrape(request: LinkedInSearchRequest): Observable<LinkedInSearchResponse> {
    return this.http.post<LinkedInSearchResponse>(`${this.jobsBaseUrl}/search/scrape`, request);
  }

  scrapeUpwork(request: LinkedInSearchRequest): Observable<UpworkScrapeResponse> {
    return this.http.post<UpworkScrapeResponse>(`${this.jobsBaseUrl}/search/scrape/upwork`, request);
  }

  loginAndScrapeUpwork(request: LinkedInSearchRequest): Observable<UpworkScrapeResponse> {
    return this.http.post<UpworkScrapeResponse>(`${this.jobsBaseUrl}/search/scrape/upwork/login-and-scrape`, request);
  }

  getJobs(): Observable<LinkedInJobSummary[]> {
    return this.http.get<LinkedInJobSummary[]>(`${this.jobsBaseUrl}/jobs`);
  }

  queryJobs(request: JobsQueryRequest): Observable<PagedJobSummaryResponse> {
    return this.http.get<PagedJobSummaryResponse>(`${this.jobsBaseUrl}/jobs/query`, {
      params: {
        page: String(request.page ?? 1),
        pageSize: String(request.pageSize ?? 20),
        sortBy: request.sortBy ?? 'capturedAt',
        sortDirection: request.sortDirection ?? 'desc',
        title: request.title ?? '',
        company: request.company ?? '',
        location: request.location ?? '',
        source: request.source ?? '',
        searchTerm: request.searchTerm ?? '',
        salaryRange: request.salaryRange ?? ''
      }
    });
  }

  getJobById(id: number): Observable<LinkedInJobDetail> {
    return this.http.get<LinkedInJobDetail>(`${this.jobsBaseUrl}/jobs/${id}`);
  }

  getUpworkDetailById(id: number): Observable<LinkedInJobDetail> {
    return this.http.get<LinkedInJobDetail>(`${this.jobsBaseUrl}/upwork/${id}/detail`);
  }
}
