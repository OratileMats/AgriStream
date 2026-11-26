using System.ComponentModel.DataAnnotations;

namespace AgriStream.Models
{
    public static class Roles
    {
        public const string Farmer = "Farmer";
        public const string Agronomist = "Agronomist";
        public const string Admin = "Admin";
        public static readonly string[] All = { Farmer, Agronomist, Admin };
    }

    /// <summary>Row in the Azure SQL "Users" table (task b).</summary>
    public class User
    {
        [Key]
        public int UserId { get; set; }

        [Required, MaxLength(50)]
        public string Username { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [Required, MaxLength(20)]
        public string Role { get; set; } = Roles.Farmer;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Master device registry in Azure SQL. The queue-triggered function updates Status here.
    /// </summary>
    public class Device
    {
        [Key, MaxLength(20)]
        public string DeviceId { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(100)]
        public string FarmName { get; set; } = string.Empty;

        [MaxLength(30)]
        public string Status { get; set; } = "Idle";

        [MaxLength(200)]
        public string? LastCommand { get; set; }

        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }
}
