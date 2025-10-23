# 📊 PHÂN TÍCH CHI TIẾT LUỒNG 2: ĐẶT LỊCH LẺ (PAY-PER-SWAP)

## 🎯 MỤC TIÊU
- **Frontend:** Người dùng KHÔNG CÓ GÓI, muốn đặt 1 lịch hẹn và trả tiền cho riêng lần đó
- **Backend:** Tạo Reservation + Payment cùng lúc, hỗ trợ 2 phương thức: VNPay và Cash
- **Giá:** 25.000 VND/lần đổi

---

## 🔍 PHÂN TÍCH TỪNG BƯỚC CHI TIẾT

### **BƯỚC CHUẨN BỊ: Frontend kiểm tra gói subscription**

#### Frontend Flow:
```javascript
// User mở BookingWizard
const response = await api.get('/api/v1/subscriptions/mine')
if (!response.data || !response.data.isActive) {
  // → User KHÔNG CÓ GÓI → Hiển thị giá 25.000 VND/lần
}
```

#### Backend Status:
| Thành phần | Có sẵn? | Ghi chú |
|------------|---------|---------|
| Endpoint `GET /api/v1/subscriptions/mine` | ✅ ĐÃ CÓ | Endpoint hiện tại đã hoạt động tốt |
| Logic kiểm tra `isActive` | ✅ ĐÃ CÓ | Frontend tự xử lý |

**Kết luận:** ✅ **Không cần thay đổi gì**

---

### **BƯỚC 1-3: Frontend thu thập thông tin (Chọn Trạm, Xe, Slot)**

#### Frontend Flow:
```javascript
// Bước 1: Chọn trạm
selectedStation = { id: "guid", name: "Trạm Q1" }

// Bước 2: Chọn xe
selectedVehicle = { id: "guid", plate: "51F-12345" }

// Bước 3: Chọn ngày giờ
selectedSlot = {
  date: "2025-10-25",
  startTime: "09:00:00",
  endTime: "09:30:00"
}
```

#### Backend Status:
| Thành phần | Có sẵn? | Ghi chú |
|------------|---------|---------|
| API lấy danh sách trạm | ✅ ĐÃ CÓ | `GET /api/v1/stations` |
| API lấy slots available | ✅ ĐÃ CÓ | `GET /api/v1/slot-reservations/available` |
| API lấy vehicles của user | ✅ ĐÃ CÓ | `GET /api/v1/vehicles/mine` |

**Kết luận:** ✅ **Không cần thay đổi gì**

---

### **BƯỚC 4: Frontend hiển thị xác nhận và 2 nút thanh toán**

#### Frontend Flow:
```javascript
// Hiển thị tóm tắt
<Summary>
  Trạm: Trạm Q1
  Xe: 51F-12345
  Ngày giờ: 25/10/2025 09:00-09:30
  Tổng cộng: 25.000 VND
</Summary>

// Hiển thị 2 nút
<Button onClick={() => handleConfirmAndPay('vnpay')}>Thanh toán VNPay</Button>
<Button onClick={() => handleConfirmAndPay('cash')}>Thanh toán Tiền mặt</Button>
```

#### Backend Status:
**Kết luận:** ✅ **Frontend tự xử lý UI, không cần BE**

---

## 🔥 PHẦN QUAN TRỌNG: LUỒNG THANH TOÁN

---

### **LUỒNG 2A: THANH TOÁN VNPAY**

#### 📍 **BƯỚC 2A.1: Frontend gọi API tạo reservation + payment**

##### Frontend Request:
```javascript
const handleConfirmAndPay = async (method) => {
  const response = await api.post('/api/v1/payments/create-pay-per-swap-reservation', {
    stationId: "guid-station",
    batteryModelId: "guid-battery-model", // Lấy từ vehicle
    slotDate: "2025-10-25",
    slotStartTime: "09:00:00",
    slotEndTime: "09:30:00",
    amount: 25000,
    paymentMethod: 0  // 0 = VNPay
  })
}
```

##### Backend Current Status:
| Thành phần | Hiện tại | Cần có | Gap |
|------------|----------|--------|-----|
| **Endpoint** | ❌ KHÔNG CÓ `POST /api/v1/payments/create-pay-per-swap-reservation` | ✅ Cần tạo mới | ⚠️ **THIẾU HOÀN TOÀN** |
| **DTO Request** | ❌ Không có | ✅ `CreatePayPerSwapReservationRequest` | ⚠️ **CẦN TẠO** |
| **DTO Response** | ❌ Không có | ✅ `CreatePayPerSwapReservationResponse` | ⚠️ **CẦN TẠO** |

##### Chi tiết DTO cần tạo:

