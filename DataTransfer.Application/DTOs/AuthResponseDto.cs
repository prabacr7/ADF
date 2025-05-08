namespace DataTransfer.Application.DTOs
{
    public class AuthResponseDto
    {
        public bool Success { get; set; }
        public string? Token { get; set; }
        public string? UserName { get; set; }
        public string? Message { get; set; }
        public int? UserId { get; set; }
    }
} 