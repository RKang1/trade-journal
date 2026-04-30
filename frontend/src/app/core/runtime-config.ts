import { environment } from '../../environments/environment';

type RuntimeConfig = {
	apiBaseUrl?: string;
	googleClientId?: string;
};

declare global {
	interface Window {
		__TRADE_JOURNAL_CONFIG__?: RuntimeConfig;
	}
}

function readRuntimeConfig(): RuntimeConfig {
	if (typeof window === 'undefined') {
		return {};
	}

	return window.__TRADE_JOURNAL_CONFIG__ ?? {};
}

const runtimeConfig = readRuntimeConfig();

export const appRuntimeConfig = {
	apiBaseUrl: runtimeConfig.apiBaseUrl ?? environment.apiBaseUrl,
	googleClientId: runtimeConfig.googleClientId ?? environment.googleClientId,
};
