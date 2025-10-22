# 🔄 USER JOURNEY FLOW - UPDATED & VALIDATED
## Luồng người dùng thực tế dựa trên Backend API hiện tại

> **Cập nhật:** 22/10/2025  
> **Trạng thái Backend:** ✅ 100% Complete  
> **Frontend:** 🔄 Cần implement Phase 2 & 3

---

## 📊 OVERVIEW - CÁC PHASE HOÀN THÀNH

| Phase | Tên | Backend Status | Frontend Status | Ưu tiên |
|-------|-----|----------------|-----------------|---------|
| ✅ 1 | Registration & Vehicle Setup | ✅ Complete | ✅ Complete | - |
| 🔄 2 | Subscription & Payment | ✅ Complete | 🔄 **TODO** | ⭐⭐⭐ |
| 🔄 3 | Usage Tracking Display | ✅ Complete | 🔄 **TODO** | ⭐⭐⭐ |
| ✅ 4 | Reservation System | ✅ Complete | ✅ Complete | - |
| ✅ 5 | Swap Execution & Counter | ✅ Complete | ✅ Complete | - |
| ✅ 6 | Limit Enforcement | ✅ Complete | ✅ Complete | - |
| ✅ 7 | Auto-Expire Middleware | ✅ Complete | N/A | - |

---

## 🎯 LUỒNG 1: REGISTRATION & VEHICLE SETUP
### ✅ Phase 1 - ĐÃ HOÀN THÀNH

```
┌─────────────────────────────────────────────────────────────────┐
│  PHASE 1: REGISTRATION & VEHICLE SETUP                          │
│  Backend: ✅ Complete | Frontend: ✅ Complete                    │
└─────────────────────────────────────────────────────────────────┘
```

### **Bước 1.1: Đăng ký tài khoản**
```http
POST /api/v1/auth/register
Content-Type: application/json

{
  "email": "user@example.com",
  "password": "SecurePass123!",
  "phoneNumber": "0901234567",
  "fullName": "Nguyễn Văn A"
}
```

**Response:**
```json
{
  "userId": "guid-user-123",
  "message": "Đăng ký thành công! Vui lòng đăng nhập."
}
```

---

### **Bước 1.2: Đăng nhập**
```http
POST /api/v1/auth/login
Content-Type: application/json

{
  "email": "user@example.com",
  "password": "SecurePass123!"
}
```

**Response:**
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "userId": "guid-user-123",
  "role": "Driver",
  "fullName": "Nguyễn Văn A"
}
```

**Frontend Action:**
- Lưu `accessToken` vào localStorage/sessionStorage
- Set Authorization header cho các request tiếp theo: `Bearer {accessToken}`

---

### **Bước 1.3: Liên kết xe (Link Vehicle)**
```http
POST /api/v1/vehicles
Authorization: Bearer {accessToken}
Content-Type: application/json

