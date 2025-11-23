// K6 Load Test: Prometheus Metrics Endpoint
// Tests the /metrics endpoint under sustained load

import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate, Trend } from 'k6/metrics';
import { BASE_URL, SLA, thresholds } from '../lib/config.js';

// Custom metrics
const metricsSuccessRate = new Rate('metrics_fetch_success');
const metricsLatency = new Trend('metrics_fetch_latency');

// Load test configuration
export const options = {
  scenarios: {
    // High sustained load for metrics endpoint (scrapers + dashboards)
    sustained_load: {
      executor: 'constant-arrival-rate',
      rate: 200,  // 200 req/s (Prometheus scrapes + Grafana dashboards)
      timeUnit: '1s',
      duration: '5m',
      preAllocatedVUs: 20,
      maxVUs: 100,
    },
  },
  thresholds: {
    ...thresholds,
    'metrics_fetch_success': ['rate>0.999'],  // Very high success rate expected
    'metrics_fetch_latency': ['p(95)<100', 'p(99)<200'],  // Metrics should be fast
  },
};

/**
 * Main test function
 */
export default function () {
  const url = `${BASE_URL}/metrics`;

  const startTime = new Date().getTime();
  const response = http.get(url);
  const duration = new Date().getTime() - startTime;

  // Record metrics
  metricsLatency.add(duration);

  const success = check(response, {
    'status is 200': (r) => r.status === 200,
    'has prometheus format': (r) => r.body.includes('# HELP') && r.body.includes('# TYPE'),
    'has deployment metrics': (r) => r.body.includes('deployments_started_total'),
    'response time < 100ms': (r) => r.timings.duration < 100,
  });

  metricsSuccessRate.add(success);

  if (!success) {
    console.error(`Metrics fetch failed: ${response.status}`);
  }

  // Minimal think time (Prometheus scrapes are frequent)
  sleep(0.01);
}

/**
 * Setup function
 */
export function setup() {
  console.log('=== Metrics Endpoint Load Test Starting ===');
  console.log(`Target: ${BASE_URL}/metrics`);
  console.log('Scenario: High sustained load (200 req/s for 5 minutes)');
  console.log('SLA: p95 < 100ms, p99 < 200ms, success rate > 99.9%');
}

/**
 * Teardown function
 */
export function teardown(data) {
  console.log('=== Metrics Endpoint Load Test Complete ===');
}

/**
 * Handle summary
 */
export function handleSummary(data) {
  return {
    'stdout': textSummary(data),
    'metrics-load-test-results.json': JSON.stringify(data),
  };
}

function textSummary(data) {
  let summary = '\n';
  summary += '=== Metrics Endpoint Load Test Results ===\n\n';

  const metrics = data.metrics;

  if (metrics.http_reqs) {
    summary += `Total Requests: ${metrics.http_reqs.values.count}\n`;
    summary += `Request Rate: ${metrics.http_reqs.values.rate.toFixed(2)}/s\n\n`;
  }

  if (metrics.http_req_duration) {
    const duration = metrics.http_req_duration.values;
    summary += `Response Time:\n`;
    summary += `  avg: ${duration.avg.toFixed(2)}ms\n`;
    summary += `  p50: ${duration['p(50)'].toFixed(2)}ms\n`;
    summary += `  p95: ${duration['p(95)'].toFixed(2)}ms ${duration['p(95)'] < 100 ? '✓' : '✗'}\n`;
    summary += `  p99: ${duration['p(99)'].toFixed(2)}ms ${duration['p(99)'] < 200 ? '✓' : '✗'}\n\n`;
  }

  if (metrics.http_req_failed) {
    const errorRate = metrics.http_req_failed.values.rate;
    const successRate = 1 - errorRate;
    summary += `Success Rate: ${(successRate * 100).toFixed(3)}% ${successRate >= 0.999 ? '✓' : '✗'}\n`;
  }

  return summary;
}
