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
    /// Get all subscription plans (VinFast-based pricing)
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var plans = await _db.SubscriptionPlans
            .AsNoTracking()
            .Include(sp => sp.BatteryModel)
            .Where(sp => sp.IsActive)
            .OrderBy(sp => sp.MonthlyFeeUnder1500Km)
            .Select(sp => new
            {
                sp.Id,
                sp.Name,
                sp.Description,
                Pricing = new
                {
                    Under1500Km = sp.MonthlyFeeUnder1500Km,
                    From1500To3000Km = sp.MonthlyFee1500To3000Km,
                    Over3000Km = sp.MonthlyFeeOver3000Km,
                    DepositRequired = sp.DepositAmount
                },
                BatteryModel = new
                {
                    sp.BatteryModel.Id,
                    sp.BatteryModel.Name,
                    sp.BatteryModel.Voltage,
                    sp.BatteryModel.CapacityWh
                },
                BusinessRules = new
                {
                    BillingDay = sp.BillingCycleDay,
                    OverdueInterestRate = sp.OverdueInterestRate,
                    MaxOverdueMonths = sp.MaxOverdueMonths
                },
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
                Pricing = new
                {
                    Under1500Km = sp.MonthlyFeeUnder1500Km,
                    From1500To3000Km = sp.MonthlyFee1500To3000Km,
                    Over3000Km = sp.MonthlyFeeOver3000Km,
                    DepositRequired = sp.DepositAmount
                },
                BatteryModel = new
                {
                    sp.BatteryModel.Id,
                    sp.BatteryModel.Name,
                    sp.BatteryModel.Voltage,
                    sp.BatteryModel.CapacityWh
                },
                BusinessRules = new
                {
                    BillingDay = sp.BillingCycleDay,
                    OverdueInterestRate = sp.OverdueInterestRate,
                    MaxOverdueMonths = sp.MaxOverdueMonths
                },
                sp.CreatedAt
            })
            .FirstOrDefaultAsync();

        if (plan == null)
            return NotFound(new { error = new { code = "PLAN_NOT_FOUND", message = "Subscription plan not found" } });

        return Ok(plan);
    }

    /// <summary>
    /// Calculate monthly fee based on km usage
    /// </summary>
    [HttpPost("{id:guid}/calculate-fee")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CalculateFee(Guid id, [FromBody] CalculateFeeRequest request)
    {
        var plan = await _db.SubscriptionPlans.FindAsync(id);
        if (plan == null || !plan.IsActive)
            return NotFound(new { error = new { code = "PLAN_NOT_FOUND", message = "Subscription plan not found" } });

        decimal monthlyFee;
        string feeCategory;

        if (request.KmUsed < 1500)
        {
            monthlyFee = plan.MonthlyFeeUnder1500Km;
            feeCategory = "Under 1500km";
        }
        else if (request.KmUsed <= 3000)
        {
            monthlyFee = plan.MonthlyFee1500To3000Km;
            feeCategory = "1500-3000km";
        }
        else
        {
            monthlyFee = plan.MonthlyFeeOver3000Km;
            feeCategory = "Over 3000km";
        }

        var result = new
        {
            PlanName = plan.Name,
            KmUsed = request.KmUsed,
            FeeCategory = feeCategory,
            MonthlyFee = monthlyFee,
            VATAmount = monthlyFee * 0.1m, // 10% VAT
            TotalAmount = monthlyFee * 1.1m,
            CalculatedAt = DateTime.UtcNow
        };

        return Ok(result);
    }
}

public class CalculateFeeRequest
{
    public int KmUsed { get; set; }
}