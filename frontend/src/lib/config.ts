declare global {
  interface Window {
    __APP_CONFIG__?: { appBaseUrl?: string; storeId?: string }
  }
}

/** The seeded store (fixed UUIDv7). The landing page loads it. */
export const DEFAULT_STORE_ID = '0199f0a0-0000-7000-8000-000000000001'

function runtime() {
  return (typeof window !== 'undefined' && window.__APP_CONFIG__) || {}
}

/** Public base URL used for QR codes and shareable links (APP_BASE_URL). Defaults to the current origin. */
export function appBaseUrl(): string {
  const configured = runtime().appBaseUrl || import.meta.env.VITE_APP_BASE_URL || window.location.origin
  return configured.replace(/\/+$/, '')
}

export function storeId(): string {
  return runtime().storeId || import.meta.env.VITE_STORE_ID || DEFAULT_STORE_ID
}

export function registrationUrl(eventId: string): string {
  return `${appBaseUrl()}/registration/${eventId}`
}
