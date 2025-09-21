using EVBSS.Api.Data;
using EVBSS.Api.Dtos.Common;
using EVBSS.Api.Dtos.Users;
using EVBSS.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BCrypt.Net;

namespace EVBSS.Api.Controllers;

[ApiController]
[Route("api/v1/admin/users")]
[Authorize(Roles = "Admin")]
public class AdminUsersController : ControllerBase
{
    private readonly AppDbContext _db;
    public AdminUsersController(AppDbContext db) => _db = db;

    // GET /api/v1/admin/users?page=1&pageSize=20&role=Driver
    [HttpGet]
    public async Task<ActionResult<PagedResult<AdminUserDto>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Role? role = null)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var q = _db.Users.AsNoTracking();
        if (role.HasValue)
            q = q.Where(u => u.Role == role.Value);

        var total = await q.CountAsync();

        var users = await q.OrderByDescending(u => u.CreatedAt)
                          .Skip((page - 1) * pageSize)
                          .Take(pageSize)
                          .ToListAsync();

        var items = new List<AdminUserDto>();
        foreach (var user in users)
        {
            var activeSubscription = await _db.UserSubscriptions
                .Include(us => us.SubscriptionPlan)
                .Where(us => us.UserId == user.Id && us.Status == UserSubscriptionStatus.Active && us.EndDate > DateTime.UtcNow)
                .FirstOrDefaultAsync();

            items.Add(new AdminUserDto(
                user.Id,
                user.Email,
                user.Name,
                user.Phone,
                user.Role,
                user.CreatedAt,
                user.LastLogin,
                activeSubscription?.SubscriptionPlan.Name,
                activeSubscription?.EndDate,
                activeSubscription?.Status
            ));
        }

        return new PagedResult<AdminUserDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            Total = total
        };
    }

    // GET /api/v1/admin/users/{id}
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AdminUserDto>> GetById(Guid id)
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
        if (user is null)
            return NotFound(new { error = new { code = "USER_NOT_FOUND", message = "User not found" } });

        var activeSubscription = await _db.UserSubscriptions
            .Include(us => us.SubscriptionPlan)
            .Where(us => us.UserId == user.Id && us.Status == UserSubscriptionStatus.Active && us.EndDate > DateTime.UtcNow)
            .FirstOrDefaultAsync();

        return new AdminUserDto(
            user.Id,
            user.Email,
            user.Name,
            user.Phone,
            user.Role,
            user.CreatedAt,
            user.LastLogin,
            activeSubscription?.SubscriptionPlan.Name,
            activeSubscription?.EndDate,
            activeSubscription?.Status
        );
    }

    // POST /api/v1/admin/users
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest req)
    {
        var existingUser = await _db.Users.AnyAsync(u => u.Email == req.Email);
        if (existingUser)
            return BadRequest(new { error = new { code = "EMAIL_EXISTS", message = "Email already exists" } });

        var user = new User
        {
            Email = req.Email.Trim().ToLower(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            Name = req.Name?.Trim(),
            Phone = req.Phone?.Trim(),
            Role = req.Role
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return Created($"/api/v1/admin/users/{user.Id}", new { user.Id });
    }

    // PUT /api/v1/admin/users/{id}
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserRequest req)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user is null)
            return NotFound(new { error = new { code = "USER_NOT_FOUND", message = "User not found" } });

        if (!string.IsNullOrWhiteSpace(req.Email))
        {
            var emailExists = await _db.Users.AnyAsync(u => u.Email == req.Email && u.Id != id);
            if (emailExists)
                return BadRequest(new { error = new { code = "EMAIL_EXISTS", message = "Email already exists" } });
            
            user.Email = req.Email.Trim().ToLower();
        }

        if (!string.IsNullOrWhiteSpace(req.Name))
            user.Name = req.Name.Trim();

        if (!string.IsNullOrWhiteSpace(req.Phone))
            user.Phone = req.Phone.Trim();

        if (req.Role.HasValue)
            user.Role = req.Role.Value;

        await _db.SaveChangesAsync();

        return Ok(new { message = "User updated successfully", userId = user.Id });
    }

    // DELETE /api/v1/admin/users/{id}
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user is null)
            return NotFound(new { error = new { code = "USER_NOT_FOUND", message = "User not found" } });

        // Check if user has active reservations or subscriptions
        var hasActiveReservations = await _db.Reservations.AnyAsync(r => r.UserId == id && (r.Status == ReservationStatus.Held || r.Status == ReservationStatus.Confirmed));
        var hasActiveSubscriptions = await _db.UserSubscriptions.AnyAsync(us => us.UserId == id && us.Status == UserSubscriptionStatus.Active);

        if (hasActiveReservations || hasActiveSubscriptions)
            return BadRequest(new { error = new { code = "USER_HAS_ACTIVE_DATA", message = "Cannot delete user with active reservations or subscriptions" } });

        _db.Users.Remove(user);
        await _db.SaveChangesAsync();

        return Ok(new { message = "User deleted successfully" });
    }

    // POST /api/v1/admin/users/{id}/assign-subscription
    [HttpPost("{id:guid}/assign-subscription")]
    public async Task<IActionResult> AssignSubscription(Guid id, [FromBody] AssignSubscriptionRequest req)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user is null)
            return NotFound(new { error = new { code = "USER_NOT_FOUND", message = "User not found" } });

        var subscriptionPlan = await _db.SubscriptionPlans.FirstOrDefaultAsync(sp => sp.Id == req.SubscriptionPlanId);
        if (subscriptionPlan is null)
            return NotFound(new { error = new { code = "SUBSCRIPTION_PLAN_NOT_FOUND", message = "Subscription plan not found" } });

        // Cancel existing active subscription
        var existingSubscription = await _db.UserSubscriptions
            .Where(us => us.UserId == id && us.Status == UserSubscriptionStatus.Active)
            .FirstOrDefaultAsync();

        if (existingSubscription != null)
        {
            existingSubscription.Status = UserSubscriptionStatus.Cancelled;
        }

        var startDate = req.StartDate ?? DateTime.UtcNow;
        var endDate = startDate.AddDays(subscriptionPlan.DurationDays);

        var userSubscription = new UserSubscription
        {
            UserId = id,
            SubscriptionPlanId = req.SubscriptionPlanId,
            StartDate = startDate,
            EndDate = endDate,
            Status = UserSubscriptionStatus.Active
        };

        _db.UserSubscriptions.Add(userSubscription);
        await _db.SaveChangesAsync();

        return Ok(new { message = "Subscription assigned successfully", subscriptionId = userSubscription.Id });
    }

    // PUT /api/v1/admin/users/{id}/cancel-subscription
    [HttpPut("{id:guid}/cancel-subscription")]
    public async Task<IActionResult> CancelSubscription(Guid id)
    {
        var activeSubscription = await _db.UserSubscriptions
            .Where(us => us.UserId == id && us.Status == UserSubscriptionStatus.Active)
            .FirstOrDefaultAsync();

        if (activeSubscription is null)
            return NotFound(new { error = new { code = "NO_ACTIVE_SUBSCRIPTION", message = "No active subscription found for this user" } });

        activeSubscription.Status = UserSubscriptionStatus.Cancelled;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Subscription cancelled successfully" });
    }
}