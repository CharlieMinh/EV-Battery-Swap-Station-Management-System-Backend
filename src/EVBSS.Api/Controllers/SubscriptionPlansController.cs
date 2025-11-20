using EVBSS.Api.Data;
using EVBSS.Api.Models; // Cần để truy cập model SubscriptionPlan
using Microsoft.AspNetCore.Authorization; // Cần cho [Authorize]
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations; // Cần cho validation attributes

namespace EVBSS.Api.Controllers;

[ApiController]
[Route("api/v1/subscription-plans")]
public class SubscriptionPlansController : ControllerBase
{
    private readonly AppDbContext _db;

    public SubscriptionPlansController(AppDbContext db) => _db = db;

    // =========================================================================
    // READ ENDPOINTS (CÔNG KHAI)
    // =========================================================================

    /// <summary>
    /// Lấy tất cả các gói đăng ký đang hoạt động
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var plans = await _db.SubscriptionPlans
            .AsNoTracking()
            .Include(sp => sp.BatteryModel)
            .OrderBy(sp => sp.BatteryModel.Name).ThenBy(sp => sp.MonthlyPrice) // Sắp xếp theo loại pin, rồi đến giá
            .Select(sp => new
            {
                sp.Id,
                sp.Name,
                sp.Description,
                sp.MonthlyPrice,
                sp.MaxSwapsPerMonth,
                sp.Benefits,
                sp.RefundPolicy,
                BatteryModel = new
                {
                    sp.BatteryModel.Id,
                    sp.BatteryModel.Name,
                    sp.BatteryModel.Voltage,
                    sp.BatteryModel.CapacityWh
                },
                BillingCycleDays = 30,
                sp.CreatedAt
            })
            .ToListAsync();

        return Ok(plans);
    }

    /// <summary>
    /// Lấy thông tin một gói đăng ký theo ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var plan = await _db.SubscriptionPlans
            .AsNoTracking()
            .Include(sp => sp.BatteryModel)
            .Where(sp => sp.Id == id && sp.IsActive)
            .Select(sp => new
            {
                sp.Id,
                sp.Name,
                sp.Description,
                sp.MonthlyPrice,
                sp.MaxSwapsPerMonth,
                sp.Benefits,
                sp.RefundPolicy,
                BatteryModel = new
                {
                    sp.BatteryModel.Id,
                    sp.BatteryModel.Name,
                    sp.BatteryModel.Voltage,
                    sp.BatteryModel.CapacityWh
                },
                BillingCycleDays = 30,
                sp.CreatedAt
            })
            .FirstOrDefaultAsync();

        if (plan == null)
            return NotFound(new { error = new { code = "PLAN_NOT_FOUND", message = "Không tìm thấy gói đăng ký." } });

        return Ok(plan);
    }

    // =========================================================================
    // MANAGEMENT ENDPOINTS (CHỈ DÀNH CHO ADMIN)
    // =========================================================================

    /// <summary>
    /// [Admin] Tạo một gói đăng ký mới
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreatePlan([FromBody] CreateSubscriptionPlanDto createDto)
    {
        // Kiểm tra xem BatteryModelId có tồn tại không
        var batteryModelExists = await _db.BatteryModels.AnyAsync(bm => bm.Id == createDto.BatteryModelId);
        if (!batteryModelExists)
        {
            ModelState.AddModelError(nameof(createDto.BatteryModelId), "Loại pin không tồn tại.");
            return BadRequest(ModelState);
        }

        var newPlan = new SubscriptionPlan
        {
            Name = createDto.Name,
            Description = createDto.Description,
            MonthlyPrice = createDto.MonthlyPrice,
            MaxSwapsPerMonth = createDto.MaxSwapsPerMonth,
            Benefits = createDto.Benefits,
            RefundPolicy = createDto.RefundPolicy,
            BatteryModelId = createDto.BatteryModelId,
            IsActive = true // Mặc định là active khi tạo mới
        };

        _db.SubscriptionPlans.Add(newPlan);
        await _db.SaveChangesAsync();

        // Trả về gói đã tạo cùng với đường dẫn để truy cập nó
        return CreatedAtAction(nameof(GetById), new { id = newPlan.Id }, newPlan);
    }

    /// <summary>
    /// [Admin] Cập nhật thông tin một gói đăng ký
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdatePlan(Guid id, [FromBody] UpdateSubscriptionPlanDto updateDto)
    {
        // Cho phép null (unlimited) hoặc >= 1
        if (updateDto.MaxSwapsPerMonth.HasValue && updateDto.MaxSwapsPerMonth.Value < 1)
        {
            ModelState.AddModelError(nameof(updateDto.MaxSwapsPerMonth),
                "Số lần đổi phải lớn hơn 0 hoặc để trống (null) cho gói không giới hạn.");
            return BadRequest(ModelState);
        }

        var planToUpdate = await _db.SubscriptionPlans.FindAsync(id);
        if (planToUpdate == null)
        {
            return NotFound(new { error = new { code = "PLAN_NOT_FOUND", message = "Không tìm thấy gói đăng ký để cập nhật." } });
        }

        // Kiểm tra xem BatteryModelId mới có tồn tại không (nếu có thay đổi)
        if (planToUpdate.BatteryModelId != updateDto.BatteryModelId)
        {
            var batteryModelExists = await _db.BatteryModels.AnyAsync(bm => bm.Id == updateDto.BatteryModelId);
            if (!batteryModelExists)
            {
                ModelState.AddModelError(nameof(updateDto.BatteryModelId), "Loại pin không tồn tại.");
                return BadRequest(ModelState);
            }
        }

        // Cập nhật các trường
        planToUpdate.Name = updateDto.Name;
        planToUpdate.Description = updateDto.Description;
        planToUpdate.MonthlyPrice = updateDto.MonthlyPrice;
        planToUpdate.MaxSwapsPerMonth = updateDto.MaxSwapsPerMonth;
        planToUpdate.Benefits = updateDto.Benefits;
        planToUpdate.RefundPolicy = updateDto.RefundPolicy;
        planToUpdate.BatteryModelId = updateDto.BatteryModelId;
        planToUpdate.IsActive = updateDto.IsActive;
        planToUpdate.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return NoContent(); // HTTP 204: Thành công, không cần trả về nội dung
    }

    /// <summary>
    /// [Admin] Xóa mềm một gói đăng ký (chuyển IsActive = false)
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeletePlan(Guid id)
    {
        var planToDelete = await _db.SubscriptionPlans.FindAsync(id);
        if (planToDelete == null)
        {
            return NotFound(new { error = new { code = "PLAN_NOT_FOUND", message = "Không tìm thấy gói đăng ký để xóa." } });
        }

        if (!planToDelete.IsActive)
        {
            return NoContent(); // Đã bị vô hiệu hóa rồi, không cần làm gì thêm
        }

        planToDelete.IsActive = false; // Soft delete
        planToDelete.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return NoContent();
    }
}


