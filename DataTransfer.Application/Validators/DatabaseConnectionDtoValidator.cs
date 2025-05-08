using DataTransfer.Application.DTOs;
using FluentValidation;

namespace DataTransfer.Application.Validators
{
    public class DatabaseConnectionDtoValidator : AbstractValidator<DatabaseConnectionDto>
    {
        public DatabaseConnectionDtoValidator()
        {
            RuleFor(x => x.ServerName)
                .NotEmpty()
                .WithMessage("Server name is required");

            RuleFor(x => x.DatabaseName)
                .NotEmpty()
                .WithMessage("Database name is required");

            // Since the DTO doesn't have authentication properties, we'll remove those rules
            // If authentication is needed, these properties should be added to the DTO first

            // Removing ConnectionTimeout validation as it doesn't exist in the DTO
        }
    }
} 