**1. CreatePayPerSwapReservationRequest.cs**
```csharp
namespace EVBSS.Api.Dtos.Payments;

public class CreatePayPerSwapReservationRequest
{
    [Required]
    public Guid StationId { get; set; }
    
    [Required]
    public Guid BatteryModelId { get; set; }
    
    [Required]
    public DateOnly SlotDate { get; set; }
    
    [Required]
    public TimeSpan SlotStartTime { get; set; }
    
    [Required]
    public TimeSpan SlotEndTime { get; set; }
    
    [Required]
    [Range(1, double.MaxValue)]
    public decimal Amount { get; set; }  // 25000
    
    [Required]
    public PaymentMethod PaymentMethod { get; set; }  // 0=VNPay, 1=Cash
}
```

**2. CreatePayPerSwapReservationResponse.cs (LUỒNG 2A - VNPay)**
```csharp
namespace EVBSS.Api.Dtos.Payments;

public class CreatePayPerSwapReservationResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    
    // LUỒNG 2A: VNPay → Trả về paymentUrl
    public string? PaymentUrl { get; set; }
    
    // LUỒNG 2B: Cash → Trả về reservation details
    public Guid? ReservationId { get; set; }
    public Guid? PaymentId { get; set; }
    public string? QRCode { get; set; }
    public string? Status { get; set; }
    
    // Common fields
    public decimal Amount { get; set; }
    public string? Instructions { get; set; }
}
```

##### Backend Logic cần implement:

**PaymentsController.cs** (Thêm method mới)
```csharp
[HttpPost("create-pay-per-swap-reservation")]
public async Task<ActionResult<CreatePayPerSwapReservationResponse>> CreatePayPerSwapReservation(
    [FromBody] CreatePayPerSwapReservationRequest request)
{
    try
    {
        var userId = GetCurrentUserId();
        var ipAddress = GetClientIpAddress();
        
        // Gọi service xử lý logic
        var result = await _paymentService.CreatePayPerSwapReservationAsync(userId, request, ipAddress);
        
        if (result.Success)
        {
            return Ok(result);
        }
        else
        {
            return BadRequest(result);
        }
    }
    catch (ActiveReservationExistsException ex)
    {
        return BadRequest(new CreatePayPerSwapReservationResponse
        {
            Success = false,
            Message = ex.Message
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error creating pay-per-swap reservation");
        return StatusCode(500, new CreatePayPerSwapReservationResponse
        {
            Success = false,
            Message = "Có lỗi xảy ra khi tạo lịch hẹn."
        });
    }
}
```

**PaymentService.cs** (Thêm method mới)
```csharp
public async Task<CreatePayPerSwapReservationResponse> CreatePayPerSwapReservationAsync(
    Guid userId, 
    CreatePayPerSwapReservationRequest request, 
    string ipAddress)
{
    using var transaction = await _context.Database.BeginTransactionAsync();
    
    try
    {
        // BƯỚC 1: Tạo Reservation (dùng lại SlotReservationService)
        var reservation = await _slotReservationService.CreateReservationAsync(
            userId,
            request.StationId,
            request.BatteryModelId,
            request.SlotDate,
            request.SlotStartTime,
            request.SlotEndTime
        );
        
        // BƯỚC 2: Tạo Payment (Status: Pending, Type: PayPerSwap)
        var payment = new Payment
        {
            UserId = userId,
            ReservationId = reservation.Id,  // ⚠️ QUAN TRỌNG: Link Payment với Reservation
            Method = request.PaymentMethod,
            Type = PaymentType.PayPerSwap,  // ⚠️ QUAN TRỌNG
            Amount = request.Amount,
            Status = PaymentStatus.Pending,
            Description = $"Thanh toán đặt lịch đổi pin - {request.SlotDate:dd/MM/yyyy} {request.SlotStartTime}",
            VnpTxnRef = GenerateTransactionReference(),
            PaymentReference = GenerateTransactionReference(),
            CreatedAt = DateTime.UtcNow
        };
        
        _context.Payments.Add(payment);
        await _context.SaveChangesAsync();
        
        // BƯỚC 3: Phân nhánh theo PaymentMethod
        if (request.PaymentMethod == PaymentMethod.VNPay)
        {
            // LUỒNG 2A: VNPay
            var paymentUrl = await GenerateVnPayUrlForReservation(payment, reservation, ipAddress);
            
            await transaction.CommitAsync();
            
            _logger.LogInformation(
                "Created pay-per-swap reservation {ReservationId} with VNPay payment {PaymentId} for user {UserId}",
                reservation.Id, payment.Id, userId
            );
            
            return new CreatePayPerSwapReservationResponse
            {
                Success = true,
                Message = "Đã tạo lịch hẹn. Vui lòng thanh toán qua VNPay.",
                PaymentUrl = paymentUrl,
                ReservationId = reservation.Id,
                PaymentId = payment.Id,
                Amount = request.Amount
            };
        }
        else // PaymentMethod.Cash
        {
            // LUỒNG 2B: Cash (sẽ phân tích ở phần sau)
            await transaction.CommitAsync();
            
            return new CreatePayPerSwapReservationResponse
            {
                Success = true,
                Message = "Đã tạo lịch hẹn. Vui lòng thanh toán tiền mặt tại trạm.",
                ReservationId = reservation.Id,
                PaymentId = payment.Id,
                QRCode = reservation.QRCode,
                Status = reservation.Status.ToString(),
                Amount = request.Amount,
                Instructions = "Vui lòng đến trạm đúng giờ và xuất trình mã QR để thanh toán tiền mặt."
            };
        }
    }
    catch (Exception ex)
    {
        await transaction.RollbackAsync();
        _logger.LogError(ex, "Error creating pay-per-swap reservation for user {UserId}", userId);
        throw;
    }
}
```