{
  "licensePlate": "51-A1 12345",
  "vehicleModelId": "guid-vf3-model",
  "vin": "VF3ABC123456789"
}
```

**Response:**
```json
{
  "id": "guid-vehicle-001",
  "plate": "51-A1 12345",
  "vin": "VF3ABC123456789",
  "vehicleModel": {
    "name": "VF3",
    "batteryCapacity": 18.64
  },
  "compatibleBatteryModelId": "guid-battery-model-vf3",
  "compatibleModel": {
    "name": "Pin VF3 - 18.64 kWh",
    "capacity": 18.64
  }
}
```

**⚠️ LƯU Ý:**
- Hệ thống **TỰ ĐỘNG** detect `compatibleBatteryModelId` dựa trên `vehicleModelId`
- VF3 → Pin VF3 (18.64 kWh)
- VF5 → Pin VF5 (37.23 kWh)
- VF8 → Pin VF8 (87.7 kWh)
- VF9 → Pin VF9 (92 kWh)

---

## 🎯 LUỒNG 2: SUBSCRIPTION & PAYMENT
### 🔄 Phase 2 - BACKEND ✅ | FRONTEND 🔄 TODO

```
┌─────────────────────────────────────────────────────────────────┐
│  PHASE 2: SUBSCRIPTION SELECTION & PAYMENT                      │
│  Backend: ✅ Complete | Frontend: 🔄 TODO                        │
└─────────────────────────────────────────────────────────────────┘
```

### **Bước 2.1: Xem danh sách gói subscription**
```http
GET /api/v1/subscription-plans
Authorization: Bearer {accessToken}
```

**Response:**
```json
[
  {
    "id": "guid-basic",
    "name": "Gói Basic - 10 lần/tháng",
    "description": "Phù hợp cho người dùng thường xuyên",
    "monthlyPrice": 450000,
    "maxSwapsPerMonth": 10,
    "requiresDeposit": false,
    "depositAmount": 0,
    "benefits": "Tiết kiệm 10%, Ưu tiên đặt chỗ",
    "refundPolicy": "Hoàn tiền theo tỷ lệ ngày còn lại",
    "batteryModelId": "guid-battery-vf3",
    "batteryModelName": "Pin VF3 - 18.64 kWh"
  },
  {
    "id": "guid-standard",
    "name": "Gói Standard - 20 lần/tháng",
    "monthlyPrice": 850000,
    "maxSwapsPerMonth": 20,
    "batteryModelName": "Pin VF3 - 18.64 kWh"
  },
  {
    "id": "guid-premium",
    "name": "Gói Premium - Không giới hạn",
    "monthlyPrice": 1500000,
    "maxSwapsPerMonth": null,
    "batteryModelName": "Pin VF3 - 18.64 kWh"
  }
]
```

**Frontend UI:**
```
┌─────────────────────────────────────────────┐
│  Chọn Gói Subscription                      │
├─────────────────────────────────────────────┤
│  📦 Gói Basic - 450,000 VND/tháng           │
│  ✓ 10 lần đổi pin/tháng                     │
│  ✓ Ưu tiên đặt chỗ                          │
│  ✓ Tiết kiệm 10%                            │
│  [Chọn gói này]                             │
├─────────────────────────────────────────────┤
│  📦 Gói Standard - 850,000 VND/tháng        │
│  ✓ 20 lần đổi pin/tháng                     │
│  [Chọn gói này]                             │
├─────────────────────────────────────────────┤
│  📦 Gói Premium - 1,500,000 VND/tháng       │
│  ✓ Không giới hạn số lần                    │
│  [Chọn gói này]                             │
└─────────────────────────────────────────────┘
```

---

### **Bước 2.2: Tạo subscription (Chọn gói)**
```http
POST /api/v1/subscriptions
Authorization: Bearer {accessToken}
Content-Type: application/json

{
  "subscriptionPlanId": "guid-basic",
  "vehicleId": "guid-vehicle-001",
  "startDate": null  // null = bắt đầu ngay hôm nay
}
```

**Response:**
```json
{
  "subscriptionId": "guid-subscription-001",
  "message": "Đăng ký gói Gói Basic - 10 lần/tháng thành công!",
  "requiresDeposit": false,
  "depositAmount": 0,
  "monthlyPrice": 450000,
  "maxSwapsPerMonth": 10,
  "startDate": "2025-01-20T00:00:00Z",
  "billingPeriodStart": "2025-01-20T00:00:00Z",
  "billingPeriodEnd": "2025-02-19T23:59:59Z"
}
```

**⚠️ QUAN TRỌNG:**
- Subscription được tạo nhưng `IsActive = false` cho đến khi thanh toán thành công
- Chu kỳ thanh toán: **30 ngày** (từ `billingPeriodStart` đến `billingPeriodEnd`)
- Counter `currentMonthSwapCount` = 0 khi khởi tạo

**Frontend Action:**
```javascript
// Lưu subscriptionId để track payment
localStorage.setItem('pendingSubscriptionId', response.subscriptionId);

// Redirect đến payment page với amount
window.location.href = `/payment?amount=${response.monthlyPrice}&description=Gói Basic - tháng 1/2025`;
```

---

### **Bước 2.3: Tạo URL thanh toán VNPay**
```http
POST /api/v1/payments/vnpay/create-payment-url
Authorization: Bearer {accessToken}
Content-Type: application/json

