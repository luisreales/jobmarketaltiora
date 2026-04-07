import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { LinkedInJobSummary } from '../../models/job.models';
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
  private readonly jobsService = inject(JobsService);

  ngOnInit(): void {
    this.refresh();
  }

  refresh(): void {
    this.loading = true;
    this.jobsService.getJobs().subscribe({
      next: (jobs) => (this.jobs = jobs),
      error: () => (this.jobs = []),
      complete: () => (this.loading = false)
    });
  }
}
