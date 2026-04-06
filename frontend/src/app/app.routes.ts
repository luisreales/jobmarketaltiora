import { Routes } from '@angular/router';
import { Dashboard } from './pages/dashboard/dashboard';
import { HotelDetail } from './pages/hotel-detail/hotel-detail';
import { Search } from './pages/search/search';

export const routes: Routes = [
	{ path: '', redirectTo: 'dashboard', pathMatch: 'full' },
	{ path: 'dashboard', component: Dashboard },
	{ path: 'search', component: Search },
	{ path: 'hotels/:id', component: HotelDetail },
	{ path: '**', redirectTo: 'dashboard' }
];
