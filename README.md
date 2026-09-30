# Money Transfer API

A small REST API for viewing accounts, sending money between them and viewing transfer history. Built with .NET 10 (ASP.NET Core minimal APIs), PostgreSQL 17 and EF Core.

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Docker, with Docker Compose

## Quick start

```bash
# 1. Start PostgreSQL
docker compose up -d

# 2. Create the schema
dotnet run --project src/MoneyTransfer.Api -- migrate

# 3. Add the test data (safe to run more than once)
dotnet run --project src/MoneyTransfer.Api -- seed

# 4. Run the API (listens on http://localhost:5298)
dotnet run --project src/MoneyTransfer.Api

# 5. Run the tests (needs Docker; starts its own throwaway PostgreSQL)
dotnet test
```

To start again from an empty database: `docker compose down -v`, then steps 1 to 3.

Notes:
- The first `migrate` on an empty database logs one `fail` line while EF Core checks for its own history table. This is expected and harmless; the migrations are applied right after it.
- The database password in `docker-compose.yml` and `appsettings.Development.json` is committed on purpose: it only protects a local throwaway database. A real deployment would read it from environment variables or a secrets store.

## Test data

| Customer | API key | Account | Currency | Balance (cents) |
| --- | --- | --- | --- | --- |
| Alice | `alice-key` | `alice-eur` | EUR | 100000 |
| Alice | `alice-key` | `alice-usd` | USD | 50000 |
| Bob | `bob-key` | `bob-eur` | EUR | 25000 |

## API

Every request needs the customer's API key in the `X-Api-Key` header. All amounts are integers in minor units (cents). JSON uses snake_case.

| Method and path | Purpose |
| --- | --- |
| `GET /accounts` | The caller's own accounts |
| `POST /transfers` | Move money; requires an `Idempotency-Key` header |
| `GET /accounts/{accountId}/transfers?limit=20&cursor=...` | History of one of the caller's accounts, newest first |

### List accounts

```bash
curl -s http://localhost:5298/accounts -H "X-Api-Key: alice-key"
```

```json
{"data":[{"id":"alice-eur","currency":"EUR","balance":100000},{"id":"alice-usd","currency":"USD","balance":50000}]}
```

### Create a transfer

```bash
curl -i http://localhost:5298/transfers \
  -H "X-Api-Key: alice-key" \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: 7f3c2a90-1b4e-4c8d-9a61-2f5e8d0b3c11" \
  -d '{"source_account_id":"alice-eur","destination_account_id":"bob-eur","amount":2500,"currency":"EUR"}'
```

`201 Created`:

```json
{"id":1,"source_account_id":"alice-eur","destination_account_id":"bob-eur","amount":2500,"currency":"EUR","created_at":"2026-09-30T16:48:08.36591+00:00"}
```

Sending the same request again with the same key returns the same response, with the header `Idempotent-Replayed: true`, and moves no money. Reusing the key with a different body returns `409`.

### Transfer history

```bash
curl -s "http://localhost:5298/accounts/alice-eur/transfers?limit=2" -H "X-Api-Key: alice-key"
```

```json
{"data":[{"id":2,"direction":"incoming","source_account_id":"bob-eur","destination_account_id":"alice-eur","amount":50,"currency":"EUR","created_at":"..."},{"id":1,"direction":"outgoing","source_account_id":"alice-eur","destination_account_id":"bob-eur","amount":2500,"currency":"EUR","created_at":"..."}],"next_cursor":null}
```

Pass `next_cursor` as `?cursor=` to get the next page; it is `null` on the last page. `limit` defaults to 20, maximum 100.

### Errors

