import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { AuthResponse, UserProfile } from './api.types';
import { appRuntimeConfig } from './runtime-config';

const TOKEN_KEY = 'trade-journal.token';
const USER_KEY = 'trade-journal.user';

@Injectable({ providedIn: 'root' })
export class AuthService {
	private readonly http = inject(HttpClient);
	private readonly baseUrl = appRuntimeConfig.apiBaseUrl;

	private readonly tokenSignal = signal<string | null>(this.readToken());
	private readonly userSignal = signal<UserProfile | null>(this.readUser());

	readonly token = this.tokenSignal.asReadonly();
	readonly user = this.userSignal.asReadonly();
	readonly isAuthenticated = computed(() => this.tokenSignal() !== null);

	signInWithGoogle(idToken: string): Observable<AuthResponse> {
		return this.http.post<AuthResponse>(`${this.baseUrl}/api/auth/google`, { idToken }).pipe(
			tap((response) => {
				this.persist(response.token, response.user);
			}),
		);
	}

	signOut(): void {
		localStorage.removeItem(TOKEN_KEY);
		localStorage.removeItem(USER_KEY);
		this.tokenSignal.set(null);
		this.userSignal.set(null);
	}

	private persist(token: string, user: UserProfile): void {
		localStorage.setItem(TOKEN_KEY, token);
		localStorage.setItem(USER_KEY, JSON.stringify(user));
		this.tokenSignal.set(token);
		this.userSignal.set(user);
	}

	private readToken(): string | null {
		if (typeof localStorage === 'undefined') return null;
		return localStorage.getItem(TOKEN_KEY);
	}

	private readUser(): UserProfile | null {
		if (typeof localStorage === 'undefined') return null;
		const raw = localStorage.getItem(USER_KEY);
		if (!raw) return null;
		try {
			return JSON.parse(raw) as UserProfile;
		} catch {
			return null;
		}
	}
}
