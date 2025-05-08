using DataTransfer.Application.DTOs;
using DataTransfer.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace DataTransfer.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TablesController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<TablesController> _logger;

        public TablesController(IMediator mediator, ILogger<TablesController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetTables([FromQuery] DatabaseConnectionDto connection, [FromQuery] bool isSource = true)
        {
            try
            {
                var query = new GetTablesQuery
                {
                    Connection = connection,
                    IsSource = isSource
                };

                var tables = await _mediator.Send(query);
                return Ok(tables);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting tables");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("columns")]
        public async Task<IActionResult> GetTableColumns([FromQuery] DatabaseConnectionDto connection, [FromQuery] string table, [FromQuery] bool isSource = true)
        {
            try
            {
                var query = new GetTableInfoQuery
                {
                    Connection = connection,
                    TableName = table,
                    IsSource = isSource
                };

                var tableInfo = await _mediator.Send(query);
                return Ok(tableInfo.Columns);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting columns for table {Table}", table);
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
} 