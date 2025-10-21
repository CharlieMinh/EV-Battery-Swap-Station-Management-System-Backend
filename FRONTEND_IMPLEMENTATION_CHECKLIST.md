# 🎨 FRONTEND IMPLEMENTATION CHECKLIST

**Backend Status:** ✅ 100% Complete  
**Frontend Status:** 🔄 Needs Implementation

---

## 📋 **PHASE 2: SUBSCRIPTION & PAYMENT**

### **Backend APIs Ready:**
- ✅ `GET /api/v1/subscription-plans` - Get all plans
- ✅ `POST /api/v1/subscriptions` - Create subscription  
- ✅ `POST /api/v1/payments/vnpay/create` - Create VNPay payment
- ✅ `GET /api/v1/payments/vnpay/callback` - Payment callback handler

### **Frontend TODO:**

#### **Page 1: SubscriptionPlansPage** 🔄
**Route:** `/subscriptions/plans` or `/pricing`

**Requirements:**
- Display 4 subscription tiers (Basic, Standard, Premium, VIP)
- Show monthly price (450k, 850k, 1.5M, 2.5M VND)
- Show swap limits (10, 20, unlimited, unlimited)
- Show benefits & refund policy
- Highlight recommended plan
- Filter by user's vehicle battery model
- Button "Chọn gói này" → navigate to confirmation

**API Call:**
```typescript
GET /api/v1/subscription-plans
```

**Component Structure:**
```
SubscriptionPlansPage
├── PlanCard (x4)
│   ├── PlanName (Basic/Standard/Premium/VIP)
│   ├── Price (450k/tháng)
│   ├── SwapLimit (10 lần/tháng hoặc Không giới hạn)
│   ├── BenefitsList
│   └── SelectButton
└── ComparisonTable (optional)
```

**Example Code:**
```typescript
// pages/SubscriptionPlansPage.tsx
export function SubscriptionPlansPage() {
  const { data: plans } = useFetch('/api/v1/subscription-plans');
  const { vehicle } = useUserVehicle();
  
  // Filter compatible plans
  const compatiblePlans = plans?.filter(
    p => p.batteryModel.id === vehicle.compatibleBatteryModelId
  );
  
  return (
    <div className="plans-container">
      <h1>Chọn gói subscription</h1>
      <div className="plans-grid">
        {compatiblePlans?.map(plan => (
          <PlanCard 
            key={plan.id}
            plan={plan}
            onSelect={() => handleSelectPlan(plan.id)}
          />
        ))}
      </div>
    </div>
  );
}
```

---

#### **Page 2: PaymentPage** 🔄
**Route:** `/payment`

**Requirements:**
- Display selected subscription plan summary
- Show amount to pay
- Show billing period (30 days)
- Button "Thanh toán qua VNPay" → redirect to VNPay
- Loading state while creating payment URL

**API Calls:**
```typescript
// 1. Create subscription
POST /api/v1/subscriptions
{
  "subscriptionPlanId": "guid",
  "vehicleId": "guid",
  "startDate": "2025-10-21"
}

// 2. Create VNPay payment
POST /api/v1/payments/vnpay/create
{
  "subscriptionId": "subscription-guid",
  "orderInfo": "Thanh toán gói Basic - Tháng 10/2025",
  "returnUrl": "http://yourapp.com/payment/callback"
}
```

**Component Structure:**
```
PaymentPage
├── PaymentSummary
│   ├── PlanName
│   ├── Amount
│   ├── BillingPeriod
│   └── Terms
└── VnPayButton (redirects to paymentUrl)
```

