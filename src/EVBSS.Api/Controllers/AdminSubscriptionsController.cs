using EVBSS.Api.Data;
using EVBSS.Api.Dtos.Common;
using EVBSS.Api.Dtos.Subscriptions;
using EVBSS.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EVBSS.Api.Controllers;

[ApiController]
[Route("api/v1/admin/subscriptions")]
[Authorize(Roles = "Admin")]
public class AdminSubscriptionsController : ControllerBase
{
    private readonly AppDbContext _db;
    public AdminSubscriptionsController(AppDbContext db) => _db = db;

    // GET /api/v1/admin/subscriptions?page=1&pageSize=20&includeInactive=true
    [HttpGet]
    public async Task<ActionResult<PagedResult<SubscriptionPlanDto>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool includeInactive = true)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var q = _db.SubscriptionPlans.AsNoTracking();
        if (!includeInactive)
            q = q.Where(sp => sp.IsActive);

        var total = await q.CountAsync();

        var items = await q.OrderByDescending(sp => sp.CreatedAt)
                          .Skip((page - 1) * pageSize)
                          .Take(pageSize)
                          .Select(sp => new SubscriptionPlanDto(
                              sp.Id,
                              sp.Name,
                              sp.Description,
                              sp.Price,
                              sp.DurationDays,
                              sp.SwapsPerDay,
                              sp.IsActive,
                              sp.CreatedAt
                          ))
                          .ToListAsync();

        return new PagedResult<SubscriptionPlanDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            Total = total
        };
    }

    // GET /api/v1/admin/subscriptions/{id}
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SubscriptionPlanDto>> GetById(Guid id)
    {
        var plan = await _db.SubscriptionPlans.AsNoTracking().FirstOrDefaultAsync(sp => sp.Id == id);
        if (plan is null)
            return NotFound(new { error = new { code = "SUBSCRIPTION_PLAN_NOT_FOUND", message = "Subscription plan not found" } });

        return new SubscriptionPlanDto(
            plan.Id,
            plan.Name,
            plan.Description,
            plan.Price,
            plan.DurationDays,
            plan.SwapsPerDay,
            plan.IsActive,
            plan.CreatedAt
        );
    }

    // POST /api/v1/admin/subscriptions
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSubscriptionPlanRequest req)
    {
        var plan = new SubscriptionPlan
        {
            Name = req.Name.Trim(),
            Description = req.Description.Trim(),
            Price = req.Price,
            DurationDays = req.DurationDays,
            SwapsPerDay = req.SwapsPerDay,
            IsActive = req.IsActive
        };

        _db.SubscriptionPlans.Add(plan);
        await _db.SaveChangesAsync();

        return Created($"/api/v1/admin/subscriptions/{plan.Id}", new { plan.Id });
    }

    // PUT /api/v1/admin/subscriptions/{id}
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSubscriptionPlanRequest req)
    {
        var plan = await _db.SubscriptionPlans.FirstOrDefaultAsync(sp => sp.Id == id);
        if (plan is null)
            return NotFound(new { error = new { code = "SUBSCRIPTION_PLAN_NOT_FOUND", message = "Subscription plan not found" } });

        if (!string.IsNullOrWhiteSpace(req.Name))
            plan.Name = req.Name.Trim();

        if (!string.IsNullOrWhiteSpace(req.Description))
            plan.Description = req.Description.Trim();

        if (req.Price.HasValue)
            plan.Price = req.Price.Value;

        if (req.DurationDays.HasValue)
            plan.DurationDays = req.DurationDays.Value;

        if (req.SwapsPerDay.HasValue)
            plan.SwapsPerDay = req.SwapsPerDay.Value;

        if (req.IsActive.HasValue)
            plan.IsActive = req.IsActive.Value;

        await _db.SaveChangesAsync();

        return Ok(new { message = "Subscription plan updated successfully", planId = plan.Id });
    }

    // DELETE /api/v1/admin/subscriptions/{id}
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var plan = await _db.SubscriptionPlans.FirstOrDefaultAsync(sp => sp.Id == id);
        if (plan is null)
            return NotFound(new { error = new { code = "SUBSCRIPTION_PLAN_NOT_FOUND", message = "Subscription plan not found" } });

        // Check if plan has active subscriptions
        var hasActiveSubscriptions = await _db.UserSubscriptions.AnyAsync(us => us.SubscriptionPlanId == id && us.Status == UserSubscriptionStatus.Active);
        if (hasActiveSubscriptions)
            return BadRequest(new { error = new { code = "PLAN_HAS_ACTIVE_SUBSCRIPTIONS", message = "Cannot delete plan with active subscriptions" } });

        _db.SubscriptionPlans.Remove(plan);
        await _db.SaveChangesAsync();

        return Ok(new { message = "Subscription plan deleted successfully" });
    }

    // PUT /api/v1/admin/subscriptions/{id}/deactivate
    [HttpPut("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        var plan = await _db.SubscriptionPlans.FirstOrDefaultAsync(sp => sp.Id == id);
        if (plan is null)
            return NotFound(new { error = new { code = "SUBSCRIPTION_PLAN_NOT_FOUND", message = "Subscription plan not found" } });

        plan.IsActive = false;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Subscription plan deactivated successfully", planId = plan.Id });
    }

    // PUT /api/v1/admin/subscriptions/{id}/activate
    [HttpPut("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id)
    {
        var plan = await _db.SubscriptionPlans.FirstOrDefaultAsync(sp => sp.Id == id);
        if (plan is null)
            return NotFound(new { error = new { code = "SUBSCRIPTION_PLAN_NOT_FOUND", message = "Subscription plan not found" } });

        plan.IsActive = true;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Subscription plan activated successfully", planId = plan.Id });
    }

    // GET /api/v1/admin/subscriptions/user-subscriptions?page=1&pageSize=20&status=Active
    [HttpGet("user-subscriptions")]
    public async Task<ActionResult<PagedResult<UserSubscriptionDto>>> GetUserSubscriptions(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] UserSubscriptionStatus? status = null)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var q = _db.UserSubscriptions
            .Include(us => us.User)
            .Include(us => us.SubscriptionPlan)
            .AsNoTracking();

        if (status.HasValue)
            q = q.Where(us => us.Status == status.Value);

        var total = await q.CountAsync();

        var items = await q.OrderByDescending(us => us.CreatedAt)
                          .Skip((page - 1) * pageSize)
                          .Take(pageSize)
                          .Select(us => new UserSubscriptionDto(
                              us.Id,
                              us.UserId,
                              us.User.Email,
                              us.User.Name,
                              new SubscriptionPlanDto(
                                  us.SubscriptionPlan.Id,
                                  us.SubscriptionPlan.Name,
                                  us.SubscriptionPlan.Description,
                                  us.SubscriptionPlan.Price,
                                  us.SubscriptionPlan.DurationDays,
                                  us.SubscriptionPlan.SwapsPerDay,
                                  us.SubscriptionPlan.IsActive,
                                  us.SubscriptionPlan.CreatedAt
                              ),
                              us.StartDate,
                              us.EndDate,
                              us.Status,
                              us.SwapsUsed,
                              us.CreatedAt
                          ))
                          .ToListAsync();

        return new PagedResult<UserSubscriptionDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            Total = total
        };
    }
}