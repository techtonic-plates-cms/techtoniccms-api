/**
 * K6 Benchmark 4.1 — Throughput Geral da API GraphQL
 * Mede RPS sustentado e latência sob concorrência real.
 */

import http from 'k6/http';
import { check } from 'k6';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5095';
const API_KEY = __ENV.API_KEY || ''; // Optional: use API key for auth

export const options = {
  stages: [
    { duration: '30s', target: 10 },
    { duration: '1m',  target: 50 },
    { duration: '30s', target: 100 },
    { duration: '30s', target: 0 },
  ],
  thresholds: {
    http_req_duration: ['p(95)<500'],
    http_req_failed: ['rate<0.01'],
  },
};

function headers() {
  const h = {
    'Content-Type': 'application/json',
  };
  if (API_KEY) {
    h['X-API-Key'] = API_KEY;
  }
  return h;
}

export default function () {
  const query = JSON.stringify({
    query: `
      query {
        collections(first: 10) {
          nodes {
            id
            name
            slug
          }
        }
      }
    `
  });

  const res = http.post(`${BASE_URL}/graphql`, query, { headers: headers() });

  check(res, {
    'status is 200': (r) => r.status === 200,
    'no errors': (r) => {
      const body = r.json();
      return body && !body.errors;
    },
  });
}