---

#### 📍 **VẤN ĐỀ QUAN TRỌNG: Payment.ReservationId**

##### Hiện trạng Payment Model:
```csharp
public class Payment
{
    // ✅ Có field này cho Subscription
    public Guid? UserSubscriptionId { get; set; }
    
    // ❌ THIẾU field này cho Reservation (Pay-per-swap)
    // public Guid? ReservationId { get; set; }  // ← CẦN THÊM
}
```

##### Tại sao cần ReservationId?
1. ✅ **Link Payment với Reservation**: Biết payment này thuộc reservation nào
2. ✅ **Validation**: Tránh trùng payment cho cùng 1 reservation
3. ✅ **Tracking**: Dễ truy vết lịch sử thanh toán
4. ✅ **Business Logic**: Khi payment complete → có thể update reservation status

##### Cách sửa:

**1. Update Payment.cs**
```csharp
public class Payment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string PaymentReference { get; set; } = null!;
    
    // Link to either Subscription OR Reservation (mutual exclusive)
    public Guid? UserSubscriptionId { get; set; }  // Cho LUỒNG 1
    public Guid? ReservationId { get; set; }       // Cho LUỒNG 2 ⬅️ THÊM MỚI
    public Guid UserId { get; set; }

    // ... rest of properties
    
    // Navigation properties
    public UserSubscription? UserSubscription { get; set; }
    public Reservation? Reservation { get; set; }  // ⬅️ THÊM MỚI
    public User User { get; set; } = null!;
}
```

**2. Update AppDbContext.cs**
```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // ... existing code ...
    
    // Payment → Reservation relationship (NEW)
    modelBuilder.Entity<Payment>()
        .HasOne(p => p.Reservation)
        .WithMany()  // Reservation không có collection Payments (nếu không cần)
        .HasForeignKey(p => p.ReservationId)
        .OnDelete(DeleteBehavior.Restrict);
}
```

**3. Tạo Migration**
```bash
dotnet ef migrations add AddReservationIdToPayment --project src/EVBSS.Api
dotnet ef database update --project src/EVBSS.Api
```

---

#### 📍 **BƯỚC 2A.2: Backend trả về paymentUrl**

##### Frontend nhận response:
```javascript
const response = await api.post('/api/v1/payments/create-pay-per-swap-reservation', {...})

// Response:
// {
//   "success": true,
//   "message": "Đã tạo lịch hẹn. Vui lòng thanh toán qua VNPay.",
//   "paymentUrl": "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html?vnp_...",
//   "reservationId": "guid-reservation",
//   "paymentId": "guid-payment",
//   "amount": 25000
// }
```

##### Backend Status:
| Thành phần | Có sẵn? | Cần điều chỉnh? |
|------------|---------|-----------------|
| VnPayService.GenerateVnPayUrl() | ✅ ĐÃ CÓ | ⚠️ **CẦN ADAPT** cho Reservation (hiện tại chỉ xử lý Subscription) |

##### Logic cần thêm vào PaymentService:

**GenerateVnPayUrlForReservation()** (helper method)
```csharp
private async Task<string> GenerateVnPayUrlForReservation(
    Payment payment, 
    Reservation reservation, 
    string ipAddress)
{
    // Tương tự VnPayService nhưng cho Reservation
    var vnpParams = new SortedDictionary<string, string>
    {
        {"vnp_Version", "2.1.0"},
        {"vnp_Command", "pay"},
        {"vnp_TmnCode", _vnPayConfig.TmnCode},
        {"vnp_Amount", ((long)(payment.Amount * 100)).ToString()},
        {"vnp_CreateDate", DateTime.UtcNow.ToString("yyyyMMddHHmmss")},
        {"vnp_CurrCode", "VND"},
        {"vnp_IpAddr", ipAddress},
        {"vnp_Locale", "vn"},
        {"vnp_OrderInfo", $"Thanh toan dat lich doi pin {reservation.SlotDate:yyyyMMdd}"},
        {"vnp_OrderType", "other"},
        {"vnp_ReturnUrl", _vnPayConfig.ReturnUrl},
        {"vnp_TxnRef", payment.VnpTxnRef}
    };
    
    // Tạo query string + secure hash (tương tự VnPayService)
    var queryString = BuildQueryString(vnpParams);
    var secureHash = ComputeHmacSha512(_vnPayConfig.HashSecret, queryString);
    
    return $"{_vnPayConfig.Url}?{queryString}&vnp_SecureHash={secureHash}";
}
```

---

#### 📍 **BƯỚC 2A.3: Frontend redirect user đến VNPay**

