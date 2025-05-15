using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
            [FromQuery(Name = "id")] string[]? ids      // IDs
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
        [HttpGet("{id}")]
        public async Task<ActionResult<ShiftDTO>> GetShift(string id)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized("User ID is missing.");
            }

            var shift = (await FilterShiftsAsync(userId: userId, ids: [id])).FirstOrDefault();

            return shift != default ? Ok(shift.ToDTO()) : NotFound("No matching shift found.");
        }


        // PUT: api/Shifts/5
        [HttpPut("{id}")]
        public async Task<ActionResult<ShiftDTO>> PutShift(string id, ShiftDTO recievedShiftDTO)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized("User ID is missing.");
            }

            // Check if shift exists
            var existingShift = (await FilterShiftsAsync(userId: userId, ids: [id])).FirstOrDefault();

            if (existingShift is null)
            {
                return NotFound("No matching shift found.");
            }

            // To get YearMonth and Day from the DTO
            var recievedShift = Shift.FromDTO(recievedShiftDTO);

            var partitionChanged =
                existingShift.YearMonth != recievedShift.YearMonth ||
                existingShift.Day != recievedShift.Day;

            if (partitionChanged)
            {
                // If the partition key has changed, create a new shift in the new partition
                _context.Shifts.Add(new Shift
                {
                    Id = id,
                    UserId = userId,
                    Workplace = recievedShift.Workplace,
                    PayRate = recievedShift.PayRate,
                    StartTime = recievedShift.StartTime,
                    EndTime = recievedShift.EndTime,
                    UnpaidBreaks = recievedShift.UnpaidBreaks
                });

                _context.Shifts.Remove(existingShift);
            }
            else
            {
                existingShift.Workplace = recievedShift.Workplace;
                existingShift.PayRate = recievedShift.PayRate;
                existingShift.StartTime = recievedShift.StartTime;
                existingShift.EndTime = recievedShift.EndTime;
                existingShift.UnpaidBreaks = recievedShift.UnpaidBreaks;

                _context.Shifts.Update(existingShift);
            }

            await _context.SaveChangesAsync();

            var updatedShift = (await FilterShiftsAsync(userId: userId, ids: [recievedShift.Id]))
                .FirstOrDefault();

            if (updatedShift is null)
            {
                return NotFound("Something went wrong. Updated shift not found.");
            }

            return Ok(updatedShift.ToDTO());
        }


        // POST: api/Shifts
        [HttpPost]
        public async Task<ActionResult<ShiftDTO>> PostShift(ShiftDTO recievedShiftDTO)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized("User ID is missing.");
            }

            var recievedShift = Shift.FromDTO(recievedShiftDTO);
            recievedShift.Id = Guid.NewGuid().ToString(); // Generate a new ID for the shift
            recievedShift.UserId = userId; // Set the UserId to the current user's ID

            _context.Shifts.Add(recievedShift);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                return Conflict();
            }

            var addeddShift = (await FilterShiftsAsync(
                userId: userId,
                ids: [recievedShift.Id]
                ))
                .FirstOrDefault();

            if (addeddShift is null)
            {
                return NotFound("Something went wrong. Added shift not found.");
            }

            return CreatedAtAction("GetShift", new { id = addeddShift.Id }, addeddShift.ToDTO());
        }

        // POST: api/Shifts/batch
        [HttpPost("batch")]
        public async Task<ActionResult<IEnumerable<ShiftDTO>>> PostShiftBatch(ShiftDTO[] recievedShiftDTOs)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized("User ID is missing.");
            }

            var recievedShifts = recievedShiftDTOs.Select(shiftDTO =>
            {
                var shift = Shift.FromDTO(shiftDTO);
                shift.Id = Guid.NewGuid().ToString(); // Generate a new ID for the shift
                shift.UserId = userId; // Set the UserId to the current user's ID
                return shift;
            }).ToList();

            _context.Shifts.AddRange(recievedShifts);

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
                ids: recievedShifts.Select(s => s.Id).ToArray()
                ))
                .Select(s => s.ToDTO())
                .ToList();

            if (addedShifts is null || addedShifts.Count != recievedShifts.Count)
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
            [FromQuery(Name = "id")] string[]? ids      // IDs
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
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteShift(string id)
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
            string[]? ids = null                                    // IDs
        )
        {
            return await _context.Shifts
                .WithPartitionKey(userId)

                // Use current order for performance
                .Where(TimeRangeFilter(startTime, endTime))
                .Where(IdFilter(ids))
                .Where(DateFilter(year, month, day))

                .ToListAsync();

            // Filters
            static Expression<Func<Shift, bool>> TimeRangeFilter(DateTime? start, DateTime? end) =>
                s => (!start.HasValue || s.StartTime >= start.Value) &&
                     (!end.HasValue || s.EndTime <= end.Value);

            static Expression<Func<Shift, bool>> IdFilter(string[]? ids) =>
                s => ids == null || !ids.Any() || ids.Contains(s.Id);

            static Expression<Func<Shift, bool>> DateFilter(int? year, int? month, int? day) =>
                s => (!year.HasValue || s.YearMonth.StartsWith($"{year:D4}-")) &&
                     (!month.HasValue || s.YearMonth.EndsWith($"-{month:D2}")) &&
                     (!day.HasValue || s.Day == day);
        }
    }
}
