# 🎯 VNPAY PAYMENT INTEGRATION - MASTER GUIDE
## Hướng dẫn tích hợp hoàn chỉnh Cổng thanh toán VNPAY

> **⚠️ ĐÂY LÀ CƠ HỘI CUỐI CÙNG - ĐỌC KỸ VÀ THỰC HIỆN CHÍNH XÁC 100%**

---

## 📊 TIMELINE - QUY TRÌNH TÍCH HỢP

```
1. Cài đặt Code        →  2. Kiểm tra tích hợp  →  3. Golive kết nối
   (Build URL             (Test cases)              (Deploy production)
    thanh toán)            
```

### 3 Bước Merchant cần xử lý:

1. ✅ **Cài đặt code build URL thanh toán** chuyển hướng
2. ✅ **Cài đặt code vnp_ReturnUrl** - URL thông báo kết quả thanh toán
3. ✅ **Cài đặt code IPN URL** - cập nhật kết quả thanh toán. **Gửi lại VNPAY URL này khi thiết lập xong**

---

## 🔄 MÔ HÌNH KẾT NỐI - LUỒNG THANH TOÁN

### Bước 1: Khách hàng mua hàng
- Khách hàng thực hiện mua hàng trên Website/App và tiến hành thanh toán trực tuyến

### Bước 2: Merchant tạo URL thanh toán
- Website/App TMĐT **thành lập yêu cầu thanh toán dưới dạng URL** mang thông tin thanh toán
- **Chuyển hướng khách hàng sang Cổng thanh toán VNPAY** bằng URL đó
- Cổng thanh toán VNPAY xử lý yêu cầu thanh toán
- Khách hàng nhập/xác thực thông tin được yêu cầu

### Bước 3,4: Xác thực tại Ngân hàng
- Khách hàng nhập thông tin để xác minh tài khoản Ngân hàng
- Xác thực giao dịch (Nhập thông tin tài khoản, thẻ hoặc quét mã VNPAY-QR)

### Bước 5: VNPAY xử lý kết quả
**Giao dịch thành công tại Ngân hàng, VNPAY tiến hành:**

1. **Chuyển hướng khách hàng về Website/App** (`vnp_ReturnUrl`)
2. **Thông báo cho Website/App kết quả thanh toán** qua `IPN URL` (Server-to-Server)
   - ⚠️ **Merchant CẬP NHẬT KẾT QUẢ THANH TOÁN tại IPN URL này**

### Bước 6: Hiển thị kết quả
- Merchant hiển thị kết quả giao dịch tới khách hàng (`vnp_ReturnUrl`)

---

## 🔐 THÔNG TIN CẤU HÌNH

### Merchant Credentials (Sandbox)
```
vnp_TmnCode:    OE2KYEVL
vnp_HashSecret: WBXOSWTZIWY391QZZSJUGA2AF0D9QS5S
```

### URLs
```
Payment URL:    https://sandbox.vnpayment.vn/paymentv2/vpcpay.html
Return URL:     http://localhost:5173/driver?payment=vnpay
IPN URL:        https://lowly-chronoscopic-harper.ngrok-free.dev/api/v1/payments/vnpay/callback
Query API:      https://sandbox.vnpayment.vn/merchant_webapi/api/transaction
```

### Merchant Admin Portal
```
URL:      https://sandbox.vnpayment.vn/merchantv2/
Email:    phamvanminh150204@gmail.com
Password: (Mật khẩu đăng ký Merchant TEST)
```

### Test Case Portal (SIT Testing)
```
URL:      https://sandbox.vnpayment.vn/vnpaygw-sit-testing/user/login
Email:    phamvanminh150204@gmail.com
Password: (Mật khẩu đăng ký Merchant TEST)
```

---

## 🛠️ PHẦN 1: TẠO URL THANH TOÁN

### URL Format
```
https://sandbox.vnpayment.vn/paymentv2/vpcpay.html?vnp_Amount=1806000&vnp_Command=pay&vnp_CreateDate=20210801153333&vnp_CurrCode=VND&vnp_IpAddr=127.0.0.1&vnp_Locale=vn&vnp_OrderInfo=Thanh+toan+don+hang+%3A5&vnp_OrderType=other&vnp_ReturnUrl=https%3A%2F%2Fdomainmerchant.vn%2FReturnUrl&vnp_TmnCode=DEMOV210&vnp_TxnRef=5&vnp_Version=2.1.0&vnp_SecureHash=3e0d61a0c0534b2e36680b3f7277743e8784cc4e1d68fa7d276e79c23be7d6318d338b477910a27992f5057bb1582bd44bd82ae8009ffaf6d141219218625c42
```

