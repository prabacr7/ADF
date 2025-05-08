using System.ComponentModel.DataAnnotations;

namespace DataTransfer.Application.DTOs
{
    public class QueryRequestDto
    {
        [Required]
        public string ServerName { get; set; } = string.Empty;

        [Required]
        public string Database { get; set; } = string.Empty;

        [Required]
        public string Query { get; set; } = string.Empty;

        public string Authentication { get; set; } = "windows";

        public string? UserName { get; set; }

        public string? Password { get; set; }

        public int? UserId { get; set; }

        public int? MaxRows { get; set; } = 1000;

        public bool TrustServerCertificate { get; set; } = true;
    }
} 