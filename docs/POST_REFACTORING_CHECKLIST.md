# ✅ Post-Refactoring Checklist

## 🎯 Completed ✅

- [x] **Models Refactored**
  - [x] SubscriptionPlan.cs - Removed 3-tier pricing, added fixed pricing
  - [x] UserSubscription.cs - Replaced km tracking with swap counter

- [x] **Database Migration**
  - [x] Created migration: `SimplifySubscriptionPricing`
  - [x] Applied migration successfully
  - [x] All schema changes applied
  - [x] Data migrated safely

- [x] **Controllers Updated**
  - [x] SubscriptionPlansController.cs - Returns new pricing model

- [x] **Services Refactored**
  - [x] InvoiceService.cs - Fixed price invoices
  - [x] SubscriptionService.cs - Swap counter logic

- [x] **DTOs Updated**
  - [x] SubscriptionResponseDtos.cs
  - [x] UserSubscriptionDto.cs
  - [x] SubscriptionUsageDto.cs

- [x] **Configuration**
  - [x] Program.cs - New seed data (4 plans)
  - [x] AppDbContext.cs - EF Core configs

- [x] **Build & Compile**
  - [x] ✅ Build succeeded
  - [x] ✅ No compile errors

---

## ⏳ Pending Tasks

### 🔴 HIGH PRIORITY (Must Do Before Demo)

#### 1. Update SwapTransactionService
**File**: `Services/SwapTransactionService.cs`

**Location**: In the swap completion logic (after battery swap is confirmed)

**Code to Add**:
```csharp
// After successful swap, update subscription swap counter
var subscription = await _context.UserSubscriptions
    .Include(s => s.SubscriptionPlan)
    .FirstOrDefaultAsync(s => s.UserId == userId && s.IsActive);

if (subscription != null)
{
    // Increment swap counter
    subscription.CurrentMonthSwapCount++;
    
    // Optional: Check if exceeded limit
    var plan = subscription.SubscriptionPlan;
    if (plan.MaxSwapsPerMonth.HasValue && 
        subscription.CurrentMonthSwapCount > plan.MaxSwapsPerMonth)
    {
        // Option A: Charge extra fee (50,000 VND per extra swap)
        // TODO: Create invoice for extra swap
        
        // Option B: Block swap
        // throw new Exception("Bạn đã vượt quá số lần đổi pin trong tháng!");
    }
    
    await _context.SaveChangesAsync();
}
```

**Why Important**: Without this, swap counter never increases! Users won't be tracked properly.

**Time Estimate**: 30 minutes - 1 hour

---

#### 2. Test All Subscription APIs
**Goal**: Verify new model works end-to-end

**Test Cases**:
```bash
# 1. Get all plans
GET http://localhost:5000/api/v1/subscription-plans
# Expected: 4 plans with MonthlyPrice, MaxSwapsPerMonth

# 2. Get plan details
GET http://localhost:5000/api/v1/subscription-plans/{planId}
# Expected: Benefits, RefundPolicy included

# 3. Create subscription
POST http://localhost:5000/api/v1/subscriptions
{
  "planId": "...",
  "vehicleId": "..."
}
# Expected: Response includes MonthlyPrice, MaxSwapsPerMonth

# 4. Get my subscription
GET http://localhost:5000/api/v1/subscriptions/mine
# Expected: CurrentMonthSwapCount = 0 initially

# 5. Perform battery swap
POST http://localhost:5000/api/v1/swap-transactions
# Expected: After swap, CurrentMonthSwapCount = 1

# 6. Get usage statistics
GET http://localhost:5000/api/v1/subscriptions/usage
# Expected: Shows swap count, usage tier like "5/10 lần"
```

**Time Estimate**: 30-45 minutes

---

### 🟡 MEDIUM PRIORITY (Nice to Have)

#### 3. Implement Refund Calculation
**File**: `Services/SubscriptionService.cs`

**Method to Add**: `CalculateRefundAmount`

```csharp
public decimal CalculateRefundAmount(UserSubscription subscription)
{
    var today = DateTime.UtcNow;
    var periodEnd = subscription.CurrentBillingPeriodEnd;
    var periodStart = subscription.CurrentBillingPeriodStart;
    
    // Calculate days remaining in period
    var daysRemaining = (periodEnd - today).TotalDays;
    var totalDays = (periodEnd - periodStart).TotalDays;
    
    // Pro-rata calculation
    var refundPercentage = daysRemaining / totalDays;
    var refundAmount = subscription.SubscriptionPlan.MonthlyPrice * (decimal)refundPercentage;
    
    return Math.Round(refundAmount, 0); // Round to nearest VND
}
```

**Usage**: Call this when user cancels subscription

**Time Estimate**: 30 minutes

---

#### 4. Add Swap Limit Enforcement
**File**: `Services/SwapTransactionService.cs`

**Logic**: Before allowing swap, check if user has reached limit

```csharp
public async Task<bool> CanUserSwapBattery(Guid userId)
{
    var subscription = await _context.UserSubscriptions
        .Include(s => s.SubscriptionPlan)
        .FirstOrDefaultAsync(s => s.UserId == userId && s.IsActive);
    
    if (subscription == null)
    {
        // No subscription = can only use pay-per-swap (50k VND)
        return true;
    }
    
    var plan = subscription.SubscriptionPlan;
    
    // If unlimited plan, always allow
    if (!plan.MaxSwapsPerMonth.HasValue)
        return true;
    
    // Check if reached limit
    return subscription.CurrentMonthSwapCount < plan.MaxSwapsPerMonth.Value;
}
```

