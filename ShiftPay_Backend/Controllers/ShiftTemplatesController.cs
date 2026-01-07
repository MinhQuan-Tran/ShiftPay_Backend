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

			return Ok(shiftTemplates.Select(st => st.toDTO()));
		}

		// GET: api/ShiftTemplates/KFC-12345
		[HttpGet("{templateName}")]
		public async Task<ActionResult<ShiftTemplateDTO>> GetShiftTemplate(string templateName)
		{
			templateName = templateName.Trim();
			if (string.IsNullOrEmpty(templateName))
			{
				return BadRequest("Template name cannot be empty.");
			}

			var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
			if (string.IsNullOrEmpty(userId))
			{
				return Unauthorized("User ID is missing.");
			}

			var shiftTemplate = await _context.ShiftTemplates
				.WithPartitionKey(userId)
				.FirstOrDefaultAsync(st => st.TemplateName == templateName);

			return shiftTemplate != default ? Ok(shiftTemplate.toDTO()) : NotFound("No matching shift found.");
		}

		// PUT: api/ShiftTemplates/KFC-12345
		[HttpPut("{templateName}")]
		public async Task<ActionResult<ShiftTemplateDTO>> PutShiftTemplate(string templateName, ShiftTemplateDTO shiftTemplateDTO)
		{
			templateName = templateName.Trim();
			if (string.IsNullOrEmpty(templateName))
			{
				return BadRequest("Template name cannot be empty.");
			}

			var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
			if (string.IsNullOrEmpty(userId))
			{
				return Unauthorized("User ID is missing.");
			}

			var existingTemplate = await _context.ShiftTemplates
				.WithPartitionKey(userId)
				.FirstOrDefaultAsync(st => st.TemplateName == templateName);

			if (existingTemplate == default)
			{
				return NotFound("No matching shift template found to update.");
			}

			existingTemplate.Workplace = shiftTemplateDTO.Workplace;
			existingTemplate.PayRate = shiftTemplateDTO.PayRate;
			existingTemplate.StartTime = shiftTemplateDTO.StartTime;
			existingTemplate.EndTime = shiftTemplateDTO.EndTime;
			existingTemplate.UnpaidBreaks = shiftTemplateDTO.UnpaidBreaks;

			try
			{
				existingTemplate.Validate();
			}
			catch (InvalidOperationException ex)
			{
				return BadRequest(ex.Message);
			}

			await _context.SaveChangesAsync();
			return Ok(existingTemplate.toDTO());
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
			catch (InvalidOperationException ex)
			{
				return BadRequest(ex.Message);
			}

			receivedTemplate.Id = Guid.NewGuid(); // Generate new ID for the template

			_context.ShiftTemplates.Add(receivedTemplate);

			try
			{
				await _context.SaveChangesAsync();
			}
			catch (DbUpdateException)
			{
				return Conflict();
			}

			return CreatedAtAction(nameof(GetShiftTemplate), new { templateName = receivedTemplate.TemplateName }, receivedTemplate.toDTO());
		}

		// DELETE: api/ShiftTemplates/KFC-12345
		[HttpDelete("{templateName}")]
		public async Task<IActionResult> DeleteShiftTemplate(string templateName)
		{
			templateName = templateName.Trim();
			if (string.IsNullOrEmpty(templateName))
			{
				return BadRequest("Template name cannot be empty.");
			}

			var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
			if (string.IsNullOrEmpty(userId))
			{
				return Unauthorized("User ID is missing.");
			}

			var shiftTemplate = await _context.ShiftTemplates
				.WithPartitionKey(userId)
				.FirstOrDefaultAsync(st => st.TemplateName == templateName);

			if (shiftTemplate == default)
			{
				return NotFound("No matching shift template found to delete.");
			}

			_context.ShiftTemplates.Remove(shiftTemplate);
			await _context.SaveChangesAsync();
			return NoContent();
		}
	}
}
