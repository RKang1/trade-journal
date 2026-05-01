export type TradeSide = 'Long' | 'Short';
export type TradeStatus = 'Open' | 'Closed';

export interface UserProfile {
	id: string;
	email: string;
	displayName: string;
}

export interface AuthResponse {
	token: string;
	expiresAt: string;
	user: UserProfile;
}

export interface Trade {
	id: string;
	symbol: string;
	side: TradeSide;
	status: TradeStatus;
	entryAt: string;
	entryPrice: number;
	quantity: number;
	exitAt: string | null;
	exitPrice: number | null;
	fees: number | null;
	setup: string | null;
	notes: string | null;
	realizedPnl: number | null;
	createdAt: string;
	updatedAt: string;
}

export interface CreateTradeRequest {
	symbol: string;
	side: TradeSide;
	entryAt: string;
	entryPrice: number;
	quantity: number;
	fees: number | null;
	setup: string | null;
	notes: string | null;
}

export interface UpdateTradeRequest extends CreateTradeRequest {}

export interface CloseTradeRequest {
	exitAt: string;
	exitPrice: number;
	fees: number | null;
}

export interface ApiToken {
	id: string;
	name: string;
	prefix: string;
	createdAt: string;
	lastUsedAt: string | null;
}

export interface CreateApiTokenRequest {
	name: string;
}

export interface CreateApiTokenResponse {
	token: string;
	details: ApiToken;
}
