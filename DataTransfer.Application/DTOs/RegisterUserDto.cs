using System.ComponentModel.DataAnnotations;

namespace DataTransfer.Application.DTOs
{
    public class RegisterUserDto
    {
        [Required]
        [StringLength(250)]
        public string Name { get; set; } = string.Empty;
        
        [Required]
        [StringLength(250)]
        public string UserName { get; set; } = string.Empty;
        
        [Required]
        [StringLength(250)]
        public string Password { get; set; } = string.Empty;
        
        [Required]
        [EmailAddress]
        [StringLength(250)]
        public string EmailAddress { get; set; } = string.Empty;
    }
} 