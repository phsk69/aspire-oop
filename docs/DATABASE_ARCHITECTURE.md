# Database Connection Architecture

This project uses a **three-tier connection string strategy** for PostgreSQL database operations, providing security isolation and operational clarity.

## Connection Types

### 🔧 **DBO (Database Owner)**

- **Purpose**: Schema changes, migrations, database structure modifications
- **Permissions**: Full database ownership privileges
- **Usage**: Entity Framework migrations, database initialization
- **Connection Key**: `MigrationConnection`

### ✏️ **RW (Read-Write)**

- **Purpose**: Standard CRUD operations, transaction processing
- **Permissions**: INSERT, UPDATE, DELETE, SELECT on application tables
- **Usage**: Business logic operations, user data modifications
- **Connection Key**: `ReadWriteConnection`

### 👀 **RO (Read-Only)**

- **Purpose**: Query operations, reporting, analytics
- **Permissions**: SELECT only on application tables
- **Usage**: Dashboard queries, reporting services, read-heavy operations
- **Connection Key**: `PostgreSQL_RO` / `SqlServer_RO`

## DbContext Architecture

### AppDboDbContext
- **Connection**: RW (Runtime), DBO (Migrations only)
- **Purpose**: Identity management - user authentication, roles, passwords
- **Usage**: Login, registration, password changes, role assignments
- **Note**: Uses RW for normal operations, DBO only needed for schema migrations

### AppReadWriteDbContext
- **Connection**: RW
- **Purpose**: Standard business operations and Identity runtime operations
- **Usage**: Creating posts, updating user data, transactional operations

### AppReadOnlyDbContext
- **Connection**: RO
- **Purpose**: Query-only operations
- **Usage**: Dashboard data, search operations, reporting, user lookups
- **Features**: No-tracking queries, write operations throw exceptions

## Repository Pattern

### ReadWriteRepository<T>
- Uses `AppReadWriteDbContext` with RW connection
- Supports all CRUD operations
- Handles transactions and data modifications

### ReadOnlyRepository<T>
- Uses `AppReadOnlyDbContext` with RO connection
- Supports only read operations (Get, Find, Count, Exists)
- Throws exceptions on write attempts
- Optimized with no-tracking queries

## Configuration

### Connection String Format (PostgreSQL)

```json
{
  "ConnectionStrings": {
    "MigrationConnection": "Host=localhost;Database=dev_aspire_deez_nutz;Username=svc_dev_aspire_deez_nutz_dbo;Password=4Xbc8tun;SSL Mode=Require;Trust Server Certificate=true",
    "ReadWriteConnection": "Host=localhost;Database=dev_aspire_deez_nutz;Username=svc_dev_aspire_deez_nutz_rw;Password=4Xbc8tun;SSL Mode=Require;Trust Server Certificate=true",
    "ReadOnlyConnection": "Host=localhost;Database=dev_aspire_deez_nutz;Username=svc_dev_aspire_deez_nutz_ro;Password=4Xbc8tun;SSL Mode=Require;Trust Server Certificate=true"
  }
}
```

### Database User Setup (PostgreSQL)

