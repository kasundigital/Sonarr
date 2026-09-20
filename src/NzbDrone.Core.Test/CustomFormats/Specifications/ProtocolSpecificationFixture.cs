using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Validation;

namespace NzbDrone.Core.Test.CustomFormats.Specifications
{
    [TestFixture]
    public class ProtocolSpecificationFixture
    {
        [TestCase(DownloadProtocol.Usenet, DownloadProtocol.Usenet, true)]
        [TestCase(DownloadProtocol.Usenet, DownloadProtocol.Torrent, false)]
        [TestCase(DownloadProtocol.Torrent, DownloadProtocol.Torrent, true)]
        [TestCase(DownloadProtocol.Torrent, DownloadProtocol.Usenet, false)]
        public void should_match_selected_protocol(DownloadProtocol configured, DownloadProtocol actual, bool expected)
        {
            var subject = new ProtocolSpecification { Value = (int)configured };

            subject.IsSatisfiedBy(new CustomFormatInput { DownloadProtocol = actual })
                .Should().Be(expected);
        }

        [Test]
        public void should_not_match_when_protocol_is_unknown_for_local_file()
        {
            var subject = new ProtocolSpecification { Value = (int)DownloadProtocol.Usenet };

            subject.IsSatisfiedBy(new CustomFormatInput())
                .Should().BeFalse();
        }

        [Test]
        public void should_reject_unknown_protocol_configuration()
        {
            var subject = new ProtocolSpecification { Value = (int)DownloadProtocol.Unknown };

            NzbDroneValidationResult result = subject.Validate();

            result.IsValid.Should().BeFalse();
        }
    }
}