**Example Code:**
```typescript
// pages/PaymentPage.tsx
export function PaymentPage() {
  const { selectedPlan } = useLocation().state;
  const { vehicle } = useUserVehicle();
  const [loading, setLoading] = useState(false);
  
  const handlePayment = async () => {
    try {
      setLoading(true);
      
      // Step 1: Create subscription
      const subscription = await createSubscription({
        subscriptionPlanId: selectedPlan.id,
        vehicleId: vehicle.id,
        startDate: new Date().toISOString()
      });
      
      // Step 2: Create VNPay payment
      const payment = await createVnPayPayment({
        subscriptionId: subscription.subscriptionId,
        orderInfo: `Thanh toán ${selectedPlan.name} - Tháng ${format(new Date(), 'MM/yyyy')}`,
        returnUrl: `${window.location.origin}/payment/callback`
      });
      
      // Step 3: Redirect to VNPay
      if (payment.success) {
        window.location.href = payment.paymentUrl;
      } else {
        toast.error(payment.message);
      }
    } catch (error) {
      toast.error('Có lỗi xảy ra');
    } finally {
      setLoading(false);
    }
  };
  
  return (
    <div className="payment-container">
      <h1>Xác nhận thanh toán</h1>
      
      <PaymentSummary>
        <Item>
          <Label>Gói:</Label>
          <Value>{selectedPlan.name}</Value>
        </Item>
        <Item>
          <Label>Số tiền:</Label>
          <Value>{formatVND(selectedPlan.monthlyPrice)}</Value>
        </Item>
        <Item>
          <Label>Chu kỳ:</Label>
          <Value>30 ngày</Value>
        </Item>
        <Item>
          <Label>Giới hạn:</Label>
          <Value>
            {selectedPlan.maxSwapsPerMonth 
              ? `${selectedPlan.maxSwapsPerMonth} lần/tháng`
              : 'Không giới hạn'}
          </Value>
        </Item>
      </PaymentSummary>
      
      <Button 
        onClick={handlePayment}
        disabled={loading}
        className="vnpay-button"
      >
        {loading ? 'Đang xử lý...' : 'Thanh toán qua VNPay'}
      </Button>
    </div>
  );
}
```

---

#### **Page 3: PaymentCallbackPage** 🔄
**Route:** `/payment/callback`

**Requirements:**
- Receive callback from VNPay (query params)
- Parse `vnp_ResponseCode` to determine success/failure
- Show success message if `vnp_ResponseCode === '00'`
- Show failure message if other response codes
- Auto-redirect to dashboard after 3 seconds

**URL Parameters from VNPay:**
```
/payment/callback?vnp_ResponseCode=00&vnp_TxnRef=EVB20251021143022...&vnp_Amount=45000000&...
```

**Component Structure:**
```
PaymentCallbackPage
├── LoadingSpinner (while processing)
├── SuccessMessage (if responseCode === '00')
│   ├── CheckIcon
│   ├── Message "Thanh toán thành công!"
│   └── RedirectCountdown
└── FailureMessage (if responseCode !== '00')
    ├── ErrorIcon
    ├── Message "Thanh toán thất bại"
    └── RetryButton
```