**Phương thức: GET**

### Danh sách tham số GỬI SANG VNPAY (vnp_Command=pay)

| Tham số | Kiểu | Bắt buộc/Tùy chọn | Mô tả |
|---------|------|-------------------|-------|
| **vnp_Version** | Alphanumeric[1,8] | **Bắt buộc** | Phiên bản API: `2.1.0` |
| **vnp_Command** | Alpha[1,16] | **Bắt buộc** | Mã API: `pay` |
| **vnp_TmnCode** | Alphanumeric[8] | **Bắt buộc** | Mã website: `OE2KYEVL` |
| **vnp_Amount** | Numeric[1,12] | **Bắt buộc** | Số tiền x100. VD: 10,000 VND → `1000000` |
| **vnp_BankCode** | Alphanumeric[3,20] | Tùy chọn | Mã ngân hàng. Bỏ trống = user chọn tại VNPAY |
| **vnp_CreateDate** | Numeric[14] | **Bắt buộc** | Thời gian tạo GD: `yyyyMMddHHmmss` (GMT+7) |
| **vnp_CurrCode** | Alpha[3] | **Bắt buộc** | Đơn vị tiền tệ: `VND` |
| **vnp_IpAddr** | Alphanumeric[7,45] | **Bắt buộc** | IP khách hàng. VD: `13.160.92.202` |
| **vnp_Locale** | Alpha[2,5] | **Bắt buộc** | Ngôn ngữ: `vn` hoặc `en` |
| **vnp_OrderInfo** | Alphanumeric[1,255] | **Bắt buộc** | **🔴 TIẾNG VIỆT KHÔNG DẤU, không ký tự đặc biệt** |
| **vnp_OrderType** | Alpha[1,100] | **Bắt buộc** | Mã danh mục: `billpayment`, `other`, `topup` |
| **vnp_ReturnUrl** | Alphanumeric[10,255] | **Bắt buộc** | URL redirect user về |
| **vnp_TxnRef** | Alphanumeric[1,100] | **Bắt buộc** | Mã tham chiếu GD **duy nhất trong ngày** |
| **vnp_ExpireDate** | Numeric[14] | **Bắt buộc** | Hạn thanh toán: `yyyyMMddHHmmss` (GMT+7) |
| **vnp_SecureHash** | Alphanumeric[32,256] | **Bắt buộc** | Checksum HMAC-SHA512 |

### 🔴 LƯU Ý QUAN TRỌNG

#### 1. Checksum (vnp_SecureHash)
- Dữ liệu checksum được thành lập dựa trên **sắp xếp tăng dần của tên tham số** (A-Z)
- Sử dụng HMAC-SHA512 với `vnp_HashSecret`

#### 2. Số tiền (vnp_Amount)
- **BẮT BUỘC nhân với 100** để triệt tiêu phần thập phân
- VD: 100,000 VND → gửi `10000000`

#### 3. Mã ngân hàng (vnp_BankCode)
- **Tùy chọn**
- Nếu không gửi → user chọn phương thức tại VNPAY
- Nếu gửi → chọn ngân hàng trước tại website merchant
- Lấy danh sách ngân hàng:
  ```
  Endpoint: https://sandbox.vnpayment.vn/qrpayauth/api/merchant/get_bank_list
  Method: POST
  Content-Type: application/x-www-form-urlencoded
  Body: tmn_code=OE2KYEVL
  ```

#### 4. vnp_OrderInfo
- **🔴 BẮT BUỘC: Tiếng Việt KHÔNG DẤU**
- **🔴 KHÔNG được chứa ký tự đặc biệt**
- ✅ Đúng: `Nap tien cho thue bao 0123456789. So tien 100,000 VND`
- ❌ Sai: `Nạp tiền cho thuê bao 0123456789. Số tiền 100,000 VND`

#### 5. vnp_ReturnUrl
- URL thông báo kết quả khi khách hàng kết thúc thanh toán
- User sẽ được redirect về URL này

### Code C# Tạo URL Thanh Toán (Official VNPay)

