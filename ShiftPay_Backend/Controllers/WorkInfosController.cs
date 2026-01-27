using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftPay_Backend.Data;
using ShiftPay_Backend.Models;
using System.Security.Claims;

namespace ShiftPay_Backend.Controllers
{
	[Authorize]
	[Route("api/[controller]")]
	[ApiController]
	public class WorkInfosController : Controller
	{
		private readonly ILogger<WorkInfosController> _logger;
		private readonly ShiftPay_BackendContext _context;

		public WorkInfosController(ILogger<WorkInfosController> logger, ShiftPay_BackendContext context)
		{
			_logger = logger;
			_context = context;
		}

		// GET: api/WorkInfos
		[HttpGet]
		public async Task<ActionResult<IEnumerable<WorkInfoDTO>>> GetWorkInfos()
		{
			var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
			if (string.IsNullOrEmpty(userId))
			{
				return Unauthorized("User ID is missing.");
			}

			var workInfos = await _context.WorkInfos
				.WithPartitionKey(userId)
				.ToListAsync();

			return Ok(workInfos.Select(workInfo => workInfo.ToDTO()));
		}

		// GET: api/WorkInfos/{id}
		[HttpGet("{id:guid}")]
		public async Task<ActionResult<WorkInfoDTO>> GetWorkInfo(Guid id)
		{
			if (id == Guid.Empty)
			{
				return BadRequest("Id cannot be empty.");
			}

			var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
			if (string.IsNullOrEmpty(userId))
			{
				return Unauthorized("User ID is missing.");
			}

			// https://learn.microsoft.com/en-us/azure/cosmos-db/optimize-cost-reads-writes
			// https://learn.microsoft.com/en-us/ef/core/providers/cosmos/querying#findasync
			// Cosmos point-read: (id, partitionKey)
			var workInfo = await _context.WorkInfos.FindAsync(id, userId);

			if (workInfo == null)
			{
				return NotFound();
			}

			return Ok(workInfo.ToDTO());
		}

		// No PUT

		// POST: api/WorkInfos
		[HttpPost]
		public async Task<ActionResult<WorkInfoDTO>> PostWorkInfo(WorkInfoDTO workInfoDto)
		{
			var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
			if (string.IsNullOrEmpty(userId))
			{
				return Unauthorized("User ID is missing.");
			}

			WorkInfo? workInfo = null;
			if (workInfoDto.Id.HasValue && workInfoDto.Id.Value != Guid.Empty)
			{
				workInfo = await _context.WorkInfos.FindAsync(workInfoDto.Id.Value, userId);
			}

			if (workInfo == null)
			{
				try
				{
					workInfo = WorkInfo.FromDTO(workInfoDto, userId);
				}
				catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
				{
					return BadRequest(ex.Message);
				}

				_context.WorkInfos.Add(workInfo);
				try
				{
					await _context.SaveChangesAsync();
				}
				catch (DbUpdateException ex)
				{
					_logger.LogWarning(ex, "Failed to create work info for user {UserId}", userId);
					return Conflict("Failed to create work info.");
				}
				return CreatedAtAction(nameof(GetWorkInfo), new { id = workInfo.Id }, workInfo.ToDTO());
			}

			workInfo.Workplace = workInfoDto.Workplace;
			workInfo.PayRates = (workInfo.PayRates ?? [])
				.Union(workInfoDto.PayRates ?? [])
				.ToHashSet()
				.ToList();

			try
			{
				workInfo.Validate();
			}
			catch (InvalidOperationException ex)
			{
				return BadRequest(ex.Message);
			}

			try
			{
				await _context.SaveChangesAsync();
			}
			catch (DbUpdateException ex)
			{
				_logger.LogWarning(ex, "Failed to update work info {WorkInfoId} for user {UserId}", workInfo.Id, userId);
				return Conflict("Failed to update work info.");
			}

			return Ok(workInfo.ToDTO());
		}

		// DELETE: api/WorkInfos/{id}
		[HttpDelete("{id:guid}")]
		public async Task<IActionResult> DeleteWorkInfo(Guid id, decimal? payRate)
		{
			if (id == Guid.Empty)
			{
				return BadRequest("Id cannot be empty.");
			}

			var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
			if (string.IsNullOrEmpty(userId))
			{
				return Unauthorized("User ID is missing.");
			}

			// Cosmos point-read: (id, partitionKey)
			var workInfo = await _context.WorkInfos.FindAsync(id, userId);

			if (payRate.HasValue)
			{
				if (workInfo == null)
				{
					return NotFound();
				}

				if (workInfo.PayRates.Contains(payRate.Value))
				{
					workInfo.PayRates.Remove(payRate.Value);
				}

				// If not found, do nothing
			}
			else
			{
				if (workInfo != null)
				{
					_context.WorkInfos.Remove(workInfo);
				}
			}

			await _context.SaveChangesAsync();
			return NoContent();
		}
	}
}
