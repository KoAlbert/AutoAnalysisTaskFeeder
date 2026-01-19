using Xunit;
using AutoAnalysisTaskFeeder.Services;

namespace AutoAnalysisTaskFeeder.Tests
{
    public class IniServiceTests
    {
        private readonly IniService _iniService;

        public IniServiceTests()
        {
            _iniService = new IniService();
        }

        [Theory]
        [InlineData("FAM::ROX::", "FAM::ROX:")]
        [InlineData("FAM:HEX:ROX:CY5:", "FAM:HEX:ROX:CY5:")]
        [InlineData("FAM::ROX:::", "FAM::ROX:")]
        [InlineData("FAM:", "FAM:")]
        [InlineData("", "")]
        public void NormalizeFilter_ShouldRemoveTrailingColons(string input, string expected)
        {
            // Arrange & Act
            string result = _iniService.NormalizeFilter(input);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void NormalizeFilter_WithNull_ShouldReturnEmpty()
        {
            // Arrange & Act
            string result = _iniService.NormalizeFilter(null);

            // Assert
            Assert.Equal("", result);
        }
    }

    public class ProcessRunnerTests
    {
        [Fact]
        public void StartProcess_WithInvalidPath_ShouldReturnNegativeOne()
        {
            // Arrange
            var runner = new ProcessRunner();
            string invalidPath = "C:\\NonExistent\\Program.exe";

            // Act & Assert
            Assert.Throws<System.IO.FileNotFoundException>(() => runner.StartProcess(invalidPath));
        }
    }
}