**Example Code:**
```typescript
// pages/PaymentCallbackPage.tsx
export function PaymentCallbackPage() {
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const [countdown, setCountdown] = useState(3);
  
  const responseCode = searchParams.get('vnp_ResponseCode');
  const isSuccess = responseCode === '00';
  
  useEffect(() => {
    if (isSuccess && countdown > 0) {
      const timer = setTimeout(() => {
        setCountdown(countdown - 1);
      }, 1000);
      return () => clearTimeout(timer);
    }
    
    if (isSuccess && countdown === 0) {
      navigate('/dashboard');
    }
  }, [isSuccess, countdown, navigate]);
  
  if (!responseCode) {
    return <LoadingSpinner message="Đang xử lý thanh toán..." />;
  }
  
  return (
    <div className="callback-container">
      {isSuccess ? (
        <SuccessCard>
          <CheckCircleIcon size={64} color="green" />
          <h1>Thanh toán thành công!</h1>
          <p>Gói subscription đã được kích hoạt.</p>
          <p>Tự động chuyển về dashboard sau {countdown}s...</p>
          <Button onClick={() => navigate('/dashboard')}>
            Về dashboard ngay
          </Button>
        </SuccessCard>
      ) : (
        <FailureCard>
          <XCircleIcon size={64} color="red" />
          <h1>Thanh toán thất bại</h1>
          <p>Mã lỗi: {responseCode}</p>
          <p>{getErrorMessage(responseCode)}</p>
          <Button onClick={() => navigate('/subscriptions/plans')}>
            Thử lại
          </Button>
        </FailureCard>
      )}
    </div>
  );
}

function getErrorMessage(code: string): string {
  const errorMessages: Record<string, string> = {
    '07': 'Trừ tiền thành công. Giao dịch bị nghi ngờ',
    '09': 'Giao dịch không thành công do thẻ chưa đăng ký dịch vụ',
    '10': 'Giao dịch không thành công do thẻ hết hạn',
    '11': 'Giao dịch không thành công do thẻ bị khóa',
    '12': 'Giao dịch không thành công do thẻ chưa kích hoạt',
    '13': 'Giao dịch không thành công do OTP không đúng',
    '24': 'Giao dịch bị hủy bởi khách hàng',
    '51': 'Giao dịch không thành công do tài khoản không đủ số dư',
    '65': 'Giao dịch không thành công do vượt quá hạn mức giao dịch',
    '75': 'Ngân hàng thanh toán đang bảo trì',
    '79': 'Giao dịch không thành công do nhập sai mật khẩu quá số lần quy định'
  };
  return errorMessages[code] || 'Lỗi không xác định';
}
```

---

## 📊 **PHASE 3: USAGE TRACKING DISPLAY**

### **Backend APIs Ready:**
- ✅ `GET /api/v1/subscriptions/mine` - Get active subscription with usage
- ✅ `GET /api/v1/subscriptions/mine/usage` - Get detailed usage stats

### **Frontend TODO:**

#### **Component 1: UsageDashboard** 🔄
**Location:** Dashboard page or separate `/usage` page

**Requirements:**
- Display current subscription plan name
- Show swap usage: "5/10 lần" or "12 lần (không giới hạn)"
- Progress bar showing usage percentage
- Days remaining until expiration
- Warning when near limit (≤2 swaps left)
- Warning when near expiration (≤7 days)
- Error state when expired
- Show total amount paid
- Show monthly usage history (last 6 months)

**API Call:**
```typescript
GET /api/v1/subscriptions/mine/usage
```

**Component Structure:**
```
UsageDashboard
├── SubscriptionHeader
│   ├── PlanName (Basic)
│   ├── StatusBadge (Active/Expired)
│   └── DaysRemaining (Còn 25 ngày)
├── UsageCard
│   ├── UsageTitle "Sử dụng tháng này"
│   ├── UsageCounter "5/10 lần"
│   ├── ProgressBar (50%)
│   └── WarningBanner (if near limit)
├── ExpirationCard
│   ├── ExpirationDate "Hết hạn: 20/11/2025"
│   ├── Countdown "Còn 25 ngày"
│   └── RenewalButton (if < 7 days)
├── StatisticsCard
│   ├── TotalSwaps (45 lần)
│   ├── TotalPaid (4,500,000 VND)
│   └── MonthlyAverage (7.5 lần/tháng)
└── MonthlyHistoryTable
    └── MonthRow (x6)
        ├── Month (Tháng 10/2025)
        ├── SwapCount (5 lần)
        ├── Fee (450,000 VND)
        └── Status (Đã thanh toán)
```

