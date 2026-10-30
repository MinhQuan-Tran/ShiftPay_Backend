using Newtonsoft.Json;
using System.Globalization;

namespace ShiftPay_Backend.Models
{
	public class WorkInfo
	{
		[JsonProperty(PropertyName = "id")]
		public required Guid Id { get; set; } = Guid.NewGuid();

		public required string UserId { get; set; }

		// The Cosmos unique key remains one workplace per user.
		public required string Workplace { get; set; }

		// Retained for existing documents and older clients. With profiles, this is a projection.
		public List<decimal> PayRates { get; set; } = [];

		// EF may materialize a missing owned collection as []; the marker preserves that distinction.
		public List<WorkProfile>? Profiles { get; set; }
		public bool HasProfiles { get; set; }

		public void Validate()
		{
			if (string.IsNullOrWhiteSpace(Workplace))
			{
				throw new InvalidOperationException("Workplace cannot be empty or whitespace.");
			}

			if (PayRates == null || PayRates.Any(rate => rate < 0))
			{
				throw new InvalidOperationException("PayRates must be a list of non-negative values.");
			}

			if (Profiles == null) return;

			var profileIds = new HashSet<string>(StringComparer.Ordinal);
			foreach (var profile in Profiles)
			{
				if (profile == null || !ValidId(profile.Id) || !profileIds.Add(profile.Id))
				{
					throw new InvalidOperationException("Profiles must have unique, non-empty IDs of at most 100 characters.");
				}

				if (profile.Position?.Length > 200 || profile.Location?.Length > 200)
				{
					throw new InvalidOperationException("Position and location must be at most 200 characters.");
				}

				if (profile.PayRates == null)
				{
					throw new InvalidOperationException("Each profile must include a payRates list.");
				}

				var rateIds = new HashSet<string>(StringComparer.Ordinal);
				foreach (var rate in profile.PayRates)
				{
					if (rate == null || !ValidId(rate.Id) || !rateIds.Add(rate.Id))
					{
						throw new InvalidOperationException("Pay rates must have unique, non-empty IDs of at most 100 characters within a profile.");
					}

					if (rate.HourlyRate < 0)
					{
						throw new InvalidOperationException("Hourly rates cannot be negative.");
					}

					if (rate.Name == null || rate.Name.Length > 100 ||
						(string.IsNullOrWhiteSpace(rate.Name) && !IsLegacyRateId(rate.Id)))
					{
						throw new InvalidOperationException("Pay rate names must be non-empty and at most 100 characters. Migrated legacy rates may have an empty name.");
					}
				}
			}
		}

		public WorkInfoDTO ToDTO()
		{
			var profiles = CopyProfiles(EffectiveProfiles());
			return new WorkInfoDTO
			{
				Id = Id,
				Workplace = Workplace,
				PayRates = ProjectRates(profiles),
				Profiles = profiles
			};
		}

		public static WorkInfo FromDTO(WorkInfoDTO dto, string userId)
		{
			ArgumentNullException.ThrowIfNull(dto);
			var workInfo = new WorkInfo
			{
				Id = dto.Id.GetValueOrDefault() == Guid.Empty ? Guid.NewGuid() : dto.Id!.Value,
				UserId = userId,
				Workplace = dto.Workplace?.Trim() ?? string.Empty,
				PayRates = dto.PayRates == null ? null! : [.. dto.PayRates],
				Profiles = dto.Profiles == null ? null : CopyProfiles(dto.Profiles),
				HasProfiles = dto.Profiles != null
			};
			workInfo.Validate();
			if (workInfo.Profiles != null) workInfo.PayRates = ProjectRates(workInfo.Profiles);
			return workInfo;
		}

		// POST is additive for offline sync/import; edits and removals use PUT.
		public void MergeFrom(WorkInfoDTO dto)
		{
			var incoming = FromDTO(dto, UserId);
			EnsureSameWorkplace(incoming.Workplace);
			var profiles = EffectiveProfiles();
			if (incoming.Profiles == null)
			{
				foreach (var amount in incoming.PayRates.Distinct())
				{
					if (profiles.SelectMany(profile => profile.PayRates).Any(rate => rate.HourlyRate == amount)) continue;
					var legacyId = $"legacy-{FormatRate(amount)}";
					var idTaken = profiles.SelectMany(profile => profile.PayRates).Any(rate => rate.Id == legacyId);
					var legacy = profiles.FirstOrDefault(profile => profile.Id == "legacy");
					if (legacy == null)
					{
						legacy = new WorkProfile { Id = "legacy" };
						profiles.Add(legacy);
					}
					legacy.PayRates.Add(new NamedPayRate
					{
						Id = idTaken ? Guid.NewGuid().ToString() : legacyId,
						Name = idTaken ? "Custom" : string.Empty,
						HourlyRate = amount
					});
				}
			}
			else foreach (var profile in incoming.Profiles)
			{
				var existing = profiles.FirstOrDefault(item => item.Id == profile.Id);
				if (existing == null)
				{
					profiles.Add(profile);
					continue;
				}

				existing.Position ??= profile.Position;
				existing.Location ??= profile.Location;
				foreach (var rate in profile.PayRates)
				{
					if (existing.PayRates.All(item => item.Id != rate.Id)) existing.PayRates.Add(rate);
				}
			}

			Profiles = profiles;
			HasProfiles = true;
			PayRates = ProjectRates(profiles);
		}

