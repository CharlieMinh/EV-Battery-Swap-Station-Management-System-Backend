using EVBSS.Api.Data;
using EVBSS.Api.Dtos.Users;
using EVBSS.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EVBSS.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _db;

    public UsersController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Create a new user (Admin only)
    /// Admin can create accounts for Staff and Driver roles
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest req)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // Check if email already exists
        var email = req.Email.Trim().ToLower();
        if (await _db.Users.AnyAsync(u => u.Email == email))
        {
            return Conflict(new { error = "Email already exists" });
        }

        // Prevent creating another Admin (optional security measure)
        // Admin accounts should be created through a special process
        if (req.Role == Role.Admin)
        {
            return BadRequest(new { error = "Cannot create Admin accounts through this endpoint. Please contact system administrator." });
        }

        // Validate StationId for Staff role
        if (req.Role == Role.Staff)
        {
            if (!req.StationId.HasValue)
            {
                return BadRequest(new { error = "StationId is required for Staff role." });
            }

            var stationExists = await _db.Stations.AnyAsync(s => s.Id == req.StationId.Value);
            if (!stationExists)
            {
                return BadRequest(new { error = "Invalid StationId." });
            }
        }

        // Create new user
        var user = new User
        {
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            Name = req.Name.Trim(),
            Phone = req.PhoneNumber?.Trim(),
            Role = req.Role,
            Status = req.Status ?? UserStatus.Active, // Default to Active if not specified
            CreatedAt = DateTime.UtcNow,
            StationId = req.Role == Role.Staff ? req.StationId : null
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        // Load station details for the response
        await _db.Entry(user).Reference(u => u.Station).LoadAsync();

        return CreatedAtAction(
            nameof(GetUserById), 
            new { id = user.Id }, 
            new UserResponse
            {
                Id = user.Id,
                Email = user.Email,
                Name = user.Name,
                PhoneNumber = user.Phone,
                Role = user.Role.ToString(),
                Status = user.Status.ToString(),
                CreatedAt = user.CreatedAt,
                LastLogin = user.LastLogin,
                StationId = user.StationId,
                StationName = user.Station?.Name
            });
    }

    /// <summary>
    /// Get all users with optional filtering by role
    /// </summary>
    /// <param name="role">Filter by role: Driver (0), Staff (1), Admin (2)</param>
    /// <param name="search">Search by name, email, or phone</param>
    /// <param name="page">Page number (default: 1)</param>
    /// <param name="pageSize">Items per page (default: 20)</param>
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAllUsers(
        [FromQuery] Role? role = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var query = _db.Users.AsNoTracking().AsQueryable();

        // Filter by role
        if (role.HasValue)
        {
            query = query.Where(u => u.Role == role.Value);
        }

        // Search by name, email, or phone
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.Trim().ToLower();
            query = query.Where(u =>
                (u.Name != null && u.Name.ToLower().Contains(searchLower)) ||
                u.Email.ToLower().Contains(searchLower) ||
                (u.Phone != null && u.Phone.Contains(searchLower))
            );
        }

        // Get total count
        var totalItems = await query.CountAsync();

        // Get paginated data
        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new UserResponse
            {
                Id = u.Id,
                Email = u.Email,
                Name = u.Name,
                PhoneNumber = u.Phone,
                Role = u.Role.ToString(),
                Status = u.Status.ToString(),
                CreatedAt = u.CreatedAt,
                LastLogin = u.LastLogin
            })
            .ToListAsync();

        return Ok(new
        {
            page,
            pageSize,
            totalItems,
            totalPages = (int)Math.Ceiling(totalItems / (double)pageSize),
            data = users
        });
    }

    /// <summary>
    /// Get all customers/drivers (for Admin customer management and staff)
    /// </summary>
    [HttpGet("customers")]
    [Authorize(Roles = "Admin,Staff")]
    public async Task<IActionResult> GetAllCustomers(
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var query = _db.Users
            .AsNoTracking()
            .Where(u => u.Role == Role.Driver); // Only customers/drivers

        // Search
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.Trim().ToLower();
            query = query.Where(u =>
                (u.Name != null && u.Name.ToLower().Contains(searchLower)) ||
                u.Email.ToLower().Contains(searchLower) ||
                (u.Phone != null && u.Phone.Contains(searchLower))
            );
        }

        var totalItems = await query.CountAsync();

        var customers = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new CustomerResponse
            {
                Id = u.Id,
                Email = u.Email,
                Name = u.Name,
                PhoneNumber = u.Phone,
                Status = u.Status.ToString(),
                CreatedAt = u.CreatedAt,
                LastLogin = u.LastLogin,
                // Count reservations for this customer
                TotalReservations = _db.Reservations.Count(r => r.UserId == u.Id),
                CompletedReservations = _db.Reservations.Count(r => r.UserId == u.Id && r.Status == ReservationStatus.Completed)
            })
            .ToListAsync();

        return Ok(new
        {
            page,
            pageSize,
            totalItems,
            totalPages = (int)Math.Ceiling(totalItems / (double)pageSize),
            data = customers
        });
    }

    /// <summary>
    /// Get all staff members (for Admin staff management)
    /// </summary>
    [HttpGet("staff")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAllStaff(
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var query = _db.Users
            .AsNoTracking()
            .Where(u => u.Role == Role.Staff); // Only staff

        // Search
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.Trim().ToLower();
            query = query.Where(u =>
                (u.Name != null && u.Name.ToLower().Contains(searchLower)) ||
                u.Email.ToLower().Contains(searchLower) ||
                (u.Phone != null && u.Phone.Contains(searchLower))
            );
        }

        var totalItems = await query.CountAsync();

        var staff = await query
            .Include(u => u.Station) // Eager load station data
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new UserResponse
            {
                Id = u.Id,
                Email = u.Email,
                Name = u.Name,
                PhoneNumber = u.Phone,
                Role = u.Role.ToString(),
                Status = u.Status.ToString(),
                CreatedAt = u.CreatedAt,
                LastLogin = u.LastLogin,
                StationId = u.StationId,
                StationName = u.Station != null ? u.Station.Name : null
            })
            .ToListAsync();

        return Ok(new
        {
            page,
            pageSize,
            totalItems,
            totalPages = (int)Math.Ceiling(totalItems / (double)pageSize),
            data = staff
        });
    }

    /// <summary>
    /// Get staff member details by ID (Admin only)
    /// Returns detailed information including work statistics
    /// </summary>
    [HttpGet("staff/{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetStaffById(Guid id)
    {
        var staff = await _db.Users
            .AsNoTracking()
            .Include(u => u.Station) // Eager load station data
            .FirstOrDefaultAsync(u => u.Id == id && u.Role == Role.Staff);

        if (staff == null)
        {
            return NotFound(new { error = "Staff member not found" });
        }

        // Calculate date for recent activity (last 30 days)
        var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);

        // Get statistics
        var totalReservationsVerified = await _db.Reservations
            .CountAsync(r => r.VerifiedByStaffId == id);

        var recentReservationsVerified = await _db.Reservations
            .CountAsync(r => r.VerifiedByStaffId == id && r.CheckedInAt >= thirtyDaysAgo);

        // Count swap transactions where this staff was involved in any capacity
        var totalSwapTransactions = await _db.SwapTransactions
            .CountAsync(st => st.CheckedInByStaffId == id 
                           || st.BatteryIssuedByStaffId == id 
                           || st.BatteryReceivedByStaffId == id 
                           || st.CompletedByStaffId == id);

        var recentSwapTransactions = await _db.SwapTransactions
            .CountAsync(st => (st.CheckedInByStaffId == id 
                            || st.BatteryIssuedByStaffId == id 
                            || st.BatteryReceivedByStaffId == id 
                            || st.CompletedByStaffId == id) 
                           && st.StartedAt >= thirtyDaysAgo);

        return Ok(new StaffDetailResponse
        {
            Id = staff.Id,
            Email = staff.Email,
            Name = staff.Name,
            PhoneNumber = staff.Phone,
            Role = staff.Role.ToString(),
            Status = staff.Status.ToString(),
            CreatedAt = staff.CreatedAt,
            LastLogin = staff.LastLogin,
            StationId = staff.StationId,
            StationName = staff.Station?.Name,
            TotalReservationsVerified = totalReservationsVerified,
            TotalSwapTransactions = totalSwapTransactions,
            RecentReservationsVerified = recentReservationsVerified,
            RecentSwapTransactions = recentSwapTransactions
        });
    }

    /// <summary>
    /// Get user by ID (Admin only)
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetUserById(Guid id)
    {
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null)
        {
            return NotFound(new { error = "User not found" });
        }

        return Ok(new UserDetailResponse
        {
            Id = user.Id,
            Email = user.Email,
            Name = user.Name,
            PhoneNumber = user.Phone,
            Role = user.Role.ToString(),
            Status = user.Status.ToString(),
            CreatedAt = user.CreatedAt,
            LastLogin = user.LastLogin,
            // Statistics
            TotalReservations = await _db.Reservations.CountAsync(r => r.UserId == user.Id),
            CompletedReservations = await _db.Reservations.CountAsync(r => r.UserId == user.Id && r.Status == ReservationStatus.Completed),
            CancelledReservations = await _db.Reservations.CountAsync(r => r.UserId == user.Id && r.Status == ReservationStatus.Cancelled),
            TotalVehicles = await _db.Vehicles.CountAsync(v => v.UserId == user.Id)
        });
    }

    /// <summary>
    /// Update user info
    /// - Driver: Can only update their own profile (Name, Phone only)
    /// - Staff: Can update Driver profiles (Name, Phone only)
    /// - Admin: Full access to update any user
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin,Staff,Driver")]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserRequest req)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var user = await _db.Users.FindAsync(id);
        if (user == null)
        {
            return NotFound(new { error = "User not found" });
        }

        // Get current user info
        var currentUserIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var currentUserRoleClaim = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;

        if (currentUserIdClaim == null || currentUserRoleClaim == null)
        {
            return Unauthorized(new { error = "Invalid user claims" });
        }

        var currentUserId = Guid.Parse(currentUserIdClaim);
        var currentUserRole = Enum.Parse<Role>(currentUserRoleClaim);

        // Authorization logic based on role
        if (currentUserRole == Role.Driver)
        {
            // Driver can only update their own profile
            if (currentUserId != id)
            {
                return Forbid();
            }

            // Driver cannot change their own role
            if (req.Role.HasValue)
            {
                return BadRequest(new { error = "You are not allowed to change your role" });
            }
        }
        else if (currentUserRole == Role.Staff)
        {
            // Staff can only update Driver profiles
            if (user.Role != Role.Driver)
            {
                return Forbid();
            }

            // Staff cannot change roles or status
            if (req.Role.HasValue)
            {
                return BadRequest(new { error = "Staff members are not allowed to change user roles" });
            }

            if (req.Status.HasValue)
            {
                return BadRequest(new { error = "Staff members are not allowed to change user status" });
            }
        }
        // Admin has full access - no additional checks needed

        // Update fields
        if (!string.IsNullOrWhiteSpace(req.Name))
        {
            user.Name = req.Name.Trim();
        }

        if (!string.IsNullOrWhiteSpace(req.PhoneNumber))
        {
            user.Phone = req.PhoneNumber.Trim();
        }

        // Only Admin can change roles
        if (req.Role.HasValue && currentUserRole == Role.Admin)
        {
            user.Role = req.Role.Value;
        }

        // Only Admin can change status
        if (req.Status.HasValue && currentUserRole == Role.Admin)
        {
            user.Status = req.Status.Value;
        }

        // Only Admin can change the station
        if (req.StationId.HasValue && currentUserRole == Role.Admin)
        {
            // Check if the station exists before assigning
            var stationExists = await _db.Stations.AnyAsync(s => s.Id == req.StationId.Value);
            if (!stationExists)
            {
                return BadRequest(new { error = "Invalid StationId." });
            }
            user.StationId = req.StationId.Value;
        }
        // Allow admin to un-assign a staff from a station
        else if (!req.StationId.HasValue && currentUserRole == Role.Admin && user.Role == Role.Staff)
        {
            user.StationId = null;
        }

        await _db.SaveChangesAsync();

        // Load station details for the response
        await _db.Entry(user).Reference(u => u.Station).LoadAsync();

        return Ok(new UserResponse
        {
            Id = user.Id,
            Email = user.Email,
            Name = user.Name,
            PhoneNumber = user.Phone,
            Role = user.Role.ToString(),
            Status = user.Status.ToString(),
            CreatedAt = user.CreatedAt,
            LastLogin = user.LastLogin,
            StationId = user.StationId,
            StationName = user.Station?.Name
        });
    }

    /// <summary>
    /// Delete user (Admin only)
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null)
        {
            return NotFound(new { error = "User not found" });
        }

        // Don't allow deleting yourself
        var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (currentUserId != null && Guid.Parse(currentUserId) == id)
        {
            return BadRequest(new { error = "Cannot delete your own account" });
        }

        // Check if user has active reservations
        var hasActiveReservations = await _db.Reservations
            .AnyAsync(r => r.UserId == id && (r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.CheckedIn));

        if (hasActiveReservations)
        {
            return BadRequest(new { error = "Cannot delete user with active reservations. Please cancel or complete them first." });
        }

        _db.Users.Remove(user);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// Get user statistics (Admin only)
    /// </summary>
    [HttpGet("statistics")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetUserStatistics()
    {
        var totalUsers = await _db.Users.CountAsync();
        var totalCustomers = await _db.Users.CountAsync(u => u.Role == Role.Driver);
        var totalStaff = await _db.Users.CountAsync(u => u.Role == Role.Staff);
        var totalAdmins = await _db.Users.CountAsync(u => u.Role == Role.Admin);

        // Active users (logged in within last 30 days)
        var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
        var activeUsers = await _db.Users.CountAsync(u => u.LastLogin >= thirtyDaysAgo);

        // New users this month
        var firstDayOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var newUsersThisMonth = await _db.Users.CountAsync(u => u.CreatedAt >= firstDayOfMonth);

        return Ok(new
        {
            totalUsers,
            totalCustomers,
            totalStaff,
            totalAdmins,
            activeUsers,
            newUsersThisMonth
        });
    }

    /// <summary>
    /// Change user's own password
    /// - Any authenticated user can change their own password
    /// - Requires current password verification
    /// </summary>
    [HttpPost("change-password")]
    [Authorize]
    public async Task<ActionResult<ChangePasswordResponse>> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // Get current user ID from JWT claims
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new { error = "Invalid user token" });
        }

        // Get user from database
        var user = await _db.Users.FindAsync(userId);
        if (user == null)
        {
            return NotFound(new { error = "User not found" });
        }

        // Check if user uses email/password authentication
        if (user.AuthMethod != AuthMethod.Local)
        {
            return BadRequest(new { error = "Password change is only available for email/password accounts. Google accounts should change password through Google." });
        }

        // Verify current password
        if (string.IsNullOrEmpty(user.PasswordHash))
        {
            return BadRequest(new { error = "No password set for this account" });
        }

        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
        {
            return BadRequest(new { error = "Current password is incorrect" });
        }

        // Hash new password
        var newPasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);

        // Update password
        user.PasswordHash = newPasswordHash;
        await _db.SaveChangesAsync();

        return Ok(new ChangePasswordResponse
        {
            Message = "Password changed successfully",
            ChangedAt = DateTime.UtcNow
        });
    }
}
