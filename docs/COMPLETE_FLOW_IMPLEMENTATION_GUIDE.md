# 🚀 COMPLETE BUSINESS FLOW IMPLEMENTATION GUIDE

**Date:** October 21, 2025  
**Status:** Ready for Implementation  
**Prerequisites:** ✅ Invoice System Removed

---

## 📋 TABLE OF CONTENTS

1. [Overview](#overview)
2. [System Architecture](#system-architecture)
3. [Phase-by-Phase Implementation](#phase-by-phase-implementation)
4. [API Endpoints Reference](#api-endpoints-reference)
5. [Frontend Integration Guide](#frontend-integration-guide)
6. [Testing Scenarios](#testing-scenarios)

---

## 🎯 OVERVIEW

This guide provides a complete roadmap for implementing the full reservation + subscription + payment flow for the EV Battery Swap Station Management System.

### **Business Model:**
- Fixed monthly subscription (450k, 850k, 1.5M, 2.5M VND)
- Swap limits: 10, 20, unlimited, unlimited
- 30-day billing cycle
- Swap counter tracking
- VNPay payment integration

---

## 🏗️ SYSTEM ARCHITECTURE

```
┌─────────────────────────────────────────────────────────────────┐
│                    USER JOURNEY FLOW                            │
└─────────────────────────────────────────────────────────────────┘

PHASE 1: Registration & Vehicle Setup
├─ Register Account
├─ Login (Driver Role)
└─ Link Vehicle → Detect Compatible Battery

PHASE 2: Subscription Selection & Payment
├─ View Plans (4 tiers)
├─ Select Plan → Create Subscription
├─ Payment via VNPay
└─ Callback → Activate Subscription

PHASE 3: Reservation with Usage Tracking
├─ Check Usage (5/10 lần)
├─ Select Station
├─ Select Time Slot
└─ Confirm Reservation

PHASE 4: Swap Execution
├─ Arrive at Station
├─ Staff Scan QR
├─ Battery Swap
└─ Complete → Counter++ (6/10)

PHASE 5: Usage Enforcement
├─ After 9 Swaps → Warning (1 left)
└─ Try 11th → Error (limit reached)

PHASE 6: Auto-Expire (Simplified) ✅ IMPLEMENTED
├─ 30 Days Pass → Subscription auto-expires
├─ User notified: "Gói đã hết hạn"
└─ User re-subscribes manually (prepaid model)

Note: See SIMPLIFIED_PHASE6_AUTO_EXPIRE.md for details
```

---

## 📱 PHASE-BY-PHASE IMPLEMENTATION

### **PHASE 1: REGISTRATION & VEHICLE SETUP** ✅

#### **Already Implemented:**
- User registration with email/password
- Login with JWT tokens
- Vehicle linking with automatic battery model detection

#### **API Endpoints:**
```http
POST /api/v1/auth/register
POST /api/v1/auth/login
POST /api/v1/vehicles
```

#### **Frontend Components Needed:**
- `RegisterPage.tsx` ✅
- `LoginPage.tsx` ✅
- `VehicleLinkingPage.tsx` ✅

---

### **PHASE 2: SUBSCRIPTION SELECTION & PAYMENT** 🔄

#### **Implementation Status:**
- ✅ Backend: Fully functional
- ⚠️ Frontend: Needs implementation

#### **Step 2.1: View Subscription Plans**

**API:**
```http
GET /api/v1/subscription-plans

Response:
[
  {
    "id": "guid",
    "name": "Basic",
    "monthlyPrice": 450000,
    "maxSwapsPerMonth": 10,
    "batteryModel": { "name": "VF5 Battery Pack" }
  },
  {
    "id": "guid",
    "name": "Standard",
    "monthlyPrice": 850000,
    "maxSwapsPerMonth": 20,
    "batteryModel": { "name": "VF5 Battery Pack" }
  }
  // ... Premium, VIP
]
```

**Frontend Component:**
```typescript
// components/SubscriptionPlansPage.tsx
export function SubscriptionPlansPage() {
  const { data: plans } = useFetch('/api/v1/subscription-plans');
  const { vehicle } = useVehicle(); // Get user's vehicle
  
  // Filter plans compatible with vehicle's battery
  const compatiblePlans = plans?.filter(
    p => p.batteryModelId === vehicle.compatibleBatteryModelId
  );
  
  return (
    <div className="plans-grid">
      {compatiblePlans?.map(plan => (
        <PlanCard 
          key={plan.id} 
          plan={plan}
          onSelect={() => handleSelectPlan(plan.id)}
        />
      ))}
    </div>
  );
}

// components/PlanCard.tsx
function PlanCard({ plan, onSelect }) {
  return (
    <Card className={plan.isRecommended ? 'recommended' : ''}>
      <h3>{plan.name}</h3>
      <Price>{formatVND(plan.monthlyPrice)}/tháng</Price>
      <Feature>
        {plan.maxSwapsPerMonth 
          ? `${plan.maxSwapsPerMonth} lần đổi pin`
          : 'Không giới hạn'}
      </Feature>
      <Button onClick={onSelect}>Chọn gói này</Button>
    </Card>
  );
}
```

#### **Step 2.2: Create Subscription**

**API:**
```http
POST /api/v1/subscriptions
Body: {
  "subscriptionPlanId": "guid-basic-plan",
  "vehicleId": "guid-my-vehicle"
}

Response:
{
  "subscriptionId": "guid",
  "requiresDeposit": false,
  "monthlyPrice": 450000,
  "maxSwapsPerMonth": 10,
  "billingPeriodStart": "2025-10-21",
  "billingPeriodEnd": "2025-11-20"
}
```

**Frontend:**
```typescript
async function handleSelectPlan(planId: string) {
  try {
    const subscription = await createSubscription({
      subscriptionPlanId: planId,
      vehicleId: vehicle.id
    });
    
    // Navigate to payment
    navigate('/payment', { 
      state: { subscription } 
    });
  } catch (error) {
    if (error.message.includes('đã có gói subscription')) {
      toast.error('Bạn đã có gói đang hoạt động');
    }
  }
}
```

#### **Step 2.3: Payment via VNPay**

**API:**
```http
POST /api/v1/payments/vnpay/create
Body: {
  "subscriptionId": "guid",
  "orderInfo": "Thanh toán gói Basic - Tháng 10/2025",
  "returnUrl": "https://yourapp.com/payment/callback"
}

Response:
{
  "success": true,
  "paymentUrl": "https://sandbox.vnpayment.vn/...",
  "paymentReference": "EVB20251021143022...",
  "paymentId": "guid"
}
```

**Frontend:**
```typescript
// pages/PaymentPage.tsx
export function PaymentPage() {
  const location = useLocation();
  const { subscription } = location.state;
  
  const handlePayment = async () => {
    const result = await createVnPayPayment({
      subscriptionId: subscription.subscriptionId,
      orderInfo: `Thanh toán ${subscription.planName}`,
      returnUrl: `${window.location.origin}/payment/callback`
    });
    
    if (result.success) {
      // Redirect to VNPay
      window.location.href = result.paymentUrl;
    }
  };
  
  return (
    <PaymentSummary>
      <h2>Xác nhận thanh toán</h2>
      <Item>
        <Label>Gói đã chọn:</Label>
        <Value>{subscription.planName}</Value>
      </Item>
      <Item>
        <Label>Số tiền:</Label>
        <Value>{formatVND(subscription.monthlyPrice)}</Value>
      </Item>
      <Item>
        <Label>Chu kỳ:</Label>
        <Value>
          {format(subscription.billingPeriodStart)} - 
          {format(subscription.billingPeriodEnd)}
        </Value>
      </Item>
      <Button onClick={handlePayment}>
        Thanh toán qua VNPay
      </Button>
    </PaymentSummary>
  );
}

// pages/PaymentCallbackPage.tsx
export function PaymentCallbackPage() {
  const searchParams = useSearchParams();
  const navigate = useNavigate();
  
  useEffect(() => {
    const responseCode = searchParams.get('vnp_ResponseCode');
    
    if (responseCode === '00') {
      // Payment successful
      toast.success('Thanh toán thành công!');
      navigate('/dashboard');
    } else {
      // Payment failed
      toast.error('Thanh toán thất bại!');
      navigate('/subscriptions');
    }
  }, []);
  
  return <LoadingSpinner message="Đang xử lý thanh toán..." />;
}
```

---

### **PHASE 3: RESERVATION WITH USAGE TRACKING** 🔄

#### **Implementation Status:**
- ✅ Backend: Usage API exists
- ⚠️ Frontend: Needs usage display

#### **Step 3.1: Display Usage Dashboard**

**API:**
```http
GET /api/v1/subscriptions/mine/usage

Response:
{
  "subscriptionId": "guid",
  "subscriptionPlanName": "Basic",
  "currentMonthSwapCount": 5,
  "maxSwapsPerMonth": 10,
  "usageTier": "5/10 lần",
  "currentBillingPeriodStart": "2025-10-21",
  "currentBillingPeriodEnd": "2025-11-20",
  "currentMonthFee": 450000,
  "totalSwapTransactions": 45,
  "totalAmountPaid": 4500000
}
```

**Frontend:**
```typescript
// components/UsageDashboard.tsx
export function UsageDashboard() {
  const { data: usage } = useFetch('/api/v1/subscriptions/mine/usage');
  
  if (!usage) return <LoadingSpinner />;
  
  const remaining = usage.maxSwapsPerMonth 
    ? usage.maxSwapsPerMonth - usage.currentMonthSwapCount
    : '∞';
  
  const percentage = usage.maxSwapsPerMonth
    ? (usage.currentMonthSwapCount / usage.maxSwapsPerMonth) * 100
    : 0;
  
  const isNearLimit = remaining <= 2 && remaining !== '∞';
  
  return (
    <UsageCard>
      <Header>
        <Title>Gói {usage.subscriptionPlanName}</Title>
        <Badge type={isNearLimit ? 'warning' : 'info'}>
          {usage.usageTier}
        </Badge>
      </Header>
      
      <ProgressBar value={percentage} max={100} />
      
      <Stats>
        <Stat>
          <Label>Đã sử dụng</Label>
          <Value>{usage.currentMonthSwapCount} lần</Value>
        </Stat>
        <Stat>
          <Label>Còn lại</Label>
          <Value className={isNearLimit ? 'text-warning' : ''}>
            {remaining} lần
          </Value>
        </Stat>
      </Stats>
      
      {isNearLimit && (
        <Alert type="warning">
          ⚠️ Chỉ còn {remaining} lần đổi pin! 
          Vui lòng cân nhắc nâng cấp gói.
        </Alert>
      )}
      
      <BillingInfo>
        <Label>Chu kỳ hiện tại:</Label>
        <Value>
          {formatDate(usage.currentBillingPeriodStart)} - 
          {formatDate(usage.currentBillingPeriodEnd)}
        </Value>
      </BillingInfo>
    </UsageCard>
  );
}
```

#### **Step 3.2: Reservation with Usage Check**

**API:**
```http
POST /api/v1/reservations
Body: {
  "stationId": "guid",
  "vehicleId": "guid",
  "reservationDate": "2025-10-25",
  "timeSlotStart": "10:00",
  "timeSlotEnd": "11:00"
}

// Backend validation:
// 1. Check if user has active subscription
// 2. Check if swap count < max swaps
// 3. Check slot availability
```

**Frontend:**
```typescript
// pages/ReservationPage.tsx
export function ReservationPage() {
  const { data: usage } = useFetch('/api/v1/subscriptions/mine/usage');
  const [selectedStation, setSelectedStation] = useState(null);
  const [selectedDate, setSelectedDate] = useState(null);
  const [selectedSlot, setSelectedSlot] = useState(null);
  
  // Check if can make reservation
  const canReserve = !usage?.maxSwapsPerMonth || 
    usage.currentMonthSwapCount < usage.maxSwapsPerMonth;
  
  if (!canReserve) {
    return (
      <Alert type="error">
        <h3>Đã hết lượt đổi pin</h3>
        <p>Bạn đã sử dụng hết {usage.maxSwapsPerMonth} lần trong tháng này.</p>
        <p>Chu kỳ mới bắt đầu từ {formatDate(usage.currentBillingPeriodEnd)}</p>
        <Button onClick={() => navigate('/upgrade')}>
          Nâng cấp gói
        </Button>
      </Alert>
    );
  }
  
  return (
    <ReservationFlow>
      <UsageBanner usage={usage} />
      
      <Step1>
        <h3>Chọn trạm</h3>
        <StationList onSelect={setSelectedStation} />
      </Step1>
      
      <Step2>
        <h3>Chọn ngày</h3>
        <DatePicker 
          minDate={new Date()}
          onChange={setSelectedDate}
        />
      </Step2>
      
      <Step3>
        <h3>Chọn khung giờ</h3>
        <TimeSlotGrid 
          stationId={selectedStation?.id}
          date={selectedDate}
          onSelect={setSelectedSlot}
        />
      </Step3>
      
      <ConfirmButton 
        disabled={!selectedStation || !selectedDate || !selectedSlot}
        onClick={handleConfirm}
      >
        Xác nhận đặt lịch
      </ConfirmButton>
    </ReservationFlow>
  );
}

// components/UsageBanner.tsx
function UsageBanner({ usage }) {
  const remaining = usage.maxSwapsPerMonth - usage.currentMonthSwapCount;
  
  return (
    <Banner type={remaining <= 2 ? 'warning' : 'info'}>
      📊 Bạn còn {remaining} lần đổi pin trong tháng này
      <ProgressBar 
        value={usage.currentMonthSwapCount} 
        max={usage.maxSwapsPerMonth}
      />
    </Banner>
  );
}
```

---

### **PHASE 4: SWAP EXECUTION & COUNTER INCREMENT** ✅

#### **Already Implemented:**
- Staff completes swap transaction
- System validates swap limit BEFORE completing
- Counter increments AFTER successful swap
- Error message when limit reached

**Key Code (SwapTransactionService.cs):**
```csharp
// Validation BEFORE swap
if (subscription.CurrentMonthSwapCount >= MaxSwapsPerMonth) {
    throw "Đã đạt giới hạn 10 lần đổi pin trong tháng này. " +
          "Hiện tại: 10/10 lần.";
}

// Increment AFTER success
subscription.CurrentMonthSwapCount++;
_logger.LogInformation("Swap counter: {Count}/{Max}", 
    subscription.CurrentMonthSwapCount, MaxSwapsPerMonth);
```

**Frontend (Driver View):**
```typescript
// components/SwapCompletedNotification.tsx
export function SwapCompletedNotification({ swap }) {
  const { data: usage } = useFetch('/api/v1/subscriptions/mine/usage');
  
  return (
    <Notification type="success">
      <Icon>✅</Icon>
      <Title>Đổi pin thành công!</Title>
      <Message>
        Bạn đã sử dụng {usage.currentMonthSwapCount}/{usage.maxSwapsPerMonth} lần 
        trong tháng này.
      </Message>
      {usage.maxSwapsPerMonth - usage.currentMonthSwapCount <= 2 && (
        <Warning>
          ⚠️ Chỉ còn {usage.maxSwapsPerMonth - usage.currentMonthSwapCount} lần!
        </Warning>
      )}
    </Notification>
  );
}
```

---

### **PHASE 5: USAGE TRACKING & LIMIT ENFORCEMENT** ✅

#### **Already Implemented:**
- Real-time usage tracking via API
- Limit validation on each swap
- Error thrown when limit exceeded

**Frontend Integration:**
```typescript
// hooks/useSwapUsage.ts
export function useSwapUsage() {
  const { data: usage, refetch } = useFetch('/api/v1/subscriptions/mine/usage');
  
  const remaining = usage?.maxSwapsPerMonth 
    ? usage.maxSwapsPerMonth - usage.currentMonthSwapCount
    : Infinity;
  
  const canSwap = remaining > 0;
  const isNearLimit = remaining <= 2;
  const percentage = usage?.maxSwapsPerMonth
    ? (usage.currentMonthSwapCount / usage.maxSwapsPerMonth) * 100
    : 0;
  
  return {
    usage,
    remaining,
    canSwap,
    isNearLimit,
    percentage,
    refetch
  };
}

// Usage in components:
function ReservationButton() {
  const { canSwap, isNearLimit, remaining } = useSwapUsage();
  
  if (!canSwap) {
    return <Button disabled>Đã hết lượt đổi pin</Button>;
  }
  
  return (
    <Button warning={isNearLimit}>
      Đặt lịch {isNearLimit && `(Còn ${remaining} lần)`}
    </Button>
  );
}
```

---

### **PHASE 6: AUTO-EXPIRE SUBSCRIPTIONS (SIMPLIFIED)** ✅ IMPLEMENTED

> **📖 Full details:** See [SIMPLIFIED_PHASE6_AUTO_EXPIRE.md](./SIMPLIFIED_PHASE6_AUTO_EXPIRE.md)

#### **Implementation Status:**
- ✅ Backend: Auto-expire logic complete
- ✅ Middleware: Checks every 5 minutes on any request
- ✅ DTOs: Added `IsExpired` and `DaysRemaining` fields
- ⚠️ Frontend: Needs UI to display expiration warnings

#### **What Was Implemented:**

**Simplified Approach (No Background Job Needed):**
```csharp
// Services/SubscriptionService.cs
public async Task CheckAndExpireSubscriptionsAsync()
{
    var now = DateTime.UtcNow;
    
    // Find active subscriptions that have passed their billing end date
    var expiredSubscriptions = await _context.UserSubscriptions
        .Where(us => us.IsActive && us.CurrentBillingPeriodEnd < now)
        .ToListAsync();
    
    if (!expiredSubscriptions.Any())
        return;
    
    foreach (var subscription in expiredSubscriptions)
    {
        subscription.IsActive = false; // ✅ Just deactivate, user re-subscribes manually
        subscription.UpdatedAt = now;
        
        _logger.LogInformation(
            "Auto-expired subscription {SubscriptionId} for user {UserId}",
            subscription.Id, subscription.UserId);
    }
    
    await _context.SaveChangesAsync();
}

// Middleware: Checks every 5 minutes on any request (no background job!)
app.UseMiddleware<SubscriptionExpirationMiddleware>();
```

**User Flow:**
1. **Day 30:** Subscription expires → Middleware sets `IsActive = false`
2. **User tries to swap:** Error: "Gói đã hết hạn"
3. **User re-subscribes:** New subscription created → New 30-day period

**Benefits:**
- ✅ No complex background job
- ✅ No email reminders needed
- ✅ No auto-payment confusion
- ✅ Prepaid model (user pays before using)
- ✅ Works on any hosting (Azure, AWS, on-premise)

#### **Frontend Integration:**

```typescript
// Check expiration status
GET /api/v1/subscriptions/mine
Response:
{
  "isExpired": true,
  "daysRemaining": null,  // null if expired
  "currentBillingPeriodEnd": "2025-10-20T00:00:00Z"
}

// Display warning
<SubscriptionStatus />  // Shows "Còn 5 ngày" or "Đã hết hạn"
```

---

## 📚 API ENDPOINTS REFERENCE

### **Authentication**
```
POST   /api/v1/auth/register
POST   /api/v1/auth/login
POST   /api/v1/auth/logout
```

### **Vehicles**
```
GET    /api/v1/vehicles/mine
POST   /api/v1/vehicles
PUT    /api/v1/vehicles/{id}
DELETE /api/v1/vehicles/{id}
```

### **Subscription Plans**
```
GET    /api/v1/subscription-plans
GET    /api/v1/subscription-plans/{id}
```

### **Subscriptions**
```
POST   /api/v1/subscriptions              # Create
GET    /api/v1/subscriptions/mine          # Get my subscription
GET    /api/v1/subscriptions/mine/usage    # ⭐ Get usage stats
POST   /api/v1/subscriptions/cancel        # Cancel
```

### **Payments** ⭐ UPDATED
```
POST   /api/v1/payments/vnpay/create       # Create payment (with subscriptionId)
GET    /api/v1/payments/vnpay/callback     # VNPay callback
GET    /api/v1/payments/my-payments        # Payment history
GET    /api/v1/payments/{id}               # Payment details
```

### **Stations**
```
GET    /api/v1/stations
GET    /api/v1/stations/{id}
```

### **Reservations**
```
POST   /api/v1/reservations
GET    /api/v1/reservations/mine
GET    /api/v1/reservations/{id}
POST   /api/v1/reservations/{id}/cancel
```

### **Swap Transactions**
```
GET    /api/v1/swap-transactions/my-history
GET    /api/v1/swap-transactions/{id}
```

---

## 🎨 FRONTEND INTEGRATION GUIDE

### **Required Pages:**

1. **SubscriptionPlansPage** (`/subscriptions`) 🔄 NEW
   - Display 4 subscription tiers
   - Highlight recommended plan
   - Show features comparison
   - Filter by compatible battery

2. **PaymentPage** (`/payment`) 🔄 NEW
   - Show payment summary
   - VNPay integration
   - Payment confirmation

3. **PaymentCallbackPage** (`/payment/callback`) 🔄 NEW
   - Handle VNPay callback
   - Show success/failure message
   - Redirect appropriately

4. **DashboardPage** (`/dashboard`) 🔄 UPDATE
   - **Add UsageDashboard component**
   - Show swap usage (5/10 lần)
   - Progress bar
   - Warning when near limit

5. **ReservationPage** (`/reservations/new`) 🔄 UPDATE
   - **Add usage check before allowing reservation**
   - Show usage banner
   - Disable if limit reached

### **Required Components:**

```
components/
├── subscription/
│   ├── SubscriptionPlansGrid.tsx     🆕
│   ├── PlanCard.tsx                  🆕
│   ├── PlanComparisonTable.tsx       🆕
│   └── UsageDashboard.tsx            🆕⭐
├── payment/
│   ├── PaymentSummary.tsx            🆕
│   ├── VnPayButton.tsx               🆕
│   └── PaymentReminderBanner.tsx     🆕
├── reservation/
│   ├── UsageBanner.tsx               🆕⭐
│   └── LimitReachedAlert.tsx         🆕
└── shared/
    ├── ProgressBar.tsx               🆕
    └── UsageBadge.tsx                🆕
```

### **Required Hooks:**

```typescript
// hooks/useSubscription.ts
export function useSubscription() {
  const { data, isLoading, error } = useFetch('/api/v1/subscriptions/mine');
  return { subscription: data, isLoading, error };
}

// hooks/useSwapUsage.ts ⭐ CRITICAL
export function useSwapUsage() {
  const { data, refetch } = useFetch('/api/v1/subscriptions/mine/usage');
  
  const remaining = data?.maxSwapsPerMonth 
    ? data.maxSwapsPerMonth - data.currentMonthSwapCount
    : Infinity;
  
  return {
    usage: data,
    remaining,
    canSwap: remaining > 0,
    isNearLimit: remaining <= 2,
    percentage: (data?.currentMonthSwapCount / data?.maxSwapsPerMonth) * 100,
    refetch
  };
}

// hooks/usePayment.ts
export function usePayment() {
  const createPayment = async (subscriptionId, orderInfo) => {
    const response = await api.post('/api/v1/payments/vnpay/create', {
      subscriptionId,
      orderInfo,
      returnUrl: `${window.location.origin}/payment/callback`
    });
    return response.data;
  };
  
  return { createPayment };
}
```

---

## 🧪 TESTING SCENARIOS

### **Scenario 1: New User Complete Flow**

```
1. Register account
   POST /auth/register
   → Email: test@example.com

2. Login
   POST /auth/login
   → Get access token

3. Link vehicle
   POST /vehicles
   → VIN: VF12345, Plate: 51A-12345

4. View plans
   GET /subscription-plans
   → See 4 tiers

5. Select Basic plan
   POST /subscriptions
   → subscriptionPlanId: basic-guid

6. Create payment
   POST /payments/vnpay/create
   → subscriptionId: sub-guid
   → Get payment URL

7. Simulate VNPay callback
   GET /payments/vnpay/callback?vnp_ResponseCode=00&...
   → Subscription activated

8. Check usage
   GET /subscriptions/mine/usage
   → currentMonthSwapCount: 0
   → maxSwapsPerMonth: 10

9. Create reservation
   POST /reservations
   → Reservation created

10. Complete swap (as staff)
    PUT /swap-transactions/{id}/complete
    → Counter: 1/10
```

### **Scenario 2: Approaching Limit**

```
1. User has 8/10 swaps used
   GET /subscriptions/mine/usage
   → Show warning: "Còn 2 lần"

2. Complete 9th swap
   → Counter: 9/10
   → Show critical warning

3. Complete 10th swap
   → Counter: 10/10
   → Show "Đã hết lượt"

4. Try 11th swap
   PUT /swap-transactions/{id}/complete
   → ❌ ERROR 400: "Đã đạt giới hạn"
```

### **Scenario 3: Billing Cycle Renewal**

```
1. User at end of 30-day cycle
   currentBillingPeriodEnd: 2025-11-20
   currentMonthSwapCount: 10/10

2. Background job runs (Nov 20)
   → Reset counter: 0/10
   → New period: Nov 20 - Dec 20

3. User can swap again
   GET /subscriptions/mine/usage
   → currentMonthSwapCount: 0
   → maxSwapsPerMonth: 10

4. Payment reminder sent
   → Email with renewal payment link
```

---

## ✅ IMPLEMENTATION CHECKLIST

### **Phase 2: Subscription & Payment**
- [ ] Create SubscriptionPlansPage
- [ ] Create PaymentPage
- [ ] Create PaymentCallbackPage
- [ ] Integrate VNPay SDK
- [ ] Add payment flow to user journey
- [ ] Test payment success/failure scenarios

### **Phase 3: Usage Tracking**
- [ ] Create UsageDashboard component
- [ ] Create useSwapUsage hook
- [ ] Add usage display to Dashboard
- [ ] Add usage banner to ReservationPage
- [ ] Implement limit reached UI
- [ ] Test usage display updates

### **Phase 4: Reservation Integration**
- [ ] Add usage check to reservation flow
- [ ] Display remaining swaps
- [ ] Show warning when near limit
- [ ] Block reservation when limit reached
- [ ] Test reservation limits

### **Phase 6: Billing Cycle**
### **Phase 6: Auto-Expire** ✅ BACKEND COMPLETE
- [x] Create CheckAndExpireSubscriptionsAsync method
- [x] Create SubscriptionExpirationMiddleware
- [x] Add IsExpired and DaysRemaining to DTOs
- [x] Register middleware in Program.cs
- [ ] Frontend: Display expiration warnings
- [ ] Frontend: Block actions when expired
- [ ] Frontend: Re-subscribe flow

---

## 📞 SUPPORT & NEXT STEPS

### **Priority Implementation Order:**

1. **HIGH**: Usage Display (Phase 3)
   - Users need to see their swap count
   - Critical for UX
   - **Backend:** ✅ Ready (`/subscriptions/mine/usage`)
   - **Frontend:** 🔄 Needs implementation

2. **HIGH**: Payment Flow (Phase 2)
   - Users can't subscribe without payment
   - Revenue-critical
   - **Backend:** ✅ Ready (VNPay integration complete)
   - **Frontend:** 🔄 Needs implementation

3. **MEDIUM**: Reservation Integration (Phase 4)
   - Enhance existing reservation system
   - Enforce limits
   - **Backend:** ✅ Limit validation working
   - **Frontend:** 🔄 Needs usage banner

4. **COMPLETE**: Auto-Expire (Phase 6) ✅
   - **Backend:** ✅ Middleware running
   - **Frontend:** 🔄 Needs UI for expired state

### **Development Timeline Estimate:**

- **Week 1**: Phase 2 (Payment UI) - 3-4 days
- **Week 1**: Phase 3 (Usage Dashboard) - 2-3 days  
- **Week 2**: Phase 4 (Reservation Integration) - 2-3 days
- **Week 2**: Phase 6 (Expiration UI) - 1-2 days ✅ Backend done
- **Week 3**: Testing & Polish - 5 days

**Total: ~2-3 weeks for frontend implementation**  
*Backend already 90% complete!*

---

**Last Updated:** October 21, 2025  
**Version:** 2.0  
**Status:** Backend Ready ✅ | Frontend Ready for Implementation 🚀  
**Phase 6:** ✅ Simplified (Auto-Expire) - No background job needed!
