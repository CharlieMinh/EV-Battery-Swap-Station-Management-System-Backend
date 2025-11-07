1. Luồng 1: Mua GÓI (Subscription) bằng Tiền mặt
Luồng này đơn giản, chỉ xử lý việc thanh toán, không liên quan đến lịch hẹn.
Khởi tạo: User chọn gói và chọn "Thanh toán Tiền mặt". Hệ thống tạo ra một Payment (với type = 'Subscription', method = 'Cash', status = 'Pending').
Xác nhận (Tại trạm): Staff tìm Payment này và xác nhận đã thu tiền. Payment.Status chuyển thành Completed. UserSubscription được kích hoạt.
Tự động hủy (Timeout):
Rule: Nếu Payment này vẫn Pending quá 72 giờ (3 ngày) kể từ lúc tạo.
Hành động (BE): Một "Scheduled Job" (chạy hàng đêm) sẽ tự động tìm và chuyển Payment.Status = 'Cancelled'.
Hậu quả: Không có hình phạt. User muốn mua thì phải tạo lại giao dịch mới.

--------------------------------------------------------------------------------------

2. Luồng 2: Đặt LỊCH ĐỔI PIN (Reservation)
Đây là luồng nghiệp vụ cốt lõi, xử lý cả 2 hình thức thanh toán (Gói hoặc Tiền mặt).

**QUAN TRỌNG - SPAM PROTECTION:**
- BE có ràng buộc: **User chỉ được có TỐI ĐA 1 reservation đang hoạt động** (status = Pending hoặc CheckedIn).
- Nếu user đã có reservation active, phải hủy hoặc hoàn thành trước khi đặt lịch mới.
- Logic này đảm bảo user không thể spam reservations.

Bước 1: User Đặt lịch
Khi user chọn slot, xe và nhấn "Xác nhận", hệ thống sẽ rẽ nhánh:

Kịch bản A: User chọn thanh toán bằng GÓI (Logic "Deferred Quota Deduction")
Kiểm tra quota: BE kiểm tra user còn quota (CurrentMonthSwapCount < swapsLimit).
Tạo Reservation: Nếu còn quota, BE tạo Reservation với UserSubscriptionId.
**QUAN TRỌNG:** CurrentMonthSwapCount **CHƯA bị trừ** tại thời điểm này.
Lý do: User có thể bận/quên đến trạm. Chỉ trừ quota khi user thực sự đổi pin (công bằng).
Spam protection: User chỉ có thể đặt 1 lịch tại một thời điểm (validation "1 active reservation").
Cảnh báo (FE): Giao diện nên hiển thị: "Lượt đổi pin sẽ được trừ khi bạn hoàn thành đổi pin tại trạm. Bạn có thể hủy lịch bất cứ lúc nào mà không mất lượt."

Kịch bản B: User chọn thanh toán bằng TIỀN MẶT (Pay-per-Swap)
Kiểm tra vi phạm: BE kiểm tra User.NoShowCount (số lần vi phạm: hủy muộn + no-show).
Rule: Nếu User.NoShowCount >= 3 → Trả về lỗi 403 (Forbidden), cấm user chọn thanh toán tiền mặt. Yêu cầu họ thanh toán online (VNPay).
Tạo thanh toán: Nếu NoShowCount < 3, BE tạo:
Reservation (với ReservationId)
Payment (với type = 'PayPerSwap', method = 'Cash', status = 'Pending', và reservationId được liên kết)
Spam protection: User chỉ có thể đặt 1 lịch tại một thời điểm.

Bước 2: User đến trạm và hoàn thành đổi pin
Khi user check-in tại trạm và staff xác nhận đổi pin thành công:
Gói: CurrentMonthSwapCount++ (trừ quota SAU KHI đổi pin thành công)
Tiền mặt: Payment.Status = Completed (staff xác nhận đã thu tiền)
Reservation.Status = Completed

Bước 3: Xử lý Hủy lịch
Kịch bản C: User Tự Hủy lịch
Kiểm tra thời gian: BE so sánh thời gian hủy với Reservation.SlotDate + SlotStartTime.
Nếu Hủy Sớm (trước giờ hẹn > 1 giờ):
Reservation.Status = Cancelled.
Gói: KHÔNG cần hoàn quota (vì chưa trừ).
Tiền mặt: Payment.Status = Cancelled.
Hậu quả: Không có hình phạt. User có thể đặt lịch mới.
Nếu Hủy Muộn (trong vòng ≤ 1 giờ trước giờ hẹn):
Reservation.Status = Cancelled.
Gói: KHÔNG cần hoàn quota (vì chưa trừ), NHƯNG user bị ghi nhận vi phạm.
Tiền mặt: Payment.Status = Cancelled VÀ áp dụng phạt (User.NoShowCount++).
Hậu quả: User bị tăng vi phạm. Nếu NoShowCount >= 3 → Bị chặn thanh toán tiền mặt.
Cảnh báo (FE): Khi user bấm hủy trong vòng 1 giờ, Modal xác nhận phải hiển thị rõ: "Hủy sát giờ sẽ bị tính 1 lần vi phạm. Nếu bạn vi phạm 3 lần, bạn sẽ không thể thanh toán bằng tiền mặt."
Kịch bản D: Staff Hủy lịch (Lỗi do trạm)
Hành động: Reservation.Status = Cancelled (với CancelReason ghi rõ do staff).
Hậu quả:
Gói: KHÔNG cần hoàn quota (vì chưa trừ). User có thể đặt lịch mới ngay.
Tiền mặt: Payment.Status = Cancelled.
Hậu quả: User KHÔNG bị hình phạt (NoShowCount không tăng).
Bước 4: Xử lý Không đến (No-Show)
Rule: Một "Scheduled Job" của BE (SlotReservationBackgroundService) sẽ chạy mỗi 5 phút để quét các Reservation vẫn Pending sau khi đã qua SlotEndTime + 15 phút.
Hành động:
Chuyển Reservation.Status = 'Expired' (với CancelReason = NoShow).
Hậu quả:
Gói: KHÔNG cần hoàn quota (vì chưa trừ). User có thể đặt lịch mới. NHƯNG user bị ghi nhận vi phạm (để tracking trong tương lai nếu cần).
Tiền mặt: Payment.Status = Cancelled VÀ áp dụng phạt (User.NoShowCount++).
Hệ quả lâu dài: Nếu User.NoShowCount >= 3 → Bị chặn thanh toán tiền mặt trong các lần đặt lịch tiếp theo.

