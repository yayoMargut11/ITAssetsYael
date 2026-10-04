# Sistema de Gestión y Resguardo de Activos TI

API REST en ASP.NET Core 8 + SQL Server (ADO.NET + Stored Procedures) para administrar activos de TI,
sus asignaciones a colaboradores, proveedores y el historial completo de movimientos.

## Requisitos
- .NET SDK 8+
- SQL Server 2019/2022 (Docker en Mac M1: ver abajo)

## Instalación

### 1. Base de datos (SQL Server en Docker, puerto 9999)
```bash
docker run --platform linux/amd64 -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=<tu-password>" \
  -p 9999:1433 --name sqlserver -d mcr.microsoft.com/mssql/server:2022-latest
```
Crear la base y ejecutar los scripts **en orden** (VS Code + extensión mssql, o `sqlcmd`):
```sql
CREATE DATABASE ITAssetsDb;
```
1. `ITAssets.Api/Database/01_Schema.sql`: tablas, índices únicos y `sp_AssignAsset`
2. `ITAssets.Api/Database/02_StoredProcedures.sql`: resto de los SPs
3. `ITAssets.Api/Database/03_Seed.sql`: roles, usuarios, proveedores, colaboradores y activos de ejemplo

### 2. Variables de entorno / secretos
| Clave (JSON / user-secrets) | Variable de entorno | Descripción |
|---|---|---|
| `ConnectionStrings:Default` | `ConnectionStrings__Default` | Cadena de conexión a SQL Server |
| `Jwt:Key` | `Jwt__Key` | Clave de firma JWT (mínimo 32 caracteres) |
| `Jwt:Issuer`, `Jwt:Audience`, `Jwt:ExpiresMinutes` | `Jwt__Issuer`, ... | Opcionales (hay valores por defecto en `appsettings.json`) |

Opción A, user-secrets (desarrollo):
```bash
cd ITAssets.Api
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:Default" "Server=localhost,9999;Database=ITAssetsDb;User Id=sa;Password=<tu-password>;TrustServerCertificate=True"
dotnet user-secrets set "Jwt:Key" "<cadena-aleatoria-de-32+-caracteres>"
```
Opción B, variables de entorno:
```bash
export ConnectionStrings__Default="Server=localhost,9999;Database=ITAssetsDb;User Id=sa;Password=<tu-password>;TrustServerCertificate=True"
export Jwt__Key="<cadena-aleatoria-de-32+-caracteres>"
```
Si falta alguna, la API se niega a arrancar con un mensaje claro. Ningún secreto está en el repositorio.

### 3. Ejecutar
```bash
dotnet run --project ITAssets.Api      # Swagger en http://localhost:<puerto>/swagger
```

### 4. Usuarios de prueba
| Usuario | Contraseña | Rol |
|---|---|---|
| `admin` | `Admin#2026` | Administrador |
| `operador` | `Operador#2026` | Operador |

En Swagger: `POST /api/auth/login` → copiar el `token` → botón **Authorize** → pegarlo.

## Endpoints y permisos
| Método y ruta | Administrador | Operador |
|---|:-:|:-:|
| `POST /api/auth/login` | público | público |
| `GET /api/assets` (`search`, `status`, `category`, `page`, `pageSize`), `GET /api/assets/{id}`, `GET /api/assets/{id}/history` | ✔ | ✔ |
| `POST /api/assets`, `PUT /api/assets/{id}` | ✔ | ✘ |
| `POST /api/assets/{id}/assign`, `POST /api/assets/{id}/return` | ✔ | ✔ |
| `GET /api/employees`, `GET /api/employees/{id}`, `POST /api/employees` | ✔ | ✔ |
| `PATCH /api/employees/{id}/active` | ✔ | ✘ |
| `GET /api/suppliers`, `GET /api/suppliers/{id}` | ✔ | ✔ |
| `POST /api/suppliers` | ✔ | ✘ |

## Errores
Todas las respuestas de error usan `application/problem+json` con `status`, `title`, `detail`, `code` (estable, para el cliente) y `traceId`.
Nunca se devuelven mensajes de SQL, stack traces ni datos internos (detalle solo en el log del servidor).
Ejemplos: `ASSET_NOT_AVAILABLE` (409), `EMPLOYEE_INACTIVE` (422), `NO_ACTIVE_ASSIGNMENT` (409), `ACCOUNT_LOCKED` (423), `TOO_MANY_REQUESTS` (429).

