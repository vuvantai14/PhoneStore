# PhoneStore catalog import

This development utility validates and imports the intentional 40-product demonstration catalog.
It never runs from Web or Admin startup.

From the repository root:

```powershell
dotnet run --project tools/PhoneStore.CatalogImport -- --execute
```

Without `--execute`, it only validates the required local image files. The import is transactional and idempotent.
