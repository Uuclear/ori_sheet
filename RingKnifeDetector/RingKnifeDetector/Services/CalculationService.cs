using RingKnifeDetector.Models;

namespace RingKnifeDetector.Services
{
    /// <summary>
    /// 计算服务
    /// </summary>
    public class CalculationService
    {
        private const int DecimalPlaces = 2;

        /// <summary>
        /// 计算含水率
        /// </summary>
        /// <param name="boxMass">铝盒质量</param>
        /// <param name="wetMass">湿样质量</param>
        /// <param name="dryMass">干样质量</param>
        /// <returns>含水率（百分比）</returns>
        public decimal? CalculateMoisture(decimal? boxMass, decimal? wetMass, decimal? dryMass)
        {
            if (boxMass == null || wetMass == null || dryMass == null)
                return null;

            var drySoil = dryMass.Value - boxMass.Value;
            if (drySoil <= 0)
                return null;

            var wetSoil = wetMass.Value - boxMass.Value;
            if (wetSoil <= drySoil)
                return null;

            var rate = (wetSoil - drySoil) / drySoil * 100;
            return Math.Round(rate, DecimalPlaces, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// 计算单个环刀结果
        /// </summary>
        /// <param name="ring">环刀测量数据</param>
        /// <returns>环刀计算结果</returns>
        public RingPointResult CalculateRing(RingMeasurement ring)
        {
            decimal? wetMass = null;
            decimal? wetDensity = null;

            if (ring.RingSampleMass != null && ring.RingMass != null)
            {
                wetMass = ring.RingSampleMass.Value - ring.RingMass.Value;
            }

            if (wetMass != null && ring.RingVolume != null && ring.RingVolume > 0)
            {
                wetDensity = wetMass.Value / ring.RingVolume.Value;
            }

            // 计算含水率（取前两个铝盒）
            var moistureRates = new List<decimal?>();
            foreach (var box in ring.Boxes.Take(2))
            {
                moistureRates.Add(CalculateMoisture(box.BoxMass, box.WetSampleMass, box.DrySampleMass));
            }

            var avgMoisture = CalculateAverage(moistureRates);

            decimal? dryDensity = null;
            if (wetDensity != null && avgMoisture != null)
            {
                var factor = 1 + avgMoisture.Value / 100;
                if (factor > 0)
                {
                    dryDensity = Math.Round(wetDensity.Value / factor, DecimalPlaces, MidpointRounding.AwayFromZero);
                }
            }

            return new RingPointResult
            {
                WetMass = wetMass != null ? Math.Round(wetMass.Value, DecimalPlaces, MidpointRounding.AwayFromZero) : null,
                WetDensity = wetDensity != null ? Math.Round(wetDensity.Value, DecimalPlaces, MidpointRounding.AwayFromZero) : null,
                MoistureRates = moistureRates,
                AvgMoisture = avgMoisture,
                DryDensity = dryDensity
            };
        }

        /// <summary>
        /// 计算单个测点结果
        /// </summary>
        /// <param name="sample">环刀样品数据</param>
        /// <param name="params">检测参数</param>
        /// <returns>测点计算结果</returns>
        public SamplePointResult CalculatePoint(RingKnifeSample sample, RecordParams @params)
        {
            var rings = GetRingsForSample(sample);
            var ringResults = rings.Select(CalculateRing).ToList();

            var wetDensities = ringResults.Select(r => r.WetDensity).ToList();
            var dryDensities = ringResults.Select(r => r.DryDensity).ToList();
            var moistureAvgs = ringResults.Select(r => r.AvgMoisture).ToList();

            var allMoistureRates = new List<decimal?>();
            foreach (var r in ringResults)
            {
                allMoistureRates.AddRange(r.MoistureRates);
            }

            var avgWetDensity = CalculateAverage(wetDensities);
            var avgMoisture = CalculateAverage(moistureAvgs);
            var avgDryDensity = CalculateAverage(dryDensities);

            var first = ringResults.FirstOrDefault() ?? new RingPointResult();

            decimal? compactionCoeff = null;
            decimal? compactionPercent = null;
            var conclusion = string.Empty;

            if (avgDryDensity != null && @params.MaxDryDensity != null && @params.MaxDryDensity > 0)
            {
                var coeff = avgDryDensity.Value / @params.MaxDryDensity.Value;
                compactionCoeff = Math.Round(coeff, DecimalPlaces, MidpointRounding.AwayFromZero);
                compactionPercent = Math.Round(coeff * 100, DecimalPlaces, MidpointRounding.AwayFromZero);
            }

            if (@params.DesignRequirement != null && compactionCoeff != null)
            {
                decimal? target;
                decimal? actual;

                if (@params.ResultType == "compaction_percent")
                {
                    target = @params.DesignRequirement;
                    actual = compactionPercent;
                }
                else
                {
                    target = @params.DesignRequirement;
                    actual = compactionCoeff;
                }

                if (actual != null && actual >= target)
                {
                    conclusion = "符合设计要求";
                }
                else
                {
                    conclusion = "不符合设计要求";
                }
            }

            return new SamplePointResult
            {
                SampleNo = sample.SampleNo,
                Elevation = sample.Elevation,
                Thickness = sample.Thickness,
                SamplingDate = sample.SamplingDate,
                TestDate = sample.TestDate,
                WetMass = first.WetMass,
                WetDensity = first.WetDensity,
                AvgWetDensity = avgWetDensity,
                MoistureRates = allMoistureRates,
                AvgMoisture = avgMoisture,
                DryDensity = first.DryDensity,
                AvgDryDensity = avgDryDensity,
                CompactionCoeff = compactionCoeff,
                CompactionPercent = compactionPercent,
                Conclusion = conclusion,
                Rings = ringResults
            };
        }

        /// <summary>
        /// 计算所有测点结果
        /// </summary>
        /// <param name="request">计算请求</param>
        /// <returns>计算响应</returns>
        public CalcResponse CalculateAll(CalcRequest request)
        {
            var results = request.Samples.Select(s => CalculatePoint(s, request.Params)).ToList();
            var conclusions = results.Where(r => !string.IsNullOrEmpty(r.Conclusion)).Select(r => r.Conclusion).ToList();

            string overall;
            if (!conclusions.Any())
            {
                overall = string.Empty;
            }
            else if (conclusions.All(c => c == "符合设计要求"))
            {
                if (request.Params.ResultType == "compaction_percent")
                {
                    overall = "所检样品压实度符合设计要求。";
                }
                else
                {
                    overall = "所检样品压实系数符合设计要求。";
                }
            }
            else
            {
                if (request.Params.ResultType == "compaction_percent")
                {
                    overall = "所检样品压实度不符合设计要求。";
                }
                else
                {
                    overall = "所检样品压实系数不符合设计要求。";
                }
            }

            return new CalcResponse
            {
                Results = results,
                OverallConclusion = overall
            };
        }

        /// <summary>
        /// 获取样品的环刀测量列表
        /// </summary>
        private List<RingMeasurement> GetRingsForSample(RingKnifeSample sample)
        {
            if (sample.Rings.Any())
            {
                return sample.Rings;
            }

            return new List<RingMeasurement>
            {
                new RingMeasurement
                {
                    RingSampleMass = sample.RingSampleMass,
                    RingMass = sample.RingMass,
                    RingVolume = sample.RingVolume ?? 200,
                    Boxes = sample.Boxes
                }
            };
        }

        /// <summary>
        /// 计算平均值
        /// </summary>
        private decimal? CalculateAverage(List<decimal?> values)
        {
            var validValues = values.Where(v => v != null).ToList();
            if (!validValues.Any())
                return null;

            var sum = validValues.Sum(v => v!.Value);
            var avg = sum / validValues.Count;
            return Math.Round(avg, DecimalPlaces, MidpointRounding.AwayFromZero);
        }
    }
}