**Example Code:**
```typescript
// components/UsageDashboard.tsx
export function UsageDashboard() {
  const { data: usage, isLoading } = useFetch('/api/v1/subscriptions/mine/usage');
  
  if (isLoading) return <LoadingSpinner />;
  if (!usage) return <EmptyState message="Chưa có gói subscription" />;
  
  const remaining = usage.maxSwapsPerMonth 
    ? usage.maxSwapsPerMonth - usage.currentMonthSwapCount
    : Infinity;
  
  const percentage = usage.maxSwapsPerMonth
    ? (usage.currentMonthSwapCount / usage.maxSwapsPerMonth) * 100
    : 0;
  
  const isNearLimit = remaining <= 2 && remaining !== Infinity;
  const isNearExpiration = usage.daysRemaining && usage.daysRemaining <= 7;
  
  return (
    <div className="usage-dashboard">
      {/* Header */}
      <SubscriptionHeader>
        <PlanName>{usage.subscriptionPlanName}</PlanName>
        <StatusBadge type={usage.isExpired ? 'error' : 'success'}>
          {usage.isExpired ? 'Đã hết hạn' : 'Đang hoạt động'}
        </StatusBadge>
        {!usage.isExpired && usage.daysRemaining && (
          <DaysRemaining>
            Còn {usage.daysRemaining} ngày
          </DaysRemaining>
        )}
      </SubscriptionHeader>
      
      {/* Expiration Warning */}
      {usage.isExpired && (
        <Alert type="error">
          <AlertIcon>⚠️</AlertIcon>
          <AlertTitle>Gói đã hết hạn</AlertTitle>
          <AlertMessage>
            Gói subscription đã hết hạn vào {formatDate(usage.currentBillingPeriodEnd)}.
            Vui lòng đăng ký lại để tiếp tục sử dụng.
          </AlertMessage>
          <Button onClick={() => navigate('/subscriptions/plans')}>
            Đăng ký lại
          </Button>
        </Alert>
      )}
      
      {isNearExpiration && !usage.isExpired && (
        <Banner type="warning">
          <Icon>⏰</Icon>
          <Message>
            Gói sẽ hết hạn sau {usage.daysRemaining} ngày 
            (vào {formatDate(usage.currentBillingPeriodEnd)})
          </Message>
          <Button onClick={() => navigate('/payment')}>
            Gia hạn ngay
          </Button>
        </Banner>
      )}
      
      {/* Usage Card */}
      <UsageCard>
        <CardTitle>Sử dụng tháng này</CardTitle>
        <UsageCounter className={isNearLimit ? 'text-warning' : ''}>
          {usage.usageTier}
        </UsageCounter>
        <ProgressBar value={percentage} max={100} />
        
        {isNearLimit && (
          <Warning>
            ⚠️ Chỉ còn {remaining} lần! Cân nhắc nâng cấp gói.
          </Warning>
        )}
      </UsageCard>
      
      {/* Expiration Card */}
      <ExpirationCard>
        <CardTitle>Chu kỳ hiện tại</CardTitle>
        <DateRange>
          {formatDate(usage.currentBillingPeriodStart)} - 
          {formatDate(usage.currentBillingPeriodEnd)}
        </DateRange>
        <Fee>{formatVND(usage.currentMonthFee)}/tháng</Fee>
      </ExpirationCard>
      
      {/* Statistics */}
      <StatisticsCard>
        <Stat>
          <StatLabel>Tổng số lần đổi pin</StatLabel>
          <StatValue>{usage.totalSwapTransactions} lần</StatValue>
        </Stat>
        <Stat>
          <StatLabel>Tổng đã thanh toán</StatLabel>
          <StatValue>{formatVND(usage.totalAmountPaid)}</StatValue>
        </Stat>
      </StatisticsCard>
      
      {/* Monthly History */}
      <MonthlyHistoryTable>
        <TableHeader>
          <th>Tháng</th>
          <th>Số lần</th>
          <th>Phí</th>
          <th>Trạng thái</th>
        </TableHeader>
        <TableBody>
          {usage.monthlyUsage.map(month => (
            <TableRow key={`${month.year}-${month.month}`}>
              <td>{month.monthName}</td>
              <td>{month.usageTier}</td>
              <td>{formatVND(month.monthlyFee)}</td>
              <td>
                <StatusBadge type={month.isPaid ? 'success' : 'error'}>
                  {month.isPaid ? 'Đã thanh toán' : 'Chưa thanh toán'}
                </StatusBadge>
              </td>
            </TableRow>
          ))}
        </TableBody>
      </MonthlyHistoryTable>
    </div>
  );
}
```

