from ring_knife.calc.ring_knife import calc_moisture, calculate_point
from ring_knife.schemas.models import AluminumBox, RecordParams, RingKnifeSample


def test_moisture():
    rate = calc_moisture(15.2, 45.6, 42.1)
    assert rate == 13.01


def test_calculate_point():
    sample = RingKnifeSample(
        ring_sample_mass=432.5,
        ring_mass=200.0,
        ring_volume=200.0,
        boxes=[
            AluminumBox(box_mass=15.2, wet_sample_mass=45.6, dry_sample_mass=42.1),
            AluminumBox(box_mass=15.0, wet_sample_mass=44.8, dry_sample_mass=41.5),
        ],
    )
    params = RecordParams(max_dry_density=1.85, design_requirement=0.93)
    result = calculate_point(sample, params)
    assert result.wet_mass == 232.5
    assert result.wet_density == 1.16
    assert result.avg_moisture == 12.73
    assert result.dry_density == 1.03
    assert result.compaction_coeff == 0.56
    assert result.conclusion == "不符合设计要求"
