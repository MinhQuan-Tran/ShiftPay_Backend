namespace ShiftPay_Backend.Models
{
	public class ShiftTemplate
	{
		public required Guid Id { get; set; } = Guid.NewGuid();

		public required string UserId { get; set; } // Partition Key

		public required string TemplateName
		{
			get;
			set
			{
				ArgumentException.ThrowIfNullOrWhiteSpace(value);
				field = value.Trim();
			}
		}

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
		/// Validates that the shift template has valid state (StartTime before EndTime, valid breaks, etc.)
		/// </summary>
		/// <exception cref="InvalidOperationException">Thrown when the template is in an invalid state</exception>
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
				foreach (var unpaidBreak in UnpaidBreaks)
				{
					if (unpaidBreak < TimeSpan.Zero)
					{
						throw new InvalidOperationException("UnpaidBreaks cannot contain negative time spans.");
					}
				}

				var totalBreakTime = UnpaidBreaks.Aggregate(TimeSpan.Zero, (sum, brk) => sum + brk);
				var shiftDuration = EndTime - StartTime;

				if (totalBreakTime >= shiftDuration)
				{
					throw new InvalidOperationException($"Total unpaid breaks ({totalBreakTime}) cannot be greater than or equal to shift duration ({shiftDuration}).");
				}
			}
		}

		public ShiftTemplateDTO toDTO()
		{
			return new ShiftTemplateDTO
			{
				Id = this.Id,
				TemplateName = this.TemplateName,
				Workplace = this.Workplace,
				PayRate = this.PayRate,
				StartTime = this.StartTime,
				EndTime = this.EndTime,
				UnpaidBreaks = this.UnpaidBreaks,
			};
		}

		public static ShiftTemplate FromDTO(ShiftTemplateDTO dto, string userId)
		{
			var template = new ShiftTemplate
			{
				Id = dto.Id ?? Guid.NewGuid(),
				UserId = userId,
				TemplateName = dto.TemplateName,
				Workplace = dto.Workplace,
				PayRate = dto.PayRate,
				StartTime = dto.StartTime,
				EndTime = dto.EndTime,
				UnpaidBreaks = dto.UnpaidBreaks ?? new List<TimeSpan>()
			};

			template.Validate();
			return template;
		}
	}

	public class ShiftTemplateDTO : ShiftDTO
	{
		public required string TemplateName { get; set => field = value.Trim(); }
	}
}
