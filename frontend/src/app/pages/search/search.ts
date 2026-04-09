import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import {
  LinkedInAuthStatus,
  LinkedInJobDetail,
  LinkedInSearchResponse,
  UpworkScrapeResponse,
  UpworkTouchedJob
} from '../../models/job.models';
import { JobsService } from '../../services/jobs.service';

@Component({
  selector: 'app-search',
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './search.html',
  styleUrl: './search.scss'
})
export class Search {
  linkedInAuthStatus: LinkedInAuthStatus | null = null;
  upworkAuthStatus: LinkedInAuthStatus | null = null;
  scrapeResult: LinkedInSearchResponse | UpworkScrapeResponse | null = null;
  upworkTouchedJobs: UpworkTouchedJob[] = [];
  selectedUpworkDetail: LinkedInJobDetail | null = null;
  selectedUpworkJobId: number | null = null;
  loading = false;
  loadingDetail = false;
  scrapeForm;
  authError = false;
  private readonly jobsService = inject(JobsService);
  private readonly fb = inject(FormBuilder);

  private isUpworkResponse(result: LinkedInSearchResponse | UpworkScrapeResponse): result is UpworkScrapeResponse {
    return 'provider' in result && Array.isArray((result as UpworkScrapeResponse).touched);
  }

  constructor() {
    this.scrapeForm = this.fb.group({
      query: ['.NET', Validators.required],
      location: ['Remote', Validators.required],
      limit: [20, [Validators.required, Validators.min(1), Validators.max(100)]],
      startPage: [1, [Validators.required, Validators.min(1), Validators.max(50)]],
      endPage: [1, [Validators.required, Validators.min(1), Validators.max(50)]],
      mode: ['upwork-scrape']
    });

    this.refreshStatus();
  }

  get isLinkedInConnected(): boolean {
    return this.linkedInAuthStatus?.isAuthenticated === true;
  }

  get isUpworkConnected(): boolean {
    return this.upworkAuthStatus?.isAuthenticated === true;
  }

  get isUpworkMode(): boolean {
    const mode = this.scrapeForm.getRawValue().mode;
    return mode === 'upwork-scrape' || mode === 'upwork-login-and-scrape';
  }

  refreshStatus(): void {
    this.jobsService.getProviderAuthStatus('linkedin').subscribe({
      next: (status) => {
        this.linkedInAuthStatus = status;
        this.authError = false;
      },
      error: () => {
        this.linkedInAuthStatus = null;
        this.authError = true;
      }
    });

    this.jobsService.getProviderAuthStatus('upwork').subscribe({
      next: (status) => {
        this.upworkAuthStatus = status;
      },
      error: () => {
        this.upworkAuthStatus = null;
      }
    });
  }

  connectLinkedIn(): void {
    if (this.isLinkedInConnected || this.loading) {
      return;
    }

    this.loading = true;
    this.jobsService.connectProvider('linkedin').subscribe({
      next: (status) => {
        this.linkedInAuthStatus = status;
        this.authError = false;
      },
      error: () => {
        this.linkedInAuthStatus = null;
        this.authError = true;
        this.loading = false;
      },
      complete: () => (this.loading = false)
    });
  }

  connectUpwork(): void {
    if (this.loading) {
      return;
    }

    this.loading = true;
    this.jobsService.connectProvider('upwork').subscribe({
      next: (status) => {
        this.upworkAuthStatus = status;
        this.authError = false;
      },
      error: () => {
        this.upworkAuthStatus = null;
        this.authError = true;
        this.loading = false;
      },
      complete: () => (this.loading = false)
    });
  }

  disconnect(): void {
    if (!this.isLinkedInConnected || this.loading) {
      return;
    }

    this.loading = true;
    this.jobsService.disconnect().subscribe({
      next: (status) => {
        this.linkedInAuthStatus = status;
        this.authError = false;
      },
      error: () => {
        this.linkedInAuthStatus = null;
        this.authError = true;
        this.loading = false;
      },
      complete: () => (this.loading = false)
    });
  }

  runScraping(): void {
    if (this.scrapeForm.invalid) {
      this.scrapeForm.markAllAsTouched();
      return;
    }

    this.loading = true;
    this.scrapeResult = null;
    this.upworkTouchedJobs = [];
    this.selectedUpworkDetail = null;
    this.selectedUpworkJobId = null;

    const payload = this.scrapeForm.getRawValue();
    const requestPayload = {
      query: payload.query ?? '',
      location: payload.location ?? '',
      limit: Number(payload.limit ?? 20),
      startPage: Number(payload.startPage ?? 1),
      endPage: Number(payload.endPage ?? 1)
    };

    const mode = payload.mode;
    const request$ = mode === 'upwork-login-and-scrape'
      ? this.jobsService.loginAndScrapeUpwork(requestPayload)
      : mode === 'upwork-scrape'
        ? this.jobsService.scrapeUpwork(requestPayload)
        : this.jobsService.scrape(requestPayload);

    request$.subscribe({
      next: (result) => {
        this.scrapeResult = result;
        if (this.isUpworkResponse(result)) {
          this.upworkTouchedJobs = result.touched ?? [];
        }
      },
      error: () => {
        this.scrapeResult = null;
        this.upworkTouchedJobs = [];
      },
      complete: () => {
        this.loading = false;
        this.refreshStatus();
      }
    });
  }

  openUpworkDetail(jobId: number): void {
    if (this.loadingDetail || this.selectedUpworkJobId === jobId) {
      return;
    }

    this.loadingDetail = true;
    this.selectedUpworkJobId = jobId;
    this.jobsService.getUpworkDetailById(jobId).subscribe({
      next: (detail) => {
        this.selectedUpworkDetail = detail;
      },
      error: () => {
        this.selectedUpworkDetail = null;
      },
      complete: () => {
        this.loadingDetail = false;
      }
    });
  }
}