```csharp
protected void btnPay_Click(object sender, EventArgs e)
{
    // Get Config
    string vnp_Returnurl = ConfigurationManager.AppSettings["vnp_Returnurl"];
    string vnp_Url = ConfigurationManager.AppSettings["vnp_Url"];
    string vnp_TmnCode = ConfigurationManager.AppSettings["vnp_TmnCode"];
    string vnp_HashSecret = ConfigurationManager.AppSettings["vnp_HashSecret"];
    
    if (string.IsNullOrEmpty(vnp_TmnCode) || string.IsNullOrEmpty(vnp_HashSecret))
    {
        lblMessage.Text = "Vui lòng cấu hình vnp_TmnCode, vnp_HashSecret";
        return;
    }
    
    // Prepare Order
    OrderInfo order = new OrderInfo();
    order.OrderId = DateTime.Now.Ticks; // Mã giao dịch merchant
    order.Amount = 100000; // Số tiền (VND)
    order.Status = "0"; // 0: Pending
    order.OrderDesc = txtOrderDesc.Text;
    order.CreatedDate = DateTime.Now;
    string locale = cboLanguage.SelectedItem.Value;
    
    // Build VNPAY URL
    VnPayLibrary vnpay = new VnPayLibrary();
    
    vnpay.AddRequestData("vnp_Version", VnPayLibrary.VERSION); // 2.1.0
    vnpay.AddRequestData("vnp_Command", "pay");
    vnpay.AddRequestData("vnp_TmnCode", vnp_TmnCode);
    
    // 🔴 NHÂN 100
    vnpay.AddRequestData("vnp_Amount", (order.Amount * 100).ToString());
    
    if (cboBankCode.SelectedItem != null && !string.IsNullOrEmpty(cboBankCode.SelectedItem.Value))
    {
        vnpay.AddRequestData("vnp_BankCode", cboBankCode.SelectedItem.Value);
    }
    
    vnpay.AddRequestData("vnp_CreateDate", order.CreatedDate.ToString("yyyyMMddHHmmss"));
    vnpay.AddRequestData("vnp_CurrCode", "VND");
    vnpay.AddRequestData("vnp_IpAddr", Utils.GetIpAddress());
    
    if (!string.IsNullOrEmpty(locale))
        vnpay.AddRequestData("vnp_Locale", locale);
    else
        vnpay.AddRequestData("vnp_Locale", "vn");
    
    // 🔴 OrderInfo - KHÔNG DẤU
    vnpay.AddRequestData("vnp_OrderInfo", "Thanh toan don hang:" + order.OrderId);
    vnpay.AddRequestData("vnp_OrderType", orderCategory.SelectedItem.Value);
    vnpay.AddRequestData("vnp_ReturnUrl", vnp_Returnurl);
    
    // 🔴 TxnRef - DUY NHẤT TRONG NGÀY
    vnpay.AddRequestData("vnp_TxnRef", order.OrderId.ToString());
    
    // Expire Date
    vnpay.AddRequestData("vnp_ExpireDate", txtExpire.Text);
    
    // Billing Info (Optional)
    vnpay.AddRequestData("vnp_Bill_Mobile", txt_billing_mobile.Text.Trim());
    vnpay.AddRequestData("vnp_Bill_Email", txt_billing_email.Text.Trim());
    // ... more billing fields
    
    // Create Payment URL
    string paymentUrl = vnpay.CreateRequestUrl(vnp_Url, vnp_HashSecret);
    log.InfoFormat("VNPAY URL: {0}", paymentUrl);
    
    // Redirect to VNPAY
    Response.Redirect(paymentUrl);
}
```

---

## 🔔 PHẦN 2: CÀI ĐẶT IPN URL (Server-to-Server Callback)

### Mục đích
- **🔴 VNPAY gọi về server merchant để thông báo kết quả thanh toán**
- **🔴 CẬP NHẬT DATABASE TẠI ĐÂY**
- **🔴 TRẢ VỀ JSON cho VNPAY biết đã nhận**

### Yêu cầu
- ✅ **IPN URL cần có SSL** (HTTPS)
- ✅ **Nhận kết quả phản hồi từ VNPAY**
- ✅ **Kiểm tra dữ liệu (checksum)**
- ✅ **Cập nhật kết quả vào Database**
- ✅ **Phản hồi RspCode và Message cho VNPAY**

