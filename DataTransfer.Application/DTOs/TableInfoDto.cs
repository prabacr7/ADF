using DataTransfer.Core.Entities;

namespace DataTransfer.Application.DTOs
{
    public class TableInfoDto
    {
        public string Schema { get; set; } = "dbo";
        public string Name { get; set; } = string.Empty;
        public string FullName => $"{Schema}.{Name}";
        public List<ColumnInfoDto> Columns { get; set; } = new List<ColumnInfoDto>();

        public static TableInfoDto FromEntity(TableInfo entity)
        {
            return new TableInfoDto
            {
                Schema = entity.Schema,
                Name = entity.Name,
                Columns = entity.Columns.Select(ColumnInfoDto.FromEntity).ToList()
            };
        }

        public static TableInfo ToEntity(TableInfoDto dto)
        {
            return new TableInfo
            {
                Schema = dto.Schema,
                Name = dto.Name,
                Columns = dto.Columns.Select(ColumnInfoDto.ToEntity).ToList()
            };
        }
    }

    public class ColumnInfoDto
    {
        public string Name { get; set; } = string.Empty;
        public string DataType { get; set; } = string.Empty;
        public bool IsNullable { get; set; }
        public bool IsPrimaryKey { get; set; }
        public int OrdinalPosition { get; set; }

        public static ColumnInfoDto FromEntity(ColumnInfo entity)
        {
            return new ColumnInfoDto
            {
                Name = entity.Name,
                DataType = entity.DataType,
                IsNullable = entity.IsNullable,
                IsPrimaryKey = entity.IsPrimaryKey,
                OrdinalPosition = entity.OrdinalPosition
            };
        }

        public static ColumnInfo ToEntity(ColumnInfoDto dto)
        {
            return new ColumnInfo
            {
                Name = dto.Name,
                DataType = dto.DataType,
                IsNullable = dto.IsNullable,
                IsPrimaryKey = dto.IsPrimaryKey,
                OrdinalPosition = dto.OrdinalPosition
            };
        }
    }
} 