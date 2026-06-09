from __future__ import annotations



from decimal import ROUND_HALF_EVEN, Decimal



from ring_knife.schemas.models import (

    CalcRequest,

    CalcResponse,

    RecordParams,

    RingKnifeSample,

    RingMeasurement,

    RingPointResult,

    SamplePointResult,

)



DECIMAL_PLACES = 2





def _to_decimal(value: float | None) -> Decimal | None:

    if value is None:

        return None

    return Decimal(str(value))





def _round2(value: Decimal | None) -> float | None:

    if value is None:

        return None

    quant = Decimal("0.01")

    return float(value.quantize(quant, rounding=ROUND_HALF_EVEN))





def _avg(values: list[float | None]) -> float | None:

    nums = [v for v in values if v is not None]

    if not nums:

        return None

    return _round2(Decimal(str(sum(nums))) / Decimal(len(nums)))





def calc_moisture(box_mass: float | None, wet_mass: float | None, dry_mass: float | None) -> float | None:

    bm = _to_decimal(box_mass)

    wm = _to_decimal(wet_mass)

    dm = _to_decimal(dry_mass)

    if bm is None or wm is None or dm is None:

        return None

    dry_soil = dm - bm

    if dry_soil <= 0:

        return None

    wet_soil = wm - bm

    if wet_soil <= dry_soil:

        return None

    rate = (wet_soil - dry_soil) / dry_soil * Decimal(100)

    return _round2(rate)





def _rings_for_sample(sample: RingKnifeSample) -> list[RingMeasurement]:

    if sample.rings:

        return sample.rings

    return [

        RingMeasurement(

            ring_sample_mass=sample.ring_sample_mass,

            ring_mass=sample.ring_mass,

            ring_volume=sample.ring_volume or 200,

            boxes=sample.boxes,

        )

    ]





def _calculate_ring(ring: RingMeasurement) -> RingPointResult:

    ring_sample = _to_decimal(ring.ring_sample_mass)

    ring_mass = _to_decimal(ring.ring_mass)

    ring_volume = _to_decimal(ring.ring_volume)



    wet_mass: Decimal | None = None

    wet_density: Decimal | None = None

    if ring_sample is not None and ring_mass is not None:

        wet_mass = ring_sample - ring_mass

    if wet_mass is not None and ring_volume is not None and ring_volume > 0:

        wet_density = wet_mass / ring_volume



    moisture_rates: list[float | None] = []

    for box in ring.boxes[:2]:

        moisture_rates.append(calc_moisture(box.box_mass, box.wet_sample_mass, box.dry_sample_mass))



    avg_moisture = _avg(moisture_rates)



    dry_density: float | None = None

    if wet_density is not None and avg_moisture is not None:

        factor = Decimal(1) + Decimal(str(avg_moisture)) / Decimal(100)

        if factor > 0:

            dry_density = _round2(wet_density / factor)



    return RingPointResult(

        wet_mass=_round2(wet_mass),

        wet_density=_round2(wet_density),

        moisture_rates=moisture_rates,

        avg_moisture=avg_moisture,

        dry_density=dry_density,

    )





def calculate_point(sample: RingKnifeSample, params: RecordParams) -> SamplePointResult:

    ring_results = [_calculate_ring(ring) for ring in _rings_for_sample(sample)]



    wet_densities = [r.wet_density for r in ring_results]

    dry_densities = [r.dry_density for r in ring_results]

    moisture_avgs = [r.avg_moisture for r in ring_results]

    all_moisture_rates: list[float | None] = []

    for r in ring_results:

        all_moisture_rates.extend(r.moisture_rates)



    avg_wet_density = _avg(wet_densities)

    avg_moisture = _avg(moisture_avgs)

    avg_dry_density = _avg(dry_densities)



    first = ring_results[0] if ring_results else RingPointResult()

    compaction_coeff: float | None = None

    compaction_percent: float | None = None

    conclusion = ""



    if avg_dry_density is not None and params.max_dry_density:

        max_dd = Decimal(str(params.max_dry_density))

        if max_dd > 0:

            coeff = Decimal(str(avg_dry_density)) / max_dd

            compaction_coeff = _round2(coeff)

            compaction_percent = _round2(coeff * Decimal(100))



    if params.design_requirement is not None and compaction_coeff is not None:

        if params.result_type == "compaction_percent":

            target = params.design_requirement

            actual = compaction_percent

        else:

            target = params.design_requirement

            actual = compaction_coeff

        if actual is not None and actual >= target:

            conclusion = "符合设计要求"

        else:

            conclusion = "不符合设计要求"



    return SamplePointResult(

        sample_no=sample.sample_no,

        elevation=sample.elevation,

        thickness=sample.thickness,

        sampling_date=sample.sampling_date,

        test_date=sample.test_date,

        wet_mass=first.wet_mass,

        wet_density=first.wet_density,

        avg_wet_density=avg_wet_density,

        moisture_rates=all_moisture_rates,

        avg_moisture=avg_moisture,

        dry_density=first.dry_density,

        avg_dry_density=avg_dry_density,

        compaction_coeff=compaction_coeff,

        compaction_percent=compaction_percent,

        conclusion=conclusion,

        rings=ring_results,

    )





def calculate_all(request: CalcRequest) -> CalcResponse:

    results = [calculate_point(s, request.params) for s in request.samples]

    conclusions = [r.conclusion for r in results if r.conclusion]

    if not conclusions:

        overall = ""

    elif all(c == "符合设计要求" for c in conclusions):

        if request.params.result_type == "compaction_percent":

            overall = "所检样品压实度符合设计要求。"

        else:

            overall = "所检样品压实系数符合设计要求。"

    else:

        if request.params.result_type == "compaction_percent":

            overall = "所检样品压实度不符合设计要求。"

        else:

            overall = "所检样品压实系数不符合设计要求。"

    return CalcResponse(results=results, overall_conclusion=overall)

