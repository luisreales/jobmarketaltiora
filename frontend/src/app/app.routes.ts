import { Routes } from '@angular/router';

export const routes: Routes = [
	{ path: '', redirectTo: 'jobs', pathMatch: 'full' },
	{ path: 'jobs', loadComponent: () => import('./pages/dashboard/dashboard').then((m) => m.Dashboard) },
	{ path: 'opportunities', loadComponent: () => import('./pages/opportunities/opportunities').then((m) => m.Opportunities) },
	{ path: 'opportunities/:id', loadComponent: () => import('./pages/opportunity-detail/opportunity-detail').then((m) => m.OpportunityDetail) },
	{ path: 'products', loadComponent: () => import('./pages/products/products').then((m) => m.Products) },
	{ path: 'products/:id', loadComponent: () => import('./pages/product-detail/product-detail').then((m) => m.ProductDetail) },
	{ path: 'opportunity-ideas', loadComponent: () => import('./pages/opportunity-ideas/opportunity-ideas').then((m) => m.OpportunityIdeas) },
	{ path: 'commercial-strategies', loadComponent: () => import('./pages/commercial-strategies/commercial-strategies').then((m) => m.CommercialStrategies) },
	{ path: 'mvp-requirements', loadComponent: () => import('./pages/mvp-requirements/mvp-requirements').then((m) => m.MvpRequirements) },
	{ path: 'ai-audit', loadComponent: () => import('./pages/ai-audit/ai-audit').then((m) => m.AiAudit) },
	{ path: 'prompt-ai', loadComponent: () => import('./pages/prompt-ai/prompt-ai').then((m) => m.PromptAi) },
	{ path: 'scraping', loadComponent: () => import('./pages/scraping/scraping').then((m) => m.ScrapingComponent) },
	{ path: 'jobs/:id', loadComponent: () => import('./pages/job-detail/job-detail').then((m) => m.JobDetail) },
	{ path: '**', redirectTo: 'jobs' }
];
