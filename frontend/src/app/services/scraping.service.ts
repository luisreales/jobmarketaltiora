import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface ScrapeRequest {
  query: string;
  location: string;
  limit: number;
  providers: string[];
  totalPaging?: number;
  startPage?: number;
  endPage?: number;
  showBrowser?: boolean;
  analysisPromptKey?: string;
}

export interface ScrapeResult {
  savedCount: number;
  totalFound: number;
  executedAtUtc: string;
  activeAnalysisPromptKey?: string;
  touchedCount?: number;
}

export interface DataQualityReport {
  totalJobs: number;
  duplicateCount: number;
  staleUnprocessedCount: number;
  unprocessedCount: number;
  cleanCount: number;
}

export interface PurgeResult {
  dryRun: boolean;
  deletedDuplicates: number;
  deletedStaleUnprocessed: number;
  totalDeleted: number;
  staleDaysThreshold: number;
}

@Injectable({
  providedIn: 'root'
})
export class ScrapingService {
  private apiUrl = `${environment.apiUrl}/api/jobs`;

  constructor(private http: HttpClient) {}

  scrapeLinkedIn(request: ScrapeRequest): Observable<ScrapeResult> {
    return this.http.post<ScrapeResult>(`${this.apiUrl}/search/scrape`, request);
  }

  scrapeUpwork(request: ScrapeRequest): Observable<ScrapeResult> {
    return this.http.post<ScrapeResult>(`${this.apiUrl}/search/scrape/upwork/login-and-scrape`, request);
  }

  scrapeMultiProvider(request: ScrapeRequest): Observable<ScrapeResult> {
    return this.http.post<ScrapeResult>(`${this.apiUrl}/search/scrape`, request);
  }

  getDataQuality(): Observable<DataQualityReport> {
    return this.http.get<DataQualityReport>(`${this.apiUrl}/jobs/quality`);
  }

  purgeJobs(dryRun: boolean, staleDays: number): Observable<PurgeResult> {
    return this.http.post<PurgeResult>(
      `${this.apiUrl}/jobs/purge?dryRun=${dryRun}&staleDays=${staleDays}`,
      {}
    );
  }
}