### URL Format từ VNPAY
```
https://{domain}/IPN?vnp_Amount=1000000&vnp_BankCode=NCB&vnp_BankTranNo=VNP14226112&vnp_CardType=ATM&vnp_OrderInfo=Thanh+toan+don+hang+thoi+gian%3A+2023-12-07+17%3A00%3A44&vnp_PayDate=20231207170112&vnp_ResponseCode=00&vnp_TmnCode=CTTVNP01&vnp_TransactionNo=14226112&vnp_TransactionStatus=00&vnp_TxnRef=166117&vnp_SecureHash=b6dababca5e07a2d8e32fdd3cf05c29cb426c721ae18e9589f7ad0e2db4b657c6e0e5cc8e271cf745162bcb100fdf2f64520554a6f5275bc4c5b5b3e57dc4b4b
```

**Phương thức: GET**

### Danh sách tham số NHẬN VỀ từ VNPAY

| Tham số | Kiểu | Bắt buộc/Tùy chọn | Mô tả |
|---------|------|-------------------|-------|
| **vnp_TmnCode** | Alphanumeric[8] | **Bắt buộc** | Mã website: `OE2KYEVL` |
| **vnp_Amount** | Numeric[1,12] | **Bắt buộc** | Số tiền **đã nhân 100**. Cần chia 100 |
| **vnp_BankCode** | Alphanumeric[3,20] | **Bắt buộc** | Mã ngân hàng. VD: `NCB` |
| **vnp_BankTranNo** | Alphanumeric[1,255] | Tùy chọn | Mã GD tại Ngân hàng |
| **vnp_CardType** | Alpha[2,20] | Tùy chọn | Loại thẻ: `ATM`, `QRCODE` |
| **vnp_PayDate** | Numeric[14] | Tùy chọn | Thời gian thanh toán: `yyyyMMddHHmmss` |
| **vnp_OrderInfo** | Alphanumeric[1,255] | **Bắt buộc** | Thông tin đơn hàng (không dấu) |
| **vnp_TransactionNo** | Numeric[1,15] | **Bắt buộc** | Mã GD tại VNPAY |
| **vnp_ResponseCode** | Numeric[2] | **Bắt buộc** | Mã phản hồi. `00` = Thành công |
| **vnp_TransactionStatus** | Numeric[2] | **Bắt buộc** | Trạng thái GD. `00` = Thành công |
| **vnp_TxnRef** | Alphanumeric[1,100] | **Bắt buộc** | Mã tham chiếu (giống lúc gửi) |
| **vnp_SecureHash** | Alphanumeric[32,256] | **Bắt buộc** | Checksum để verify |

### 🔴 LƯU Ý IPN URL

#### 1. Kiểm tra toàn vẹn dữ liệu
- **BẮT BUỘC kiểm tra checksum TRƯỚC KHI xử lý**

#### 2. Cập nhật Database
- **🔴 CẬP NHẬT KẾT QUẢ THANH TOÁN TẠI ĐÂY**
- Đây là URL server-to-server (VNPAY gọi merchant)

#### 3. Trả về JSON Response
```json
{
  "RspCode": "00",
  "Message": "Confirm Success"
}
```

#### 4. Cơ chế Retry IPN
- **RspCode: 00, 02** → VNPAY kết thúc luồng (đã cập nhật thành công)
- **RspCode: 01, 04, 97, 99** hoặc timeout → VNPAY retry
- **Tổng số lần retry: 10 lần**
- **Khoảng cách: 5 phút/lần**

#### 5. Response Codes cho IPN

| RspCode | Message | Ý nghĩa |
|---------|---------|---------|
| **00** | Confirm Success | ✅ Đã cập nhật thành công |
| **01** | Order not found | ❌ Không tìm thấy đơn hàng → Retry |
| **02** | Order already confirmed | ✅ Đơn hàng đã xác nhận trước đó |
| **04** | Invalid amount | ❌ Số tiền không khớp → Retry |
| **97** | Invalid signature | ❌ Chữ ký không hợp lệ → Retry |
| **99** | Unknown error | ❌ Lỗi không xác định → Retry |

### Code C# IPN Handler (Official VNPay)