---

#### **Component 2: UsageBanner** 🔄
**Location:** Reservation page, Dashboard header

**Requirements:**
- Compact display of current usage
- Show "5/10 lần" or "Còn 5 lần"
- Warning color when near limit
- Error color when limit reached or expired
- Click to navigate to full usage page

**API Call:**
```typescript
GET /api/v1/subscriptions/mine/usage
```

**Example Code:**
```typescript
// components/UsageBanner.tsx
export function UsageBanner() {
  const { data: usage } = useFetch('/api/v1/subscriptions/mine/usage');
  
  if (!usage) return null;
  
  const remaining = usage.maxSwapsPerMonth 
    ? usage.maxSwapsPerMonth - usage.currentMonthSwapCount
    : Infinity;
  
  const isNearLimit = remaining <= 2 && remaining !== Infinity;
  const isLimitReached = remaining === 0;
  
  const bannerType = isLimitReached || usage.isExpired 
    ? 'error' 
    : isNearLimit 
      ? 'warning' 
      : 'info';
  
  return (
    <Banner type={bannerType} onClick={() => navigate('/usage')}>
      <Icon>📊</Icon>
      <Message>
        {usage.isExpired 
          ? 'Gói đã hết hạn'
          : isLimitReached
            ? 'Đã hết lượt đổi pin'
            : `Bạn còn ${remaining} lần đổi pin trong tháng này`}
      </Message>
      <Badge>{usage.usageTier}</Badge>
    </Banner>
  );
}
```

---

#### **Component 3: ReservationGuard** 🔄
**Location:** Wrap around reservation creation flow

**Requirements:**
- Check if user has active subscription
- Check if subscription expired
- Check if swap limit reached
- Block reservation if any condition fails
- Show appropriate error message

**API Call:**
```typescript
GET /api/v1/subscriptions/mine/usage
```

**Example Code:**
```typescript
// components/ReservationGuard.tsx
export function ReservationGuard({ children }: { children: React.ReactNode }) {
  const { data: usage, isLoading } = useFetch('/api/v1/subscriptions/mine/usage');
  
  if (isLoading) return <LoadingSpinner />;
  
  if (!usage) {
    return (
      <Alert type="error">
        <AlertTitle>Chưa có gói subscription</AlertTitle>
        <AlertMessage>
          Bạn cần đăng ký gói subscription trước khi đặt lịch đổi pin.
        </AlertMessage>
        <Button onClick={() => navigate('/subscriptions/plans')}>
          Xem các gói
        </Button>
      </Alert>
    );
  }
  
  if (usage.isExpired) {
    return (
      <Alert type="error">
        <AlertTitle>Gói đã hết hạn</AlertTitle>
        <AlertMessage>
          Gói subscription đã hết hạn vào {formatDate(usage.currentBillingPeriodEnd)}.
        </AlertMessage>
        <Button onClick={() => navigate('/subscriptions/plans')}>
          Đăng ký lại
        </Button>
      </Alert>
    );
  }
  
  const remaining = usage.maxSwapsPerMonth 
    ? usage.maxSwapsPerMonth - usage.currentMonthSwapCount
    : Infinity;
  
  if (remaining === 0) {
    return (
      <Alert type="error">
        <AlertTitle>Đã hết lượt đổi pin</AlertTitle>
        <AlertMessage>
          Bạn đã sử dụng hết {usage.maxSwapsPerMonth} lần trong tháng này.
          Chu kỳ mới bắt đầu từ {formatDate(usage.currentBillingPeriodEnd)}.
        </AlertMessage>
        <Button onClick={() => navigate('/subscriptions/plans')}>
          Nâng cấp gói
        </Button>
      </Alert>
    );
  }
  
  return <>{children}</>;
}

// Usage in ReservationPage:
export function ReservationPage() {
  return (
    <ReservationGuard>
      <ReservationFlow />
    </ReservationGuard>
  );
}
```

