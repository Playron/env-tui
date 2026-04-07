# Kontaktliste-ekstraktor

Fullstack-applikasjon for å ekstrahere strukturert kontaktinformasjon fra ulike filformater.
Bruker AI (Claude, OpenAI eller Ollama) som intelligent fallback for ustrukturerte filer.

## Støttede filformater

| Format | Ekstraksjon |
|--------|-------------|
| `.csv` | Regex + kolonnegjenkjenning |
| `.xlsx` | Regex + kolonnegjenkjenning |
| `.vcf` | vCard-parsing |
| `.pdf` | Regex → AI-fallback |
| `.docx` | Regex → AI-fallback |
| `.txt` | Regex → AI-fallback |

## Teknologistabel

- **Backend:** .NET 10 Minimal API (C#) — ingen controllers, kun `MapGroup/MapPost/MapGet`
- **ORM:** Entity Framework Core med SQLite
- **AI:** `ILlmService` med støtte for Claude, OpenAI og Ollama via `HttpClient`
- **Frontend:** React 18 + TypeScript + Vite
- **Styling:** Tailwind CSS

## Forutsetninger

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org/) og npm
- (Valgfritt) API-nøkkel for Claude/OpenAI, eller Ollama kjørende lokalt

## Oppsett og kjøring

### 1. Backend

```bash
cd ContactExtractor/src/ContactExtractor.Api

# Sett AI API-nøkkel via user-secrets (anbefalt for utvikling)
dotnet user-secrets set "Llm:ApiKey" "din-nøkkel-her"

# Kjør serveren
dotnet run
```

Serveren starter på `http://localhost:5000`.  
Swagger UI: `http://localhost:5000/swagger`

Databasen (`contactextractor-dev.db`) opprettes og migreres automatisk ved første kjøring.

### 2. Frontend

```bash
cd contact-extractor-ui
npm install
npm run dev
```

Frontend starter på `http://localhost:5173` og proxyer API-kall til backend.

### 3. Tester

```bash
cd ContactExtractor
dotnet test
```

## AI-konfigurasjon

AI-provider konfigureres i `appsettings.json` (eller via user-secrets i utvikling):

### Claude (Anthropic)

```json
{
  "Llm": {
    "Provider": "claude",
    "ApiKey": "",
    "Model": "claude-sonnet-4-5"
  }
}
```

```bash
dotnet user-secrets set "Llm:ApiKey" "sk-ant-..."
```

### OpenAI

```json
{
  "Llm": {
    "Provider": "openai",
    "ApiKey": "",
    "Model": "gpt-4o"
  }
}
```

```bash
dotnet user-secrets set "Llm:ApiKey" "sk-..."
```

### Ollama (lokal kjøring)

```json
{
  "Llm": {
    "Provider": "ollama",
    "Model": "llama3.1",
    "BaseUrl": "http://localhost:11434"
  }
}
```

### Deaktiver AI

```json
{
  "Llm": {
    "Provider": "none"
  }
}
```

**Ingen kodeendring er nødvendig for å bytte provider** — kun konfigurasjonsfilen.

## AI-strategi

For strukturerte filer (CSV, Excel, VCF) brukes kun regex og kolonnegjenkjenning.

For ustrukturerte filer (PDF, Word, TXT) brukes en tostegs-tilnærming:

1. Ekstraher råtekst fra filen
2. Forsøk regex-basert ekstraksjon (e-post, telefon, navn-mønstre)
3. Hvis regex finner < 2 kontakter eller konfidensen er lav → send teksten til LLM
4. Merge og dedupliser resultater fra regex + LLM

Kontakter merkes med `ExtractionSource = "regex"` eller `"ai"`.

## Prosjektstruktur

```
env-tui/
├── ContactExtractor/               # .NET backend
│   ├── ContactExtractor.sln
│   ├── src/
│   │   └── ContactExtractor.Api/
│   │       ├── Program.cs          # Minimal API oppsett
│   │       ├── Endpoints/          # Upload, Contact, Export, Settings
│   │       ├── AI/                 # ILlmService, providers (Claude/OpenAI/Ollama)
│   │       ├── Domain/             # Entities + Value Objects (DDD)
│   │       ├── Infrastructure/     # EF Core DbContext + konfigurasjoner
│   │       ├── Services/           # IFileParser + alle parsere
│   │       └── Contracts/          # DTOs (records)
│   └── tests/
│       └── ContactExtractor.Tests/ # xUnit-tester (parsing, regex, prompt, LLM-response)
│
└── contact-extractor-ui/           # React frontend
    └── src/
        ├── components/             # UI-komponenter inkl. AiBadge og ConfidenceBar
        ├── hooks/                  # useFileUpload, useContacts
        ├── services/               # API-klient
        └── types/                  # TypeScript-typer
```

## API-endepunkter

| Metode | URL | Beskrivelse |
|--------|-----|-------------|
| `POST` | `/api/upload` | Last opp fil og ekstraher kontakter |
| `POST` | `/api/upload/preview` | Forhåndsvisning + kolonne-mapping-forslag |
| `GET`  | `/api/upload/supported-formats` | Støttede filformater |
| `GET`  | `/api/contacts` | Hent alle opplastingssesjoner |
| `GET`  | `/api/contacts/{sessionId}` | Hent kontakter for en sesjon |
| `PUT`  | `/api/contacts/{sessionId}/contacts/{contactId}` | Oppdater en kontakt |
| `DELETE` | `/api/contacts/{sessionId}` | Slett sesjon og alle kontakter |
| `POST` | `/api/export/{sessionId}/csv` | Eksporter til CSV |
| `POST` | `/api/export/{sessionId}/excel` | Eksporter til Excel |
| `GET`  | `/api/settings/llm` | Vis aktiv AI-provider (uten API-nøkkel) |

## EF Core-migreringer

```bash
cd ContactExtractor/src/ContactExtractor.Api

# Opprett ny migrasjon
dotnet ef migrations add <Navn>

# Kjør migreringer manuelt
dotnet ef database update
```

Migreringer kjøres automatisk ved oppstart i Development-miljøet.

## Miljøvariabler

| Nøkkel | Beskrivelse |
|--------|-------------|
| `ConnectionStrings:Default` | SQLite connection string |
| `Llm:Provider` | AI-provider: `claude`, `openai`, `ollama`, `none` |
| `Llm:ApiKey` | API-nøkkel (bruk user-secrets i utvikling) |
| `Llm:Model` | Valgfri model-override |
| `Llm:BaseUrl` | Base URL for Ollama |
| `Llm:MaxInputCharacters` | Maks tegn sendt til LLM (standard: 50 000) |
| `AllowedOrigins` | CORS-tillatte origins (standard: `http://localhost:5173`) |