```csharp
protected void Page_Load(object sender, EventArgs e)
{
    string returnContent = string.Empty;
    
    if (Request.QueryString.Count > 0)
    {
        string vnp_HashSecret = ConfigurationManager.AppSettings["vnp_HashSecret"];
        var vnpayData = Request.QueryString;
        VnPayLibrary vnpay = new VnPayLibrary();
        
        // Collect all vnp_* parameters
        foreach (string s in vnpayData)
        {
            if (!string.IsNullOrEmpty(s) && s.StartsWith("vnp_"))
            {
                vnpay.AddResponseData(s, vnpayData[s]);
            }
        }
        
        // Get important data
        long orderId = Convert.ToInt64(vnpay.GetResponseData("vnp_TxnRef"));
        long vnp_Amount = Convert.ToInt64(vnpay.GetResponseData("vnp_Amount")) / 100; // 🔴 CHIA 100
        long vnpayTranId = Convert.ToInt64(vnpay.GetResponseData("vnp_TransactionNo"));
        string vnp_ResponseCode = vnpay.GetResponseData("vnp_ResponseCode");
        string vnp_TransactionStatus = vnpay.GetResponseData("vnp_TransactionStatus");
        string vnp_SecureHash = Request.QueryString["vnp_SecureHash"];
        
        // 🔴 VERIFY SIGNATURE
        bool checkSignature = vnpay.ValidateSignature(vnp_SecureHash, vnp_HashSecret);
        
        if (checkSignature)
        {
            // 🔴 GET ORDER FROM DATABASE
            OrderInfo order = new OrderInfo(); // Get from DB
            order.OrderId = orderId;
            order.Amount = 100000;
            order.PaymentTranId = vnpayTranId;
            order.Status = "0"; // 0: Pending, 1: Success, 2: Failed
            
            if (order != null)
            {
                // 🔴 CHECK AMOUNT
                if (order.Amount == vnp_Amount)
                {
                    // 🔴 CHECK STATUS (chưa xử lý)
                    if (order.Status == "0")
                    {
                        // 🔴 CHECK RESPONSE CODE
                        if (vnp_ResponseCode == "00" && vnp_TransactionStatus == "00")
                        {
                            // ✅ THANH TOÁN THÀNH CÔNG
                            log.InfoFormat("Thanh toan thanh cong, OrderId={0}, VNPAY TranId={1}", 
                                orderId, vnpayTranId);
                            order.Status = "1";
                        }
                        else
                        {
                            // ❌ THANH TOÁN THẤT BẠI
                            log.InfoFormat("Thanh toan loi, OrderId={0}, VNPAY TranId={1}, ResponseCode={2}",
                                orderId, vnpayTranId, vnp_ResponseCode);
                            order.Status = "2";
                        }
                        
                        // 🔴 UPDATE DATABASE HERE
                        // SaveToDB(order);
                        
                        returnContent = "{\"RspCode\":\"00\",\"Message\":\"Confirm Success\"}";
                    }
                    else
                    {
                        // Đơn hàng đã xử lý trước đó
                        returnContent = "{\"RspCode\":\"02\",\"Message\":\"Order already confirmed\"}";
                    }
                }
                else
                {
                    // Số tiền không khớp
                    returnContent = "{\"RspCode\":\"04\",\"Message\":\"invalid amount\"}";
                }
            }
            else
            {
                // Không tìm thấy order
                returnContent = "{\"RspCode\":\"01\",\"Message\":\"Order not found\"}";
            }
        }
        else
        {
            // Chữ ký không hợp lệ
            log.InfoFormat("Invalid signature, InputData={0}", Request.RawUrl);
            returnContent = "{\"RspCode\":\"97\",\"Message\":\"Invalid signature\"}";
        }
    }
    else
    {
        returnContent = "{\"RspCode\":\"99\",\"Message\":\"Input data required\"}";
    }
    
    // 🔴 TRẢ VỀ JSON
    Response.ClearContent();
    Response.Write(returnContent);
    Response.End();
}
```

---

## 🌐 PHẦN 3: CÀI ĐẶT RETURN URL (User Redirect)

### Mục đích
- **Hiển thị kết quả thanh toán cho khách hàng**
- **🔴 KHÔNG CẬP NHẬT DATABASE TẠI ĐÂY**
- Chỉ kiểm tra checksum và hiển thị thông báo

### URL Format từ VNPAY
```
https://{domain}/ReturnUrl?vnp_Amount=1000000&vnp_BankCode=NCB&vnp_BankTranNo=VNP14226112&vnp_CardType=ATM&vnp_OrderInfo=Thanh+toan+don+hang+thoi+gian%3A+2023-12-07+17%3A00%3A44&vnp_PayDate=20231207170112&vnp_ResponseCode=00&vnp_TmnCode=CTTVNP01&vnp_TransactionNo=14226112&vnp_TransactionStatus=00&vnp_TxnRef=166117&vnp_SecureHash=...
```

### Danh sách tham số
**Giống với tham số IPN URL**

