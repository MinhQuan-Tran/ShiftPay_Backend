using Newtonsoft.Json;

namespace ShiftPay_Backend.Models
{
    public class Shift
    {
        [JsonProperty(PropertyName = "id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        public string UserId { get; set; } = string.Empty; // Partition Key 1

        public string YearMonth { get; private set; } = string.Empty; // Partition Key 2

        public int Day { get; private set; } // Partition Key 3

        public required string Workplace { get; set; }

        public required decimal PayRate { get; set; }

        private DateTime _startTime;
        public required DateTime StartTime
        {
            get => _startTime;
            set
            {
                if (value == default)
                {
                    throw new ArgumentException("StartTime cannot be default value.");
                }

                if (EndTime != default && value > EndTime)
                {
                    throw new ArgumentException($"StartTime cannot be greater than EndTime. value: {value}. EndTime: {EndTime}");
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
                    throw new ArgumentException("EndTime cannot be default value.");
                }

                if (StartTime != default && value < StartTime)
                {
                    throw new ArgumentException("EndTime cannot be less than StartTime.");
                }

                _endTime = value;
            }
        }

        public List<TimeSpan> UnpaidBreaks { get; set; } = new List<TimeSpan>();

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
            return new Shift
            {
                Id = dto.Id ?? Guid.NewGuid(),
                UserId = userId,
                Workplace = dto.Workplace,
                PayRate = dto.PayRate,
                StartTime = dto.StartTime,
                EndTime = dto.EndTime,
                UnpaidBreaks = dto.UnpaidBreaks ?? new List<TimeSpan>()
            };
        }
    }

    public class ShiftDTO
    {
        public Guid? Id { get; set; }

        public required string Workplace { get; set; }

        public required decimal PayRate { get; set; }

        public required DateTime StartTime { get; set; }

        public required DateTime EndTime { get; set; }

        public required List<TimeSpan> UnpaidBreaks { get; set; }
    }
}