{
  "amount": 450000,
  "description": "Thanh toán gói Basic - tháng 1/2025",
  "returnUrl": "https://yourapp.com/payment/callback"
}
```

**Response:**
```json
{
  "paymentUrl": "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html?vnp_Amount=45000000&vnp_Command=pay&vnp_TxnRef=PAY20250120123456..."
}
```

**Frontend Action:**
```javascript
// Redirect user đến VNPay
window.location.href = response.paymentUrl;
```

---

### **Bước 2.4: VNPay Callback (sau khi user thanh toán)**
```http
GET /api/v1/payments/vnpay/callback?vnp_TxnRef=PAY20250120123456&vnp_ResponseCode=00&vnp_Amount=45000000&vnp_SecureHash=abc123...
```

**⚠️ VNPay Response Codes:**
- `00` = Thành công
- `24` = User hủy giao dịch
- `09` = Chưa đăng ký InternetBanking
- `51` = Không đủ số dư

**Nếu `vnp_ResponseCode = 00` (Success):**
```json
{
  "success": true,
  "message": "Thanh toán thành công!",
  "transactionId": "PAY20250120123456",
  "amount": 450000,
  "paymentTime": "2025-01-20T14:30:00Z"
}
```

**Backend Action (Automatic):**
1. Verify signature từ VNPay
2. Tìm Payment record với `TransactionId = vnp_TxnRef`
3. Update `Status = Completed`
4. **Activate subscription:** `IsActive = true`
5. Update `LastPaymentDate = DateTime.UtcNow`

**Frontend Action:**
```javascript
// Show success message
alert('Thanh toán thành công! Subscription đã được kích hoạt.');

// Clear pending subscription
localStorage.removeItem('pendingSubscriptionId');

