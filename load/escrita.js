import http from 'k6/http';
import { check } from 'k6';
import { Counter } from 'k6/metrics';

const RATE = parseInt(__ENV.RATE || '200');
const DURACAO = __ENV.DURACAO || '30s';
const CONTAS = parseInt(__ENV.CONTAS || '200');
const HOT = parseFloat(__ENV.HOT || '0');
const LANC = __ENV.LANC || 'http://lancamentos-api:8080';
const KC = __ENV.KC || 'http://keycloak:8080';

const aceitos = new Counter('lancamentos_aceitos');

export const options = {
  scenarios: {
    escrita: {
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
    http_req_duration: ['p(95)<300'],
  },
};

export function setup() {
  const res = http.post(
    `${KC}/realms/Verx/protocol/openid-connect/token`,
    { client_id: 'verx-api', grant_type: 'password', username: 'carga.teste', password: 'dev-only-carga' });
  const token = res.json('access_token');
  const headers = { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' };
  const rodada = `${Date.now()}`;
  for (let i = 1; i <= CONTAS; i += 50) {
    const lote = [];
    for (let j = i; j < i + 50 && j <= CONTAS; j++) {
      lote.push(['POST', `${LANC}/api/contas/${idConta(j)}/lancamentos`,
        JSON.stringify({ tipo: 'Credito', valor: 1000000 }),
        { headers: { ...headers, 'Idempotency-Key': `carga-inicial-${rodada}-${j}` } }]);
    }
    http.batch(lote);
  }
  return { token };
}

function idConta(i) {
  return '00000000-0000-0000-0000-' + String(i).padStart(12, '0');
}

function conta() {
  return idConta(Math.random() < HOT ? 1 : 1 + Math.floor(Math.random() * CONTAS));
}

export default function (dados) {
  const id = conta();
  const tipo = Math.random() < 0.6 ? 'Credito' : 'Debito';
  const valor = (1 + Math.floor(Math.random() * 100000)) / 100;
  const res = http.post(
    `${LANC}/api/contas/${id}/lancamentos`,
    JSON.stringify({ tipo, valor }),
    {
      headers: {
        Authorization: `Bearer ${dados.token}`,
        'Content-Type': 'application/json',
        'Idempotency-Key': `${__VU}-${__ITER}-${Date.now()}-${Math.random()}`,
      },
    });
  const ok = check(res, { 'criado (201)': (r) => r.status === 201 });
  if (ok) aceitos.add(1);
}
