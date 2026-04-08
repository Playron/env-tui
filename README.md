# Kontaktliste-ekstraktor

Fullstack-applikasjon for å ekstrahere strukturert kontaktinformasjon fra ulike filformater.
Bruker AI (Claude, OpenAI eller Ollama) som intelligent fallback for ustrukturerte filer.

## Funksjoner

| Fase | Funksjon |
|------|----------|
| **Fase 1** | Filparsing (CSV, Excel, PDF, Word, TXT, VCF), EF Core + SQLite, React-frontend |
| **Fase 2** | AI-ekstraksjon (Claude, OpenAI, Ollama), konfigurerbar via appsettings |
| **Fase 3** | Asynkron arkitektur (MassTransit + InMemory/RabbitMQ), SSE-streaming |
| **Fase 4** | Keycloak JWT-autentisering (valgfri, `EnableAuth: false` for utvikling) |
| **Fase 5** | Duplikatdeteksjon, kontaktvalidering, navnenormalisering via AI, tagging |
| **Fase 6** | Dashboard, audit-logg, webhooks, CRM-eksport (HubSpot, Google), alle eksportformater |

## Støttede filformater

| Format | Ekstraksjon |
|--------|-------------|
| `.csv` | Regex + kolonnegjenkjenning |
| `.xlsx` | Regex + kolonnegjenkjenning |
| `.vcf` | vCard-parsing |
| `.pdf` | Regex → AI-fallback |
| `.docx` | Regex → AI-fallback |
| `.txt` | Regex → AI-fallback |

## Eksportformater

| Format | Endepunkt |
|--------|-----------|
| Standard CSV | `POST /api/export/{id}/csv` |
| Excel (.xlsx) | `POST /api/export/{id}/excel` |
| vCard (.vcf) | `POST /api/export/{id}/vcard` |
| Google Contacts CSV | `POST /api/export/{id}/google` |
| Outlook CSV | `POST /api/export/{id}/outlook` |

## Teknologistabel

- **Backend:** .NET 10 Minimal API (C#) — ingen controllers, kun `MapGroup/MapPost/MapGet`
- **ORM:** Entity Framework Core 9 med SQLite (utvikling) / SQL Server (produksjon)
- **AI:** Abstrakt `ILlmService` — Claude, OpenAI eller Ollama
- **Meldingskø:** MassTransit med InMemory (utvikling) eller RabbitMQ (produksjon)
- **Sanntid:** Server-Sent Events via `IAsyncEnumerable` + `Channel<T>`
- **Auth:** Keycloak (JWT Bearer), valgfri i utvikling
- **Frontend:** React 18 + TypeScript + Vite + Tailwind CSS

## Forutsetninger

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org/) og npm
- (Valgfritt) API-nøkkel for Claude/OpenAI, eller Ollama kjørende lokalt
- (Fase 3+) Docker for RabbitMQ (valgfri — InMemory brukes som standard)
- (Fase 4+) Docker for Keycloak (valgfri — `EnableAuth: false` for utvikling)

## Oppsett og kjøring

### 1. Infrastruktur (valgfri)

```bash
# Start Keycloak + RabbitMQ via Docker Compose
docker compose up -d
```

### 2. Backend

```bash
cd ContactExtractor/src/ContactExtractor.Api

# Sett AI API-nøkkel via user-secrets (anbefalt for utvikling)
dotnet user-secrets set "Llm:ApiKey" "din-nøkkel-her"

# Kjør serveren
dotnet run
```

Serveren starter på `http://localhost:5000`.
Swagger UI: `http://localhost:5000/swagger`

Databasen (`contactextractor.db`) opprettes og migreres automatisk ved første kjøring.

### 3. Frontend

```bash
cd contact-extractor-ui
npm install
npm run dev
```

Frontend starter på `http://localhost:5173`.

## Konfigurasjon (appsettings.json)

```json
{
  "UseRabbitMq": false,
  "EnableAuth": false,
  "Keycloak": {
    "Authority": "http://localhost:8080/realms/contact-extractor",
    "Audience": "contact-extractor-api"
  },
  "Llm": {
    "Provider": "claude",
    "ApiKey": "",
    "Model": null,
    "MaxInputCharacters": 50000
  }
}
```

- Sett `UseRabbitMq: true` for å bruke RabbitMQ i stedet for InMemory-transport
- Sett `EnableAuth: true` for å aktivere Keycloak JWT-autentisering
- Sett `Provider` til `claude`, `openai`, `ollama` eller `none`

## Tester

```bash
cd ContactExtractor
dotnet test
```

Inkluderer tester for:
- E-post og telefonnummer-validering
- Kolonnegjenkjenning (norsk + engelsk)
- Regex-ekstraksjon fra tekst
- VCard-parsing
- LLM-prompt-bygging og JSON-deserialisering
- Duplikatdeteksjon (Jaro-Winkler, eksakt match)
- Webhook HMAC-signatur

## API-endepunkter

### Upload
| Metode | URL | Beskrivelse |
|--------|-----|-------------|
| POST | `/api/upload` | Last opp fil (asynkron, returnerer 202) |
| GET | `/api/upload/{id}/stream` | SSE-stream for fremdrift |
| GET | `/api/upload/{id}/result` | Polling-fallback (200 når ferdig, 202 pågår) |
| POST | `/api/upload/preview` | Forhåndsvisning og kolonne-mapping |
| GET | `/api/upload/supported-formats` | Støttede filformater |

### Kontakter
| Metode | URL | Beskrivelse |
|--------|-----|-------------|
| GET | `/api/contacts` | Alle sesjoner |
| GET | `/api/contacts/{id}` | Kontakter for sesjon |
| PUT | `/api/contacts/{sessionId}/contacts/{contactId}` | Oppdater kontakt |
| DELETE | `/api/contacts/{id}` | Slett sesjon |

### Tags (Fase 5)
| Metode | URL | Beskrivelse |
|--------|-----|-------------|
| GET | `/api/tags` | Alle tags |
| POST | `/api/tags` | Opprett tag |
| PUT | `/api/tags/{id}` | Oppdater tag |
| DELETE | `/api/tags/{id}` | Slett tag |
| POST | `/api/tags/contacts/add` | Legg til tag på kontakter (bulk) |
| POST | `/api/tags/contacts/remove` | Fjern tag fra kontakter (bulk) |

### Duplikater (Fase 5)
| Metode | URL | Beskrivelse |
|--------|-----|-------------|
| GET | `/api/duplicates` | Alle uløste duplikatgrupper |
| POST | `/api/duplicates/{id}/merge` | Slå sammen kontakter |
| POST | `/api/duplicates/{id}/dismiss` | Avvis som ikke-duplikater |

### Dashboard + Audit (Fase 6)
| Metode | URL | Beskrivelse |
|--------|-----|-------------|
| GET | `/api/dashboard` | Statistikk |
| GET | `/api/dashboard/audit` | Audit-logg for bruker |

### Webhooks (Fase 6)
| Metode | URL | Beskrivelse |
|--------|-----|-------------|
| GET | `/api/webhooks` | Alle webhooks |
| POST | `/api/webhooks` | Registrer webhook |
| DELETE | `/api/webhooks/{id}` | Slett webhook |
| POST | `/api/webhooks/{id}/test` | Send test-payload |

### Integrasjoner (Fase 6)
| Metode | URL | Beskrivelse |
|--------|-----|-------------|
| GET | `/api/integrations` | Tilgjengelige CRM-integrasjoner |
| POST | `/api/integrations/hubspot/export/{id}` | Eksporter til HubSpot |
| POST | `/api/integrations/google/export/{id}` | Eksporter til Google Contacts |
