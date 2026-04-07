import { Routes } from '@angular/router';
import { Dashboard } from './pages/dashboard/dashboard';
import { JobDetail } from './pages/job-detail/job-detail';
import { Search } from './pages/search/search';

export const routes: Routes = [
	{ path: '', redirectTo: 'jobs', pathMatch: 'full' },
	{ path: 'jobs', component: Dashboard },
	{ path: 'scraping', component: Search },
	{ path: 'jobs/:id', component: JobDetail },
	{ path: '**', redirectTo: 'jobs' }
];
