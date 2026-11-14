declare global {
  interface Window {
    /** Optionally injected by the Electron preload script or a hosting page. */
    __PHARMACORE_API__?: string;
  }
}

export const API_BASE_URL: string = window.__PHARMACORE_API__ ?? 'http://localhost:5080';
