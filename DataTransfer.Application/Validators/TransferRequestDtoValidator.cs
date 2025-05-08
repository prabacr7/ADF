using DataTransfer.Application.DTOs;
using FluentValidation;

namespace DataTransfer.Application.Validators
{
    public class TransferRequestDtoValidator : AbstractValidator<TransferRequestDto>
    {
        public TransferRequestDtoValidator(DatabaseConnectionDtoValidator connectionValidator)
        {
            RuleFor(x => x.SourceConnection)
                .SetValidator(connectionValidator)
                .WithMessage("Invalid source connection");

            RuleFor(x => x.DestinationConnection)
                .SetValidator(connectionValidator)
                .WithMessage("Invalid destination connection");

            RuleFor(x => x.SourceTable)
                .NotEmpty()
                .WithMessage("Source table is required");

            RuleFor(x => x.DestinationTable)
                .NotEmpty()
                .WithMessage("Destination table is required");

            RuleFor(x => x.ColumnMappings)
                .NotEmpty()
                .WithMessage("At least one column mapping is required");

            RuleFor(x => x.ColumnMappings)
                .Must(mappings => mappings.Any(m => m.IsIncluded))
                .When(x => x.ColumnMappings.Any())
                .WithMessage("At least one column must be included in the transfer");

            RuleFor(x => x.BatchSize)
                .GreaterThan(0)
                .WithMessage("Batch size must be greater than 0");
        }
    }
} 