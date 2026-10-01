# Vyracare API Finance

API responsável pelos lançamentos financeiros, boletos e indicadores mensais do dashboard.

## Endpoints

- `POST /api/finance/entries`
- `POST /api/finance/invoices`
- `PATCH /api/finance/invoices/{id}/status`
- `GET /api/finance/dashboard/summary?month=yyyy-MM`
- `GET /health`

O resumo considera lançamentos confirmados do mês, compara com o mês anterior e agrega todos os boletos pendentes. Datas são armazenadas em UTC e agrupadas usando `Finance:TimeZone`.

Swagger local: `http://localhost:5004/swagger`.
