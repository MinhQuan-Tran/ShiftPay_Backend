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
		[HttpGet("{id}")]
		public async Task<ActionResult<WorkInfoDTO>> GetWorkInfo(string id)
		{
			if (string.IsNullOrWhiteSpace(id))
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

			string id;
			try
			{
				id = WorkInfo.CreateId(workInfoDto.Workplace);
			}
			catch (ArgumentException ex)
			{
				return BadRequest(ex.Message);
			}


			// Cosmos point-read: (partitionKey, id)
			var workInfo = await _context.WorkInfos.FindAsync(id, userId);

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
				await _context.SaveChangesAsync();
				return CreatedAtAction(nameof(GetWorkInfo), new { id = workInfo.Id }, workInfo.ToDTO());
			}

			workInfo.PayRates = workInfo.PayRates.Union(workInfoDto.PayRates).ToHashSet().ToList();

			try
			{
				workInfo.Validate();
			}
			catch (InvalidOperationException ex)
			{
				return BadRequest(ex.Message);
			}

			await _context.SaveChangesAsync();

			return Ok(workInfo.ToDTO());
		}

		// DELETE: api/WorkInfos/{id}
		[HttpDelete("{id}")]
		public async Task<IActionResult> DeleteWorkInfo(string id, decimal? payRate)
		{
			if (string.IsNullOrWhiteSpace(id))
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
