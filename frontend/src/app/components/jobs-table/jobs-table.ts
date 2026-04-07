import { CommonModule } from '@angular/common';
import { Component, Input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { LinkedInJobSummary } from '../../models/job.models';

@Component({
  selector: 'app-jobs-table',
  imports: [CommonModule, RouterLink],
  templateUrl: './jobs-table.html',
  styleUrl: './jobs-table.scss'
})
export class JobsTableComponent {
  @Input() jobs: LinkedInJobSummary[] = [];
}