// Redirect về dashboard
window.location.href = '/dashboard';
```

**Nếu `vnp_ResponseCode != 00` (Failed):**
```json
{
  "success": false,
  "message": "Thanh toán thất bại: Không đủ số dư",
  "errorCode": "51"
}
```

**Frontend Action:**
```javascript
alert('Thanh toán thất bại! Vui lòng thử lại.');
window.location.href = '/subscription-plans';
```

---

## 🎯 LUỒNG 3: USAGE TRACKING & DASHBOARD
### 🔄 Phase 3 - BACKEND ✅ | FRONTEND 🔄 TODO

```
┌─────────────────────────────────────────────────────────────────┐
│  PHASE 3: USAGE TRACKING DISPLAY                                │
│  Backend: ✅ Complete | Frontend: 🔄 TODO                        │
└─────────────────────────────────────────────────────────────────┘
```

### **Bước 3.1: Xem thông tin subscription hiện tại**
```http
GET /api/v1/subscriptions/mine
Authorization: Bearer {accessToken}
```

**Response:**
```json
{
  "id": "guid-subscription-001",
  "userId": "guid-user-123",
  "startDate": "2025-01-20T00:00:00Z",
  "endDate": null,
  "isActive": true,
  "isExpired": false,
  "daysRemaining": 25,
  "currentBillingPeriodStart": "2025-01-20T00:00:00Z",
  "currentBillingPeriodEnd": "2025-02-19T23:59:59Z",
  "currentMonthSwapCount": 5,
  "subscriptionPlan": {
    "name": "Gói Basic - 10 lần/tháng",
    "monthlyPrice": 450000,
    "maxSwapsPerMonth": 10
  },
  "vehicle": {
    "plate": "51-A1 12345",
    "model": "VF3"
  }
}
```

**Frontend Dashboard UI:**
```
┌─────────────────────────────────────────────┐
│  📊 Dashboard - Gói của bạn                 │
├─────────────────────────────────────────────┤
│  Gói: Gói Basic - 10 lần/tháng              │
│  Xe: VF3 (51-A1 12345)                      │
│                                             │
│  ┌─────────────────────────────────────┐   │
│  │  Đã sử dụng: 5/10 lần               │   │
│  │  ████████░░░░░░░░░░░░ 50%           │   │
│  └─────────────────────────────────────┘   │
│                                             │
│  ⏰ Còn 25 ngày trong chu kỳ hiện tại       │
│  📅 Hết hạn: 19/02/2025                     │
│                                             │
│  [Xem lịch sử] [Đặt lịch swap]             │
└─────────────────────────────────────────────┘
```

**⚠️ TRƯỜNG HỢP ĐẶC BIỆT:**

**Nếu `isExpired = true`:**
```json
{
  "isActive": false,
  "isExpired": true,
  "daysRemaining": null,
  "message": "Subscription đã hết hạn. Vui lòng gia hạn."
}
```

**Frontend UI khi expired:**
```
┌─────────────────────────────────────────────┐
│  ⚠️ Subscription đã hết hạn                 │
├─────────────────────────────────────────────┤
│  Gói của bạn đã hết hạn vào 19/02/2025      │
│                                             │
│  [Gia hạn ngay] [Đổi gói khác]              │
└─────────────────────────────────────────────┘
```

---

### **Bước 3.2: Xem chi tiết usage (Lịch sử sử dụng)**
```http
GET /api/v1/subscriptions/mine/usage
Authorization: Bearer {accessToken}
```

**Response:**
```json
{
  "subscriptionId": "guid-subscription-001",
  "subscriptionPlanName": "Gói Basic - 10 lần/tháng",
  "vehiclePlate": "51-A1 12345",
  "currentBillingPeriodStart": "2025-01-20T00:00:00Z",
  "currentBillingPeriodEnd": "2025-02-19T23:59:59Z",
  "currentMonthSwapCount": 5,
  "maxSwapsPerMonth": 10,
  "currentMonthFee": 450000,
  "usageTier": "5/10 lần",
  "totalSwapTransactions": 15,
  "totalAmountPaid": 1350000,
  "monthlyUsage": [
    {
      "year": 2024,
      "month": 11,
      "monthName": "tháng mười một",
      "swapCount": 8,
      "monthlyFee": 450000,
      "usageTier": "8/10 lần",
      "isPaid": true
    },
    {
      "year": 2024,
      "month": 12,
      "monthName": "tháng mười hai",
      "swapCount": 2,
      "monthlyFee": 450000,
      "usageTier": "2/10 lần",
      "isPaid": true
    },
    {
      "year": 2025,
      "month": 1,
      "monthName": "tháng một",
      "swapCount": 5,
      "monthlyFee": 450000,
      "usageTier": "5/10 lần (đang sử dụng)",
      "isPaid": true
    }
  ]
}
```

**Frontend Usage History UI:**
```
┌─────────────────────────────────────────────┐
│  📈 Lịch sử sử dụng                         │
├─────────────────────────────────────────────┤
│  Tổng đã thanh toán: 1,350,000 VND          │
│  Tổng số lần swap: 15 lần                   │
│                                             │
│  ┌─────────────────────────────────────┐   │
│  │  Tháng 11/2024: 8/10 lần ✅ Đã TT  │   │
│  │  Tháng 12/2024: 2/10 lần ✅ Đã TT  │   │
│  │  Tháng 01/2025: 5/10 lần 🔄 Đang   │   │
│  └─────────────────────────────────────┘   │
└─────────────────────────────────────────────┘
```

---

## 🎯 LUỒNG 4: RESERVATION SYSTEM
### ✅ Phase 4 - ĐÃ HOÀN THÀNH

```
┌─────────────────────────────────────────────────────────────────┐
│  PHASE 4: RESERVATION WITH SUBSCRIPTION                         │
│  Backend: ✅ Complete | Frontend: ✅ Complete                    │
└─────────────────────────────────────────────────────────────────┘
```

### **Bước 4.1: Xem danh sách trạm**
```http
GET /api/v1/stations?city=Ho Chi Minh City&district=Quan 1
Authorization: Bearer {accessToken}
```

**Response:**
```json
[
  {
    "id": "guid-station-001",
    "name": "Trạm Quận 1 - Nguyễn Huệ",
    "address": "123 Nguyễn Huệ, Quận 1",
    "latitude": 10.7769,
    "longitude": 106.7009,
    "batteryModels": [
      {
        "id": "guid-battery-vf3",
        "name": "Pin VF3 - 18.64 kWh",
        "availableCount": 5
      }
    ]
  }
]
```

---

### **Bước 4.2: Xem khung giờ available**
```http
GET /api/v1/slot-reservations/available-slots?stationId=guid-station-001&date=2025-01-25&batteryModelId=guid-battery-vf3
Authorization: Bearer {accessToken}
```

**Response:**
```json
[
  {
    "slotStartTime": "08:00:00",
    "slotEndTime": "09:00:00",
    "totalCapacity": 3,
    "currentReservations": 1,
    "isAvailable": true
  },
  {
    "slotStartTime": "10:00:00",
    "slotEndTime": "11:00:00",
    "totalCapacity": 3,
    "currentReservations": 0,
    "isAvailable": true
  },
  {
    "slotStartTime": "14:00:00",
    "slotEndTime": "15:00:00",
    "totalCapacity": 3,
    "currentReservations": 3,
    "isAvailable": false
  }
]
```

**Frontend Slot Selection UI:**
```
┌─────────────────────────────────────────────┐
│  📅 Chọn khung giờ - 25/01/2025             │
├─────────────────────────────────────────────┤
│  ✅ 08:00 - 09:00  (1/3 chỗ)  [Chọn]       │
│  ✅ 10:00 - 11:00  (0/3 chỗ)  [Chọn]       │
│  ❌ 14:00 - 15:00  (3/3 chỗ)  Đã đầy        │
└─────────────────────────────────────────────┘
```

---

### **Bước 4.3: Tạo reservation**
```http
POST /api/v1/slot-reservations
Authorization: Bearer {accessToken}
Content-Type: application/json