// =========================================================================
// DTOs (DATA TRANSFER OBJECTS)
// =========================================================================

/// <summary>
/// Dữ liệu đầu vào để tạo một gói đăng ký mới
/// </summary>
public class CreateSubscriptionPlanDto
{
    [Required(ErrorMessage = "Tên gói không được để trống")]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mô tả không được để trống")]
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "Giá tháng không được để trống")]
    [Range(0.01, double.MaxValue, ErrorMessage = "Giá tháng phải lớn hơn 0")]
    public decimal MonthlyPrice { get; set; }

    // ⭐ FIX: Removed [Range] to allow null (unlimited plans)
    // Validation moved to controller methods
    public int? MaxSwapsPerMonth { get; set; } // null = unlimited, or >= 1

    [Required(ErrorMessage = "Quyền lợi không được để trống")]
    public string Benefits { get; set; } = string.Empty;

    [Required(ErrorMessage = "Chính sách hoàn tiền không được để trống")]
    public string RefundPolicy { get; set; } = string.Empty;

    [Required(ErrorMessage = "ID loại pin không được để trống")]
    public Guid BatteryModelId { get; set; }
}

/// <summary>
/// Dữ liệu đầu vào để cập nhật một gói đăng ký
/// </summary>
public class UpdateSubscriptionPlanDto
{
    [Required(ErrorMessage = "Tên gói không được để trống")]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mô tả không được để trống")]
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "Giá tháng không được để trống")]
    [Range(0.01, double.MaxValue, ErrorMessage = "Giá tháng phải lớn hơn 0")]
    public decimal MonthlyPrice { get; set; }

    // ⭐ FIX: Removed [Range] to allow null (unlimited plans)
    // Validation moved to controller methods
    public int? MaxSwapsPerMonth { get; set; } // null = unlimited, or >= 1

    [Required(ErrorMessage = "Quyền lợi không được để trống")]
    public string Benefits { get; set; } = string.Empty;

    [Required(ErrorMessage = "Chính sách hoàn tiền không được để trống")]
    public string RefundPolicy { get; set; } = string.Empty;

    [Required(ErrorMessage = "ID loại pin không được để trống")]
    public Guid BatteryModelId { get; set; }

    [Required]
    public bool IsActive { get; set; }
}