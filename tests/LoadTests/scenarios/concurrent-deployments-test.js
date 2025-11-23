// K6 Load Test: Concurrent Deployments
// Tests the system's ability to handle multiple concurrent deployments

import http from 'k6/http';
import { check, sleep } from 'k6';
import { Counter, Rate, Trend } from 'k6/metrics';
import { API_BASE, thresholds, randomModule, randomVersion } from '../lib/config.js';
import { getAuthHeaders } from '../lib/auth.js';

// Custom metrics
const concurrentDeployments = new Counter('concurrent_deployments_total');
const deploymentSuccessRate = new Rate('concurrent_deployment_success');
const queueingTime = new Trend('deployment_queuing_time');

// Load test configuration
export const options = {
  scenarios: {
    // Ramp up concurrent deployments
    concurrent_deployments: {
      executor: 'ramping-vus',
      startVUs: 1,
      stages: [
        { duration: '30s', target: 10 },  // Ramp to 10 concurrent
        { duration: '1m', target: 10 },   // Hold at 10
        { duration: '30s', target: 20 },  // Ramp to 20 concurrent
        { duration: '1m', target: 20 },   // Hold at 20
        { duration: '30s', target: 50 },  // Ramp to 50 concurrent
        { duration: '2m', target: 50 },   // Hold at 50
        { duration: '30s', target: 0 },   // Ramp down
      ],
    },
  },
  thresholds: {
    ...thresholds,
    'concurrent_deployment_success': ['rate>0.95'],
    'deployment_queuing_time': ['p(95)<5000'],
  },
};

/**
 * Create deployment and wait for completion
 */
export default function () {
  const headers = getAuthHeaders('deployer');

  // Create deployment
  const deploymentPayload = JSON.stringify({
    moduleName: randomModule(),
    moduleVersion: randomVersion(),
    targetEnvironment: 'Development',  // Use Dev to avoid approval delays
    deploymentStrategy: 'Direct',      // Use Direct for speed
    requireApproval: false
  });

  const createUrl = `${API_BASE}/deployments`;
  const createTime = new Date().getTime();
  const createResponse = http.post(createUrl, deploymentPayload, { headers });

  const created = check(createResponse, {
    'deployment created': (r) => r.status === 202,
    'has executionId': (r) => r.json('executionId') !== undefined,
  });

  if (!created) {
    console.error(`Failed to create deployment: ${createResponse.status} ${createResponse.body}`);
    deploymentSuccessRate.add(false);
    return;
  }

  concurrentDeployments.add(1);
  const executionId = createResponse.json('executionId');

  // Poll for deployment completion (with timeout)
  const statusUrl = `${API_BASE}/deployments/${executionId}`;
  const maxPolls = 60;  // 60 seconds max
  let polls = 0;
  let completed = false;
  let finalStatus = null;

  while (polls < maxPolls && !completed) {
    sleep(1);  // Poll every second
    polls++;

    const statusResponse = http.get(statusUrl, { headers });
    if (statusResponse.status === 200) {
      const status = statusResponse.json('status');
      finalStatus = status;

      if (status === 'Succeeded' || status === 'Failed' || status === 'Cancelled') {
        completed = true;
        const completionTime = new Date().getTime();
        queueingTime.add(completionTime - createTime);
      }
    }
  }

  const success = check({ finalStatus, completed }, {
    'deployment completed': () => completed,
    'deployment succeeded': () => finalStatus === 'Succeeded',
  });

  deploymentSuccessRate.add(success);

  if (!success) {
    console.warn(`Deployment ${executionId} did not complete successfully: ${finalStatus || 'timeout'}`);
  }
}

/**
 * Setup function
 */
export function setup() {
  console.log('=== Concurrent Deployments Test Starting ===');
  console.log('Scenario: Ramping concurrent deployments (1 → 10 → 20 → 50)');
  console.log('SLA: success rate > 95%, queuing time p95 < 5s');
}

/**
 * Teardown function
 */
export function teardown(data) {
  console.log('=== Concurrent Deployments Test Complete ===');
}

/**
 * Handle summary
 */
export function handleSummary(data) {
  return {
    'stdout': textSummary(data),
    'concurrent-deployments-test-results.json': JSON.stringify(data),
  };
}

function textSummary(data) {
  let summary = '\n';
  summary += '=== Concurrent Deployments Test Results ===\n\n';

  const metrics = data.metrics;

  if (metrics.concurrent_deployments_total) {
    summary += `Total Deployments: ${metrics.concurrent_deployments_total.values.count}\n\n`;
  }

  if (metrics.deployment_queuing_time) {
    const queuing = metrics.deployment_queuing_time.values;
    summary += `Deployment Queuing Time:\n`;
    summary += `  avg: ${(queuing.avg / 1000).toFixed(2)}s\n`;
    summary += `  min: ${(queuing.min / 1000).toFixed(2)}s\n`;
    summary += `  max: ${(queuing.max / 1000).toFixed(2)}s\n`;
    summary += `  p95: ${(queuing['p(95)'] / 1000).toFixed(2)}s ${queuing['p(95)'] < 5000 ? '✓' : '✗'}\n\n`;
  }

  if (metrics.concurrent_deployment_success) {
    const successRate = metrics.concurrent_deployment_success.values.rate;
    summary += `Success Rate: ${(successRate * 100).toFixed(2)}% ${successRate >= 0.95 ? '✓' : '✗'}\n`;
  }

  return summary;
}
