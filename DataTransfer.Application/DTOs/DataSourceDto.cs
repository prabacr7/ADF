using System;
using System.ComponentModel.DataAnnotations;

namespace DataTransfer.Application.DTOs
{
    public class DataSourceDto
    {
        public int? DataSourceId { get; set; }
        
        [Required(ErrorMessage = "Display name is required")]
        public string DatasourceName { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Server name is required")]
        public string ServerName { get; set; } = string.Empty;
        
        public string UserName { get; set; } = string.Empty;
        
        public string Password { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Authentication type is required")]
        public string AuthenticationType { get; set; } = string.Empty;
        
        public string DefaultDatabaseName { get; set; } = string.Empty;
        
        public int? UserId { get; set; }
        
        public DateTime? CreatedDate { get; set; }
        
        public bool IsActive { get; set; } = true;
    }
    
    public class TestConnectionDto
    {
        [Required(ErrorMessage = "Server name is required")]
        public string ServerName { get; set; } = string.Empty;
        
        public string UserName { get; set; } = string.Empty;
        
        public string Password { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Authentication type is required")]
        public string AuthenticationType { get; set; } = string.Empty;
        
        public string DefaultDatabaseName { get; set; } = string.Empty;
    }
} 