# 🏦 Hướng dẫn tích hợp VNPay từ A-Z

## 📋 Mục lục
1. [Hiểu về VNPay Payment Flow](#1-hiểu-về-vnpay-payment-flow)
2. [Cài đặt ngrok](#2-cài-đặt-ngrok)
3. [Cấu hình VNPay](#3-cấu-hình-vnpay)
4. [Test thanh toán](#4-test-thanh-toán)
5. [Xử lý lỗi thường gặp](#5-xử-lý-lỗi-thường-gặp)

---

## 1. Hiểu về VNPay Payment Flow

### 🔄 Luồng thanh toán hoàn chỉnh:

```
┌─────────────┐         ┌─────────────┐         ┌─────────────┐
│   Browser   │         │   Backend   │         │    VNPay    │
│   (User)    │         │   (Server)  │         │   Server    │
└──────┬──────┘         └──────┬──────┘         └──────┬──────┘
       │                       │                        │
       │ 1. POST /create-      │                        │
       │    pending            │                        │
       ├──────────────────────>│                        │
       │                       │                        │
       │                       │ 2. Generate VNPay URL  │
       │                       │    với signature       │
       │                       │                        │
       │ 3. Return payment URL │                        │
       │<──────────────────────│                        │
       │                       │                        │
       │ 4. Redirect browser   │                        │
       │    đến VNPay          │                        │
       ├────────────────────────────────────────────────>│
       │                       │                        │
       │ 5. User nhập thông tin thẻ ngân hàng          │
       │    - Số thẻ, tên chủ thẻ, CVV, OTP           │
       │<───────────────────────────────────────────────│
       │                       │                        │
       │                       │ ⭐ 6. IPN CALLBACK     │
       │                       │    (Server→Server)     │
       │                       │<───────────────────────│
       │                       │                        │
       │                       │ 7. Validate signature  │
       │                       │    Update Database     │
       │                       │    - Payment: Completed│
       │                       │    - Subscription:     │
       │                       │      IsActive = true   │
       │                       │                        │
       │                       │ 8. Return RspCode=00   │
       │                       ├───────────────────────>│
       │                       │                        │
       │ 9. RETURN URL         │                        │
       │    (Browser redirect) │                        │
       │<───────────────────────────────────────────────│
       │                       │                        │
       │ 10. Hiển thị kết quả  │                        │
       │     thanh toán        │                        │
```

---

### 📌 **Giải thích 2 URL quan trọng:**

#### **A. ReturnUrl** - User thấy được
```json
"ReturnUrl": "http://localhost:5194/api/v1/payments/vnpay/return"
```

**Khi nào được gọi:**
- Sau khi user thanh toán xong (thành công hoặc thất bại)
- VNPay **redirect browser của user** về URL này

**Request từ VNPay:**
```
GET http://localhost:5194/api/v1/payments/vnpay/return?
    vnp_TxnRef=EVB20251025143000123&
    vnp_Amount=50000000&
    vnp_ResponseCode=00&
    vnp_TransactionNo=14531234&
    vnp_SecureHash=abc123...
```

**Mục đích:**
- Hiển thị UI cho user: "Thanh toán thành công!" hoặc "Thanh toán thất bại!"
- User có thể đóng trang này

**✅ Localhost OK** vì:
- Browser chạy trên máy tính bạn
- Browser có thể access `localhost:5194`

---

#### **B. IpnUrl** - VNPay server gọi trực tiếp
```json
"IpnUrl": "http://localhost:5194/api/v1/payments/vnpay/callback"
```

**Khi nào được gọi:**
- **NGAY SAU** khi user thanh toán thành công
- **TRƯỚC** khi redirect về ReturnUrl
- VNPay server gọi **TRỰC TIẾP** từ data center của họ

**Request từ VNPay:**
```
POST http://localhost:5194/api/v1/payments/vnpay/callback
Content-Type: application/x-www-form-urlencoded

vnp_TxnRef=EVB20251025143000123&
vnp_Amount=50000000&
vnp_ResponseCode=00&
vnp_TransactionNo=14531234&
vnp_SecureHash=abc123...
```

**Mục đích:**
- **CẬP NHẬT DATABASE** (Payment status, Subscription activation)
- **BẮT BUỘC** phải thành công, nếu không user mất tiền mà không nhận được dịch vụ

**❌ Localhost KHÔNG OK** vì:
```
VNPay Server (Singapore/Hà Nội data center)
     │
     │ Cố gắng gọi http://localhost:5194/...
     │
     ▼
❌ Không tìm thấy!
   (localhost của VNPay ≠ localhost của bạn)
```

---

### 🚨 **Vấn đề nếu IPN không hoạt động:**

#### **Kịch bản 1: User thanh toán thành công**
```
1. ✅ User nhập thẻ → VNPay trừ tiền → Tiền đã mất
2. ❌ VNPay gọi IPN (localhost:5194) → Timeout
3. ❌ Database: Payment.Status = "Pending" (không update)
4. ❌ Database: UserSubscription.IsActive = false (không kích hoạt)
5. ✅ Browser redirect về ReturnUrl → User thấy "Đang xử lý..."
6. ❌ User vào app → Không có gói subscription!
```

**Kết quả:** User mất 500,000 VNĐ nhưng không nhận được gói Basic! 😱

#### **Kịch bản 2: Network gián đoạn**
```
1. ✅ User thanh toán thành công
2. 🔥 User tắt browser/mất mạng ngay (không đợi ReturnUrl)
3. ❌ IPN không hoạt động → Database không update
4. ❌ Giao dịch bị "mất tích"
```

**Kết quả:** Phải reconciliation thủ công, check VNPay Merchant Portal!

---

## 2. Cài đặt ngrok

### **Ngrok là gì?**
Ngrok tạo một **tunnel từ Internet → localhost** của bạn:

```
Internet                     Ngrok Cloud              Your Computer
──────────────────────────────────────────────────────────────────────
https://abc-123.ngrok.io ─────────────────────> localhost:5194
        ↑                                              ↑
  VNPay Server                                  Backend của bạn
  có thể truy cập                               
```

---

### **Bước 1: Tải và cài đặt ngrok**

#### **Windows:**
1. Vào https://ngrok.com/download
2. Chọn **Windows (64-bit)** → Download `ngrok.exe`
3. Giải nén `ngrok.exe` vào folder:
   ```
   C:\Tools\ngrok.exe
   ```

#### **macOS (Homebrew):**
```bash
brew install ngrok/ngrok/ngrok
```

#### **Linux:**
```bash
curl -sSL https://ngrok-agent.s3.amazonaws.com/ngrok.asc \
  | sudo tee /etc/apt/trusted.gpg.d/ngrok.asc >/dev/null \
  && echo "deb https://ngrok-agent.s3.amazonaws.com buster main" \
  | sudo tee /etc/apt/sources.list.d/ngrok.list \
  && sudo apt update && sudo apt install ngrok
```

---

### **Bước 2: Đăng ký tài khoản ngrok (FREE)**

1. Vào https://dashboard.ngrok.com/signup
2. Đăng ký bằng:
   - Email/Password
   - Google account
   - GitHub account

3. Sau khi đăng ký, vào **Dashboard** → **Your Authtoken**
4. Copy authtoken (dạng: `2k3j4h5kjh345kjh3k4j5h3k4j5h`)

---

### **Bước 3: Authenticate ngrok**

```powershell
# Mở PowerShell (Windows) hoặc Terminal (Mac/Linux)
cd C:\Tools

# Authenticate (chỉ cần làm 1 lần duy nhất)
.\ngrok.exe config add-authtoken YOUR_AUTHTOKEN_HERE
```

**Output:**
```
Authtoken saved to configuration file: C:\Users\YourName\.ngrok2\ngrok.yml
```

---

### **Bước 4: Chạy ngrok**

#### **Terminal 1: Chạy backend**
```powershell
cd C:\Users\phamv\Desktop\SWP_FA25\BE\EV-Battery-Swap-Station-Management-System-Backend\src\EVBSS.Api
dotnet run
```

Đợi thấy:
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5194
```

#### **Terminal 2: Chạy ngrok**
```powershell
cd C:\Tools
.\ngrok.exe http 5194
```

**Output:**
```
ngrok                                                                   

Session Status                online
Account                       Your Name (Plan: Free)
Version                       3.5.0
Region                        Asia Pacific (ap)
Latency                       -
Web Interface                 http://127.0.0.1:4040
Forwarding                    https://7a8b-1-2-3-4.ngrok-free.app -> http://localhost:5194

Connections                   ttl     opn     rt1     rt5     p50     p90
                              0       0       0.00    0.00    0.00    0.00
```

**⭐ Copy URL này:** `https://7a8b-1-2-3-4.ngrok-free.app`

---

### **Bước 5: Kiểm tra ngrok hoạt động**

#### **Test 1: Health check**
Mở browser, truy cập:
```
https://7a8b-1-2-3-4.ngrok-free.app/api/health
```

**Kết quả mong đợi:**
```json
{
  "status": "Healthy",
  "message": "API is running"
}
```

#### **Test 2: Xem traffic trên Web Interface**
Mở browser:
```
http://127.0.0.1:4040
```

Trang này hiển thị:
- Tất cả HTTP requests đi qua ngrok
- Request headers, body
- Response status, body
- **RẤT HỮU ÍCH** để debug VNPay callback!

---

## 3. Cấu hình VNPay

### **Bước 1: Cập nhật appsettings.json**

```powershell
# Mở file appsettings.json, tìm section VnPay
# Thay đổi IpnUrl từ localhost thành ngrok URL
```

**TRƯỚC:**
```json
"VnPay": {
  "TmnCode": "OE2KYEVL",
  "HashSecret": "WBXOSWTZIWY391QZZSJUGA2AF0D9QS5S",
  "BaseUrl": "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html",
  "ReturnUrl": "http://localhost:5194/api/v1/payments/vnpay/return",
  "IpnUrl": "http://localhost:5194/api/v1/payments/vnpay/callback"
}
```

**SAU:** (Thay `YOUR_NGROK_URL` bằng URL thật từ ngrok)
```json
"VnPay": {
  "TmnCode": "OE2KYEVL",
  "HashSecret": "WBXOSWTZIWY391QZZSJUGA2AF0D9QS5S",
  "BaseUrl": "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html",
  "ReturnUrl": "http://localhost:5194/api/v1/payments/vnpay/return",
  "IpnUrl": "https://7a8b-1-2-3-4.ngrok-free.app/api/v1/payments/vnpay/callback"
}
```

**⚠️ LƯU Ý:**
- `ReturnUrl` giữ nguyên `localhost` (vì user's browser access được)
- `IpnUrl` phải dùng ngrok URL (vì VNPay server cần access được)

---

### **Bước 2: Restart backend**

```powershell
# Ctrl+C để stop dotnet run
# Chạy lại:
dotnet run
```

---

### **Bước 3: Cấu hình IPN URL trên VNPay Merchant Portal**

1. Login vào VNPay Merchant Admin:
   ```
   URL: https://sandbox.vnpayment.vn/merchantv2/
   Email: phamvanminh150204@gmail.com
   Password: <mật khẩu bạn đã đăng ký>
   ```

2. Vào menu **Cấu hình** → **Cấu hình IPN**

3. Nhập IPN URL:
   ```
   https://7a8b-1-2-3-4.ngrok-free.app/api/v1/payments/vnpay/callback
   ```

4. Click **Lưu**

**⚠️ QUAN TRỌNG:**
- Mỗi khi restart ngrok, URL sẽ thay đổi (Free plan)
- Phải update lại IPN URL trên VNPay portal mỗi lần thay đổi
- **Hoặc** upgrade ngrok Pro để có fixed domain

---

## 4. Test thanh toán

### **Test Case 1: Thanh toán Subscription**

#### **Step 1: Tạo pending subscription**
```http
POST http://localhost:5194/api/v1/subscriptions/create-pending
Authorization: Bearer YOUR_JWT_TOKEN
Content-Type: application/json

{
  "subscriptionPlanId": "GUID_OF_SUBSCRIPTION_PLAN",
  "vehicleId": "GUID_OF_VEHICLE"
}
```

**Response:**
```json
{
  "paymentId": "123e4567-e89b-12d3-a456-426614174000",
  "userSubscriptionId": "223e4567-e89b-12d3-a456-426614174000",
  "paymentUrl": "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html?vnp_Version=2.1.0&...",
  "amount": 500000,
  "planName": "Gói Basic - 10 lần/tháng",
  "message": "Gói subscription đã được tạo. Vui lòng chọn phương thức thanh toán."
}
```

#### **Step 2: Copy `paymentUrl` và mở trong browser**

#### **Step 3: Chọn ngân hàng và nhập thông tin**

**Ngân hàng:** NCB (TMCP Quốc Dân)

**Thông tin thẻ test (từ VNPay docs):**
```
Số thẻ:         9704198526191432198
Tên chủ thẻ:    NGUYEN VAN A
Ngày phát hành: 07/15
Mật khẩu OTP:   123456
```

#### **Step 4: Xác nhận thanh toán**

#### **Step 5: Theo dõi IPN callback trên ngrok Web Interface**

Mở `http://127.0.0.1:4040` trong browser, bạn sẽ thấy:

```
POST /api/v1/payments/vnpay/callback
Status: 200 OK
Request Body:
  vnp_TxnRef=EVB20251025143000123
  vnp_Amount=50000000
  vnp_ResponseCode=00
  vnp_TransactionStatus=00
  vnp_SecureHash=...

Response Body:
  { "RspCode": "00", "Message": "Confirm Success" }
```

**✅ Nếu thấy Status 200 + RspCode "00" → Thành công!**

#### **Step 6: Kiểm tra database**

```sql
-- Check Payment
SELECT Id, Status, VnpTransactionNo, VnpResponseCode, CompletedAt
FROM Payments
WHERE VnpTxnRef = 'EVB20251025143000123';

-- Kết quả mong đợi:
-- Status = Completed (2)
-- CompletedAt = <current datetime>

-- Check UserSubscription
SELECT Id, IsActive, StartDate, CurrentBillingPeriodEnd
FROM UserSubscriptions
WHERE UserId = '<user-id>';

-- Kết quả mong đợi:
-- IsActive = true (1)
-- StartDate = <current datetime>
-- CurrentBillingPeriodEnd = <30 days from now>
```

---

### **Test Case 2: Thanh toán Pay-per-Swap**

```http
POST http://localhost:5194/api/v1/reservations/pay-per-swap
Authorization: Bearer YOUR_JWT_TOKEN
Content-Type: application/json

{
  "stationId": "GUID_OF_STATION",
  "batteryModelId": "GUID_OF_BATTERY_MODEL",
  "slotDate": "2025-10-26",
  "slotStartTime": "10:00:00",
  "slotEndTime": "10:30:00",
  "paymentMethod": "VNPay",
  "amount": 50000
}
```

Sau đó làm tương tự Test Case 1.

---

## 5. Xử lý lỗi thường gặp

### **Lỗi 1: VNPay không gọi IPN callback**

**Triệu chứng:**
- Thanh toán thành công
- User redirect về ReturnUrl OK
- Database không update (Payment vẫn Pending)

**Nguyên nhân:**
- IpnUrl vẫn là `localhost`
- Hoặc ngrok URL đã thay đổi (restart ngrok)
- Hoặc chưa update IPN URL trên VNPay portal

**Giải pháp:**
1. Check ngrok terminal, copy URL mới
2. Update `appsettings.json` → IpnUrl
3. Restart backend (`dotnet run`)
4. Update IPN URL trên VNPay portal

---

### **Lỗi 2: Invalid signature**

**Triệu chứng:**
- VNPay gọi IPN callback
- Backend response `RspCode = "97"` (Invalid signature)
- Database không update

**Nguyên nhân:**
- `HashSecret` sai
- Hoặc code tính signature sai logic

**Giải pháp:**
1. Check `appsettings.json` → `VnPay.HashSecret`
2. Đảm bảo match với email VNPay gửi: `WBXOSWTZIWY391QZZSJUGA2AF0D9QS5S`
3. Check code `VnPayService.ValidateCallback()`:
   ```csharp
   var inputHash = callback.vnp_SecureHash;
   var sortedParams = queryParams
       .Where(p => p.Key != "vnp_SecureHash")
       .OrderBy(p => p.Key)
       .ToList();
   var hashData = string.Join("&", sortedParams.Select(p => $"{p.Key}={p.Value}"));
   var myChecksum = ComputeHmacSha512(_config.HashSecret, hashData);
   return myChecksum.Equals(inputHash, StringComparison.OrdinalIgnoreCase);
   ```

---

### **Lỗi 3: Ngrok free plan - URL thay đổi liên tục**

**Triệu chứng:**
- Mỗi lần restart ngrok → URL mới
- Phải update IpnUrl 2 nơi (appsettings + VNPay portal)

**Giải pháp:**
- **Option A (Khuyến nghị cho dev):** Chấp nhận, setup nhanh thôi
- **Option B:** Upgrade ngrok Pro ($8/month) → fixed domain
- **Option C:** Deploy backend lên cloud (Azure/AWS/Heroku) → có public URL thật

---

### **Lỗi 4: CORS khi test từ frontend**

**Triệu chứng:**
- Frontend gọi API → CORS error
- `Access-Control-Allow-Origin` missing

**Giải pháp:**
Check `Program.cs` có enable CORS:
```csharp
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// ...

app.UseCors();
```

---

## 📝 Checklist trước khi test

### **Backend:**
- ✅ `appsettings.json` có VNPay config đúng (TmnCode, HashSecret)
- ✅ IpnUrl sử dụng ngrok URL (không phải localhost)
- ✅ `dotnet run` thành công, app đang chạy
- ✅ `http://localhost:5194/api/health` return status OK

### **Ngrok:**
- ✅ Ngrok đã authenticate (có authtoken)
- ✅ `ngrok http 5194` đang chạy
- ✅ Forwarding URL có dạng `https://xxx.ngrok-free.app`
- ✅ Web Interface `http://127.0.0.1:4040` mở được

### **VNPay:**
- ✅ Đã login vào Merchant Portal
- ✅ Đã cập nhật IPN URL = ngrok URL + `/api/v1/payments/vnpay/callback`
- ✅ IPN URL không có typo, format đúng

### **Database:**
- ✅ Migrations đã apply
- ✅ Có ít nhất 1 User, 1 Station, 1 BatteryModel, 1 SubscriptionPlan

---

## 🎯 Kết luận

**VNPay Payment Flow = 2 callbacks:**
1. **ReturnUrl** (Browser → Backend): User thấy kết quả
2. **IpnUrl** (VNPay → Backend): Update database ⭐ **QUAN TRỌNG NHẤT**

**IpnUrl phải public accessible:**
- ❌ `localhost` không hoạt động
- ✅ Ngrok tunnel hoạt động (dev/test)
- ✅ Cloud deployment hoạt động (production)

**Ngrok = Cầu nối Internet ↔ Localhost:**
```
VNPay (Internet) → Ngrok Cloud → Ngrok Agent → localhost:5194
```

**Chi phí:**
- Ngrok Free: $0 (URL thay đổi mỗi lần restart)
- Ngrok Pro: $8/month (fixed domain)
- Cloud deployment: Tùy platform (Azure/AWS/Heroku)

---

## 📚 Tài liệu tham khảo

- **VNPay API Docs**: https://sandbox.vnpayment.vn/apis/docs/thanh-toan-pay/pay.html
- **Ngrok Docs**: https://ngrok.com/docs
- **VNPay Code Demo**: https://sandbox.vnpayment.vn/apis/vnpay-demo/

---

**Chúc bạn tích hợp thành công! 🎉**
