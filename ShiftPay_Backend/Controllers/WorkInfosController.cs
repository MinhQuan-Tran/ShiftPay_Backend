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

		// POST adds data for import/sync. PUT applies explicit edits and removals.
		[HttpPost]
		public async Task<ActionResult<WorkInfoDTO>> PostWorkInfo(WorkInfoDTO? workInfoDto)
		{
			var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
			if (string.IsNullOrEmpty(userId)) return Unauthorized("User ID is missing.");
			if (workInfoDto == null) return BadRequest("Work info is required.");

			WorkInfo incoming;
			try
			{
				incoming = WorkInfo.FromDTO(workInfoDto, userId);
			}
			catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
			{
				return BadRequest(ex.Message);
			}

			WorkInfo? workInfo = null;
			if (workInfoDto.Id.HasValue && workInfoDto.Id.Value != Guid.Empty)
			{
				workInfo = await _context.WorkInfos.FindAsync(workInfoDto.Id.Value, userId);
			}

			// Offline records may have a different ID from this user's stored workplace.
			workInfo ??= await _context.WorkInfos.WithPartitionKey(userId)
				.FirstOrDefaultAsync(item => item.Workplace == incoming.Workplace);
			var isNew = workInfo == null;
			if (isNew)
			{
				workInfo = incoming;
				_context.WorkInfos.Add(workInfo);
			}
			else
			{
				try
				{
					workInfo!.MergeFrom(workInfoDto);
				}
				catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
				{
					return BadRequest(ex.Message);
				}
			}

			try
			{
				await _context.SaveChangesAsync();
			}
			catch (DbUpdateException ex)
			{
				_logger.LogWarning(ex, "Failed to save work info for user {UserId}", userId);
				return Conflict("Failed to save work info.");
			}

			return isNew
				? CreatedAtAction(nameof(GetWorkInfo), new { id = workInfo!.Id }, workInfo.ToDTO())
				: Ok(workInfo!.ToDTO());
		}

		[HttpPut("{id:guid}")]
		public async Task<ActionResult<WorkInfoDTO>> PutWorkInfo(Guid id, WorkInfoDTO? workInfoDto)
		{
			if (id == Guid.Empty) return BadRequest("Id cannot be empty.");
			var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
			if (string.IsNullOrEmpty(userId)) return Unauthorized("User ID is missing.");
			if (workInfoDto == null) return BadRequest("Work info is required.");
			if (workInfoDto.Id.HasValue && workInfoDto.Id != id)
			{
				return BadRequest("Route id and payload id do not match.");
			}

			var workInfo = await _context.WorkInfos.FindAsync(id, userId);
			if (workInfo == null) return NotFound();

			try
			{
				workInfo.ReplaceProfiles(workInfoDto);
			}
			catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
			{
				return BadRequest(ex.Message);
			}

			try
			{
				await _context.SaveChangesAsync();
			}
			catch (DbUpdateException ex)
			{
				_logger.LogWarning(ex, "Failed to update work info {WorkInfoId} for user {UserId}", id, userId);
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

				workInfo.RemoveLegacyPayRate(payRate.Value);
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
