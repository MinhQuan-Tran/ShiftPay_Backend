using Newtonsoft.Json;

namespace ShiftPay_Backend.Models
{
    public class WorkInfo
    {
        [JsonProperty(PropertyName = "id")]
        public required Guid Id { get; set; } = Guid.NewGuid();

        public required string UserId { get; set; } // Partition Key

        public required string Workplace { get; set; } // Unique Key (per UserId)

        public List<decimal> PayRates { get; set; } = new List<decimal>();

        public WorkInfoDTO ToDTO()
        {
            return new WorkInfoDTO
            {
                Workplace = this.Workplace,
                PayRates = new List<decimal>(this.PayRates)
            };
        }

        public static WorkInfo FromDTO(WorkInfoDTO dto, string userId)
        {
            return new WorkInfo
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Workplace = dto.Workplace,
                PayRates = new List<decimal>(dto.PayRates)
            };
        }
    }

    public class WorkInfoDTO
    {
        public required string Workplace { get; set; }

        public List<decimal> PayRates { get; set; } = new List<decimal>();
    }
}
