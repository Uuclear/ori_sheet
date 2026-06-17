using RingKnifeDetector.Models;

namespace RingKnifeDetector.Helpers
{
    public static class TextSanitizer
    {
        public static string RemoveChinesePeriods(string? value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Replace("。", string.Empty);
        }

        public static void SanitizeProject(ProjectInfo project)
        {
            project.EntrustNo = RemoveChinesePeriods(project.EntrustNo);
            project.ReportNo = RemoveChinesePeriods(project.ReportNo);
            project.EntrustUnit = RemoveChinesePeriods(project.EntrustUnit);
            project.Contact = RemoveChinesePeriods(project.Contact);
            project.SupervisionUnit = RemoveChinesePeriods(project.SupervisionUnit);
            project.ConstructionUnit = RemoveChinesePeriods(project.ConstructionUnit);
            project.ProjectName = RemoveChinesePeriods(project.ProjectName);
            project.UnitAddress = RemoveChinesePeriods(project.UnitAddress);
            project.ProjectAddress = RemoveChinesePeriods(project.ProjectAddress);
            project.EntrustDate = RemoveChinesePeriods(project.EntrustDate);
            project.ProjectSection = RemoveChinesePeriods(project.ProjectSection);
            project.ReportDate = RemoveChinesePeriods(project.ReportDate);
            project.TestNature = RemoveChinesePeriods(project.TestNature);
        }

        public static void SanitizeParams(RecordParams parameters)
        {
            parameters.SoilType = RemoveChinesePeriods(parameters.SoilType);
            parameters.CompactionMethod = RemoveChinesePeriods(parameters.CompactionMethod);
            parameters.RingSpec = RemoveChinesePeriods(parameters.RingSpec);
            parameters.SampleName = RemoveChinesePeriods(parameters.SampleName);
            parameters.MaterialType = RemoveChinesePeriods(parameters.MaterialType);
            parameters.TestBasis = RemoveChinesePeriods(parameters.TestBasis);
            parameters.JudgeBasis = RemoveChinesePeriods(parameters.JudgeBasis);
            parameters.TestLocation = RemoveChinesePeriods(parameters.TestLocation);
            parameters.LimisRemark = RemoveChinesePeriods(parameters.LimisRemark);
            parameters.DesignRequirementText = RemoveChinesePeriods(parameters.DesignRequirementText);
            parameters.WitnessUnit = RemoveChinesePeriods(parameters.WitnessUnit);
            parameters.WitnessPerson = RemoveChinesePeriods(parameters.WitnessPerson);
            parameters.SamplingUnit = RemoveChinesePeriods(parameters.SamplingUnit);
            parameters.SamplingPerson = RemoveChinesePeriods(parameters.SamplingPerson);
        }
    }
}
