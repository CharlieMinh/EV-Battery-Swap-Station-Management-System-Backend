# 🔄 UPDATED FLOW IMPLEMENTATION - LOGIC "CÂN BẰNG"

**Ngày cập nhật:** November 7, 2025

## 📋 TÓM TẮT THAY ĐỔI

Hệ thống đã được cập nhật từ logic **"Deferred Quota Deduction"** (trừ quota sau swap) sang logic **"Immediate Deduction with Balanced Refund"** (trừ quota ngay, hoàn có điều kiện).

---

## 🎯 LUỒNG 1: MUA GÓI (SUBSCRIPTION) BẰNG TIỀN MẶT

**KHÔNG THAY ĐỔI** - Giữ nguyên như cũ:

1. ✅ Tạo `Payment` pending + `UserSubscription` inactive
2. ✅ Staff xác nhận → Kích hoạt gói
3. ✅ Auto-cancel sau 72h (Background Service)

---

## 🎯 LUỒNG 2: ĐẶT LỊCH ĐỔI PIN (RESERVATION)

### **THAY ĐỔI QUAN TRỌNG:**

| Hành động                 | Logic CŨ                 | Logic MỚI                                   | File đã sửa                           |
| ------------------------- | ------------------------ | ------------------------------------------- | ------------------------------------- |
| **Đặt lịch bằng GÓI**     | KHÔNG trừ quota          | ✅ **TRỪ NGAY** `CurrentMonthSwapCount++`   | `SlotReservationService.cs` line ~215 |
| **Hủy sớm (>1h) - GÓI**   | Không hoàn (vì chưa trừ) | ✅ **HOÀN QUOTA** `CurrentMonthSwapCount--` | `SlotReservationService.cs` line ~615 |
| **Hủy muộn (≤1h) - GÓI**  | Không hoàn               | ✅ **KHÔNG HOÀN** (user mất lượt)           | `SlotReservationService.cs` line ~625 |
| **Staff hủy - GÓI**       | Không hoàn               | ✅ **LUÔN HOÀN** `CurrentMonthSwapCount--`  | `SlotReservationService.cs` line ~615 |
| **No-show - GÓI**         | Không hoàn               | ✅ **KHÔNG HOÀN** (đã trừ từ đầu)           | `SlotReservationService.cs` line ~695 |
| **Swap thành công - GÓI** | ✅ Trừ quota `++`        | ✅ **KHÔNG LÀM GÌ** (đã trừ lúc đặt)        | `SwapTransactionService.cs` line ~175 |
| **Tiền mặt - Tất cả**     | Giữ nguyên               | ✅ **GIỮ NGUYÊN**                           | -                                     |

---

## 📁 FILES ĐÃ CHỈNH SỬA

### **BACKEND (4 files):**

1. **SlotReservationService.cs** (3 methods):

   - `CreateReservationAsync()` - Dòng ~215: **THÊM** trừ quota ngay
   - `CancelReservationAsync()` - Dòng ~600: **THÊM** logic hoàn quota có điều kiện
   - `ExpireOverdueReservationsAsync()` - Dòng ~690: **THÊM** log không hoàn quota

2. **SwapTransactionService.cs**:
   - `FinalizeSwapFromReservationAsync()` - Dòng ~175: **XÓA** logic trừ quota sau swap

### **FRONTEND (2 files):**

3. **BookingWizard.tsx**:

   - Dòng ~455: **THÊM** cảnh báo trừ quota ngay cho gói
   - Dòng ~475: **THÊM** cảnh báo vi phạm cho tiền mặt

4. **SwapStatus.tsx**:
   - Dòng ~95: **THÊM** function `checkCancellationTiming()`
   - Dòng ~185: **THÊM** UI hiển thị cảnh báo hủy sớm/muộn

---

## 🎨 FRONTEND - THÔNG BÁO CHO USER

### **1. Khi Đặt Lịch Bằng Gói:**

```
┌─────────────────────────────────────────────────┐
│ ℹ️ Sử dụng gói đăng ký:                        │
│                                                 │
│ • Lượt đổi pin sẽ được trừ ngay khi xác nhận   │
│ • Hủy lịch trước 1 giờ → Được hoàn lại lượt    │
│ • Hủy sát giờ hoặc no-show → Mất lượt          │
│ • Staff hủy → Luôn được hoàn                   │
└─────────────────────────────────────────────────┘
```

