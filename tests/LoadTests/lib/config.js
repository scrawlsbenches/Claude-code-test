// K6 Load Testing Configuration
// Shared configuration for all load testing scenarios

export const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';
export const API_VERSION = 'v1';
export const API_BASE = `${BASE_URL}/api/${API_VERSION}`;

// Authentication credentials (use demo credentials from system)
export const AUTH = {
  ADMIN: {
    username: 'admin',
    password: 'Admin123!'
  },
  DEPLOYER: {
    username: 'deployer',
    password: 'Deploy123!'
  },
  VIEWER: {
    username: 'viewer',
    password: 'Viewer123!'
  }
};

// Performance SLA targets
export const SLA = {
  p95_threshold: 500,  // 95th percentile < 500ms
  p99_threshold: 1000, // 99th percentile < 1000ms
  error_rate: 0.01,    // < 1% error rate
  success_rate: 0.99   // > 99% success rate
};

// Load test thresholds for k6
export const thresholds = {
  'http_req_duration': ['p(95)<500', 'p(99)<1000'],
  'http_req_failed': ['rate<0.01'],
  'http_reqs': ['rate>100'],
};

// Common HTTP headers
export const commonHeaders = {
  'Content-Type': 'application/json',
  'Accept': 'application/json'
};

// Test data generators
export function randomString(length = 10) {
  const chars = 'abcdefghijklmnopqrstuvwxyz0123456789';
  let result = '';
  for (let i = 0; i < length; i++) {
    result += chars.charAt(Math.floor(Math.random() * chars.length));
  }
  return result;
}

export function randomModule() {
  const modules = ['UserService', 'PaymentService', 'NotificationService', 'AnalyticsService', 'AuthService'];
  return modules[Math.floor(Math.random() * modules.length)];
}

export function randomVersion() {
  const major = Math.floor(Math.random() * 3) + 1;
  const minor = Math.floor(Math.random() * 10);
  const patch = Math.floor(Math.random() * 20);
  return `${major}.${minor}.${patch}`;
}

export function randomEnvironment() {
  const environments = ['Development', 'Staging', 'Production'];
  return environments[Math.floor(Math.random() * environments.length)];
}

export function randomStrategy() {
  const strategies = ['Direct', 'Rolling', 'BlueGreen', 'Canary'];
  return strategies[Math.floor(Math.random() * strategies.length)];
}
