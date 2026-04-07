import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { LinkedInAuthStatus, LinkedInSearchResponse } from '../../models/job.models';
import { JobsService } from '../../services/jobs.service';

@Component({
  selector: 'app-search',
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './search.html',
  styleUrl: './search.scss'
})
export class Search {
  authStatus: LinkedInAuthStatus | null = null;
  scrapeResult: LinkedInSearchResponse | null = null;
  loading = false;
  scrapeForm;
  authError = false;
  private readonly jobsService = inject(JobsService);
  private readonly fb = inject(FormBuilder);

  constructor() {
    this.scrapeForm = this.fb.group({
      query: ['.NET', Validators.required],
      location: ['Remote', Validators.required],
      limit: [20, [Validators.required, Validators.min(1), Validators.max(100)]]
    });

    this.refreshStatus();
  }

  get isConnected(): boolean {
    return this.authStatus?.isAuthenticated === true;
  }

  refreshStatus(): void {
    this.jobsService.getAuthStatus().subscribe({
      next: (status) => {
        this.authStatus = status;
        this.authError = false;
      },
      error: () => {
        this.authStatus = null;
        this.authError = true;
      }
    });
  }

  connect(): void {
    if (this.isConnected || this.loading) {
      return;
    }

    this.loading = true;
    this.jobsService.connect().subscribe({
      next: (status) => {
        this.authStatus = status;
        this.authError = false;
      },
      error: () => {
        this.authStatus = null;
        this.authError = true;
        this.loading = false;
      },
      complete: () => (this.loading = false)
    });
  }

  disconnect(): void {
    if (!this.isConnected || this.loading) {
      return;
    }

    this.loading = true;
    this.jobsService.disconnect().subscribe({
      next: (status) => {
        this.authStatus = status;
        this.authError = false;
      },
      error: () => {
        this.authStatus = null;
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
    const payload = this.scrapeForm.getRawValue();
    this.jobsService.scrape({
      query: payload.query ?? '',
      location: payload.location ?? '',
      limit: Number(payload.limit ?? 20)
    }).subscribe({
      next: (result) => (this.scrapeResult = result),
      error: () => (this.scrapeResult = null),
      complete: () => {
        this.loading = false;
        this.refreshStatus();
      }
    });
  }
}