{
  "stationId": "guid-station-001",
  "batteryModelId": "guid-battery-vf3",
  "slotDate": "2025-01-25",
  "slotStartTime": "10:00:00",
  "slotEndTime": "11:00:00"
}
```

**⚠️ HỆ THỐNG VALIDATE:**
1. ✅ User chỉ có **1 active reservation** tại 1 thời điểm
2. ✅ Slot phải còn chỗ trống (`currentReservations < totalCapacity`)
3. ✅ Slot phải trong vòng **7 ngày** tới
4. ✅ Battery tương thích với xe
5. ❌ **KHÔNG CHECK** subscription limit tại đây (chỉ check khi swap thực tế)

**Response:**
```json
{
  "id": "guid-reservation-001",
  "stationId": "guid-station-001",
  "stationName": "Trạm Quận 1 - Nguyễn Huệ",
  "slotDate": "2025-01-25",
  "slotStartTime": "10:00:00",
  "slotEndTime": "11:00:00",
  "status": "Pending",
  "qrCode": "RES-001-HASH123456",
  "createdAt": "2025-01-20T15:00:00Z"
}
```

**Frontend Success UI:**
```
┌─────────────────────────────────────────────┐
│  ✅ Đặt lịch thành công!                    │
├─────────────────────────────────────────────┤
│  📍 Trạm: Quận 1 - Nguyễn Huệ               │
│  📅 Ngày: 25/01/2025                        │
│  ⏰ Giờ: 10:00 - 11:00                      │
│                                             │
│  QR Code:                                   │
│  ┌───────────┐                              │
│  │ [QR IMG]  │  ← Hiển thị để staff scan    │
│  └───────────┘                              │
│                                             │
│  [Xem chi tiết] [Hủy lịch]                 │
└─────────────────────────────────────────────┘
```

---

## 🎯 LUỒNG 5: SWAP EXECUTION & COUNTER INCREMENT
### ✅ Phase 5 - ĐÃ HOÀN THÀNH

```
┌─────────────────────────────────────────────────────────────────┐
│  PHASE 5: SWAP EXECUTION & USAGE INCREMENT                      │
│  Backend: ✅ Complete | Frontend: ✅ Complete (Staff App)        │
└─────────────────────────────────────────────────────────────────┘
```

### **Bước 5.1: Staff check-in reservation (tại trạm)**
```http
POST /api/v1/slot-reservations/{reservationId}/checkin
Authorization: Bearer {staffAccessToken}
```

**Response:**
```json
{
  "id": "guid-reservation-001",
  "status": "CheckedIn",
  "checkedInAt": "2025-01-25T10:05:00Z",
  "message": "Check-in thành công!"
}
```

---

### **Bước 5.2: Staff bắt đầu swap**
```http
POST /api/v1/swap-transactions
Authorization: Bearer {staffAccessToken}
Content-Type: application/json

