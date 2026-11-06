# KimiaFaravar – Phase 1 dynamic product catalog (JSON mode)

## Included

- Product-specific specification values persisted in `inventory-operations.json` as `productValues`.
- Select options persisted as `specificationOptions`.
- Effective specifications resolved through the parent chain (without copying parent rows).
- Required field and typed-value validation (Text, Decimal, Integer, Boolean, Select).
- Prevention of duplicate SKU and duplicate combinations of variant-key specifications within the same group.
- Responsive Persian RTL product creation view and group-specific AJAX field loading.
- Product list shows each product's saved specification values.
- Existing JSON data deserializes with missing new arrays initialized to empty lists.
- JSON saves use a temporary file plus replacement (single-process use only).

## Integration instructions

1. Back up `src/KimiaFaravar.Web/App_Data/*.json` before applying this patch.
2. Copy files in this package to the repository root, preserving directories.
3. In `Storage:Provider=Json` mode, start the MVC app and log in with the local development administrator.
4. Create a group called Washer and child group Industrial Washer.
5. Create a Select specification on Washer called Material, and Decimal specifications Diameter and Length.
6. Add option values Steel and Copper to Material.
7. Create an Industrial Washer and verify inherited three fields render; save values.
8. Verify its values are present under `productValues` in `inventory-operations.json` and visible in the product list.
9. Attempt the same variant combination under a different SKU; it must fail.
10. Check group and SKU validation; test with a copy of demo data, not production inventory.

## Important limitations / deliberately excluded from this phase

- This patch does not yet implement numeric specification measurement units / dimensional normalization, product-unit conversions, reservations, document approval, or SQL Server schema migrations.
- Existing stored products have no recorded specification values. They must be reviewed and enriched manually when migrated.
- The JSON store's static SemaphoreSlim prevents concurrent writes only within one application process. It must not be shared across multiple app instances.
- Full schema migration from JSON to SQL Server and cross-process transactional integrity require a later phase.
- Role-permission mapping for non-CEO accounts in the JSON authentication service remains an existing limitation.
- No .NET SDK is installed in this execution environment; compilation and runtime/UI browser testing could not be performed. Treat as code prepared for review, NOT build-verified.
- `main` was not modified. The GitHub integration returned 403 `Resource not accessible by integration` when creating a branch, so this package has not been pushed and no PR exists.
