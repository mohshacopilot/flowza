/**
 * The Gateway's BFF endpoints (plan 001 §9). The browser holds only the Gateway's cookie; login and logout are
 * full-page navigations, so the Gateway can run the OpenID Connect redirects.
 */
export const bffEndpoints = {
  user: '/bff/user',
  login: '/bff/login',
  logout: '/bff/logout',
} as const;

/** Full-page navigation, kept in one object so tests can replace it. */
export const browser = {
  assign(url: string): void {
    window.location.assign(url);
  },
};

/** Sends the browser to the login page; afterwards the Gateway returns to the page the user was on. */
export function redirectToLogin(): void {
  const here = `${window.location.pathname}${window.location.search}`;
  browser.assign(`${bffEndpoints.login}?returnUrl=${encodeURIComponent(here)}`);
}

/**
 * Logs out. A plain navigation is safe here: the session cookie is SameSite=Strict, so a link on another site
 * cannot log anyone out.
 */
export function logout(): void {
  browser.assign(bffEndpoints.logout);
}