{
  "vehicleId": "guid-vehicle-001",
  "stationId": "guid-station-001",
  "reservationId": "guid-reservation-001",
  "notes": "Khách hàng yêu cầu kiểm tra pin"
}
```

**Response:**
```json
{
  "id": "guid-swap-001",
  "transactionNumber": "SWAP20250125001",
  "status": "CheckedIn",
  "paymentType": "Subscription",
  "userId": "guid-user-123",
  "startedAt": "2025-01-25T10:10:00Z"
}
```

**⚠️ LƯU Ý:**
- `paymentType = Subscription` nếu user có active subscription
- `paymentType = PayPerSwap` nếu user không có subscription (trả phí mỗi lần)
- Status = `CheckedIn` (chưa complete)

---

### **Bước 5.3: Staff hoàn tất swap (QUAN TRỌNG - INCREMENT COUNTER)**
```http
PUT /api/v1/swap-transactions/{swapId}/complete
Authorization: Bearer {driverAccessToken}
Content-Type: application/json

{
  "returnedBatterySerial": "BAT-OLD-12345",
  "batteryHealthReturned": 85.5,
  "notes": "Đổi pin thành công, không vấn đề"
}
```

**⚠️ HỆ THỐNG VALIDATE (TRƯỚC KHI COMPLETE):**
```csharp
// Check swap limit BEFORE completing
if (subscription != null && subscription.SubscriptionPlan.MaxSwapsPerMonth.HasValue)
{
    // Kiểm tra ĐÃ ĐẠT giới hạn chưa
    if (subscription.CurrentMonthSwapCount >= subscription.SubscriptionPlan.MaxSwapsPerMonth.Value)
    {
        throw new InvalidOperationException(
            $"Đã đạt giới hạn {subscription.SubscriptionPlan.MaxSwapsPerMonth} lần đổi pin trong tháng này. " +
            $"Hiện tại: {subscription.CurrentMonthSwapCount}/{subscription.SubscriptionPlan.MaxSwapsPerMonth} lần."
        );
    }
}

// ✅ If pass validation → Complete swap
swap.Status = SwapTransactionStatus.Completed;
swap.CompletedAt = DateTime.UtcNow;

