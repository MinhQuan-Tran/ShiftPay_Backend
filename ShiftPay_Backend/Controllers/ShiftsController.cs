using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using ShiftPay_Backend.Data;
using ShiftPay_Backend.Models;
using System.Linq.Expressions;
using System.Security.Claims;

namespace ShiftPay_Backend.Controllers
{
	[Authorize]
	[Route("api/[controller]")]
	[ApiController]
	public class ShiftsController : ControllerBase
	{
		private readonly ILogger<ShiftsController> _logger;
		private readonly ShiftPay_BackendContext _context;

		public ShiftsController(ILogger<ShiftsController> logger, ShiftPay_BackendContext context)
		{
			_logger = logger;
			_context = context;
		}

		// GET: api/Shifts?year=2023&month=10&day=15
		//                  &startTime=2023-10-15T08:00:00&endTime=2023-10-15T17:00:00
		//                  &id=shift1&id=shift2&id=shift3
		//                  (or &ids=shift1&ids=shift2&ids=shift3)
		[HttpGet]
		public async Task<ActionResult<IEnumerable<ShiftDTO>>> GetShifts(
			int? year, int? month, int? day,            // Time
			DateTime? startTime, DateTime? endTime,     // Time Range
			[FromQuery(Name = "id")] Guid[]? ids        // IDs
		)
		{
			var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
			if (string.IsNullOrEmpty(userId))
			{
				return Unauthorized("User ID is missing.");
			}

			var shifts = await FilterShiftsAsync(
				userId: userId,
				year: year, month: month, day: day,
				startTime: startTime, endTime: endTime,
				ids: ids
			);

			return Ok(shifts.Select(shift => shift.ToDTO()));
		}

		// GET: api/Shifts/abc-123
		[HttpGet("{id:guid}")]
		public async Task<ActionResult<ShiftDTO>> GetShift(Guid id)
		{
			var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
			if (string.IsNullOrEmpty(userId))
			{
				return Unauthorized("User ID is missing.");
			}

			var shift = (await FilterShiftsAsync(userId: userId, ids: [id])).FirstOrDefault();

			return shift != default ? Ok(shift.ToDTO()) : NotFound("No matching shift found.");
		}


		// PUT: api/Shifts/abc-123
		[HttpPut("{id:guid}")]
		public async Task<ActionResult<ShiftDTO>> PutShift(Guid id, ShiftDTO receivedShiftDTO)
		{
			var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
			if (string.IsNullOrEmpty(userId))
			{
				return Unauthorized("User ID is missing.");
			}

			// Optional safety: ensure payload Id (if provided) matches route Id
			if (receivedShiftDTO.Id.HasValue && receivedShiftDTO.Id.Value != id)
			{
				return BadRequest("Route id and payload id do not match.");
			}

			var existingShift = (await FilterShiftsAsync(userId: userId, ids: [id])).FirstOrDefault();
			if (existingShift is null)
			{
				return NotFound("No matching shift found.");
			}

			// To get YearMonth and Day from the DTO
			Shift receivedShift;
			try
			{
				receivedShift = Shift.FromDTO(receivedShiftDTO, userId);
			}
			catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
			{
				return BadRequest(ex.Message);
			}

			var partitionChanged =
				existingShift.YearMonth != receivedShift.YearMonth ||
				existingShift.Day != receivedShift.Day;

			EntityEntry<Shift> entry;

			if (partitionChanged)
			{
				entry = _context.Shifts.Add(new Shift
				{
					Id = id,
					UserId = userId,
					Workplace = receivedShift.Workplace,
					PayRate = receivedShift.PayRate,
					StartTime = receivedShift.StartTime,
					EndTime = receivedShift.EndTime,
					UnpaidBreaks = receivedShift.UnpaidBreaks
				});
				_context.Shifts.Remove(existingShift);
			}
			else
			{
				existingShift.Workplace = receivedShift.Workplace;
				existingShift.PayRate = receivedShift.PayRate;
				existingShift.StartTime = receivedShift.StartTime;
				existingShift.EndTime = receivedShift.EndTime;
				existingShift.UnpaidBreaks = receivedShift.UnpaidBreaks;

				try
				{
					existingShift.Validate();
				}
				catch (InvalidOperationException ex)
				{
					return BadRequest(ex.Message);
				}

				entry = _context.Shifts.Update(existingShift);
			}

			try
			{
				await _context.SaveChangesAsync();
			}
			catch (DbUpdateConcurrencyException ex)
			{
				_logger.LogWarning(ex, "Concurrency error when updating shift {ShiftId} for user {UserId}", id, userId);
				return Conflict("The shift was updated or deleted by another process.");
			}
			catch (DbUpdateException ex)
			{
				_logger.LogError(ex, "Failed to update shift {ShiftId} for user {UserId}", id, userId);
				return Conflict("Failed to update the shift.");
			}

			return Ok(entry.Entity.ToDTO());
		}

		// POST: api/Shifts
		[HttpPost]
		public async Task<ActionResult<ShiftDTO>> PostShift(ShiftDTO receivedShiftDTO)
		{
			var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
			if (string.IsNullOrEmpty(userId))
			{
				return Unauthorized("User ID is missing.");
			}

			Shift receivedShift;
			try
			{
				receivedShift = Shift.FromDTO(receivedShiftDTO, userId);
			}
			catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
			{
				return BadRequest(ex.Message);
			}

			receivedShift.Id = Guid.NewGuid(); // Generate a new ID for the shift

			_context.Shifts.Add(receivedShift);

			try
			{
				await _context.SaveChangesAsync();
			}
			catch (DbUpdateException)
			{
				return Conflict();
			}

			var addedShift = (await FilterShiftsAsync(
				userId: userId,
				ids: [receivedShift.Id]
				))
				.FirstOrDefault();

			if (addedShift is null)
			{
				return NotFound("Something went wrong. Added shift not found.");
			}

			return CreatedAtAction(nameof(GetShift), new { id = addedShift.Id }, addedShift.ToDTO());
		}

