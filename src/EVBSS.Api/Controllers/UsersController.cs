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
    /// Get all customers/drivers (for Admin customer management)
    /// </summary>
    [HttpGet("customers")]
    [Authorize(Roles = "Admin")]
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
    /// Update user info (Admin only)
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
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

        // Update fields
        if (!string.IsNullOrWhiteSpace(req.Name))
        {
            user.Name = req.Name.Trim();
        }

        if (!string.IsNullOrWhiteSpace(req.PhoneNumber))
        {
            user.Phone = req.PhoneNumber.Trim();
        }

        if (req.Role.HasValue)
        {
            user.Role = req.Role.Value;
        }

        await _db.SaveChangesAsync();

        return Ok(new UserResponse
        {
            Id = user.Id,
            Email = user.Email,
            Name = user.Name,
            PhoneNumber = user.Phone,
            Role = user.Role.ToString(),
            CreatedAt = user.CreatedAt,
            LastLogin = user.LastLogin
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
}
