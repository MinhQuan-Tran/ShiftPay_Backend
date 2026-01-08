using Newtonsoft.Json;

namespace ShiftPay_Backend.Models
{
    public class Shift
    {
        [JsonProperty(PropertyName = "id")]
        public required Guid Id { get; set; } = Guid.NewGuid();

        public required string UserId { get; set; } // Partition Key 1

        public string YearMonth { get; private set; } = string.Empty; // Partition Key 2

        public int Day { get; private set; } // Partition Key 3

		public required string Workplace
		{
			get;
			set
			{
				ArgumentException.ThrowIfNullOrWhiteSpace(value);
				field = value.Trim();
			}
		}

		public required decimal PayRate { get; set; }

        private DateTime _startTime;
        public required DateTime StartTime
        {
            get => _startTime;
            set
            {
                if (value == default)
                {
                    throw new ArgumentException("StartTime cannot be default value.", nameof(StartTime));
                }

                _startTime = value;
                YearMonth = _startTime.ToString("yyyy-MM"); // Auto-set YearMonth
                Day = _startTime.Day;                       // Auto-set Day
            }
        }

        private DateTime _endTime;
        public required DateTime EndTime
        {
            get => _endTime;
            set
            {
                if (value == default)
                {
                    throw new ArgumentException("EndTime cannot be default value.", nameof(EndTime));
                }

                _endTime = value;
            }
        }

        public List<TimeSpan> UnpaidBreaks { get; set; } = new List<TimeSpan>();

        /// <summary>
        /// Validates that the shift has valid state (StartTime before EndTime, valid breaks, etc.)
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the shift is in an invalid state</exception>
        public void Validate()
        {
            if (StartTime == default)
            {
                throw new InvalidOperationException("StartTime cannot be default value.");
            }

            if (EndTime == default)
            {
                throw new InvalidOperationException("EndTime cannot be default value.");
            }

            if (StartTime >= EndTime)
            {
                throw new InvalidOperationException($"StartTime must be before EndTime. StartTime: {StartTime}, EndTime: {EndTime}");
            }

            if (PayRate < 0)
            {
                throw new InvalidOperationException("PayRate cannot be negative.");
            }

            if (UnpaidBreaks != null)
            {
                if (UnpaidBreaks.Any(brk => brk < TimeSpan.Zero))
                {
                    throw new InvalidOperationException("UnpaidBreaks cannot contain negative time spans.");
                }

                var totalBreakTime = UnpaidBreaks.Aggregate(TimeSpan.Zero, (sum, brk) => sum + brk);
                var shiftDuration = EndTime - StartTime;

                if (totalBreakTime >= shiftDuration)
                {
                    throw new InvalidOperationException($"Total unpaid breaks ({totalBreakTime}) cannot be greater than or equal to shift duration ({shiftDuration}).");
                }
            }
        }

        public ShiftDTO ToDTO()
        {
            return new ShiftDTO
            {
                Id = Id,
				Workplace = Workplace,
                PayRate = PayRate,
                StartTime = StartTime,
                EndTime = EndTime,
                UnpaidBreaks = UnpaidBreaks ?? new List<TimeSpan>()
            };
        }

        public static Shift FromDTO(ShiftDTO dto, string userId)
        {
            var shift = new Shift
            {
                Id = dto.Id ?? Guid.NewGuid(),
                UserId = userId,
                Workplace = dto.Workplace,
                PayRate = dto.PayRate,
                StartTime = dto.StartTime,
                EndTime = dto.EndTime,
                UnpaidBreaks = dto.UnpaidBreaks ?? new List<TimeSpan>()
            };

            shift.Validate();
            return shift;
        }
    }

    public class ShiftDTO
    {
        public Guid? Id { get; set; }

		// No exception here since DTOs are just data carriers
		public required string Workplace { get; set => field = value.Trim(); }

        public required decimal PayRate { get; set; }

        public required DateTime StartTime { get; set; }

        public required DateTime EndTime { get; set; }

        public required List<TimeSpan> UnpaidBreaks { get; set; }
    }
}
