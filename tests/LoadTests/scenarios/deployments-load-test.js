// K6 Load Test: Deployment Endpoint
// Tests the deployment creation endpoint under sustained load

import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate, Trend } from 'k6/metrics';
import { API_BASE, SLA, thresholds, randomModule, randomVersion, randomEnvironment, randomStrategy } from '../lib/config.js';
import { getAuthHeaders } from '../lib/auth.js';

// Custom metrics
const deploymentCreationRate = new Rate('deployment_creation_success');
const deploymentLatency = new Trend('deployment_creation_latency');

// Load test configuration
export const options = {
  scenarios: {
    // Sustained load: 100 req/s for 10 minutes
    sustained_load: {
      executor: 'constant-arrival-rate',
      rate: 100,
      timeUnit: '1s',
      duration: '10m',
      preAllocatedVUs: 50,
      maxVUs: 200,
    },
  },
  thresholds: {
    ...thresholds,
    'deployment_creation_success': ['rate>0.99'],
    'deployment_creation_latency': ['p(95)<500', 'p(99)<1000'],
  },
};

/**
 * Create deployment request payload
 */
function createDeploymentPayload() {
  return {
    moduleName: randomModule(),
    moduleVersion: randomVersion(),
    targetEnvironment: randomEnvironment(),
    deploymentStrategy: randomStrategy(),
    requireApproval: false  // Skip approval for load testing
  };
}

/**
 * Main test function
 */
export default function () {
  const headers = getAuthHeaders('deployer');
  const payload = JSON.stringify(createDeploymentPayload());
  const url = `${API_BASE}/deployments`;

  const startTime = new Date().getTime();
  const response = http.post(url, payload, { headers });
  const duration = new Date().getTime() - startTime;

  // Record metrics
  deploymentLatency.add(duration);

  const success = check(response, {
    'status is 202': (r) => r.status === 202,
    'has executionId': (r) => r.json('executionId') !== undefined,
    'response time < 500ms': (r) => r.timings.duration < 500,
  });

  deploymentCreationRate.add(success);

  if (!success) {
    console.error(`Deployment creation failed: ${response.status} ${response.body}`);
  }

  // Small think time
  sleep(0.1);
}

/**
 * Setup function (runs once at start)
 */
export function setup() {
  console.log('=== Deployment Load Test Starting ===');
  console.log(`Target: ${API_BASE}/deployments`);
  console.log('Scenario: Sustained load (100 req/s for 10 minutes)');
  console.log('SLA: p95 < 500ms, p99 < 1000ms, success rate > 99%');
}

/**
 * Teardown function (runs once at end)
 */
export function teardown(data) {
  console.log('=== Deployment Load Test Complete ===');
}

/**
 * Handle summary (custom reporting)
 */
export function handleSummary(data) {
  return {
    'stdout': textSummary(data, { indent: '  ', enableColors: true }),
    'deployment-load-test-results.json': JSON.stringify(data),
  };
}

function textSummary(data, options) {
  const indent = options.indent || '';
  const colors = options.enableColors;

  let summary = '\n';
  summary += `${indent}=== Deployment Load Test Results ===\n\n`;

  const metrics = data.metrics;

  // HTTP request metrics
  if (metrics.http_reqs) {
    summary += `${indent}Total Requests: ${metrics.http_reqs.values.count}\n`;
    summary += `${indent}Request Rate: ${metrics.http_reqs.values.rate.toFixed(2)}/s\n\n`;
  }

  // Latency metrics
  if (metrics.http_req_duration) {
    const duration = metrics.http_req_duration.values;
    summary += `${indent}Response Time:\n`;
    summary += `${indent}  avg: ${duration.avg.toFixed(2)}ms\n`;
    summary += `${indent}  min: ${duration.min.toFixed(2)}ms\n`;
    summary += `${indent}  max: ${duration.max.toFixed(2)}ms\n`;
    summary += `${indent}  p50: ${duration['p(50)'].toFixed(2)}ms\n`;
    summary += `${indent}  p95: ${duration['p(95)'].toFixed(2)}ms ${duration['p(95)'] < SLA.p95_threshold ? '✓' : '✗'}\n`;
    summary += `${indent}  p99: ${duration['p(99)'].toFixed(2)}ms ${duration['p(99)'] < SLA.p99_threshold ? '✓' : '✗'}\n\n`;
  }

  // Success rate
  if (metrics.http_req_failed) {
    const errorRate = metrics.http_req_failed.values.rate;
    const successRate = 1 - errorRate;
    summary += `${indent}Success Rate: ${(successRate * 100).toFixed(2)}% ${successRate >= SLA.success_rate ? '✓' : '✗'}\n`;
    summary += `${indent}Error Rate: ${(errorRate * 100).toFixed(4)}%\n\n`;
  }

  // Custom metrics
  if (metrics.deployment_creation_success) {
    summary += `${indent}Deployment Creation Success: ${(metrics.deployment_creation_success.values.rate * 100).toFixed(2)}%\n`;
  }

  // VU metrics
  if (metrics.vus) {
    summary += `${indent}\nVirtual Users: ${metrics.vus.values.value}\n`;
  }

  return summary;
}
