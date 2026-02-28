# NoteSphere — Full-Stack Notes App (Vue + ASP.NET Core + SQL Server)

A beautiful notes application with **JWT auth**, **per-user authorization**, **search / filter / sort**, **pin / archive / **soft-delete\***\*, **tags**, **markdown preview**, and **responsive UI\*\*.

## Tech Stack

- **Frontend:** Vue 3 + Vite + TypeScript + TailwindCSS + Pinia + Vue Router + Axios
- **Backend:** ASP.NET Core Web API (.NET 8) + Dapper + JWT Auth
- **Database:** SQL Server
- **Extras:** Swagger/OpenAPI, health check, server-side pagination, seed data, Docker compose for SQL Server

---

## Quick Start (Recommended)

### 1) Start SQL Server (Docker)

From the project root:

```bash
docker compose up -d db
```

```PowerShell
docker exec -it notesphere-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "Your_strong_password123!" -C -Q "IF DB_ID('NoteSphereDb') IS NULL CREATE DATABASE NoteSphereDb;"
```

This starts SQL Server on `localhost:1433` using the credentials in `docker-compose.yml`.

### 2) Run Backend API

Prereqs: .NET 8 SDK

```bash
cd backend/NoteSphere.Api
dotnet restore
dotnet run
```

API:

- Swagger: `https://localhost:7045/swagger` or `http://localhost:5045/swagger`
- Health: `GET /health`

On first run, the API will:

- create DB tables if missing
- seed a demo user + some notes

Demo user (seeded):

- email: `demo@notesphere.dev`
- password: `Password123!`

### 3) Run Frontend

Prereqs: Node 18+

```bash
cd frontend
npm install
npm run dev
```

Frontend: `http://localhost:5173`

---

## Configuration

### Backend

`backend/NoteSphere.Api/appsettings.Development.json` contains the DB connection string and JWT settings.

### Frontend

`frontend/.env` controls the API base URL.

---

## Features

### Auth

- Register / login (JWT)
- Password hashing (BCrypt)
- Notes are **scoped per user** (you can only access your own notes)

### Notes

- Create / read / update / delete
- Pin & Archive
- Tags (simple, fast)
- Full-text search on title/content
- Sorting by created/updated/title
- Filtering by pinned/archived/tag
- Pagination (backend + UI)

### UI

- Clean layout with sidebar list + editor panel
- Markdown preview
- Mobile-friendly responsive design
- Toast notifications
- Optimistic UI updates where safe

---

## Project Structure

```
notesphere/
  backend/
    NoteSphere.Api/
  frontend/
  sql/
  docker-compose.yml
```

---

## Useful Commands

### Stop DB

```bash
docker compose down
```

### Run Backend Tests (lightweight)

```bash
cd backend/NoteSphere.Api
dotnet test
```

---

## Notes

- This project is intentionally designed to be **easy to run**:
  - clear separation of concerns (controllers/services/repos)
  - Dapper with parameterized queries
  - secure password hashing
  - JWT authorization per-user
  - Swagger documented endpoints

## Media in notes

- Upload images and insert as Markdown (`![](url)`)
- Draw on a canvas and insert the drawing as an image

Uploads are stored under `backend/NoteSphere.Api/wwwroot/uploads/{userId}/...` and served by the API.
