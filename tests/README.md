# EricksonLopez.Auditing — Test Suite Documentation

Esta suite de pruebas garantiza la confiabilidad, inmutabilidad, aislamiento multi-tenant y seguridad criptográfica de `EricksonLopez.Auditing` en todos sus adaptadores.

---

## 1. Estructura de Proyectos

```
tests/
├── EricksonLopez.Auditing.UnitTests/         # Tests unitarios en memoria (rápidos, aislados, deterministas)
│   ├── AuditRecordModelTests.cs             # Modelo canónico, value objects, enums
│   ├── AuditIdTests.cs                      # Generación UUIDv7 y compatibilidad .NET 8 / 9+
│   ├── AuditScopeTests.cs                   # Contexto ambiental síncrono y asíncrono (AsyncLocal)
│   ├── AuditSensitivityPipelineTests.cs     # Denylist de seguridad y redacción de PII
│   ├── InMemoryAuditStoreTests.cs           # Store en memoria y keyset pagination
│   ├── HmacIntegrityTests.cs                # Hash chaining HMAC-SHA256 y detección de tampering
│   ├── DiRegistrationTests.cs               # Extensiones de Dependency Injection
│   ├── EfCoreAuditStoreTests.cs             # Adaptador Entity Framework Core (InMemory)
│   ├── MongoAuditStoreTests.cs              # Adaptador MongoDB
│   ├── OpenTelemetryAuditTests.cs           # Integración con ActivitySource y tracing
│   ├── PostgreSqlUnitTests.cs               # Adaptador PostgreSQL (FakeDb ADO.NET)
│   ├── SqlServerUnitTests.cs                # Adaptador SQL Server (FakeDb ADO.NET)
│   ├── MySqlUnitTests.cs                    # Adaptador MySQL (FakeDb ADO.NET)
│   ├── OracleUnitTests.cs                   # Adaptador Oracle (FakeDb ADO.NET)
│   └── FakeDb.cs                            # Test double de ADO.NET para Dapper
│
└── EricksonLopez.Auditing.IntegrationTests/ # Tests de integración con motores reales
    ├── SqliteAuditStoreIntegrationTests.cs  # SQLite con base de datos en disco y DDL real
    ├── PostgreSqlAuditStoreIntegrationTests.cs # PostgreSQL 15 en Testcontainers
    ├── SqlServerAuditStoreIntegrationTests.cs  # SQL Server 2022 en Testcontainers
    ├── MySqlAuditStoreIntegrationTests.cs      # MySQL 8.0 en Testcontainers
    ├── OracleAuditStoreIntegrationTests.cs     # Oracle Free en Testcontainers
    └── TestHelpers.cs                       # Helpers de integración
```

---

## 2. Ejecución de Tests

### 2.1 Tests Unitarios (Multi-Target: .NET 8, .NET 9, .NET 10)

Los tests unitarios no requieren Docker ni bases de datos externas y se ejecutan en ~1-2 segundos:

```bash
# Ejecutar tests unitarios en todos los frameworks destino
dotnet test tests/EricksonLopez.Auditing.UnitTests/EricksonLopez.Auditing.UnitTests.csproj

# Ejecutar con reporte de cobertura de código
dotnet test tests/EricksonLopez.Auditing.UnitTests/EricksonLopez.Auditing.UnitTests.csproj --collect:"XPlat Code Coverage"
```

### 2.2 Tests de Integración (Requiere Docker)

Los tests de integración utilizan **Testcontainers** para levantar instancias reales y efímeras de PostgreSQL, SQL Server, MySQL y Oracle, además de pruebas locales de SQLite:

```bash
# Pruebas de integración locales (SQLite no requiere Docker)
dotnet test tests/EricksonLopez.Auditing.IntegrationTests/EricksonLopez.Auditing.IntegrationTests.csproj --filter "FullyQualifiedName~Sqlite"

# Suite completa de integración (Requiere Docker activo)
dotnet test tests/EricksonLopez.Auditing.IntegrationTests/EricksonLopez.Auditing.IntegrationTests.csproj
```

---

## 3. Mutation Testing (Stryker.NET)

La calidad de las aserciones se valida mediante pruebas de mutación con Stryker.NET:

```bash
# Instalar herramienta global si es necesario
dotnet tool install -g dotnet-stryker

# Ejecutar análisis de mutación sobre el proyecto de tests unitarios
cd tests/EricksonLopez.Auditing.UnitTests
dotnet stryker
```

### Política de Mutantes:
- **Core & Criptografía**: 100% Mutation Score obligatorio.
- **Adaptadores Dapper**: Umbral mínimo de 85% (mutantes sobrevivientes en `.ConfigureAwait(false)` son tolerados por no afectar semántica en ausencia de `SynchronizationContext`).

---

## 4. Patrones de Testing Utilizados

- **Fluent Data Builders**: `AuditRecordBuilder` en `EricksonLopez.Auditing.Testing` provee creación parametrizada y concisa de registros de auditoría.
- **Humble Object Pattern**: La lógica de negocio pesada reside en servicios puros (`HmacAuditIntegrityService`, `AuditSensitivityPipeline`), testeables sin I/O.
- **ADO.NET Fake Test Double**: `FakeDb.cs` intercepta comandos y parámetros de Dapper permitiendo validar consultas SQL generadas en microsegundos sin sobrecarga de base de datos.
- **Property-Based Testing**: `FsCheck.Xunit` valida propiedades universales de unicidad de UUIDv7 y determinismo criptográfico de HMAC ante entradas arbitrarias.
