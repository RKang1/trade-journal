import {
	AfterViewInit,
	ChangeDetectionStrategy,
	Component,
	ElementRef,
	ViewChild,
	computed,
	inject,
	signal,
} from '@angular/core';
import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { AuthService } from '../../core/auth.service';
import { GoogleIdentityService } from '../../core/google.service';
import { TradeService } from '../../core/trade.service';
import { CloseTradeRequest, CreateTradeRequest, Trade, TradeSide } from '../../core/api.types';

interface CloseFormState {
	tradeId: string;
	exitAt: string;
	exitPrice: number | null;
	fees: number | null;
}

@Component({
	selector: 'app-journal',
	standalone: true,
	imports: [CommonModule, ReactiveFormsModule, DatePipe, DecimalPipe],
	templateUrl: './journal.html',
	styleUrl: './journal.scss',
	changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Journal implements AfterViewInit {
	private readonly fb = inject(FormBuilder);
	private readonly auth = inject(AuthService);
	private readonly google = inject(GoogleIdentityService);
	private readonly trades = inject(TradeService);

	@ViewChild('googleButton') googleButtonRef?: ElementRef<HTMLDivElement>;

	readonly user = this.auth.user;
	readonly isAuthenticated = this.auth.isAuthenticated;
	readonly tradesList = signal<Trade[]>([]);
	readonly loadingTrades = signal(false);
	readonly errorMessage = signal<string | null>(null);
	readonly editingTradeId = signal<string | null>(null);
	readonly submitting = signal(false);

	readonly closingState = signal<CloseFormState | null>(null);

	readonly tradeForm: FormGroup = this.fb.group({
		symbol: ['', [Validators.required, Validators.maxLength(16)]],
		side: ['Long', Validators.required],
		entryAt: [this.toLocalInput(new Date()), Validators.required],
		entryPrice: [null as number | null, [Validators.required, Validators.min(0.0001)]],
		quantity: [null as number | null, [Validators.required, Validators.min(0.0001)]],
		fees: [null as number | null, [Validators.min(0)]],
		setup: [''],
		notes: [''],
	});

	readonly hasTrades = computed(() => this.tradesList().length > 0);

	ngAfterViewInit(): void {
		if (!this.isAuthenticated()) {
			this.renderGoogleButton();
		} else {
			void this.refresh();
		}
	}

	private renderGoogleButton(): void {
		const target = this.googleButtonRef?.nativeElement;
		if (!target) return;
		this.google
			.renderButton(target, (idToken) => this.onGoogleCredential(idToken))
			.catch((err) => this.errorMessage.set('Could not load Google Sign-In: ' + err));
	}

	private async onGoogleCredential(idToken: string): Promise<void> {
		this.errorMessage.set(null);
		try {
			await firstValueFrom(this.auth.signInWithGoogle(idToken));
			await this.refresh();
		} catch (err) {
			this.errorMessage.set(this.formatError(err));
		}
	}

	signOut(): void {
		this.auth.signOut();
		this.tradesList.set([]);
		this.cancelEdit();
		setTimeout(() => this.renderGoogleButton(), 0);
	}

	async refresh(): Promise<void> {
		if (!this.isAuthenticated()) return;
		this.loadingTrades.set(true);
		this.errorMessage.set(null);
		try {
			const list = await firstValueFrom(this.trades.list());
			this.tradesList.set(list);
		} catch (err) {
			this.errorMessage.set(this.formatError(err));
		} finally {
			this.loadingTrades.set(false);
		}
	}

	async submit(): Promise<void> {
		if (this.tradeForm.invalid) {
			this.tradeForm.markAllAsTouched();
			return;
		}
		this.submitting.set(true);
		this.errorMessage.set(null);
		const raw = this.tradeForm.value;
		const payload: CreateTradeRequest = {
			symbol: String(raw.symbol).trim().toUpperCase(),
			side: raw.side as TradeSide,
			entryAt: this.fromLocalInput(raw.entryAt),
			entryPrice: Number(raw.entryPrice),
			quantity: Number(raw.quantity),
			fees: this.toNullableNumber(raw.fees),
			setup: this.toNullableString(raw.setup),
			notes: this.toNullableString(raw.notes),
		};

		try {
			const editingId = this.editingTradeId();
			if (editingId) {
				await firstValueFrom(this.trades.update(editingId, payload));
			} else {
				await firstValueFrom(this.trades.create(payload));
			}
			this.resetForm();
			await this.refresh();
		} catch (err) {
			this.errorMessage.set(this.formatError(err));
		} finally {
			this.submitting.set(false);
		}
	}

	startEdit(trade: Trade): void {
		this.editingTradeId.set(trade.id);
		this.closingState.set(null);
		this.tradeForm.reset({
			symbol: trade.symbol,
			side: trade.side,
			entryAt: this.toLocalInput(new Date(trade.entryAt)),
			entryPrice: trade.entryPrice,
			quantity: trade.quantity,
			fees: trade.fees,
			setup: trade.setup ?? '',
			notes: trade.notes ?? '',
		});
	}

	cancelEdit(): void {
		this.editingTradeId.set(null);
		this.resetForm();
	}

	startClose(trade: Trade): void {
		this.editingTradeId.set(null);
		this.closingState.set({
			tradeId: trade.id,
			exitAt: this.toLocalInput(new Date()),
			exitPrice: null,
			fees: trade.fees ?? null,
		});
	}

	cancelClose(): void {
		this.closingState.set(null);
	}

	updateClose<K extends keyof CloseFormState>(key: K, value: CloseFormState[K]): void {
		const current = this.closingState();
		if (!current) return;
		this.closingState.set({ ...current, [key]: value });
	}

	async confirmClose(): Promise<void> {
		const state = this.closingState();
		if (!state) return;
		if (!state.exitPrice || state.exitPrice <= 0) {
			this.errorMessage.set('Exit price must be greater than zero.');
			return;
		}
		this.submitting.set(true);
		const payload: CloseTradeRequest = {
			exitAt: this.fromLocalInput(state.exitAt),
			exitPrice: Number(state.exitPrice),
			fees: this.toNullableNumber(state.fees),
		};
		try {
			await firstValueFrom(this.trades.close(state.tradeId, payload));
			this.closingState.set(null);
			await this.refresh();
		} catch (err) {
			this.errorMessage.set(this.formatError(err));
		} finally {
			this.submitting.set(false);
		}
	}

	private resetForm(): void {
		this.editingTradeId.set(null);
		this.tradeForm.reset({
			symbol: '',
			side: 'Long',
			entryAt: this.toLocalInput(new Date()),
			entryPrice: null,
			quantity: null,
			fees: null,
			setup: '',
			notes: '',
		});
	}

	private toLocalInput(date: Date): string {
		const pad = (n: number) => String(n).padStart(2, '0');
		return (
			date.getFullYear() +
			'-' +
			pad(date.getMonth() + 1) +
			'-' +
			pad(date.getDate()) +
			'T' +
			pad(date.getHours()) +
			':' +
			pad(date.getMinutes())
		);
	}

	private fromLocalInput(value: string): string {
		return new Date(value).toISOString();
	}

	private toNullableString(value: unknown): string | null {
		if (typeof value !== 'string') return null;
		const trimmed = value.trim();
		return trimmed.length === 0 ? null : trimmed;
	}

	private toNullableNumber(value: unknown): number | null {
		if (value === null || value === undefined || value === '') return null;
		const n = Number(value);
		return Number.isFinite(n) ? n : null;
	}

	private formatError(err: unknown): string {
		if (err && typeof err === 'object' && 'error' in err) {
			const body = (err as { error: unknown }).error;
			if (body && typeof body === 'object') {
				const detail = (body as Record<string, unknown>)['detail'];
				if (typeof detail === 'string') return detail;
				const errors = (body as Record<string, unknown>)['errors'];
				if (Array.isArray(errors) && errors.length > 0) return errors.join('\n');
			}
			const message = (err as { message?: unknown }).message;
			if (typeof message === 'string') return message;
		}
		return 'Something went wrong.';
	}
}
