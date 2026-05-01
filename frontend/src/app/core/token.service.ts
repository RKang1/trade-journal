import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApiToken, CreateApiTokenRequest, CreateApiTokenResponse } from './api.types';
import { appRuntimeConfig } from './runtime-config';

@Injectable({ providedIn: 'root' })
export class TokenService {
	private readonly http = inject(HttpClient);
	private readonly baseUrl = `${appRuntimeConfig.apiBaseUrl}/api/auth/tokens`;

	list(): Observable<ApiToken[]> {
		return this.http.get<ApiToken[]>(this.baseUrl);
	}

	create(request: CreateApiTokenRequest): Observable<CreateApiTokenResponse> {
		return this.http.post<CreateApiTokenResponse>(this.baseUrl, request);
	}

	revoke(id: string): Observable<void> {
		return this.http.delete<void>(`${this.baseUrl}/${id}`);
	}
}
