using ShiftPay_Backend.Models;

namespace ShiftPay_Backend.Tests;

public class WorkSnapshotModelTests
{
	[Fact]
	public void Shift_Roundtrip_PreservesSelectedWorkDetailsAndNumericRate()
	{
		var dto = new ShiftDTO
		{
			Workplace = "Hospital",
			PayRate = 35m,
			WorkProfileId = "6b6b7284-5591-4fcb-8641-c8347775b10a",
			PayRateId = "5bbf7cd9-4eb5-43bd-8427-cf8222dcbd16",
			Position = "Nurse",
			Location = "City campus",
			PayRateName = "Overnight",
			StartTime = new DateTime(2026, 10, 30, 21, 0, 0),
			EndTime = new DateTime(2026, 10, 31, 5, 0, 0),
			UnpaidBreaks = []
		};

		var saved = Shift.FromDTO(dto, "user-a").ToDTO();

		Assert.Equal(dto.PayRate, saved.PayRate);
		Assert.Equal(dto.WorkProfileId, saved.WorkProfileId);
		Assert.Equal(dto.PayRateId, saved.PayRateId);
		Assert.Equal(dto.Position, saved.Position);
		Assert.Equal(dto.Location, saved.Location);
		Assert.Equal(dto.PayRateName, saved.PayRateName);
	}

	[Fact]
	public void Template_Roundtrip_PreservesSelectedWorkDetailsAndNumericRate()
	{
		var dto = new ShiftTemplateDTO
		{
			TemplateName = "Overnight",
			Workplace = "Hospital",
			PayRate = 35m,
			WorkProfileId = "6b6b7284-5591-4fcb-8641-c8347775b10a",
			PayRateId = "5bbf7cd9-4eb5-43bd-8427-cf8222dcbd16",
			Position = "Nurse",
			Location = "City campus",
			PayRateName = "Overnight",
			StartTime = new DateTime(2026, 10, 30, 21, 0, 0),
			EndTime = new DateTime(2026, 10, 31, 5, 0, 0),
			UnpaidBreaks = []
		};

		var saved = ShiftTemplate.FromDTO(dto, "user-a").ToDTO();

		Assert.Equal(dto.PayRate, saved.PayRate);
		Assert.Equal(dto.WorkProfileId, saved.WorkProfileId);
		Assert.Equal(dto.PayRateId, saved.PayRateId);
		Assert.Equal(dto.Position, saved.Position);
		Assert.Equal(dto.Location, saved.Location);
		Assert.Equal(dto.PayRateName, saved.PayRateName);
	}
}