```sql
-- Create roles if they don't exist
CREATE ROLE svc_dev_aspire_deez_nutz_dbo WITH LOGIN PASSWORD '4Xbc8tun' NOCREATEDB NOCREATEROLE;
CREATE ROLE svc_dev_aspire_deez_nutz_rw WITH LOGIN PASSWORD '4Xbc8tun' NOCREATEDB NOCREATEROLE;  
CREATE ROLE svc_dev_aspire_deez_nutz_ro WITH LOGIN PASSWORD '4Xbc8tun' NOCREATEDB NOCREATEROLE;

-- Database permissions
GRANT CONNECT ON DATABASE dev_aspire_deez_nutz TO svc_dev_aspire_deez_nutz_dbo;
GRANT CONNECT ON DATABASE dev_aspire_deez_nutz TO svc_dev_aspire_deez_nutz_rw;
GRANT CONNECT ON DATABASE dev_aspire_deez_nutz TO svc_dev_aspire_deez_nutz_ro;

-- Schema permissions
GRANT USAGE, CREATE ON SCHEMA public TO svc_dev_aspire_deez_nutz_dbo;
GRANT USAGE ON SCHEMA public TO svc_dev_aspire_deez_nutz_rw;
GRANT USAGE ON SCHEMA public TO svc_dev_aspire_deez_nutz_ro;

-- Existing objects permissions
GRANT ALL ON ALL TABLES IN SCHEMA public TO svc_dev_aspire_deez_nutz_dbo;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO svc_dev_aspire_deez_nutz_rw;
GRANT SELECT ON ALL TABLES IN SCHEMA public TO svc_dev_aspire_deez_nutz_ro;

GRANT ALL ON ALL SEQUENCES IN SCHEMA public TO svc_dev_aspire_deez_nutz_dbo;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO svc_dev_aspire_deez_nutz_rw;
GRANT SELECT ON ALL SEQUENCES IN SCHEMA public TO svc_dev_aspire_deez_nutz_ro;

-- Default privileges for future objects created by DBO
ALTER DEFAULT PRIVILEGES FOR ROLE svc_dev_aspire_deez_nutz_dbo IN SCHEMA public GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO svc_dev_aspire_deez_nutz_rw;
ALTER DEFAULT PRIVILEGES FOR ROLE svc_dev_aspire_deez_nutz_dbo IN SCHEMA public GRANT SELECT ON TABLES TO svc_dev_aspire_deez_nutz_ro;
ALTER DEFAULT PRIVILEGES FOR ROLE svc_dev_aspire_deez_nutz_dbo IN SCHEMA public GRANT USAGE, SELECT ON SEQUENCES TO svc_dev_aspire_deez_nutz_rw;
ALTER DEFAULT PRIVILEGES FOR ROLE svc_dev_aspire_deez_nutz_dbo IN SCHEMA public GRANT SELECT ON SEQUENCES TO svc_dev_aspire_deez_nutz_ro;

-- Default privileges for future objects created by postgres (EF migrations)
ALTER DEFAULT PRIVILEGES FOR ROLE postgres IN SCHEMA public GRANT ALL ON TABLES TO svc_dev_aspire_deez_nutz_dbo;
ALTER DEFAULT PRIVILEGES FOR ROLE postgres IN SCHEMA public GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO svc_dev_aspire_deez_nutz_rw;
ALTER DEFAULT PRIVILEGES FOR ROLE postgres IN SCHEMA public GRANT SELECT ON TABLES TO svc_dev_aspire_deez_nutz_ro;
ALTER DEFAULT PRIVILEGES FOR ROLE postgres IN SCHEMA public GRANT ALL ON SEQUENCES TO svc_dev_aspire_deez_nutz_dbo;
ALTER DEFAULT PRIVILEGES FOR ROLE postgres IN SCHEMA public GRANT USAGE, SELECT ON SEQUENCES TO svc_dev_aspire_deez_nutz_rw;
ALTER DEFAULT PRIVILEGES FOR ROLE postgres IN SCHEMA public GRANT SELECT ON SEQUENCES TO svc_dev_aspire_deez_nutz_ro;

-- Role hierarchy
GRANT svc_dev_aspire_deez_nutz_ro TO svc_dev_aspire_deez_nutz_rw;
GRANT svc_dev_aspire_deez_nutz_rw TO svc_dev_aspire_deez_nutz_dbo;

-- Fix permissions on existing Identity tables
GRANT ALL ON TABLE "AspNetRoles" TO svc_dev_aspire_deez_nutz_dbo;
GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE "AspNetRoles" TO svc_dev_aspire_deez_nutz_rw;
GRANT SELECT ON TABLE "AspNetRoles" TO svc_dev_aspire_deez_nutz_ro;

GRANT ALL ON TABLE "AspNetUsers" TO svc_dev_aspire_deez_nutz_dbo;
GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE "AspNetUsers" TO svc_dev_aspire_deez_nutz_rw;
GRANT SELECT ON TABLE "AspNetUsers" TO svc_dev_aspire_deez_nutz_ro;

GRANT ALL ON TABLE "AspNetUserRoles" TO svc_dev_aspire_deez_nutz_dbo;
GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE "AspNetUserRoles" TO svc_dev_aspire_deez_nutz_rw;
GRANT SELECT ON TABLE "AspNetUserRoles" TO svc_dev_aspire_deez_nutz_ro;

GRANT ALL ON TABLE "AspNetUserClaims" TO svc_dev_aspire_deez_nutz_dbo;
GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE "AspNetUserClaims" TO svc_dev_aspire_deez_nutz_rw;
GRANT SELECT ON TABLE "AspNetUserClaims" TO svc_dev_aspire_deez_nutz_ro;

GRANT ALL ON TABLE "AspNetUserLogins" TO svc_dev_aspire_deez_nutz_dbo;
GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE "AspNetUserLogins" TO svc_dev_aspire_deez_nutz_rw;
GRANT SELECT ON TABLE "AspNetUserLogins" TO svc_dev_aspire_deez_nutz_ro;

GRANT ALL ON TABLE "AspNetUserTokens" TO svc_dev_aspire_deez_nutz_dbo;
GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE "AspNetUserTokens" TO svc_dev_aspire_deez_nutz_rw;
GRANT SELECT ON TABLE "AspNetUserTokens" TO svc_dev_aspire_deez_nutz_ro;

GRANT ALL ON TABLE "AspNetRoleClaims" TO svc_dev_aspire_deez_nutz_dbo;
GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE "AspNetRoleClaims" TO svc_dev_aspire_deez_nutz_rw;
GRANT SELECT ON TABLE "AspNetRoleClaims" TO svc_dev_aspire_deez_nutz_ro;
```

