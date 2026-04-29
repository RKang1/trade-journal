import { TestBed, ComponentFixture } from '@angular/core/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { describe, beforeEach, afterEach, it, expect, vi } from 'vitest';
import { Journal } from './journal';
import { GoogleIdentityService } from '../../core/google.service';
import { AuthService } from '../../core/auth.service';
import { authInterceptor } from '../../core/auth.interceptor';
import { environment } from '../../../environments/environment';
import { Trade } from '../../core/api.types';

class GoogleIdentityServiceStub {
	lastTarget: HTMLElement | null = null;
	lastCallback: ((idToken: string) => void) | null = null;
	renderButton = vi.fn(async (parent: HTMLElement, cb: (token: string) => void) => {
		this.lastTarget = parent;
		this.lastCallback = cb;
	});
	load = vi.fn(async () => undefined);
}

const sampleTrade = (overrides: Partial<Trade> = {}): Trade => ({
	id: overrides.id ?? 'trade-1',
	symbol: overrides.symbol ?? 'AAPL',
	side: overrides.side ?? 'Long',
	status: overrides.status ?? 'Open',
	entryAt: overrides.entryAt ?? '2026-04-01T10:00:00Z',
	entryPrice: overrides.entryPrice ?? 100,
	quantity: overrides.quantity ?? 10,
	exitAt: overrides.exitAt ?? null,
	exitPrice: overrides.exitPrice ?? null,
	fees: overrides.fees ?? null,
	setup: overrides.setup ?? null,
	notes: overrides.notes ?? null,
	realizedPnl: overrides.realizedPnl ?? null,
	createdAt: overrides.createdAt ?? '2026-04-01T10:00:00Z',
	updatedAt: overrides.updatedAt ?? '2026-04-01T10:00:00Z',
});