**Time Estimate**: 20 minutes

---

### 🟢 LOW PRIORITY (Post-Demo)

#### 5. Update Documentation
- [ ] Update API documentation with new response formats
- [ ] Update README with new subscription model
- [ ] Add migration guide for future developers

**Time Estimate**: 1 hour

---

#### 6. Add Monitoring/Logging
- [ ] Log swap counter increments
- [ ] Alert when users exceed limits
- [ ] Track refund calculations

**Time Estimate**: 30 minutes

---

## 📋 Testing Script for Demo

### Scenario 1: New User Subscribes to Basic Plan
```
1. User registers account
2. User adds vehicle (plate number, model)
3. User selects "Basic" plan (450k, 10 swaps/month)
4. User pays 450,000 VND immediately
5. System creates subscription with:
   - Start date: Today
   - End date: Today + 30 days
   - CurrentMonthSwapCount = 0
   - Status: Active
```

### Scenario 2: User Performs Battery Swaps
```
1. User arrives at station
2. User initiates swap transaction
3. System checks subscription:
   - Has active subscription? ✅
   - Swap count: 5/10 (still under limit)
4. System completes swap:
   - Increment counter: 5 → 6
   - No charge (included in subscription)
5. User sees updated count: "6/10 lần đã sử dụng"
```

### Scenario 3: User Exceeds Swap Limit
```
1. User has used 10/10 swaps
2. User tries 11th swap
3. System shows two options:
   - Option A: "Bạn đã hết lượt đổi pin. Đợi đến kỳ billing mới?"
   - Option B: "Đổi thêm với giá 50,000 VND/lần"
4. If Option B: Charge 50k, allow swap
```

### Scenario 4: Billing Period Ends
```
1. 30 days pass from subscription start
2. System creates invoice:
   - Amount: 450,000 VND (fixed)
   - Tax: 0 VND
   - Total: 450,000 VND
   - Due: Immediately
3. User pays invoice
4. System resets swap counter:
   - CurrentMonthSwapCount = 0
   - New billing period: Next 30 days
```

---

## 🚨 Known Issues & Limitations

### 1. Old Subscriptions
**Issue**: Existing subscriptions created before migration still have old billing cycles (26-25)

**Solution**: Migration recalculated all billing periods to 30 days from StartDate

**Status**: ✅ Fixed by migration

---

### 2. Swap Counter Not Incrementing
**Issue**: SwapTransactionService not yet updated to increment counter

**Solution**: Add increment logic (see Task #1 above)

**Status**: ⏳ Pending

---

### 3. No Extra Swap Fee Logic
**Issue**: When user exceeds limit, no charge is applied

**Solution**: 
- Option A: Block swap until next billing period
- Option B: Charge 50k VND per extra swap (recommended)

**Status**: ⏳ Pending

---

## 🎯 Demo Day Checklist (Oct 27)

### Before Demo:
- [ ] Ensure API is running (`dotnet run`)
- [ ] Database has 4 new plans seeded
- [ ] Test user account ready with vehicle
- [ ] Postman/HTTP file ready for API calls

### During Demo:
- [ ] Show 4 subscription plans (Basic, Standard, Premium, VIP)
- [ ] Create subscription (show immediate payment, no deposit)
- [ ] Perform battery swap (show counter increment)
- [ ] Show usage statistics (swap count, usage tier)
- [ ] Explain refund policy (pro-rata)

### Key Talking Points:
1. **Simplicity**: "Trước đây dùng mô hình VinFast phức tạp (3 mức giá theo km). Giờ đơn giản: 4 gói cố định."
2. **No Deposit**: "Không cần đặt cọc 5-60 triệu như trước. Chỉ trả tiền gói theo tháng."
3. **No Tax**: "Không có thuế VAT 10%. Giá đã bao gồm mọi chi phí."
4. **Transparent**: "User biết rõ số lần đã đổi pin: 5/10 lần."
5. **Flexible**: "Hủy bất cứ lúc nào, hoàn tiền theo số ngày còn lại."

---

## 📞 Quick Reference

### Get Current State:
```bash
# Check database migrations
dotnet ef migrations list

# Build project
dotnet build

# Run project
dotnet run

# Check active subscriptions
# SQL: SELECT * FROM UserSubscriptions WHERE IsActive = 1
```

### Rollback (Emergency):
```bash
# If something goes wrong, rollback migration
dotnet ef database update 20251007070330_AddOtpToUsers

# This will revert to the migration before SimplifySubscriptionPricing
```

---

## ✅ Success Criteria

You'll know the refactoring is complete when:

1. ✅ Build succeeds with no errors
2. ✅ Migration applied successfully
3. ⏳ User can create subscription with new plans
4. ⏳ Swap counter increments on each battery swap
5. ⏳ Usage statistics show swap count (not km)
6. ⏳ Billing creates invoices with fixed prices (no tax)
7. ⏳ API returns new DTO structures

**Current Status**: 80% complete  
**Remaining**: Swap counter logic + API testing  
**Time to Complete**: ~2 hours

---

**Good luck with your demo! 🚀**
