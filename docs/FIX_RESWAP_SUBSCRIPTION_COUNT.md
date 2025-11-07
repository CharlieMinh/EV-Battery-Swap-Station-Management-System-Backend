# FIX: Re-swap không nên trừ lượt đổi pin trong gói đăng ký

## Vấn đề
Khi khách hàng thực hiện re-swap (đổi pin thay thế do khiếu nại pin lỗi), hệ thống vẫn **trừ 1 lượt đổi pin** từ gói đăng ký của họ (`CurrentMonthSwapCount++`). Điều này là **sai logic kinh doanh** vì:

1. Re-swap là đổi pin thay thế do lỗi hệ thống/pin bảo hành
2. Khách hàng không nên bị tính phí hay trừ lượt khi pin bị lỗi không phải lỗi của họ
3. Re-swap phải được coi là dịch vụ bảo hành miễn phí

## Nguyên nhân
Trong `SwapTransactionService.cs`, các method xử lý hoàn tất giao dịch đổi pin (`FinalizeFromReservationAsync` và `CompleteSwapAsync`) đều tăng `CurrentMonthSwapCount` mà không kiểm tra xem đây có phải là **re-swap** hay không.

Re-swap được đánh dấu bằng thuộc tính `RelatedComplaintId` trong `SwapTransaction` (và `Reservation`).

## Giải pháp
Thêm điều kiện kiểm tra `RelatedComplaintId` trước khi:
1. **Kiểm tra giới hạn swap** (không kiểm tra giới hạn cho re-swap)
2. **Tăng counter swap** (không tăng counter cho re-swap)

### Các thay đổi trong `SwapTransactionService.cs`

#### 1. Method `FinalizeFromReservationAsync`

**A. Kiểm tra giới hạn swap (dòng ~105-115)**
```csharp
// ⭐ IMPROVEMENT 2: Check subscription limit BEFORE finalizing the transaction
// ⭐ FIX: KHÔNG kiểm tra giới hạn nếu đây là Re-swap (có RelatedComplaintId)
if (reservation.Payment == null && !reservation.RelatedComplaintId.HasValue)
{
    var activeSubscription = await _context.UserSubscriptions
        .Include(s => s.SubscriptionPlan)
        .FirstOrDefaultAsync(s => s.UserId == reservation.UserId && s.IsActive);

    if (activeSubscription?.SubscriptionPlan.MaxSwapsPerMonth != null &&
        activeSubscription.CurrentMonthSwapCount >= activeSubscription.SubscriptionPlan.MaxSwapsPerMonth.Value)
    {
        throw new InvalidOperationException(
            $"Người dùng đã đạt giới hạn {activeSubscription.SubscriptionPlan.MaxSwapsPerMonth.Value} lần đổi pin trong tháng này.");
    }
}
```

**B. Tăng counter swap (dòng ~185-220)**
```csharp
// 6. Update subscription swap count if applicable
// ⭐ FIX: KHÔNG trừ lượt khi đây là Re-swap (có RelatedComplaintId)
if (reservation.Payment == null && !swapTransaction.RelatedComplaintId.HasValue)
{
    if (reservation.UserSubscriptionId.HasValue)
    {
        var subscription = await _context.UserSubscriptions
            .FirstOrDefaultAsync(s => s.Id == reservation.UserSubscriptionId.Value);

        if (subscription != null && subscription.IsActive)
        {
            subscription.CurrentMonthSwapCount++;
            swapTransaction.UserSubscriptionId = subscription.Id;
            _logger.LogInformation("Incremented swap count for subscription {SubscriptionId} to {SwapCount}",
                subscription.Id, subscription.CurrentMonthSwapCount);
        }
        // ... (phần còn lại)
    }
}
else if (swapTransaction.RelatedComplaintId.HasValue)
{
    _logger.LogInformation("Re-swap detected (RelatedComplaintId: {ComplaintId}). Skipping swap count increment for user {UserId}.",
        swapTransaction.RelatedComplaintId.Value, reservation.UserId);
}
```

