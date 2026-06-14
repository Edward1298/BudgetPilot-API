# BudgetPilot API — Architecture

## Overview
Single-project ASP.NET Core Web API for personal/business budget management.

## .NET Version
- **.NET 10.0** (`net10.0`)
- Nullable reference types: enabled
- Implicit usings: enabled

## Project Structure

```
BudgetPilot API/
├── Controllers/          → API endpoints (HTTP layer)
│   └── UsersController.cs
├── Services/             → Business logic
│   └── UsersService.cs
├── Data/                 → Data access (DbContext)
│   └── AppDbContext.cs
├── Entities/             → Domain/DB models
│   └── UsersOBJ.cs
├── Dtos/                 → Data Transfer Objects
│   └── UsersDTO.cs
├── Program.cs            → App bootstrap & DI
├── Properties/
│   └── launchSettings.json
└── appsettings.json
```

**Request flow:** Controller → Service → DbContext → PostgreSQL

## ORM & Database

| Item              | Detail                                                    |
|-------------------|----------------------------------------------------------|
| ORM               | Entity Framework Core 10.0.5                             |
| DB Provider       | Npgsql.EntityFrameworkCore.PostgreSQL 10.0.1              |
| Database          | PostgreSQL                                                |
| Host              | Supabase (pooler: aws-0-us-west-2.pooler.supabase.com)   |
| Retry Strategy    | NpgsqlRetryingExecutionStrategy                           |
## Implemented Features

| Module | Endpoints                      | Status |
|--------|--------------------------------|--------|
| Users  | GET api/users, POST api/users  | Active |

## NuGet Dependencies

| Package                                    | Version | Purpose                     |
|--------------------------------------------|---------|-----------------------------|
| BCrypt.Net-Next                           | 4.1.0   | Password hashing            |
| Microsoft.AspNetCore.OpenApi               | 10.0.4  | OpenAPI support             |
| Microsoft.EntityFrameworkCore              | 10.0.5  | ORM                         |
| Npgsql.EntityFrameworkCore.PostgreSQL      | 10.0.1  | Postgres EF Core provider   |
| supabase-csharp                           | 0.16.2  | Supabase SDK (not yet used) |
| Swashbuckle.AspNetCore.SwaggerGen         | 10.1.5  | Swagger doc generation      |
| Swashbuckle.AspNetCore.SwaggerUI          | 10.1.5  | Swagger UI                  |

## Launch Profiles

| Profile | URL                        |
|---------|----------------------------|
| http    | http://localhost:5253      |
| https   | https://localhost:7080     |


