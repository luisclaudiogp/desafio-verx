#!/usr/bin/env bash
# Demonstracao das corridas que o upsert resolve (ADR-0012 e ADR-0015).
# Requer o servico "postgres" do docker compose em execucao.  Uso:  bash docs/demos/upsert-demo.sh
set -u
cd "$(dirname "$0")/../.."

PG="docker compose exec -T postgres psql -U postgres -q -P pager=off"
BD="demo_upsert"

preparar() {
  $PG -c "DROP DATABASE IF EXISTS $BD" -c "CREATE DATABASE $BD" >/dev/null
  $PG -d $BD -c "CREATE TABLE saldo (conta int PRIMARY KEY, total numeric NOT NULL)" \
             -c "CREATE TABLE posicao (conta int PRIMARY KEY, saldo numeric NOT NULL)" \
             -c "CREATE TABLE movimento (id int PRIMARY KEY)" >/dev/null
}

sessao() { printf "%b" "$1" | $PG -d $BD; }

titulo() { printf "\n\n=============== %s ===============\n" "$1"; }

preparar

titulo "1) PERDA DE ATUALIZACAO: ler, somar no codigo e gravar (saldo inicial 100; A soma +10, B soma +5; esperado 115)"
$PG -d $BD -c "INSERT INTO saldo VALUES (1, 100)" >/dev/null
sessao 'BEGIN;\nSELECT total AS "A leu" FROM saldo WHERE conta = 1;\nSELECT pg_sleep(2);\nUPDATE saldo SET total = 110 WHERE conta = 1;\nCOMMIT;\n' &
sessao 'BEGIN;\nSELECT total AS "B leu" FROM saldo WHERE conta = 1;\nSELECT pg_sleep(1);\nUPDATE saldo SET total = 105 WHERE conta = 1;\nCOMMIT;\n' &
wait
$PG -d $BD -c "SELECT total AS \"resultado final (esperado 115)\" FROM saldo WHERE conta = 1"

titulo "2) SOMA FEITA NO BANCO: saldo = saldo + delta (mesmo cenario; B espera o A e usa o valor ja atualizado)"
$PG -d $BD -c "UPDATE saldo SET total = 100 WHERE conta = 1" >/dev/null
sessao 'BEGIN;\nUPDATE saldo SET total = total + 10 WHERE conta = 1;\nSELECT pg_sleep(2);\nCOMMIT;\n' &
sleep 0.5
sessao 'BEGIN;\n\\echo B vai atualizar a mesma linha e deve ESPERAR o A...\nUPDATE saldo SET total = total + 5 WHERE conta = 1;\nCOMMIT;\n' &
wait
$PG -d $BD -c "SELECT total AS \"resultado final (esperado 115)\" FROM saldo WHERE conta = 1"

titulo "3) VERIFICAR E DEPOIS INSERIR: corrida e transacao 'envenenada' (conta 1 ainda sem posicao)"
sessao 'BEGIN;\nSELECT count(*) AS "A encontrou linhas" FROM posicao WHERE conta = 1;\nSELECT pg_sleep(2);\nINSERT INTO posicao VALUES (1, 0);\nCOMMIT;\n' &
sessao 'BEGIN;\nSELECT count(*) AS "B encontrou linhas" FROM posicao WHERE conta = 1;\nSELECT pg_sleep(4);\nINSERT INTO posicao VALUES (1, 0);\n\\echo --- B tenta continuar a mesma transacao depois do erro:\nSELECT count(*) FROM posicao;\nCOMMIT;\n' &
wait

titulo "4) UPSERT 'ON CONFLICT DO NOTHING': mesma corrida, sem erro e sem transacao envenenada (conta 2)"
sessao 'BEGIN;\nSELECT count(*) AS "A encontrou linhas" FROM posicao WHERE conta = 2;\nSELECT pg_sleep(2);\nINSERT INTO posicao VALUES (2, 0) ON CONFLICT DO NOTHING;\nCOMMIT;\n' &
sessao 'BEGIN;\nSELECT count(*) AS "B encontrou linhas" FROM posicao WHERE conta = 2;\nSELECT pg_sleep(4);\nINSERT INTO posicao VALUES (2, 0) ON CONFLICT DO NOTHING;\n\\echo --- B continua a mesma transacao normalmente:\nSELECT count(*) AS "linhas da conta 2" FROM posicao WHERE conta = 2;\nCOMMIT;\n' &
wait

titulo "5) IDEMPOTENCIA: DO NOTHING + RETURNING devolve so o que foi realmente inserido (evento repetido nao soma)"
echo "-- 1a entrega do lote {10, 11, 12}:"
$PG -d $BD -c "INSERT INTO movimento VALUES (10),(11),(12) ON CONFLICT (id) DO NOTHING RETURNING id"
echo "-- 2a entrega (duplicada) do lote {11, 12, 13}: so o 13 e novo:"
$PG -d $BD -c "INSERT INTO movimento VALUES (11),(12),(13) ON CONFLICT (id) DO NOTHING RETURNING id"

titulo "6) UPSERT QUE ACUMULA: 'cria se nao existe, soma se existe' em uma instrucao, com duas sessoes ao mesmo tempo"
$PG -d $BD -c "DELETE FROM saldo" >/dev/null
for i in 1 2 3 4 5 6 7 8; do
  $PG -d $BD -c "INSERT INTO saldo VALUES (7, 10) ON CONFLICT (conta) DO UPDATE SET total = saldo.total + EXCLUDED.total" >/dev/null &
done
wait
$PG -d $BD -c "SELECT total AS \"8 sessoes somando +10 (esperado 80)\" FROM saldo WHERE conta = 7"

$PG -c "DROP DATABASE IF EXISTS $BD" >/dev/null
echo
echo "Fim da demonstracao (banco $BD removido)."
