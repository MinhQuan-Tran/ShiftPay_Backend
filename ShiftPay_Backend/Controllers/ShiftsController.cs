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
    public class ShiftsController : ControllerBase
    {
        private readonly ILogger<ShiftsController> _logger;
        private readonly ShiftPay_BackendContext _context;

        public ShiftsController(ILogger<ShiftsController> logger, ShiftPay_BackendContext context)
        {
            _logger = logger;
            _context = context;
        }

        // GET: api/Shifts?year=2023&month=10&day=15&id=id1&id=id2&id=id3 (or ids=id1&ids=id2&ids=id3)
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ShiftDTO>>> GetShifts(int? year, int? month, int? day, [FromQuery(Name = "id")] string[]? ids)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized("User ID is missing.");
            }

            var shifts = await _context.Shifts
                .Where(s =>
                    // User ID
                    s.UserId == userId &&
                    // IDs
                    (ids == null || !ids.Any() || ids.Contains(s.Id)) &&
                    // Year, Month, Day
                    (!year.HasValue || s.YearMonth.StartsWith($"{year:D4}-")) &&
                    (!month.HasValue || s.YearMonth.EndsWith($"-{month:D2}")) &&
                    (!day.HasValue || s.Day == day)
                )
                .ToListAsync();

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

            var shift = await _context.Shifts
                .Where(shift => shift.UserId == userId && shift.Id == id)
                .Select(shift => shift.ToDTO())
                .FirstOrDefaultAsync();

            return shift != null ? Ok(shift) : NotFound("No matching shift found.");
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
            var existingShift = await _context.Shifts
                .WithPartitionKey(userId)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (existingShift is null)
            {
                return NotFound("No matching shift found.");
            }

            var shift = Shift.FromDTO(recievedShiftDTO);

            var partitionChanged = existingShift.YearMonth != shift.YearMonth || existingShift.Day != shift.Day;
            if (partitionChanged)
            {
                // If the partition key has changed, create a new shift in the new partition
                _context.Shifts.Add(new Shift
                {
                    Id = id,
                    UserId = userId,
                    Workplace = shift.Workplace,
                    PayRate = shift.PayRate,
                    StartTime = shift.StartTime,
                    EndTime = shift.EndTime,
                    UnpaidBreaks = shift.UnpaidBreaks
                });

                _context.Shifts.Remove(existingShift);
            }
            else
            {
                existingShift.Workplace = shift.Workplace;
                existingShift.PayRate = shift.PayRate;
                existingShift.StartTime = shift.StartTime;
                existingShift.EndTime = shift.EndTime;
                existingShift.UnpaidBreaks = shift.UnpaidBreaks;

                _context.Shifts.Update(existingShift);
            }

            await _context.SaveChangesAsync();

            var updatedShift = await _context.Shifts
                .Where(s => s.UserId == userId && s.Id == id)
                .Select(s => s.ToDTO())
                .FirstOrDefaultAsync();

            if (updatedShift is null)
            {
                return NotFound("Something went wrong. Updated shift not found.");
            }

            return Ok(updatedShift);
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

            var shift = Shift.FromDTO(recievedShiftDTO);
            shift.Id = Guid.NewGuid().ToString(); // Generate a new ID for the shift
            shift.UserId = userId; // Set the UserId to the current user's ID

            _context.Shifts.Add(shift);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                if (ShiftExists(shift.Id, userId))
                {
                    return Conflict();
                }
                else
                {
                    throw;
                }
            }

            var updatedShift = await _context.Shifts
                .Where(s => s.UserId == userId && s.Id == shift.Id)
                .Select(s => s.ToDTO())
                .FirstOrDefaultAsync();

            if (updatedShift is null)
            {
                return NotFound("Something went wrong. Updated shift not found.");
            }

            return CreatedAtAction("GetShift", new { id = updatedShift.Id }, updatedShift);
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

            var shifts = recievedShiftDTOs.Select(shiftDTO =>
            {
                var shift = Shift.FromDTO(shiftDTO);
                shift.Id = Guid.NewGuid().ToString(); // Generate a new ID for the shift
                shift.UserId = userId; // Set the UserId to the current user's ID
                return shift;
            }).ToList();

            _context.Shifts.AddRange(shifts);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                return Conflict();
            }

            var updatedShifts = await _context.Shifts
                .Where(s => s.UserId == userId && shifts.Select(r => r.Id).Contains(s.Id))
                .Select(s => s.ToDTO())
                .ToListAsync();

            if (updatedShifts is null || updatedShifts.Count != shifts.Count)
            {
                return NotFound("Something went wrong. Updated shifts not found.");
            }

            return CreatedAtAction(
                "GetShifts",
                new { id = updatedShifts.Select(s => s.Id).ToArray() },
                updatedShifts
            );
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

            var shift = await _context.Shifts.Where(e => e.UserId == userId && e.Id == id).FirstOrDefaultAsync();

            if (shift is null)
            {
                return NotFound("No matching shift found.");
            }

            _context.Shifts.Remove(shift);
            await _context.SaveChangesAsync();

            return NoContent();
        }


        private bool ShiftExists(string id, string userId)
        {
            return _context.Shifts.Any(e => e.Id == id && e.UserId == userId);
        }
    }
}
