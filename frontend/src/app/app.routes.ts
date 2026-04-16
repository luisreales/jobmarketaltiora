import { Routes } from '@angular/router';

export const routes: Routes = [
	{ path: '', redirectTo: 'jobs', pathMatch: 'full' },
	{ path: 'jobs', loadComponent: () => import('./pages/dashboard/dashboard').then((m) => m.Dashboard) },
	{ path: 'opportunities', loadComponent: () => import('./pages/opportunities/opportunities').then((m) => m.Opportunities) },
	{ path: 'products', loadComponent: () => import('./pages/products/products').then((m) => m.Products) },
	{ path: 'products/:id', loadComponent: () => import('./pages/product-detail/product-detail').then((m) => m.ProductDetail) },
	{ path: 'ai-audit', loadComponent: () => import('./pages/ai-audit/ai-audit').then((m) => m.AiAudit) },
	{ path: 'scraping', loadComponent: () => import('./pages/search/search').then((m) => m.Search) },
	{ path: 'jobs/:id', loadComponent: () => import('./pages/job-detail/job-detail').then((m) => m.JobDetail) },
	{ path: '**', redirectTo: 'jobs' }
];
