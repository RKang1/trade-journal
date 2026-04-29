import { Injectable } from '@angular/core';
import { environment } from '../../environments/environment';

declare const google: {
	accounts: {
		id: {
			initialize: (config: {
				client_id: string;
				callback: (response: { credential: string }) => void;
				auto_select?: boolean;
				ux_mode?: 'popup' | 'redirect';
			}) => void;
			renderButton: (parent: HTMLElement, options: Record<string, unknown>) => void;
			prompt: () => void;
		};
	};
};

const SCRIPT_SRC = 'https://accounts.google.com/gsi/client';

@Injectable({ providedIn: 'root' })
export class GoogleIdentityService {
	private scriptPromise: Promise<void> | null = null;

	async load(): Promise<void> {
		if (this.scriptPromise) {
			return this.scriptPromise;
		}
		this.scriptPromise = new Promise((resolve, reject) => {
			const existing = document.querySelector<HTMLScriptElement>(`script[src="${SCRIPT_SRC}"]`);
			if (existing) {
				if ((existing as any).dataset['loaded'] === 'true') {
					resolve();
					return;
				}
				existing.addEventListener('load', () => resolve(), { once: true });
				existing.addEventListener(
					'error',
					() => reject(new Error('Failed to load Google Identity Services')),
					{ once: true },
				);
				return;
			}
			const script = document.createElement('script');
			script.src = SCRIPT_SRC;
			script.async = true;
			script.defer = true;
			script.addEventListener(
				'load',
				() => {
					(script as any).dataset['loaded'] = 'true';
					resolve();
				},
				{ once: true },
			);
			script.addEventListener(
				'error',
				() => reject(new Error('Failed to load Google Identity Services')),
				{ once: true },
			);
			document.head.appendChild(script);
		});
		return this.scriptPromise;
	}

	async renderButton(parent: HTMLElement, onCredential: (idToken: string) => void): Promise<void> {
		await this.load();
		google.accounts.id.initialize({
			client_id: environment.googleClientId,
			callback: (response) => onCredential(response.credential),
			ux_mode: 'popup',
		});
		google.accounts.id.renderButton(parent, {
			theme: 'outline',
			size: 'large',
			type: 'standard',
			shape: 'rectangular',
			text: 'signin_with',
			logo_alignment: 'left',
		});
	}
}
