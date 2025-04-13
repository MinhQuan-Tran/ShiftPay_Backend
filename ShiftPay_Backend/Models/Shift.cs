using Newtonsoft.Json;

namespace ShiftPay_Backend.Models
{
    public class Shift
    {
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonProperty(PropertyName = "UserId")]
        public string UserId { get; set; } = string.Empty; // Partition Key 1

        [JsonProperty(PropertyName = "YearMonth")]
        public string YearMonth { get; private set; } = string.Empty; // Partition Key 2

        [JsonProperty(PropertyName = "Day")]
        public int Day { get; private set; } // Partition Key 3

        public required string Workplace { get; set; }

        public required decimal PayRate { get; set; }

        private DateTime _from;
        public required DateTime From
        {
            get => _from;
            set
            {
                _from = value;
                YearMonth = _from.ToString("yyyy-MM"); // Auto-set YearMonth
                Day = _from.Day;                       // Auto-set Day
            }
        }

        public required DateTime To { get; set; }

        public List<TimeSpan> UnpaidBreaks { get; set; } = new List<TimeSpan>();

        public ShiftDTO ToDTO()
        {
            return new ShiftDTO
            {
                Id = Id,
                YearMonth = YearMonth,
                Day = Day,
                Workplace = Workplace,
                PayRate = PayRate,
                From = From,
                To = To,
                UnpaidBreaks = UnpaidBreaks
            };
        }
    }

    public class ShiftDTO
    {
        public required string Id { get; set; }
        public required string YearMonth { get; set; }
        public required int Day { get; set; }
        public required string Workplace { get; set; }
        public required decimal PayRate { get; set; }
        public required DateTime From { get; set; }
        public required DateTime To { get; set; }
        public required List<TimeSpan> UnpaidBreaks { get; set; }
    }
}
