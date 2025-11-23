// K6 Authentication Helper
// Handles JWT token authentication for load tests

import http from 'k6/http';
import { check } from 'k6';
import { API_BASE, AUTH, commonHeaders } from './config.js';

// Cache for JWT tokens to avoid repeated authentication
let tokenCache = {};

/**
 * Authenticate and get JWT token
 * @param {string} role - Role to authenticate as ('admin', 'deployer', or 'viewer')
 * @returns {string} JWT token
 */
export function getAuthToken(role = 'deployer') {
  // Return cached token if available
  if (tokenCache[role]) {
    return tokenCache[role];
  }

  const credentials = AUTH[role.toUpperCase()];
  if (!credentials) {
    throw new Error(`Invalid role: ${role}`);
  }

  const loginUrl = `${API_BASE}/authentication/login`;
  const payload = JSON.stringify(credentials);

  const response = http.post(loginUrl, payload, {
    headers: commonHeaders
  });

  const success = check(response, {
    'authentication successful': (r) => r.status === 200,
    'token received': (r) => r.json('token') !== undefined
  });

  if (!success) {
    console.error(`Authentication failed for ${role}: ${response.status} ${response.body}`);
    return null;
  }

  const token = response.json('token');
  tokenCache[role] = token;

  return token;
}

/**
 * Get authorization header with JWT token
 * @param {string} role - Role to get token for
 * @returns {object} Headers object with Authorization
 */
export function getAuthHeaders(role = 'deployer') {
  const token = getAuthToken(role);
  if (!token) {
    return commonHeaders;
  }

  return {
    ...commonHeaders,
    'Authorization': `Bearer ${token}`
  };
}

/**
 * Clear cached tokens (useful for setup/teardown)
 */
export function clearTokenCache() {
  tokenCache = {};
}
