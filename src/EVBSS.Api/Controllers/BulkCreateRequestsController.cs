using EVBSS.Api.Data;
using EVBSS.Api.Dtos.BatteryUnits;
using EVBSS.Api.Dtos.BulkCreate;
using EVBSS.Api.Hubs;
using EVBSS.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace EVBSS.Api.Controllers
{
    [ApiController]
    [Route("api/bulk-create-requests")]
    [Authorize]
    public class BulkCreateRequestsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<BulkCreateRequestsController> _logger;
        private readonly IHubContext<NotificationHub> _hubContext;

        public BulkCreateRequestsController(AppDbContext context, ILogger<BulkCreateRequestsController> logger, IHubContext<NotificationHub> hubContext)
        {
            _context = context;
            _logger = logger;
            _hubContext = hubContext;
        }

        private Guid GetCurrentUserId()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                throw new InvalidOperationException("User ID not found in token.");
            }
            return new Guid(userId);
        }

        /// <summary>
        /// Admin sends a request to bulk create batteries.
        /// </summary>
        [HttpPost("request")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> RequestBulkCreate(BulkCreateBatteryUnitsDto dto)
        {
            var station = await _context.Stations.FindAsync(dto.StationId);
            if (station == null)
            {
                return BadRequest(new { Message = "Station not found." });
            }

            var batteryModel = await _context.BatteryModels.FindAsync(dto.BatteryModelId);
            if (batteryModel == null)
            {
                return BadRequest(new { Message = "Battery model not found." });
            }

            var adminId = GetCurrentUserId();

            var newRequest = new BulkCreateRequest
            {
                StationId = dto.StationId,
                BatteryModelId = dto.BatteryModelId,
                Quantity = dto.Quantity,
                Status = RequestStatus.PendingConfirmation,
                RequestedByAdminId = adminId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.BulkCreateRequests.Add(newRequest);

            // START: Added logic for staff notification
            var staffInStation = await _context.Users
                .Where(u => u.StationId == dto.StationId && u.Role == Role.Staff)
                .ToListAsync();

            if (staffInStation.Any())
            {
                var adminUser = await _context.Users.FindAsync(adminId);
                var adminIdentifier = !string.IsNullOrEmpty(adminUser?.Name) ? adminUser.Name : adminUser?.Email;
                var notificationMessage = $"New bulk create request for {dto.Quantity} batteries from admin {adminIdentifier} is awaiting confirmation.";

                foreach (var staff in staffInStation)
                {
                    _context.Notifications.Add(new Notification
                    {
                        UserId = staff.Id,
                        SenderId = adminId,
                        Message = notificationMessage,
                        Type = NotificationType.NewBulkRequest,
                        RelatedEntityId = newRequest.Id
                    });
                }
            }
            // END: Added logic

            await _context.SaveChangesAsync();

            // NOTIFICATION STEP: Send notification to staff at that station
            // The group name is a convention, e.g., "Station_{StationId}"
            await _hubContext.Clients.Group($"Station_{dto.StationId}").SendAsync("NewBulkRequest", newRequest);
            _logger.LogInformation("New bulk create request {RequestId} created by Admin {AdminId} for Station {StationId}", newRequest.Id, adminId, dto.StationId);

            return Ok(new { Message = "Request has been sent to the station. Awaiting confirmation from staff.", RequestId = newRequest.Id });
        }

        /// <summary>
        /// [Admin] Gets a list of all bulk create requests.
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllRequests()
        {
            var requests = await _context.BulkCreateRequests
                .Include(r => r.RequestedByAdmin)
                .Include(r => r.HandledByStaff)
                .Include(r => r.Station)
                .Include(r => r.BatteryModel)
                .Select(r => new 
                {
                    r.Id,
                    r.StationId,
                    StationName = r.Station.Name,
                    r.BatteryModelId,
                    BatteryModelName = r.BatteryModel.Name,
                    r.Quantity,
                    r.Status,
                    r.RequestedByAdminId,
                    RequestedByAdminName = r.RequestedByAdmin.Name,
                    r.HandledByStaffId,
                    HandledByStaffName = r.HandledByStaff.Name,
                    r.StaffNotes,
                    r.CreatedAt,
                    r.UpdatedAt
                })
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();
            
            return Ok(requests);
        }

        /// <summary>
        /// Staff gets a list of pending confirmation requests for their station.
        /// </summary>
        [HttpGet("pending")]
        [Authorize(Roles = "Staff")]
        public async Task<IActionResult> GetPendingRequests()
        {
            var staffId = GetCurrentUserId();
            var staffUser = await _context.Users.FindAsync(staffId);

            if (staffUser?.StationId == null)
            {
                return BadRequest(new { Message = "Staff is not assigned to any station." });
            }

            var requests = await _context.BulkCreateRequests
                .Where(r => r.Status == RequestStatus.PendingConfirmation && r.StationId == staffUser.StationId)
                .Include(r => r.RequestedByAdmin)
                .Include(r => r.BatteryModel)
                .Select(r => new 
                {
                    r.Id,
                    r.StationId,
                    r.BatteryModelId,
                    BatteryModelName = r.BatteryModel.Name,
                    r.Quantity,
                    r.Status,
                    r.RequestedByAdminId,
                    RequestedByAdminName = r.RequestedByAdmin.Name,
                    r.CreatedAt
                })
                .ToListAsync();
            
            return Ok(requests);
        }

        /// <summary>
        /// Staff confirms a battery creation request.
        /// </summary>
        [HttpPost("{id}/confirm")]
        [Authorize(Roles = "Staff")]
        public async Task<IActionResult> ConfirmRequest(Guid id, [FromBody] ConfirmRequestDto dto)
        {
            var staffId = GetCurrentUserId();
            var staffUser = await _context.Users.FindAsync(staffId);
            if (staffUser == null)
            {
                return Unauthorized("Staff user not found.");
            }

            if (staffUser.StationId == null)
            {
                return Forbid("Staff is not assigned to a station.");
            }

            var request = await _context.BulkCreateRequests.FindAsync(id);
            if (request == null || request.Status != RequestStatus.PendingConfirmation)
            {
                return BadRequest(new { Message = "Request is invalid or has already been processed." });
            }

            if (request.StationId != staffUser.StationId)
            {
                return Forbid("Staff can only confirm requests for their own station.");
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var batteryModel = await _context.BatteryModels.FindAsync(request.BatteryModelId);
                if (batteryModel == null) return BadRequest(new { Message = "Battery model not found" });

                var prefix = new string(batteryModel.Name.Take(3).ToArray()).ToUpper();
                var searchPrefix = $"{prefix}-";

                var lastBattery = await _context.BatteryUnits
                    .Where(b => b.Serial.StartsWith(searchPrefix))
                    .OrderByDescending(b => b.Serial)
                    .FirstOrDefaultAsync();

                int nextNumber = 1;
                if (lastBattery != null)
                {
                    var lastNumberStr = lastBattery.Serial.Split('-').Last();
                    if (int.TryParse(lastNumberStr, out int lastNumber))
                    {
                        nextNumber = lastNumber + 1;
                    }
                }

                var newBatteryUnits = new List<BatteryUnit>();
                for (int i = 0; i < request.Quantity; i++)
                {
                    var newSerial = $"{prefix}-{(nextNumber + i):D3}";
                    newBatteryUnits.Add(new BatteryUnit
                    {
                        Serial = newSerial,
                        BatteryModelId = request.BatteryModelId,
                        StationId = request.StationId,
                        Status = BatteryStatus.Full,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
                
                var generatedSerials = newBatteryUnits.Select(b => b.Serial).ToList();
                var existingSerials = await _context.BatteryUnits.Where(b => generatedSerials.Contains(b.Serial)).Select(b => b.Serial).ToListAsync();
                if (existingSerials.Any())
                {
                    return Conflict(new { Message = $"Could not generate unique serial numbers. The following already exist or were duplicated: {string.Join(", ", existingSerials)}. Please reject this request and ask Admin to create a new one." });
                }

                _context.BatteryUnits.AddRange(newBatteryUnits);

                var inventory = await _context.BatteryInventories.FirstOrDefaultAsync(i => i.StationId == request.StationId && i.BatteryModelId == request.BatteryModelId);
                if (inventory == null)
                {
                    _context.BatteryInventories.Add(new BatteryInventory
                    {
                        StationId = request.StationId,
                        BatteryModelId = request.BatteryModelId,
                        Quantity = request.Quantity,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
                else
                {
                    inventory.Quantity += request.Quantity;
                    inventory.UpdatedAt = DateTime.UtcNow;
                }

                request.Status = RequestStatus.Confirmed;
                request.HandledByStaffId = staffId;
                request.StaffNotes = dto.Notes; // Save the notes
                request.UpdatedAt = DateTime.UtcNow;

                var admins = await _context.Users.Where(u => u.Role == Role.Admin).ToListAsync();
                var staffIdentifier = !string.IsNullOrEmpty(staffUser.Name) ? staffUser.Name : staffUser.Email;
                var notificationMessage = $"Request {request.Id} confirmed by {staffIdentifier}.";
                if (!string.IsNullOrWhiteSpace(dto.Notes))
                {
                    notificationMessage += $" Note: {dto.Notes}";
                }

                foreach (var admin in admins)
                {
                    _context.Notifications.Add(new Notification
                    {
                        UserId = admin.Id,
                        SenderId = staffId,
                        Message = notificationMessage,
                        Type = NotificationType.BulkRequestConfirmed,
                        RelatedEntityId = request.Id
                    });
                }

                await _context.SaveChangesAsync(); // Save changes before committing transaction
                await transaction.CommitAsync();

                var notificationPayload = new
                {
                    Message = notificationMessage,
                    RequestId = request.Id,
                    StationId = request.StationId,
                    ConfirmedByStaffId = staffId,
                    AdminRequesterId = request.RequestedByAdminId,
                    Notes = dto.Notes
                };
                await _hubContext.Clients.Group("Admins").SendAsync("BulkRequestConfirmed", notificationPayload);

                _logger.LogInformation("Bulk create request {RequestId} confirmed by Staff {StaffId}. {Quantity} batteries created.", request.Id, staffId, request.Quantity);

                return Ok(new { Message = "Confirmation successful. Batteries have been added to the system." });
            }
            catch (DbUpdateException ex)
            {
                await transaction.RollbackAsync();
                // Check for unique constraint violation, which likely indicates a race condition
                if (ex.InnerException != null && (ex.InnerException.Message.Contains("Cannot insert duplicate key") || ex.InnerException.Message.Contains("UNIQUE constraint failed")))
                {
                    _logger.LogWarning(ex, "Conflict during bulk create confirmation for request {RequestId}. This is likely a race condition.", id);
                    return Conflict(new { Message = "A conflict occurred while generating battery serial numbers, possibly due to a simultaneous operation. Please reject this request and ask Admin to create a new one to avoid serial number gaps." });
                }
                
                _logger.LogError(ex, "A database update error occurred while confirming bulk create request {RequestId}.", id);
                return StatusCode(500, new { Message = "A database error occurred. Please try again." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "An unexpected error occurred while confirming bulk create request {RequestId}.", id);
                return StatusCode(500, new { Message = "An unexpected error occurred. Please try again." });
            }
        }

        /// <summary>
        /// Staff rejects a battery creation request.
        /// </summary>
        [HttpPost("{id}/reject")]
        [Authorize(Roles = "Staff")]
        public async Task<IActionResult> RejectRequest(Guid id, [FromBody] RejectRequestDto dto)
        {
            var staffId = GetCurrentUserId();
            var staffUser = await _context.Users.FindAsync(staffId);
            if (staffUser?.StationId == null)
            {
                return Forbid("Staff is not assigned to a station.");
            }

            var request = await _context.BulkCreateRequests.FindAsync(id);
            if (request == null || request.Status != RequestStatus.PendingConfirmation)
            {
                return BadRequest(new { Message = "Request is invalid or has already been processed." });
            }
            
            if (request.StationId != staffUser.StationId)
            {
                return Forbid("Staff can only reject requests for their own station.");
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                request.Status = RequestStatus.Rejected;
                request.HandledByStaffId = staffId;
                request.StaffNotes = dto.Notes; // Save the notes
                request.UpdatedAt = DateTime.UtcNow;

                var admins = await _context.Users.Where(u => u.Role == Role.Admin).ToListAsync();
                var staffIdentifier = !string.IsNullOrEmpty(staffUser.Name) ? staffUser.Name : staffUser.Email;
                var notificationMessage = $"Bulk create request {request.Id} for station {request.StationId} was REJECTED by staff {staffIdentifier}. Reason: {dto.Notes}";
                foreach (var admin in admins)
                {
                    _context.Notifications.Add(new Notification
                    {
                        UserId = admin.Id,
                        SenderId = staffId,
                        Message = notificationMessage,
                        Type = NotificationType.BulkRequestRejected,
                        RelatedEntityId = request.Id
                    });
                }

                await _context.SaveChangesAsync(); // Save changes before committing transaction
                await transaction.CommitAsync();

                var notificationPayload = new
                {
                    Message = notificationMessage,
                    RequestId = request.Id,
                    StationId = request.StationId,
                    RejectedByStaffId = staffId,
                    AdminRequesterId = request.RequestedByAdminId,
                    Notes = dto.Notes
                };
                await _hubContext.Clients.Group("Admins").SendAsync("BulkRequestRejected", notificationPayload);

                _logger.LogInformation("Bulk create request {RequestId} rejected by Staff {StaffId}. Reason: {Notes}", request.Id, staffId, dto.Notes);

                return Ok(new { Message = "Request has been rejected." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error rejecting bulk create request {RequestId}", id);
                return StatusCode(500, new { Message = "An internal error occurred while rejecting the request." });
            }
        }
    }
}