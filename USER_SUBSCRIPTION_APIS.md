# User Subscription Management APIs

## Tổng quan
Hệ thống User Subscription Management cho phép người dùng đăng ký gói thuê pin hàng tháng theo mô hình VinFast, với tính năng theo dõi sử dụng và thanh toán.

## Endpoint APIs

### 1. POST /api/v1/subscriptions
**Đăng ký gói subscription cho user**

**Headers:**
```
Authorization: Bearer {JWT_TOKEN}
Content-Type: application/json
```

**Request Body:**
```json
{
  "subscriptionPlanId": "guid",
  "vehicleId": "guid", 
  "startDate": "2025-09-26T10:00:00Z", // Optional, null = bắt đầu ngay
  "notes": "Ghi chú" // Optional
}
```

**Response Success (200):**
```json
{
  "subscriptionId": "guid",
  "message": "Đăng ký gói VF3-Basic thành công!",
  "requiresDeposit": true,
  "depositAmount": 7000000,
  "startDate": "2025-09-26T10:00:00Z",
  "billingPeriodStart": "2025-08-26T00:00:00Z",
  "billingPeriodEnd": "2025-09-25T23:59:59Z"
}
```

**Response Errors:**
- 400: Invalid request (xe không tương thích, gói không tồn tại)
- 409: User đã có subscription đang hoạt động
- 401: Unauthorized

---

### 2. GET /api/v1/subscriptions/mine
**Lấy thông tin subscription hiện tại của user**

**Headers:**
```
Authorization: Bearer {JWT_TOKEN}
```

**Response Success (200):**
```json
{
  "id": "guid",
  "userId": "guid",
  "subscriptionPlanId": "guid",
  "vehicleId": "guid",
  "startDate": "2025-09-26T10:00:00Z",
  "endDate": null,
  "isActive": true,
  "currentBillingPeriodStart": "2025-08-26T00:00:00Z",
  "currentBillingPeriodEnd": "2025-09-25T23:59:59Z",
  "currentMonthKmUsed": 1200,
  "depositPaid": 7000000,
  "depositPaidDate": "2025-09-26T10:00:00Z",
  "consecutiveOverdueMonths": 0,
  "isBlocked": false,
  "chargingLimitPercent": 100,
  "lastPaymentDate": "2025-09-01T00:00:00Z",
  "createdAt": "2025-09-26T10:00:00Z",
  "subscriptionPlan": {
    "id": "guid",
    "name": "VF3-Basic",
    "description": "Gói cơ bản dành cho xe nhỏ - tương đương VF3",
    "monthlyFeeUnder1500Km": 1100000,
    "monthlyFee1500To3000Km": 1400000,
    "monthlyFeeOver3000Km": 3000000,
    "depositAmount": 7000000,
    "batteryModelId": "guid",
    "batteryModelName": "BM-48V-30Ah",
    "isActive": true
  },
  "vehicle": {
    "id": "guid",
    "brand": "VinFast",
    "model": "Unknown",
    "vin": "VNFXXXXXXXXXXXXXXX",
    "plate": "30A-12345",
    "color": "Unknown",
    "year": 2025
  }
}
```

**Response Errors:**
- 404: Không có subscription đang hoạt động
- 401: Unauthorized

---

### 3. PUT /api/v1/subscriptions/mine/cancel
**Hủy subscription hiện tại của user**

**Headers:**
```
Authorization: Bearer {JWT_TOKEN}
```

**Response Success (200):**
```json
{
  "success": true,
  "message": "Hủy gói subscription thành công!",
  "endDate": "2025-09-26T10:30:00Z",
  "depositRefund": 7000000
}
```

**Response Errors:**
- 400: Không thể hủy (còn hóa đơn chưa thanh toán)
- 404: Không có subscription đang hoạt động
- 401: Unauthorized

---

### 4. GET /api/v1/subscriptions/mine/usage
**Xem thống kê sử dụng pin của user**

**Headers:**
```
Authorization: Bearer {JWT_TOKEN}
```

**Response Success (200):**
```json
{
  "subscriptionId": "guid",
  "subscriptionPlanName": "VF3-Basic",
  "vehiclePlate": "30A-12345",
  "currentBillingPeriodStart": "2025-08-26T00:00:00Z",
  "currentBillingPeriodEnd": "2025-09-25T23:59:59Z",
  "currentMonthKmUsed": 1200,
  "currentMonthFee": 1100000,
  "usageTier": "Under1500",
  "totalSwapTransactions": 15,
  "totalKmUsed": 1500,
  "totalAmountPaid": 3300000,
  "monthlyUsage": [
    {
      "year": 2025,
      "month": 9,
      "monthName": "Tháng Chín",
      "kmUsed": 1200,
      "swapCount": 12,
      "monthlyFee": 1100000,
      "usageTier": "Under1500",
      "isPaid": false
    },
    {
      "year": 2025,
      "month": 8,
      "monthName": "Tháng Tám",
      "kmUsed": 1400,
      "swapCount": 14,
      "monthlyFee": 1100000,
      "usageTier": "Under1500",
      "isPaid": true
    }
  ]
}
```

**Response Errors:**
- 404: Không có subscription đang hoạt động
- 401: Unauthorized

---

## Business Rules

### VinFast Billing Cycle
- Chu kỳ thanh toán: từ ngày 26 tháng trước đến ngày 25 tháng hiện tại
- Phí tháng được tính theo km sử dụng:
  - < 1500km: Phí thấp nhất
  - 1500-3000km: Phí trung bình  
  - > 3000km: Phí cao nhất

### Deposit System
- Mỗi gói yêu cầu tiền cọc khác nhau
- Tiền cọc được hoàn trả khi hủy subscription (nếu không có nợ)

### Usage Tracking
- Thống kê theo lịch sử 6 tháng gần nhất
- Hiển thị tình trạng thanh toán từng tháng
- Tracking số lần đổi pin và km sử dụng

### Restrictions
- User chỉ có thể có 1 subscription active tại một thời điểm
- Xe phải tương thích với loại pin trong gói
- Không thể hủy subscription khi còn hóa đơn chưa thanh toán

## Testing với Swagger UI

1. **Truy cập:** http://localhost:5194/swagger
2. **Authentication:** Sử dụng endpoint `/api/v1/auth/login` để lấy JWT token
3. **Authorize:** Click nút "Authorize" và nhập `Bearer {token}`
4. **Test APIs:** Thử nghiệm các endpoint subscription theo thứ tự:
   - Đăng nhập → Tạo xe → Tạo subscription → Xem subscription → Xem usage → Hủy subscription

## Sample Data

### Demo Users:
- **admin@evbss.local** / 12345678 (Role: Admin)
- **staff@evbss.local** / 12345678 (Role: Staff)

### Subscription Plans có sẵn:
- **VF3-Basic**: 1.1M/1.4M/3M VND, cọc 7M VND  
- **VF5-Standard**: 1.4M/1.9M/3.2M VND, cọc 15M VND
- **VF7-Premium**: 2M/3.5M/5.8M VND, cọc 41M VND
- **VF9-Luxury**: 3.2M/5.4M/8.3M VND, cọc 60M VND