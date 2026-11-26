using System.ComponentModel.DataAnnotations;

namespace AgriStream.Models
{
    public class Credentials
    {
        
        public string? UserId { get; set; }
        [Required]
        public string? Username { get; set; }
        [Required]
        public string? Password { get; set; }
    }
}
