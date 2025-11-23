// K6 Load Test: Stress Test
// Tests system limits by continuously increasing load until breaking point

import http from 'k6/http';
import { check, sleep } from 'k6';
import { Counter, Rate } from 'k6/metrics';
import { API_BASE, randomModule, randomVersion, randomEnvironment, randomStrategy } from '../lib/config.js';
import { getAuthHeaders } from '../lib/auth.js';

// Custom metrics
const totalRequests = new Counter('total_requests');
const systemHealthy = new Rate('system_healthy');

// Load test configuration
export const options = {
  scenarios: {
    stress_test: {
      executor: 'ramping-arrival-rate',
      startRate: 50,
      timeUnit: '1s',
      preAllocatedVUs: 50,
      maxVUs: 1000,
      stages: [
        { duration: '2m', target: 50 },    // Baseline
        { duration: '2m', target: 100 },   // 2x baseline
        { duration: '2m', target: 200 },   // 4x baseline
        { duration: '2m', target: 400 },   // 8x baseline
        { duration: '2m', target: 800 },   // 16x baseline
        { duration: '2m', target: 1200 },  // 24x baseline (likely breaking point)
        { duration: '2m', target: 1600 },  // 32x baseline (pushing beyond limits)
        { duration: '1m', target: 0 },     // Recovery
      ],
    },
  },
  thresholds: {
    // No strict thresholds - we expect failures at high load
    // The goal is to find the breaking point
  },
};

/**
 * Main test function
 */
export default function () {
  const headers = getAuthHeaders('deployer');
  const payload = JSON.stringify({
    moduleName: randomModule(),
    moduleVersion: randomVersion(),
    targetEnvironment: randomEnvironment(),
    deploymentStrategy: randomStrategy(),
    requireApproval: false
  });

  const url = `${API_BASE}/deployments`;
  const response = http.post(url, payload, { headers });

  totalRequests.add(1);

  // System is "healthy" if it responds with 2xx/4xx (not 5xx server errors)
  const healthy = check(response, {
    'not server error': (r) => r.status < 500,
    'responds within 5s': (r) => r.timings.duration < 5000,
  });

  systemHealthy.add(healthy);

  if (response.status >= 500) {
    console.warn(`Server error detected at load: ${response.status}`);
  }

  sleep(0.05);
}

/**
 * Setup function
 */
export function setup() {
  console.log('=== Stress Test Starting ===');
  console.log('Scenario: Increasing load until breaking point');
  console.log('Stages: 50 → 100 → 200 → 400 → 800 → 1200 → 1600 req/s');
  console.log('Goal: Find maximum sustainable throughput');
  console.log('');
  console.log('⚠️  Expected: System may fail at high loads');
  console.log('🎯 Success: Graceful degradation (rate limiting, not crashes)');
}

/**
 * Teardown function
 */
export function teardown(data) {
  console.log('=== Stress Test Complete ===');
}

/**
 * Handle summary
 */
export function handleSummary(data) {
  return {
    'stdout': textSummary(data),
    'stress-test-results.json': JSON.stringify(data),
  };
}

function textSummary(data) {
  let summary = '\n';
  summary += '=== Stress Test Results ===\n\n';

  const metrics = data.metrics;

  if (metrics.total_requests) {
    summary += `Total Requests: ${metrics.total_requests.values.count}\n`;
    summary += `Peak Rate: ${metrics.total_requests.values.rate.toFixed(2)}/s\n\n`;
  }

  if (metrics.http_req_duration) {
    const duration = metrics.http_req_duration.values;
    summary += `Response Time at Peak Load:\n`;
    summary += `  avg: ${duration.avg.toFixed(2)}ms\n`;
    summary += `  p50: ${duration['p(50)'].toFixed(2)}ms\n`;
    summary += `  p95: ${duration['p(95)'].toFixed(2)}ms\n`;
    summary += `  p99: ${duration['p(99)'].toFixed(2)}ms\n`;
    summary += `  max: ${(duration.max / 1000).toFixed(2)}s\n\n`;
  }

  if (metrics.http_req_failed) {
    const errorRate = metrics.http_req_failed.values.rate;
    const successRate = 1 - errorRate;
    summary += `Overall Success Rate: ${(successRate * 100).toFixed(2)}%\n`;
    summary += `Overall Error Rate: ${(errorRate * 100).toFixed(2)}%\n\n`;
  }

  if (metrics.system_healthy) {
    const healthRate = metrics.system_healthy.values.rate;
    summary += `System Health Rate: ${(healthRate * 100).toFixed(2)}%\n`;
    summary += '  (No 5xx errors, responds within 5s)\n\n';
  }

  // Analyze breaking point
  summary += '📊 Analysis:\n';
  summary += '  1. Review response times per stage (2m intervals)\n';
  summary += '  2. Identify load level where p95 > 1000ms\n';
  summary += '  3. Identify load level where error rate > 5%\n';
  summary += '  4. Breaking point = first sustained degradation\n\n';

  summary += '✅ Graceful degradation indicators:\n';
  summary += '  - 429 rate limiting responses (good)\n';
  summary += '  - Slow responses but no crashes (acceptable)\n';
  summary += '  - System recovers after load decreases (critical)\n\n';

  summary += '❌ Catastrophic failure indicators:\n';
  summary += '  - 5xx server errors (bad)\n';
  summary += '  - Connection timeouts (very bad)\n';
  summary += '  - System doesn\'t recover (critical issue)\n';

  return summary;
}