describe('Journal', () => {
	let fixture: ComponentFixture<Journal>;
	let component: Journal;
	let http: HttpTestingController;
	let googleStub: GoogleIdentityServiceStub;

	const tradesUrl = `${environment.apiBaseUrl}/api/trades`;
	const authUrl = `${environment.apiBaseUrl}/api/auth/google`;

	function configure(): void {
		googleStub = new GoogleIdentityServiceStub();
		TestBed.configureTestingModule({
			imports: [Journal],
			providers: [
				provideHttpClient(withInterceptors([authInterceptor])),
				provideHttpClientTesting(),
				{ provide: GoogleIdentityService, useValue: googleStub },
			],
		});
	}

	function createFixture(): void {
		fixture = TestBed.createComponent(Journal);
		component = fixture.componentInstance;
		http = TestBed.inject(HttpTestingController);
	}

	beforeEach(() => {
		localStorage.clear();
		TestBed.resetTestingModule();
		configure();
	});

	afterEach(() => {
		if (http) {
			http.verify();
		}
		localStorage.clear();
	});

	it('renders the Google sign-in container when signed out', () => {
		createFixture();
		fixture.detectChanges();
		const button = fixture.nativeElement.querySelector('[data-testid="google-button"]');
		expect(button).toBeTruthy();
		expect(googleStub.renderButton).toHaveBeenCalled();
	});

	it('does not show the trade form when signed out', () => {
		createFixture();
		fixture.detectChanges();
		const symbolInput = fixture.nativeElement.querySelector('[data-testid="symbol-input"]');
		expect(symbolInput).toBeFalsy();
	});

	it('persists token after Google sign-in and refreshes trades', async () => {
		createFixture();
		fixture.detectChanges();

		expect(googleStub.lastCallback).not.toBeNull();
		googleStub.lastCallback!('fake-id-token');

		const authReq = http.expectOne(authUrl);
		expect(authReq.request.body).toEqual({ idToken: 'fake-id-token' });
		authReq.flush({
			token: 'app-jwt',
			expiresAt: '2026-04-29T00:00:00Z',
			user: { id: 'u1', email: 'alice@example.com', displayName: 'Alice' },
		});

		await fixture.whenStable();

		const listReq = http.expectOne(tradesUrl);
		expect(listReq.request.headers.get('Authorization')).toBe('Bearer app-jwt');
		listReq.flush([]);

		await fixture.whenStable();
		fixture.detectChanges();

		const auth = TestBed.inject(AuthService);
		expect(auth.isAuthenticated()).toBe(true);
		expect(localStorage.getItem('trade-journal.token')).toBe('app-jwt');
	});

	it('disables submit until required fields are populated', () => {
		seedAuth();
		createFixture();
		fixture.detectChanges();
		flushInitialList([]);
		fixture.detectChanges();

		const submit = fixture.nativeElement.querySelector(
			'[data-testid="submit-trade"]',
		) as HTMLButtonElement;
		expect(submit.disabled).toBe(true);

		component.tradeForm.patchValue({
			symbol: 'AAPL',
			entryPrice: 100,
			quantity: 5,
		});
		fixture.detectChanges();
		expect(submit.disabled).toBe(false);
	});

	it('refreshes the list after creating a trade', async () => {
		seedAuth();
		createFixture();
		fixture.detectChanges();
		flushInitialList([]);
		fixture.detectChanges();

		component.tradeForm.patchValue({
			symbol: 'msft',
			entryPrice: 200,
			quantity: 5,
		});

		const submitPromise = component.submit();

		const create = http.expectOne(tradesUrl);
		expect(create.request.method).toBe('POST');
		expect(create.request.body.symbol).toBe('MSFT');
		create.flush(sampleTrade({ id: 't-msft', symbol: 'MSFT', entryPrice: 200, quantity: 5 }));

		await fixture.whenStable();

		const refresh = http.expectOne(tradesUrl);
		expect(refresh.request.method).toBe('GET');
		refresh.flush([sampleTrade({ id: 't-msft', symbol: 'MSFT', entryPrice: 200, quantity: 5 })]);

		await submitPromise;
		fixture.detectChanges();

		const rows = fixture.nativeElement.querySelectorAll('[data-testid="trades-table"] tbody tr');
		expect(rows.length).toBeGreaterThanOrEqual(1);
	});

	it('updates status and realized P&L when closing a trade', async () => {
		seedAuth();
		createFixture();
		fixture.detectChanges();

		const openTrade = sampleTrade({ id: 't-open', symbol: 'AAPL', status: 'Open' });
		flushInitialList([openTrade]);
		fixture.detectChanges();

		component.startClose(openTrade);
		component.updateClose('exitPrice', 110);
		component.updateClose('exitAt', '2026-04-02T10:00');

		const closePromise = component.confirmClose();

		const closeReq = http.expectOne(`${tradesUrl}/t-open/close`);
		expect(closeReq.request.method).toBe('POST');
		expect(closeReq.request.body.exitPrice).toBe(110);
		closeReq.flush(
			sampleTrade({
				id: 't-open',
				symbol: 'AAPL',
				status: 'Closed',
				exitAt: '2026-04-02T10:00:00Z',
				exitPrice: 110,
				realizedPnl: 100,
			}),
		);

		await fixture.whenStable();

		const refresh = http.expectOne(tradesUrl);
		refresh.flush([
			sampleTrade({
				id: 't-open',
				symbol: 'AAPL',
				status: 'Closed',
				exitAt: '2026-04-02T10:00:00Z',
				exitPrice: 110,
				realizedPnl: 100,
			}),
		]);

		await closePromise;
		fixture.detectChanges();

		const pnl = fixture.nativeElement.querySelector('[data-testid="trade-pnl-t-open"]');
		expect(pnl?.textContent).toContain('100');
	});

	function seedAuth(): void {
		localStorage.setItem('trade-journal.token', 'app-jwt');
		localStorage.setItem(
			'trade-journal.user',
			JSON.stringify({
				id: 'u1',
				email: 'alice@example.com',
				displayName: 'Alice',
			}),
		);
	}

	function flushInitialList(trades: Trade[]): void {
		const req = http.expectOne(tradesUrl);
		req.flush(trades);
	}
});
