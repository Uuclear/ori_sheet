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

        public static string TrimEdges(string? value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Trim().Trim('\t');
        }

        public static void SanitizeProject(ProjectInfo project)
        {
            project.EntrustNo = TrimEdges(RemoveChinesePeriods(project.EntrustNo));
            project.ReportNo = TrimEdges(RemoveChinesePeriods(project.ReportNo));
            project.EntrustUnit = TrimEdges(RemoveChinesePeriods(project.EntrustUnit));
            project.Contact = TrimEdges(RemoveChinesePeriods(project.Contact));
            project.SupervisionUnit = TrimEdges(RemoveChinesePeriods(project.SupervisionUnit));
            project.ConstructionUnit = TrimEdges(RemoveChinesePeriods(project.ConstructionUnit));
            project.ProjectName = TrimEdges(RemoveChinesePeriods(project.ProjectName));
            project.UnitAddress = TrimEdges(RemoveChinesePeriods(project.UnitAddress));
            project.ProjectAddress = TrimEdges(RemoveChinesePeriods(project.ProjectAddress));
            project.EntrustDate = TrimEdges(RemoveChinesePeriods(project.EntrustDate));
            project.ProjectSection = TrimEdges(RemoveChinesePeriods(project.ProjectSection));
            project.ReportDate = TrimEdges(RemoveChinesePeriods(project.ReportDate));
            project.TestNature = TrimEdges(RemoveChinesePeriods(project.TestNature));
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
