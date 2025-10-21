using EVBSS.Api.Data;
using EVBSS.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace EVBSS.Api.Data;

/// <summary>
/// Seed data cho VehicleModel (loại xe của hãng)
/// </summary>
public static class VehicleModelSeeder
{
    public static async Task SeedVehicleModelsAsync(AppDbContext context)
    {
        // Kiểm tra đã có data chưa
        if (await context.VehicleModels.AnyAsync())
            return;

        // Lấy battery models (giả sử đã có sẵn)
        var batteryVF3 = await context.BatteryModels.FirstOrDefaultAsync(b => b.Name.Contains("VF3"));
        var batteryVF5 = await context.BatteryModels.FirstOrDefaultAsync(b => b.Name.Contains("VF5"));
        var batteryVF8 = await context.BatteryModels.FirstOrDefaultAsync(b => b.Name.Contains("VF8"));
        var batteryVF9 = await context.BatteryModels.FirstOrDefaultAsync(b => b.Name.Contains("VF9"));

        // Nếu chưa có battery models, tạo mẫu
        if (batteryVF3 == null)
        {
            batteryVF3 = new BatteryModel
            {
                Name = "VF3 Battery Pack",
                Voltage = 400,
                CapacityWh = 30000, // 30 kWh
                Manufacturer = "VinFast"
            };
            context.BatteryModels.Add(batteryVF3);
        }

        if (batteryVF5 == null)
        {
            batteryVF5 = new BatteryModel
            {
                Name = "VF5 Battery Pack",
                Voltage = 400,  // Match database: 60V
                CapacityWh = 30000, // Match database: 30 kWh
                Manufacturer = "VinFast"
            };
            context.BatteryModels.Add(batteryVF5);
        }

        if (batteryVF8 == null)
        {
            batteryVF8 = new BatteryModel
            {
                Name = "VF8 Battery Pack",
                Voltage = 400,
                CapacityWh = 87700, // 87.7 kWh
                Manufacturer = "VinFast"
            };
            context.BatteryModels.Add(batteryVF8);
        }

        if (batteryVF9 == null)
        {
            batteryVF9 = new BatteryModel
            {
                Name = "VF9 Battery Pack",
                Voltage = 400,
                CapacityWh = 92000, // 92 kWh
                Manufacturer = "VinFast"
            };
            context.BatteryModels.Add(batteryVF9);
        }

        await context.SaveChangesAsync();

        // Tạo VehicleModels
        var vehicleModels = new List<VehicleModel>
        {
            new VehicleModel
            {
                Name = "VF3",
                FullName = "VinFast VF3",
                Brand = "VinFast",
                CompatibleBatteryModelId = batteryVF3.Id,
                ImageUrl = "https://example.com/images/vf3.jpg",
                Description = "Xe điện mini city, phù hợp di chuyển trong thành phố",
                IsActive = true
            },
            new VehicleModel
            {
                Name = "VF5",
                FullName = "VinFast VF5 Plus",
                Brand = "VinFast",
                CompatibleBatteryModelId = batteryVF5.Id,
                ImageUrl = "https://example.com/images/vf5.jpg",
                Description = "Crossover cỡ nhỏ, phù hợp gia đình trẻ",
                IsActive = true
            },
            new VehicleModel
            {
                Name = "VF8",
                FullName = "VinFast VF8",
                Brand = "VinFast",
                CompatibleBatteryModelId = batteryVF8.Id,
                ImageUrl = "https://example.com/images/vf8.jpg",
                Description = "SUV cỡ trung, mạnh mẽ và sang trọng",
                IsActive = true
            },
            new VehicleModel
            {
                Name = "VF9",
                FullName = "VinFast VF9",
                Brand = "VinFast",
                CompatibleBatteryModelId = batteryVF9.Id,
                ImageUrl = "https://example.com/images/vf9.jpg",
                Description = "SUV 3 hàng ghế, rộng rãi và tiện nghi",
                IsActive = true
            }
        };

        context.VehicleModels.AddRange(vehicleModels);
        await context.SaveChangesAsync();
    }
}
