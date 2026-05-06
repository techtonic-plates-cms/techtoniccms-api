/**
 * K6 Benchmark 4.2 — Query com e sem Filtro Dinâmico
 * Mede o impacto de filtros em campos JSONB sobre o throughput.
 */

import http from 'k6/http';
import { check } from 'k6';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5095';
const API_KEY = __ENV.API_KEY || '';

export const options = {
  scenarios: {
    without_filter: {
      executor: 'constant-vus',
      vus: 50,
      duration: '2m',
      exec: 'withoutFilter',
    },
    with_filter: {
      executor: 'constant-vus',
      vus: 50,
      duration: '2m',
      exec: 'withFilter',
    },
  },
  thresholds: {
    http_req_duration: ['p(95)<500'],
    http_req_failed: ['rate<0.01'],
  },
};

function headers() {
  const h = { 'Content-Type': 'application/json' };
  if (API_KEY) h['X-API-Key'] = API_KEY;
  return h;
}

export function withoutFilter() {
  const query = JSON.stringify({
    query: `
      query {
        blogPosts(first: 10) {
          nodes {
            id
            name
            slug
            data {
              title
            }
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

export function withFilter() {
  const query = JSON.stringify({
    query: `
      query {
        blogPosts(
          where: { data: { title: { contains: "GraphQL" } } }
          first: 10
        ) {
          nodes {
            id
            name
            slug
            data {
              title
            }
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
