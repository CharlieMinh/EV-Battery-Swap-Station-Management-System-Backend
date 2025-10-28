using System;
using System.Threading.Tasks;
using Xunit;
using Microsoft.EntityFrameworkCore;
using EVBSS.Api.Data;
using EVBSS.Api.Models;
using EVBSS.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using EVBSS.Api.Dtos.Complaints;

namespace EVBSS.Api.Tests
{
    public class BatteryComplaintServiceTests
    {
        private AppDbContext CreateContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .Options;
            return new AppDbContext(options);
        }

        [Fact]
        public async Task ReportFaultyBattery_CreatesComplaint_HappyPath()
        {
            var context = CreateContext("ReportHappyPath");

            // Seed minimal data: BatteryModel, Station, BatteryUnit, User, SwapTransaction
            var model = new BatteryModel { Id = Guid.NewGuid(), Name = "VF-Test", SwapPricePerSession = 100 }; 
            var station = new Station { Id = Guid.NewGuid(), Name = "Station A", City = "HCM" };
            var battery = new BatteryUnit { Id = Guid.NewGuid(), Serial = "SN-1", BatteryModelId = model.Id, StationId = station.Id, Status = BatteryStatus.Full };
            var user = new User { Id = Guid.NewGuid(), Email = "driver@example.com", Name = "Driver", Role = Role.Driver };
            var swap = new SwapTransaction { Id = Guid.NewGuid(), UserId = user.Id, IssuedBatteryId = battery.Id, IssuedBattery = battery, StationId = station.Id, TransactionNumber = "T1" };

            context.BatteryModels.Add(model);
            context.Stations.Add(station);
            context.BatteryUnits.Add(battery);
            context.Users.Add(user);
            context.SwapTransactions.Add(swap);
            await context.SaveChangesAsync();

            var inventoryMock = new Mock<IBatteryInventoryService>();
            var logger = new NullLogger<BatteryComplaintService>();
            var service = new BatteryComplaintService(context, logger, inventoryMock.Object);

            var request = new ReportFaultyBatteryRequest { SwapTransactionId = swap.Id, ComplaintDetails = "Pin bị lỗi" };

            var complaint = await service.ReportFaultyBatteryAsync(user.Id, request);

            Assert.NotNull(complaint);
            Assert.Equal(request.SwapTransactionId, complaint.SwapTransactionId);
            Assert.Equal(user.Id, complaint.ReportedByUserId);
            Assert.Equal(ComplaintStatus.Pending, complaint.Status);
        }

        [Fact]
        public async Task ReportFaultyBattery_Throws_WhenDuplicateComplaint()
        {
            var context = CreateContext("ReportDuplicate");

            var model = new BatteryModel { Id = Guid.NewGuid(), Name = "VF-Test", SwapPricePerSession = 100 };
            var station = new Station { Id = Guid.NewGuid(), Name = "Station A", City = "HCM" };
            var battery = new BatteryUnit { Id = Guid.NewGuid(), Serial = "SN-1", BatteryModelId = model.Id, StationId = station.Id, Status = BatteryStatus.Full };
            var user = new User { Id = Guid.NewGuid(), Email = "driver@example.com", Name = "Driver", Role = Role.Driver };
            var swap = new SwapTransaction { Id = Guid.NewGuid(), UserId = user.Id, IssuedBatteryId = battery.Id, IssuedBattery = battery, StationId = station.Id, TransactionNumber = "T1" };

            context.BatteryModels.Add(model);
            context.Stations.Add(station);
            context.BatteryUnits.Add(battery);
            context.Users.Add(user);
            context.SwapTransactions.Add(swap);
            context.BatteryComplaints.Add(new BatteryComplaint { Id = Guid.NewGuid(), SwapTransactionId = swap.Id, IssuedBatteryId = battery.Id, ReportedByUserId = user.Id, ComplaintDetails = "Existing", Status = ComplaintStatus.Pending });
            await context.SaveChangesAsync();

            var inventoryMock = new Mock<IBatteryInventoryService>();
            var logger = new NullLogger<BatteryComplaintService>();
            var service = new BatteryComplaintService(context, logger, inventoryMock.Object);

            var request = new ReportFaultyBatteryRequest { SwapTransactionId = swap.Id, ComplaintDetails = "Pin bị lỗi" };

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReportFaultyBatteryAsync(user.Id, request));
        }
    }
}
