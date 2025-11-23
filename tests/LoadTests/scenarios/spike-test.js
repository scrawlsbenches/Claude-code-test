// K6 Load Test: Spike Test
// Tests system behavior under sudden traffic spikes

import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate } from 'k6/metrics';
import { API_BASE, randomModule, randomVersion, randomEnvironment, randomStrategy } from '../lib/config.js';
import { getAuthHeaders } from '../lib/auth.js';

// Custom metrics
const spikeSuccessRate = new Rate('spike_success_rate');

// Load test configuration
export const options = {
  scenarios: {
    spike_test: {
      executor: 'ramping-arrival-rate',
      startRate: 10,
      timeUnit: '1s',
      preAllocatedVUs: 50,
      maxVUs: 500,
      stages: [
        { duration: '1m', target: 10 },    // Normal load
        { duration: '10s', target: 500 },  // Spike to 500 req/s
        { duration: '1m', target: 500 },   // Hold spike
        { duration: '10s', target: 10 },   // Drop back to normal
        { duration: '1m', target: 10 },    // Normal load
      ],
    },
  },
  thresholds: {
    'http_req_duration': ['p(95)<2000'],  // Relaxed during spike
    'http_req_failed': ['rate<0.05'],     // Allow 5% errors during spike
    'spike_success_rate': ['rate>0.90'],  // 90% success during spike
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

  const success = check(response, {
    'status is 202 or 429': (r) => r.status === 202 || r.status === 429,  // Accept rate limiting
    'not server error': (r) => r.status < 500,  // No server crashes
  });

  spikeSuccessRate.add(success);

  sleep(0.1);
}

/**
 * Setup function
 */
export function setup() {
  console.log('=== Spike Test Starting ===');
  console.log('Scenario: Sudden traffic spike (10 → 500 req/s → 10 req/s)');
  console.log('SLA: p95 < 2s, error rate < 5%, no server crashes');
}

/**
 * Handle summary
 */
export function handleSummary(data) {
  return {
    'stdout': textSummary(data),
    'spike-test-results.json': JSON.stringify(data),
  };
}

function textSummary(data) {
  let summary = '\n';
  summary += '=== Spike Test Results ===\n\n';

  const metrics = data.metrics;

  if (metrics.http_reqs) {
    summary += `Total Requests: ${metrics.http_reqs.values.count}\n`;
    summary += `Peak Rate: ${metrics.http_reqs.values.rate.toFixed(2)}/s\n\n`;
  }

  if (metrics.http_req_duration) {
    const duration = metrics.http_req_duration.values;
    summary += `Response Time During Spike:\n`;
    summary += `  avg: ${duration.avg.toFixed(2)}ms\n`;
    summary += `  p95: ${duration['p(95)'].toFixed(2)}ms ${duration['p(95)'] < 2000 ? '✓' : '✗'}\n`;
    summary += `  p99: ${duration['p(99)'].toFixed(2)}ms\n`;
    summary += `  max: ${duration.max.toFixed(2)}ms\n\n`;
  }

  if (metrics.http_req_failed) {
    const errorRate = metrics.http_req_failed.values.rate;
    summary += `Error Rate: ${(errorRate * 100).toFixed(2)}% ${errorRate < 0.05 ? '✓' : '✗'}\n`;
  }

  if (metrics.spike_success_rate) {
    const successRate = metrics.spike_success_rate.values.rate;
    summary += `Success Rate: ${(successRate * 100).toFixed(2)}% ${successRate >= 0.90 ? '✓' : '✗'}\n`;
  }

  summary += '\n💡 Note: 429 (rate limiting) responses are acceptable during spikes\n';

  return summary;
}
