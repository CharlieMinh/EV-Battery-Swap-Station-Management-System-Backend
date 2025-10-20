using EVBSS.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EVBSS.Api.Controllers;

[ApiController]
[Route("api/v1/subscription-plans")]
public class SubscriptionPlansController : ControllerBase
{
    private readonly AppDbContext _db;
    public SubscriptionPlansController(AppDbContext db) => _db = db;

    /// <summary>
    /// Get all subscription plans (Simplified fixed-price model)
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var plans = await _db.SubscriptionPlans
            .AsNoTracking()
            .Include(sp => sp.BatteryModel)
            .Where(sp => sp.IsActive)
            .OrderBy(sp => sp.MonthlyPrice)  // ✅ Sort by fixed price
            .Select(sp => new
            {
                sp.Id,
                sp.Name,
                sp.Description,
                
                // ✅ SIMPLIFIED PRICING
                MonthlyPrice = sp.MonthlyPrice,
                MaxSwapsPerMonth = sp.MaxSwapsPerMonth,
                
                // ✅ NO DEPOSIT
                RequiresDeposit = sp.RequiresDeposit,  // Always false
                DepositAmount = sp.DepositAmount,      // Always 0
                
                // ✅ BENEFITS & REFUND
                Benefits = sp.Benefits,
                RefundPolicy = sp.RefundPolicy,
                
                BatteryModel = new
                {
                    sp.BatteryModel.Id,
                    sp.BatteryModel.Name,
                    sp.BatteryModel.Voltage,
                    sp.BatteryModel.CapacityWh
                },
                
                // ✅ SIMPLIFIED INFO
                BillingCycleDays = 30,  // Fixed 30-day cycle
                
                sp.CreatedAt
            })
            .ToListAsync();

        return Ok(plans);
    }

    /// <summary>
    /// Get subscription plan by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var plan = await _db.SubscriptionPlans
            .AsNoTracking()
            .Include(sp => sp.BatteryModel)
            .Where(sp => sp.Id == id && sp.IsActive)
            .Select(sp => new
            {
                sp.Id,
                sp.Name,
                sp.Description,
                
                // ✅ SIMPLIFIED PRICING
                MonthlyPrice = sp.MonthlyPrice,
                MaxSwapsPerMonth = sp.MaxSwapsPerMonth,
                
                // ✅ NO DEPOSIT
                RequiresDeposit = sp.RequiresDeposit,
                DepositAmount = sp.DepositAmount,
                
                // ✅ BENEFITS & REFUND
                Benefits = sp.Benefits,
                RefundPolicy = sp.RefundPolicy,
                
                BatteryModel = new
                {
                    sp.BatteryModel.Id,
                    sp.BatteryModel.Name,
                    sp.BatteryModel.Voltage,
                    sp.BatteryModel.CapacityWh
                },
                
                BillingCycleDays = 30,
                
                sp.CreatedAt
            })
            .FirstOrDefaultAsync();

        if (plan == null)
            return NotFound(new { error = new { code = "PLAN_NOT_FOUND", message = "Subscription plan not found" } });

        return Ok(plan);
    }

    /// <summary>
    /// ❌ DEPRECATED - No longer calculate by km, use fixed monthly price
    /// Get plan pricing info
    /// </summary>
    [HttpGet("{id:guid}/pricing")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPricing(Guid id)
    {
        var plan = await _db.SubscriptionPlans.FindAsync(id);
        if (plan == null || !plan.IsActive)
            return NotFound(new { error = new { code = "PLAN_NOT_FOUND", message = "Subscription plan not found" } });

        // ✅ SIMPLIFIED: Fixed price, no calculation needed
        var result = new
        {
            PlanName = plan.Name,
            MonthlyPrice = plan.MonthlyPrice,
            MaxSwapsPerMonth = plan.MaxSwapsPerMonth,
            PricePerSwap = plan.MaxSwapsPerMonth.HasValue 
                ? Math.Round(plan.MonthlyPrice / plan.MaxSwapsPerMonth.Value, 0)
                : 0,
            
            // ✅ NO TAX
            TaxAmount = 0m,
            TotalAmount = plan.MonthlyPrice,
            
            Benefits = plan.Benefits,
            RefundPolicy = plan.RefundPolicy,
            
            Note = "Giá đã bao gồm tất cả, không có thuế hoặc phí bổ sung"
        };

        return Ok(result);
    }
}