		// POST: api/Shifts/batch
		[HttpPost("batch")]
		public async Task<ActionResult<IEnumerable<ShiftDTO>>> PostShiftBatch(ShiftDTO[] receivedShiftDTOs)
		{
			var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
			if (string.IsNullOrEmpty(userId))
			{
				return Unauthorized("User ID is missing.");
			}

			List<Shift> receivedShifts;
			try
			{
				receivedShifts = receivedShiftDTOs.Select(shiftDTO =>
				{
					var shift = Shift.FromDTO(shiftDTO, userId);
					shift.Id = Guid.NewGuid(); // Generate a new ID for the shift
					return shift;
				}).ToList();
			}
			catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
			{
				return BadRequest(ex.Message);
			}

			_context.Shifts.AddRange(receivedShifts);

			try
			{
				await _context.SaveChangesAsync();
			}
			catch (DbUpdateException)
			{
				return Conflict();
			}

			var addedShifts = (await FilterShiftsAsync(
				userId: userId,
				ids: receivedShifts.Select(s => s.Id).ToArray()
				))
				.Select(s => s.ToDTO())
				.ToList();

			if (addedShifts is null || addedShifts.Count != receivedShifts.Count)
			{
				return NotFound("Something went wrong. Updated shifts not found.");
			}

			return CreatedAtAction(
				"GetShifts",
				new { id = addedShifts.Select(s => s.Id).ToArray() },
				addedShifts
			);
		}


		// DELETE: api/Shifts?year=2023&month=10&day=15
		//                  &startTime=2023-10-15T08:00:00&endTime=2023-10-15T17:00:00
		//                  &id=shift1&id=shift2&id=shift3
		//                  (or &ids=shift1&ids=shift2&ids=shift3)
		[HttpDelete]
		public async Task<IActionResult> DeleteShifts(
			int? year, int? month, int? day,            // Time
			DateTime? startTime, DateTime? endTime,     // Time Range
			[FromQuery(Name = "id")] Guid[]? ids        // IDs
		)
		{
			var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
			if (string.IsNullOrEmpty(userId))
			{
				return Unauthorized("User ID is missing.");
			}

			var shifts = await FilterShiftsAsync(
				userId: userId,
				year: year, month: month, day: day,
				startTime: startTime, endTime: endTime,
				ids: ids
			);

			if (shifts.Count == 0)
			{
				return NotFound("No matching shifts found.");
			}

			_context.Shifts.RemoveRange(shifts);
			await _context.SaveChangesAsync();

			return NoContent();
		}

		// DELETE: api/Shifts/5
		[HttpDelete("{id:guid}")]
		public async Task<IActionResult> DeleteShift(Guid id)
		{
			var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
			if (string.IsNullOrEmpty(userId))
			{
				return Unauthorized("User ID is missing.");
			}

			var shift = (await FilterShiftsAsync(userId: userId, ids: [id])).FirstOrDefault();

			if (shift is null)
			{
				return NotFound("No matching shift found.");
			}

			_context.Shifts.Remove(shift);
			await _context.SaveChangesAsync();

			return NoContent();
		}

		private async Task<List<Shift>> FilterShiftsAsync(
			string userId,                                          // User ID
			int? year = null, int? month = null, int? day = null,   // Time
			DateTime? startTime = null, DateTime? endTime = null,   // Time Range
			Guid[]? ids = null                                      // IDs
		)
		{
			string? YearMonth = year.HasValue && month.HasValue ? $"{year:D4}-{month:D2}" : null;

			var query = _context.Shifts.WithPartitionKey(userId);
			var queryYearMonth = YearMonth != null ? _context.Shifts.WithPartitionKey(userId, YearMonth) : null;
			var queryDay = day.HasValue ? (queryYearMonth ?? query).Where(shift => shift.Day == day) : null;
			//var queryYearMonthDay = (year.HasValue || month.HasValue || day.HasValue) ?  : null;


			return await _context.Shifts
				.WithPartitionKey(userId)

				// Use current order for performance
				.Where(TimeRangeFilter(startTime, endTime))
				.Where(IdFilter(ids))
				.Where(DateFilter(year, month, day))
				.ToListAsync();

			// Filters
			static Expression<Func<Shift, bool>> TimeRangeFilter(DateTime? start, DateTime? end) =>
				shift => (!start.HasValue || shift.StartTime >= start.Value) &&
					 (!end.HasValue || shift.EndTime <= end.Value);

			static Expression<Func<Shift, bool>> IdFilter(Guid[]? ids) =>
				shift => ids == null || !ids.Any() || ids.Contains(shift.Id);

			static Expression<Func<Shift, bool>> DateFilter(int? year, int? month, int? day) =>
				shift => (!year.HasValue || shift.YearMonth.StartsWith($"{year:D4}-")) &&
					 (!month.HasValue || shift.YearMonth.EndsWith($"-{month:D2}")) &&
					 (!day.HasValue || shift.Day == day);
		}
	}
}
