import {
	ChangeDetectionStrategy,
	Component,
	OnInit,
	inject,
	signal,
} from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AuthService } from '../../core/auth.service';
import { TokenService } from '../../core/token.service';
import { ApiToken } from '../../core/api.types';

@Component({
	selector: 'app-tokens',
	standalone: true,
	imports: [CommonModule, ReactiveFormsModule, RouterLink, DatePipe],
	templateUrl: './tokens.html',
	styleUrl: './tokens.scss',
	changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Tokens implements OnInit {
	private readonly fb = inject(FormBuilder);
	private readonly auth = inject(AuthService);
	private readonly tokens = inject(TokenService);

	readonly user = this.auth.user;
	readonly isAuthenticated = this.auth.isAuthenticated;
	readonly tokensList = signal<ApiToken[]>([]);
	readonly loading = signal(false);
	readonly submitting = signal(false);
	readonly errorMessage = signal<string | null>(null);
	readonly newToken = signal<{ name: string; token: string } | null>(null);
	readonly copied = signal(false);

	readonly tokenForm: FormGroup = this.fb.group({
		name: ['', [Validators.required, Validators.maxLength(100)]],
	});

	ngOnInit(): void {
		if (this.isAuthenticated()) {
			void this.refresh();
		}
	}

	async refresh(): Promise<void> {
		if (!this.isAuthenticated()) return;
		this.loading.set(true);
		this.errorMessage.set(null);
		try {
			const list = await firstValueFrom(this.tokens.list());
			this.tokensList.set(list);
		} catch (err) {
			this.errorMessage.set(this.formatError(err));
		} finally {
			this.loading.set(false);
		}
	}

	async create(): Promise<void> {
		if (this.tokenForm.invalid) {
			this.tokenForm.markAllAsTouched();
			return;
		}
		this.submitting.set(true);
		this.errorMessage.set(null);
		this.newToken.set(null);
		this.copied.set(false);
		try {
			const name = String(this.tokenForm.value.name).trim();
			const created = await firstValueFrom(this.tokens.create({ name }));
			this.newToken.set({ name: created.details.name, token: created.token });
			this.tokenForm.reset({ name: '' });
			await this.refresh();
		} catch (err) {
			this.errorMessage.set(this.formatError(err));
		} finally {
			this.submitting.set(false);
		}
	}

	async revoke(token: ApiToken): Promise<void> {
		const confirmed =
			typeof window === 'undefined'
				? true
				: window.confirm(`Revoke token "${token.name}"? Any clients using it will stop working.`);
		if (!confirmed) return;
		this.errorMessage.set(null);
		try {
			await firstValueFrom(this.tokens.revoke(token.id));
			if (this.newToken()) this.newToken.set(null);
			await this.refresh();
		} catch (err) {
			this.errorMessage.set(this.formatError(err));
		}
	}

	dismissNewToken(): void {
		this.newToken.set(null);
		this.copied.set(false);
	}

	async copyNewToken(): Promise<void> {
		const value = this.newToken();
		if (!value) return;
		try {
			await navigator.clipboard.writeText(value.token);
			this.copied.set(true);
		} catch {
			this.copied.set(false);
		}
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