#### 2. Method `CompleteSwapAsync`

**A. Kiểm tra giới hạn swap (dòng ~375-390)**
```csharp
// 2. Check subscription swap limit BEFORE completing (if user has subscription)
// ⭐ FIX: KHÔNG kiểm tra giới hạn nếu đây là Re-swap (có RelatedComplaintId)
if (swap.UserSubscriptionId.HasValue && !swap.RelatedComplaintId.HasValue)
{
    var subscription = await _context.UserSubscriptions
        .Include(us => us.SubscriptionPlan)
        .FirstOrDefaultAsync(us => us.Id == swap.UserSubscriptionId);

    if (subscription != null && subscription.SubscriptionPlan.MaxSwapsPerMonth.HasValue)
    {
        if (subscription.CurrentMonthSwapCount >= subscription.SubscriptionPlan.MaxSwapsPerMonth.Value)
        {
            throw new InvalidOperationException(
                $"Đã đạt giới hạn {subscription.SubscriptionPlan.MaxSwapsPerMonth} lần đổi pin trong tháng này...");
        }
    }
}
```

**B. Tăng counter swap (dòng ~415-435)**
```csharp
// 4. Increment swap counter for subscription users
// ⭐ FIX: KHÔNG trừ lượt khi đây là Re-swap (có RelatedComplaintId)
if (swap.UserSubscriptionId.HasValue && !swap.RelatedComplaintId.HasValue)
{
    var subscription = await _context.UserSubscriptions
        .Include(us => us.SubscriptionPlan)
        .FirstOrDefaultAsync(us => us.Id == swap.UserSubscriptionId);

    if (subscription != null)
    {
        subscription.CurrentMonthSwapCount++;
        _logger.LogInformation(
            "Incremented swap count for user {UserId}, subscription {SubscriptionId}: {CurrentCount}/{MaxCount}",
            userId, subscription.Id, subscription.CurrentMonthSwapCount,
            subscription.SubscriptionPlan.MaxSwapsPerMonth?.ToString() ?? "Unlimited");
    }
}
else if (swap.RelatedComplaintId.HasValue)
{
    _logger.LogInformation("Re-swap detected (RelatedComplaintId: {ComplaintId}). Skipping swap count increment for user {UserId}.",
        swap.RelatedComplaintId.Value, userId);
}
```

## Kết quả
Sau khi áp dụng fix này:

✅ **Re-swap (khiếu nại pin lỗi) KHÔNG trừ lượt đổi pin** từ gói đăng ký  
✅ **Re-swap KHÔNG kiểm tra giới hạn swap** của gói  
✅ **Swap thường vẫn hoạt động bình thường** (trừ lượt và kiểm tra giới hạn)  
✅ **Logging rõ ràng** để audit khi re-swap được thực hiện  

## Testing
Để test fix này:

1. **Tạo user có subscription với giới hạn swap (ví dụ: 5 lượt/tháng)**
2. **Thực hiện swap thường** → Kiểm tra `CurrentMonthSwapCount` tăng
3. **Báo lỗi pin (tạo complaint)**
4. **Staff xác nhận lỗi và thực hiện re-swap**
5. **Kiểm tra `CurrentMonthSwapCount` KHÔNG tăng** sau re-swap
6. **Kiểm tra logs** để xác nhận message "Re-swap detected... Skipping swap count increment"

## File thay đổi
- `src/EVBSS.Api/Services/SwapTransactionService.cs`

## Commit message gợi ý
```
fix: Re-swap không trừ lượt đổi pin trong gói đăng ký

- Thêm kiểm tra RelatedComplaintId trước khi tăng CurrentMonthSwapCount
- Bỏ qua kiểm tra giới hạn swap cho re-swap
- Thêm logging rõ ràng khi skip swap count cho re-swap
- Fix trong FinalizeFromReservationAsync và CompleteSwapAsync
```

---
**Ngày fix:** 2025-11-06  
**Developer:** GitHub Copilot