### 🔴 LƯU Ý RETURN URL

#### 1. Chỉ kiểm tra và hiển thị
- **CHỈ kiểm tra toàn vẹn dữ liệu (checksum)**
- **CHỈ hiển thị thông báo tới khách hàng**
- **🔴 KHÔNG CẬP NHẬT KẾT QUẢ GIAO DỊCH**

#### 2. Frontend sẽ nhận URL này
- Frontend cần parse query params
- Hiển thị thông báo cho user
- **Không cần gọi API backend** (vì IPN đã cập nhật DB rồi)

### Code C# Return URL Handler (Official VNPay)

```csharp
protected void Page_Load(object sender, EventArgs e)
{
    log.InfoFormat("Begin VNPAY Return, URL={0}", Request.RawUrl);
    
    if (Request.QueryString.Count > 0)
    {
        string vnp_HashSecret = ConfigurationManager.AppSettings["vnp_HashSecret"];
        var vnpayData = Request.QueryString;
        VnPayLibrary vnpay = new VnPayLibrary();
        
        // Collect all vnp_* parameters
        foreach (string s in vnpayData)
        {
            if (!string.IsNullOrEmpty(s) && s.StartsWith("vnp_"))
            {
                vnpay.AddResponseData(s, vnpayData[s]);
            }
        }
        
        // Get data
        long orderId = Convert.ToInt64(vnpay.GetResponseData("vnp_TxnRef"));
        long vnpayTranId = Convert.ToInt64(vnpay.GetResponseData("vnp_TransactionNo"));
        string vnp_ResponseCode = vnpay.GetResponseData("vnp_ResponseCode");
        string vnp_TransactionStatus = vnpay.GetResponseData("vnp_TransactionStatus");
        string vnp_SecureHash = Request.QueryString["vnp_SecureHash"];
        string TerminalID = Request.QueryString["vnp_TmnCode"];
        long vnp_Amount = Convert.ToInt64(vnpay.GetResponseData("vnp_Amount")) / 100;
        string bankCode = Request.QueryString["vnp_BankCode"];
        
        // 🔴 VERIFY SIGNATURE
        bool checkSignature = vnpay.ValidateSignature(vnp_SecureHash, vnp_HashSecret);
        
        if (checkSignature)
        {
            if (vnp_ResponseCode == "00" && vnp_TransactionStatus == "00")
            {
                // ✅ THANH TOÁN THÀNH CÔNG
                displayMsg.InnerText = "Giao dịch được thực hiện thành công. Cảm ơn quý khách đã sử dụng dịch vụ";
                log.InfoFormat("Thanh toan thanh cong, OrderId={0}, VNPAY TranId={1}", 
                    orderId, vnpayTranId);
            }
            else
            {
                // ❌ THANH TOÁN THẤT BẠI
                displayMsg.InnerText = "Có lỗi xảy ra trong quá trình xử lý. Mã lỗi: " + vnp_ResponseCode;
                log.InfoFormat("Thanh toan loi, OrderId={0}, VNPAY TranId={1}, ResponseCode={2}", 
                    orderId, vnpayTranId, vnp_ResponseCode);
            }
            
            // Hiển thị thông tin
            displayTmnCode.InnerText = "Mã Website (Terminal ID):" + TerminalID;
            displayTxnRef.InnerText = "Mã giao dịch thanh toán:" + orderId.ToString();
            displayVnpayTranNo.InnerText = "Mã giao dịch tại VNPAY:" + vnpayTranId.ToString();
            displayAmount.InnerText = "Số tiền thanh toán (VND):" + vnp_Amount.ToString();
            displayBankCode.InnerText = "Ngân hàng thanh toán:" + bankCode;
        }
        else
        {
            log.InfoFormat("Invalid signature, InputData={0}", Request.RawUrl);
            displayMsg.InnerText = "Có lỗi xảy ra trong quá trình xử lý";
        }
    }
}
```

---

## 📊 BẢNG MÃ LỖI HỆ THỐNG THANH TOÁN PAY

### vnp_TransactionStatus (Trạng thái giao dịch)