##### Frontend Flow:
```javascript
if (response.data.success && response.data.paymentUrl) {
  // Redirect user đến VNPay
  window.location.href = response.data.paymentUrl
}
```

##### Backend Status:
**Kết luận:** ✅ **Frontend tự xử lý, không cần BE**

---

#### 📍 **BƯỚC 2A.4: User thanh toán trên VNPay**

**Kết luận:** ✅ **VNPay xử lý, không cần BE**

---

#### 📍 **BƯỚC 2A.5: VNPay gọi IPN callback**

##### VNPay Request:
```
GET /api/v1/payments/vnpay/callback?
  vnp_TmnCode=VNPAY_TMN_CODE&
  vnp_Amount=2500000&  (25.000 VND * 100)
  vnp_TxnRef=EVB20251025090000123&
  vnp_ResponseCode=00&
  vnp_TransactionStatus=00&
  vnp_SecureHash=...
```

##### Backend Current Status:
| Thành phần | Có sẵn? | Vấn đề |
|------------|---------|--------|
| Endpoint callback | ✅ ĐÃ CÓ `GET /api/v1/payments/vnpay/callback` | ✅ OK |
| `VnPayService.ProcessCallbackAsync()` | ✅ ĐÃ CÓ | ⚠️ **CHỈ XỬ LÝ SUBSCRIPTION** |

##### Vấn đề với logic hiện tại:

**VnPayService.ProcessCallbackAsync() (HIỆN TẠI)**
```csharp
public async Task<VnPayCallbackResponse> ProcessCallbackAsync(VnPayCallbackRequest callback)
{
    // 1. Validate signature ✅ OK
    // 2. Find payment ✅ OK
    var payment = await _context.Payments
        .Include(p => p.UserSubscription)  // ⬅️ CHỈ LOAD SUBSCRIPTION
        .FirstOrDefaultAsync(p => p.VnpTxnRef == callback.vnp_TxnRef);
    
    // 3. Update payment status ✅ OK
    if (isSuccess) {
        payment.Status = PaymentStatus.Completed;
        
        // 4. KÍCH HOẠT SUBSCRIPTION ⬅️ LOGIC CHỈ CHO SUBSCRIPTION
        if (payment.UserSubscription != null && !payment.UserSubscription.IsActive) {
            // Activate subscription...
        }
    }
    
    await _context.SaveChangesAsync();
}
```

##### Logic cần sửa:

**VnPayService.ProcessCallbackAsync() (SAU KHI SỬA)**
```csharp
public async Task<VnPayCallbackResponse> ProcessCallbackAsync(VnPayCallbackRequest callback)
{
    // 1. Validate signature ✅ GIỮ NGUYÊN
    // 2. Find payment - INCLUDE cả Reservation
    var payment = await _context.Payments
        .Include(p => p.UserSubscription)
        .Include(p => p.Reservation)  // ⬅️ THÊM MỚI
        .FirstOrDefaultAsync(p => p.VnpTxnRef == callback.vnp_TxnRef);
    
    // 3. Update payment status ✅ GIỮ NGUYÊN
    if (isSuccess && amount == payment.Amount)
    {
        payment.Status = PaymentStatus.Completed;
        payment.CompletedAt = DateTime.UtcNow;

        // 4. PHÂN NHÁNH THEO LOẠI PAYMENT
        if (payment.Type == PaymentType.Subscription && payment.UserSubscription != null)
        {
            // ⬅️ LUỒNG 1: KÍCH HOẠT SUBSCRIPTION (LOGIC CŨ - GIỮ NGUYÊN)
            if (!payment.UserSubscription.IsActive)
            {
                var now = DateTime.UtcNow;
                payment.UserSubscription.IsActive = true;
                payment.UserSubscription.StartDate = now;
                payment.UserSubscription.EndDate = now.AddDays(30);
                // ... rest of activation logic
                
                _logger.LogInformation(
                    "Subscription {SubscriptionId} ACTIVATED after VNPay payment {PaymentId}",
                    payment.UserSubscription.Id, payment.Id
                );
            }
        }
        else if (payment.Type == PaymentType.PayPerSwap && payment.Reservation != null)
        {
            // ⬅️ LUỒNG 2: THANH TOÁN CHO RESERVATION (LOGIC MỚI)
            // Reservation.Status GIỮ NGUYÊN = Pending (chờ check-in)
            // Payment đã Completed → User có thể check-in bằng QR
            
            _logger.LogInformation(
                "Pay-per-swap payment {PaymentId} COMPLETED for reservation {ReservationId}. User can now check-in.",
                payment.Id, payment.Reservation.Id
            );
            
            // ⚠️ LƯU Ý: Không update Reservation.Status ở đây
            // Reservation.Status sẽ chuyển từ Pending → CheckedIn khi Staff scan QR
        }

        _logger.LogInformation("Payment {PaymentId} completed successfully", payment.Id);
    }
    else
    {
        // Payment failed
        payment.Status = PaymentStatus.Failed;
        payment.CompletedAt = DateTime.UtcNow;
        
        _logger.LogWarning("Payment {PaymentId} failed with response code {Code}", 
            payment.Id, callback.vnp_ResponseCode);
    }

    await _context.SaveChangesAsync();
    return new VnPayCallbackResponse();
}
```

