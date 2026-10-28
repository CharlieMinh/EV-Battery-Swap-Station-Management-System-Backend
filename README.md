# EV Battery Swap Station Management - Backend

ASP.NET Core backend for managing EV battery swap station operations: reservations, battery inventory, subscriptions, payments, and staff notifications.

[API reference](API_DOCUMENTATION_TABLE.md) · [Frontend repository](https://github.com/CharlieMinh/EV-Battery-Swap-Station-Management-System-Frontend) · [Tiếng Việt](#tiếng-việt)

## Main workflows

- **Reservations:** station and time-slot availability, reservation creation, and swap workflows.
- **Station operations:** station records, battery models and units, inventory, and stock requests.
- **Subscriptions and payments:** subscription plans, payment records, and VNPay integration.
- **Authentication:** JWT bearer authentication and role-based authorization.
- **Notifications:** a SignalR notification hub and background processing.
- **Vehicle documents:** AWS Rekognition integration for vehicle registration recognition.

These describe the repository's implemented areas. External integrations need their own configuration and validation in the target environment.

## Technology

| Area | Stack |
| --- | --- |
| Runtime | .NET 9 / ASP.NET Core |
| Data | Entity Framework Core 9 / SQL Server |
| Authentication | JWT bearer / BCrypt |
| Real-time updates | SignalR |
| Integrations | VNPay / AWS Rekognition / Google authentication |
| API documentation | Swagger / OpenAPI |

## Source organization

The application is organized within `src/EVBSS.Api`:

```text
src/EVBSS.Api/
  Controllers/      HTTP endpoints
  Services/         Business workflows and external integrations
  Data/             EF Core database access
  Models/           Application data models
  Configuration/    Typed configuration
  Hubs/             SignalR notifications
  Program.cs        Dependency registration and request pipeline
```

A typical request flows from a controller into a service and then into EF Core / SQL Server. Some workflows also call VNPay or AWS Rekognition. The notification hub is mapped at `/notificationHub`.

## Local development

### Prerequisites

- .NET 9 SDK.
- A dedicated development SQL Server database.
- Configuration for the database and JWT authentication.
- Separate credentials/configuration for the external integrations you intend to exercise.

### Configuration

Use local .NET user secrets or environment variables. Do not commit real keys or service credentials.

| Required at startup | Configuration key | Environment variable |
| --- | --- | --- |
| Database connection | `ConnectionStrings:Default` | `ConnectionStrings__Default` |
| JWT signing key | `Jwt:Key` | `Jwt__Key` |
| Token issuer | `Jwt:Issuer` | `Jwt__Issuer` |
| Token audience | `Jwt:Audience` | `Jwt__Audience` |

The integrations use the `Vnpay` and `AWS` configuration sections. Configure only the services you plan to use, and inspect their configuration classes for the complete fields.

### Build and run

From the repository root, after configuring the values above:

```powershell
dotnet restore src/EVBSS.Api/EVBSS.Api.csproj
dotnet build src/EVBSS.Api/EVBSS.Api.csproj
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project src/EVBSS.Api/EVBSS.Api.csproj --urls http://localhost:5080
```

**Database startup behavior:** `Program.cs` applies EF Core migrations and seeds initial records during startup. Use an isolated development database and review the seed code before running.

Swagger is enabled in Development at [http://localhost:5080/swagger](http://localhost:5080/swagger). Live payment callbacks and cloud integrations require separate reachable endpoints and service configuration; a local startup alone does not verify them.

## Explore the implementation

- [HTTP endpoint table](API_DOCUMENTATION_TABLE.md)
- [Startup and dependency registration](src/EVBSS.Api/Program.cs)
- [Time-slot reservations](src/EVBSS.Api/Services/SlotReservationService.cs)
- [VNPay integration](docs/FRONTEND_VNPAY_INTEGRATION.md)
- [Battery stock request workflow](docs/BATTERY_STOCK_REQUEST_SUMMARY.md)

## Tiếng Việt

Backend quản lý trạm đổi pin xe điện, phát triển bằng ASP.NET Core 9 và Entity Framework Core với SQL Server. Các nhóm nghiệp vụ chính gồm đặt lịch theo khung giờ, quản lý tồn kho pin, gói đăng ký, thanh toán VNPay và cập nhật thông báo qua SignalR.

Mã nguồn tập trung trong project `src/EVBSS.Api`, chia theo controller, service, data, model và cấu hình. Xem bảng API và các tài liệu phía trên để theo dõi từng luồng nghiệp vụ.

Để chạy cục bộ, cần .NET 9 SDK, cơ sở dữ liệu SQL Server riêng cho phát triển và cấu hình JWT. Ứng dụng tự chạy migration và seed dữ liệu khi khởi động; hãy kiểm tra mã seed và không trỏ vào cơ sở dữ liệu dùng chung hoặc production. Các tích hợp bên ngoài cần được cấu hình và kiểm tra riêng.
