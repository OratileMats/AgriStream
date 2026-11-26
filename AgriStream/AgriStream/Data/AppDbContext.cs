using AgriStream.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AgriStream.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users => Set<User>();
        public DbSet<Device> Devices => Set<Device>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>().HasIndex(u => u.Username).IsUnique();
        }
    }

    public static class DbSeeder
    {
        public static async Task SeedAsync(AppDbContext db, IConfiguration config)
        {
            await db.Database.EnsureCreatedAsync();

            var hasher = new PasswordHasher<User>();

            if (!await db.Users.AnyAsync())
            {
                var adminPassword = config["Seed:AdminPassword"] ?? "Admin@12345";
                User Make(string name, string pw, string role)
                {
                    var u = new User { Username = name, Role = role };
                    u.PasswordHash = hasher.HashPassword(u, pw);
                    return u;
                }

                db.Users.AddRange(
                    Make("admin", adminPassword, Roles.Admin),
                    Make("farmer1", "Farmer@12345", Roles.Farmer),
                    Make("agro1", "Agro@12345", Roles.Agronomist));
            }

            if (!await db.Devices.AnyAsync())
            {
                db.Devices.AddRange(
                    new Device { DeviceId = "SEN001", Name = "Soil Sensor 1", FarmName = "Farm A" },
                    new Device { DeviceId = "SEN002", Name = "Soil Sensor 2", FarmName = "Farm A" },
                    new Device { DeviceId = "SEN003", Name = "Soil Sensor 3", FarmName = "Farm B" },
                    new Device { DeviceId = "SEN004", Name = "Soil Sensor 4", FarmName = "Farm B" },
                    new Device { DeviceId = "SEN005", Name = "Soil Sensor 5", FarmName = "Farm C" },
                    new Device { DeviceId = "SEN006", Name = "Soil Sensor 6", FarmName = "Farm C" });
            }

            await db.SaveChangesAsync();
        }
    }
}
