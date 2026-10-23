import http from 'k6/http';
import { check } from 'k6';

const RATE = parseInt(__ENV.RATE || '500');
const DURACAO = __ENV.DURACAO || '30s';
const CONTAS = parseInt(__ENV.CONTAS || '200');
const CONS = __ENV.CONS || 'http://consolidado-api:8080';
const KC = __ENV.KC || 'http://keycloak:8080';

export const options = {
  scenarios: {
    leitura: {
      executor: 'constant-arrival-rate',
      rate: RATE,
      timeUnit: '1s',
      duration: DURACAO,
      preAllocatedVUs: Math.max(50, Math.ceil(RATE / 4)),
      maxVUs: Math.max(200, RATE),
    },
  },
  thresholds: {
    http_req_failed: ['rate<0.01'],
    http_req_duration: ['p(95)<150'],
  },
};

export function setup() {
  const res = http.post(
    `${KC}/realms/Verx/protocol/openid-connect/token`,
    { client_id: 'verx-api', grant_type: 'password', username: 'carga.teste', password: 'dev-only-carga' });
  return { token: res.json('access_token') };
}

export default function (dados) {
  const i = 1 + Math.floor(Math.random() * CONTAS);
  const id = '00000000-0000-0000-0000-' + String(i).padStart(12, '0');
  const hoje = new Date().toISOString().substring(0, 10);
  const url = Math.random() < 0.7
    ? `${CONS}/api/contas/${id}/saldo-diario/${hoje}`
    : `${CONS}/api/contas/${id}/saldo`;
  const res = http.get(url, { headers: { Authorization: `Bearer ${dados.token}` } });
  check(res, { 'ok (200)': (r) => r.status === 200 });
}
