using System;
using FluentValidation;
using NzbDrone.Core.Annotations;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Validation;

namespace NzbDrone.Core.CustomFormats
{
    public class ProtocolSpecificationValidator : AbstractValidator<ProtocolSpecification>
    {
        public ProtocolSpecificationValidator()
        {
            RuleFor(c => c.Value).Custom((value, context) =>
            {
                if (!Enum.IsDefined(typeof(DownloadProtocol), value) || (DownloadProtocol)value == DownloadProtocol.Unknown)
                {
                    context.AddFailure($"Invalid protocol condition value: {value}");
                }
            });
        }
    }

    public class ProtocolSpecification : CustomFormatSpecificationBase
    {
        private static readonly ProtocolSpecificationValidator Validator = new();

        public override int Order => 6;
        public override string ImplementationName => "Protocol";

        [FieldDefinition(1, Label = "Protocol", Type = FieldType.Select, SelectOptions = typeof(DownloadProtocol))]
        public int Value { get; set; }

        protected override bool IsSatisfiedByWithoutNegate(CustomFormatInput input)
        {
            return input.DownloadProtocol.HasValue && input.DownloadProtocol.Value == (DownloadProtocol)Value;
        }

        public override NzbDroneValidationResult Validate()
        {
            return new NzbDroneValidationResult(Validator.Validate(this));
        }
    }
}
