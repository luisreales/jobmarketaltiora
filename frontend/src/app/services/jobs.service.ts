import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  LinkedInAuthStatus,
  LinkedInJobDetail,
  LinkedInJobSummary,
  LinkedInSearchRequest,
  LinkedInSearchResponse
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

  disconnect(): Observable<LinkedInAuthStatus> {
    return this.http.post<LinkedInAuthStatus>(`${this.authBaseUrl}/logout/linkedin`, {});
  }

  getAuthStatus(): Observable<LinkedInAuthStatus> {
    return this.http.get<LinkedInAuthStatus>(`${this.authBaseUrl}/status/linkedin`);
  }

  scrape(request: LinkedInSearchRequest): Observable<LinkedInSearchResponse> {
    return this.http.post<LinkedInSearchResponse>(`${this.jobsBaseUrl}/search/scrape`, request);
  }

  getJobs(): Observable<LinkedInJobSummary[]> {
    return this.http.get<LinkedInJobSummary[]>(`${this.jobsBaseUrl}/jobs`);
  }

  getJobById(id: number): Observable<LinkedInJobDetail> {
    return this.http.get<LinkedInJobDetail>(`${this.jobsBaseUrl}/jobs/${id}`);
  }
}
