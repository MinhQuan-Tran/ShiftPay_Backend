using Newtonsoft.Json;

namespace ShiftPay_Backend.Models
{
	public class WorkInfo
	{
		[JsonProperty(PropertyName = "id")]
		public required Guid Id { get; set; } = Guid.NewGuid();

		public required string UserId { get; set; } // Partition Key

		public required string Workplace // Unique Key (per UserId), no updates allowed
		{
			get;
			set
			{
				ArgumentException.ThrowIfNullOrWhiteSpace(value);
				field = value.Trim();
			}
		}


		public List<decimal> PayRates { get; set; } = new List<decimal>();

		/// <summary>
		/// Validates that the work info has valid state (non-negative pay rates, valid workplace, etc.)
		/// </summary>
		/// <exception cref="InvalidOperationException">Thrown when the work info is in an invalid state</exception>
		public void Validate()
		{
			if (string.IsNullOrWhiteSpace(Workplace))
			{
				throw new InvalidOperationException("Workplace cannot be empty or whitespace.");
			}

			if (PayRates != null && PayRates.Any(payRate => payRate < 0))
			{
				throw new InvalidOperationException("PayRates cannot contain negative values.");
			}
		}

		public WorkInfoDTO ToDTO()
		{
			return new WorkInfoDTO
			{
				Id = Id,
				Workplace = this.Workplace,
				PayRates = new List<decimal>(this.PayRates ?? [])
			};
		}

		public static WorkInfo FromDTO(WorkInfoDTO dto, string userId)
		{
			var workInfo = new WorkInfo
			{
				Id = dto.Id ?? Guid.NewGuid(),
				UserId = userId,
				Workplace = dto.Workplace,
				PayRates = new List<decimal>(dto.PayRates ?? [])
			};

			workInfo.Validate();
			return workInfo;
		}

	}

	public class WorkInfoDTO
	{
		public Guid? Id { get; set; }

		public required string Workplace { get; set => field = value.Trim(); }

		public List<decimal> PayRates { get; set; } = new List<decimal>();
	}
}