		public void ReplaceProfiles(WorkInfoDTO dto)
		{
			ArgumentNullException.ThrowIfNull(dto);
			if (dto.Profiles == null)
			{
				throw new InvalidOperationException("Profiles is required when replacing work info.");
			}

			var replacement = FromDTO(dto, UserId);
			EnsureSameWorkplace(replacement.Workplace);
			var incomingProfiles = replacement.Profiles!;
			var profiles = Profiles ?? [];
			profiles.RemoveAll(profile => incomingProfiles.All(incoming => incoming.Id != profile.Id));
			foreach (var incoming in incomingProfiles)
			{
				var existing = profiles.FirstOrDefault(profile => profile.Id == incoming.Id);
				if (existing == null)
				{
					profiles.Add(incoming);
					continue;
				}

				// Keep tracked owned instances when IDs match; replace their values, not identities.
				existing.Position = incoming.Position;
				existing.Location = incoming.Location;
				existing.PayRates.RemoveAll(rate => incoming.PayRates.All(item => item.Id != rate.Id));
				foreach (var rate in incoming.PayRates)
				{
					var existingRate = existing.PayRates.FirstOrDefault(item => item.Id == rate.Id);
					if (existingRate == null) existing.PayRates.Add(rate);
					else
					{
						existingRate.Name = rate.Name;
						existingRate.HourlyRate = rate.HourlyRate;
					}
				}
			}

			Profiles = profiles;
			HasProfiles = true;
			PayRates = ProjectRates(Profiles!);
		}

		public void RemoveLegacyPayRate(decimal amount)
		{
			if (!HasProfiles && Profiles is not { Count: > 0 })
			{
				PayRates = (PayRates ?? []).Where(rate => rate != amount).ToList();
				return;
			}

			foreach (var profile in Profiles ?? [])
			{
				profile.PayRates.RemoveAll(rate => rate.HourlyRate == amount &&
					string.IsNullOrWhiteSpace(rate.Name) && IsLegacyRateId(rate.Id));
			}
			PayRates = ProjectRates(Profiles ?? []);
		}

		private List<WorkProfile> EffectiveProfiles() => HasProfiles || Profiles is { Count: > 0 }
			? Profiles ?? [] : LegacyProfiles(PayRates ?? []);

		private void EnsureSameWorkplace(string workplace)
		{
			if (!string.Equals(Workplace, workplace, StringComparison.Ordinal))
			{
				throw new InvalidOperationException("Workplace cannot be changed. Create a new workplace instead.");
			}
		}

		private static bool ValidId(string? id) => !string.IsNullOrWhiteSpace(id) && id.Length <= 100 && id == id.Trim();

		private static string FormatRate(decimal rate) => rate.ToString("0.############################", CultureInfo.InvariantCulture);

		private static bool IsLegacyRateId(string id) => id.StartsWith("legacy-", StringComparison.Ordinal) &&
			decimal.TryParse(id.AsSpan(7), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount) &&
			amount >= 0 && id == $"legacy-{FormatRate(amount)}";

		private static List<WorkProfile> LegacyProfiles(IEnumerable<decimal> rates) =>
		[
			new WorkProfile
			{
				Id = "legacy",
				PayRates = rates.Distinct().Select(rate => new NamedPayRate
				{
					Id = $"legacy-{FormatRate(rate)}", Name = string.Empty, HourlyRate = rate
				}).ToList()
			}
		];

		private static List<decimal> ProjectRates(IEnumerable<WorkProfile> profiles) =>
			profiles.SelectMany(profile => profile.PayRates).Select(rate => rate.HourlyRate).Distinct().ToList();

		private static List<WorkProfile> CopyProfiles(IEnumerable<WorkProfile> profiles) => profiles.Select(profile =>
		{
			if (profile == null) throw new InvalidOperationException("Profiles cannot contain null entries.");
			return new WorkProfile
			{
				Id = profile.Id,
				Position = OptionalText(profile.Position),
				Location = OptionalText(profile.Location),
				PayRates = profile.PayRates?.Select(rate =>
				{
					if (rate == null) throw new InvalidOperationException("Pay rates cannot contain null entries.");
					return new NamedPayRate { Id = rate.Id, Name = rate.Name?.Trim()!, HourlyRate = rate.HourlyRate };
				}).ToList()!
			};
		}).ToList();

		private static string? OptionalText(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
	}

	public class WorkProfile
	{
		public string Id { get; set; } = string.Empty;
		public string? Position { get; set; }
		public string? Location { get; set; }
		public List<NamedPayRate> PayRates { get; set; } = [];
	}

	public class NamedPayRate
	{
		public string Id { get; set; } = string.Empty;
		public string Name { get; set; } = string.Empty;
		public decimal HourlyRate { get; set; }
	}

	public class WorkInfoDTO
	{
		public Guid? Id { get; set; }
		public required string Workplace { get; set; }
		public List<decimal> PayRates { get; set; } = [];
		public List<WorkProfile>? Profiles { get; set; }
	}
}
