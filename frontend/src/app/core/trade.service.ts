import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { CloseTradeRequest, CreateTradeRequest, Trade, UpdateTradeRequest } from './api.types';

@Injectable({ providedIn: 'root' })
export class TradeService {
	private readonly http = inject(HttpClient);
	private readonly baseUrl = `${environment.apiBaseUrl}/api/trades`;

	list(): Observable<Trade[]> {
		return this.http.get<Trade[]>(this.baseUrl);
	}

	create(request: CreateTradeRequest): Observable<Trade> {
		return this.http.post<Trade>(this.baseUrl, request);
	}

	update(id: string, request: UpdateTradeRequest): Observable<Trade> {
		return this.http.put<Trade>(`${this.baseUrl}/${id}`, request);
	}

	close(id: string, request: CloseTradeRequest): Observable<Trade> {
		return this.http.post<Trade>(`${this.baseUrl}/${id}/close`, request);
	}
}
