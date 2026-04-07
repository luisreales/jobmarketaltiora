import { CommonModule } from '@angular/common';
import { Component, Input } from '@angular/core';
import { LinkedInJobDetail } from '../../models/job.models';

@Component({
  selector: 'app-job-detail-card',
  imports: [CommonModule],
  templateUrl: './job-detail-card.html',
  styleUrl: './job-detail-card.scss'
})
export class JobDetailCardComponent {
  @Input() job: LinkedInJobDetail | null = null;
}
