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

        // GET: api/Shifts?year=2023&month=10&day=15
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ShiftDTO>>> GetShifts(int? year, int? month, int? day)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized("User ID is missing.");
            }

            var shifts = await _context.Shifts
                .Where(s =>
                    s.UserId == userId &&
                    (!year.HasValue || s.YearMonth.StartsWith($"{year:D4}-")) &&
                    (!month.HasValue || s.YearMonth.EndsWith($"-{month:D2}")) &&
                    (!day.HasValue || s.Day == day)
                )
                .ToListAsync();

            return Ok(shifts.Select(shift => shift.ToDTO()));
        }

        // GET: api/Shifts/5
        [HttpGet("{id}")]
        public async Task<ActionResult<ShiftDTO>> GetShift(string id)
        {
            Console.WriteLine($"Shift #{id}");

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized("User ID is missing.");
            }

            var shift = await _context.Shifts
                .Where(shift => shift.UserId == userId && shift.Id == id)
                .Select(shift => shift.ToDTO())
                .FirstOrDefaultAsync();

            return shift != null ? Ok(shift) : NotFound();
        }

        // PUT: api/Shifts/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutShift(string id, Shift shift)
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
                return NotFound();
            }

            var partitionChanged = existingShift.YearMonth != shift.YearMonth || existingShift.Day != shift.Day;
            if (partitionChanged)
            {
                _context.Shifts.Remove(existingShift);
                _context.Shifts.Add(new Shift
                {
                    Id = id,
                    UserId = userId,
                    Workplace = shift.Workplace,
                    PayRate = shift.PayRate,
                    From = shift.From,
                    To = shift.To,
                    UnpaidBreaks = shift.UnpaidBreaks
                });
            }
            else
            {
                existingShift.Workplace = shift.Workplace;
                existingShift.PayRate = shift.PayRate;
                existingShift.From = shift.From;
                existingShift.To = shift.To;
                existingShift.UnpaidBreaks = shift.UnpaidBreaks;

                _context.Shifts.Update(existingShift);
            }

            await _context.SaveChangesAsync();

            var updatedShift = await _context.Shifts
                .Where(s => s.UserId == userId && s.Id == id)
                .Select(s => s.ToDTO())
                .FirstOrDefaultAsync();

            return Ok(updatedShift);
        }

        // POST: api/Shifts
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<Shift>> PostShift(Shift shift)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized("User ID is missing.");
            }
            shift.UserId = userId;

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

            return CreatedAtAction("GetShift", new { id = shift.Id }, shift.ToDTO());
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
                return NotFound();
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
