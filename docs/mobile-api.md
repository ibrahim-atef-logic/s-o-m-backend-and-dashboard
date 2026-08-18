# Mobile API inventory — salesorderapp.logictec.online

Base: `https://salesorderapp.logictec.online`
Prefix: `/api/v1`
Auth: `Authorization: Bearer <accessToken>` unless noted.
`company` on catalog/SO calls is the D365 DataArea (`mm`), never the login registry key `logic-trial`.

Envelope:

```json
{ "success": true, "data": ... }
{ "success": false, "error": { "code": "STABLE_CODE", "message": "..." } }
```

UTF-8 JSON. 401 without a token uses the same envelope (`UNAUTHORIZED`) except a few
anonymous 400s documented below.

QA login (as of 2026-08-18): `logic-trial` / personnel `1006` / password **`1234`**.
`12344` / `123` still works.

---

## Auth

### POST `/api/v1/auth/login` — anonymous

Body: `{ "company": "logic-trial", "personnelNumber": "1006", "password": "1234" }`

`company` is the **environment registry key**. `personnelNumber` is always a string.

200 `data`: `{ accessToken, refreshToken, user }` where `user` includes
`personnelNumber`, `workerRecId`, `name`, `userId`, `activationRecId`, `isActive`,
`userInfoEnable`, `company`, `companies[]`, `retailChannelId`, `channelType`,
`inventLocation`, `currency`, `defaultCustAccount`, `activeCompany`, `activeWarehouse`,
`needsWarehouseSelection`.

Errors: `400 VALIDATION_ERROR`, `400 AUTH_COMPANY_UNKNOWN`, `401 AUTH_FAILED`,
`403 ACCOUNT_DISABLED`, `502 DYNAMICS_*`.

```bash
curl -sS -X POST https://salesorderapp.logictec.online/api/v1/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"company":"logic-trial","personnelNumber":"1006","password":"1234"}'
```

### POST `/api/v1/auth/refresh` — anonymous

Body: `{ "refreshToken": "..." }`
200: same `data` shape as login (new tokens + user).
Errors: `401 UNAUTHORIZED`.

```bash
curl -sS -X POST https://salesorderapp.logictec.online/api/v1/auth/refresh \
  -H 'Content-Type: application/json' \
  -d '{"refreshToken":"<refreshToken>"}'
```

### POST `/api/v1/auth/logout` — anonymous

Body: `{ "refreshToken": "..." }` (optional). 200 `{ success: true, data: ... }`.

```bash
curl -sS -X POST https://salesorderapp.logictec.online/api/v1/auth/logout \
  -H 'Content-Type: application/json' \
  -d '{"refreshToken":"<refreshToken>"}'
```

### GET `/api/v1/auth/me` — Bearer

200 `data` = the same `user` object as login.

```bash
curl -sS https://salesorderapp.logictec.online/api/v1/auth/me \
  -H "Authorization: Bearer $TOKEN"
```

### POST `/api/v1/auth/change-password` — Bearer

Body: `{ "oldPassword": "...", "newPassword": "..." }`. Personnel from JWT.
200 `{ isSuccess, message, activationRecId }`.
`400 PASSWORD_CHANGE_FAILED` on wrong old password.

**Do not run this against QA `1006` / `12344`.** It writes to D365.

```bash
curl -sS -X POST https://salesorderapp.logictec.online/api/v1/auth/change-password \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d '{"oldPassword":"<current>","newPassword":"<new>"}'
```

---

## Catalog

All require Bearer + `company` DataArea. `403 FORBIDDEN_COMPANY` if the JWT cannot use that DataArea.

### GET `/api/v1/warehouses?company={dataArea}`

D365: `SiteAndWarehouseMobiles`, filtered server-side to `InventLocationType == Standard`.

200 `data[]`: `{ dataAreaId, inventLocationId, name, inventSiteId, inventLocationType }`.

```bash
curl -sS 'https://salesorderapp.logictec.online/api/v1/warehouses?company=mm' \
  -H "Authorization: Bearer $TOKEN"
```

### GET `/api/v1/customers?company={dataArea}&search={term}&top={n}`

D365: `CustomersV3`. `top` default 50, hard cap 200.
`search` is an **exact** `CustomerAccount` match (this F&O environment rejects
`contains`/`startswith` on strings). Empty search returns the first `top` rows.

200 `data[]`: `{ dataAreaId, customerAccount, name, customerGroupId, salesCurrencyCode, primaryPhone, addressCity }`.

```bash
curl -sS 'https://salesorderapp.logictec.online/api/v1/customers?company=mm&search=MMS021&top=50' \
  -H "Authorization: Bearer $TOKEN"
```

### GET `/api/v1/barcodes/{code}?company={dataArea}`

D365: `LogicRetailItemBarcodes_BI`.
200: `{ barcode, itemNumber, productName, productDescription, unitId, dataArea }`.
404 `BARCODE_NOT_FOUND`.

