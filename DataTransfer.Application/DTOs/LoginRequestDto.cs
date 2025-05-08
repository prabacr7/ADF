using System.ComponentModel.DataAnnotations;

namespace DataTransfer.Application.DTOs
{
    public class LoginRequestDto
    {
        [Required]
        public string UserName { get; set; } = string.Empty;
        
        [Required]
        public string Password { get; set; } = string.Empty;
    }
} 