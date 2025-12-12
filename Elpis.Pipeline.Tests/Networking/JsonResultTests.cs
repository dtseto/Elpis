using PandoraSharp;
using Util;
using Xunit;

namespace Elpis.Pipeline.Tests.Networking
{
    public class JsonResultTests
    {
        [Fact]
        public void Result_exposes_payload_when_stat_ok()
        {
            var json = @"{ ""stat"": ""ok"", ""result"": { ""token"": ""abc123"" } }";

            var result = new JSONResult(json);

            Assert.False(result.Fault);
            Assert.Equal("abc123", result.Result["token"].ToString());
        }

        [Fact]
        public void FaultObject_maps_error_code_and_message()
        {
            var json = @"
            {
                ""stat"": ""fail"",
                ""code"": 9,
                ""message"": ""missing"",
                ""result"": null
            }";

            var result = new JSONResult(json);

            Assert.True(result.Fault);
            Assert.Equal(ErrorCodes.PARAMETER_MISSING, result.FaultCode);
            Assert.StartsWith("[ERROR CODE 9]", result.FaultString);
            Assert.Contains("Elpis requires an update", result.FaultString);

            var fault = result.FaultObject;
            Assert.Equal(ErrorCodes.PARAMETER_MISSING, fault.Error);
            Assert.Equal(result.FaultString, fault.FaultString);
        }
    }
}