##### ⚠️ **LƯU Ý QUAN TRỌNG:**
- **Subscription Payment**: Completed → Kích hoạt subscription (IsActive=true)
- **Pay-per-swap Payment**: Completed → KHÔNG THAY ĐỔI Reservation.Status
- **Lý do:** Reservation.Status chỉ chuyển từ Pending → CheckedIn khi Staff scan QR tại trạm (flow riêng)

---

#### 📍 **BƯỚC 2A.6: VNPay redirect user về frontend**

##### VNPay Request:
```
GET /payment/success?ref=EVB20251025090000123&amount=2500000
```

##### Frontend Flow:
```javascript
// Page: /payment/success
useEffect(() => {
  const txnRef = searchParams.get('ref')
  const amount = searchParams.get('amount')
  
  // Hiển thị thông báo thành công
  showSuccessMessage(`Thanh toán ${amount/100} VND thành công!`)
  
  // Redirect về trang Reservations
  setTimeout(() => router.push('/reservations'), 3000)
}, [])
```

##### Backend Status:
**Kết luận:** ✅ **Frontend tự xử lý, không cần BE**

---

### **LUỒNG 2B: THANH TOÁN TIỀN MẶT**

#### 📍 **BƯỚC 2B.1: Frontend gọi cùng API nhưng paymentMethod=1**

##### Frontend Request:
```javascript
const handleConfirmAndPay = async (method) => {
  const response = await api.post('/api/v1/payments/create-pay-per-swap-reservation', {
    stationId: "guid-station",
    batteryModelId: "guid-battery-model",
    slotDate: "2025-10-25",
    slotStartTime: "09:00:00",
    slotEndTime: "09:30:00",
    amount: 25000,
    paymentMethod: 1  // ⬅️ 1 = Cash (khác với VNPay)
  })
}
```

##### Backend Logic (đã phân tích ở BƯỚC 2A.1):
```csharp
// Trong PaymentService.CreatePayPerSwapReservationAsync()

if (request.PaymentMethod == PaymentMethod.VNPay)
{
    // LUỒNG 2A: Return paymentUrl
}
else // PaymentMethod.Cash
{
    // LUỒNG 2B: Return reservation details + QR code
    return new CreatePayPerSwapReservationResponse
    {
        Success = true,
        Message = "Đã tạo lịch hẹn. Vui lòng thanh toán tiền mặt tại trạm.",
        ReservationId = reservation.Id,
        PaymentId = payment.Id,
        QRCode = reservation.QRCode,  // ⬅️ QUAN TRỌNG: Trả về QR
        Status = "Pending",  // Reservation status
        Amount = request.Amount,
        Instructions = "Vui lòng đến trạm đúng giờ và xuất trình mã QR để thanh toán tiền mặt."
    };
}
```

##### Response Format:
```json
{
  "success": true,
  "message": "Đã tạo lịch hẹn. Vui lòng thanh toán tiền mặt tại trạm.",
  "reservationId": "guid-reservation",
  "paymentId": "guid-payment",
  "qrCode": "eyJyaWQiOiJndWlkIiwidHMiOjE3Mjk1ODc2MDAsInNpZyI6IjEyMyJ9",
  "status": "Pending",
  "amount": 25000,
  "instructions": "Vui lòng đến trạm đúng giờ và xuất trình mã QR để thanh toán tiền mặt."
}
```

##### Backend Status:
| Thành phần | Có sẵn? | Ghi chú |
|------------|---------|---------|
| QR Code generation | ✅ ĐÃ CÓ | `SlotReservationService.GenerateQRCode()` |
| Response format | ⚠️ CẦN TẠO | DTO đã thiết kế ở trên |

---

#### 📍 **BƯỚC 2B.2: Frontend nhận response và hiển thị**

##### Frontend Flow:
```javascript
const response = await api.post('/api/v1/payments/create-pay-per-swap-reservation', {...})

if (response.data.success) {
  // Lưu vào state
  setActiveReservation(response.data)
  
  // Chuyển sang màn hình thành công (Bước 5)
  setBookingStep(5)
}
```

##### Frontend UI (Bước 5):
```jsx
<SuccessScreen>
  <CheckIcon />
  <h2>Đặt lịch thành công!</h2>
  <p>Vui lòng thanh toán 25.000 VND tại trạm khi check-in.</p>
  
  <QRCodeDisplay data={activeReservation.qrCode} />
  
  <ReservationDetails>
    Trạm: Trạm Q1
    Ngày giờ: 25/10/2025 09:00-09:30
    Trạng thái: Đang chờ
  </ReservationDetails>
  
  <Button onClick={() => router.push('/reservations')}>
    Xem lịch hẹn của tôi
  </Button>
</SuccessScreen>
```

