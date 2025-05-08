using DataTransfer.Core.Entities;
using DataTransfer.Core.Enums;

namespace DataTransfer.Application.DTOs
{
    public class TransferRequestDto
    {
        public DatabaseConnectionDto SourceConnection { get; set; } = new DatabaseConnectionDto();
        public DatabaseConnectionDto DestinationConnection { get; set; } = new DatabaseConnectionDto();
        public string SourceTable { get; set; } = string.Empty;
        public string DestinationTable { get; set; } = string.Empty;
        public List<ColumnMappingDto> ColumnMappings { get; set; } = new List<ColumnMappingDto>();
        public TransferMode TransferMode { get; set; } = TransferMode.TruncateAndInsert;
        public string? BeforeScript { get; set; }
        public string? AfterScript { get; set; }
        public int BatchSize { get; set; } = 1000;

        public static TransferRequestDto FromEntity(TransferRequest entity)
        {
            return new TransferRequestDto
            {
                SourceConnection = DatabaseConnectionDto.FromEntity(entity.SourceConnection),
                DestinationConnection = DatabaseConnectionDto.FromEntity(entity.DestinationConnection),
                SourceTable = entity.SourceTable,
                DestinationTable = entity.DestinationTable,
                ColumnMappings = entity.ColumnMappings.Select(ColumnMappingDto.FromEntity).ToList(),
                TransferMode = entity.TransferMode,
                BeforeScript = entity.BeforeScript,
                AfterScript = entity.AfterScript,
                BatchSize = entity.BatchSize
            };
        }

        public static TransferRequest ToEntity(TransferRequestDto dto)
        {
            return new TransferRequest
            {
                SourceConnection = DatabaseConnectionDto.ToEntity(dto.SourceConnection),
                DestinationConnection = DatabaseConnectionDto.ToEntity(dto.DestinationConnection),
                SourceTable = dto.SourceTable,
                DestinationTable = dto.DestinationTable,
                ColumnMappings = dto.ColumnMappings.Select(ColumnMappingDto.ToEntity).ToList(),
                TransferMode = dto.TransferMode,
                BeforeScript = dto.BeforeScript,
                AfterScript = dto.AfterScript,
                BatchSize = dto.BatchSize
            };
        }
    }

    public class ColumnMappingDto
    {
        public string SourceColumn { get; set; } = string.Empty;
        public string DestinationColumn { get; set; } = string.Empty;
        public bool IsIncluded { get; set; } = true;

        public static ColumnMappingDto FromEntity(ColumnMapping entity)
        {
            return new ColumnMappingDto
            {
                SourceColumn = entity.SourceColumn,
                DestinationColumn = entity.DestinationColumn,
                IsIncluded = entity.IsIncluded
            };
        }

        public static ColumnMapping ToEntity(ColumnMappingDto dto)
        {
            return new ColumnMapping
            {
                SourceColumn = dto.SourceColumn,
                DestinationColumn = dto.DestinationColumn,
                IsIncluded = dto.IsIncluded
            };
        }
    }
} 