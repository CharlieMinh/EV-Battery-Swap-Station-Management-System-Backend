# EV Battery Swap Station Management System - Backend API

This is an ASP.NET Core Web API for managing Electric Vehicle Battery Swap Stations with JWT authentication using HttpOnly cookies.

## Features

- **User Authentication**: JWT-based authentication with secure HttpOnly cookies
- **Role-based Authorization**: Support for Driver, Staff, and Admin roles
- **Station Management**: API endpoints for managing battery swap stations
- **Secure Password Handling**: BCrypt password hashing
- **CORS Support**: Configured for frontend development

## Prerequisites

- .NET 8.0 SDK
- SQL Server (LocalDB for development)

## Getting Started

### 1. Configuration

The application uses the following configuration sources (in order of priority):

1. **Environment Variables** (Production)
2. **appsettings.json** (Development)

#### Required Environment Variables for Production:

```bash
JWT_SECRET_KEY=your-super-secret-jwt-key-must-be-at-least-32-characters-long
```

#### Database Connection:

The connection string is configured in `appsettings.json`:
```json
{
  "ConnectionStrings": {
    "Default": "Server=(localdb)\\mssqllocaldb;Database=EVBSS_Dev;Trusted_Connection=true;MultipleActiveResultSets=true"
  }
}
```

### 2. Running the Application

```bash
# Clone the repository
git clone <repository-url>
cd EV-Battery-Swap-Station-Management-System-Backend

# Restore dependencies
dotnet restore

# Build the project
dotnet build

# Run the application
dotnet run --project src/EVBSS.Api
```

The API will be available at `http://localhost:8080` (or the configured port).

### 3. Database Setup

The application uses Entity Framework Core with automatic migrations. The database will be created automatically when the application starts.

## Security Considerations

### JWT Configuration

- **Secret Key**: Must be at least 32 characters long
- **Environment Variables**: Use `JWT_SECRET_KEY` environment variable in production
- **Token Expiration**: Configurable via `Jwt:ExpireMinutes` (default: 60 minutes)

### Cookie Security

The application uses secure HttpOnly cookies with the following settings:

**Production:**
- `HttpOnly: true` - Prevents XSS attacks
- `Secure: true` - HTTPS only
- `SameSite: Strict` - CSRF protection

**Development:**
- `HttpOnly: true`
- `Secure: false` - Allows HTTP for local development
- `SameSite: None` - Allows cross-origin requests

### Password Security

- Passwords are hashed using BCrypt with automatic salt generation
- Minimum password length: 6 characters (configurable in validation attributes)

## API Endpoints

### Authentication

- `POST /api/v1/auth/register` - User registration
- `POST /api/v1/auth/login` - User login (sets HttpOnly cookie)
- `POST /api/v1/auth/logout` - User logout (clears cookie)
- `GET /api/v1/auth/me` - Get current user information

### Health Check

- `GET /api/health/ping` - Health check endpoint

### Stations

- `GET /api/stations` - Get all stations
- `GET /api/stations/{id}` - Get station by ID

## Error Handling

The API returns consistent error responses in the following format:

```json
{
  "error": {
    "code": "ERROR_CODE",
    "message": "Human-readable error message",
    "details": {} // Optional additional details
  }
}
```

Common error codes:
- `EMAIL_EXISTS` - Email already registered
- `INVALID_CREDENTIALS` - Invalid login credentials
- `VALIDATION_ERROR` - Input validation failed
- `REGISTRATION_FAILED` - Registration process failed
- `LOGIN_FAILED` - Login process failed

## Development Notes

### CORS Configuration

The application is configured to accept requests from:
- `http://localhost:3000` (React development server)
- `http://localhost:5173` (Vite development server)

### Swagger/OpenAPI

Swagger UI is available at `/swagger` for API documentation and testing.

## Deployment

### Production Checklist

1. **Set Environment Variables:**
   ```bash
   export JWT_SECRET_KEY="your-production-secret-key-at-least-32-chars"
   export ASPNETCORE_ENVIRONMENT="Production"
   ```

2. **Update Connection String:**
   - Configure production database connection string
   - Use Azure Key Vault or similar for sensitive configuration

3. **HTTPS Configuration:**
   - Enable HTTPS redirection in production
   - Update cookie settings for secure environments

4. **Security Headers:**
   - Consider adding security headers middleware
   - Configure CORS for production domains only

## Contributing

1. Follow the existing code style and patterns
2. Add appropriate error handling
3. Include validation for user inputs
4. Update this README for any new features or configuration changes

## License

[Your License Here]