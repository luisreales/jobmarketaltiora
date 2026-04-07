import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { LinkedInJobDetail } from '../../models/job.models';
import { JobsService } from '../../services/jobs.service';
import { JobDetailCardComponent } from '../../components/job-detail-card/job-detail-card';

@Component({
  selector: 'app-job-detail',
  imports: [CommonModule, RouterLink, JobDetailCardComponent],
  templateUrl: './job-detail.html',
  styleUrl: './job-detail.scss'
})
export class JobDetail implements OnInit {
  jobId = 0;
  job: LinkedInJobDetail | null = null;
  private readonly route = inject(ActivatedRoute);
  private readonly jobsService = inject(JobsService);

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    if (!id) {
      return;
    }

    this.jobId = id;
    this.jobsService.getJobById(id).subscribe({
      next: (job) => (this.job = job),
      error: () => (this.job = null)
    });
  }
}
