# ProjectX

ProjectX is a modular business application workspace built with ASP.NET Core, Entity Framework Core, SQL Server LocalDB, Vue 3, TypeScript, and Vite.

The repository currently contains three application areas:

- `ProjectX.IAM`: identity and access management, including authentication, users, roles, permissions, JWT issuance, and audit views.
- `ProjectX.PM`: project management, including the project catalog consumed by IAM and downstream applications.
- `ProjectX.POS`: point-of-sale operations, including products, customers, sales, checkout, and dashboard endpoints.

## Repository Layout

```text
ProjectX.IAM/
  ProjectX.IAM.slnx
  src/
    ProjectX.IAM.API/
    ProjectX.IAM.Application/
    ProjectX.IAM.Domain/
    ProjectX.IAM.Infrastructure/
    ProjectX.IAM.UI/

ProjectX.PM/
  ProjectX.PM.slnx
  src/
    ProjectX.PM.API/
    ProjectX.PM.Application/
    ProjectX.PM.Domain/
    ProjectX.PM.Infrastructure/
    ProjectX.PM.UI/

ProjectX.POS/
  ProjectX.POS.slnx
  src/
    ProjectX.POS.API/
    ProjectX.POS.Application/
    ProjectX.POS.Domain/
    ProjectX.POS.Infrastructure/
    ProjectX.POS.UI/
```

Each backend follows a Domain/Application/Infrastructure/API split. Each frontend is a standalone Vue/Vite app under its module's `UI` project.

## Prerequisites

- .NET SDK 10.0
- Node.js and npm
- SQL Server LocalDB

The APIs apply Entity Framework Core migrations during startup and seed their local development data.

## Local Ports

| Service | HTTPS | HTTP | UI |
| --- | ---: | ---: | ---: |
| IAM API | `https://localhost:7141` | `http://localhost:5254` | `http://localhost:5173` |
| PM API | `https://localhost:7013` | `http://localhost:5203` | `http://localhost:5174` |
| POS API | `https://localhost:7014` | `http://localhost:5204` | `http://localhost:5175` |

Scalar API reference is available at `/scalar` for each API when running locally.

## Configuration

Development settings are checked in under each API's `appsettings.Development.json` or `appsettings.json`.

Default local databases:

- `ProjectX.IAM.Db`
- `ProjectX.PM.Db`
- `ProjectX.POS.Db`

Default IAM seed account:

```text
Username: admin
Email: admin@projectx.local
Password: ChangeMe123!
```

Frontend environment examples are available in:

- `ProjectX.IAM/src/ProjectX.IAM.UI/.env.example`
- `ProjectX.PM/src/ProjectX.PM.UI/.env.example`
- `ProjectX.POS/src/ProjectX.POS.UI/.env.example`

Copy the relevant example to `.env` before running a UI when local overrides are needed.

## Run the APIs

Start each API from its module folder or pass the project path from the repository root.

```powershell
dotnet run --project ProjectX.IAM/src/ProjectX.IAM.API/ProjectX.IAM.API.csproj --launch-profile https
dotnet run --project ProjectX.PM/src/ProjectX.PM.API/ProjectX.PM.API.csproj --launch-profile https
dotnet run --project ProjectX.POS/src/ProjectX.POS.API/ProjectX.POS.API.csproj --launch-profile https
```

IAM should be started before signing in from any UI. PM should be available when IAM initializes if you want IAM to sync the project catalog during startup.

## Run the UIs

Install dependencies once per UI project, then run the Vite dev server.

```powershell
cd ProjectX.IAM/src/ProjectX.IAM.UI
npm install
npm run dev
```

```powershell
cd ProjectX.PM/src/ProjectX.PM.UI
npm install
npm run dev
```

```powershell
cd ProjectX.POS/src/ProjectX.POS.UI
npm install
npm run dev
```

## Build

Build each backend solution:

```powershell
dotnet build ProjectX.IAM/ProjectX.IAM.slnx
dotnet build ProjectX.PM/ProjectX.PM.slnx
dotnet build ProjectX.POS/ProjectX.POS.slnx
```

Build each frontend:

```powershell
cd ProjectX.IAM/src/ProjectX.IAM.UI
npm run build
```

```powershell
cd ProjectX.PM/src/ProjectX.PM.UI
npm run build
```

```powershell
cd ProjectX.POS/src/ProjectX.POS.UI
npm run build
```

## Test

The IAM UI includes a Vitest script:

```powershell
cd ProjectX.IAM/src/ProjectX.IAM.UI
npm test
```

Use `dotnet test` for backend test projects as they are added.

## Notes

- IAM issues JWTs used by PM and POS.
- PM exposes the project catalog used by IAM for project-aware access management.
- POS requires IAM authentication and project context headers for protected retail operations.
- Local development secrets in checked-in appsettings are intended for development only.