## Security Benefits

1. **Principle of Least Privilege**: Each connection has only necessary permissions
2. **Operation Isolation**: Write and read operations are clearly separated
3. **Audit Trail**: Database logs can distinguish between operation types
4. **Attack Surface Reduction**: Compromised RO connections cannot modify data
5. **Schema Protection**: Only DBO connections can alter database structure

## Development Workflow

### Migrations (DBO Only)
```bash
make ef-add-migration NAME=AddNewFeature
make ef-update-database
```

### Application Operations (RW/RO)
- **Identity Operations**: Login, registration, password changes (RW)
- **Business Operations**: CRUD on application data (RW)
- **Query Operations**: Reporting, dashboards, search (RO)
- Controllers automatically use appropriate repositories based on operation type

## DBO Connection Usage Pattern

**DBO connections are used ONLY for:**
- ✅ Entity Framework migrations (`dotnet ef migrations add/update`)
- ✅ Database schema initialization
- ✅ Index creation/modification
- ✅ Table structure changes

**DBO connections are NOT used for:**
- ❌ User authentication/authorization (uses RW)
- ❌ Business logic operations (uses RW) 
- ❌ Runtime application operations (uses RW/RO)
- ❌ API request processing (uses RW/RO)

This ensures DBO privileges are isolated to schema management only.

## Environment Configuration

### Development
- All connection strings in `appsettings.Development.secrets.json`
- Can use localhost PostgreSQL with separate users

### Production
- Connection strings from secure key management (Azure Key Vault, AWS Secrets Manager)
- Separate database instances for maximum security
- Network-level isolation between services

## Monitoring & Logging

- Database provider service logs connection type usage
- Repository operations are tagged with connection type
- Performance monitoring can track RO vs RW operation patterns

## Migration Path

For existing applications:
1. Create new database users with appropriate permissions
2. Update connection strings in secrets file
3. Test migrations with DBO connection
4. Gradually migrate repositories to use appropriate contexts
5. Monitor and validate security isolation

This architecture provides a robust foundation for secure, scalable database operations while maintaining clear operational boundaries.

## DB Config example