```bash
curl -sS 'https://salesorderapp.logictec.online/api/v1/barcodes/123456?company=mm' \
  -H "Authorization: Bearer $TOKEN"
```

### GET `/api/v1/pricing?item={item}&company={dataArea}&custAccount=&priceGroup=&unitId=`

D365: `LogicRetailSalesPriceAgreements_BI`, field `Price`.
200: `{ itemNumber, price, unitId, customerAccountNumber, priceCustomerGroupCode, dataArea }`.
404 `NO_PRICE`.

```bash
curl -sS 'https://salesorderapp.logictec.online/api/v1/pricing?item=BG410.003&company=mm&custAccount=MMS021' \
  -H "Authorization: Bearer $TOKEN"
```

### GET `/api/v1/inventory?item={item}&warehouse={wh}&company={dataArea}`

D365: `LogicRetailWarehouseOnHand_BI`.
200: `{ itemNumber, warehouseId, availableSalesQuantity, availableOnHandQuantity, unit, productName }`.
404 `NO_STOCK`.

```bash
curl -sS 'https://salesorderapp.logictec.online/api/v1/inventory?item=BG410.003&warehouse=MMS000WH&company=mm' \
  -H "Authorization: Bearer $TOKEN"
```

---

## Sales orders

### GET `/api/v1/sales-orders?company={dataArea}`

Open orders for the JWT sales taker. D365: `LogicRetailSalesOrdersHeaders_BI`.

200 `data[]`: `{ salesId, custAccount, salesName, workerSalesTaker, salesStatus, documentStatus, dataArea, priceGroupId, inventLocationId, inventSiteId, createdDateTime }`.

```bash
curl -sS 'https://salesorderapp.logictec.online/api/v1/sales-orders?company=mm' \
  -H "Authorization: Bearer $TOKEN"
```

### GET `/api/v1/sales-orders/{salesId}?company={dataArea}`

Same object, one row. 404 `NOT_FOUND`.

### GET `/api/v1/sales-orders/{salesId}/lines?company={dataArea}`

D365: `LogicRetailSalesOrdersLines_BI`. **No price field** on this entity.

200 `data[]`: `{ recordId, salesId, itemId, productName, salesQty, salesUnit, lineNum, dataArea }`.
`salesQty` is a decimal from D365.

404 `SO_NOT_OPEN` if the header is not an open order for this user.

### POST `/api/v1/sales-orders`

Body: `{ "company": "mm", "custAccount": "MMS021", "inventLocationId": "MMS000WH", "inventSiteId": "MMS000", "currencyCode": "SAR" }`

Only `custAccount` is required. Company / warehouse / site / currency fall back from JWT / warehouse lookup.
Sales taker is **always** JWT `personnelNumber` (`OrderTakerPersonnelNumber` on `SalesOrderHeadersV4`).

201 `data`: `{ salesOrderNumber, dataAreaId, custAccount, inventLocationId, inventSiteId, currencyCode, orderTakerPersonnelNumber }`.

Errors: `400 VALIDATION_ERROR`, `400 WAREHOUSE_REQUIRED`, `403 FORBIDDEN_COMPANY`, `502 DYNAMICS_*`.

```bash
curl -sS -X POST https://salesorderapp.logictec.online/api/v1/sales-orders \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d '{"company":"mm","custAccount":"MMS021","inventLocationId":"MMS000WH"}'
```

### POST `/api/v1/sales-orders/{salesId}/lines/full`

