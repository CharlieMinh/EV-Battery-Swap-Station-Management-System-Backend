using EVBSS.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace EVBSS.Api.Data;

/// <summary>
/// Cửa vào DB của ứng dụng. Quản lý kết nối, track entity, và mapping bảng.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // Mỗi DbSet tương ứng một bảng
    public DbSet<Station> Stations => Set<Station>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // Ví dụ: thêm chỉ mục để sau này filter theo City/IsActive nhanh hơn
        b.Entity<Station>().HasIndex(s => new { s.City, s.IsActive });

        base.OnModelCreating(b);
    }
}
