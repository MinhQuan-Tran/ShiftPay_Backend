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

			// Cosmos: WorkInfo partition key is (UserId, Workplace). Constrain to the UserId partition.
			var workInfos = await _context.WorkInfos
				.WithPartitionKey(userId)
				.ToListAsync();

			return Ok(workInfos.Select(workInfo => workInfo.ToDTO()));
		}

		// GET: api/WorkInfos/KFC
		[HttpGet("{workplace}")]
		public async Task<ActionResult<WorkInfoDTO>> GetWorkInfo(string workplace)
		{
			var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
			if (string.IsNullOrEmpty(userId))
			{
				return Unauthorized("User ID is missing.");
			}

			var workInfo = await _context.WorkInfos
				.WithPartitionKey(userId, workplace)
				.FirstOrDefaultAsync();

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

			// Cosmos: WorkInfo uses a composite partition key (UserId, Workplace)
			var workInfo = await _context.WorkInfos
				.WithPartitionKey(userId, workInfoDto.Workplace)
				.FirstOrDefaultAsync();

			if (workInfo == null)
			{
				workInfo = WorkInfo.FromDTO(workInfoDto, userId);
				_context.WorkInfos.Add(workInfo);
				await _context.SaveChangesAsync();
				return CreatedAtAction(nameof(GetWorkInfo), new { workplace = workInfo.Workplace }, workInfo.ToDTO());
			}

			workInfo.PayRates = workInfo.PayRates.Union(workInfoDto.PayRates).ToHashSet().ToList();

			_context.WorkInfos.Update(workInfo);
			await _context.SaveChangesAsync();

			return Ok(workInfo.ToDTO());
		}

		// DELETE: api/WorkInfos/KFC
		[HttpDelete("{workplace}")]
		public async Task<IActionResult> DeleteWorkInfo(string workplace, decimal? payRate)
		{
			var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
			if (string.IsNullOrEmpty(userId))
			{
				return Unauthorized("User ID is missing.");
			}

			// Cosmos: WorkInfo uses a composite partition key (UserId, Workplace)
			var workInfo = await _context.WorkInfos
				.WithPartitionKey(userId, workplace)
				.FirstOrDefaultAsync();

			if (payRate.HasValue)
			{
				if (workInfo == null)
				{
					return NotFound();
				}

				if (workInfo.PayRates.Contains(payRate.Value))
				{
					workInfo.PayRates.Remove(payRate.Value);
					_context.WorkInfos.Update(workInfo);
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
