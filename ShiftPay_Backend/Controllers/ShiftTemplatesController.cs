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
	public class ShiftTemplatesController : ControllerBase
	{
		private readonly ILogger<ShiftTemplatesController> _logger;
		private readonly ShiftPay_BackendContext _context;

		public ShiftTemplatesController(ILogger<ShiftTemplatesController> logger, ShiftPay_BackendContext context)
		{
			_logger = logger;
			_context = context;
		}

		// GET: api/ShiftTemplates
		[HttpGet]
		public async Task<ActionResult<IEnumerable<ShiftTemplateDTO>>> GetShiftTemplates()
		{
			var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
			if (string.IsNullOrEmpty(userId))
			{
				return Unauthorized("User ID is missing.");
			}

			var shiftTemplates = await _context.ShiftTemplates
				.WithPartitionKey(userId)
				.ToListAsync();

			return Ok(shiftTemplates.Select(st => st.ToDTO()));
		}

		// GET: api/ShiftTemplates/{id}
		[HttpGet("{id:guid}")]
		public async Task<ActionResult<ShiftTemplateDTO>> GetShiftTemplate(Guid id)
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

			var shiftTemplate = await _context.ShiftTemplates
				.WithPartitionKey(userId)
				.FirstOrDefaultAsync(st => st.Id == id);

			return shiftTemplate != default ? Ok(shiftTemplate.ToDTO()) : NotFound("No matching shift template found.");
		}

		// POST: api/ShiftTemplates
		[HttpPost]
		public async Task<ActionResult<ShiftTemplateDTO>> PostShiftTemplate(ShiftTemplateDTO receivedShiftTemplateDTO)
		{
			var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
			if (string.IsNullOrEmpty(userId))
			{
				return Unauthorized("User ID is missing.");
			}

			ShiftTemplate receivedTemplate;
			try
			{
				receivedTemplate = ShiftTemplate.FromDTO(receivedShiftTemplateDTO, userId);
			}
			catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
			{
				return BadRequest(ex.Message);
			}

			var existingTemplate = await _context.ShiftTemplates
				.WithPartitionKey(userId)
				.FirstOrDefaultAsync(st => st.TemplateName == receivedTemplate.TemplateName);

			if (existingTemplate != default)
			{
				existingTemplate.Workplace = receivedTemplate.Workplace;
				existingTemplate.PayRate = receivedTemplate.PayRate;
				existingTemplate.StartTime = receivedTemplate.StartTime;
				existingTemplate.EndTime = receivedTemplate.EndTime;
				existingTemplate.UnpaidBreaks = receivedTemplate.UnpaidBreaks;

				try
				{
					existingTemplate.Validate();
				}
				catch (InvalidOperationException ex)
				{
					return BadRequest(ex.Message);
				}

				await _context.SaveChangesAsync();
				return Ok(existingTemplate.ToDTO());
			}

			receivedTemplate.Id = Guid.NewGuid();
			_context.ShiftTemplates.Add(receivedTemplate);

			try
			{
				await _context.SaveChangesAsync();
			}
			catch (DbUpdateException)
			{
				return Conflict();
			}

			return CreatedAtAction(nameof(GetShiftTemplate), new { id = receivedTemplate.Id }, receivedTemplate.ToDTO());
		}

		// DELETE: api/ShiftTemplates/{id}
		[HttpDelete("{id:guid}")]
		public async Task<IActionResult> DeleteShiftTemplate(Guid id)
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

			var shiftTemplate = await _context.ShiftTemplates
				.WithPartitionKey(userId)
				.FirstOrDefaultAsync(st => st.Id == id);

			if (shiftTemplate == default)
			{
				return NotFound("No matching shift template found.");
			}

			_context.ShiftTemplates.Remove(shiftTemplate);
			await _context.SaveChangesAsync();
			return NoContent();
		}
	}
}