---

## 🧪 **TESTING GUIDE**

### **Test Phase 2: Payment Flow**

**Test Case 1: Happy Path**
```
1. User selects Basic plan (450k)
2. System creates subscription (inactive)
3. System generates VNPay URL
4. User redirects to VNPay sandbox
5. User enters test card info
6. VNPay redirects to /payment/callback?vnp_ResponseCode=00
7. System activates subscription (isActive = true)
8. User sees success message
9. User redirected to dashboard
```

**Test Case 2: Payment Failure**
```
1-4. Same as happy path
5. User cancels payment or enters wrong info
6. VNPay redirects to /payment/callback?vnp_ResponseCode=24
7. System keeps subscription inactive
8. User sees failure message
9. User can retry
```

### **Test Phase 3: Usage Display**

**Test Case 1: Normal Usage**
```
Given: User has subscription with 5/10 swaps used
When: User views usage dashboard
Then: Shows "5/10 lần" with 50% progress bar
```

**Test Case 2: Near Limit**
```
Given: User has 9/10 swaps used
When: User views usage dashboard
Then: Shows warning "Chỉ còn 1 lần!"
```

**Test Case 3: Expired**
```
Given: User's subscription expired yesterday
When: User views usage dashboard
Then: Shows error "Gói đã hết hạn" with re-subscribe button
```

**Test Case 4: Block Reservation**
```
Given: User has 10/10 swaps used
When: User tries to create reservation
Then: Shows error "Đã hết lượt" and blocks action
```

---

## ✅ **IMPLEMENTATION CHECKLIST**

### **Phase 2: Payment**
- [ ] Create `SubscriptionPlansPage.tsx`
  - [ ] Fetch plans from API
  - [ ] Display plan cards
  - [ ] Filter by vehicle battery
  - [ ] Handle plan selection
- [ ] Create `PaymentPage.tsx`
  - [ ] Create subscription via API
  - [ ] Create VNPay payment
  - [ ] Redirect to VNPay
- [ ] Create `PaymentCallbackPage.tsx`
  - [ ] Parse callback params
  - [ ] Show success/failure
  - [ ] Auto-redirect
- [ ] Test end-to-end payment flow
- [ ] Test error cases (failed payment, timeout, etc.)

### **Phase 3: Usage Tracking**
- [ ] Create `UsageDashboard.tsx`
  - [ ] Fetch usage from API
  - [ ] Display usage counter
  - [ ] Show progress bar
  - [ ] Display expiration info
  - [ ] Show monthly history
- [ ] Create `UsageBanner.tsx`
  - [ ] Compact usage display
  - [ ] Warning states
  - [ ] Navigate to full page
- [ ] Create `ReservationGuard.tsx`
  - [ ] Check subscription status
  - [ ] Check expiration
  - [ ] Check swap limit
  - [ ] Block if needed
- [ ] Test usage display updates after swap
- [ ] Test expiration warnings
- [ ] Test limit enforcement

---

## 📖 **API DOCUMENTATION SUMMARY**

### **Phase 2 APIs:**
```
GET  /api/v1/subscription-plans          ✅ Ready
POST /api/v1/subscriptions                ✅ Ready
POST /api/v1/payments/vnpay/create        ✅ Ready
GET  /api/v1/payments/vnpay/callback      ✅ Ready (auto-handled)
```

### **Phase 3 APIs:**
```
GET  /api/v1/subscriptions/mine           ✅ Ready
GET  /api/v1/subscriptions/mine/usage     ✅ Ready
```

### **All APIs tested in:**
`COMPLETE_API_TEST.http` ✅

---

**Last Updated:** October 21, 2025  
**Status:** Backend 100% Ready | Frontend Ready for Implementation