## Decisiones técnicas
- **Concurrencia en tres capas** para "un activo, una asignación activa": (1) `UPDLOCK, HOLDLOCK` sobre la fila del activo dentro de la transacción del SP serializa a los competidores;
  (2) índice único filtrado `UX_Assignments_ActiveAsset (AssetId) WHERE ReturnedAt IS NULL` como garantía final a nivel de datos;
  (3) la API traduce `ASSET_NOT_AVAILABLE`/2601/2627/1205 a un 409 controlado. La devolución usa el mismo candado.
- **Lógica transaccional en Stored Procedures** (asignar, devolver, crear/actualizar activo): la regla, el cambio de estado y la auditoría son atómicos. La API valida entrada (DataAnnotations + `IValidatableObject`) y los SPs vuelven a validar reglas de negocio.
- **Errores de negocio como códigos**: los SPs hacen `THROW 500xx`; `SqlErrorMapper` mapea el *número* (no el mensaje) a HTTP. Tabla de códigos en el encabezado de `02_StoredProcedures.sql`.
- **ADO.NET con un único `SqlExecutor`**: solo ejecuta SPs con parámetros tipados, sin SQL concatenado.
- **Seguridad**: BCrypt (work factor 11), validación de firma/issuer/audience/expiración del JWT (clock skew 30 s), roles por endpoint,
  respuestas 401/403 uniformes, mismo error para usuario inexistente y contraseña incorrecta (más un hash de relleno para igualar tiempos),
  bloqueo de cuenta 15 min tras 5 fallos (en BD) y rate limiting de 10 intentos/min por IP en el login.
- **Estado del activo**: `Asignado` solo se alcanza/abandona vía asignar/devolver; `Retirado` es terminal. `sp_UpdateAsset` lo impone, así `Assets.Status` nunca contradice a `Assignments`.
- **Concurrencia optimista opcional** en edición de activos mediante `RowVer`.
- **Capas**: Controllers → Repositories (ADO.NET) / AuthService. No añadí una capa de servicios intermedia para activos porque la lógica de negocio vive en los SPs y el controlador solo orquesta.

## Pruebas
```bash
dotnet test                                   # unitarias (no requieren BD); las de integración se omiten
export ConnectionStrings__Default="..."       # con BD lista: corre también las de integración
dotnet test
```
- Unitarias: mapeo de errores SQL, validaciones de entrada, emisión/validación/expiración de JWT, flujo de login (bloqueo, credenciales, inactivos).
- Integración (SQL Server real): 10 asignaciones simultáneas del mismo activo con `Task.WhenAll` → solo 1 éxito; colaborador inactivo; devolución sin asignación; ciclo asignar → devolver → reasignar con historial; activo retirado.

## Uso de IA  *(completa con tu información real)*
- Herramienta: Claude (Anthropic).
- En qué parte: <diseño del esquema, SPs, boilerplate de la API, pruebas, README: ajusta a lo que realmente hayas usado>.
- Validaciones que realicé: <p. ej. ejecuté los scripts y probé los SPs manualmente, corrí las pruebas, probé los endpoints en Swagger, revisé el candado de concurrencia>.
- Decisiones técnicas propias: <p. ej. permisos por rol, política de bloqueo, estructura de capas>.

## Tiempo invertido
<horas aproximadas: completar>

## Pendientes y riesgos
- **Frontend**: no incluido (opcional).
- **Cambio de contraseña / gestión de usuarios** y *refresh tokens*: no implementados; el JWT dura 60 min.
- **Rate limiting** es en memoria por instancia; con varias instancias haría falta un almacén compartido (p. ej. Redis). Detrás de un proxy hay que configurar `ForwardedHeaders` para ver la IP real.
- **Edición de proveedores/colaboradores** (más allá de alta, consulta y activar/desactivar) y **baja lógica de proveedores**.
- **Arrendamiento**: no hay alertas por `RentalEndDate` próximo a vencer.
- **Auditoría de login** (éxitos/fallos) solo en logs; podría persistirse en tabla.
- **Producción**: usar HTTPS, un login de SQL con permisos mínimos (solo `EXECUTE` sobre los SPs, no `sa`), y gestor de secretos.