##### Backend Status:
**Kết luận:** ✅ **Frontend tự xử lý UI, không cần BE**

---

#### 📍 **BƯỚC 2B.3: User đến trạm check-in và thanh toán**

##### Workflow tại trạm:

**1. User đến trạm đúng giờ (09:00-09:30)**

**2. Staff quét QR code**
```javascript
// Staff App
const qrData = scanQR()  // "eyJyaWQiOiJndWlkIiwidHMiOjE3Mjk1ODc2MDAsInNpZyI6IjEyMyJ9"

const response = await api.post('/api/v1/slot-reservations/check-in', {
  reservationId: extractReservationId(qrData),
  qrCodeData: qrData,
  staffId: currentStaffId
})
```

**3. Backend validate QR + reservation**

##### Backend Current Status:
| Thành phần | Có sẵn? | Vấn đề |
|------------|---------|--------|
| Endpoint check-in | ✅ ĐÃ CÓ `POST /api/v1/slot-reservations/check-in` | ✅ OK |
| `SlotReservationService.CheckInAsync()` | ✅ ĐÃ CÓ | ⚠️ **CẦN KIỂM TRA PAYMENT** |

##### Logic cần bổ sung:

**SlotReservationService.CheckInAsync() (HIỆN TẠI)**
```csharp
public async Task<CheckInResult> CheckInAsync(
    Guid reservationId, 
    string qrCodeData, 
    Guid staffId)
{
    // 1. Validate QR code ✅ OK
    // 2. Find reservation ✅ OK
    // 3. Validate time window ✅ OK
    // 4. Assign battery ✅ OK
    // 5. Update status ✅ OK
    
    reservation.Status = ReservationStatus.CheckedIn;
    reservation.CheckedInAt = DateTime.UtcNow;
    reservation.VerifiedByStaffId = staffId;
    reservation.BatteryUnitId = battery.Id;
    
    await _context.SaveChangesAsync();
}
```

##### Logic SAU KHI BỔ SUNG (cho LUỒNG 2B - Cash):

**SlotReservationService.CheckInAsync() (MỚI)**
```csharp
public async Task<CheckInResult> CheckInAsync(
    Guid reservationId, 
    string qrCodeData, 
    Guid staffId)
{
    // ... existing validation logic ...
    
    // ⬅️ THÊM MỚI: Kiểm tra payment status (cho pay-per-swap)
    var payment = await _context.Payments
        .FirstOrDefaultAsync(p => 
            p.ReservationId == reservationId && 
            p.Type == PaymentType.PayPerSwap);
    
    if (payment != null)
    {
        // Có payment → Phải kiểm tra đã thanh toán chưa
        if (payment.Status == PaymentStatus.Pending && payment.Method == PaymentMethod.Cash)
        {
            // ⬅️ TRƯỜNG HỢP: Chọn Cash NHƯNG CHƯA THANH TOÁN
            // → Staff phải confirm thanh toán trước khi check-in
            
            throw new PaymentRequiredException(
                "Khách hàng chưa thanh toán. Vui lòng xác nhận thanh toán tiền mặt trước."
            );
        }
        else if (payment.Status != PaymentStatus.Completed)
        {
            // Payment failed hoặc cancelled
            throw new InvalidOperationException(
                $"Payment không hợp lệ (Status: {payment.Status}). Không thể check-in."
            );
        }
        
        // ✅ Payment.Status == Completed → OK, tiếp tục check-in
    }
    
    // ... existing battery assignment + status update logic ...
    
    reservation.Status = ReservationStatus.CheckedIn;
    reservation.CheckedInAt = DateTime.UtcNow;
    reservation.VerifiedByStaffId = staffId;
    reservation.BatteryUnitId = battery.Id;
    
    await _context.SaveChangesAsync();
    
    _logger.LogInformation(
        "Checked in reservation {ReservationId}, payment {PaymentId} (Status: {PaymentStatus})",
        reservationId, payment?.Id, payment?.Status
    );
}
```

##### ⚠️ **VẤN ĐỀ: Staff confirm thanh toán CASH thế nào?**

**Có 2 CÁCH XỬ LÝ:**

---

##### **CÁCH 1: Staff confirm payment RIÊNG BIỆT (TRƯỚC khi check-in)**

**Workflow:**
1. User đến trạm → Staff scan QR
2. Backend báo: "Khách chưa thanh toán"
3. Staff nhận tiền 25.000 VND
4. Staff gọi API confirm payment: `POST /api/v1/payments/{paymentId}/confirm-cash`
5. Backend update Payment.Status = Completed
6. Staff gọi lại API check-in → Thành công

**Ưu điểm:**
- ✅ Tách biệt payment và check-in
- ✅ Dùng lại API confirm-cash đã có từ LUỒNG 1
- ✅ Tracking rõ ràng: Staff nào confirm payment, Staff nào verify check-in

**Nhược điểm:**
- ❌ Staff phải gọi 2 API (nhiều bước)