### **2. Khi Đặt Lịch Bằng Tiền Mặt:**

```
┌─────────────────────────────────────────────────┐
│ ⚠️ Lưu ý quan trọng:                           │
│                                                 │
│ • Hủy trong vòng 1h → +1 vi phạm               │
│ • Không đến (No-show) → +1 vi phạm             │
│ • Vi phạm 3 lần → Không được thanh toán tiền   │
└─────────────────────────────────────────────────┘
```

### **3. Khi Hủy Lịch (Hủy Sớm):**

```
┌─────────────────────────────────────────────────┐
│ ✓ Hủy sớm (>1h)                                │
│                                                 │
│ ✓ Hủy trước 1 giờ không bị phạt.               │
│ ✓ Gói sẽ được hoàn lại lượt.                   │
└─────────────────────────────────────────────────┘
```

### **4. Khi Hủy Lịch (Hủy Muộn):**

```
┌─────────────────────────────────────────────────┐
│ ⚠️ Hủy sát giờ (≤1h)                           │
│                                                 │
│ • Hủy sát giờ sẽ bị hình phạt:                 │
│   - Nếu đặt bằng Gói: Mất lượt (không hoàn)    │
│   - Nếu đặt bằng Tiền mặt: Tăng vi phạm +1     │
│   - Vi phạm 3 lần → Không được thanh toán tiền │
└─────────────────────────────────────────────────┘
```

---

## 🧪 BẢNG TEST CASES

| Kịch bản               | Hành động    | Quota Before | Quota After | Penalty | Note          |
| ---------------------- | ------------ | ------------ | ----------- | ------- | ------------- |
| **Gói: Đặt lịch**      | User book    | 0/10         | **1/10**    | -       | ✅ Trừ ngay   |
| **Gói: Hủy sớm**       | Cancel >1h   | 1/10         | **0/10**    | -       | ✅ Hoàn lại   |
| **Gói: Hủy muộn**      | Cancel ≤1h   | 1/10         | **1/10**    | -       | ❌ Không hoàn |
| **Gói: No-show**       | Not arrive   | 1/10         | **1/10**    | +1      | ❌ Không hoàn |
| **Gói: Staff hủy**     | Staff cancel | 1/10         | **0/10**    | -       | ✅ Luôn hoàn  |
| **Gói: Swap OK**       | Complete     | 1/10         | **1/10**    | -       | ✅ Giữ nguyên |
| **Tiền mặt: Hủy muộn** | Cancel ≤1h   | -            | -           | +1      | ❌ Vi phạm    |
| **Tiền mặt: No-show**  | Not arrive   | -            | -           | +1      | ❌ Vi phạm    |

---

## ✅ CHECKLIST HOÀN THÀNH

### **Backend:**

- [x] Trừ quota ngay khi đặt lịch (Gói)
- [x] Hoàn quota khi hủy sớm (Gói)
- [x] KHÔNG hoàn quota khi hủy muộn (Gói)
- [x] LUÔN hoàn quota khi Staff hủy (Gói)
- [x] KHÔNG hoàn quota cho no-show (Gói)
- [x] Xóa logic trừ quota sau swap (Gói)
- [x] Giữ nguyên logic penalty cho tiền mặt
- [x] Log đầy đủ cho tất cả cases

### **Frontend:**

- [x] Cảnh báo trừ quota ngay (BookingWizard)
- [x] Cảnh báo vi phạm tiền mặt (BookingWizard)
- [x] Hiển thị warning hủy sớm/muộn (SwapStatus)
- [x] Phân biệt rõ penalty cho từng case
- [x] UI responsive và user-friendly

---

## 📊 TỔNG KẾT

**Logic mới "Cân Bằng" đảm bảo:**

1. ✅ **Công bằng:** User biết trước hậu quả khi đặt/hủy lịch
2. ✅ **Minh bạch:** Quota được trừ ngay, không ẩn sau swap
3. ✅ **Linh hoạt:** Hủy sớm được hoàn, hủy muộn bị phạt
4. ✅ **Nhất quán:** Staff cancel luôn ân xá
5. ✅ **Phòng spam:** Penalty system cho tiền mặt vẫn hoạt động

**Status: IMPLEMENTATION COMPLETED** ✅

---

_Tài liệu này thay thế `COMPLETE_FLOW_IMPLEMENTATION_GUIDE.md` với logic cũ._