| Mã | Mô tả |
|----|-------|
| **00** | ✅ Giao dịch thành công |
| **01** | ⏳ Giao dịch chưa hoàn tất |
| **02** | ❌ Giao dịch bị lỗi |
| **04** | ⚠️ Giao dịch đảo (Khách hàng đã bị trừ tiền tại Ngân hàng nhưng GD chưa thành công ở VNPAY) |
| **05** | 🔄 VNPAY đang xử lý giao dịch này (GD hoàn tiền) |
| **06** | 🔄 VNPAY đã gửi yêu cầu hoàn tiền sang Ngân hàng (GD hoàn tiền) |
| **07** | ⚠️ Giao dịch bị nghi ngờ gian lận |
| **09** | ❌ GD Hoàn trả bị từ chối |

### vnp_ResponseCode (VNPAY phản hồi qua IPN và Return URL)

| Mã | Mô tả |
|----|-------|
| **00** | ✅ Giao dịch thành công |
| **07** | ⚠️ Trừ tiền thành công. Giao dịch bị nghi ngờ (liên quan tới lừa đảo, giao dịch bất thường) |
| **09** | ❌ Giao dịch không thành công do: Thẻ/Tài khoản của khách hàng chưa đăng ký dịch vụ InternetBanking tại ngân hàng |
| **10** | ❌ Giao dịch không thành công do: Khách hàng xác thực thông tin thẻ/tài khoản không đúng quá 3 lần |
| **11** | ❌ Giao dịch không thành công do: Đã hết hạn chờ thanh toán. Xin quý khách vui lòng thực hiện lại giao dịch |
| **12** | ❌ Giao dịch không thành công do: Thẻ/Tài khoản của khách hàng bị khóa |
| **13** | ❌ Giao dịch không thành công do Quý khách nhập sai mật khẩu xác thực giao dịch (OTP). Xin quý khách vui lòng thực hiện lại giao dịch |
| **24** | ❌ Giao dịch không thành công do: Khách hàng hủy giao dịch |
| **51** | ❌ Giao dịch không thành công do: Tài khoản của quý khách không đủ số dư để thực hiện giao dịch |
| **65** | ❌ Giao dịch không thành công do: Tài khoản của Quý khách đã vượt quá hạn mức giao dịch trong ngày |
| **75** | ⚠️ Ngân hàng thanh toán đang bảo trì |
| **79** | ❌ Giao dịch không thành công do: KH nhập sai mật khẩu thanh toán quá số lần quy định. Xin quý khách vui lòng thực hiện lại giao dịch |
| **99** | ❌ Các lỗi khác (lỗi còn lại, không có trong danh sách mã lỗi đã liệt kê) |

---

## 🔧 CẤU TRÚC DỰ ÁN

### appsettings.json
```json
{
  "VnPay": {
    "TmnCode": "OE2KYEVL",
    "HashSecret": "WBXOSWTZIWY391QZZSJUGA2AF0D9QS5S",
    "BaseUrl": "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html",
    "ReturnUrl": "http://localhost:5173/driver?payment=vnpay",
    "IpnUrl": "https://lowly-chronoscopic-harper.ngrok-free.dev/api/v1/payments/vnpay/callback",
    "Version": "2.1.0",
    "Command": "pay",
    "CurrCode": "VND",
    "Locale": "vn"
  }
}
```

### Services Structure
```
Services/
  VnPayService.cs
    - GeneratePaymentUrl()       // Tạo URL thanh toán
    - VerifySignature()           // Verify checksum
    - ProcessIpnCallback()        // Xử lý IPN (cập nhật DB)
    - ProcessReturnUrl()          // Xử lý Return URL (hiển thị)
    
Controllers/
  PaymentsController.cs
    - [HttpGet] VnPayCallback()   // IPN endpoint
    - [HttpGet] VnPayReturn()     // Return endpoint (optional, FE handle)
```

---

## ✅ CHECKLIST TRIỂN KHAI

### Phase 1: Chuẩn bị
- [x] Có TmnCode: `OE2KYEVL`
- [x] Có HashSecret: `WBXOSWTZIWY391QZZSJUGA2AF0D9QS5S`
- [x] Đã đọc kỹ tài liệu VNPay
- [x] Đã hiểu rõ luồng thanh toán
- [ ] Đã setup ngrok cho IPN URL

### Phase 2: Code Implementation
- [ ] Tạo VnPayService.cs mới
- [ ] Implement GeneratePaymentUrl()
  - [ ] Sắp xếp params theo A-Z
  - [ ] vnp_Amount nhân 100
  - [ ] vnp_OrderInfo không dấu
  - [ ] Tạo HMAC-SHA512 signature
