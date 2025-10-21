# 🔧 API Routes Fixed - Tổng Hợp Lỗi Đã Sửa

## 📋 Vấn Đề Phát Hiện

Hệ thống có **2 loại API routes khác nhau**:

### **Loại 1: Routes với `/api/v1/` prefix** ✅
```
/api/v1/auth/...
/api/v1/stations/...
/api/v1/subscriptions/...
/api/v1/swaps/...
/api/v1/users/...
/api/v1/vehicles/...
/api/v1/vehicle-models/...
/api/v1/subscription-plans/...
/api/v1/payments/...
/api/v1/invoices/...
/api/v1/reservations/...
/api/v1/slot-reservations/...
```

### **Loại 2: Routes KHÔNG CÓ `/v1/`** ⚠️
```
/api/health
/api/inventory/...
/api/batterymodels/...
/api/batteryunits/...
```

---

## 🛠️ Các File Đã Được Fix

### **1. battery-inventory-test.http**

**Lỗi cũ:**
```http
@baseUrl = http://localhost:5000
POST {{baseUrl}}/api/auth/login        # ❌ Sai: /api/api/auth/login
GET {{baseUrl}}/api/inventory/...      # ❌ Sai: /api/api/inventory/...
```

**Đã fix:**
```http
@baseUrl = http://localhost:5000/api
POST {{baseUrl}}/v1/auth/login         # ✅ Đúng: /api/v1/auth/login
GET {{baseUrl}}/inventory/...          # ✅ Đúng: /api/inventory/...
GET {{baseUrl}}/v1/stations            # ✅ Đúng: /api/v1/stations
GET {{baseUrl}}/batterymodels          # ✅ Đúng: /api/batterymodels
```

**Thay đổi:**
- ✅ Đổi `@baseUrl` từ `http://localhost:5000` → `http://localhost:5000/api`
- ✅ Fix 10 endpoints:
  - Login: `/v1/auth/login`
  - Stations: `/v1/stations`
  - Battery Models: `/batterymodels` (không có v1)
  - Inventory: `/inventory/...` (không có v1)
  - Health: `/inventory/health` (không có v1)

---

### **2. COMPLETE_API_TEST.http**

**Lỗi cũ:**
```http
@adminPassword = Admin123!@#           # ❌ Sai: Password không khớp seed
GET {{baseUrl}}/health                 # ❌ Sai: /api/v1/health (không tồn tại)
GET {{baseUrl}}/battery-models         # ❌ Sai: /api/v1/battery-models (không tồn tại)
```

**Đã fix:**
```http
@adminPassword = 12345678Swp@          # ✅ Đúng: Khớp với seed trong Program.cs
GET http://localhost:5000/api/health   # ✅ Đúng: Hardcode URL cho special routes
GET http://localhost:5000/api/batterymodels  # ✅ Đúng: Không có v1 prefix
```

**Thay đổi:**
- ✅ Đổi `@adminPassword` từ `Admin123!@#` → `12345678Swp@`
- ✅ Đổi `@staffPassword` từ `Staff123!@#` → `12345678Swp@`
- ✅ Fix health endpoint: `/api/health` (hardcode vì không có v1)
- ✅ Fix battery-models: `/api/batterymodels` (hardcode vì không có v1)
- ✅ Fix battery-units: `/api/batteryunits` (hardcode vì không có v1)

---

## 📊 Bảng Tổng Hợp Routes

| Endpoint | Route | Có `/v1/`? | Controller |
|----------|-------|------------|------------|
| Health | `/api/health` | ❌ | HealthController |
| Inventory | `/api/inventory/...` | ❌ | InventoryController |
| Battery Models | `/api/batterymodels/...` | ❌ | BatteryModelsController |
| Battery Units | `/api/batteryunits/...` | ❌ | BatteryUnitsController |
| Auth | `/api/v1/auth/...` | ✅ | AuthController |
| Users | `/api/v1/users/...` | ✅ | UsersController |
| Stations | `/api/v1/stations/...` | ✅ | StationsController |
| Vehicles | `/api/v1/vehicles/...` | ✅ | VehiclesController |
| Vehicle Models | `/api/v1/vehicle-models/...` | ✅ | VehicleModelsController |
| Subscriptions | `/api/v1/subscriptions/...` | ✅ | SubscriptionsController |
| Subscription Plans | `/api/v1/subscription-plans/...` | ✅ | SubscriptionPlansController |
| Swaps | `/api/v1/swaps/...` | ✅ | SwapTransactionsController |
| Reservations | `/api/v1/reservations/...` | ✅ | ReservationsController |
| Slot Reservations | `/api/v1/slot-reservations/...` | ✅ | SlotReservationsController |
| Payments | `/api/v1/payments/...` | ✅ | PaymentsController |
| Invoices | `/api/v1/invoices/...` | ✅ | InvoicesController |
| Admin Stations | `/api/v1/admin/stations/...` | ✅ | AdminStationsController |

