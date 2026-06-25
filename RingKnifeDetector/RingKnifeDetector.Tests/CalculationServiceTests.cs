using RingKnifeDetector.Models;
using RingKnifeDetector.Services;

namespace RingKnifeDetector.Tests;

public class CalculationServiceTests
{
    private readonly CalculationService _service = new();

    [Fact]
    public void CalculateMoisture_WithValidData_ReturnsCorrectResult()
    {
        var result = _service.CalculateMoisture(10, 25, 20);
        Assert.Equal(50.00m, result);
    }

    [Fact]
    public void CalculateMoisture_WithNullValues_ReturnsNull()
    {
        Assert.Null(_service.CalculateMoisture(null, 25, 20));
        Assert.Null(_service.CalculateMoisture(10, null, 20));
        Assert.Null(_service.CalculateMoisture(10, 25, null));
    }

    [Fact]
    public void CalculateMoisture_WithInvalidData_ReturnsNull()
    {
        Assert.Null(_service.CalculateMoisture(20, 25, 20));
        Assert.Null(_service.CalculateMoisture(10, 15, 20));
    }

    [Fact]
    public void CalculateRing_WithValidData_ReturnsCorrectResult()
    {
        var ring = new RingMeasurement
        {
            RingSampleMass = 400m,
            RingMass = 200m,
            RingVolume = 200m,
            Boxes = new List<AluminumBox>
            {
                new() { BoxMass = 10m, WetSampleMass = 25m, DrySampleMass = 20m },
                new() { BoxMass = 10m, WetSampleMass = 30m, DrySampleMass = 22m }
            }
        };

        var result = _service.CalculateRing(ring);

        Assert.NotNull(result.WetMass);
        Assert.Equal(200m, result.WetMass);
        Assert.NotNull(result.WetDensity);
        Assert.Equal(1.00m, result.WetDensity);
        Assert.NotNull(result.AvgMoisture);
        Assert.NotNull(result.DryDensity);
    }

    [Fact]
    public void CalculatePoint_WithValidData_ReturnsCorrectResult()
    {
        var sample = new RingKnifeSample
        {
            SampleNo = "1",
            Elevation = "100m",
            SamplingDate = "2024-01-01",
            TestDate = "2024-01-01",
            Thickness = "30cm",
            Rings = new List<RingMeasurement>
            {
                new()
                {
                    RingSampleMass = 400m,
                    RingMass = 200m,
                    RingVolume = 200m,
                    Boxes = new List<AluminumBox>
                    {
                        new() { BoxMass = 10m, WetSampleMass = 25m, DrySampleMass = 20m },
                        new() { BoxMass = 10m, WetSampleMass = 30m, DrySampleMass = 22m }
                    }
                }
            }
        };

        var parameters = new RecordParams
        {
            MaxDryDensity = 1.92m,
            DesignRequirement = 0.93m,
            ResultType = "compaction_coeff"
        };

        var result = _service.CalculatePoint(sample, parameters);

        Assert.Equal("1", result.SampleNo);
        Assert.Equal("100m", result.Elevation);
        Assert.NotNull(result.AvgDryDensity);
        Assert.NotNull(result.CompactionCoeff);
        Assert.False(string.IsNullOrEmpty(result.Conclusion));
    }

    [Fact]
    public void CalculateAll_WithValidData_ReturnsCorrectResult()
    {
        var request = new CalcRequest
        {
            Params = new RecordParams
            {
                MaxDryDensity = 1.92m,
                DesignRequirement = 0.93m,
                ResultType = "compaction_coeff"
            },
            Samples = new List<RingKnifeSample>
            {
                new()
                {
                    SampleNo = "1",
                    Rings = new List<RingMeasurement>
                    {
                        new()
                        {
                            RingSampleMass = 400m,
                            RingMass = 200m,
                            RingVolume = 200m,
                            Boxes = new List<AluminumBox>
                            {
                                new() { BoxMass = 10m, WetSampleMass = 25m, DrySampleMass = 20m },
                                new() { BoxMass = 10m, WetSampleMass = 30m, DrySampleMass = 22m }
                            }
                        }
                    }
                }
            }
        };

        var response = _service.CalculateAll(request);

        Assert.Single(response.Results);
        Assert.False(string.IsNullOrEmpty(response.OverallConclusion));
    }

    [Fact]
    public void CalculateAll_Group3_RequiresAllRingsToPass()
    {
        var ringTemplate = new RingMeasurement
        {
            RingSampleMass = 596m,
            RingMass = 176m,
            RingVolume = 200m,
            Boxes = new List<AluminumBox>
            {
                new() { BoxMass = 10m, WetSampleMass = 21m, DrySampleMass = 20m },
                new() { BoxMass = 10m, WetSampleMass = 21m, DrySampleMass = 20m }
            }
        };

        var request = new CalcRequest
        {
            Params = new RecordParams
            {
                MaxDryDensity = 1.92m,
                DesignRequirement = 0.93m,
                ResultType = "compaction_coeff",
                RecordTemplate = "group3"
            },
            Samples = new List<RingKnifeSample>
            {
                new()
                {
                    SampleNo = "1",
                    Rings = new List<RingMeasurement> { ringTemplate, ringTemplate, ringTemplate }
                }
            }
        };

        var response = _service.CalculateAll(request);
        var sample = response.Results[0];

        Assert.Equal(3, sample.Rings.Count);
        Assert.All(sample.Rings, ring => Assert.Equal("符合设计要求", ring.Conclusion));
        Assert.Contains("符合设计要求", response.OverallConclusion);
    }
}