- [ ] Implement VerifySignature()
  - [ ] Parse vnp_* params
  - [ ] Loại bỏ vnp_SecureHash
  - [ ] Tạo lại signature
  - [ ] So sánh với signature nhận được
- [ ] Implement ProcessIpnCallback()
  - [ ] Verify signature
  - [ ] Get order từ DB
  - [ ] Check amount
  - [ ] Check status
  - [ ] Update DB
  - [ ] Return JSON response

### Phase 3: API Endpoints
- [ ] Tạo IPN endpoint: `POST /api/v1/payments/vnpay/callback`
  - [ ] Nhận query params từ VNPAY
  - [ ] Gọi ProcessIpnCallback()
  - [ ] Trả về JSON với RspCode

### Phase 4: Testing
- [ ] Test tạo payment URL
- [ ] Test redirect to VNPAY
- [ ] Test thanh toán thành công
- [ ] Test thanh toán thất bại
- [ ] Test IPN callback nhận đúng
- [ ] Test signature verification
- [ ] Test update DB
- [ ] Test retry mechanism

### Phase 5: Configuration VNPay Portal
- [ ] Login vào Merchant Admin
- [ ] Configure IPN URL: `https://lowly-chronoscopic-harper.ngrok-free.dev/api/v1/payments/vnpay/callback`
- [ ] Test IPN URL từ SIT Portal

### Phase 6: Production Ready
- [ ] Replace sandbox URLs với production
- [ ] Update credentials production
- [ ] Enable HTTPS cho IPN URL
- [ ] Deploy và monitor

---

## 🎯 NHỮNG ĐIỂM QUAN TRỌNG NHẤT

### 1. 🔴 vnp_OrderInfo - KHÔNG DẤU
```csharp
// ❌ SAI
vnp_OrderInfo = "Nạp tiền thuê bao - Gói Premium 50GB";

// ✅ ĐÚNG
vnp_OrderInfo = "Nap tien thue bao - Goi Premium 50GB";
```

### 2. 🔴 vnp_Amount - NHÂN 100
```csharp
// ❌ SAI
vnp_Amount = 2200000; // 2,200,000 VND

// ✅ ĐÚNG
vnp_Amount = 220000000; // 2,200,000 VND x 100
```

### 3. 🔴 IPN vs Return URL
```
IPN URL:    Server-to-Server → CẬP NHẬT DATABASE
Return URL: Browser Redirect  → HIỂN THỊ cho user
```

### 4. 🔴 Signature Generation
```csharp
// HashData: KHÔNG URLEncode, plain text
hashData = "vnp_Amount=220000000&vnp_Command=pay&vnp_CreateDate=..."

// Query Params: CÓ URLEncode
query = "vnp_Amount=220000000&vnp_Command=pay&vnp_CreateDate=..."
```

### 5. 🔴 IPN Response
```json
{
  "RspCode": "00",  // 00, 02 = success → VNPAY stop
  "Message": "Confirm Success"  // 01, 04, 97, 99 = retry
}
```

### 6. 🔴 IPv4 Only
```csharp
// ❌ SAI
vnp_IpAddr = "::1"; // IPv6

// ✅ ĐÚNG
vnp_IpAddr = "127.0.0.1"; // IPv4
```

---

## 📚 TÀI LIỆU THAM KHẢO

- **Tài liệu API**: https://sandbox.vnpayment.vn/apis/docs/thanh-toan-pay/pay.html
- **Code Demo**: https://sandbox.vnpayment.vn/apis/vnpay-demo/code-demo-tích-hợp
- **Merchant Admin**: https://sandbox.vnpayment.vn/merchantv2/
- **SIT Testing**: https://sandbox.vnpayment.vn/vnpaygw-sit-testing/user/login
- **Email hỗ trợ**: phamvanminh150204@gmail.com

---

## 🚀 HÀNH ĐỘNG NGAY BÂY GIỜ

1. ✅ **Đọc kỹ document này** - ĐÃY LÀ CƠ HỘI CUỐI CÙNG
2. ⏳ **Tạo VnPayService mới** - Implement từ đầu theo spec
3. ⏳ **Test từng bước** - Không skip bất kỳ bước nào
4. ⏳ **Verify với VNPay sandbox** - Test thật với sandbox
5. ⏳ **Deploy production** - Go live

---

> **⚠️ GHI NHỚ: ĐÂY LÀ CƠ HỘI CUỐI CÙNG. THỰC HIỆN CHÍNH XÁC 100% THEO TÀI LIỆU VNPAY.**
