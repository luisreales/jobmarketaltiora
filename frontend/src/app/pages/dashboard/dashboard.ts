import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { JobsQueryRequest, LinkedInJobSummary } from '../../models/job.models';
import { JobsService } from '../../services/jobs.service';
import { JobsTableComponent } from '../../components/jobs-table/jobs-table';

@Component({
  selector: 'app-dashboard',
  imports: [CommonModule, JobsTableComponent],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.scss'
})
export class Dashboard implements OnInit {
  jobs: LinkedInJobSummary[] = [];
  loading = false;
  totalCount = 0;
  query: JobsQueryRequest = {
    page: 1,
    pageSize: 20,
    sortBy: 'capturedAt',
    sortDirection: 'desc'
  };

  private readonly jobsService = inject(JobsService);

  ngOnInit(): void {
    // Initial load is triggered by the jobs table through queryChange.
  }

  refresh(query: JobsQueryRequest): void {
    this.loading = true;
    this.query = { ...this.query, ...query };

    this.jobsService.queryJobs(this.query).subscribe({
      next: (response) => {
        this.jobs = response.items;
        this.totalCount = response.totalCount;
      },
      error: () => {
        this.jobs = [];
        this.totalCount = 0;
      },
      complete: () => {
        this.loading = false;
      }
    });
  }

  reloadCurrentQuery(): void {
    this.refresh(this.query);
  }
}