See [duplicate-line report](#duplicate-line-report) below.

Body: `{ "company": "mm", "itemNumber": "BG410.003", "quantity": 2, "ifExists": "fail" }`

`quantity` is a **whole integer**. `ifExists`: omit/`fail` | `add` | `replace`.

201 create / 200 update / 409 duplicate / 422 other business job failures (`NO_PRICE` etc still nested in `data` for old clients).

### POST `/api/v1/sales-orders/{salesId}/lines/quick`

Body: `{ "company": "mm", "lines": [ { "barcode": "...", "quantity": 1 } ] }` max 10.

201 all synced. Mixed failures: 422 with `data.items[]`. If **every** line is a duplicate and none synced: **409 `LINE_ALREADY_EXISTS`**.

### GET `/api/v1/sales-orders/{salesId}/failed-lines?company={dataArea}&mode=`

200 `data[]` of previously persisted failed job items (`commentAr` / `commentEn`).

```bash
curl -sS 'https://salesorderapp.logictec.online/api/v1/sales-orders/MM-245265/failed-lines?company=mm' \
  -H "Authorization: Bearer $TOKEN"
```

---

## ProblemDetails vs envelope

Anonymous 401 (no Bearer) on protected routes is `{ success: false, error: { code: "UNAUTHORIZED", message } }`.

Missing JSON fields on `[ApiController]` records now return **400 `{ success: false, error: { code: "VALIDATION_ERROR", message } }`**
(not ASP.NET ProblemDetails). That factory was added with the line-duplicate work.

`GET /health` is **not** enveloped: `{ ok, dynamicsMode, liveConfigured, env }`.

---

## Duplicate-line report

### 1. Before

Code path: **custom pre-check** on `LogicRetailSalesOrdersLines_BI` (`SalesId + DataArea + ItemId`),
**not** an OData `SalesOrderLines` insert. The insert is skipped.

Live POST of `BG410.003` on `MM-245265` (already a line):

```
HTTP 422 Unprocessable Entity
{
  "success": true,
  "data": {
    "success": false,
    "jobId": "...",
    "item": {
      "itemNumber": "BG410.003",
      "quantity": 1,
      "status": "failed",
      "commentEn": "This line already exists in the system or has been entered previously."
    }
  }
}
```

No `error.code`. Outer `success: true` + HTTP 422 is why the Flutter client mapped this to a generic Dynamics/server error.

D365 Infolog: none (write never sent). If the pre-check missed (item padding) and `POST /data/SalesOrderLines` ran, F&O returned a generic `DYNAMICS_ERROR` — that path is now mapped to `LINE_ALREADY_EXISTS` when the Infolog contains "already exists".

### 2. After (omit `ifExists` / `"fail"`)

```
HTTP 409 Conflict
{
  "success": false,
  "error": {
    "code": "LINE_ALREADY_EXISTS",
    "message": "Item BG410.003 is already on sales order MM-245265.",
    "itemNumber": "BG410.003",
    "salesId": "MM-245265",
    "existingLineRecId": 5648085576,
    "existingQuantity": 1
  }
}
```

### 3. Decision

**Fail-fast remains the default** (`LINE_ALREADY_EXISTS` / 409) so old app builds keep working.

**Option A upsert** is also live: `"ifExists": "add"` | `"replace"` on the same POST.
No PATCH endpoint was added.

### 4. Contract

**POST `/api/v1/sales-orders/{salesId}/lines/full`**

| ifExists | Already on SO | Result |
| --- | --- | --- |
| omitted / `fail` | yes | 409 `LINE_ALREADY_EXISTS` |
| `add` | yes | 200, quantity = existing + body.quantity |
| `replace` | yes | 200, quantity = body.quantity |
| any | no | 201 new `SalesOrderLines` row |

Success `data`: `{ success, jobId, salesId, itemNumber, quantity, updated, inventTransId, price, unitId, item: { id, itemNumber, quantity, status, price, unitId, availableQty } }`.

**POST `/api/v1/sales-orders/{salesId}/lines/quick`**

Per-line `data.items[]` with `status` / `code`. Hard-fail 409 `LINE_ALREADY_EXISTS` when the whole batch is duplicates. No `ifExists` on quick.

**PATCH**: not added.

### 5. Error codes (full + quick)

| Code | HTTP | When |
| --- | --- | --- |
| `LINE_ALREADY_EXISTS` | 409 | Item already on this SO (full fail-fast, or quick all-duplicate) |
| `VALIDATION_ERROR` | 400 | Missing itemNumber / empty quick lines / bad `ifExists` |
| `INVALID_QTY` | 400 | `quantity < 1` |
| `MAX_LINES` | 400 | Quick > 10 lines |
| `NO_PRICE` | 422 nested `data.success=false` | No row on `LogicRetailSalesPriceAgreements_BI` |
| `BARCODE_NOT_FOUND` | 404 on GET barcode; quick line comment "No matching product" | |
| `NO_STOCK` | 422 nested, or 404 on GET inventory | |
| `QTY_EXCEEDS_STOCK` | 422 nested | Requested qty > available sales qty |
| `SO_NOT_OPEN` | 404 | Header not open / not this sales taker |
| `LINE_NOT_FOUND` | 404 | Upsert target missing on `SalesOrderLines` |
| `FORBIDDEN_COMPANY` | 403 | DataArea not on JWT |
| `UNAUTHORIZED` | 401 | No/invalid Bearer |
| `DYNAMICS_UNAVAILABLE` / `DYNAMICS_FORBIDDEN` / `DYNAMICS_THROTTLED` / `DYNAMICS_ERROR` | 503/401/429/502 | Real F&O outage only — **not** used for duplicates |

### 6. Quantity type

API `quantity` is **int** (mobile rounds). D365 `SalesOrderLines.OrderedSalesQuantity` and
`LogicRetailSalesOrdersLines_BI.SalesQty` are **decimal**.

### 7. Curl

Duplicate-fail:

```bash
curl -sS -D - -X POST \
  "https://salesorderapp.logictec.online/api/v1/sales-orders/MM-245265/lines/full" \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d '{"company":"mm","itemNumber":"BG410.003","quantity":1}'
```

Upsert-success:

```bash
curl -sS -D - -X POST \
  "https://salesorderapp.logictec.online/api/v1/sales-orders/MM-245265/lines/full" \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d '{"company":"mm","itemNumber":"BG410.003","quantity":1,"ifExists":"add"}'
```
