# Portfolio CMS — Complete Setup Guide
## From zero to running locally in ~20 minutes

---

## STEP 1 — Install Prerequisites

Install these tools before anything else. Each link goes to the official installer.

| Tool | Why needed | Download |
|------|-----------|---------|
| .NET 9 SDK | Builds and runs all C# projects | https://dotnet.microsoft.com/download/dotnet/9 |
| Docker Desktop | Runs SQL Server + containerises the API | https://www.docker.com/products/docker-desktop |
| Visual Studio 2022 (Community is free) OR Rider | IDE with Blazor + EF Core tooling | https://visualstudio.microsoft.com |
| Git | Version control | https://git-scm.com |

Verify .NET after install:
```bash
dotnet --version
# Should print: 9.0.x
```

---

## STEP 2 — Clone / Open the Project

If you're starting from scratch, create the folder and open it:
```bash
# Option A — if you already have the files
cd PortfolioCMS

# Option B — after pushing to GitHub
git clone https://github.com/YOUR_USERNAME/portfolio-cms.git
cd portfolio-cms
```

Open `PortfolioCMS.sln` in Visual Studio or Rider.

---

## STEP 3 — Start SQL Server with Docker

You don't need to install SQL Server. Docker runs it in a container:

```bash
# From the root of the project (where docker-compose.yml is)
docker compose up sqlserver -d

# Verify it's running
docker ps
# You should see: portfolio-sql   Up   0.0.0.0:1433->1433/tcp
```

SQL Server is now running on `localhost:1433`
- Username: `sa`
- Password: `YourStrong@Password123`

You can connect to it with Azure Data Studio or SQL Server Management Studio using these credentials.

---

## STEP 4 — Configure appsettings

Open `src/PortfolioCMS.API/appsettings.Development.json` and verify the connection string matches:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost,1433;Database=PortfolioCMS_Dev;User Id=sa;Password=YourStrong@Password123;TrustServerCertificate=True"
  }
}
```

**IMPORTANT — Change the JWT secret** in `appsettings.json`:
```json
"Jwt": {
  "Secret": "CHANGE-THIS-TO-A-LONG-RANDOM-SECRET-KEY-AT-LEAST-32-CHARS"
}
```

Generate a strong secret:
```bash
# PowerShell
[System.Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))

# or just type any long random string — 40+ characters
```

---

## STEP 5 — Run EF Core Migrations

The database schema is created automatically, but you need to run migrations once.

```bash
# From the solution root
cd src/PortfolioCMS.API

dotnet ef database update \
  --project ../PortfolioCMS.Infrastructure \
  --startup-project .
```

If `dotnet ef` is not found, install the tools first:
```bash
dotnet tool install --global dotnet-ef
```

On next startup, the `DatabaseSeeder` will automatically populate default data:
- Admin user: `admin` / `Admin@123456`
- 5 default sections (Hero, About, Skills, Projects, Contact)
- Default theme
- 1 sample project

**CHANGE THE DEFAULT PASSWORD** before deploying. Edit `DatabaseSeeder.cs`:
```csharp
PasswordHash = BCrypt.Net.BCrypt.HashPassword("YourNewPassword")
```

---

## STEP 6 — Run the API

### Option A — Visual Studio
Set `PortfolioCMS.API` as the startup project and press F5.

### Option B — Terminal
```bash
cd src/PortfolioCMS.API
dotnet run
```

The API starts on `http://localhost:5000`

Open Swagger UI to verify: **http://localhost:5000/swagger**

You should see all endpoints listed. Test the login:
1. Click `POST /api/auth/login`
2. Click "Try it out"
3. Enter `{ "username": "admin", "password": "Admin@123456" }`
4. Click Execute — you should get back a JWT token

---

## STEP 7 — Run the Blazor Frontend

Open a **second terminal**:

```bash
cd src/PortfolioCMS.Web
dotnet run
```

Blazor starts on `http://localhost:5001`

Open it in your browser. You should see the portfolio homepage with default content.

### Test the admin panel
Navigate to: **http://localhost:5001/admin/login**

Login with `admin` / `Admin@123456`

You'll reach the admin dashboard where you can:
- Edit any section text, subtitle, colors
- Add/edit/delete project cards
- Change the theme colors with a color picker
- View the audit log

---

## STEP 8 — Verify SignalR live updates

1. Open the public portfolio in one browser tab: `http://localhost:5001`
2. Open the admin panel in another tab: `http://localhost:5001/admin/login`
3. Log in as admin and edit the Hero section title
4. Click "Save Changes"
5. Switch to the public tab — the title updates **without a page refresh**

This is SignalR working in real time.

---

## STEP 9 — Run Tests

```bash
# From solution root
dotnet test

# Or just the unit tests
dotnet test tests/PortfolioCMS.UnitTests

# With coverage output
dotnet test --collect:"XPlat Code Coverage"
```

---

## STEP 10 — Optional: AI Features

To enable the AI text improvement and project description generator:

### Option A — OpenAI (easier, free tier available)
1. Go to https://platform.openai.com and create an account
2. Generate an API key
3. Add to `appsettings.json`:
```json
"OpenAI": {
  "ApiKey": "sk-..."
}
```

### Option B — Azure OpenAI
1. Create Azure OpenAI resource in Azure Portal
2. Deploy a model (gpt-4o or gpt-4)
3. Add to `appsettings.json`:
```json
"AzureOpenAI": {
  "Endpoint": "https://YOUR_RESOURCE.openai.azure.com",
  "ApiKey": "your-key",
  "DeploymentName": "gpt-4"
}
```

If neither is configured, the AI buttons simply return 503 — the rest of the app works fine.

---

## STEP 11 — Optional: Image Uploads (Azure Blob Storage)

By default, images are saved to `wwwroot/uploads/` locally.

To use Azure Blob Storage:
1. Create a Storage Account in Azure Portal
2. Create a container named `portfolio-images`
3. Add to `appsettings.json`:
```json
"Azure": {
  "StorageConnectionString": "DefaultEndpointsProtocol=https;AccountName=...;AccountKey=...;",
  "BlobContainerName": "portfolio-images"
}
```

---

## Project Structure Reference

```
PortfolioCMS/
├── src/
│   ├── PortfolioCMS.Domain/          ← Entities, enums — no dependencies
│   │   ├── Entities/Entities.cs      ← All DB models
│   │   └── Enums/SectionType.cs
│   │
│   ├── PortfolioCMS.Application/     ← Business logic — depends only on Domain
│   │   ├── DTOs/                     ← Request/response shapes
│   │   ├── Features/
│   │   │   ├── Sections/             ← CQRS handlers for sections
│   │   │   ├── Projects/             ← CQRS handlers for projects
│   │   │   ├── Theme/                ← Theme + Auth handlers
│   │   │   └── AI/                   ← AI handlers
│   │   └── Common/
│   │       ├── Interfaces/           ← Contracts (IAppDbContext, ITokenService...)
│   │       └── Behaviours/           ← MediatR validation pipeline
│   │
│   ├── PortfolioCMS.Infrastructure/  ← EF Core, JWT, Azure, SignalR, AI
│   │   ├── Persistence/
│   │   │   ├── AppDbContext.cs
│   │   │   ├── Configurations/       ← EF entity configs
│   │   │   └── Seeders/              ← Default data on first run
│   │   ├── Services/                 ← All interface implementations
│   │   └── Hubs/                     ← SignalR hub
│   │
│   ├── PortfolioCMS.API/             ← ASP.NET Core Minimal APIs entry point
│   │   ├── Program.cs                ← All endpoints defined here
│   │   ├── Middleware/               ← Global exception handler
│   │   └── appsettings.json
│   │
│   └── PortfolioCMS.Web/             ← Blazor WASM frontend
│       ├── Pages/                    ← Index.razor, AdminDashboard.razor, AdminLogin.razor
│       ├── Components/
│       │   ├── Public/               ← Hero, About, Skills, Projects, Contact
│       │   └── Admin/                ← SectionsEditor, ProjectsEditor, ThemeEditor, AuditLog
│       ├── Services/                 ← PortfolioApiService, PortfolioHubService
│       ├── wwwroot/
│       │   ├── index.html            ← Blazor host page
│       │   ├── app.css               ← All styles
│       │   └── appsettings.json      ← API base URL for WASM
│       └── _Imports.razor            ← Global usings for all components
│
├── tests/
│   └── PortfolioCMS.UnitTests/       ← xUnit + Moq tests
│
├── .github/workflows/ci-cd.yml       ← GitHub Actions: build → test → deploy
├── Dockerfile                        ← Multi-stage API container
├── docker-compose.yml                ← Run everything locally with one command
└── PortfolioCMS.sln
```

---

## Quick Commands Reference

```bash
# Start SQL Server
docker compose up sqlserver -d

# Run API
cd src/PortfolioCMS.API && dotnet run

# Run Blazor (second terminal)
cd src/PortfolioCMS.Web && dotnet run

# Add new EF migration (after changing entities)
dotnet ef migrations add MigrationName \
  --project src/PortfolioCMS.Infrastructure \
  --startup-project src/PortfolioCMS.API

# Apply migrations
dotnet ef database update \
  --project src/PortfolioCMS.Infrastructure \
  --startup-project src/PortfolioCMS.API

# Run all tests
dotnet test

# Build Docker image for API
docker build -t portfolio-cms-api .

# Run full stack with Docker
docker compose up
```

---

## Default Credentials (change before deploying!)

| What | Value |
|------|-------|
| Admin username | `admin` |
| Admin password | `Admin@123456` |
| SQL Server host | `localhost:1433` |
| SQL Server user | `sa` |
| SQL Server password | `YourStrong@Password123` |
| API URL | `http://localhost:5000` |
| Swagger UI | `http://localhost:5000/swagger` |
| Blazor app | `http://localhost:5001` |
| Admin panel | `http://localhost:5001/admin/login` |
