import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Params, RouterLink } from '@angular/router';
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
  loading = true;
  backLinkPath = '/jobs';
  backLinkLabel = '← Back to Jobs';
  backLinkQueryParams: Params = {};
  private readonly route = inject(ActivatedRoute);
  private readonly jobsService = inject(JobsService);

  ngOnInit(): void {
    this.restoreBackLinkFromQuery();

    const id = Number(this.route.snapshot.paramMap.get('id'));
    if (!id) {
      this.loading = false;
      return;
    }

    this.jobId = id;
    this.jobsService.getJobById(id).subscribe({
      next: (job) => {
        this.job = job;
        this.loading = false;
      },
      error: () => {
        this.job = null;
        this.loading = false;
      }
    });
  }

  private restoreBackLinkFromQuery(): void {
    const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');
    if (!returnUrl || !returnUrl.startsWith('/')) {
      return;
    }

    const [path, rawQuery] = returnUrl.split('?');
    if (!path) {
      return;
    }

    this.backLinkPath = path;
    this.backLinkLabel = this.resolveBackLinkLabel(path);

    const parsedParams: Params = {};
    if (rawQuery) {
      const searchParams = new URLSearchParams(rawQuery);
      searchParams.forEach((value, key) => {
        parsedParams[key] = value;
      });
    }

    this.backLinkQueryParams = parsedParams;
  }

  private resolveBackLinkLabel(path: string): string {
    if (path.startsWith('/opportunities')) return '← Back to Opportunities';
    if (path.startsWith('/ai-audit')) return '← Back to AI Audit';
    return '← Back to Jobs';
  }
}
