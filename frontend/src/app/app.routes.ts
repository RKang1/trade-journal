import { Routes } from '@angular/router';

export const routes: Routes = [
	{
		path: 'journal',
		loadComponent: () => import('./pages/journal/journal').then((m) => m.Journal),
	},
	{ path: '', pathMatch: 'full', redirectTo: 'journal' },
	{ path: '**', redirectTo: 'journal' },
];
