using EVBSS.Api.Data;
using EVBSS.Api.Dtos.Invoices;
using EVBSS.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace EVBSS.Api.Services;

public interface IInvoiceService
{
    Task<IEnumerable<InvoiceDto>> GetUserInvoicesAsync(Guid userId);
    Task<InvoiceDto?> GetInvoiceByIdAsync(Guid invoiceId, Guid userId);
    Task<Invoice> CreateSubscriptionDepositInvoiceAsync(Guid userId, UserSubscription subscription);
    // ✅ SIMPLIFIED: No km parameter
    Task<Invoice> CreateMonthlySubscriptionInvoiceAsync(UserSubscription subscription, DateTime billingPeriodStart, DateTime billingPeriodEnd);
}

public class InvoiceService : IInvoiceService
{
    private readonly AppDbContext _context;
    private readonly ILogger<InvoiceService> _logger;

    public InvoiceService(AppDbContext context, ILogger<InvoiceService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IEnumerable<InvoiceDto>> GetUserInvoicesAsync(Guid userId)
    {
        var invoices = await _context.Invoices
            .AsNoTracking()
            .Include(i => i.UserSubscription)
                .ThenInclude(us => us!.SubscriptionPlan)
            .Include(i => i.UserSubscription)
                .ThenInclude(us => us!.Vehicle)
            .Where(i => i.UserId == userId)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();

        return invoices.Select(MapToDto);
    }

    public async Task<InvoiceDto?> GetInvoiceByIdAsync(Guid invoiceId, Guid userId)
    {
        var invoice = await _context.Invoices
            .AsNoTracking()
            .Include(i => i.UserSubscription)
                .ThenInclude(us => us!.SubscriptionPlan)
            .Include(i => i.UserSubscription)
                .ThenInclude(us => us!.Vehicle)
            .FirstOrDefaultAsync(i => i.Id == invoiceId && i.UserId == userId);

        return invoice != null ? MapToDto(invoice) : null;
    }

    public async Task<Invoice> CreateSubscriptionDepositInvoiceAsync(Guid userId, UserSubscription subscription)
    {
        var subscriptionPlan = await _context.SubscriptionPlans
            .FirstOrDefaultAsync(sp => sp.Id == subscription.SubscriptionPlanId);

        if (subscriptionPlan == null)
        {
            throw new InvalidOperationException("Subscription plan not found");
        }

        var invoice = new Invoice
        {
            UserId = userId,
            UserSubscriptionId = subscription.Id,
            Type = InvoiceType.Deposit,
            InvoiceNumber = await GenerateInvoiceNumberAsync(),
            IssueDate = DateTime.UtcNow,
            DueDate = DateTime.UtcNow.AddDays(7), // 7 days to pay deposit
            SubtotalAmount = subscriptionPlan.DepositAmount,
            TaxAmount = 0, // Deposits are typically not taxed
            TotalAmount = subscriptionPlan.DepositAmount,
            Notes = $"Tiền cọc gói {subscriptionPlan.Name}",
            Status = PaymentStatus.Pending
        };

        _context.Invoices.Add(invoice);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created deposit invoice {InvoiceId} for subscription {SubscriptionId}, amount {Amount}", 
            invoice.Id, subscription.Id, invoice.TotalAmount);

        return invoice;
    }

    // ✅ SIMPLIFIED: No km parameter, fixed monthly price
    public async Task<Invoice> CreateMonthlySubscriptionInvoiceAsync(UserSubscription subscription, DateTime billingPeriodStart, DateTime billingPeriodEnd)
    {
        var subscriptionPlan = await _context.SubscriptionPlans
            .FirstOrDefaultAsync(sp => sp.Id == subscription.SubscriptionPlanId);

        if (subscriptionPlan == null)
        {
            throw new InvalidOperationException("Subscription plan not found");
        }

        // ✅ FIXED PRICE - No calculation needed!
        var monthlyFee = subscriptionPlan.MonthlyPrice;
        var totalAmount = monthlyFee;  // ✅ NO TAX

        var invoice = new Invoice
        {
            UserId = subscription.UserId,
            UserSubscriptionId = subscription.Id,
            Type = InvoiceType.SubscriptionMonthly,
            InvoiceNumber = await GenerateInvoiceNumberAsync(),
            IssueDate = DateTime.UtcNow,
            DueDate = DateTime.UtcNow,  // ✅ PAY IMMEDIATELY!
            BillingPeriodStart = billingPeriodStart,
            BillingPeriodEnd = billingPeriodEnd,
            SubtotalAmount = monthlyFee,
            TaxAmount = 0,  // ✅ NO TAX
            TotalAmount = totalAmount,
            Notes = $"Gói {subscriptionPlan.Name} - Từ {billingPeriodStart:dd/MM/yyyy} đến {billingPeriodEnd:dd/MM/yyyy}",
            Status = PaymentStatus.Pending
        };

        _context.Invoices.Add(invoice);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created subscription invoice {InvoiceId} for subscription {SubscriptionId}, period {Start}-{End}, amount {Amount}", 
            invoice.Id, subscription.Id, billingPeriodStart.ToString("yyyy-MM-dd"), billingPeriodEnd.ToString("yyyy-MM-dd"), invoice.TotalAmount);

        return invoice;
    }

    private async Task<string> GenerateInvoiceNumberAsync()
    {
        var today = DateTime.UtcNow;
        var prefix = $"EVB-INV-{today:yyyyMM}";
        
        var lastInvoice = await _context.Invoices
            .Where(i => i.InvoiceNumber.StartsWith(prefix))
            .OrderByDescending(i => i.InvoiceNumber)
            .FirstOrDefaultAsync();

        var sequence = 1;
        if (lastInvoice != null)
        {
            var lastSequence = lastInvoice.InvoiceNumber.Substring(prefix.Length);
            if (int.TryParse(lastSequence, out var parsedSequence))
            {
                sequence = parsedSequence + 1;
            }
        }

        return $"{prefix}{sequence:D4}";
    }

    private InvoiceDto MapToDto(Invoice invoice)
    {
        return new InvoiceDto
        {
            Id = invoice.Id,
            InvoiceNumber = invoice.InvoiceNumber,
            Type = invoice.Type.ToString(),
            IssueDate = invoice.IssueDate,
            DueDate = invoice.DueDate,
            PaidDate = invoice.PaidDate,
            BillingPeriodStart = invoice.BillingPeriodStart,
            BillingPeriodEnd = invoice.BillingPeriodEnd,
            KmUsedInPeriod = invoice.KmUsedInPeriod,
            SubtotalAmount = invoice.SubtotalAmount,
            TaxAmount = invoice.TaxAmount,
            TotalAmount = invoice.TotalAmount,
            PaidAmount = invoice.PaidAmount,
            RemainingAmount = invoice.RemainingAmount,
            OverdueFeeAmount = invoice.OverdueFeeAmount,
            Status = invoice.Status.ToString(),
            IsOverdue = invoice.IsOverdue,
            DaysOverdue = invoice.DaysOverdue,
            Notes = invoice.Notes,
            UserSubscriptionId = invoice.UserSubscriptionId,
            SubscriptionPlanName = invoice.UserSubscription?.SubscriptionPlan?.Name,
            VehiclePlate = invoice.UserSubscription?.Vehicle?.Plate,
            CreatedAt = invoice.CreatedAt,
            UpdatedAt = invoice.UpdatedAt
        };
    }
}