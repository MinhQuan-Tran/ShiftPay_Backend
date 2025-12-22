using Newtonsoft.Json;

namespace ShiftPay_Backend.Models
{
    public class WorkInfo
    {
        [JsonProperty(PropertyName = "id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        public required string UserId { get; set; } // Partition Key 1

        public required string Workplace { get; set; } // Partition Key 2

        public List<decimal> PayRates { get; set; } = new List<decimal>();

        public WorkInfoDTO ToDTO()
        {
            return new WorkInfoDTO
            {
                Id = this.Id,
                Workplace = this.Workplace,
                PayRates = new List<decimal>(this.PayRates)
            };
        }

        public static WorkInfo FromDTO(WorkInfoDTO dto, string userId)
        {
            return new WorkInfo
            {
                Id = dto.Id ?? Guid.NewGuid(),
                UserId = userId,
                Workplace = dto.Workplace,
                PayRates = new List<decimal>(dto.PayRates)
            };
        }
    }

    public class WorkInfoDTO
    {
        public Guid? Id { get; set; }

        public required string Workplace { get; set; }

        public List<decimal> PayRates { get; set; } = new List<decimal>();
    }
}