```SQL
-- PostgreSQL script to create database, roles, and users for dev_aspire_deez_nutz
-- Run this script as a superuser

-- Create roles
CREATE ROLE dev_aspire_deez_nuts_dbo;
CREATE ROLE dev_aspire_deez_nuts_rw;
CREATE ROLE dev_aspire_deez_nuts_ro;

-- Grant CREATEDB privilege to dbo role
ALTER ROLE dev_aspire_deez_nuts_dbo CREATEDB;

-- Create the database owned by the dbo role
CREATE DATABASE dev_aspire_deez_nutz OWNER dev_aspire_deez_nuts_dbo;

-- Connect to the target database
\c dev_aspire_deez_nutz;

-- Grant connect privileges to the database
GRANT CONNECT ON DATABASE dev_aspire_deez_nutz TO dev_aspire_deez_nuts_dbo;
GRANT CONNECT ON DATABASE dev_aspire_deez_nutz TO dev_aspire_deez_nuts_rw;
GRANT CONNECT ON DATABASE dev_aspire_deez_nutz TO dev_aspire_deez_nuts_ro;

-- Grant usage on public schema
GRANT USAGE ON SCHEMA public TO dev_aspire_deez_nuts_dbo;
GRANT USAGE ON SCHEMA public TO dev_aspire_deez_nuts_rw;
GRANT USAGE ON SCHEMA public TO dev_aspire_deez_nuts_ro;

-- Grant permissions on existing tables
-- DBO role: Full ownership (ALL PRIVILEGES)
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA public TO dev_aspire_deez_nuts_dbo;

-- Read-write role: SELECT, INSERT, UPDATE, DELETE
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO dev_aspire_deez_nuts_rw;

-- Read-only role: SELECT only
GRANT SELECT ON ALL TABLES IN SCHEMA public TO dev_aspire_deez_nuts_ro;

-- Grant permissions on existing sequences
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public TO dev_aspire_deez_nuts_dbo;
GRANT USAGE, SELECT, UPDATE ON ALL SEQUENCES IN SCHEMA public TO dev_aspire_deez_nuts_rw;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO dev_aspire_deez_nuts_ro;

-- Set default privileges for future objects created by the dbo role
-- Future tables
ALTER DEFAULT PRIVILEGES FOR ROLE dev_aspire_deez_nuts_dbo IN SCHEMA public GRANT ALL PRIVILEGES ON TABLES TO dev_aspire_deez_nuts_dbo;
ALTER DEFAULT PRIVILEGES FOR ROLE dev_aspire_deez_nuts_dbo IN SCHEMA public GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO dev_aspire_deez_nuts_rw;
ALTER DEFAULT PRIVILEGES FOR ROLE dev_aspire_deez_nuts_dbo IN SCHEMA public GRANT SELECT ON TABLES TO dev_aspire_deez_nuts_ro;

-- Future sequences
ALTER DEFAULT PRIVILEGES FOR ROLE dev_aspire_deez_nuts_dbo IN SCHEMA public GRANT ALL PRIVILEGES ON SEQUENCES TO dev_aspire_deez_nuts_dbo;
ALTER DEFAULT PRIVILEGES FOR ROLE dev_aspire_deez_nuts_dbo IN SCHEMA public GRANT USAGE, SELECT, UPDATE ON SEQUENCES TO dev_aspire_deez_nuts_rw;
ALTER DEFAULT PRIVILEGES FOR ROLE dev_aspire_deez_nuts_dbo IN SCHEMA public GRANT USAGE, SELECT ON SEQUENCES TO dev_aspire_deez_nuts_ro;

-- Grant permissions on existing functions
GRANT ALL PRIVILEGES ON ALL FUNCTIONS IN SCHEMA public TO dev_aspire_deez_nuts_dbo;
GRANT EXECUTE ON ALL FUNCTIONS IN SCHEMA public TO dev_aspire_deez_nuts_rw;
GRANT EXECUTE ON ALL FUNCTIONS IN SCHEMA public TO dev_aspire_deez_nuts_ro;

-- Default privileges for future functions
ALTER DEFAULT PRIVILEGES FOR ROLE dev_aspire_deez_nuts_dbo IN SCHEMA public GRANT ALL PRIVILEGES ON FUNCTIONS TO dev_aspire_deez_nuts_dbo;
ALTER DEFAULT PRIVILEGES FOR ROLE dev_aspire_deez_nuts_dbo IN SCHEMA public GRANT EXECUTE ON FUNCTIONS TO dev_aspire_deez_nuts_rw;
ALTER DEFAULT PRIVILEGES FOR ROLE dev_aspire_deez_nuts_dbo IN SCHEMA public GRANT EXECUTE ON FUNCTIONS TO dev_aspire_deez_nuts_ro;

-- Create service users and assign them to roles
CREATE USER svc_dev_aspire_deez_nuts_dbo WITH PASSWORD 'change_me_dbo_password';
CREATE USER svc_dev_aspire_deez_nuts_rw WITH PASSWORD 'change_me_rw_password';
CREATE USER svc_dev_aspire_deez_nuts_ro WITH PASSWORD 'change_me_ro_password';

-- Grant roles to service users
GRANT dev_aspire_deez_nuts_dbo TO svc_dev_aspire_deez_nuts_dbo;
GRANT dev_aspire_deez_nuts_rw TO svc_dev_aspire_deez_nuts_rw;
GRANT dev_aspire_deez_nuts_ro TO svc_dev_aspire_deez_nuts_ro;

```