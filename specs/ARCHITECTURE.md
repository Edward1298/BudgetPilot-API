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
│   ├── UsersController.cs
│   ├── AccountsController.cs
│   ├── CategoriesController.cs
│   └── TransactionsController.cs
├── Services/             → Business logic
│   ├── UserService.cs
│   ├── AccountsService.cs
│   ├── CategoriesService.cs
│   └── TransactionsService.cs
├── Data/                 → Data access (DbContext)
│   └── AppDbContext.cs
├── Entities/             → Domain/DB models
│   ├── UsersOBJ.cs
│   ├── AccountsOBJ.cs
│   ├── CategoriesOBJ.cs
│   └── TransactionsOBJ.cs
├── Dtos/                 → Data Transfer Objects
│   ├── RegisterDTO.cs
│   ├── LoginDTO.cs
│   ├── AccountsDTO.cs
│   ├── CategoriesDTO.cs
│   └── TransactionsDTO.cs
├── Program.cs            → App bootstrap & DI
├── Properties/
│   └── launchSettings.json
└── appsettings.json
```

**Request flow:** Controller → Service → DbContext → SQL Server

## ORM & Database

| Item              | Detail                                                    |
|-------------------|----------------------------------------------------------|
| ORM               | Entity Framework Core 10.0.5                             |
| DB Provider       | Microsoft.EntityFrameworkCore.SqlServer                  |
| Database          | SQL Server (local instance)                              |
| Connection        | Windows Authentication (`Trusted_Connection=True`)       |
| Retry Strategy    | `EnableRetryOnFailure()`                                 |

## Implemented Features

| Module       | Endpoints                                              | Status |
|--------------|--------------------------------------------------------|--------|
| Users        | POST /api/v1/users/register, POST /api/v1/users/login, GET /api/v1/users, GET /api/v1/users/{id}, GET /api/v1/users/me | Active |
| Accounts     | GET /api/v1/accounts, GET /api/v1/accounts/{id}, POST /api/v1/accounts, PUT /api/v1/accounts/{id}, DELETE /api/v1/accounts/{id} | Active |
| Categories   | GET /api/v1/categories, GET /api/v1/categories/{id}, POST /api/v1/categories, PUT /api/v1/categories/{id}, DELETE /api/v1/categories/{id} | Active |
| Transactions | GET /api/v1/transactions, GET /api/v1/transactions/{id}, POST /api/v1/transactions, PUT /api/v1/transactions/{id}, DELETE /api/v1/transactions/{id} | Active |

## NuGet Dependencies

| Package                                    | Version | Purpose                     |
|--------------------------------------------|---------|-----------------------------|
| BCrypt.Net-Next                           | 4.1.0   | Password hashing            |
| Microsoft.AspNetCore.Authentication.JwtBearer | 10.0.9 | JWT authentication        |
| Microsoft.AspNetCore.OpenApi               | 10.0.4  | OpenAPI support             |
| Microsoft.EntityFrameworkCore              | 10.0.5  | ORM                         |
| Microsoft.EntityFrameworkCore.SqlServer    | 10.0.5  | SQL Server EF Core provider |
| Swashbuckle.AspNetCore.SwaggerGen         | 10.1.5  | Swagger doc generation      |
| Swashbuckle.AspNetCore.SwaggerUI          | 10.1.5  | Swagger UI                  |

## Launch Profiles

| Profile | URL                        |
|---------|----------------------------|
| http    | http://localhost:5253      |
| https   | https://localhost:7080     |

## Database Setup

The database schema is created via the raw SQL script at `scripts/setup-sqlserver.sql`. Run it in SQL Server Management Studio (SSMS) before starting the API. No EF Core migrations are used.