**Implementation:**
```csharp
// Staff App Flow
try {
  // Bước 1: Thử check-in
  await api.post('/api/v1/slot-reservations/check-in', {
    reservationId, qrCodeData, staffId
  })
} catch (error) {
  if (error.code === 'PAYMENT_REQUIRED') {
    // Bước 2: Hiển thị popup xác nhận thanh toán
    const confirmed = await showConfirmDialog(
      "Khách hàng chưa thanh toán 25.000 VND. Đã nhận tiền chưa?"
    )
    
    if (confirmed) {
      // Bước 3: Confirm payment
      await api.post(`/api/v1/payments/${error.paymentId}/confirm-cash`, {
        notes: "Nhận tiền mặt 25.000 VND tại trạm"
      })
      
      // Bước 4: Retry check-in
      await api.post('/api/v1/slot-reservations/check-in', {
        reservationId, qrCodeData, staffId
      })
    }
  }
}
```

**Backend status:**
- ✅ API `POST /payments/{id}/confirm-cash` ĐÃ CÓ từ LUỒNG 1
- ⚠️ CẦN SỬA để xử lý cả PayPerSwap (hiện tại chỉ xử lý Subscription)

**PaymentService.ConfirmCashPaymentAsync() CẦN SỬA:**
```csharp
public async Task<ConfirmCashPaymentResponse> ConfirmCashPaymentAsync(
    Guid staffId, 
    Guid paymentId, 
    ConfirmCashPaymentRequest request)
{
    // ... existing validation ...
    
    payment.Status = PaymentStatus.Completed;
    payment.CompletedAt = DateTime.UtcNow;
    payment.ProcessedByStaffId = staffId;
    
    // PHÂN NHÁNH THEO LOẠI PAYMENT
    if (payment.Type == PaymentType.Subscription && payment.UserSubscription != null)
    {
        // ⬅️ LUỒNG 1: Kích hoạt subscription (LOGIC CŨ - GIỮ NGUYÊN)
        if (!payment.UserSubscription.IsActive)
        {
            var now = DateTime.UtcNow;
            payment.UserSubscription.IsActive = true;
            payment.UserSubscription.StartDate = now;
            // ... rest of activation
        }
        
        return new ConfirmCashPaymentResponse
        {
            Success = true,
            Message = "Xác nhận thanh toán thành công. Gói subscription đã được kích hoạt!",
            PaymentId = payment.Id,
            SubscriptionActivated = true,
            SubscriptionId = payment.UserSubscription.Id
        };
    }
    else if (payment.Type == PaymentType.PayPerSwap && payment.Reservation != null)
    {
        // ⬅️ LUỒNG 2: Thanh toán cho reservation (LOGIC MỚI)
        // KHÔNG cần kích hoạt gì, chỉ update payment status
        
        return new ConfirmCashPaymentResponse
        {
            Success = true,
            Message = "Xác nhận thanh toán thành công. Khách hàng có thể check-in.",
            PaymentId = payment.Id,
            SubscriptionActivated = false,  // N/A
            SubscriptionId = null,
            ReservationId = payment.Reservation.Id  // ⬅️ THÊM FIELD MỚI VÀO DTO
        };
    }
    
    await _context.SaveChangesAsync();
}
```

---

##### **CÁCH 2: Staff confirm payment CÙNG LÚC với check-in (1 API call)**

**Workflow:**
1. User đến trạm → Staff scan QR
2. Backend phát hiện: Payment pending + Method = Cash
3. Backend tự động confirm payment + check-in trong cùng transaction
4. Staff chỉ gọi 1 API: `POST /api/v1/slot-reservations/check-in`

**Ưu điểm:**
- ✅ Đơn giản: Staff chỉ gọi 1 API
- ✅ Atomic: Payment + Check-in trong cùng transaction
- ✅ UX tốt hơn

**Nhược điểm:**
- ❌ Ít flexible: Không tách biệt payment và check-in
- ❌ Khó tracking: Không rõ Staff nào confirm payment (vì cùng 1 staffId)

**Implementation:**
```csharp
// SlotReservationService.CheckInAsync() (MỚI)
public async Task<CheckInResult> CheckInAsync(
    Guid reservationId, 
    string qrCodeData, 
    Guid staffId)
{
    using var transaction = await _context.Database.BeginTransactionAsync();
    
    try
    {
        // ... existing validation ...
        
        // Kiểm tra payment
        var payment = await _context.Payments
            .FirstOrDefaultAsync(p => 
                p.ReservationId == reservationId && 
                p.Type == PaymentType.PayPerSwap);
        
        if (payment != null && payment.Status == PaymentStatus.Pending)
        {
            if (payment.Method == PaymentMethod.Cash)
            {
                // ⬅️ AUTO-CONFIRM CASH PAYMENT
                payment.Status = PaymentStatus.Completed;
                payment.CompletedAt = DateTime.UtcNow;
                payment.ProcessedByStaffId = staffId;
                
                _logger.LogInformation(
                    "Auto-confirmed CASH payment {PaymentId} during check-in by staff {StaffId}",
                    payment.Id, staffId
                );
            }
            else
            {
                // VNPay payment vẫn pending → Lỗi
                throw new InvalidOperationException(
                    "Thanh toán VNPay chưa hoàn tất. Vui lòng thanh toán trước khi check-in."
                );
            }
        }
        
        // ... existing battery assignment + check-in logic ...
        
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        
        return new CheckInResult { Success = true };
    }
    catch
    {
        await transaction.RollbackAsync();
        throw;
    }
}
```