// ✅ INCREMENT COUNTER
subscription.CurrentMonthSwapCount++;
```

**Response (Success):**
```json
{
  "id": "guid-swap-001",
  "transactionNumber": "SWAP20250125001",
  "status": "Completed",
  "completedAt": "2025-01-25T10:25:00Z",
  "userSubscription": {
    "currentMonthSwapCount": 6,
    "maxSwapsPerMonth": 10,
    "usageTier": "6/10 lần"
  },
  "message": "Đổi pin thành công! Bạn đã sử dụng 6/10 lần trong tháng này."
}
```

**Frontend Driver App - Success Notification:**
```
┌─────────────────────────────────────────────┐
│  ✅ Đổi pin thành công!                     │
├─────────────────────────────────────────────┤
│  📍 Trạm: Quận 1 - Nguyễn Huệ               │
│  ⏰ Thời gian: 10:10 - 10:25 (15 phút)      │
│                                             │
│  📊 Đã sử dụng: 6/10 lần                    │
│  ██████████████░░░░░░░ 60%                  │
│                                             │
│  Còn 4 lần trong tháng này                  │
│                                             │
│  [Xem chi tiết] [Đặt lịch tiếp]            │
└─────────────────────────────────────────────┘
```

---

## 🎯 LUỒNG 6: LIMIT ENFORCEMENT
### ✅ Phase 6 - ĐÃ HOÀN THÀNH

```
┌─────────────────────────────────────────────────────────────────┐
│  PHASE 6: USAGE LIMIT ENFORCEMENT                               │
│  Backend: ✅ Complete | Frontend: ✅ Complete                    │
└─────────────────────────────────────────────────────────────────┘
```

### **Tình huống 6.1: Đã dùng 9/10 lần - Hiển thị warning**

**GET /api/v1/subscriptions/mine/usage:**
```json
{
  "currentMonthSwapCount": 9,
  "maxSwapsPerMonth": 10,
  "usageTier": "9/10 lần",
  "daysRemaining": 15
}
```

**Frontend Warning UI:**
```
┌─────────────────────────────────────────────┐
│  ⚠️ Sắp hết lượt đổi pin                    │
├─────────────────────────────────────────────┤
│  Bạn đã sử dụng: 9/10 lần                   │
│  ████████████████████░░ 90%                 │
│                                             │
│  ⚠️ Chỉ còn 1 lần đổi pin!                  │
│  Còn 15 ngày trong chu kỳ hiện tại          │
│                                             │
│  💡 Cân nhắc nâng cấp lên gói cao hơn?      │
│  [Xem gói khác] [Tiếp tục]                  │
└─────────────────────────────────────────────┘
```

---

### **Tình huống 6.2: Lần thứ 11 - BLOCK SWAP (ĐÃ VƯỢ T LIMIT)**

**PUT /api/v1/swap-transactions/{swapId}/complete:**

**Response (Error 400):**
```json
{
  "error": "InvalidOperation",
  "message": "Đã đạt giới hạn 10 lần đổi pin trong tháng này. Hiện tại: 10/10 lần. Vui lòng nâng cấp gói hoặc chờ đến chu kỳ thanh toán tiếp theo (từ 2025-02-19).",
  "details": {
    "currentCount": 10,
    "maxCount": 10,
    "nextBillingDate": "2025-02-19T23:59:59Z"
  }
}
```

**Frontend Error UI:**
```
┌─────────────────────────────────────────────┐
│  ❌ Không thể đổi pin                       │
├─────────────────────────────────────────────┤
│  Bạn đã sử dụng hết 10/10 lần               │
│  trong tháng này.                           │
│                                             │
│  📅 Chu kỳ tiếp theo: 19/02/2025            │
│                                             │
│  Lựa chọn của bạn:                          │
│  1️⃣ Đợi đến 19/02 (reset về 0/10)          │
│  2️⃣ Nâng cấp lên gói không giới hạn        │
│                                             │
│  [Đợi chu kỳ mới] [Nâng cấp ngay]          │
└─────────────────────────────────────────────┘
```

---

## 🎯 LUỒNG 7: AUTO-EXPIRE & BILLING RENEWAL
### ✅ Phase 7 - ĐÃ HOÀN THÀNH (Backend Middleware)

```
┌─────────────────────────────────────────────────────────────────┐
│  PHASE 7: AUTO-EXPIRE MIDDLEWARE                                │
│  Backend: ✅ Complete (Middleware) | Frontend: N/A               │
└─────────────────────────────────────────────────────────────────┘
```

### **7.1: Middleware tự động check expiration**

**Code Implementation:**
```csharp
// Program.cs - Middleware
app.Use(async (context, next) =>
{
    var subscriptionService = context.RequestServices.GetRequiredService<ISubscriptionService>();
    await subscriptionService.CheckAndExpireSubscriptionsAsync();
    await next();
});
```

**Logic:**
```csharp
public async Task CheckAndExpireSubscriptionsAsync()
{
    var now = DateTime.UtcNow;
    
    // Tìm subscriptions hết hạn (currentBillingPeriodEnd < now)
    var expiredSubscriptions = await _context.UserSubscriptions
        .Where(us => us.IsActive && us.CurrentBillingPeriodEnd < now)
        .ToListAsync();
    
    foreach (var subscription in expiredSubscriptions)
    {
        // ❌ Set inactive
        subscription.IsActive = false;
        subscription.UpdatedAt = now;
    }
    
    await _context.SaveChangesAsync();
}
```

**⚠️ KẾT QUẢ:**
- Nếu `CurrentBillingPeriodEnd` đã qua (ví dụ: 19/02/2025 23:59:59)
- Hệ thống **TỰ ĐỘNG** set `IsActive = false`
- User **KHÔNG THỂ** đặt lịch hoặc swap cho đến khi gia hạn

---

### **7.2: User thấy thông báo hết hạn**

**GET /api/v1/subscriptions/mine:**
```json
{
  "isActive": false,
  "isExpired": true,
  "currentBillingPeriodEnd": "2025-02-19T23:59:59Z",
  "message": "Subscription đã hết hạn. Vui lòng gia hạn để tiếp tục sử dụng."
}
```

**Frontend Expired UI:**
```
┌─────────────────────────────────────────────┐
│  ⚠️ Subscription đã hết hạn                 │
├─────────────────────────────────────────────┤
│  Gói của bạn đã hết hạn vào 19/02/2025      │
│                                             │
│  Vui lòng gia hạn để tiếp tục sử dụng:     │
│                                             │
│  [Gia hạn gói hiện tại - 450,000 VND]      │
│  [Đổi sang gói khác]                        │
└─────────────────────────────────────────────┘
```

---

### **7.3: User gia hạn subscription**

**POST /api/v1/payments/vnpay/create-payment-url:**
```json
{
  "amount": 450000,
  "description": "Gia hạn gói Basic - tháng 2/2025",
  "returnUrl": "https://yourapp.com/payment/callback"
}
```

**Sau khi thanh toán thành công:**
1. Backend tự động update:
   - `IsActive = true`
   - `CurrentBillingPeriodStart = 2025-02-19`
   - `CurrentBillingPeriodEnd = 2025-03-21` (30 ngày tiếp)
   - `CurrentMonthSwapCount = 0` (RESET counter)
   - `LastPaymentDate = DateTime.UtcNow`

**Response:**
```json
{
  "success": true,
  "message": "Gia hạn thành công! Subscription đã được kích hoạt lại.",
  "newBillingPeriodStart": "2025-02-19T00:00:00Z",
  "newBillingPeriodEnd": "2025-03-21T23:59:59Z",
  "currentMonthSwapCount": 0
}
```

---

## 📊 TỔNG KẾT - CHECKLIST CHO FRONTEND TEAM

### ✅ ĐÃ HOÀN THÀNH (Backend + Frontend)
- [x] Phase 1: Registration & Vehicle Setup
- [x] Phase 4: Reservation System
- [x] Phase 5: Swap Execution (Staff App)
- [x] Phase 6: Limit Enforcement Logic
- [x] Phase 7: Auto-Expire Middleware

### 🔄 CẦN IMPLEMENT (Frontend Only)
- [ ] **Phase 2: Subscription & Payment UI**
  - [ ] `SubscriptionPlansPage` - Hiển thị danh sách gói
  - [ ] `PaymentPage` - VNPay redirect
  - [ ] `PaymentCallbackPage` - Handle callback từ VNPay
  
- [ ] **Phase 3: Usage Tracking UI**
  - [ ] `DashboardPage` - Overview subscription status
  - [ ] `UsageHistoryPage` - Lịch sử 6 tháng gần nhất
  - [ ] Warning banner khi gần hết lượt (9/10)
  - [ ] Error modal khi vượt limit (11/10)
  - [ ] Expired notification khi hết hạn

---

## 🎯 RECOMMENDED FRONTEND IMPLEMENTATION ORDER

### **Sprint 1: Subscription Plans & Payment** (3-5 ngày)
1. Tạo `SubscriptionPlansPage`
   - GET `/api/v1/subscription-plans`
   - Hiển thị danh sách gói dạng card
   - Button "Chọn gói"

2. Tạo `PaymentPage`
   - POST `/api/v1/subscriptions` → lưu subscriptionId
   - POST `/api/v1/payments/vnpay/create-payment-url`
   - Redirect đến VNPay

3. Tạo `PaymentCallbackPage`
   - Nhận query params từ VNPay
   - Hiển thị success/failure message
   - Redirect về dashboard

### **Sprint 2: Usage Dashboard** (2-3 ngày)
4. Tạo `DashboardPage`
   - GET `/api/v1/subscriptions/mine`
   - Hiển thị progress bar: `currentMonthSwapCount/maxSwapsPerMonth`
   - Hiển thị `daysRemaining`
   - Button "Đặt lịch" (nếu còn lượt)

5. Tạo `UsageHistoryPage`
   - GET `/api/v1/subscriptions/mine/usage`
   - Table/List 6 tháng gần nhất
   - Chart thể hiện usage trend

### **Sprint 3: Warnings & Error Handling** (1-2 ngày)
6. Implement Warning Logic
   - Nếu `currentMonthSwapCount >= 8` → Show warning banner
   - "Chỉ còn 2 lần, cân nhắc nâng cấp"

7. Implement Error Modal
   - Catch 400 error từ `/complete-swap`
   - Parse error message
   - Show modal với 2 options: Đợi hoặc Nâng cấp

8. Implement Expired Notification
   - Check `isExpired` trong dashboard
   - Show banner "Hết hạn, vui lòng gia hạn"
   - Disable button "Đặt lịch"

---

## 📞 CONTACT & SUPPORT

**Backend Team:**
- API Base URL: `https://your-api.com/api/v1`
- Swagger Doc: `https://your-api.com/swagger`

**Questions?**
- Slack: #backend-support
- Email: backend-team@company.com

---

**Tài liệu này được cập nhật:** 22/10/2025  
**Version:** 2.0 (Validated with actual backend code)
