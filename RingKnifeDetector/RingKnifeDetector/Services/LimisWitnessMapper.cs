using RingKnifeDetector.Helpers;
using RingKnifeDetector.Models;

namespace RingKnifeDetector.Services
{
    public sealed class WitnessSamplingFields
    {
        public string SupervisionWitness { get; set; } = string.Empty;
        public string SampleSampling { get; set; } = string.Empty;
        public string Contact { get; set; } = string.Empty;
        public string SampleName { get; set; } = string.Empty;
        public string TypeSpecification { get; set; } = string.Empty;
        public string TestBasis { get; set; } = string.Empty;
    }

    /// <summary>
    /// 见证送样委托的 LIMIS 字段映射（监理单位→工程见证，施工单位→样品取样）。
    /// </summary>
    internal static class LimisWitnessMapper
    {
        public static WitnessSamplingFields Map(
            ProjectInfo project,
            Dictionary<string, object>? orderRow,
            Dictionary<string, object>? taskRow,
            Dictionary<string, object>? reportRow)
        {
            var fields = new WitnessSamplingFields();
            if (!TestNatureHelper.IsWitnessSampling(project.TestNature))
                return fields;

            var rows = new[] { orderRow, taskRow, reportRow };
            fields.SupervisionWitness = FormatParty(
                Pick(rows,
                    "witnessUnitName", "witnessUnit", "supervisorWitnessUnit", "supervisorWitness",
                    "witnessName", "witnessPersonName", "witnessPerson", "witnessMan",
                    "jlUnitName", "supervisorUnitName", "supervisionUnitName"),
                Pick(rows,
                    "witnessPostName", "witnessPost", "witnessPersonPost", "witnessManPost"));

            fields.SampleSampling = FormatParty(
                Pick(rows,
                    "samplingUnitName", "samplingUnit", "sampleSamplingUnit", "qyUnitName",
                    "samplerUnitName", "takeSampleUnitName", "sgUnitName", "constructionUnitName"),
                Pick(rows,
                    "samplingPersonName", "samplingPerson", "samplerName", "takeSamplePerson",
                    "sampler", "qyPersonName", "samplePersonName"));

            fields.Contact = FormatContact(
                Pick(rows,
                    "witnessLinkMan", "witnessContact", "witnessPostNo", "witnessMan",
                    "samplingLinkMan", "samplingContact", "samplingPostNo", "samplerName",
                    "orderLinkMan", "linkMan", "linkManName", "clientLinkMan", "clientPostNo",
                    "contactPerson", "contactName", "entrustLinkMan"),
                Pick(rows,
                    "witnessTel", "witnessPhone", "witnessMobile", "samplingTel", "samplingPhone",
                    "samplerTel", "orderLinkTel", "linkTel", "linkManTel", "clientTel",
                    "contactTel", "contactPhone", "entrustTel", "mobilePhone"));

            fields.SampleName = Pick(rows,
                "sampleName", "SampleName", "productName", "sampleDesc", "specimenName");

            fields.TypeSpecification = Pick(rows,
                "typeSpecification", "TypeSpecification", "specModel", "modelSpecification",
                "sampleSpecification", "specificationModel", "productSpec");

            fields.TestBasis = TestBasisNormalizer.Normalize(Pick(rows,
                "testBasisName", "TestBasisName", "testingStandard", "testingStandardName",
                "standardName", "testStandard", "testingBasis", "basisName", "testBasis",
                "standardCodeName", "jcyj"));

            return fields;
        }

        public static void MergeHtml(WitnessSamplingFields target, WitnessSamplingFields html)
        {
            MergeField(html.SupervisionWitness, v => target.SupervisionWitness = v);
            MergeField(html.SampleSampling, v => target.SampleSampling = v);
            MergeField(html.Contact, v => target.Contact = v);
            MergeField(html.SampleName, v => target.SampleName = v);
            MergeField(html.TypeSpecification, v => target.TypeSpecification = v);
            MergeField(html.TestBasis, v => target.TestBasis = v);
        }

        public static void ApplyToProject(ProjectInfo project, WitnessSamplingFields fields)
        {
            if (!TestNatureHelper.IsWitnessSampling(project.TestNature))
                return;

            if (!string.IsNullOrWhiteSpace(fields.SupervisionWitness))
                project.SupervisionUnit = fields.SupervisionWitness;
            if (!string.IsNullOrWhiteSpace(fields.SampleSampling))
                project.ConstructionUnit = fields.SampleSampling;
            if (!string.IsNullOrWhiteSpace(fields.Contact))
                project.Contact = fields.Contact;
        }

        private static void MergeField(string value, Action<string> assign)
        {
            if (!string.IsNullOrWhiteSpace(value))
                assign(value.Trim());
        }

        private static string Pick(IEnumerable<Dictionary<string, object>?> rows, params string[] keys)
        {
            foreach (var row in rows)
            {
                if (row == null) continue;
                foreach (var key in keys)
                {
                    if (!row.TryGetValue(key, out var value)) continue;
                    var text = value?.ToString()?.Trim() ?? string.Empty;
                    if (!string.IsNullOrEmpty(text) && text != "/")
                        return text;
                }
            }
            return string.Empty;
        }

        private static string FormatParty(string primary, string secondary)
        {
            primary = primary.Trim();
            secondary = secondary.Trim();
            if (string.IsNullOrEmpty(primary)) return secondary;
            if (string.IsNullOrEmpty(secondary) || primary.Contains(secondary, StringComparison.Ordinal))
                return primary;
            return $"{primary} {secondary}";
        }

        private static string FormatContact(string person, string phone)
        {
            person = person.Trim();
            phone = phone.Trim();
            if (!string.IsNullOrEmpty(person) && !string.IsNullOrEmpty(phone))
                return $"{person} {phone}";
            return !string.IsNullOrEmpty(person) ? person : phone;
        }
    }
}
