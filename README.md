# Skill Hub — Core Backend REST API

[![CI/CD Backend](https://github.com/Chamiya09/Skill-Hub-BackEnd/actions/workflows/deploy-backend.yml/badge.svg)](https://github.com/Chamiya09/Skill-Hub-BackEnd/actions)
[![.NET](https://img.shields.io/badge/.NET-9.0%20%2F%208.0-512BD4.svg)](https://dotnet.microsoft.com/)
[![Entity Framework Core](https://img.shields.io/badge/EF%20Core-9.0-512BD4.svg)](https://docs.microsoft.com/ef/core/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-16%20(NeonDB)-4169E1.svg)](https://neon.tech/)
[![Azure Container Apps](https://img.shields.io/badge/Azure-Container%20Apps-0078D4.svg)](https://ca-skillhub-backend.bravebay-18c4a18e.eastasia.azurecontainerapps.io)

The transactional core and API gateway for **Skill Hub**. Built on ASP.NET Core, it coordinates relational persistence, JWT authentication, candidate evaluation workflows, Google Calendar holiday conflict checking, and communications with both the Judge0 sandbox and Python AI microservices.

---

## 🌟 Component Overview & Responsibilities

- **Identity & Authorization:** Role-Based Access Control (RBAC) supporting `CANDIDATE`, `Company`, and `Admin` using cryptographically signed JWT Bearers.
- **ATS Business Logic:** Vacancy lifecycle, applicant pipeline progression, and event scheduling.
- **Microservice Orchestration:** Dispatching candidate CVs to the Python LangGraph AI agent and sending untrusted code solutions to Judge0.
- **Production Base URL:** [https://ca-skillhub-backend.bravebay-18c4a18e.eastasia.azurecontainerapps.io](https://ca-skillhub-backend.bravebay-18c4a18e.eastasia.azurecontainerapps.io)
- **Interactive Swagger Documentation:** [https://ca-skillhub-backend.bravebay-18c4a18e.eastasia.azurecontainerapps.io/swagger](https://ca-skillhub-backend.bravebay-18c4a18e.eastasia.azurecontainerapps.io/swagger)
- **Health Probe:** [https://ca-skillhub-backend.bravebay-18c4a18e.eastasia.azurecontainerapps.io/health](https://ca-skillhub-backend.bravebay-18c4a18e.eastasia.azurecontainerapps.io/health)

---

## 🛠️ Technology Stack & Core Dependencies

- **Framework:** ASP.NET Core 9 (C# 12)
- **Web Server:** Kestrel with HTTP/2 and Envoy TLS reverse-proxy compatibility
- **ORM & Data Access:** Entity Framework Core 9 with `Npgsql.EntityFrameworkCore.PostgreSQL`
- **Database:** NeonDB Serverless PostgreSQL 16 (with PgBouncer connection pooling)
- **Authentication:** `Microsoft.AspNetCore.Authentication.JwtBearer` & `BCrypt.Net-Next`
- **API Documentation:** Swashbuckle OpenAPI / Swagger UI

---

## 📋 Prerequisites

- **.NET SDK:** `9.0` (or `8.0 LTS`)
- **Database:** PostgreSQL 15+ instance (or free NeonDB serverless connection)
- **Docker:** (Optional, for containerized local execution)

---

## 🚀 Quickstart & Local Setup

### 1. Clone Repository
```bash
git clone https://github.com/Chamiya09/Skill-Hub-BackEnd.git
cd Skill-Hub-BackEnd
```

### 2. Configure Settings
Create an `appsettings.Development.json` file in the project root:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "<ENTER_POSTGRESQL_CONNECTION_STRING>"
  },
  "JwtSettings": {
    "SecretKey": "<ENTER_JWT_SECRET_KEY_MIN_32_CHARACTERS>",
    "Issuer": "<ENTER_JWT_ISSUER>",
    "Audience": "<ENTER_JWT_AUDIENCE>",
    "ExpirationInHours": 24
  },
  "AiAgent": {
    "BaseUrl": "<ENTER_AI_AGENT_MICROSERVICE_URL>"
  },
  "ExecutionEngine": {
    "Provider": "Judge0",
    "Judge0Url": "<ENTER_JUDGE0_EXECUTION_ENGINE_URL>",
    "ApiKey": "<ENTER_JUDGE0_API_KEY>"
  },
  "GoogleCalendar": {
    "ApiKey": "<ENTER_GOOGLE_CALENDAR_API_KEY>",
    "DefaultCalendarId": "en.lk#holiday@group.v.calendar.google.com",
    "DefaultCountryCode": "LK"
  }
}
```

### 3. Restore & Build
```bash
dotnet restore
dotnet build
```

### 4. Run the API
```bash
dotnet run
```
On startup, `Program.cs` automatically connects to PostgreSQL and applies idempotent DDL migrations. The API will listen on `http://localhost:5155`. Visit `http://localhost:5155/swagger` to inspect endpoints.

---

## 🧪 Seeding & Default Test Accounts

| Role | Email | Password | Access Rights |
| :--- | :--- | :--- | :--- |
| **Super Admin** | `admin@<ENTER_DOMAIN>` | `<ENTER_ADMIN_PASSWORD>` | Full platform administration |
| **Employer / HR** | `hr@<ENTER_DOMAIN>` | `<ENTER_HR_PASSWORD>` | Vacancies, applicant screening, interview scheduling |
| **Candidate** | `candidate@<ENTER_DOMAIN>` | `<ENTER_CANDIDATE_PASSWORD>` | Job applications, code exams, profile management |

---

## ☁️ Deployment & CI/CD Pipeline

- **Containerization:** Packaged using a multi-stage `Dockerfile` (`mcr.microsoft.com/dotnet/aspnet:9.0`).
- **Registry:** Images are tagged (`sha-{commit}` and `:latest`) and pushed to **GitHub Container Registry (`ghcr.io`)**.
- **Hosting:** **Azure Container Apps** with zero-downtime rolling updates, consumption autoscaling (1–10 replicas), and managed Envoy ingress.