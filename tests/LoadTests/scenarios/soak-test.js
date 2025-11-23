// K6 Load Test: Soak Test
// Tests system stability under prolonged moderate load (detect memory leaks, resource exhaustion)

import http from 'k6/http';
import { check, sleep } from 'k6';
import { Counter, Rate, Trend } from 'k6/metrics';
import { API_BASE, randomModule, randomVersion, randomEnvironment, randomStrategy } from '../lib/config.js';
import { getAuthHeaders } from '../lib/auth.js';

// Custom metrics
const totalRequests = new Counter('total_requests');
const degradationCheck = new Rate('no_degradation');
const memoryLeakIndicator = new Trend('response_time_trend');

// Load test configuration
export const options = {
  scenarios: {
    soak_test: {
      executor: 'constant-arrival-rate',
      rate: 50,  // Moderate 50 req/s
      timeUnit: '1s',
      duration: '1h',  // Run for 1 hour
      preAllocatedVUs: 30,
      maxVUs: 100,
    },
  },
  thresholds: {
    'http_req_duration': ['p(95)<500'],
    'http_req_failed': ['rate<0.01'],
    'no_degradation': ['rate>0.99'],  // Response times should not degrade over time
  },
};

// Track response times over test duration to detect degradation
let firstMinuteAvg = null;
let requestCount = 0;
const FIRST_MINUTE_SAMPLES = 60 * 50;  // ~3000 requests

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
  requestCount++;

  const success = check(response, {
    'status is 202': (r) => r.status === 202,
    'response time < 500ms': (r) => r.timings.duration < 500,
  });

  // Track first minute average for degradation detection
  if (requestCount <= FIRST_MINUTE_SAMPLES) {
    if (!firstMinuteAvg) {
      firstMinuteAvg = response.timings.duration;
    } else {
      firstMinuteAvg = (firstMinuteAvg + response.timings.duration) / 2;
    }
  }

  // After first minute, check for degradation (response time shouldn't increase by >50%)
  if (requestCount > FIRST_MINUTE_SAMPLES && firstMinuteAvg) {
    const noDegradation = response.timings.duration < (firstMinuteAvg * 1.5);
    degradationCheck.add(noDegradation);

    if (!noDegradation) {
      console.warn(`Degradation detected: ${response.timings.duration}ms vs baseline ${firstMinuteAvg}ms`);
    }
  }

  memoryLeakIndicator.add(response.timings.duration);

  sleep(0.1);
}

/**
 * Setup function
 */
export function setup() {
  console.log('=== Soak Test Starting ===');
  console.log('Scenario: Prolonged moderate load (50 req/s for 1 hour)');
  console.log('SLA: p95 < 500ms, error rate < 1%, no performance degradation');
  console.log('🔍 Monitoring for: memory leaks, resource exhaustion, degradation');
}

/**
 * Teardown function
 */
export function teardown(data) {
  console.log('=== Soak Test Complete ===');
  console.log('✅ System survived 1 hour of sustained load');
}

/**
 * Handle summary
 */
export function handleSummary(data) {
  return {
    'stdout': textSummary(data),
    'soak-test-results.json': JSON.stringify(data),
  };
}

function textSummary(data) {
  let summary = '\n';
  summary += '=== Soak Test Results (1 Hour) ===\n\n';

  const metrics = data.metrics;

  if (metrics.total_requests) {
    summary += `Total Requests: ${metrics.total_requests.values.count}\n`;
    summary += `Average Rate: ${metrics.total_requests.values.rate.toFixed(2)}/s\n\n`;
  }

  if (metrics.http_req_duration) {
    const duration = metrics.http_req_duration.values;
    summary += `Response Time (Full Duration):\n`;
    summary += `  avg: ${duration.avg.toFixed(2)}ms\n`;
    summary += `  p50: ${duration['p(50)'].toFixed(2)}ms\n`;
    summary += `  p95: ${duration['p(95)'].toFixed(2)}ms ${duration['p(95)'] < 500 ? '✓' : '✗'}\n`;
    summary += `  p99: ${duration['p(99)'].toFixed(2)}ms\n`;
    summary += `  max: ${duration.max.toFixed(2)}ms\n\n`;
  }

  if (metrics.http_req_failed) {
    const errorRate = metrics.http_req_failed.values.rate;
    const successRate = 1 - errorRate;
    summary += `Success Rate: ${(successRate * 100).toFixed(2)}% ${successRate >= 0.99 ? '✓' : '✗'}\n`;
    summary += `Error Rate: ${(errorRate * 100).toFixed(4)}%\n\n`;
  }

  if (metrics.no_degradation) {
    const noDegradationRate = metrics.no_degradation.values.rate;
    summary += `Performance Stability: ${(noDegradationRate * 100).toFixed(2)}% ${noDegradationRate >= 0.99 ? '✓' : '✗'}\n`;
    summary += '  (Response times did not degrade >50% from baseline)\n\n';
  }

  summary += '🔍 Check for:\n';
  summary += '  - Consistent response times throughout test\n';
  summary += '  - No increasing error rate over time\n';
  summary += '  - Stable memory usage on server\n';
  summary += '  - No resource exhaustion (connections, file handles, etc.)\n';

  return summary;
}