All errors use [RFC 9457 ProblemDetails](https://www.rfc-editor.org/rfc/rfc9457) (`application/problem+json`), with the standard `type`, `title`, `status` and `detail` fields and a `traceId` that matches the server log. Business errors add a machine-readable `code`; validation errors list problems per field in `errors`.

```json
{"status":422,"detail":"The source account does not have enough funds.","code":"insufficient_funds","traceId":"00-..."}
```

```json
{"status":400,"title":"One or more validation errors occurred.","errors":{"amount":["Amount is required."],"Idempotency-Key":["The Idempotency-Key header is required."]}}
```

(some standard fields omitted for brevity)

| Status | When | `code` |
| --- | --- | --- |
| 400 | Invalid input: missing or invalid field, missing `Idempotency-Key`, same source and destination, unreadable JSON, bad `limit` or `cursor` | — (see `errors`) |
| 401 | Missing or invalid API key | — |
| 404 | Account does not exist, or is not the caller's | `source_account_not_found`, `destination_account_not_found`, `account_not_found` |
| 409 | `Idempotency-Key` already used with a different request | `idempotency_key_reused` |
| 422 | Valid request that current data does not allow | `insufficient_funds`, `currency_mismatch` |
| 500 | Unexpected error: generic message, details only in the server log | — |

## Tests

`dotnet test` runs integration tests against a real PostgreSQL 17, started in Docker by [Testcontainers](https://dotnet.testcontainers.org/) and removed afterwards. Requests go through the full application (authentication, validation, services, database) using `WebApplicationFactory`; only the connection string is replaced. Before each test the tables are truncated and re-seeded, and balances are checked directly in the database.

| Test | Proves |
| --- | --- |
| `Successful_transfer_moves_money_and_records_the_transfer` | Debit, credit and transfer record |
| `Transfer_with_insufficient_funds_is_rejected_and_changes_nothing` | 422, balances unchanged |
| `Customer_cannot_send_money_from_another_customers_account` | 404, balances unchanged |
| `Same_request_and_idempotency_key_twice_moves_money_only_once` | Same transfer returned, money moves once |
| `Reusing_an_idempotency_key_with_a_different_body_returns_conflict` | 409, first transfer kept |
| `Same_request_sent_twice_at_the_same_time_moves_money_only_once` | Concurrent duplicates are safe |
| `Two_concurrent_transfers_exceeding_the_balance_cannot_make_it_negative` | One 201, one 422, balance never negative |
| `Opposite_transfers_at_the_same_time_all_succeed_without_deadlock` | 20 concurrent A→B / B→A transfers, no deadlock |
| `Pagination_returns_every_transfer_exactly_once_even_when_new_ones_arrive` | No missing or repeated records across pages |
| `Customer_cannot_see_the_history_of_an_account_they_do_not_own` | 404 |

## Project structure

```
src/MoneyTransfer.Api/
  Program.cs     startup, middleware pipeline, migrate/seed commands
  Auth/          API key hashing and authentication handler
  Data/          entities, AppDbContext, seed
  Migrations/    EF Core migrations
  Errors/        global exception handler
  Endpoints/     HTTP endpoints (thin: validate, call a service, map the result)
  Transfers/     validation, transfer service (locking, idempotency), history, cursors
tests/MoneyTransfer.Api.Tests/
```

## Decisions and Assumptions

### Where I departed from the brief

The example request uses `"amoun"`, which I treated as a typo for `amount`. The request's `currency` repeats what the accounts already know, so I kept it only as a safety check: if it doesn't match both accounts, the transfer is refused with 422. Alice appears twice in the test data, the second time without an API key, so I read it as one customer who owns two accounts. The brief doesn't say how the API key is sent. I used an `X-Api-Key` header, because a query parameter would end up in server and proxy logs.

I also filled two gaps. A transfer from an account to itself is rejected. A `limit` above 100 returns 400 rather than being quietly reduced, because a client that asks for 500 and silently gets 100 may believe it has everything.

Two status-code choices run through the API. When someone uses an account that isn't theirs, the answer is 404, exactly as if it didn't exist; a 403 would confirm that the account is real. And 400 means the request itself is wrong, which can be seen without looking at any data, while 422 means the request is fine but the current data doesn't allow it, such as insufficient funds.

### Keeping money correct under concurrency

The risk is two requests reading the same balance at the same moment, both deciding there is enough money, and both spending it. Each transfer therefore runs in one transaction that first locks both accounts with `SELECT ... FOR UPDATE`. A second request for the same account waits for the first to finish, then reads the balance the first one left behind. All the checks (ownership, currency, funds) happen after the lock, so they always see current data. The two accounts are always locked in the same order, by account id, which is what stops a transfer from A to B and one from B to A from deadlocking. The database constraints (`balance >= 0`, `amount > 0`, foreign keys) stay in place as a last line of defence.

I chose explicit locks over SERIALIZABLE isolation, which would need retry logic, and over optimistic concurrency, which keeps failing on a busy account.

### Idempotency

A client whose connection drops can't tell whether its transfer went through, so it needs to be able to retry safely. Each `Idempotency-Key` is stored in PostgreSQL, per customer, together with a hash of the request and the response that was sent. The key is written in the same transaction as the money movement, so the two can never disagree: if the transfer commits, the key commits with it, and if anything fails, both roll back.

The key is inserted before any work is done. If it already exists, the stored response is returned, or 409 if the request is different. If two identical requests arrive at the same moment, the second waits on the key's primary key until the first commits, then returns the first one's answer. "The same request" means the same validated fields, so a retry that formats its JSON differently still matches. Decisions the server actually made, including business failures such as insufficient funds, are stored so a retry gets the same answer. Validation errors and crashes are not, so the client can fix the request, or simply retry.

### Pagination

History uses keyset pagination: each page continues from the last transfer id the client saw. Offset pagination ("skip 20 rows") repeats or drops a record whenever a new transfer arrives between pages; a position in the data can't shift like that. It pages by id rather than `created_at`, because ids are unique and increasing while two transfers can share a timestamp. The cursor is opaque, so its format can change later without breaking clients.

### Security, errors and scope

API keys are stored only as SHA-256 hashes. That suits long random keys and allows a single indexed lookup; slow, salted hashes such as bcrypt are meant for passwords people choose. Every endpoint requires a key by default, and every error uses the standard ProblemDetails format, so stack traces and database errors never reach the client.

The structure is deliberately small: one project, folders by responsibility, thin endpoints and small services, with no repository layer. Migrations and seeding are explicit commands, so the database never changes on startup.

### Known limitations

Idempotency keys never expire; a clean-up job by date would be the next step. A transfer that commits at the exact moment a page is read can get a lower id than rows already returned, so it shows up on refresh rather than on a later page. Double-entry ledger entries, rate limiting, audit logging and key rotation were left out on purpose.