---

## 🎯 Giải Pháp Đã Áp Dụng

### **Chiến Lược 1: Dùng Variable `{{baseUrl}}`**
Cho các routes **CÓ** `/v1/`:
```http
@baseUrl = http://localhost:5000/api/v1
GET {{baseUrl}}/stations          # → /api/v1/stations ✅
GET {{baseUrl}}/subscriptions     # → /api/v1/subscriptions ✅
```

### **Chiến Lược 2: Hardcode Full URL**
Cho các routes **KHÔNG CÓ** `/v1/`:
```http
GET http://localhost:5000/api/health         # ✅
GET http://localhost:5000/api/batterymodels  # ✅
GET http://localhost:5000/api/inventory/all  # ✅
```

### **Chiến Lược 3: Mixed Approach (battery-inventory-test.http)**
```http
@baseUrl = http://localhost:5000/api
GET {{baseUrl}}/v1/auth/login       # → /api/v1/auth/login ✅
GET {{baseUrl}}/inventory/health    # → /api/inventory/health ✅
GET {{baseUrl}}/batterymodels       # → /api/batterymodels ✅
```

---

## ✅ Kết Quả Sau Khi Fix

### **battery-inventory-test.http**
- ✅ Tất cả 10 endpoints đã đúng route
- ✅ Login endpoint: `/api/v1/auth/login`
- ✅ Inventory endpoints: `/api/inventory/...`
- ✅ Health check: `/api/inventory/health`

### **COMPLETE_API_TEST.http**
- ✅ Admin password đúng: `12345678Swp@`
- ✅ Health endpoint: `/api/health`
- ✅ Battery Models: `/api/batterymodels`
- ✅ Battery Units: `/api/batteryunits`
- ✅ Các endpoints khác giữ nguyên `/api/v1/...`

---

## 🚀 Cách Test Sau Khi Fix

### **1. Chạy App**
```bash
cd src\EVBSS.Api
dotnet run --urls "http://localhost:5000"
```

### **2. Test battery-inventory-test.http**
```
Bước 1: Login → Copy token
Bước 2: Get Stations → Copy stationId
Bước 3: Get Battery Models → Copy batteryModelId
Bước 4-10: Test các inventory endpoints
```

### **3. Test COMPLETE_API_TEST.http**
```
Section 1: Login với password 12345678Swp@ → Copy token
Section 2: Test stations
Section 3: Test battery models (đã fix route)
...
Section 12: Complete workflow
```

---

## 🔍 Debugging Tips

### **Nếu gặp lỗi 404 Not Found:**
1. Check controller route decoration:
   ```csharp
   [Route("api/v1/[controller]")]  // → /api/v1/users
   [Route("api/[controller]")]     // → /api/users (no v1)
   ```

2. Check HTTP file URL:
   ```http
   # Sai
   GET {{baseUrl}}/users  # với @baseUrl = http://localhost:5000/api/v1/users
   
   # Đúng
   GET {{baseUrl}}/users  # với @baseUrl = http://localhost:5000/api/v1
   ```

### **Nếu gặp lỗi 401 Unauthorized:**
- Check token có hết hạn không
- Check password có đúng `12345678Swp@` không
- Re-login để lấy token mới

### **Nếu gặp lỗi 500 Internal Server Error:**
- Check app logs trong terminal
- Check database connection
- Check seed data đã chạy chưa

---

## 📝 Notes Quan Trọng

1. **Không thống nhất route prefix**: Một số controller dùng `api/v1/`, một số dùng `api/`. Đây là thiết kế hiện tại của hệ thống, không phải bug.

2. **Password seed**: `12345678Swp@` cho cả Admin và Staff. Nếu đổi password trong seed, phải cập nhật lại file test.

3. **Battery routes đặc biệt**: `/api/batterymodels` và `/api/batteryunits` không có `/v1/` vì controller decoration là `[Route("api/[controller]")]`.

4. **Inventory routes**: `/api/inventory/...` không có `/v1/` vì đây là tính năng mới, route được thiết kế riêng.

---

## ✅ Checklist Final

- [x] Fix `battery-inventory-test.http` baseUrl
- [x] Fix `battery-inventory-test.http` password
- [x] Fix `battery-inventory-test.http` 10 endpoints
- [x] Fix `COMPLETE_API_TEST.http` admin password
- [x] Fix `COMPLETE_API_TEST.http` staff password
- [x] Fix `COMPLETE_API_TEST.http` health endpoint
- [x] Fix `COMPLETE_API_TEST.http` battery-models endpoints
- [x] Fix `COMPLETE_API_TEST.http` battery-units endpoints
- [x] Test login với password mới
- [x] Tạo file documentation (API_ROUTES_FIXED.md)

**Status: ✅ HOÀN THÀNH - Tất cả routes đã được fix!**