---

##### **⚖️ SO SÁNH 2 CÁCH:**

| Tiêu chí | CÁCH 1: Riêng biệt | CÁCH 2: Cùng lúc |
|----------|-------------------|------------------|
| Số API calls | 2 (confirm + check-in) | 1 (check-in) |
| UX | ⚠️ Nhiều bước | ✅ Đơn giản |
| Tracking | ✅ Rõ ràng | ⚠️ Khó phân biệt |
| Flexibility | ✅ Cao | ⚠️ Thấp |
| Error handling | ⚠️ Phức tạp | ✅ Đơn giản |
| Transaction | ⚠️ 2 transactions | ✅ 1 transaction atomic |

**💡 KHUYẾN NGHỊ: CÁCH 2 (Cùng lúc)**
- Đơn giản hơn cho Staff
- Atomic transaction đảm bảo consistency
- Phù hợp với business flow: Thanh toán và check-in xảy ra cùng lúc tại trạm

---

## 📋 TÓM TẮT CÁC THAY ĐỔI CẦN THỰC HIỆN

### **1. Database Schema**
```sql
-- Thêm ReservationId vào Payment
ALTER TABLE Payments ADD ReservationId uniqueidentifier NULL;
ALTER TABLE Payments ADD CONSTRAINT FK_Payments_Reservations 
  FOREIGN KEY (ReservationId) REFERENCES Reservations(Id);
```

### **2. Models**
- ✅ `Payment.cs`: Thêm `ReservationId` property + navigation
- ✅ `AppDbContext.cs`: Thêm relationship configuration

### **3. DTOs** (Tạo mới)
- ✅ `CreatePayPerSwapReservationRequest.cs`
- ✅ `CreatePayPerSwapReservationResponse.cs`
- ✅ Update `ConfirmCashPaymentResponse.cs`: Thêm `ReservationId` field

### **4. Services**
- ✅ `IPaymentService.cs`: Thêm method `CreatePayPerSwapReservationAsync()`
- ✅ `PaymentService.cs`: 
  - Implement `CreatePayPerSwapReservationAsync()`
  - Helper: `GenerateVnPayUrlForReservation()`
  - Update `ConfirmCashPaymentAsync()`: Xử lý cả PayPerSwap
- ✅ `VnPayService.cs`:
  - Update `ProcessCallbackAsync()`: Xử lý cả PayPerSwap
- ✅ `SlotReservationService.cs`:
  - Update `CheckInAsync()`: Auto-confirm cash payment

### **5. Controllers**
- ✅ `PaymentsController.cs`: Thêm endpoint `POST /api/v1/payments/create-pay-per-swap-reservation`

### **6. Migrations**
```bash
dotnet ef migrations add AddReservationIdToPayment --project src/EVBSS.Api
dotnet ef database update --project src/EVBSS.Api
```

---

## 🎯 KẾT LUẬN

### **Điểm mạnh hiện tại:**
1. ✅ Đã có `SlotReservationService` → Tái sử dụng logic tạo reservation
2. ✅ Đã có `VnPayService` → Chỉ cần adapt cho PayPerSwap
3. ✅ Đã có QR code generation
4. ✅ Đã có check-in flow

### **Gap chính:**
1. ❌ **Thiếu API chính**: `POST /api/v1/payments/create-pay-per-swap-reservation`
2. ❌ **Thiếu field**: `Payment.ReservationId`
3. ⚠️ **Callback logic**: VnPay chỉ xử lý Subscription
4. ⚠️ **Check-in logic**: Chưa kiểm tra payment status

### **Độ phức tạp implement:**
- 🟢 **Low:** Database migration (thêm 1 column)
- 🟡 **Medium:** DTOs, endpoint mới
- 🟡 **Medium:** Update VnPay callback logic
- 🟢 **Low:** Update check-in logic

### **Thời gian ước tính:**
- Database + Models: **30 phút**
- DTOs: **20 phút**
- PaymentService: **1 giờ**
- VnPayService update: **30 phút**
- SlotReservationService update: **30 phút**
- Controller: **20 phút**
- Testing: **1 giờ**

**TỔNG: ~4 giờ** (nếu không có vấn đề gì phát sinh)

---

## 📝 NEXT STEPS

Bạn muốn tôi:
1. ✅ **Tạo TODO list chi tiết** để implement LUỒNG 2?
2. ✅ **Bắt đầu implement** từng task?
3. ✅ **Tạo migration** cho Payment.ReservationId?
4. ✅ **Tạo HTTP test file** cho LUỒNG 2?

Hãy cho tôi biết! 🚀
